using System.IO;
using System.Text;

namespace Gammtek.Conduit.IO
{
	public class DataReader : BinaryReader
	{
		private ByteOrder _byteOrder;

		public DataReader(Stream input, ByteOrder byteOrder = ByteOrder.LittleEndian, Encoding encoding = null, bool leaveOpen = false)
			: base(input, encoding ?? new UTF8Encoding(), leaveOpen)
		{
			ByteOrder = byteOrder;
		}

		public ByteOrder ByteOrder
		{
			get { return _byteOrder; }
			set
			{
				Converter = (value == ByteOrder.BigEndian) ? ByteOrderConverter.BigEndian : ByteOrderConverter.LittleEndian;
				_byteOrder = value;
			}
		}

		public ByteOrderConverter Converter { get; protected set; }

		public virtual long Length => BaseStream.Length;

		public virtual long Position
		{
			get { return BaseStream.Position; }
			set { BaseStream.Position = value; }
		}

		public override bool ReadBoolean()
		{
			return Converter.ToBoolean(ReadBytes(1), 0);
		}

		public override decimal ReadDecimal()
		{
			return Converter.ToDecimal(ReadBytes(16), 0);
		}

		public override double ReadDouble()
		{
			return Converter.ToDouble(ReadBytes(8), 0);
		}

		public override short ReadInt16()
		{
			return Converter.ToInt16(ReadBytes(2), 0);
		}

		public override int ReadInt32()
		{
			return Converter.ToInt32(ReadBytes(4), 0);
		}

		public override long ReadInt64()
		{
			return Converter.ToInt64(ReadBytes(8), 0);
		}

		public override float ReadSingle()
		{
			return Converter.ToSingle(ReadBytes(4), 0);
		}

		public override ushort ReadUInt16()
		{
			return Converter.ToUInt16(ReadBytes(2), 0);
		}

		public override uint ReadUInt32()
		{
			return Converter.ToUInt32(ReadBytes(4), 0);
		}

		public override ulong ReadUInt64()
		{
			return Converter.ToUInt64(ReadBytes(8), 0);
		}

		public virtual long Seek(long offset, SeekOrigin origin = SeekOrigin.Begin)
		{
			return BaseStream.Seek(offset, origin);
		}

		public virtual byte[] ToArray()
		{
			using (var stream = new MemoryStream())
			{
				BaseStream.CopyTo(stream);

				return stream.ToArray();
			}
		}
	}
}
