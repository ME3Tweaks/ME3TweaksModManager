using System.IO;
using LegendaryExplorerCore.Coalesced;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal;
using ME3TweaksModManager.modmanager.save.game2.FileFormats;
using ME3TweaksModManager.modmanager.save.game2;
using ME3TweaksModManager.modmanager.save.game3;
using ME3TweaksModManager.modmanager.save.le1;

namespace ME3TweaksModManager.modmanager.save
{
    public class SaveFileLoader
    {
        /// <summary>
        /// Reads a save file from the given stream to an ISaveFile object
        /// </summary>
        /// <param name="stream"></param>
        /// <param name="game"></param>
        /// <param name="fileName"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public static ISaveFile LoadSaveFile(Stream stream, MEGame game, string fileName = null)
        {
            var version = stream.ReadUInt32();
            stream.Position -= 4;
            switch (version)
            {
                case 50: // LE1 decompressed
                case 0x9E2A83C1: // LE1 compressed
                    return Read(stream, fileName, MEGame.LE1);
                case 29:
                    // This is ME2... wonder how it's different...
                    return Read(stream, fileName, MEGame.ME2);
                case 30:
                    return Read(stream, fileName, MEGame.LE2);
                case 59: // ME3/LE3 share the same format
                    return Read(stream, fileName, MEGame.LE3);
                default:
                    throw new Exception($@"Save version not supported: {version}");

            }

            return null;
        }

        private static ISaveFile Read(Stream input, string fileName = null, MEGame expectedGame = MEGame.Unknown)
        {

            if (input == null)
            {
                throw new ArgumentNullException("input");
            }

            ISaveFile save = null;
            uint saveVersion = 0;
            switch (expectedGame)
            {
                case MEGame.LE1:
                    saveVersion = 50;
                    save = new LE1SaveFile();
                    break;
                case MEGame.ME2:
                    saveVersion = 29;
                    save = new SaveFileGame2();
                    break;
                case MEGame.LE2:
                    saveVersion = 30;
                    save = new LE2SaveFile();
                    break;
                case MEGame.ME3:
                case MEGame.LE3:
                    saveVersion = 59;
                    save = new SaveFileGame3();
                    break;
                default:
                    throw new Exception(@"Loader not implemented");
            }

            save.SaveFilePath = fileName;

            if (fileName != null)
            {
                // Setup save params
                var sgName = Path.GetFileNameWithoutExtension(fileName);

                if (expectedGame == MEGame.LE1)
                {
                    if (sgName.EndsWith(@"AutoSave"))
                    {
                        save.SaveGameType = ESFXSaveGameType.SaveGameType_Auto;
                    }
                    else
                    {
                        // Parse number
                        var numStr = sgName.Substring(sgName.LastIndexOf("_") + 1);
                        if (int.TryParse(numStr, out var saveNum))
                        {
                            save.SaveNumber = saveNum;
                            save.SaveGameType = ESFXSaveGameType.SaveGameType_Manual;
                        }
                    }
                }
                else
                {
                    if (sgName.StartsWith(@"Save_"))
                    {
                        // Parse number
                        var numStr = sgName.Substring(sgName.IndexOf("_") + 1);
                        if (int.TryParse(numStr, out var saveNum))
                        {
                            save.SaveNumber = saveNum;
                            save.SaveGameType = ESFXSaveGameType.SaveGameType_Manual;
                        }
                    }
                    else if (sgName.StartsWith(@"AutoSave"))
                    {
                        save.SaveGameType = ESFXSaveGameType.SaveGameType_Auto;
                    }
                    else if (sgName.StartsWith(@"ChapterSave"))
                    {
                        save.SaveGameType = ESFXSaveGameType.SaveGameType_Chapter;
                    }
                    else if (sgName.StartsWith(@"QuickSave"))
                    {
                        save.SaveGameType = ESFXSaveGameType.SaveGameType_Quick;
                    }
                }
            }


            var reader = new UnrealStream(input, true, saveVersion);
            save.Serialize(reader);

            // Have to figure out how to handle this later since it has decompression.

            var finalPos = input.Position;
            var crc = input.ReadUInt32();
            if (expectedGame == MEGame.LE1)
            {
                input.ReadInt32(); // Compression Flag (should be ZLib 0)
                input.ReadInt32(); // Uncompressed Size
            }

            if (input.Position != input.Length)
            {
                save.IsValid = false;
#if DEBUG
                throw new FormatException("did not consume entire file");
#endif
            }

            input.Position = 0;

            var calculatedCRC = Crc32.Compute(input.ReadToBuffer(finalPos));
            if (crc != calculatedCRC)
            {
                save.IsValid = false;
            }

            return save;
        }
    }
}
