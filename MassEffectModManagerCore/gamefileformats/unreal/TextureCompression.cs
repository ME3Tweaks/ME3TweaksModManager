/*
 * Copyright (C) 2019 Pawel Kolodziejski
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

using System.IO;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MassEffectModManagerCore.modmanager.helpers;

namespace ME3Explorer.Unreal
{
    /// <summary>
    /// Storage Types for Texture2D
    /// </summary>
    public enum StorageTypes
    {
        pccUnc = StorageFlags.noFlags,                                     // ME1 (Compressed PCC), ME2 (Compressed PCC)
        pccLZO = StorageFlags.compressedLZO,                               // ME1 (Uncompressed PCC)
        pccZlib = StorageFlags.compressedZlib,                             // ME1 (Uncompressed PCC)
        extUnc = StorageFlags.externalFile,                                // ME3 (DLC TFC archive)
        extLZO = StorageFlags.externalFile | StorageFlags.compressedLZO,   // ME1 (Reference to PCC), ME2 (TFC archive)
        extZlib = StorageFlags.externalFile | StorageFlags.compressedZlib, // ME3 (non-DLC TFC archive)
        empty = StorageFlags.externalFile | StorageFlags.unused,           // ME1, ME2, ME3
    }

    /// <summary>
    /// Storage type flags for Texture2D
    /// </summary>
    [Flags]
    public enum StorageFlags
    {
        noFlags = 0,
        externalFile = 1 << 0,
        compressedZlib = 1 << 1,
        compressedLZO = 1 << 4,
        unused = 1 << 5,
    }

    static public class TextureCompression
    {
        const uint textureTag = 0x9E2A83C1;
        const uint maxBlockSize = 0x20000; // 128KB

        const int SizeOfChunkBlock = 8;
        public struct ChunkBlock
        {
            public uint comprSize;
            public uint uncomprSize;
            public byte[] compressedBuffer;
            public byte[] uncompressedBuffer;
        }

        const int SizeOfChunk = 16;
        public struct Chunk
        {
            public uint uncomprOffset;
            public uint uncomprSize;
            public uint comprOffset;
            public uint comprSize;
            public List<ChunkBlock> blocks;
        }

        public static byte[] CompressTexture(byte[] inputData, StorageTypes type)
        {
            using (MemoryStream ouputStream = new MemoryStream())
            {
                uint compressedSize = 0;
                uint dataBlockLeft = (uint)inputData.Length;
                uint newNumBlocks = ((uint)inputData.Length + maxBlockSize - 1) / maxBlockSize;
                List<ChunkBlock> blocks = new List<ChunkBlock>();
                using (MemoryStream inputStream = new MemoryStream(inputData))
                {
                    // skip blocks header and table - filled later
                    ouputStream.Seek(SizeOfChunk + SizeOfChunkBlock * newNumBlocks, SeekOrigin.Begin);

                    for (int b = 0; b < newNumBlocks; b++)
                    {
                        ChunkBlock block = new ChunkBlock();
                        block.uncomprSize = Math.Min(maxBlockSize, dataBlockLeft);
                        dataBlockLeft -= block.uncomprSize;
                        block.uncompressedBuffer = inputStream.ReadToBuffer(block.uncomprSize);
                        blocks.Add(block);
                    }
                }

                Parallel.For(0, blocks.Count, b =>
                {
                    ChunkBlock block = blocks[b];
                    if (type == StorageTypes.extLZO || type == StorageTypes.pccLZO)
                        block.compressedBuffer = LZO2Helper.LZO2.Compress(block.uncompressedBuffer);
                    else if (type == StorageTypes.extZlib || type == StorageTypes.pccZlib)
                        block.compressedBuffer = ZlibHelper.Zlib.Compress(block.uncompressedBuffer);
                    else
                        throw new Exception("Compression type not expected!");
                    if (block.compressedBuffer.Length == 0)
                        throw new Exception("Compression failed!");
                    block.comprSize = (uint)block.compressedBuffer.Length;
                    blocks[b] = block;
                });

                for (int b = 0; b < blocks.Count; b++)
                {
                    ChunkBlock block = blocks[b];
                    ouputStream.Write(block.compressedBuffer, 0, (int)block.comprSize);
                    compressedSize += block.comprSize;
                }

                ouputStream.SeekBegin();
                ouputStream.WriteUInt32(textureTag);
                ouputStream.WriteUInt32(maxBlockSize);
                ouputStream.WriteUInt32(compressedSize);
                ouputStream.WriteInt32(inputData.Length);
                foreach (ChunkBlock block in blocks)
                {
                    ouputStream.WriteUInt32(block.comprSize);
                    ouputStream.WriteUInt32(block.uncomprSize);
                }

                return ouputStream.ToArray();
            }
        }

        public static void DecompressTexture(byte[] DecompressedBuffer, MemoryStream stream, StorageTypes type, int uncompressedSize, int compressedSize)
        {
            uint blockTag = stream.ReadUInt32();
            if (blockTag != textureTag)
                throw new Exception("Texture tag wrong");
            uint blockSize = stream.ReadUInt32();
            if (blockSize != maxBlockSize)
                throw new Exception("Texture header broken");
            uint compressedChunkSize = stream.ReadUInt32();
            uint uncompressedChunkSize = stream.ReadUInt32();
            if (uncompressedChunkSize != uncompressedSize)
                throw new Exception("Texture header broken");

            uint blocksCount = (uncompressedChunkSize + maxBlockSize - 1) / maxBlockSize;
            if ((compressedChunkSize + SizeOfChunk + SizeOfChunkBlock * blocksCount) != compressedSize)
                throw new Exception("Texture header broken");

            var blocks = new List<ChunkBlock>();
            for (uint b = 0; b < blocksCount; b++)
            {
                ChunkBlock block = new ChunkBlock
                {
                    comprSize = stream.ReadUInt32(),
                    uncomprSize = stream.ReadUInt32()
                };
                blocks.Add(block);
            }

            for (int b = 0; b < blocks.Count; b++)
            {
                ChunkBlock block = blocks[b];
                block.compressedBuffer = stream.ReadToBuffer(blocks[b].comprSize);
                block.uncompressedBuffer = new byte[maxBlockSize * 2];
                blocks[b] = block;
            }

            Parallel.For(0, blocks.Count, b =>
            {
                uint dstLen;
                ChunkBlock block = blocks[b];
                if (type == StorageTypes.extLZO || type == StorageTypes.pccLZO)
                    dstLen = LZO2Helper.LZO2.Decompress(block.compressedBuffer, block.comprSize, block.uncompressedBuffer);
                else if (type == StorageTypes.extZlib || type == StorageTypes.pccZlib)
                    dstLen = ZlibHelper.Zlib.Decompress(block.compressedBuffer, block.comprSize, block.uncompressedBuffer);
                else
                    throw new Exception("Compression type not expected!");
                if (dstLen != block.uncomprSize)
                    throw new Exception("Decompressed data size not expected!");
            });

            int dstPos = 0;
            for (int b = 0; b < blocks.Count; b++)
            {
                Buffer.BlockCopy(blocks[b].uncompressedBuffer, 0, DecompressedBuffer, dstPos, (int)blocks[b].uncomprSize);
                dstPos += (int)blocks[b].uncomprSize;
            }
        }
    }
}
