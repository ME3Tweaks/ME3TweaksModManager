using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using ME3TweaksCore.Diagnostics;
using Microsoft.Web.WebView2.Core;
using Newtonsoft.Json.Linq;

namespace ME3TweaksModManager.modmaker.editor
{
    /// <summary>A command the page's title bar asked the host to carry out.</summary>
    public class EditorCommandEventArgs : EventArgs
    {
        public EditorCommandEventArgs(string command, int? index, bool? value, IReadOnlyList<int> ids = null)
        {
            Command = command;
            Index = index;
            Value = value;
            Ids = ids ?? [];
        }

        /// <summary>
        /// What was asked for: "new", "open", "save", "saveAs", "compile", "page",
        /// "requireAllPlayers", "strings".
        /// </summary>
        public string Command { get; }

        /// <summary>The editor page to switch to, for "page". Null for every other command.</summary>
        public int? Index { get; }

        /// <summary>The new setting, for "requireAllPlayers". Null for every other command.</summary>
        public bool? Value { get; }

        /// <summary>The TLK string references to look up, for "strings". Empty for every other command.</summary>
        public IReadOnlyList<int> Ids { get; }
    }

    /// <summary>The edit document a page posted, and the collection it belongs to.</summary>
    public class EditsChangedEventArgs : EventArgs
    {
        public EditsChangedEventArgs(string collection, JObject document)
        {
            Collection = collection;
            Document = document;
        }

        public string Collection { get; }
        public JObject Document { get; }
    }

    /// <summary>
    /// Hosts a ModMaker editor page in WebView2.
    ///
    /// Follows ME3TweaksCoreWPF's MLogViewerControl, which is the established pattern for WebView2 in
    /// this codebase, with two deliberate departures - both noted where they happen:
    ///
    ///  - assets are served straight out of the assembly rather than extracted to a temp folder, so
    ///    nothing is written to disk and path traversal is not something that has to be defended
    ///    against; only names the manifest actually contains can be served at all.
    ///  - the payload is injected with AddScriptToExecuteOnDocumentCreatedAsync rather than by
    ///    ExecuteScriptAsync after a sleep. The sleep is a race: it waits on a duration rather than on
    ///    the document, and a slow machine loses it.
    /// </summary>
    public partial class ModMakerEditorControl : UserControl
    {
        /// <summary>
        /// The origin the page is served from.
        ///
        /// `.invalid` is reserved by RFC 2606 and can never resolve, so if the interception below ever
        /// fails to fire, the request fails closed instead of leaving the machine and reaching
        /// somebody's real server with a ModMaker payload attached.
        /// </summary>
        private const string EditorOrigin = @"https://modmaker.invalid";

        /// <summary>Prefix of the embedded web assets, as the assembly manifest names them.</summary>
        private const string ResourcePrefix = @"ME3TweaksModManager.modmaker.editor.web.";

        /// <summary>
        /// TEMPORARY: where the wavelist stage's character renders are being made.
        ///
        /// They are read off disk rather than out of the assembly because the set is still being
        /// drawn - three enemies of twenty-five so far - and embedding them would mean a rebuild per
        /// picture. When the set is finished it moves into the web bundle and this constant, and the
        /// branch in <see cref="ArtFile"/> that uses it, both go away; the embedded lookup underneath
        /// already works and is what will serve them.
        /// </summary>
        private const string ArtOverrideFolder =
            @"G:\My Drive\Mass Effect Legendary Modding\LE3\ProjectMilan\ModMaker\EnemyImages\Processed";

        /// <summary>The request path the pages fetch character art under.</summary>
        private const string ArtPath = @"/art/";

        /// <summary>
        /// The editor pages' own background, as `--bg` in each page's stylesheet.
        ///
        /// Duplicated here rather than read from the page, because it is needed BEFORE any page has
        /// loaded - that is the entire point of it. Every page shares the one value; if that stops
        /// being true this becomes a per-page property rather than a constant.
        /// </summary>
        private static readonly System.Drawing.Color PageBackground =
            System.Drawing.Color.FromArgb(0xFF, 0x14, 0x17, 0x1C);

