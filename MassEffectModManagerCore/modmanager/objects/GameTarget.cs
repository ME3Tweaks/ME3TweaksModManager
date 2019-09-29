using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Windows.Media;
using MassEffectModManagerCore.modmanager.helpers;
using Serilog;

namespace MassEffectModManagerCore.modmanager.objects
{

    public class GameTarget : IEqualityComparer<GameTarget>, INotifyPropertyChanged
    {
        public const uint MEMI_TAG = 0x494D454D;

        private static readonly Color ME1BackgroundColor = Color.FromArgb(80, 181, 181, 181);
        private static readonly Color ME2BackgroundColor = Color.FromArgb(80, 255, 176, 171);
        private static readonly Color ME3BackgroundColor = Color.FromArgb(80, 196, 24, 24);

        public event PropertyChangedEventHandler PropertyChanged;

        public Mod.MEGame Game { get; }
        public string TargetPath { get; }
        public bool RegistryActive { get; set; }
        public Brush BackgroundColor
        {
            get
            {
                if (RegistryActive)
                {
                    switch (Game)
                    {
                        case Mod.MEGame.ME1:
                            return new SolidColorBrush(ME1BackgroundColor);
                        case Mod.MEGame.ME2:
                            return new SolidColorBrush(ME2BackgroundColor);
                        case Mod.MEGame.ME3:
                            return new SolidColorBrush(ME3BackgroundColor);
                    }
                }
                return null;
            }
        }

        public bool Selectable { get; internal set; } = true;
        public string ALOTVersion { get; private set; }

        public GameTarget(Mod.MEGame game, string target, bool currentRegistryActive)
        {
            this.Game = game;
            this.RegistryActive = currentRegistryActive;
            this.TargetPath = target.TrimEnd('\\');
            if (game != Mod.MEGame.Unknown)
            {
                var alotInfo = GetInstalledALOTInfo();
                if (alotInfo != null)
                {
                    ALOTInstalled = true;
                    ALOTVersion = alotInfo.ToString();
                }
            }
        }

        public bool Equals(GameTarget x, GameTarget y)
        {
            return x.TargetPath == y.TargetPath && x.Game == y.Game;
        }

        public int GetHashCode(GameTarget obj)
        {
            return obj.TargetPath.GetHashCode();
        }

        public bool ALOTInstalled { get; private set; }

        public ALOTVersionInfo GetInstalledALOTInfo()
        {
            string gamePath = getALOTMarkerFilePath();
            if (gamePath != null && File.Exists(gamePath))
            {
                try
                {
                    using (FileStream fs = new FileStream(gamePath, System.IO.FileMode.Open, FileAccess.Read))
                    {
                        fs.SeekEnd();
                        long endPos = fs.Position;
                        fs.Position = endPos - 4;
                        uint memi = fs.ReadUInt32();

                        if (memi == MEMI_TAG)
                        {
                            //ALOT has been installed
                            fs.Position = endPos - 8;
                            int installerVersionUsed = fs.ReadInt32();
                            int perGameFinal4Bytes = -20;
                            switch (Game)
                            {
                                case Mod.MEGame.ME1:
                                    perGameFinal4Bytes = 0;
                                    break;
                                case Mod.MEGame.ME2:
                                    perGameFinal4Bytes = 4352;
                                    break;
                                case Mod.MEGame.ME3:
                                    perGameFinal4Bytes = 16777472;
                                    break;
                            }

                            if (installerVersionUsed >= 10 && installerVersionUsed != perGameFinal4Bytes) //default bytes before 178 MEMI Format
                            {
                                fs.Position = endPos - 12;
                                short ALOTVER = fs.ReadInt16();
                                byte ALOTUPDATEVER = (byte)fs.ReadByte();
                                byte ALOTHOTFIXVER = (byte)fs.ReadByte();

                                //unused for now
                                fs.Position = endPos - 16;
                                int MEUITMVER = fs.ReadInt32();

                                return new ALOTVersionInfo(ALOTVER, ALOTUPDATEVER, ALOTHOTFIXVER, MEUITMVER);
                            }
                            else
                            {
                                return new ALOTVersionInfo(0, 0, 0, 0); //MEMI tag but no info we know of
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    Log.Error($"Error reading ALOT marker file for {Game}. ALOT Info will be returned as null (nothing installed). " + e.Message);
                    return null;
                }
            }
            return null;
        }

        private string getALOTMarkerFilePath()
        {
            switch (Game)
            {
                case Mod.MEGame.ME1:
                    return Path.Combine(TargetPath, @"BioGame\CookedPC\testVolumeLight_VFX.upk");
                case Mod.MEGame.ME2:
                    return Path.Combine(TargetPath, @"BioGame\CookedPC\BIOC_Materials.pcc");
                case Mod.MEGame.ME3:
                    return Path.Combine(TargetPath, @"BIOGame\CookedPCConsole\adv_combat_tutorial_xbox_D_Int.afc");
                default:
                    throw new Exception("Unknown game to find ALOT marker for!");
            }
        }
    }
}
