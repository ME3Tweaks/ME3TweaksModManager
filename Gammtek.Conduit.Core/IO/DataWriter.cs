using System.IO;
using System.Text;

namespace Gammtek.Conduit.IO
{
	public class DataWriter : BinaryWriter
	{
		public new static readonly DataWriter Null = new DataWriter();
		private ByteOrder _byteOrder;

		public DataWriter(Stream output, ByteOrder byteOrder = ByteOrder.LittleEndian, Encoding encoding = null, bool leaveOpen = false)
			: base(output, encoding ?? new UTF8Encoding(), leaveOpen)
		{
			ByteOrder = byteOrder;
		}

		protected DataWriter()
		{
			ByteOrder = ByteOrder.LittleEndian;
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

		public virtual long Seek(long offset, SeekOrigin origin = SeekOrigin.Begin)
		{
			return BaseStream.Seek(offset, origin);
		}

		public override void Write(bool value)
		{
			Write(Converter.GetBytes(value), 0, 1);
		}

		public override void Write(decimal value)
		{
			Write(Converter.GetBytes(value), 0, 16);
		}

		public override void Write(double value)
		{
			Write(Converter.GetBytes(value), 0, 8);
		}

		public override void Write(float value)
		{
			Write(Converter.GetBytes(value), 0, 4);
		}

		public override void Write(int value)
		{
			Write(Converter.GetBytes(value), 0, 4);
		}

		public override void Write(long value)
		{
			Write(Converter.GetBytes(value), 0, 8);
		}

		public override void Write(short value)
		{
			Write(Converter.GetBytes(value), 0, 2);
		}

		public override void Write(uint value)
		{
			Write(Converter.GetBytes(value), 0, 4);
		}

		public override void Write(ulong value)
		{
			Write(Converter.GetBytes(value), 0, 8);
		}

		public override void Write(ushort value)
		{
			Write(Converter.GetBytes(value), 0, 2);
		}
	}
}
