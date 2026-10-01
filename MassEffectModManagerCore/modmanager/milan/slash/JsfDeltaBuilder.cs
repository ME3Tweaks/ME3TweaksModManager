using System.Buffers.Binary;
using System.ComponentModel;

namespace ME3TweaksModManager.modmanager.milan.slash
{
    /// <summary>
    /// Builds JoJoDiff-format (.jsf) delta patches that can be applied by ME3TweaksCore's <c>JPatch.ApplyJPatch</c>.
    /// <para>
    /// Unlike jdiff, which only searches a few MB ahead of its current position, this indexes the entire source
    /// file, so it can find matches no matter how far apart they are. That matters for slashing: removing most of
    /// the RefShaderCache means the next kept record is often hundreds of MB further into the source, and a
    /// windowed diff would fall back to embedding the kept data as literal bytes - exactly the payload we are trying
    /// to avoid shipping.
    /// </para>
    /// <para>
    /// The algorithm is rsync-like: every B-aligned source block is hashed into an index, and a rolling hash over the
    /// destination looks up candidate matches, which are verified and then extended in both directions. Small in-place
    /// modifications (such as a changed 4-byte file offset) are handled by trying to realign at the same source/dest
    /// delta a few bytes past the mismatch, so they become tiny MOD operations instead of new index searches.
    /// </para>
    /// </summary>
    [Localizable(false)]
    internal static class JsfDeltaBuilder
    {
        // JoJoDiff opcodes. Every opcode is written as ESC followed by the opcode byte.
        private const byte OP_ESC = 0xA7;
        private const byte OP_MOD = 0xA6;
        private const byte OP_INS = 0xA5;
        private const byte OP_DEL = 0xA4;
        private const byte OP_EQL = 0xA3;
        private const byte OP_BKT = 0xA2;

        /// <summary>
        /// Number of bytes that must match at the previous source/dest delta for us to resume copying after a mismatch.
        /// </summary>
        private const int MinRealignRun = 8;

        /// <summary>
        /// Largest in-place modification (in bytes) we will bridge when realigning backwards from a found match.
        /// </summary>
        private const int MaxRealignGap = 32;

        /// <summary>
        /// Upper bound on the number of index entries. Block size grows with the source so large sources (the 1.3GB
        /// RefShaderCache) keep the index to a reasonable size.
        /// </summary>
        private const int MaxIndexEntries = 8_000_000;

        private const ulong HashMultiplier = 0x100000001B3UL;

        /// <summary>
        /// Computes a delta that transforms <paramref name="source"/> into <paramref name="dest"/> and writes it to <paramref name="output"/>.
        /// </summary>
        public static void Build(byte[] source, byte[] dest, Stream output)
        {
            var writer = new JsfWriter(output);
            int destLen = dest.Length;
            if (destLen == 0)
                return;

            int blockSize = ChooseBlockSize(source.Length);
            var index = BuildIndex(source, blockSize);
            ulong outgoingFactor = Pow(HashMultiplier, blockSize - 1);

            int pos = 0; // Current position in dest
            int literalStart = 0; // Start of the dest bytes that have not yet been emitted
            long lastDelta = 0; // source position - dest position of the last emitted copy
            ulong hash = 0;
            int hashPos = -1; // Dest position that 'hash' is valid for

            while (pos < destLen)
            {
                int candidate = -1;

                // 1. Try to continue at the same delta as the last match. This is what makes in-place changes cheap.
                long continuePos = pos + lastDelta;
                if (continuePos >= 0 && continuePos + MinRealignRun <= source.Length && pos + MinRealignRun <= destLen
                    && dest.AsSpan(pos, MinRealignRun).SequenceEqual(source.AsSpan((int)continuePos, MinRealignRun)))
                {
                    candidate = (int)continuePos;
                }
                else if (index != null && pos + blockSize <= destLen)
                {
                    // 2. Look the current window up in the source index.
                    hash = hashPos >= 0 && hashPos == pos - 1
                        ? RollHash(hash, dest[pos - 1], dest[pos + blockSize - 1], outgoingFactor)
                        : ComputeHash(dest, pos, blockSize);
                    hashPos = pos;

                    if (index.TryGetValue(MixHash(hash), out int indexedPos)
                        && dest.AsSpan(pos, blockSize).SequenceEqual(source.AsSpan(indexedPos, blockSize)))
                    {
                        candidate = indexedPos;
                    }
                }

                if (candidate < 0)
                {
                    pos++;
                    continue;
                }

                // Extend the match backwards into the pending literal bytes, then forwards as far as it goes.
                int back = 0;
                while (pos - back > literalStart && candidate - back > 0 && dest[pos - back - 1] == source[candidate - back - 1])
                {
                    back++;
                }

                int forward = dest.AsSpan(pos).CommonPrefixLength(source.AsSpan(candidate));
                int matchDestStart = pos - back;
                int matchSourceStart = candidate - back;
                int matchLength = back + forward;

                EmitLiteralThenCopy(writer, source, dest, literalStart, matchDestStart, matchSourceStart, matchLength);

                pos = matchDestStart + matchLength;
                literalStart = pos;
                lastDelta = (long)matchSourceStart - matchDestStart;
                hashPos = -1;
            }

            writer.Literal(dest, literalStart, destLen - literalStart, -1);
        }

