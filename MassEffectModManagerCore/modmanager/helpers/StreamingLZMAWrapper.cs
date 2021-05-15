using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LegendaryExplorerCore.Compression;
using LegendaryExplorerCore.Helpers;
using SevenZip;

namespace MassEffectModManagerCore.modmanager.helpers
{
    /// <summary>
    /// Wrapper class that can decompress an LZMA stream. Works with Streaming and NonStreaming LZMA (LegendaryExplorerCore can only do LZMA)
    /// </summary>
    public static class StreamingLZMAWrapper
    {
        /// <summary>
        /// Decompresses LZMA. Uses native code if non-streamed (produced by lzma.exe), uses managed code if streamed (which can be done by things such as PHP)
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        public static byte[] DecompressLZMA(MemoryStream input)
        {
            input.Position = 5;
            var lzmaLen = input.ReadInt32();
            input.Position = 0;
            if (lzmaLen > 0)
            {
                // Non streamed LZMA
                return LZMA.DecompressLZMAFile(input.ToArray());
            }
            else
            {
                // It's streaming lzma. MEM code can't handle streamed so we have to fallback
                var lzmads = new LzmaDecodeStream(input);
                using var decompressedStream = new MemoryStream();
                int bufSize = 24576, count;
                byte[] buf = new byte[bufSize];
                while (/*lzmads.Position < lzmaFile.Length && */(count = lzmads.Read(buf, 0, bufSize)) > 0)
                {
                    decompressedStream.Write(buf, 0, count);
                }
                return decompressedStream.ToArray();
            }
        }
    }
}
