using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using MassEffectModManagerCore.modmanager.helpers;
using MassEffectModManagerCore.modmanager.objects.mod.merge;
using Serilog;

namespace MassEffectModManagerCore.modmanager.objects.mod
{
    public partial class Mod
    {
        public IMergeMod LoadMergeMod(string fullPath)
        {
            Stream sourceStream;
            if (IsInArchive)
            {
                // Check archive lists it as Store. If any other format we will not load this mod as it was improperly deployed
                var storageType = Archive.GetStorageTypeOfFile(fullPath);
                if (storageType != @"Copy")
                {
                    Log.Error($@"Mod has merge that is in an archive, but the storage type is not listed as 'Copy'. Mod Manager will not load mods from archive that contain merge mods that were not deployed using Mod Manager.");
                    LoadFailedReason = $@"This mod has a merge mod that is in the archive, but the storage type is not listed as 'Copy'. Mod Manager will not load mods from archive that contain merge mods that were not deployed using Mod Manager.";
                    return null;
                }

                // Load from archive
                sourceStream = new MemoryStream();
                Archive.ExtractFile(fullPath, sourceStream);
                sourceStream.Position = 0;
            }
            else
            {
                // Load from disk
                sourceStream = File.OpenRead(fullPath);
            }

            IMergeMod mm = null;
            try
            {
                // if modpath = "" there is no starting /
                int extraLen = ModPath.Length > 0 ? 2 : 1;
                mm = MergeModLoader.LoadMergeMod(sourceStream, fullPath.Substring(ModPath.Length + extraLen + Mod.MergeModFolderName.Length), IsInArchive);
            }
            catch (Exception e)
            {
                Log.Error($@"Exception loading merge mod {fullPath}: {e.Message}");
                Log.Error(e.StackTrace);
                LoadFailedReason = $@"An error occurred reading referenced merge mod '{fullPath}': {e.Message}. See the logs for more information.";
                return null;
            }
            finally
            {
                if (!IsInArchive)
                {
                    // Do not keep stream open
                    sourceStream.Dispose();
                }
            }

            return mm;
        }
    }
}
