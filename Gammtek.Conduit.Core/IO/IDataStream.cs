using System.Text;

namespace Gammtek.Conduit.IO
{
	public interface IDataStream
	{
		bool ReadBoolean();

		byte ReadByte();

		byte[] ReadBytes(int count);

		char ReadChar();

		char[] ReadChars(int count);

		decimal ReadDecimal();

		double ReadDouble();

		short ReadInt16();

		int ReadInt32();

		long ReadInt64();

		//object ReadObject();

		sbyte ReadSByte();

		float ReadSingle();

		ushort ReadUInt16();

		uint ReadUInt32();

		ulong ReadUInt64();

		void Write(bool value);

		void Write(byte value);

		void Write(byte[] value);

		void Write(char value, Encoding encoding = null);

		void Write(char[] value, Encoding encoding = null);

		void Write(decimal value);

		void Write(double value);

		void Write(float value);

		void Write(int value);

		void Write(long value);

		//void Write(object value);

		void Write(sbyte value);

		void Write(short value);

		void Write(uint value);

		void Write(ulong value);

		void Write(ushort value);
	}
}