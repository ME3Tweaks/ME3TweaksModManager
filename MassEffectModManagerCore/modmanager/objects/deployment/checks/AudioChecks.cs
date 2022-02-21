using LegendaryExplorerCore.GameFilesystem;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal;
using ME3TweaksCore.GameFilesystem;
using ME3TweaksCore.Services;
using ME3TweaksModManager.modmanager.diagnostics;
using ME3TweaksModManager.modmanager.helpers;
using ME3TweaksModManager.modmanager.localizations;
using Microsoft.AppCenter.Crashes;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ME3TweaksModManager.modmanager.objects.deployment.checks
{
    internal static class AudioChecks
    {
        public static void AddAudioChecks(EncompassingModDeploymentCheck check)
        {
            if (check.ModBeingDeployed.Game.IsGame2() || check.ModBeingDeployed.Game.IsGame3())
            {
                check.DeploymentChecklistItems.Add(new DeploymentChecklistItem()
                {
                    ItemText = M3L.GetString(M3L.string_audioCheck),
                    ModToValidateAgainst = check.ModBeingDeployed,
                    internalValidationTarget = check.internalValidationTarget,
                    ValidationFunction = CheckAFCs,
                    DialogMessage = M3L.GetString(M3L.string_audioCheckDetectedErrors),
                    DialogTitle = M3L.GetString(M3L.string_audioIssuesDetectedInMod)
                }); ;
            }
        }


        /// <summary>
        /// Checks for broken audio
        /// </summary>
        /// <param name="item"></param>
        private static void CheckAFCs(DeploymentChecklistItem item)
        {
            item.ItemText = M3L.GetString(M3L.string_checkingAudioReferencesInMod);
            var referencedFiles = item.ModToValidateAgainst.GetAllRelativeReferences().Select(x => Path.Combine(item.ModToValidateAgainst.ModPath, x)).ToList();
            int numChecked = 0;

            Predicate<string> predicate = s => s.ToLowerInvariant().EndsWith(@".afc", true, null);
            List<string> gameFiles = M3Directories.EnumerateGameFiles(item.internalValidationTarget, predicate);

            Dictionary<string, MemoryStream> cachedAudio = new Dictionary<string, MemoryStream>();

            foreach (var f in referencedFiles)
            {
                if (item.CheckDone) return;
                numChecked++;
                item.ItemText = $@"{M3L.GetString(M3L.string_checkingAudioReferencesInMod)} [{numChecked}/{referencedFiles.Count}]";
                if (f.RepresentsPackageFilePath())
                {
                    var relativePath = f.Substring(item.ModToValidateAgainst.ModPath.Length + 1);
                    M3Log.Information(@"Checking file for audio issues: " + f);
                    var package = MEPackageHandler.OpenMEPackage(f);
                    var wwiseStreams = package.Exports.Where(x => x.ClassName == @"WwiseStream" && !x.IsDefaultObject).ToList();
                    foreach (var wwisestream in wwiseStreams)
                    {
                        if (item.CheckDone) return;
                        //Check each reference.
                        var afcNameProp = wwisestream.GetProperty<NameProperty>(@"Filename");
                        if (afcNameProp != null)
                        {
                            string afcNameWithExtension = afcNameProp + @".afc";
                            int audioSize = BitConverter.ToInt32(wwisestream.Data, wwisestream.Data.Length - 8);
                            int audioOffset = BitConverter.ToInt32(wwisestream.Data, wwisestream.Data.Length - 4);

                            string afcPath = null;
                            Stream audioStream = null;
                            var localDirectoryAFCPath = Path.Combine(Path.GetDirectoryName(wwisestream.FileRef.FilePath), afcNameWithExtension);
                            bool isInOfficialArea = false;
                            if (File.Exists(localDirectoryAFCPath))
                            {
                                //local afc
                                afcPath = localDirectoryAFCPath;
                            }
                            else if (referencedFiles.Any(x => Path.GetFileName(x).Equals(afcNameWithExtension, StringComparison.InvariantCultureIgnoreCase)))
                            {
                                //found afc in mod.
                                //if there is multiple same-named AFCs in the mod, this might fail.
                                afcPath = Path.Combine(item.ModToValidateAgainst.ModPath, referencedFiles.FirstOrDefault(x => Path.GetFileName(x).Equals(afcNameWithExtension, StringComparison.InvariantCultureIgnoreCase)));
#if DEBUG
                                if (!File.Exists(afcPath))
                                {
                                    Debugger.Break();
                                }
#endif
                            }
                            else
                            {
                                //Check game
                                var fullPath = gameFiles.FirstOrDefault(x => Path.GetFileName(x).Equals(afcNameWithExtension, StringComparison.InvariantCultureIgnoreCase));
                                if (fullPath != null)
                                {
                                    afcPath = fullPath;
                                    isInOfficialArea = M3Directories.IsInBasegame(afcPath, item.internalValidationTarget) || M3Directories.IsInOfficialDLC(afcPath, item.internalValidationTarget);
                                }
                                else if (cachedAudio.TryGetValue(afcNameProp.Value.Name, out var cachedAudioStream))
                                {
                                    audioStream = cachedAudioStream;
                                    //isInOfficialArea = true; //cached from vanilla SFAR
                                }
                                else if (MEDirectories.OfficialDLC(item.internalValidationTarget.Game).Any(x => afcNameProp.Value.Name.StartsWith(x)))
                                {
                                    var dlcName = afcNameProp.Value.Name.Substring(0, afcNameProp.Value.Name.LastIndexOf(@"_", StringComparison.InvariantCultureIgnoreCase));
                                    var audio = VanillaDatabaseService.FetchFileFromVanillaSFAR(dlcName, afcNameWithExtension /*, ValidationTarget*/);
                                    if (audio != null)
                                    {
                                        cachedAudio[afcNameProp.Value.Name] = audio;
                                    }

                                    audioStream = audio;
                                    //isInOfficialArea = true; as this is in a vanilla SFAR we don't test against this since it will be correct.
                                    continue;
                                }
                                else
                                {
                                    M3Log.Warning($@"Could not find AFC file {afcNameProp.ToString()}.afc. Export: {wwisestream.UIndex} {wwisestream.ObjectName}");
                                    item.AddSignificantIssue(M3L.GetString(M3L.string_interp_couldNotFindReferencedAFC, relativePath, wwisestream.InstancedFullPath, afcNameProp.ToString()));
                                    continue;
                                }
                            }

                            if (afcPath != null)
                            {
                                audioStream = new FileStream(afcPath, FileMode.Open);
                            }

                            try
                            {
                                audioStream.Seek(audioOffset, SeekOrigin.Begin);
                                if (audioStream.Position > audioStream.Length - 4)
                                {
                                    M3Log.Warning($@"Found broken audio: {wwisestream.UIndex} {wwisestream.ObjectName} has broken audio, pointer points inside of AFC, but the size of it extends beyond the end of the AFC. Package file: {relativePath}, referenced AFC: {afcPath} @ 0x{audioOffset:X8}. The AFC is only 0x{audioStream.Length:X8} bytes long.");
                                    item.AddSignificantIssue(M3L.GetString(M3L.string_interp_invalidAudioPointerOutsideAFC, relativePath, wwisestream.UIndex, wwisestream.ObjectName, audioOffset, afcPath, audioStream.Length));
                                    if (audioStream is FileStream) audioStream.Close();
                                    continue;
                                }

                                if (audioStream.ReadStringASCIINull(4) != @"RIFF")
                                {
                                    M3Log.Warning($@"Found broken audio: {wwisestream.UIndex} {wwisestream.ObjectName} has broken audio, pointer points to data that does not start with RIFF, which is the start of audio data. Package file: {relativePath}, referenced AFC: {afcPath} @ 0x{audioOffset:X8}.");
                                    item.AddSignificantIssue(M3L.GetString(M3L.string_interp_invalidAudioPointer, relativePath, wwisestream.InstancedFullPath));
                                    if (audioStream is FileStream) audioStream.Close();
                                    continue;
                                }

                                //attempt to seek audio length.
                                audioStream.Seek(audioSize + 4, SeekOrigin.Current);

                                //Check if this file is in basegame
                                if (isInOfficialArea)
                                {
                                    //Verify offset is not greater than vanilla size
                                    var vanillaInfo = VanillaDatabaseService.GetVanillaFileInfo(item.internalValidationTarget, afcPath.Substring(item.internalValidationTarget.TargetPath.Length + 1));
                                    if (vanillaInfo == null)
                                    {
                                        Crashes.TrackError(new Exception($@"Vanilla information was null when performing vanilla file check for {afcPath.Substring(item.internalValidationTarget.TargetPath.Length + 1)}"));
                                    }

                                    if (audioOffset >= vanillaInfo[0].size)
                                    {
                                        M3Log.Warning($@"Found broken audio: {wwisestream.UIndex} {wwisestream.ObjectName} has broken audio, pointer points beyond the end of the AFC file. Package file: {relativePath}, referenced AFC: {afcPath} @ 0x{audioOffset:X8}.");
                                        item.AddSignificantIssue(M3L.GetString(M3L.string_interp_audioStoredInOfficialAFC, relativePath, wwisestream.InstancedFullPath));
                                    }
                                }

                                if (audioStream is FileStream) audioStream.Close();
                            }
                            catch (Exception e)
                            {
                                M3Log.Error($@"Error checking for broken audio: {wwisestream?.UIndex} {wwisestream?.ObjectName}. Package file: {relativePath}, referenced AFC: {afcPath} @ 0x{audioOffset:X8}. The error was: {e.Message}");
                                e.LogStackTrace();
                                if (audioStream is FileStream) audioStream.Close();
                                item.AddSignificantIssue(M3L.GetString(M3L.string_errorValidatingAudioReference, relativePath, wwisestream.InstancedFullPath, e.Message));
                                continue;
                            }
                        }
                    }
                }
            }


            if (!item.HasAnyMessages())
            {
                item.ItemText = M3L.GetString(M3L.string_noAudioIssuesWereDetected);
                item.ToolTip = M3L.GetString(M3L.string_validationOK);
            }
            else
            {
                item.ItemText = M3L.GetString(M3L.string_audioIssuesWereDetected);
                item.ToolTip = M3L.GetString(M3L.string_validationFailed);
            }

            cachedAudio.Clear();
        }
    }
}
