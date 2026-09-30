using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ME3TweaksModManager.modmaker.editor;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ME3TweaksModManager.modmaker.project
{
    /// <summary>
    /// A user's in-progress mod: the `.mmproj` file described by schema/project.schema.json.
    ///
    /// Backed by a JObject rather than by a POCO graph, and that is the whole design. The schema
    /// requires that keys this build does not recognise SURVIVE a round trip - a project written by a
    /// newer Mod Manager, or against a bundle that has since changed, must not be quietly stripped by
    /// being opened. Deserialising into typed properties and re-serialising them does exactly that
    /// stripping, silently, and the user finds out when their mod compiles without half its edits.
    /// So the document is the source of truth and the typed accessors below are views onto it.
    ///
    /// The file is SPARSE. It stores what differs from the generated bundle, never a snapshot of it.
    /// Anything absent inherits, which is what lets a later Milan build reach a mod that did not
    /// override the thing that changed.
    /// </summary>
    public class ModMakerProject
    {
        public const int CurrentFormat = 1;

        /// <summary>Magic string, so a stray .json is not mistaken for a project.</summary>
        public const string KindString = @"me3tweaks.modmaker.project";

        public const string FileExtension = @".mmproj";

        /// <summary>The document as it will be written, including anything this build does not model.</summary>
        public JObject Document { get; }

        /// <summary>Where it was loaded from or last saved to; null for a project never saved.</summary>
        public string FilePath { get; private set; }

        private ModMakerProject(JObject document, string path)
        {
            Document = document;
            FilePath = path;
        }

        public static ModMakerProject Create(string name, string game, string bundleVersion)
        {
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException(@"A project needs a name", nameof(name));

            var now = DateTime.UtcNow.ToString(@"o");
            var document = new JObject
            {
                [@"format"] = CurrentFormat,
                [@"kind"] = KindString,
                [@"meta"] = new JObject
                {
                    [@"name"] = name,
                    [@"revision"] = 1,
                    [@"created"] = now,
                    [@"modified"] = now,
                },
                [@"bundle"] = new JObject
                {
                    [@"game"] = game,
                    [@"version"] = bundleVersion ?? @"0",
                },
                // Required by the schema even when empty: a project with no `values` is a project the
                // user has not edited yet, not a malformed one.
                [@"values"] = new JObject(),
            };

            return new ModMakerProject(document, null);
        }

        /// <summary>
        /// Reads a project.
        ///
        /// Refuses a newer format and a file that is not a project at all, in both cases by throwing
        /// rather than by returning something empty - an editor that silently opens a blank project
        /// over the user's file is how work gets overwritten.
        /// </summary>
        public static ModMakerProject Load(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentNullException(nameof(path));

            JObject document;
            using (var reader = new JsonTextReader(new StreamReader(path)))
            {
                // DateParseHandling.None: dates stay the strings they were written as. Left to itself
                // Newtonsoft turns an ISO timestamp into a DateTime and writes it back in local time
                // with a different offset - rewriting a field nobody edited, in a file that is
                // supposed to round-trip byte-stable apart from what changed.
                reader.DateParseHandling = DateParseHandling.None;
                document = JObject.Load(reader);
            }

            var kind = (string)document[@"kind"];
            if (kind != KindString)
                throw new InvalidDataException($@"{Path.GetFileName(path)} is not a ModMaker project.");

            var format = (int?)document[@"format"] ?? CurrentFormat;
            if (format > CurrentFormat)
                throw new NotSupportedException(
                    $@"{Path.GetFileName(path)} is format {format}; this build understands {CurrentFormat}. Update Mod Manager.");

            MoveFactionTogglesToTheWavelist(document);
            return new ModMakerProject(document, path);
        }

        /// <summary>
        /// Moves faction toggles out of the maplist collection and into the wavelist one.
        ///
        /// They were written with the maps because the lobby's two pickers are one config write and
        /// the same list subtraction. They are EDITED from the wavelist page now, because turning a
        /// faction off takes its whole wavelist out of the game with it - and a page may only post to
        /// the collection it edits, so the toggle has to be stored where it is shown.
        ///
        /// Done on load rather than by teaching the compiler to read both places. A toggle the editor
        /// cannot show is a toggle the user cannot find or undo, and it would still be in their build.
        ///
        /// This is a migration, not a format bump: the shape of an entry has not changed, only which
        /// collection holds it, and a project that has already been migrated passes straight through.
        /// </summary>
        private static void MoveFactionTogglesToTheWavelist(JObject document)
        {
            if (document[@"collections"] is not JObject collections) return;
            if (collections[MaplistEditDocument.CollectionId] is not JObject maplist) return;
            if (maplist[@"pickers"] is not JArray pickers) return;

            var moving = pickers.OfType<JObject>()
                .Where(p => string.Equals((string)p[@"picker"], MatchPickers.Factions,
                                          StringComparison.OrdinalIgnoreCase))
                .ToList();
            if (moving.Count == 0) return;

            foreach (var entry in moving) entry.Remove();
            if (!pickers.HasValues) maplist.Remove(@"pickers");

            if (collections[WavelistEditDocument.CollectionId] is not JObject wavelist)
            {
                wavelist = new JObject { [@"format"] = WavelistEditDocument.CurrentFormat };
                collections[WavelistEditDocument.CollectionId] = wavelist;
            }

            if (wavelist[@"pickers"] is not JArray target)
            {
                target = new JArray();
                wavelist[@"pickers"] = target;
            }

            // Anything already in the wavelist collection wins. It was made by this build in the
            // editor that owns the toggle now, so it is the later statement of the two.
            var known = target.OfType<JObject>().Select(p => (string)p[@"id"])
                              .ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (var entry in moving)
                if (known.Add((string)entry[@"id"])) target.Add(entry);
        }

        /// <summary>Writes the project, stamping meta.modified. Revision is a compile counter and is left alone.</summary>
        public void Save(string path = null)
        {
            path ??= FilePath ?? throw new InvalidOperationException(@"This project has never been saved; a path is required.");

            Meta[@"modified"] = DateTime.UtcNow.ToString(@"o");

            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);

            File.WriteAllText(path, Document.ToString(Formatting.Indented));
            FilePath = path;
        }

        public string Name
        {
            get => (string)Meta[@"name"];
            set => Meta[@"name"] = value;
        }

        public JObject Meta => Child(@"meta");
        public JObject Bundle => Child(@"bundle");

        /// <summary>
        /// Whether every player in the match must have this mod installed.
        ///
        /// Drops the DLC's mismatch flag, so the game will not put this player in a lobby with anyone
        /// who does not have it. Off by default: config differences between host and clients do
        /// desync, but not usually harmfully, and a mod that cannot be played with strangers is a
        /// much bigger cost than the desync for most of what ModMaker builds.
        /// </summary>
        public bool RequireAllPlayers
        {
            get => (bool?)Document[@"meta"]?[@"requireAllPlayers"] ?? false;
            set
            {
                // Written only when true, so a project that never touched it stays sparse and a
                // future default change reaches it.
                if (value) Meta[@"requireAllPlayers"] = true;
                else (Document[@"meta"] as JObject)?.Remove(@"requireAllPlayers");
            }
        }

        /// <summary>Sparse leaf overrides, keyed "&lt;classId&gt;[@&lt;instance&gt;]/&lt;leafId&gt;".</summary>
        public JObject Values => Child(@"values");

        /// <summary>
        /// Replaces the whole leaf-override map, as the generic value editor posts it.
        ///
        /// Removed rather than written as an empty object when there is nothing in it, exactly as the
        /// collection setters do: the file is a sparse override document, and an empty map is a
        /// statement that says nothing.
        /// </summary>
        public void SetValues(JObject values)
        {
            if (values == null || !values.HasValues)
            {
                Document.Remove(@"values");
                return;
            }

            Document[@"values"] = values;
        }

        /// <summary>
        /// Payloads for the structured editors, keyed by collection id.
        ///
        /// A WRITE accessor: touching it creates the map. Readers use <see cref="CollectionIds"/> or
        /// go through the typed accessors, because a getter that adds an empty `collections` to the
        /// document leaves it in the saved file - and the file is meant to hold only what the user
        /// changed.
        /// </summary>
        public JObject Collections => Child(@"collections");

        /// <summary>The collection ids this project carries, without creating the map to find out.</summary>
        public IEnumerable<string> CollectionIds =>
            (Document[@"collections"] as JObject)?.Properties().Select(p => p.Name) ?? Enumerable.Empty<string>();

        /// <summary>
        /// The wavelist editor's payload.
        ///
        /// Reading an empty collection gives an empty document rather than null: "the user has not
        /// edited any waves" and "this project has no wavelist collection" are the same state, and
        /// making callers tell them apart buys nothing.
        /// </summary>
        public WavelistEditDocument WavelistEdits
        {
            get => WavelistEditDocument.FromJson(
                (Document[@"collections"] as JObject)?[WavelistEditDocument.CollectionId] as JObject);
            set
            {
                if (value == null || value.Count == 0)
                {
                    if (Document[@"collections"] is not JObject) return;
                    // Removed rather than written as an empty object. The file is a sparse override
                    // document; an empty collection is a statement that says nothing.
                    Collections.Remove(WavelistEditDocument.CollectionId);
                    if (!Collections.HasValues) Document.Remove(@"collections");
                    return;
                }

                Collections[WavelistEditDocument.CollectionId] = value.ToJson();
            }
        }

        /// <summary>
        /// The enemy stat editor's payload. Reads without creating the collections map, exactly as
        /// <see cref="WavelistEdits"/> does.
        /// </summary>
        public EnemyEditDocument EnemyEdits
        {
            get => EnemyEditDocument.FromJson(
                (Document[@"collections"] as JObject)?[EnemyEditDocument.CollectionId] as JObject);
            set
            {
                if (value == null || value.Count == 0)
                {
                    if (Document[@"collections"] is not JObject) return;
                    Collections.Remove(EnemyEditDocument.CollectionId);
                    if (!Collections.HasValues) Document.Remove(@"collections");
                    return;
                }

                Collections[EnemyEditDocument.CollectionId] = value.ToJson();
            }
        }

        /// <summary>
        /// The maplist editor's payload. Reads without creating the collections map, as the others do.
        /// </summary>
        public MaplistEditDocument MaplistEdits
        {
            get => MaplistEditDocument.FromJson(
                (Document[@"collections"] as JObject)?[MaplistEditDocument.CollectionId] as JObject);
            set
            {
                if (value == null || value.Count == 0)
                {
                    if (Document[@"collections"] is not JObject) return;
                    Collections.Remove(MaplistEditDocument.CollectionId);
                    if (!Collections.HasValues) Document.Remove(@"collections");
                    return;
                }

                Collections[MaplistEditDocument.CollectionId] = value.ToJson();
            }
        }

        /// <summary>Gets a child object, creating it if the document does not carry one yet.</summary>
        private JObject Child(string name)
        {
            if (Document[name] is JObject existing) return existing;
            var created = new JObject();
            Document[name] = created;
            return created;
        }
    }
}
