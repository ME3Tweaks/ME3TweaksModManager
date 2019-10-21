using MassEffectModManagerCore.modmanager.helpers;
using System;
using System.IO;
using System.Threading.Tasks;
using ZlibHelper;

namespace AmaroK86.MassEffect3.ZlibBlock
{
    public static class ZBlock
    {
        public static readonly uint magic = 0x9E2A83C1;
        public static readonly uint maxSegmentSize = 0x20000;

        /*
         * Name function: Compress
         * Purpose: compress a part of the byte array into a Zlib Block
         * Input: - buffer: byte array
         * Output: compressed byte array block, the structure is:
         *         - magic word
         *         - max segment size
         *         - total compressed size
         *         - total uncompressed size
         *         - segment list
         *         - compressed data list
         */
        public static byte[] Compress(byte[] buffer)
        {
            if (buffer == null)
                throw new ArgumentNullException();

            MemoryStream headBlock = new MemoryStream();
            MemoryStream dataBlock = new MemoryStream();

            int numSeg = (int)Math.Ceiling(buffer.Length / (double)maxSegmentSize);

            headBlock.WriteUInt32(magic);
            headBlock.WriteUInt32(maxSegmentSize);
            headBlock.WriteUInt32(0x0);            //total compressed size, still to calculate
            headBlock.WriteInt32(buffer.Length);          //total uncompressed size

            int offset = 0;
            for (int i = buffer.Length; i > 0; i -= (int)maxSegmentSize)
            {
                int copyBytes = Math.Min(i, (int)maxSegmentSize);
                uint precCompSize = (uint)dataBlock.Length;
                byte[] src = new byte[copyBytes];
                Buffer.BlockCopy(buffer, offset, src, 0, copyBytes);
                byte[] dst = Zlib.Compress(src);
                if (dst.Length == 0)
                    throw new Exception("Zlib compression failed!");

                dataBlock.Write(dst);
                offset += dst.Length;
                headBlock.WriteUInt32((uint)dst.Length); //compressed segment size
                headBlock.WriteInt32(copyBytes); //uncompressed segment size
                //Console.WriteLine("  Segment size: {0}, total read: {1}, compr size: {2}", maxSegmentSize, copyBytes, (uint)dataBlock.Length - precCompSize);
            }

            headBlock.Seek(8, SeekOrigin.Begin);
            headBlock.WriteInt32((int)dataBlock.Length); // total compressed size

            byte[] finalBlock = new byte[headBlock.Length + dataBlock.Length];
            Buffer.BlockCopy(headBlock.ToArray(), 0, finalBlock, 0, (int)headBlock.Length);
            Buffer.BlockCopy(dataBlock.ToArray(), 0, finalBlock, (int)headBlock.Length, (int)dataBlock.Length);
            headBlock.Close();
            dataBlock.Close();

            return finalBlock;
        }

        /*
         * Name function: Compress
         * Purpose: compress a part of the stream into a Zlib Block
         * Input: - inStream: input Stream
         *        - count: num of bytes to compress starting from the Stream position
         * Output: compressed byte array block, the structure is:
         *         - magic word
         *         - max segment size
         *         - total compressed size
         *         - total uncompressed size
         *         - segment list
         *         - compressed data list
         */
        public static byte[] Compress(Stream inStream, int count)
        {
            if (count < 0)
                throw new FormatException();
            if (inStream.Position + count > inStream.Length)
                throw new ArgumentOutOfRangeException();
            byte[] buffer = new byte[count];
            inStream.Read(buffer, 0, count);
            return Compress(buffer);
        }

        public static Task<byte[]> DecompressAsync(byte[] buffer)
        {
            if (buffer == null)
                throw new ArgumentNullException();
            return Task.Run(() =>
            {
                return Decompress(buffer);
            });
        }

        public static byte[] Decompress(byte[] buffer)
        {
            if (buffer == null)
                throw new ArgumentNullException();

            using (MemoryStream buffStream = new MemoryStream(buffer))
            {
                uint magicStream = buffStream.ReadUInt32();
                if (magicStream != magic)
                {
                    throw new InvalidDataException("found an invalid zlib block");
                }

                uint buffMaxSegmentSize = buffStream.ReadUInt32();
                if (buffMaxSegmentSize != maxSegmentSize)
                {
                    throw new FormatException();
                }

                uint totComprSize = buffStream.ReadUInt32();
                uint totUncomprSize = buffStream.ReadUInt32();

                byte[] outputBuffer = new byte[totUncomprSize];
                int numOfSegm = (int)Math.Ceiling(totUncomprSize / (double)maxSegmentSize);
                int headSegm = 16;
                int dataSegm = headSegm + (numOfSegm * 8);
                int buffOff = 0;

                for (int i = 0; i < numOfSegm; i++)
                {
                    buffStream.Seek(headSegm, SeekOrigin.Begin);
                    int comprSegm = buffStream.ReadInt32();
                    int uncomprSegm = buffStream.ReadInt32();
                    headSegm = (int)buffStream.Position;

                    buffStream.Seek(dataSegm, SeekOrigin.Begin);
                    //Console.WriteLine("compr size: {0}, uncompr size: {1}, data offset: 0x{2:X8}", comprSegm, uncomprSegm, dataSegm);
                    byte[] src = buffStream.ReadToBuffer(comprSegm);
                    byte[] dst = new byte[uncomprSegm];
                    if (Zlib.Decompress(src, (uint)src.Length, dst) != uncomprSegm)
                        throw new Exception("Zlib decompression failed!");

                    Buffer.BlockCopy(dst, 0, outputBuffer, buffOff, uncomprSegm);

                    buffOff += uncomprSegm;
                    dataSegm += comprSegm;
                }
                buffStream.Close();
                return outputBuffer;
            }
        }

        public static byte[] Decompress(Stream inStream, int count)
        {
            if (count < 0)
                throw new FormatException();
            if (inStream.Position + count > inStream.Length)
                return new byte[count];
            byte[] buffer = new byte[count];
            inStream.Read(buffer, 0, count);
            return Decompress(buffer);
        }
    }
}