        /// <summary>
        /// The one message type any editor page sends. There is deliberately not a type per page: the
        /// host does not parse the document, so a page-specific type would only give the two sides a
        /// second thing to disagree about.
        /// </summary>
        private const string EditMessageType = @"modmaker.edits";

        /// <summary>
        /// The other one: the page's title bar asking the host to do something only the host can do.
        ///
        /// A second type rather than a second channel, because it travels the same way and for the
        /// same reason the edits do - the page owns no project state and decides nothing.
        /// </summary>
        private const string CommandMessageType = @"modmaker.command";

        /// <summary>
        /// Most string references one message may ask the host to resolve.
        ///
        /// The page asks for what one open class shows, which is single digits. The cap is not for
        /// that - it is so a malformed or repeated message cannot turn a preview into a TLK dump.
        /// </summary>
        private const int MaxStringLookups = 512;

        private readonly string _page;
        private readonly string _collectionId;
        private readonly string _payloadJson;
        private readonly string _savedEditsJson;
        private readonly string _chromeJson;

        /// <summary>
        /// True once the page can be scripted into. State pushed before then is dropped rather than
        /// queued: <see cref="PostState"/> is given a WHOLE state every time, so the next push carries
        /// everything a dropped one would have.
        /// </summary>
        private bool _ready;

        /// <summary>
        /// Raised when the page reports a change to the user's edits.
        ///
        /// The page posts on every change rather than behind a save button, because the host owns the
        /// project file and its dirty state; a page sitting on unsent edits loses them if the window
        /// is closed. The argument is the complete edit document, not a delta, so a message that goes
        /// missing costs nothing.
        ///
        /// Raised as JSON rather than as a typed document: this control hosts every editor page, and
        /// which document type a collection deserialises to belongs to the window that owns the
        /// project - not to the thing that owns the WebView2.
        /// </summary>
        public event EventHandler<EditsChangedEventArgs> EditsChanged;

        /// <summary>
        /// Raised when the page's title bar asks for a project command.
        ///
        /// The bar lives in the page because a WPF strip above the WebView2 is a piece of another
        /// application sitting on top of it - it cannot show which editor is current and it shares
        /// none of the page's palette. What it cannot be is in CHARGE: opening a file, saving one and
        /// compiling all belong to the window that owns the project, so the page asks.
        /// </summary>
        public event EventHandler<EditorCommandEventArgs> CommandInvoked;

        /// <param name="page">Page file name, e.g. "wavelist.html".</param>
        /// <param name="collectionId">
        /// The project collection this page edits - "wavelist", "enemy". Sent to the page and checked
        /// on the way back, so a message from the wrong page cannot land in the wrong collection.
        /// </param>
        /// <param name="payloadJson">
        /// The editor's data, already serialised. Passed as text rather than as an object because the
        /// page is the only consumer and the shape belongs to the payload builder, not to this host.
        /// </param>
        /// <param name="savedEditsJson">
        /// Edits already in the project, so reopening the editor shows the user's own work rather than
        /// the shipped waves. Null when there are none.
        /// </param>
        /// <param name="chromeJson">
        /// The title bar's first state, so it is drawn populated rather than appearing empty and
        /// filling in - which would be a flicker on every page switch, and switching pages rebuilds
        /// this control.
        /// </param>
        public ModMakerEditorControl(string page, string collectionId, string payloadJson, string savedEditsJson = null,
                                     string chromeJson = null)
        {
            _page = page ?? throw new ArgumentNullException(nameof(page));
            _collectionId = collectionId ?? throw new ArgumentNullException(nameof(collectionId));
            _payloadJson = payloadJson ?? @"null";
            _savedEditsJson = string.IsNullOrWhiteSpace(savedEditsJson) ? @"null" : savedEditsJson;
            _chromeJson = string.IsNullOrWhiteSpace(chromeJson) ? @"null" : chromeJson;

            InitializeComponent();

            // Painted by the browser itself between navigations, in place of its default white.
            //
            // Switching editor pages tears the whole control down and builds a new one, so there is a
            // window - browser init, then navigation - where nothing has drawn a page yet and
            // something has to fill it. White is the one colour guaranteed to read as a fault on a
            // dark editor. Set before EnsureCoreWebView2Async so it applies to the FIRST paint rather
            // than to the second one.
            webView.DefaultBackgroundColor = PageBackground;

            InitializeAsync();
        }

