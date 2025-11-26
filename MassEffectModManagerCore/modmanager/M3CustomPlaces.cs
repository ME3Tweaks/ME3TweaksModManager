using Microsoft.Win32;

namespace ME3TweaksModManager.modmanager
{
    internal static class M3CustomPlaces
    {
        // From LEX
        //public static List<FileDialogCustomPlace> GameCustomPlaces
        //{
        //    get
        //    {
        //        List<FileDialogCustomPlace> list = new List<FileDialogCustomPlace>();
        //        if (ME1Directory.DefaultGamePath != null && Directory.Exists(ME1Directory.DefaultGamePath)) list.Add(new FileDialogCustomPlace(ME1Directory.DefaultGamePath));
        //        if (ME2Directory.DefaultGamePath != null && Directory.Exists(ME2Directory.DefaultGamePath)) list.Add(new FileDialogCustomPlace(ME2Directory.DefaultGamePath));
        //        if (ME3Directory.DefaultGamePath != null && Directory.Exists(ME3Directory.DefaultGamePath)) list.Add(new FileDialogCustomPlace(ME3Directory.DefaultGamePath));
        //        if (LE1Directory.DefaultGamePath != null && Directory.Exists(LE1Directory.DefaultGamePath)) list.Add(new FileDialogCustomPlace(LE1Directory.DefaultGamePath));
        //        if (LE2Directory.DefaultGamePath != null && Directory.Exists(LE2Directory.DefaultGamePath)) list.Add(new FileDialogCustomPlace(LE2Directory.DefaultGamePath));
        //        if (LE3Directory.DefaultGamePath != null && Directory.Exists(LE3Directory.DefaultGamePath)) list.Add(new FileDialogCustomPlace(LE3Directory.DefaultGamePath));

        //        // Useful place: ME3Tweaks Mod Manager library
        //        var m3settingsFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), @"ME3TweaksModManager", @"settings.ini");
        //        if (File.Exists(m3settingsFile))
        //            try
        //            {
        //                DuplicatingIni ini = DuplicatingIni.LoadIni(m3settingsFile);
        //                var libraryPath = ini.GetValue(@"ModLibrary", @"LibraryPath");
        //                if (libraryPath.HasValue && Directory.Exists(libraryPath.Value))
        //                {
        //                    list.Add(new FileDialogCustomPlace(libraryPath.Value));
        //                }
        //            }
        //            catch (Exception)
        //            {
        //                // Don't do anything
        //            }

        //        return list;
        //    }
        //}

        public static List<FileDialogCustomPlace> TextureLibraryCustomPlace
        {
            get
            {
                List<FileDialogCustomPlace> list = new List<FileDialogCustomPlace>();
                list.Add(new FileDialogCustomPlace(@M3LoadedMods.GetTextureLibraryDirectory()));
                return list;
            }
        }

    }
}