        /// <summary>
        /// Convenience overload that returns the patch bytes.
        /// </summary>
        public static byte[] Build(byte[] source, byte[] dest)
        {
            using var ms = new MemoryStream();
            Build(source, dest, ms);
            return ms.ToArray();
        }

        /// <summary>
        /// Emits the literal region [literalStart, matchDestStart) followed by a copy. Before doing so, the literal
        /// region is scanned backwards for more data at the same delta as the match (separated by small modified gaps)
        /// so that e.g. a record header before a changed offset field is copied instead of stored.
        /// </summary>
        private static void EmitLiteralThenCopy(JsfWriter writer, byte[] source, byte[] dest, int literalStart, int matchDestStart, int matchSourceStart, int matchLength)
        {
            var segments = new List<(int destStart, int sourceStart, int length)> { (matchDestStart, matchSourceStart, matchLength) };

            int curDest = matchDestStart;
            int curSource = matchSourceStart;
            while (true)
            {
                bool found = false;
                for (int gap = 1; gap <= MaxRealignGap; gap++)
                {
                    int runDestEnd = curDest - gap;
                    int runSourceEnd = curSource - gap;
                    if (runDestEnd - MinRealignRun < literalStart || runSourceEnd - MinRealignRun < 0)
                        break;

                    if (dest.AsSpan(runDestEnd - MinRealignRun, MinRealignRun).SequenceEqual(source.AsSpan(runSourceEnd - MinRealignRun, MinRealignRun)))
                    {
                        int runLength = MinRealignRun;
                        while (runDestEnd - runLength > literalStart && runSourceEnd - runLength > 0 && dest[runDestEnd - runLength - 1] == source[runSourceEnd - runLength - 1])
                        {
                            runLength++;
                        }

                        curDest = runDestEnd - runLength;
                        curSource = runSourceEnd - runLength;
                        segments.Add((curDest, curSource, runLength));
                        found = true;
                        break;
                    }
                }

                if (!found)
                    break;
            }

            // Segments were collected back to front
            segments.Reverse();
            int emitted = literalStart;
            foreach (var segment in segments)
            {
                writer.Literal(dest, emitted, segment.destStart - emitted, segment.sourceStart);
                writer.Copy(segment.sourceStart, segment.length);
                emitted = segment.destStart + segment.length;
            }
        }

        private static int ChooseBlockSize(long sourceLength)
        {
            int blockSize = 32;
            while (blockSize < 4096 && sourceLength / blockSize > MaxIndexEntries)
            {
                blockSize *= 2;
            }
            return blockSize;
        }

        private static Dictionary<ulong, int> BuildIndex(byte[] source, int blockSize)
        {
            int blockCount = source.Length / blockSize;
            if (blockCount == 0)
                return null;

            var index = new Dictionary<ulong, int>(blockCount);
            for (int i = 0; i < blockCount; i++)
            {
                int offset = i * blockSize;
                // Keep the first occurrence; matches are always extended, so which duplicate we pick only affects patch size
                index.TryAdd(MixHash(ComputeHash(source, offset, blockSize)), offset);
            }
            return index;
        }