        private async void InitializeAsync()
        {
            try
            {
                await webView.EnsureCoreWebView2Async(null);

                // Before any page script runs, so the page can read it at top level rather than
                // polling for it. This is the contract wavelist.html expects.
                await webView.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(
                    $@"window.__PAYLOAD__ = {_payloadJson}; window.__EDITS__ = {_savedEditsJson}; " +
                    $@"window.__CHROME__ = {_chromeJson}; " +
                    $@"window.__COLLECTION__ = ""{_collectionId}"";");

                webView.CoreWebView2.WebMessageReceived += OnWebMessageReceived;

                webView.CoreWebView2.AddWebResourceRequestedFilter($@"{EditorOrigin}/*", CoreWebView2WebResourceContext.All);
                webView.CoreWebView2.WebResourceRequested += OnWebResourceRequested;
                webView.CoreWebView2.NewWindowRequested += OnNewWindowRequested;
                webView.NavigationStarting += OnNavigationStarting;

                // The editor is an application surface, not a web page: the context menu and dev tools
                // are off, and there is nothing to zoom.
                webView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
                webView.CoreWebView2.Settings.AreDevToolsEnabled = false;
                webView.CoreWebView2.Settings.IsZoomControlEnabled = false;
                webView.CoreWebView2.Settings.IsStatusBarEnabled = false;

                _ready = true;
                webView.CoreWebView2.Navigate($@"{EditorOrigin}/{_page}");
            }
            catch (Exception e)
            {
                // WebView2's runtime can be missing or blocked. Failing silently would leave a blank
                // window with no indication of why.
                MLog.Error($@"ModMaker editor could not start WebView2: {e.Message}");
                ShowError(e.Message);
            }
        }

        /// <summary>
        /// Receives an edit document from the page.
        ///
        /// Everything here is treated as data. The page is our own and is served out of the assembly,
        /// but a message handler that trusts its input is the wrong shape to leave behind for the
        /// editors that come after this one - so the message is parsed, its type is checked, and a
        /// document this build does not understand is reported rather than half-applied.
        /// </summary>
        private void OnWebMessageReceived(object sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            try
            {
                if (JObject.Parse(e.WebMessageAsJson) is not { } message) return;

                var type = (string)message[@"type"];
                if (type == CommandMessageType)
                {
                    // Named, not dispatched here: which commands exist and what they do belongs to
                    // the window that owns the project. An unknown one is reported rather than
                    // ignored, because the only way to produce one is a page and a host built apart.
                    if ((string)message[@"command"] is not { Length: > 0 } command)
                    {
                        MLog.Warning(@"ModMaker editor sent a command with no name; ignored.");
                        return;
                    }

                    // Capped, and integers only. The page is ours and asks for the handful of refs
                    // the open class uses, but this is the one command that carries a LIST, and a
                    // list is the shape that turns a display convenience into a way to make the host
                    // do unbounded work.
                    var ids = (message[@"ids"] as JArray ?? [])
                        .Select(t => (int?)t)
                        .Where(i => i is > 0)
                        .Select(i => i.Value)
                        .Distinct()
                        .Take(MaxStringLookups)
                        .ToList();

                    CommandInvoked?.Invoke(this, new EditorCommandEventArgs(
                        command, (int?)message[@"index"], (bool?)message[@"value"], ids));
                    return;
                }

                if (type != EditMessageType) return;

                // The page is told which collection it is editing and says so on the way back. A
                // mismatch is not something the shipped pages can produce; it is checked because the
                // consequence - one editor's document landing in another's collection - is silent, and
                // the project file would carry it forward.
                var collection = (string)message[@"collection"];
                if (!string.Equals(collection, _collectionId, StringComparison.OrdinalIgnoreCase))
                {
                    MLog.Warning($@"ModMaker editor page for '{_collectionId}' sent edits for '{collection}'; ignored.");
                    return;
                }

                if (message[@"document"] is not JObject document) return;
                EditsChanged?.Invoke(this, new EditsChangedEventArgs(_collectionId, document));
            }
            catch (Exception ex)
            {
                MLog.Error($@"ModMaker editor sent a message that could not be read: {ex.Message}");
            }
        }

