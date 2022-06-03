using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CliWrap;
using CliWrap.EventStream;
using Serilog;

// License: Do whatever you want with this.
namespace MassEffectModManagerCore.modmanager.helpers
{
    /// <summary>
    /// Class that can determine if a version of .NET Core is installed
    /// </summary>
    public class DotNetRuntimeVersionDetector
    {
        /// <summary>
        /// This is very windows specific
        /// </summary>
        /// <param name="desktopVersionsOnly">If it needs to filter to Windows Desktop versions only (WPF/Winforms).</param>
        /// <returns>List of versions matching the specified version</returns>
        public static async Task<Version[]> GetInstalledRuntimeVersions(bool desktopVersion)
        {
            try
            {
                var cmd = Cli.Wrap(@"dotnet.exe").WithArguments(@"--list-runtimes").WithValidation(CommandResultValidation.None);
                var runtimes = new List<Version>();
                await foreach (var cmdEvent in cmd.ListenAsync())
                {
                    switch (cmdEvent)
                    {
                        case StartedCommandEvent started:
                            break;
                        case StandardOutputCommandEvent stdOut:
                            if (string.IsNullOrWhiteSpace(stdOut.Text))
                            {
                                continue;
                            }

                            if (stdOut.Text.StartsWith(@"Microsoft.NETCore.App") && !desktopVersion)
                            {
                                runtimes.Add(parseVersion(stdOut.Text));
                            }
                            else if (stdOut.Text.StartsWith(@"Microsoft.WindowsDesktop.App") && desktopVersion)
                            {
                                runtimes.Add(parseVersion(stdOut.Text));
                            }

                            break;
                        case StandardErrorCommandEvent stdErr:
                            break;
                        case ExitedCommandEvent exited:
                            break;
                    }
                }
                return runtimes.ToArray();
            }
            catch (Exception e)
            {
                Log.Error($@"Error determining installed dotnet runtimes: {e.Message}");
                return Array.Empty<Version>();
            }

        }

        private static Version parseVersion(string stdOutText)
        {
            var split = stdOutText.Split(' ');
            return Version.Parse(split[1]); // 0 = SDK name, 1 = version, 2+ = path parts
        }
    }
}
