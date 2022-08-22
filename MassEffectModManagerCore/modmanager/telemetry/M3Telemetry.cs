using LegendaryExplorerCore.Gammtek.Extensions;
using LegendaryExplorerCore.Misc;
using ME3TweaksCore.Helpers;
using Pathoschild.FluentNexus.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ME3TweaksModManager.modmanager.telemetry
{
    /// <summary>
    /// Holds code for sending some telemetry data - used to move telemetry code out of main logic files
    /// </summary>
    internal class M3Telemetry
    {
        public static void SubmitScreenResolutionInfo(GameTarget target)
        {
            //Detect screen resolution - useful info for scene modders
            string resolution = @"Could not detect";
            var iniFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), @"BioWare");
            try
            {
                switch (target.Game)
                {
                    case MEGame.ME1:
                        {
                            iniFile = Path.Combine(iniFile, @"Mass Effect", @"Config", @"BIOEngine.ini");
                            if (File.Exists(iniFile))
                            {
                                var dini = DuplicatingIni.LoadIni(iniFile);
                                var section = dini.Sections.FirstOrDefault(x => x.Header == @"WinDrv.WindowsClient");
                                if (section != null)
                                {
                                    var resx = section.Entries.FirstOrDefault(x => x.Key == @"StartupResolutionX");
                                    var resy = section.Entries.FirstOrDefault(x => x.Key == @"StartupResolutionY");
                                    if (resx != null && resy != null)
                                    {
                                        resolution = $@"{resx.Value}x{resy.Value}";
                                    }
                                }
                            }
                        }
                        break;
                    case MEGame.ME2:
                    case MEGame.ME3:
                        {
                            iniFile = Path.Combine(iniFile, @"Mass Effect " + target.Game.ToGameNum(), @"BIOGame", @"Config", @"Gamersettings.ini");
                            if (File.Exists(iniFile))
                            {
                                var dini = DuplicatingIni.LoadIni(iniFile);
                                var section = dini.Sections.FirstOrDefault(x => x.Header == @"SystemSettings");
                                if (section != null)
                                {
                                    var resx = section.Entries.FirstOrDefault(x => x.Key == @"ResX");
                                    var resy = section.Entries.FirstOrDefault(x => x.Key == @"ResY");
                                    if (resx != null && resy != null)
                                    {
                                        resolution = $@"{resx.Value}x{resy.Value}";
                                    }
                                }
                            }
                        }
                        break;
                    case MEGame.LE1:
                    case MEGame.LE2:
                    case MEGame.LE3:
                        {
                            iniFile = Path.Combine(target.TargetPath, @"BioGame", @"Config", @"Gamersettings.ini");
                            if (File.Exists(iniFile))
                            {
                                var dini = DuplicatingIni.LoadIni(iniFile);
                                var section = dini.Sections.FirstOrDefault(x => x.Header == @"SystemSettings");
                                if (section != null)
                                {
                                    var resx = section.Entries.FirstOrDefault(x => x.Key == @"ResX");
                                    var resy = section.Entries.FirstOrDefault(x => x.Key == @"ResY");
                                    if (resx != null && resy != null)
                                    {
                                        resolution = $@"{resx.Value}x{resy.Value}";
                                    }
                                }
                            }
                        }
                        break;
                }

                TelemetryInterposer.TrackEvent(@"Launched game", new Dictionary<string, string>()
                {
                    {@"Game", target.Game.ToString()},
                    {@"Screen resolution", resolution}
                });
            }
            catch (Exception e)
            {
                M3Log.Error(@"Error trying to detect screen resolution: " + e.Message);
            }
        }
    }
}
