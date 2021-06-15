using System.Linq;
using System.Text;
using LegendaryExplorerCore.Misc;

namespace MassEffectModManagerCore.modmanager.helpers
{
    /// <summary>
    /// The code from this class is from https://github.com/ME3Explorer/ME3Explorer/blob/v2.0.10/ME3Explorer/PAREditor/PAREditor.cs
    /// </summary>
    class PARTools
    {
        private static string parEncKey = @"q@pO3o#5jNA6$sjP3qwe1";

        /// <summary>
        /// Decodes a .par file to it's ini contents
        /// </summary>
        /// <param name="parEncryptedFile"></param>
        /// <returns></returns>
        public static DuplicatingIni DecodePAR(byte[] parEncryptedFile)
        {
            int PARFileKeyPosition = 0;
            byte[] PARFileContentToProcess;
            char[] PARFileXORedContent = new char[parEncryptedFile.Length];
            byte[] PARFileKeyByteArray = Encoding.UTF8.GetBytes(parEncKey);
            if (parEncryptedFile[0] != 0x2A || //Magic? CRC?
                parEncryptedFile[1] != 0x02 ||
                parEncryptedFile[2] != 0x11 ||
                parEncryptedFile[3] != 0x3C)
                PARFileContentToProcess = parEncryptedFile.Skip(4).ToArray();
            else
                PARFileContentToProcess = parEncryptedFile;
            for (int i = 0; i < PARFileContentToProcess.Length; i++)
            {
                PARFileXORedContent[i] = (char)(PARFileContentToProcess[i] ^ PARFileKeyByteArray[PARFileKeyPosition]);
                PARFileKeyPosition = ((PARFileKeyPosition + 1) % PARFileKeyByteArray.Length);
            }

            var decPar = new string(PARFileXORedContent);
            return DuplicatingIni.ParseIni(decPar);
        }
        
        /// <summary>
        /// Encodes a string of byes to a .par file. This does NOT calculate the checksum!
        /// </summary>
        /// <param name="parContentsUTF8"></param>
        /// <returns></returns>
        //public byte[] EncodePAR(byte[] parContentsUTF8)
        //{
        //    int PARFileKeyPosition = 0;
        //    byte[] PARFileXORedContent = new byte[parContentsUTF8.Length];
        //    byte[] PARFileKeyByteArray = Encoding.UTF8.GetBytes(parEncKey);
        //    for (int i = 0; i < parContentsUTF8.Length; i++)
        //    {
        //        PARFileXORedContent[i] = (byte)(PARFileKeyByteArray[PARFileKeyPosition] ^ parContentsUTF8[i]);
        //        PARFileKeyPosition = ((PARFileKeyPosition + 1) % PARFileKeyByteArray.Length);
        //    }
        //    return PARFileXORedContent;
        //}
    }
}