        private static ulong ComputeHash(byte[] data, int offset, int length)
        {
            ulong hash = 0;
            for (int i = 0; i < length; i++)
            {
                hash = hash * HashMultiplier + data[offset + i] + 1UL;
            }
            return hash;
        }

        private static ulong RollHash(ulong hash, byte outgoing, byte incoming, ulong outgoingFactor)
        {
            hash -= (outgoing + 1UL) * outgoingFactor;
            return hash * HashMultiplier + incoming + 1UL;
        }

        /// <summary>
        /// The polynomial hash is computed mod 2^64, which leaves its low bits poorly distributed. Mix before using it as a key.
        /// </summary>
        private static ulong MixHash(ulong hash)
        {
            hash ^= hash >> 33;
            hash *= 0xFF51AFD7ED558CCDUL;
            hash ^= hash >> 33;
            return hash;
        }

        private static ulong Pow(ulong value, int exponent)
        {
            ulong result = 1;
            for (int i = 0; i < exponent; i++)
            {
                result *= value;
            }
            return result;
        }

        /// <summary>
        /// Serializes operations in the JoJoDiff binary format, tracking the patcher's source cursor so copies can be
        /// positioned with relative DEL/BKT operations.
        /// </summary>
        private sealed class JsfWriter
        {
            private readonly Stream output;
            private long sourcePos;

            public JsfWriter(Stream output)
            {
                this.output = output;
            }

            /// <summary>
            /// Writes dest bytes that could not be copied. If the next copy starts exactly where the source cursor will be
            /// after skipping these bytes, MOD is used so the skip is free; otherwise INS is used.
            /// </summary>
            public void Literal(byte[] data, int start, int length, long nextCopySource)
            {
                if (length <= 0)
                    return;

                bool modify = nextCopySource >= 0 && nextCopySource == sourcePos + length;
                output.WriteByte(OP_ESC);
                output.WriteByte(modify ? OP_MOD : OP_INS);
                for (int i = start; i < start + length; i++)
                {
                    byte b = data[i];
                    output.WriteByte(b);
                    if (b == OP_ESC)
                    {
                        output.WriteByte(OP_ESC); // ESC ESC decodes to a single literal ESC
                    }
                }

                if (modify)
                {
                    sourcePos += length;
                }
            }

            public void Copy(long sourceStart, long length)
            {
                if (sourceStart > sourcePos)
                {
                    WriteOp(OP_DEL, sourceStart - sourcePos);
                }
                else if (sourceStart < sourcePos)
                {
                    WriteOp(OP_BKT, sourcePos - sourceStart);
                }

                WriteOp(OP_EQL, length);
                sourcePos = sourceStart + length;
            }

            private void WriteOp(byte opcode, long length)
            {
                // The patcher reads lengths as signed 32-bit values
                while (length > 0)
                {
                    long chunk = Math.Min(length, int.MaxValue);
                    output.WriteByte(OP_ESC);
                    output.WriteByte(opcode);
                    WriteLength(chunk);
                    length -= chunk;
                }
            }

            /// <summary>
            /// JoJoDiff length encoding, as decoded by JPatch.getJLength:
            /// 1-252: one byte (value - 1). 253-508: 252, (value - 253). up to 0xFFFF: 253, big-endian ushort. Else: 254, big-endian uint.
            /// </summary>
            private void WriteLength(long length)
            {
                if (length <= 252)
                {
                    output.WriteByte((byte)(length - 1));
                }
                else if (length <= 508)
                {
                    output.WriteByte(252);
                    output.WriteByte((byte)(length - 253));
                }
                else if (length <= 0xFFFF)
                {
                    output.WriteByte(253);
                    Span<byte> buf = stackalloc byte[2];
                    BinaryPrimitives.WriteUInt16BigEndian(buf, (ushort)length);
                    output.Write(buf);
                }
                else
                {
                    output.WriteByte(254);
                    Span<byte> buf = stackalloc byte[4];
                    BinaryPrimitives.WriteUInt32BigEndian(buf, (uint)length);
                    output.Write(buf);
                }
            }
        }
    }
}
