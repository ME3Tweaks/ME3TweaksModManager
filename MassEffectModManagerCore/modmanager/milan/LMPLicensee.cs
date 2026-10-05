using System.Buffers.Binary;
using LegendaryExplorerCore.Helpers;

namespace ME3TweaksModManager.modmanager.milan
{
    /// <summary>
    /// Converts LE3 packages to LMP packages by changing the licensee version in their header. Everything Project Milan
    /// builds on (MilanDelta, slash) is generated against packages that have already been converted, so the installer does
    /// this itself, and the deltas never contain the change.
    /// </summary>
    internal static class LMPLicensee
    {
        /// <summary>
        /// Offset of the licensee version (ushort) in a package header: it follows the magic (uint) and file version (ushort).
        /// </summary>
        private const int LicenseeOffset = 0x6;

        /// <summary>
        /// If this file is a package, and so must be converted.
        /// </summary>
        public static bool AppliesTo(string path) => path.RepresentsPackageFilePath();

        /// <summary>
        /// Sets the LMP licensee version in a package held in memory.
        /// </summary>
        public static void Set(Span<byte> packageData)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(packageData.Slice(LicenseeOffset, sizeof(ushort)), MEPackage.LMPLicenseeVersion);
        }

        /// <summary>
        /// Sets the LMP licensee version in a package file on disk.
        /// </summary>
        public static void Set(string packagePath)
        {
            Span<byte> licensee = stackalloc byte[sizeof(ushort)];
            BinaryPrimitives.WriteUInt16LittleEndian(licensee, MEPackage.LMPLicenseeVersion);
            using var stream = new FileStream(packagePath, FileMode.Open, FileAccess.Write);
            stream.Seek(LicenseeOffset, SeekOrigin.Begin);
            stream.Write(licensee);
        }

        /// <summary>
        /// Wraps a package stream so it reads as if the LMP licensee version had been set, without copying or modifying it.
        /// Used to patch the 1.3GB RefShaderCache straight from LE3 into the installation.
        /// </summary>
        public static Stream WrapForReading(Stream packageStream) => new LicenseeOverrideStream(packageStream);

        /// <summary>
        /// Read-only, seekable view of a stream with the licensee bytes substituted.
        /// </summary>
        private sealed class LicenseeOverrideStream : Stream
        {
            private readonly Stream inner;
            private readonly byte[] licensee = new byte[sizeof(ushort)];

            public LicenseeOverrideStream(Stream inner)
            {
                this.inner = inner;
                BinaryPrimitives.WriteUInt16LittleEndian(licensee, MEPackage.LMPLicenseeVersion);
            }

            public override int Read(byte[] buffer, int offset, int count) => Read(buffer.AsSpan(offset, count));

            public override int Read(Span<byte> buffer)
            {
                long start = inner.Position;
                int read = inner.Read(buffer);

                // Substitute whatever part of the licensee falls within the range just read
                for (int i = 0; i < licensee.Length; i++)
                {
                    long index = LicenseeOffset + i - start;
                    if (index >= 0 && index < read)
                        buffer[(int)index] = licensee[i];
                }
                return read;
            }

            public override long Seek(long offset, SeekOrigin origin) => inner.Seek(offset, origin);
            public override bool CanRead => true;
            public override bool CanSeek => inner.CanSeek;
            public override bool CanWrite => false;
            public override long Length => inner.Length;

            public override long Position
            {
                get => inner.Position;
                set => inner.Position = value;
            }

            public override void Flush() { }
            public override void SetLength(long value) => throw new NotSupportedException();
            public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        }
    }
}
