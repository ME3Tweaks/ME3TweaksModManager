using System.IO;
using MassEffectModManagerCore.modmanager.helpers;

namespace MassEffectModManagerCore.GameDirectories

{
    public enum EMountFileFlag
    {
        ME2_NoSaveFileDependency = 0x0, //Based on tajfun research
        ME2_SaveFileDependency = 0x2, //Based on tajfun research
        ME3_SPOnly_NoSaveFileDependency = 0x8,
        ME3_SPOnly_SaveFileDependency = 0x9,
        ME3_SPMP_SaveFileDependency = 0x1C,
        ME3_MPOnly_Patch = 0x0C,
        ME3_MPOnly_1 = 0x14,
        ME3_MPOnly_2 = 0x34,
        ME1_SaveFileDependency = 0x100, //not an actual value.
        ME1_NoSaveFileDependency = 0x101 //not an actual value.
    }

    public class MountFile
    {
        public bool IsME2 { get; set; }
        public ushort MountPriority { get; set; }
        public string ME2Only_DLCFolderName { get; set; }
        public string ME2Only_DLCHumanName { get; set; }
        public int TLKID { get; set; }
        public EMountFileFlag MountFlag { get; set; }
        /// <summary>
        /// Instantiates an empty mount file. Used for creating a new mount.
        /// </summary>
        public MountFile()
        {

        }

        /// <summary>
        /// Instantiates a mount file from a mount file on disk.
        /// </summary>
        /// <param name="filepath">Mountfile to load</param>
        public MountFile(string filepath)
        {
            using (MemoryStream ms = new MemoryStream(File.ReadAllBytes(filepath)))
            {
                byte b = (byte)ms.ReadByte();
                if (b == 0)
                {
                    LoadMountFileME2(ms);
                }
                else
                {
                    LoadMountFileME3(ms);
                }
            }
        }

        public static ushort GetMountPriority(string filepath) => new MountFile(filepath).MountPriority;

        private void LoadMountFileME2(MemoryStream ms)
        {
            IsME2 = true;
            ms.Seek(0x28, SeekOrigin.Begin);
            MountFlag = (EMountFileFlag)ms.ReadByte();
            ms.Seek(0xC, SeekOrigin.Begin);
            MountPriority = ms.ReadUInt16();
            ms.Seek(0x2C, SeekOrigin.Begin);
            ME2Only_DLCHumanName = ms.ReadStringASCIINull(ms.ReadInt32());
            TLKID = ms.ReadInt32();
            ME2Only_DLCFolderName = ms.ReadStringASCIINull(ms.ReadInt32());

        }

        private void LoadMountFileME3(MemoryStream ms)
        {
            ms.Seek(0x10, SeekOrigin.Begin);
            MountPriority = ms.ReadUInt16();
            ms.Seek(0x18, SeekOrigin.Begin);
            MountFlag = (EMountFileFlag)ms.ReadByte();
            ms.Seek(0x1C, SeekOrigin.Begin);
            TLKID = ms.ReadInt32();
        }

        /// <summary>
        /// Writes this Mountfile to the specified path.
        /// </summary>
        /// <param name="path">Path to write mount file to</param>
        public void WriteMountFile(string path)
        {
            MemoryStream ms = new MemoryStream();
            if (IsME2)
            {
                ms.WriteByte(0x0);

                //@ 0x01 - Mount Flag
                // NOT ACTUALY MOUNT FLAG IT SEEMS, ACCORDING TO TAJFUN.
                ms.WriteByte(0x2);
                ms.WriteInt16(0x0);

                //@ 0x04
                ms.WriteInt32(0x82);
                ms.WriteInt32(0x40);

                //@ 0x0C - Mount Priority
                ms.WriteUInt16(MountPriority);
                ms.WriteInt16(0x0);

                //@ 0x10
                ms.WriteInt32(0x03);

                //@ 0x14 - Appears to be a GUID. Common across all DLC though. Maybe some sort of magic GUID or something.
                var guidbytes = new byte[] { 0xAE, 0x0F, 0x43, 0xDD, 0x0B, 0x52, 0x5D, 0x4C, 0x9E, 0x28, 0x0D, 0x77, 0x6D, 0x86, 0x91, 0x55 };
                ms.WriteFromBuffer(guidbytes);
                ms.WriteInt32(0x0);

                //@ 0x28 - Mount Flag
                ms.WriteInt32((int) MountFlag);

                //@ 0x2C - Common Name
                //ms.WriteInt32(commonname.Length);
                ms.WriteUnrealStringASCII(ME2Only_DLCHumanName);

                //@ 0x00 After CommonName - TLK ID
                ms.WriteInt32(TLKID);

                //@ 0x00 After TLKID - FolderName
                //ms.WriteInt32(dlcfolder.Length);
                ms.WriteUnrealStringASCII(ME2Only_DLCFolderName);

                //@ Final 4 bytes
                ms.WriteInt32(0x0);
            }
            else
            {
                ms.WriteInt32(0x1);
                ms.WriteInt32(0x2AC);
                ms.WriteInt32(0xC2);
                ms.WriteInt32(0x3006B);

                //@ 0x10 - Mount Priority
                ms.WriteUInt16(MountPriority);
                ms.WriteUInt16(0x0);
                ms.WriteInt32(0x0);

                //@ 0x18 - Mount Flag
                ms.WriteInt32((byte)MountFlag); //Write as 32-bit since the rest is just zeros anyways.

                //@ 0x1C - TLK ID (x2)
                ms.WriteInt32(TLKID);
                ms.WriteInt32(TLKID);
                ms.WriteInt32(0x0);
                ms.WriteInt32(0x0);

                //@ 0x2C - Unknown, Possible double GUID?
                // Also all remaining zeros.
                var guidbytes = new byte[] { 0x5A, 0x7B, 0xBD, 0x26, 0xDD, 0x41, 0x7E, 0x49, 0x9C, 0xC6, 0x60, 0xD2, 0x58, 0x72, 0x78, 0xEB, 0x2E, 0x2C, 0x6A, 0x06, 0x13, 0x0A, 0xE4, 0x47, 0x83, 0xEA, 0x08, 0xF3, 0x87, 0xA0, 0xE2, 0xDA, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 };
                ms.WriteFromBuffer(guidbytes);

            }
            File.WriteAllBytes(path, ms.ToArray());
        }
    }
}