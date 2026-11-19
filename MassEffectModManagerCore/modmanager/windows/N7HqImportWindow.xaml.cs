using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using ME3TweaksCoreWPF.UI;
using ME3TweaksModManager.extensions;
using ME3TweaksModManager.modmanager.helpers.n7hq;
using Microsoft.Web.WebView2.Core;
using Newtonsoft.Json.Linq;

namespace ME3TweaksModManager.modmanager.windows
{
    /// <summary>
    /// Hosts N7 HQ so the user can log in, then reads their profile pages
    /// </summary>
    [AddINotifyPropertyChangedInterface]
    public partial class N7HqImportWindow : Window
    {
        private const string N7HqHost = @"n7hq.masseffect.com";
        private const string StartUrl = @"https://n7hq.masseffect.com/en/home";
        private const string ScraperResource = @"ME3TweaksModManager.modmanager.helpers.n7hq.N7HqScraper.js";

        public string StatusText { get; set; } = "Loading N7 HQ...";
        public bool OnProfilePage { get; set; }
        public bool Importing { get; set; }
        public GenericCommand ImportCommand { get; }

        /// <summary>
        /// Set when a profile was read successfully
        /// </summary>
        public N7HqScrape Result { get; private set; }

        /// <summary>
        /// The scrape JSON exactly as the page returned it
        /// </summary>
        public string ResultJson { get; private set; }

        private TaskCompletionSource<string> pendingScrape;

        public N7HqImportWindow()
        {
            ImportCommand = new GenericCommand(Import, () => OnProfilePage && !Importing);
            InitializeComponent();
            this.ApplyDarkNetWindowTheme();
            Loaded += async (_, _) => await InitializeWebViewAsync();
        }

        private async Task InitializeWebViewAsync()
        {
            try
            {
                // Own profile folder so the N7 HQ login stays out of other Mod Manager web views
                var userData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"ME3TweaksModManager", @"N7HQWebView");
                var environment = await CoreWebView2Environment.CreateAsync(null, userData);
                await webView.EnsureCoreWebView2Async(environment);
                webView.CoreWebView2.NewWindowRequested += NewWindowRequested;
                webView.CoreWebView2.NavigationCompleted += (_, _) => UpdatePageState();
                // N7 HQ uses Turbo, which changes pages without a full navigation
                webView.CoreWebView2.SourceChanged += (_, _) => UpdatePageState();
                webView.CoreWebView2.WebMessageReceived += WebMessageReceived;
                webView.CoreWebView2.Navigate(StartUrl);
            }
            catch (Exception e)
            {
                M3Log.Exception(e, @"Could not start WebView2 for N7 HQ import");
                StatusText = $"Could not start the web view: {e.Message}";
            }
        }

        private void NewWindowRequested(object sender, CoreWebView2NewWindowRequestedEventArgs e)
        {
            // Keep login popups in this window
            e.Handled = true;
            webView.CoreWebView2.Navigate(e.Uri);
        }

        private void UpdatePageState()
        {
            OnProfilePage = IsProfilePage(webView.CoreWebView2.Source);
            if (Importing)
            {
                return;
            }
            StatusText = OnProfilePage
                ? "Ready to import the profile shown."
                : "Log in, then open the profile to import.";
            CommandManager.InvalidateRequerySuggested();
        }

        /// <summary>
        /// Profile pages look like /en/home?name=X&amp;platform=pc
        /// </summary>
        private static bool IsProfilePage(string url)
        {
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps
                || !uri.Host.Equals(N7HqHost, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
            var query = System.Web.HttpUtility.ParseQueryString(uri.Query);
            return uri.AbsolutePath.Contains(@"/home", StringComparison.OrdinalIgnoreCase)
                   && !string.IsNullOrEmpty(query[@"name"]) && !string.IsNullOrEmpty(query[@"platform"]);
        }

        private void WebMessageReceived(object sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            // Only trust our own script on N7 HQ
            if (!Uri.TryCreate(e.Source, UriKind.Absolute, out var source) || !source.Host.Equals(N7HqHost, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            JObject message;
            try
            {
                message = JObject.Parse(e.WebMessageAsJson);
            }
            catch (Exception)
            {
                return;
            }

            switch ((string)message[@"type"])
            {
                case @"progress":
                    StatusText = (string)message[@"text"];
                    break;
                case @"result":
                    pendingScrape?.TrySetResult(message[@"data"]?.ToString(Newtonsoft.Json.Formatting.None));
                    break;
                case @"error":
                    pendingScrape?.TrySetException(new InvalidOperationException((string)message[@"message"]));
                    break;
            }
        }

        private async void Import()
        {
            Importing = true;
            StatusText = "Reading profile...";
            try
            {
                string script;
                using (var stream = M3Utilities.ExtractInternalFileToStream(ScraperResource))
                using (var reader = new StreamReader(stream))
                {
                    script = await reader.ReadToEndAsync();
                }

                pendingScrape = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
                await webView.CoreWebView2.ExecuteScriptAsync(script);
                // ExecuteScriptAsync doesn't wait for promises, so the result comes back as a message
                await webView.CoreWebView2.ExecuteScriptAsync(
                    @"window.__m3N7HqScrape().then(r => chrome.webview.postMessage({type: 'result', data: r}), e => chrome.webview.postMessage({type: 'error', message: String((e && e.message) || e)}));");

                var json = await pendingScrape.Task.WaitAsync(TimeSpan.FromMinutes(3));
                var scrape = N7HqScrape.FromJson(json);
                if (scrape.Characters.Count == 0 && scrape.Inventory.Count == 0)
                {
                    throw new InvalidDataException("No profile data was found. Make sure you're logged in and viewing your own profile.");
                }

                ResultJson = json;
                Result = scrape;
                DialogResult = true;
            }
            catch (Exception e)
            {
                M3Log.Exception(e, @"Reading the N7 HQ profile failed");
                StatusText = $"Could not read the profile: {e.Message}";
            }
            finally
            {
                pendingScrape = null;
                Importing = false;
                CommandManager.InvalidateRequerySuggested();
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}
