using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using LegendaryExplorerCore.Misc;
using LegendaryExplorerCore.Packages;
using ME3TweaksCore.Objects;
using ME3TweaksCore.Targets;
using ME3TweaksModManager.modmanager.diagnostics;
using ME3TweaksModManager.modmanager.localizations;
using Newtonsoft.Json;

namespace ME3TweaksModManager.modmanager.objects.mod.merge.v1
{
    public class MergeFile1
    {
        [JsonProperty(@"filename")]
        public string FileName { get; set; }

        [JsonProperty(@"changes")]
        public List<MergeFileChange1> MergeChanges { get; set; }
        [JsonProperty(@"applytoalllocalizations")] public bool ApplyToAllLocalizations { get; set; }

        [JsonIgnore] public MergeMod1 Parent;

        [JsonIgnore]
        public MergeMod1 OwningMM => Parent;

        public void SetupParent(MergeMod1 mm)
        {
            Parent = mm;
            foreach (var mc in MergeChanges)
            {
                mc.SetupParent(this);
            }
        }

        /// <summary>
        /// Applies the changes this merge file describes
        /// </summary>
        /// <param name="gameTarget"></param>
        /// <param name="loadedFiles"></param>
        /// <param name="associatedMod"></param>
        /// <param name="mergeWeightDelegate">Callback to submit completed weight for progress tracking</param>
        /// <param name="mergeStatusDelegate">Callback to submit a new text string to display</param>
        /// <exception cref="Exception"></exception>
        public void ApplyChanges(GameTarget gameTarget, CaseInsensitiveDictionary<string> loadedFiles, Mod associatedMod, Action<int> mergeWeightDelegate, Action<string, string> addTrackedFileDelegate = null)
        {
            var targetFiles = new SortedSet<string>();
            if (ApplyToAllLocalizations)
            {
                var targetnameBase = Path.GetFileNameWithoutExtension(FileName).StripUnrealLocalization();
                var targetExtension = Path.GetExtension(FileName);
                var localizations = GameLanguage.GetLanguagesForGame(associatedMod.Game);

                // Ensure end name is not present on base
                //foreach (var l in localizations)
                //{
                //    if (targetnameBase.EndsWith($@"_{l.FileCode}", StringComparison.InvariantCultureIgnoreCase))
                //        targetnameBase = targetnameBase.Substring(0, targetnameBase.Length - (l.FileCode.Length + 1)); //_FileCode
                //}

                var hasOneFile = false;
                foreach (var l in localizations)
                {
                    var targetname = $@"{targetnameBase}_{l.FileCode}{targetExtension}";
                    hasOneFile |= addMergeTarget(targetname, loadedFiles, targetFiles, mergeWeightDelegate);
                }

                if (!hasOneFile)
                {
                    throw new Exception(M3L.GetString(M3L.string_interp_mergefile_noLocalizedFiles, FileName));
                }
            }
            else
            {
                addMergeTarget(FileName, loadedFiles, targetFiles, mergeWeightDelegate);
            }

            var mac = new MergeAssetCache1();
            foreach (string f in targetFiles)
            {
                M3Log.Information($@"Opening package {f}");
#if DEBUG
                var sw = Stopwatch.StartNew();
#endif
                // Open as memorystream as we need to hash this file for tracking
                using var ms = new MemoryStream(File.ReadAllBytes(f));

                var existingMD5 = M3Utilities.CalculateMD5(ms);
                var package = MEPackageHandler.OpenMEPackageFromStream(ms, f);
#if DEBUG
                Debug.WriteLine($@"Opening package {f} took {sw.ElapsedMilliseconds} ms");
#endif
                foreach (var pc in MergeChanges)
                {
                    pc.ApplyChanges(package, mac, associatedMod, gameTarget, mergeWeightDelegate);
                }

                var track = package.IsModified;
                if (package.IsModified)
                {
                    M3Log.Information($@"Saving package {package.FilePath}");
#if DEBUG
                    sw.Restart();
#endif
                    package.Save(savePath: f, compress: true);
#if DEBUG
                    Debug.WriteLine($@"Saving package {f} took {sw.ElapsedMilliseconds} ms");
#endif
                }
                else
                {
                    M3Log.Information($@"Package {package.FilePath} was not modified. This change is likely already installed, not saving package");
                }

                if (track)
                {
                    // Add to basegame database.
                    addTrackedFileDelegate?.Invoke(existingMD5, f);
                }

            }
        }

        private bool addMergeTarget(string fileName, CaseInsensitiveDictionary<string> loadedFiles,
            SortedSet<string> targetFiles, Action<int> mergeWeightDelegate)
        {
            if (loadedFiles.TryGetValue(fileName, out string fullpath))
            {
                targetFiles.Add(fullpath);
                return true;
            }
            M3Log.Warning($@"File not found in game: {fileName}, skipping...");
            mergeWeightDelegate?.Invoke(GetMergeWeightSingle()); // Skip this weight
            return false;
        }

        public int GetMergeCount() => ApplyToAllLocalizations ? GameLanguage.GetLanguagesForGame(OwningMM.Game).Length : 1;


        /// <summary>
        /// Gets the merge weight for a single file (not to all localizations)
        /// </summary>
        /// <returns></returns>
        public int GetMergeWeightSingle()
        {
            var weight = 0;

            foreach (var v in MergeChanges)
            {
                weight += v.GetMergeWeight();
            }

            Debug.WriteLine($@"Single merge weight for {FileName}: weight");
            return weight;
        }

        public int GetMergeWeight()
        {
            // Merge weight is the number of files to merge multiplied by the amount of a single merge
            var multiplier = ApplyToAllLocalizations ? GameLanguage.GetLanguagesForGame(OwningMM.Game).Length : 1;
            return multiplier * GetMergeWeightSingle();
        }


        /// <summary>
        /// Validates this MergeFile. Throws an exception if the validation fails.
        /// </summary>
        public void Validate()
        {
            if (FileName == null) throw new Exception(M3L.GetString(M3L.string_filenameCannotBeNullInAMergeManifestFile!));
            var safeFiles = MergeModLoader.GetAllowedMergeTargetFilenames(Parent.Game);

            if (!safeFiles.Any(x => FileName.StartsWith(Path.GetFileNameWithoutExtension(x), StringComparison.InvariantCultureIgnoreCase)))
            {
                // Does this catch DLC startups? 
                throw new Exception(M3L.GetString(M3L.string_interp_targetingNonStartupFile, FileName));
            }

            foreach (var mc in MergeChanges)
            {
                mc.Validate();
            }
        }
    }
}
