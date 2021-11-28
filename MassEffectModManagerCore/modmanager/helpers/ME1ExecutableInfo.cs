using System.IO;
using LegendaryExplorerCore.Helpers;

//Taken from ALOT Installer V4
namespace ME3TweaksModManager.modmanager.helpers
{
    public class ME1ExecutableInfo
    {
        private ME1ExecutableInfo()
        {

        }

        /// <summary>
        /// enableLocalPhysXCore renamed to enableLocalPhysXCor_
        /// </summary>
        public bool HasPhysXCoreChanged { get; set; }
        /// <summary>
        /// Mass Effect renamed to Mass_Effect
        /// </summary>
        public bool HasProductNameChanged { get; set; }
        /// <summary>
        /// If LAA is applied or not
        /// </summary>
        public bool HasLAAApplied { get; set; }
        /// <summary>
        /// Hash of the reversed executable
        /// </summary>
        public string OriginalExecutableHash { get; set; }

        /// <summary>
        /// Gets information about Mass Effect's executalbe, based on what it originally was without MEM edits.
        /// </summary>
        /// <param name="executablePath"></param>
        /// <param name="calculateOriginalHash">Calculate MD5 of the reversed executable</param>
        /// <returns></returns>
        public static ME1ExecutableInfo GetExecutableInfo(string executablePath, bool calculateOriginalHash)
        {
            ME1ExecutableInfo info = new ME1ExecutableInfo();
            var s = new MemoryStream(File.ReadAllBytes(executablePath));
            if (s.Length == 0)
            {
                info.OriginalExecutableHash = @"Empty executable file";
                return info; // This is a blank executable. Do not operate on it
            }


            // Reverse LAA
            s.Seek(0x3C, SeekOrigin.Begin);
            var fileHeaderOffset = s.ReadUInt32() + 4;
            s.Seek(fileHeaderOffset + 0x12, SeekOrigin.Begin);
            var flag = s.ReadUInt16();
            if ((flag & 0x20) == 0x20)
            {
                info.HasLAAApplied = true;
            }
            s.Seek(-2, SeekOrigin.Current);
            ushort mask = 1 << 6; //0x20
            flag &= (ushort)~mask;
            s.WriteUInt16(flag);

            // Reverse Productname Mass_Effect to Mass Effect
            int productNameOffset = findUnicodeStrInBinary(s, @"Mass_Effect", 0x1000000);
            if (productNameOffset > 0)
            {
                info.HasProductNameChanged = true;
                //Debug.WriteLine($@"Mass_Effect @ 0x{productNameOffset:X8}");
                s.Seek(productNameOffset, SeekOrigin.Begin);
                s.WriteStringUnicode(@"Mass Effect");
            }

            // Reverse enableLocalPhysXCor_
            int enableLocalPhysXCor_Offset = findUnicodeStrInBinary(s, @"enableLocalPhysXCor_", 0x300000);
            if (enableLocalPhysXCor_Offset > 0)
            {
                info.HasPhysXCoreChanged = true;
                //Debug.WriteLine($@"enableLocalPhysXCor_ @ 0x{enableLocalPhysXCor_Offset:X8}");
                s.Seek(enableLocalPhysXCor_Offset, SeekOrigin.Begin);
                s.WriteStringUnicode(@"enableLocalPhysXCore");
            }

            if (calculateOriginalHash)
            {
                info.OriginalExecutableHash = M3Utilities.CalculateMD5(s);
            }

            return info;
        }

        private static int findUnicodeStrInBinary(MemoryStream s, string searchStr, int startPos)
        {
            var strLen = searchStr.Length;
            for (int i = startPos; i <= s.Length - strLen; i++) //Value will never appear before 0x1000000 so don't waste our time looking there
            {
                s.Seek(i, SeekOrigin.Begin);
                if (s.ReadByte() == searchStr[0])
                {
                    int j = 1; //Start at len 1
                    for (; j < strLen * 2; j++)
                    {
                        if (j % 2 == 1)
                        {
                            s.ReadByte();
                        }
                        else if (s.ReadByte() != searchStr[j / 2])
                        {
                            break;
                        }
                    }

                    if (j % 2 == 0 && j / 2 == strLen)
                    {
                        return i;
                    }
                }
            }
            return -1;
        }
    }
}