        /// <summary>
        /// Pushes the title bar's state into the page.
        ///
        /// A whole state rather than a delta, for the same reason the page posts a whole edit
        /// document: a push that lands while the page is being rebuilt costs nothing, because the
        /// next one carries everything anyway.
        /// </summary>
        public void PostState(string chromeJson)
        {
            if (!_ready || webView?.CoreWebView2 == null || string.IsNullOrWhiteSpace(chromeJson)) return;

            try
            {
                // Fire and forget: this is a redraw of a title bar, and awaiting it would mean every
                // caller - each of which is already on the UI thread reacting to a click - became
                // async to update a label.
                _ = webView.CoreWebView2.ExecuteScriptAsync(
                    $@"window.__modmakerChrome && window.__modmakerChrome.apply({chromeJson});");
            }
            catch (Exception e)
            {
                MLog.Warning($@"ModMaker editor could not update its title bar: {e.Message}");
            }
        }

        /// <summary>
        /// Hands the page the text behind a set of string references.
        ///
        /// Pushed rather than returned, because the channel to a page is one-way in each direction:
        /// this is the answer to a "strings" command, and the page matches it up by id.
        /// </summary>
        public void PostStrings(string json)
        {
            if (!_ready || webView?.CoreWebView2 == null || string.IsNullOrWhiteSpace(json)) return;

            try
            {
                _ = webView.CoreWebView2.ExecuteScriptAsync(
                    $@"window.__modmakerStrings && window.__modmakerStrings.apply({json});");
            }
            catch (Exception e)
            {
                MLog.Warning($@"ModMaker editor could not deliver string lookups: {e.Message}");
            }
        }

        private void ShowError(string message)
        {
            ErrorText.Text = $@"The ModMaker editor could not start: {message}";
            ErrorText.Visibility = Visibility.Visible;
            webView.Visibility = Visibility.Collapsed;
        }

        private static readonly Dictionary<string, string> MimeTypes = new(StringComparer.OrdinalIgnoreCase)
        {
            [@".html"] = @"text/html",
            [@".js"] = @"application/javascript",
            [@".css"] = @"text/css",
            [@".json"] = @"application/json",
            [@".png"] = @"image/png",
            [@".jpg"] = @"image/jpeg",
            [@".svg"] = @"image/svg+xml",
            [@".woff2"] = @"font/woff2",
        };

