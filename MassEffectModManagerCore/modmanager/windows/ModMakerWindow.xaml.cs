using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Packages;
using ME3TweaksCore.Diagnostics;
using ME3TweaksCore.Targets;
using ME3TweaksModManager.extensions;
using ME3TweaksModManager.modmaker.compiler;
using ME3TweaksModManager.modmaker.editor;
using ME3TweaksModManager.modmaker.generator;
using ME3TweaksModManager.modmaker.project;
using ME3TweaksModManager.modmanager.helpers;
using ME3TweaksModManager.modmanager.localizations;
using Microsoft.Win32;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ME3TweaksModManager.modmanager.windows
{
    /// <summary>
    /// Hosts the ModMaker editor. A window rather than a panel: the editor is a workspace the user
    /// stays in, and the wavelist grid alone wants more width than the main window's panel area.
    ///
    /// The window owns the PROJECT; the page owns the editing. That split is why the page posts every
    /// change instead of offering a save button of its own - a page holding unsent edits loses them
    /// when the window closes, and it has no idea which file they belong to.
    /// </summary>
    public partial class ModMakerWindow : Window
    {
        private readonly WavelistCatalogue _waves;
        private readonly DifficultyCatalogue _difficulty;
        private readonly MapListCatalogue _maps;
        private readonly ObjectiveCatalogue _objectives;
        private readonly WeaponCatalogue _weapons;
        private readonly SectionIndex _sections;
        private readonly GameTarget _target;
        private readonly string _payloadJson;
        private readonly string _enemyPayloadJson;
        private readonly string _mapPayloadJson;
        private readonly MEGame _game;

        /// <summary>
        /// The editor pages this window offers, in the order they are listed.
        ///
        /// A page is offered only when its payload was built, so a scan that could not read the
        /// difficulty tables leaves the enemy editor out of the list rather than opening it empty.
        /// </summary>
        private readonly List<EditorPage> _pages = new();

        private readonly string _valuesPayloadJson;
        private readonly string _objectivePayloadJson;
        private readonly string _weaponPayloadJson;

        /// <summary>
        /// The install's TLKs, for showing the words behind a string reference.
        ///
        /// Held rather than baked into the payload because the page can ask about a reference the
        /// user has just TYPED, which no payload could have anticipated - and because the whole TLK
        /// is far larger than the surface that uses it.
        /// </summary>
        private readonly GameStrings _strings;
        private readonly IReadOnlyDictionary<string, CatalogueClass> _classes;
        private readonly InstanceCatalogue _instances;

        private ModMakerEditorControl _current;

        /// <summary>
        /// Which editor is open. Kept here rather than read back off a control, because the control
        /// that used to hold it - the picker in the removed WPF strip - is now a row of tabs inside
        /// the page, and the page is rebuilt every time the project changes underneath it.
        /// </summary>
        private int _pageIndex;

        /// <summary>True while a compile is running, so the page can grey its own button.</summary>
        private bool _compiling;

        private sealed record EditorPage(string Title, string Page, string Collection, string Payload);

        private ModMakerProject _project;

        /// <summary>
        /// True when the in-memory edits differ from what is on disk.
        ///
        /// Tracked rather than derived by comparing documents: an edit that changes a value and an
        /// edit that changes it back are both changes the user made, and the second one still has to
        /// reach the file.
        /// </summary>
        private bool _dirty;

        /// <summary>The user's wavelist edits, as the page last reported them. Sparse - only the cells they changed.</summary>
        public WavelistEditDocument Edits { get; private set; } = new();

        /// <summary>The user's enemy stat edits. Sparse in the same way.</summary>
        public EnemyEditDocument EnemyEdits { get; private set; } = new();

        /// <summary>The user's map edits: availability and presentation, both sparse.</summary>
        public MaplistEditDocument MaplistEdits { get; private set; } = new();

        /// <summary>
        /// The user's generic leaf overrides, keyed "&lt;classId&gt;[@&lt;instance&gt;]/&lt;leafId&gt;".
        ///
        /// A flat map rather than a document type, because that is what the project file holds - the
        /// generic surface has no rows to model, only keys and values.
        /// </summary>
        public JObject ValueEdits { get; private set; } = new();

        /// <summary>The user's objective edits: rule values and which waves carry one. Sparse.</summary>
        public ObjectiveEditDocument ObjectiveEdits { get; private set; } = new();

        /// <summary>The user's weapon stat edits. Sparse.</summary>
        public WeaponEditDocument WeaponEdits { get; private set; } = new();

        private readonly string _baseTitle;

        /// <param name="payloadJson">
        /// The wavelist payload, from <see cref="WavelistEditorPayload"/>. Built by the caller so the
        /// window does not have to know how to scan a game, and so it can be shown against a fixture.
        /// </param>
        /// <param name="waves">
        /// The catalogue the payload was built from, so edits can be checked against the installed
        /// game as they are made. Optional: without it the editor still works, it just cannot say
        /// whether an edit is one the game can load.
        /// </param>
        /// <param name="sections">
        /// The merged config, indexed. Needed only to compile: it is what says which coalesced
        /// sub-file each edited section lives in.
        /// </param>
        /// <param name="target">The install being edited against, read again at compile time for the mount position.</param>
        /// <param name="game">Recorded in a new project, so a project says which game it was built against.</param>
        /// <param name="enemyPayloadJson">
        /// The enemy stat payload, from <see cref="EnemyEditorPayload"/>. Null leaves that editor out
        /// of the list entirely, which is the honest outcome when the difficulty tables were not read.
        /// </param>
        /// <param name="difficulty">The difficulty tables the enemy payload was built from, for validation and compiling.</param>
        /// <param name="mapPayloadJson">The map payload, from <see cref="MaplistEditorPayload"/>. Null leaves that editor out.</param>
        /// <param name="maps">The map roster the payload was built from, for validation and compiling.</param>
        /// <param name="valuesPayloadJson">
        /// The generic value payload, from <see cref="ValuesEditorPayload"/>. Null leaves that editor
        /// out, which is what happens when the definition bundle could not be built.
        /// </param>
        /// <param name="classes">
        /// The scanned classes. Needed to compile a value edit to a property the config does not set:
        /// the class is the only place that property's real spelling exists.
        /// </param>
        /// <param name="instances">The per-object classes, for value keys that name an instance.</param>
        /// <param name="weaponPayloadJson">
        /// The weapons payload, from <see cref="WeaponEditorPayload"/>. Null leaves that editor out.
        /// </param>
        /// <param name="weapons">The weapons the payload was built from, for validation and compiling.</param>
        /// <param name="objectivePayloadJson">
        /// The objectives payload, from <see cref="ObjectiveEditorPayload"/>. Null leaves that editor
        /// out, which is the honest outcome when the engagement classes were not read.
        /// </param>
        /// <param name="objectives">The objectives the payload was built from, for validation and compiling.</param>
        /// <param name="strings">
        /// The install's TLKs, so a string reference can be shown as the line of text it names.
        /// Optional: without it the editor shows the numbers, which is what it did before.
        /// </param>
        public ModMakerWindow(string payloadJson, WavelistCatalogue waves = null, SectionIndex sections = null,
                              GameTarget target = null, MEGame game = MEGame.LMP,
                              string enemyPayloadJson = null, DifficultyCatalogue difficulty = null,
                              string mapPayloadJson = null, MapListCatalogue maps = null,
                              string valuesPayloadJson = null,
                              IReadOnlyDictionary<string, CatalogueClass> classes = null,
                              InstanceCatalogue instances = null,
                              GameStrings strings = null,
                              string objectivePayloadJson = null,
                              ObjectiveCatalogue objectives = null,
                              string weaponPayloadJson = null,
                              WeaponCatalogue weapons = null)
        {
            InitializeComponent();
            this.ApplyDarkNetWindowTheme();

            _waves = waves;
            _difficulty = difficulty;
            _maps = maps;
            _sections = sections;
            _target = target;
            _payloadJson = payloadJson;
            _enemyPayloadJson = enemyPayloadJson;
            _mapPayloadJson = mapPayloadJson;
            _valuesPayloadJson = valuesPayloadJson;
            _classes = classes;
            _instances = instances;
            _strings = strings;
            _objectivePayloadJson = objectivePayloadJson;
            _objectives = objectives;
            _weaponPayloadJson = weaponPayloadJson;
            _weapons = weapons;
            _game = game;
            _baseTitle = Title;

            if (payloadJson != null)
                _pages.Add(new EditorPage(@"Wavelists", @"wavelist.html", WavelistEditDocument.CollectionId, payloadJson));
            if (enemyPayloadJson != null)
                _pages.Add(new EditorPage(@"Enemies", @"enemy.html", EnemyEditDocument.CollectionId, enemyPayloadJson));
            if (mapPayloadJson != null)
                _pages.Add(new EditorPage(@"Maps", @"maplist.html", MaplistEditDocument.CollectionId, mapPayloadJson));
            // Last, deliberately. The dedicated editors model what they edit; this one is every other
            // config value in the game, and it is the place to go when none of them covers it.
            if (weaponPayloadJson != null)
                _pages.Add(new EditorPage(@"Weapons", @"weapons.html", WeaponEditDocument.CollectionId, weaponPayloadJson));
            if (objectivePayloadJson != null)
                _pages.Add(new EditorPage(@"Objectives", @"objectives.html", ObjectiveEditDocument.CollectionId, objectivePayloadJson));
            if (valuesPayloadJson != null)
                _pages.Add(new EditorPage(@"Values", @"values.html", ValuesEditorPayload.CollectionId, valuesPayloadJson));

            _project = ModMakerProject.Create(@"Untitled", game.ToString(), @"0");
            LoadEditorForProject();
        }

        /// <summary>
        /// Builds a fresh WebView2 host carrying the current project's edits.
        ///
        /// Recreated rather than messaged when the project changes, because the page reads its saved
        /// edits from a global set before any script runs. Pushing a whole new document into a live
        /// page would need a second, different path into the same state - two ways for the page and
        /// the file to disagree.
        /// </summary>
        private void LoadEditorForProject()
        {
            Edits = _project.WavelistEdits;
            EnemyEdits = _project.EnemyEdits;
            MaplistEdits = _project.MaplistEdits;
            // Read without creating the map, so opening a project that has no value edits does not
            // add an empty `values` to it and leave it there when it is saved.
            ValueEdits = (_project.Document[@"values"] as JObject) ?? new JObject();
            ObjectiveEdits = ObjectiveEditDocument.FromJson(
                _project.Document[ObjectiveEditDocument.CollectionId] as JObject);
            WeaponEdits = WeaponEditDocument.FromJson(
                _project.Document[WeaponEditDocument.CollectionId] as JObject);

            _dirty = false;
            ShowEditor(_pageIndex);
            UpdateChrome();
        }

        /// <summary>
        /// Whether the built DLC lets its owner match with players who do not have it.
        ///
        /// A property of the mod rather than of a build, so it is stored in the project and travels
        /// with it. Nothing else in the window changes: it reaches the game as one bit of the mount
        /// file, resolved at compile time.
        /// </summary>
        private void SetRequireAllPlayers(bool required)
        {
            if (required == _project.RequireAllPlayers) return;

            _project.RequireAllPlayers = required;
            _dirty = true;
            UpdateChrome();
        }

        /// <summary>
        /// Swaps in the chosen editor, built fresh against the project's current edits.
        ///
        /// Rebuilt rather than kept alive and hidden: a page reads its saved edits from a global set
        /// before any script runs, so a page that was open while the project changed underneath it
        /// would need a second path into the same state. Two ways for the page and the file to
        /// disagree is exactly the bug that costs a user their work.
        /// </summary>
        private void ShowEditor(int index)
        {
            if (index < 0 || index >= _pages.Count) return;
            _pageIndex = index;
            var page = _pages[index];

            var saved = page.Collection switch
            {
                EnemyEditDocument.CollectionId => EnemyEdits.Count > 0 ? EnemyEdits.ToJsonString(indented: false) : null,
                MaplistEditDocument.CollectionId => MaplistEdits.Count > 0 ? MaplistEdits.ToJsonString(indented: false) : null,
                ValuesEditorPayload.CollectionId => ValueEdits.HasValues ? ValueEdits.ToString(Formatting.None) : null,
                ObjectiveEditDocument.CollectionId => ObjectiveEdits.Count > 0 ? ObjectiveEdits.ToJsonString(indented: false) : null,
                WeaponEditDocument.CollectionId => WeaponEdits.Count > 0 ? WeaponEdits.ToJsonString(indented: false) : null,
                _ => Edits.Count > 0 ? Edits.ToJsonString(indented: false) : null,
            };

            if (_current != null)
            {
                _current.EditsChanged -= OnEditsChanged;
                _current.CommandInvoked -= OnCommandInvoked;
            }

            _current = new ModMakerEditorControl(page.Page, page.Collection, page.Payload, saved, ChromeState());
            _current.EditsChanged += OnEditsChanged;
            _current.CommandInvoked += OnCommandInvoked;
            EditorHost.Content = _current;
        }

        /// <summary>
        /// Carries out a command the page's title bar asked for.
        ///
        /// Every one of these was a WPF button a moment ago and the handlers are unchanged - the page
        /// asks for exactly what the strip used to do directly. Nothing here trusts the message
        /// beyond its name: a page cannot name a file, choose a mount flag or pick an editor that is
        /// not in the list, because none of those values travel in the message.
        /// </summary>
        private void OnCommandInvoked(object sender, EditorCommandEventArgs e)
        {
            switch (e.Command)
            {
                case @"new": NewProject_Click(this, null); break;
                case @"open": OpenProject_Click(this, null); break;
                case @"save": SaveProject_Click(this, null); break;
                case @"saveAs": SaveProjectAs_Click(this, null); break;
                case @"compile": Compile_Click(this, null); break;
                case @"requireAllPlayers": SetRequireAllPlayers(e.Value == true); break;
                case @"strings": ResolveStrings(e.Ids); break;

                // Bounds-checked rather than trusted: the tabs are drawn from the list this window
                // sent, but an index is the one value in these messages that indexes anything.
                case @"page" when e.Index is { } i && i >= 0 && i < _pages.Count:
                    if (i != _pageIndex) ShowEditor(i);
                    break;

                default:
                    MLog.Warning($@"ModMaker editor asked for a command this build does not have: {e.Command}");
                    break;
            }
        }

        /// <summary>
        /// Answers a page's request for the text behind some string references.
        ///
        /// Every id asked about is answered, including the ones that resolve to nothing: a page that
        /// only hears about the hits cannot tell "not looked up yet" from "there is no such line",
        /// and would go on asking about the same absent reference forever.
        /// </summary>
        private void ResolveStrings(IReadOnlyList<int> ids)
        {
            if (_current == null || ids == null || ids.Count == 0) return;

            var found = new JObject();
            foreach (var id in ids) found[id.ToString(CultureInfo.InvariantCulture)] = _strings?.Resolve(id);

            _current.PostStrings(found.ToString(Formatting.None));
        }

        /// <summary>
        /// Everything the page's title bar draws, as JSON.
        ///
        /// Built rather than bound: the bar is redrawn from a whole state each time, so there is no
        /// per-field push to keep in step and no way for half of it to be stale.
        /// </summary>
        private string ChromeState()
        {
            var state = new JObject
            {
                [@"project"] = new JObject
                {
                    [@"name"] = _project.Name,
                    [@"file"] = _project.FilePath,
                    [@"dirty"] = _dirty,
                    [@"requireAllPlayers"] = _project.RequireAllPlayers,
                },
                [@"pages"] = new JArray(_pages.Select((p, i) => new JObject
                {
                    [@"title"] = p.Title,
                    [@"active"] = i == _pageIndex,
                })),
                [@"status"] = _status,
                [@"problems"] = _problems > 0,
                [@"busy"] = _compiling,
                // Said up front so the button can explain itself. The editor can be opened against a
                // fixture with no install behind it, and then it can never compile.
                [@"canCompile"] = _target != null && _sections != null,
            };
            return state.ToString(Formatting.None);
        }

        /// <summary>
        /// Takes an edit document from whichever page is open and files it under its collection.
        ///
        /// Both documents are validated as they are made rather than at compile time. None of these
        /// failures throw when compiled - they produce a wave that quietly spawns nothing, or an enemy
        /// that behaves the way it always did - so the moment to catch one is while the user still
        /// remembers making it.
        /// </summary>
        private void OnEditsChanged(object sender, EditsChangedEventArgs e)
        {
            if (string.Equals(e.Collection, EnemyEditDocument.CollectionId, StringComparison.OrdinalIgnoreCase))
            {
                EnemyEdits = EnemyEditDocument.FromJson(e.Document);
                _project.EnemyEdits = EnemyEdits;

                if (_difficulty != null)
                {
                    foreach (var problem in EnemyEditValidator.Validate(EnemyEdits, _difficulty))
                        MLog.Warning($@"ModMaker enemy: {problem}");
                }
            }
            else if (string.Equals(e.Collection, ValuesEditorPayload.CollectionId, StringComparison.OrdinalIgnoreCase))
            {
                // Written straight through: `values` is a flat map in the project file, not a
                // collection with rows, and there is nothing to deserialise into. It is also not
                // validated here - a leaf edit is checked when it is COMPILED, against the install
                // being compiled against, which is not necessarily the one it was authored on.
                ValueEdits = e.Document ?? new JObject();
                _project.SetValues(ValueEdits);
            }
            else if (string.Equals(e.Collection, WeaponEditDocument.CollectionId, StringComparison.OrdinalIgnoreCase))
            {
                WeaponEdits = WeaponEditDocument.FromJson(e.Document);

                if (WeaponEdits.Count > 0) _project.Document[WeaponEditDocument.CollectionId] = e.Document;
                else _project.Document.Remove(WeaponEditDocument.CollectionId);

                if (_weapons != null)
                {
                    foreach (var problem in WeaponEditValidator.Validate(WeaponEdits, _weapons))
                        MLog.Warning($@"ModMaker weapons: {problem}");
                }
            }
            else if (string.Equals(e.Collection, ObjectiveEditDocument.CollectionId, StringComparison.OrdinalIgnoreCase))
            {
                ObjectiveEdits = ObjectiveEditDocument.FromJson(e.Document);

                // Removed when empty rather than left as an empty object: a project that has never
                // had an objective edit should not carry the key, and one that has had them all
                // reverted should stop carrying it.
                if (ObjectiveEdits.Count > 0) _project.Document[ObjectiveEditDocument.CollectionId] = e.Document;
                else _project.Document.Remove(ObjectiveEditDocument.CollectionId);

                if (_objectives != null)
                {
                    foreach (var problem in ObjectiveEditValidator.Validate(ObjectiveEdits, _objectives))
                        MLog.Warning($@"ModMaker objectives: {problem}");
                }
            }
            else if (string.Equals(e.Collection, MaplistEditDocument.CollectionId, StringComparison.OrdinalIgnoreCase))
            {
                MaplistEdits = MaplistEditDocument.FromJson(e.Document);
                _project.MaplistEdits = MaplistEdits;

                if (_maps != null)
                {
                    foreach (var problem in MaplistEditValidator.Validate(MaplistEdits, _maps))
                        MLog.Warning($@"ModMaker maplist: {problem}");
                }
            }
            else
            {
                Edits = WavelistEditDocument.FromJson(e.Document);
                _project.WavelistEdits = Edits;

                if (_waves != null)
                {
                    foreach (var problem in WavelistEditValidator.Validate(Edits, _waves))
                        MLog.Warning($@"ModMaker wavelist: {problem}");
                }

                // The faction toggles this collection also carries are checked against the LOBBY's
                // lists rather than the wave catalogue - the wave catalogue knows which wavelists
                // exist, not which of them a player can choose.
                if (_maps != null)
                {
                    foreach (var problem in MaplistEditValidator.ValidateFactionToggles(Edits.Pickers, _maps))
                        MLog.Warning($@"ModMaker wavelist: {problem}");
                }
            }

            _dirty = true;
            UpdateChrome();
        }

        /// <summary>The status line the page's title bar shows, and whether it is reporting a problem.</summary>
        private string _status = string.Empty;

        private int _problems;

        /// <summary>
        /// Recomputes the window title and the page's title bar, and pushes the bar into the page.
        ///
        /// One method rather than two, because the two must not disagree: the title's unsaved marker
        /// and the bar's unsaved dot are the same fact, and the way that goes wrong is one of them
        /// being updated on a path the other is not.
        /// </summary>
        private void UpdateChrome()
        {
            _problems = (_waves == null
                    ? 0
                    : WavelistEditValidator.Validate(Edits, _waves).Count(p => p.Severity == WavelistProblemSeverity.Error))
                + (_difficulty == null
                    ? 0
                    : EnemyEditValidator.Validate(EnemyEdits, _difficulty).Count(p => p.Severity == EnemyProblemSeverity.Error))
                + (_maps == null
                    ? 0
                    : MaplistEditValidator.Validate(MaplistEdits, _maps).Count(p => p.Severity == MaplistProblemSeverity.Error))
                + (_objectives == null
                    ? 0
                    : ObjectiveEditValidator.Validate(ObjectiveEdits, _objectives).Count(p => p.Severity == ObjectiveProblemSeverity.Error))
                + (_weapons == null
                    ? 0
                    : WeaponEditValidator.Validate(WeaponEdits, _weapons).Count(p => p.Severity == WeaponProblemSeverity.Error));

            Title = $@"{_baseTitle} — {_project.Name}{(_dirty ? @" *" : string.Empty)}";

            // Counted per editor rather than as one total: "3 edits" over two editors tells the user
            // nothing about which mod they are building.
            var parts = new List<string>();
            if (Edits.Count > 0) parts.Add($@"{Edits.Count} wave cell(s)");
            if (EnemyEdits.Count > 0) parts.Add($@"{EnemyEdits.Count} enemy stat(s)");
            if (MaplistEdits.Count > 0) parts.Add($@"{MaplistEdits.Count} map change(s)");
            if (WeaponEdits.Count > 0) parts.Add($@"{WeaponEdits.Count} weapon stat(s)");
            if (ObjectiveEdits.Count > 0) parts.Add($@"{ObjectiveEdits.Count} objective change(s)");
            if (ValueEdits.Count > 0) parts.Add($@"{ValueEdits.Count} value(s)");

            // The file name is no longer in here: the bar shows the project by name and its path in
            // the chip's tooltip, so repeating it in the status was the same string twice.
            _status = parts.Count == 0
                ? @"no edits"
                : string.Join(@", ", parts) + (_problems > 0 ? $@", {_problems} problem(s)" : string.Empty);

            _current?.PostState(ChromeState());
        }

        // ------------------------------------------------------------------ compiling

        /// <summary>
        /// Compiles the project into a mod, in two named halves.
        ///
        /// <see cref="ProjectCompiler"/> resolves the project against this install into a manifest,
        /// and <see cref="ModEmitter"/> writes that manifest out as a DLC mod. The manifest is saved
        /// beside the mod as well: everything the emitter did is in it, in text, which is the only
        /// practical way to answer "why did my wave not change" - the failure modes here produce a mod
        /// that installs and misbehaves rather than an error.
        /// </summary>
        private void Compile_Click(object sender, RoutedEventArgs e)
        {
            if (_target == null || _sections == null)
            {
                M3L.ShowDialog(this, @"This editor was opened without an install to compile against.",
                    @"ModMaker", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            var task = BackgroundTaskEngine.SubmitBackgroundJob(@"ModMakerCompile",
                $@"Compiling {_project.Name}", $@"Compiled {_project.Name}");

            _compiling = true;
            UpdateChrome();

            CompileResult resolved = null;
            EmitResult emitted = null;
            Exception failure = null;

            Task.Run(() =>
            {
                try
                {
                    var mount = InstallMount.Read(_target);
                    resolved = ProjectCompiler.Compile(_project, new CompileInputs
                    {
                        Waves = _waves,
                        Difficulty = _difficulty,
                        Maps = _maps,
                        Objectives = _objectives,
                        Weapons = _weapons,
                        Sections = _sections,
                        Classes = _classes,
                        Instances = _instances,
                        Game = _game.ToString(),
                        MountPriority = mount.Priority,
                        App = $@"ME3TweaksModManager {App.AppVersion}",
                    });

                    foreach (var problem in resolved.Problems) MLog.Information($@"ModMaker compile: {problem}");
                    if (!resolved.Succeeded) return;

                    emitted = ModEmitter.Emit(resolved.Manifest, progress: text =>
                        BackgroundTaskEngine.SubmitBackgroundTaskUpdate(task, text));

                    if (emitted.Succeeded)
                    {
                        // Beside the mod, not inside the DLC: it is a build record, not content.
                        resolved.Manifest.Save(Path.Combine(emitted.ModDirectory, @"modmaker-build.json"));
                    }
                }
                catch (Exception ex)
                {
                    failure = ex;
                    MLog.Exception(ex, @"ModMaker compile failed");
                }
            }).ContinueWithOnUIThread(_ =>
            {
                BackgroundTaskEngine.SubmitJobCompletion(task);
                _compiling = false;
                UpdateChrome();

                if (failure != null)
                {
                    M3L.ShowDialog(this, $@"Compile failed: {failure.Message}", @"ModMaker",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                if (resolved is not { Succeeded: true })
                {
                    M3L.ShowDialog(this,
                        @"This project could not be compiled:" + Environment.NewLine + Environment.NewLine +
                        string.Join(Environment.NewLine, resolved.Errors.Select(p => $@"• {p.Where}: {p.Message}")),
                        @"ModMaker", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                if (emitted is not { Succeeded: true })
                {
                    M3L.ShowDialog(this,
                        @"The mod could not be written:" + Environment.NewLine + Environment.NewLine +
                        string.Join(Environment.NewLine, emitted?.Problems.Select(p => $@"• {p.Message}") ?? []),
                        @"ModMaker", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                // The revision is a compile counter, mirroring the old server-side one: it becomes the
                // mod's version, so a rebuilt mod looks newer than the copy already installed.
                _project.Meta[@"revision"] = ((int?)_project.Meta[@"revision"] ?? 1) + 1;
                if (_project.FilePath != null) Save(_project.FilePath);
                UpdateChrome();

                var warnings = resolved.Warnings.ToList();
                M3L.ShowDialog(this,
                    $@"{_project.Name} was built into:" + Environment.NewLine + emitted.ModDirectory +
                    Environment.NewLine + Environment.NewLine +
                    $@"{resolved.WavelistRowsWritten} wave row(s), {resolved.EnemyRowsWritten} enemy row(s), " +
                    $@"{resolved.MaplistWritesWritten} map change(s) and {resolved.ObjectiveWritesWritten} " +
                    $@"objective write(s)." +
                    (warnings.Count > 0
                        ? Environment.NewLine + Environment.NewLine + @"Warnings:" + Environment.NewLine +
                          string.Join(Environment.NewLine, warnings.Select(p => $@"• {p.Where}: {p.Message}"))
                        : string.Empty),
                    @"ModMaker", MessageBoxButton.OK,
                    warnings.Count > 0 ? MessageBoxImage.Warning : MessageBoxImage.Information);
            });
        }

        // ------------------------------------------------------------------ project commands

        private void NewProject_Click(object sender, RoutedEventArgs e)
        {
            if (!ConfirmDiscard(@"Start a new project")) return;

            _project = ModMakerProject.Create(@"Untitled", _game.ToString(), @"0");
            LoadEditorForProject();
        }

        private void OpenProject_Click(object sender, RoutedEventArgs e)
        {
            if (!ConfirmDiscard(@"Open another project")) return;

            var dialog = new OpenFileDialog
            {
                Filter = $@"ModMaker project|*{ModMakerProject.FileExtension}",
                CheckFileExists = true,
            };
            if (dialog.ShowDialog() != true) return;

            try
            {
                _project = ModMakerProject.Load(dialog.FileName);
            }
            catch (Exception ex)
            {
                // Refused rather than opened empty: an editor that silently shows a blank project over
                // the user's file is how work gets overwritten.
                MLog.Exception(ex, @"ModMaker could not open a project");
                M3L.ShowDialog(this, ex.Message, @"ModMaker", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            LoadEditorForProject();
        }

        private void SaveProject_Click(object sender, RoutedEventArgs e)
        {
            if (_project.FilePath == null) SaveProjectAs_Click(sender, e);
            else Save(_project.FilePath);
        }

        private void SaveProjectAs_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new SaveFileDialog
            {
                Filter = $@"ModMaker project|*{ModMakerProject.FileExtension}",
                FileName = _project.Name + ModMakerProject.FileExtension,
            };
            if (dialog.ShowDialog() != true) return;

            // The file the user picked names the project, so "Silver Grind.mmproj" does not stay
            // called "Untitled" everywhere the name is shown.
            if (string.IsNullOrWhiteSpace(_project.Name) || _project.Name == @"Untitled")
                _project.Name = Path.GetFileNameWithoutExtension(dialog.FileName);

            Save(dialog.FileName);
        }

        private void Save(string path)
        {
            try
            {
                _project.Save(path);
                _dirty = false;
                UpdateChrome();
            }
            catch (Exception ex)
            {
                MLog.Exception(ex, @"ModMaker could not save a project");
                M3L.ShowDialog(this, ex.Message, @"ModMaker", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Asks before throwing away unsaved edits. Returns false to abandon whatever prompted it.
        /// </summary>
        private bool ConfirmDiscard(string action)
        {
            if (!_dirty) return true;

            var result = M3L.ShowDialog(this,
                $@"{_project.Name} has unsaved changes. {action} anyway?",
                @"ModMaker", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            return result == MessageBoxResult.Yes;
        }

        private void ModMakerWindow_OnClosing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (!ConfirmDiscard(@"Close the editor")) e.Cancel = true;
        }
    }
}
