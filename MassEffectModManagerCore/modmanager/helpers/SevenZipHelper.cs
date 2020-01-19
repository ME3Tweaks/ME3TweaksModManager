/*
 * SevenZip Helper
 *
 * Copyright (C) 2015 Pawel Kolodziejski <aquadran at users.sourceforge.net>
 *
 * This program is free software; you can redistribute it and/or
 * modify it under the terms of the GNU General Public License
 * as published by the Free Software Foundation; either version 2
 * of the License, or (at your option) any later version.

 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.

 * You should have received a copy of the GNU General Public License
 * along with this program; if not, write to the Free Software
 * Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA 02110-1301, USA.
 *
 */

using MassEffectModManagerCore.modmanager.helpers;
using SevenZip;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace SevenZipHelper
{
    public static class LZMA
    {
        [DllImport("sevenzipwrapper.dll", CharSet = CharSet.Auto, CallingConvention = CallingConvention.Cdecl)]
        private static extern int SevenZipDecompress([In] byte[] srcBuf, uint srcLen, [Out] byte[] dstBuf, ref uint dstLen);

        [DllImport("sevenzipwrapper.dll", CharSet = CharSet.Auto, CallingConvention = CallingConvention.Cdecl)]
        private static extern int SevenZipCompress(int compressionLevel, [In] byte[] srcBuf, uint srcLen, [Out] byte[] dstBuf, ref uint dstLen);


        public static byte[] Decompress(byte[] src, uint dstLen)
        {
            uint len = dstLen;
            byte[] dst = new byte[dstLen];

            int status = SevenZipDecompress(src, (uint)src.Length, dst, ref len);
            if (status != 0)
                return new byte[0];

            return dst;
        }

        public static byte[] Compress(byte[] src, int compressionLevel = 9)
        {
            uint dstLen = (uint)(src.Length * 2 + 8);
            byte[] tmpbuf = new byte[dstLen];

            int status = SevenZipCompress(compressionLevel, src, (uint)src.Length, tmpbuf, ref dstLen);
            if (status != 0)
                return new byte[0];

            byte[] dst = new byte[dstLen];
            Array.Copy(tmpbuf, dst, (int)dstLen);

            return dst;
        }

        /// <summary>
        /// Compresses the input data and returns LZMA compressed data, with the proper header for an LZMA file.
        /// </summary>
        /// <param name="src">Source data</param>
        /// <returns>Byte array of compressed data</returns>

        public static byte[] CompressToLZMAFile(byte[] src)
        {
            var compressedBytes = SevenZipHelper.LZMA.Compress(src);
            byte[] fixedBytes = new byte[compressedBytes.Length + 8]; //needs 8 byte header written into it (only mem version needs this)
            Buffer.BlockCopy(compressedBytes, 0, fixedBytes, 0, 5);
            fixedBytes.OverwriteRange(5, BitConverter.GetBytes(src.Length));
            Buffer.BlockCopy(compressedBytes, 5, fixedBytes, 13, compressedBytes.Length - 5);
            return fixedBytes;
        }

        internal static byte[] DecompressLZMAFile(byte[] lzmaFile)
        {
            int len = (int)BitConverter.ToInt32(lzmaFile, 5); //this is technically a 32-bit but since MEM code can't handle 64 bit sizes we are just going to use 32bit.

            if (len >= 0)
            {
                byte[] strippedData = new byte[lzmaFile.Length - 8];
                //Non-Streamed (made from disk)
                Buffer.BlockCopy(lzmaFile, 0, strippedData, 0, 5);
                Buffer.BlockCopy(lzmaFile, 13, strippedData, 5, lzmaFile.Length - 13);
                return Decompress(strippedData, (uint)len);
            }
            else if (len == -1)
            {
                //Streamed. MEM code can't handle streamed so we have to fallback
                var lzmads = new LzmaDecodeStream(new MemoryStream(lzmaFile));
                using var decompressedStream = new MemoryStream();
                int bufSize = 24576, count;
                byte[] buf = new byte[bufSize];
                while (/*lzmads.Position < lzmaFile.Length && */(count = lzmads.Read(buf, 0, bufSize)) > 0)
                {
                    decompressedStream.Write(buf, 0, count);
                }
                return decompressedStream.ToArray();
            }
            else
            {
                Debug.WriteLine("Cannot decompress LZMA array: Length is not positive or -1 (" + len + ")! This is not an LZMA array");
                return null; //Not LZMA!
            }
        }

        internal static void DecompressLZMAStream(MemoryStream compressedStream, MemoryStream decompressedStream)
        {
            compressedStream.Seek(5, SeekOrigin.Begin);
            int len = compressedStream.ReadInt32();
            compressedStream.Seek(0, SeekOrigin.Begin);

            if (len >= 0)
            {
                byte[] strippedData = new byte[compressedStream.Length - 8];
                compressedStream.Read(strippedData, 0, 5);
                compressedStream.Seek(8, SeekOrigin.Current); //Skip 8 bytes for length.
                compressedStream.Read(strippedData, 5, (int)compressedStream.Length - 13);
                var decompressed = Decompress(strippedData, (uint)len);
                decompressedStream.Write(decompressed);
            }
            else if (len == -1)
            {
                SevenZipExtractor.DecompressStream(compressedStream, decompressedStream, null, null);
            }
            else
            {
                Debug.WriteLine("LZMA Stream to decompess has wrong length: " + len);
            }
        }
    }
}