        /// <summary>
        /// Serves a page asset out of the assembly.
        ///
        /// The request path is turned into a manifest resource name and looked up; anything the
        /// manifest does not contain is a 404. Because nothing is resolved against the filesystem,
        /// there is no traversal to guard - the log viewer needs that guard precisely because it
        /// extracts to a folder first.
        /// </summary>
        private void OnWebResourceRequested(object sender, CoreWebView2WebResourceRequestedEventArgs e)
        {
            if (!Uri.TryCreate(e.Request.Uri, UriKind.Absolute, out var uri)) return;

            // The disk folder first, the assembly second. That order is what lets the renders be
            // worked on without a rebuild, and it is also the order that makes moving them into the
            // bundle a deletion rather than a migration.
            var stream = ArtFile(uri.AbsolutePath);
            var name = stream != null ? null : ResourceNameFor(uri.AbsolutePath);
            stream ??= name == null ? null : Assembly.GetExecutingAssembly().GetManifestResourceStream(name);

            if (stream == null)
            {
                MLog.Warning($@"ModMaker editor requested an asset that is not embedded: {uri.AbsolutePath}");
                e.Response = webView.CoreWebView2.Environment.CreateWebResourceResponse(
                    null, 404, @"Not Found", @"Cache-Control: no-store");
                return;
            }

            var contentType = MimeTypes.TryGetValue(Path.GetExtension(uri.AbsolutePath), out var ct)
                ? ct : @"application/octet-stream";

            e.Response = webView.CoreWebView2.Environment.CreateWebResourceResponse(
                stream, 200, @"OK", $"Content-Type: {contentType}\nCache-Control: no-store");
        }

        /// <summary>
        /// A character render from the override folder, or null.
        ///
        /// This one IS resolved against the filesystem, so unlike the assembly lookup it has to
        /// defend itself: only a bare file name directly under /art/ is served, and only a .png. The
        /// page is ours and asks for exactly that, but a request handler reading disk on behalf of a
        /// web page is the wrong shape to leave unguarded for whatever is built on it next.
        /// </summary>
        private static Stream ArtFile(string absolutePath)
        {
            if (!absolutePath.StartsWith(ArtPath, StringComparison.OrdinalIgnoreCase)) return null;

            var name = Uri.UnescapeDataString(absolutePath[ArtPath.Length..]);
            if (name.Length == 0 || name.Length > 128) return null;
            if (!name.EndsWith(@".png", StringComparison.OrdinalIgnoreCase)) return null;
            if (name.IndexOfAny(['/', '\\', ':']) >= 0) return null;
            if (name != Path.GetFileName(name)) return null;

            try
            {
                var path = Path.Combine(ArtOverrideFolder, name);
                return File.Exists(path) ? File.OpenRead(path) : null;
            }
            catch (Exception e)
            {
                // A missing drive is the normal case on any machine but the one the art is drawn on,
                // and the page already draws a placeholder for art it does not get.
                MLog.Warning($@"ModMaker could not read character art '{name}': {e.Message}");
                return null;
            }
        }

        /// <summary>
        /// Maps "/wavelist.html" to its manifest resource name, or null when the path is not a plain
        /// file name under the web folder. Subfolders become dots, which is how the build names them.
        /// </summary>
        private static string ResourceNameFor(string absolutePath)
        {
            var relative = absolutePath.TrimStart('/');
            if (relative.Length == 0) return null;
            if (relative.Contains(@"..", StringComparison.Ordinal)) return null;

            var candidate = ResourcePrefix + relative.Replace('/', '.');
            return Assembly.GetExecutingAssembly().GetManifestResourceNames()
                           .FirstOrDefault(n => n.Equals(candidate, StringComparison.Ordinal));
        }

        private static void OpenExternally(string uri)
        {
            try
            {
                Process.Start(new ProcessStartInfo(uri) { UseShellExecute = true });
            }
            catch (Exception e)
            {
                MLog.Warning($@"Could not open {uri} externally: {e.Message}");
            }
        }

        private void OnNewWindowRequested(object sender, CoreWebView2NewWindowRequestedEventArgs e)
        {
            e.Handled = true;
            OpenExternally(e.Uri);
        }

        /// <summary>
        /// The editor never navigates off its own origin. Anything that tries is a link the user
        /// clicked, and it belongs in their browser rather than inside the app.
        /// </summary>
        private void OnNavigationStarting(object sender, CoreWebView2NavigationStartingEventArgs e)
        {
            if (e.Uri.StartsWith(EditorOrigin, StringComparison.OrdinalIgnoreCase)) return;

            e.Cancel = true;
            OpenExternally(e.Uri);
        }
    }
}
