using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Packages;

namespace ME3TweaksModManager.modmanager.save.game2.FileFormats
{
    public class UnrealStream : IUnrealStream
    {
        public Stream Stream { get; }
        public bool Loading { get; }
        public uint Version { get; private set; }

        public UnrealStream(Stream stream, bool loading, uint version)
        {
            this.Stream = stream;
            this.Loading = loading;
            this.Version = version;
        }

        public void Serialize(ref bool value)
        {
            if (this.Loading == true)
            {
                value = this.Stream.ReadInt32() != 0;
            }
            else
            {
                this.Stream.WriteUInt32(value ? 1u : 0u);
            }
        }

        public void Serialize(ref byte value)
        {
            if (this.Loading == true)
            {
                value = (byte)this.Stream.ReadByte(); //does not catch end of stream condition!
            }
            else
            {
                this.Stream.WriteByte(value);
            }
        }

        public void Serialize(ref int value)
        {
            if (this.Loading == true)
            {
                value = this.Stream.ReadInt32();
            }
            else
            {
                this.Stream.WriteInt32(value);
            }
        }

        public void Serialize(ref uint value)
        {
            if (this.Loading == true)
            {
                value = this.Stream.ReadUInt32();
            }
            else
            {
                this.Stream.WriteUInt32(value);
            }
        }

        public void Serialize(ref float value)
        {
            if (this.Loading == true)
            {
                value = this.Stream.ReadFloat();
            }
            else
            {
                this.Stream.WriteFloat(value);
            }
        }

        public void Serialize(ref string value)
        {
            if (this.Loading == true)
            {
                value = this.Stream.ReadUnrealString();
            }
            else
            {
                this.Stream.WriteUnrealString(value, MEGame.ME2);
            }
        }

        public void Serialize(ref Guid value)
        {
            if (this.Loading == true)
            {
                value = this.Stream.ReadGuid();
            }
            else
            {
                this.Stream.WriteGuid(value);
            }
        }

        public void SerializeEnum<TEnum>(ref TEnum value)
        {
            if (this.Loading == true)
            {
                value = this.Stream.ReadValueEnum<TEnum>();
            }
            else
            {
                this.Stream.WriteValueEnum<TEnum>(value);
            }
        }

        public void SerializeEnum<TEnum>(ref TEnum value, Func<IUnrealStream, bool> condition, Func<TEnum> defaultValue)
        {
            if (condition == null)
            {
                throw new ArgumentNullException("condition");
            }

            if (defaultValue == null)
            {
                throw new ArgumentNullException("defaultValue");
            }

            if (condition(this) == false)
            {
                this.SerializeEnum(ref value);
            }
            else
            {
                value = defaultValue();
            }
        }

        public void Serialize(ref bool value, Func<IUnrealStream, bool> condition, Func<bool> defaultValue)
        {
            if (condition == null)
            {
                throw new ArgumentNullException("condition");
            }

            if (defaultValue == null)
            {
                throw new ArgumentNullException("defaultValue");
            }

            if (condition(this) == false)
            {
                this.Serialize(ref value);
            }
            else
            {
                value = defaultValue();
            }
        }

        public void Serialize(ref byte value, Func<IUnrealStream, bool> condition, Func<byte> defaultValue)
        {
            if (condition == null)
            {
                throw new ArgumentNullException("condition");
            }

            if (defaultValue == null)
            {
                throw new ArgumentNullException("defaultValue");
            }

            if (condition(this) == false)
            {
                this.Serialize(ref value);
            }
            else
            {
                value = defaultValue();
            }
        }

        public void Serialize(ref int value, Func<IUnrealStream, bool> condition, Func<int> defaultValue)
        {
            if (condition == null)
            {
                throw new ArgumentNullException("condition");
            }

            if (defaultValue == null)
            {
                throw new ArgumentNullException("defaultValue");
            }

            if (condition(this) == false)
            {
                this.Serialize(ref value);
            }
            else
            {
                value = defaultValue();
            }
        }

        public void Serialize(ref uint value, Func<IUnrealStream, bool> condition, Func<uint> defaultValue)
        {
            if (condition == null)
            {
                throw new ArgumentNullException("condition");
            }

            if (defaultValue == null)
            {
                throw new ArgumentNullException("defaultValue");
            }

            if (condition(this) == false)
            {
                this.Serialize(ref value);
            }
            else
            {
                value = defaultValue();
            }
        }

        public void Serialize(ref float value, Func<IUnrealStream, bool> condition, Func<float> defaultValue)
        {
            if (condition == null)
            {
                throw new ArgumentNullException("condition");
            }

            if (defaultValue == null)
            {
                throw new ArgumentNullException("defaultValue");
            }

            if (condition(this) == false)
            {
                this.Serialize(ref value);
            }
            else
            {
                value = defaultValue();
            }
        }

        public void Serialize(ref string value, Func<IUnrealStream, bool> condition, Func<string> defaultValue)
        {
            if (condition == null)
            {
                throw new ArgumentNullException("condition");
            }

            if (defaultValue == null)
            {
                throw new ArgumentNullException("defaultValue");
            }

            if (condition(this) == false)
            {
                this.Serialize(ref value);
            }
            else
            {
                value = defaultValue();
            }
        }

        public void Serialize(ref Guid value, Func<IUnrealStream, bool> condition, Func<Guid> defaultValue)
        {
            if (condition == null)
            {
                throw new ArgumentNullException("condition");
            }

            if (defaultValue == null)
            {
                throw new ArgumentNullException("defaultValue");
            }

            if (condition(this) == false)
            {
                this.Serialize(ref value);
            }
            else
            {
                value = defaultValue();
            }
        }

        public void Serialize<TType>(ref TType value, Func<IUnrealStream, bool> condition, Func<TType> defaultValue)
            where TType : class, IUnrealSerializable, new()
        {
            if (condition == null)
            {
                throw new ArgumentNullException("condition");
            }

            if (defaultValue == null)
            {
                throw new ArgumentNullException("defaultValue");
            }

            if (condition(this) == false)
            {
                this.Serialize(ref value);
            }
            else
            {
                value = defaultValue();
                if (value == null)
                {
                    throw new ArgumentException("evaluated default value cannot be null", "defaultValue");
                }
            }
        }

        public void Serialize(ref BitArray list, Func<IUnrealStream, bool> condition, Func<BitArray> defaultList)
        {
            if (condition == null)
            {
                throw new ArgumentNullException("condition");
            }

            if (defaultList == null)
            {
                throw new ArgumentNullException("defaultList");
            }

            if (condition(this) == false)
            {
                this.Serialize(ref list);
            }
            else
            {
                list = defaultList();
                if (list == null)
                {
                    throw new ArgumentException("evaluated default list cannot be null", "defaultList");
                }
            }
        }

        public void Serialize(ref List<byte> list)
        {
            if (list == null)
            {
                throw new ArgumentNullException("list", "serializable list should not be null");
            }

            this.ReadBasicList(list, r => (byte)r.Stream.ReadByte());
        }

       private void ReadBasicList<TType>(List<TType> list, Func<IUnrealStream, TType> readValue)
        {
            var count = this.Stream.ReadUInt32();
            if (count >= 0x7FFFFF)
            {
                throw new FormatException("too many items in list");
            }

            list.Clear();
            for (uint i = 0; i < count; i++)
            {
                list.Add(readValue(this));
            }
        }

        public void Serialize(ref List<byte> list, Func<IUnrealStream, bool> condition, Func<List<byte>> defaultList)
        {
            if (condition == null)
            {
                throw new ArgumentNullException("condition");
            }

            if (defaultList == null)
            {
                throw new ArgumentNullException("defaultList");
            }

            if (condition(this) == false)
            {
                this.Serialize(ref list);
            }
            else
            {
                list = defaultList();
                if (list == null)
                {
                    throw new ArgumentException("evaluated default list cannot be null", "defaultList");
                }
            }
        }

        public void Serialize(ref List<int> list, Func<IUnrealStream, bool> condition, Func<List<int>> defaultList)
        {
            if (condition == null)
            {
                throw new ArgumentNullException("condition");
            }

            if (defaultList == null)
            {
                throw new ArgumentNullException("defaultList");
            }

            if (condition(this) == false)
            {
                this.Serialize(ref list);
            }
            else
            {
                list = defaultList();
                if (list == null)
                {
                    throw new ArgumentException("evaluated default list cannot be null", "defaultList");
                }
            }
        }

        public void Serialize(ref List<float> list, Func<IUnrealStream, bool> condition, Func<List<float>> defaultList)
        {
            if (condition == null)
            {
                throw new ArgumentNullException("condition");
            }

            if (defaultList == null)
            {
                throw new ArgumentNullException("defaultList");
            }

            if (condition(this) == false)
            {
                this.Serialize(ref list);
            }
            else
            {
                list = defaultList();
                if (list == null)
                {
                    throw new ArgumentException("evaluated default list cannot be null", "defaultList");
                }
            }
        }

        public void Serialize(ref List<string> list, Func<IUnrealStream, bool> condition,
            Func<List<string>> defaultList)
        {
            if (condition == null)
            {
                throw new ArgumentNullException("condition");
            }

            if (defaultList == null)
            {
                throw new ArgumentNullException("defaultList");
            }

            if (condition(this) == false)
            {
                this.Serialize(ref list);
            }
            else
            {
                list = defaultList();
                if (list == null)
                {
                    throw new ArgumentException("evaluated default list cannot be null", "defaultList");
                }
            }
        }

        public void Serialize(ref List<Guid> list, Func<IUnrealStream, bool> condition, Func<List<Guid>> defaultList)
        {
            if (condition == null)
            {
                throw new ArgumentNullException("condition");
            }

            if (defaultList == null)
            {
                throw new ArgumentNullException("defaultList");
            }

            if (condition(this) == false)
            {
                this.Serialize(ref list);
            }
            else
            {
                list = defaultList();
                if (list == null)
                {
                    throw new ArgumentException("evaluated default list cannot be null", "defaultList");
                }
            }
        }

        public void SerializeEnum<TEnum>(ref List<TEnum> list, Func<IUnrealStream, bool> condition,
            Func<List<TEnum>> defaultList)
        {
            if (condition == null)
            {
                throw new ArgumentNullException("condition");
            }

            if (defaultList == null)
            {
                throw new ArgumentNullException("defaultList");
            }

            if (condition(this) == false)
            {
                this.SerializeEnum(ref list);
            }
            else
            {
                list = defaultList();
                if (list == null)
                {
                    throw new ArgumentException("evaluated default list cannot be null", "defaultList");
                }
            }
        }

        public void Serialize<TType>(ref List<TType> list, Func<IUnrealStream, bool> condition,
            Func<List<TType>> defaultList) where TType : class, IUnrealSerializable, new()
        {
            if (condition == null)
            {
                throw new ArgumentNullException("condition");
            }

            if (defaultList == null)
            {
                throw new ArgumentNullException("defaultList");
            }

            if (condition(this) == false)
            {
                this.Serialize(ref list);
            }
            else
            {
                list = defaultList();
                if (list == null)
                {
                    throw new ArgumentException("evaluated default list cannot be null", "defaultList");
                }
            }
        }

        public void Serialize<TType>(ref BindingList<TType> list) where TType : class, IUnrealSerializable, new()
        {
            if (list == null)
            {
                throw new ArgumentNullException("list", "serializable list should not be null");
            }

            if (this.Loading == true)
            {
                var count = this.Stream.ReadUInt32();
                if (count >= 0x7FFFFF)
                {
                    throw new FormatException("too many items in list");
                }

                list.Clear();
                for (uint i = 0; i < count; i++)
                {
                    var item = new TType();
                    item.Serialize(this);
                    list.Add(item);
                }
            }
            else
            {
                this.Stream.WriteUInt32((uint)list.Count);
                foreach (var item in list)
                {
                    item.Serialize(this);
                }
            }
        }

        public void Serialize<TType>(ref BindingList<TType> list, Func<IUnrealStream, bool> condition,
                Func<BindingList<TType>> defaultList) where TType : class, IUnrealSerializable, new()
        {
            if (condition == null)
            {
                throw new ArgumentNullException("condition");
            }

            if (defaultList == null)
            {
                throw new ArgumentNullException("defaultList");
            }

            if (condition(this) == false)
            {
                this.Serialize(ref list);
            }
            else
            {
                list = defaultList();
                if (list == null)
                {
                    throw new ArgumentException("evaluated default list cannot be null", "defaultList");
                }
            }
        }

        public void Serialize(ref List<bool> values)
        {
            if (this.Loading == true)
            {
                uint count = this.Stream.ReadUInt32();

                if (count >= 0x7FFFFF)
                {
                    throw new Exception("sanity check");
                }

                List<bool> list = new List<bool>();

                for (uint i = 0; i < count; i++)
                {
                    list.Add(this.Stream.ReadInt32() != 0);
                }

                values = list;
            }
            else
            {
                if (values == null)
                {
                    throw new ArgumentNullException("values");
                }

                this.Stream.WriteInt32(values.Count);
                foreach (bool value in values)
                {
                    this.Stream.WriteUInt32(value ? 1u : 0u);
                }
            }
        }

        public void Serialize(ref List<int> values)
        {
            if (this.Loading == true)
            {
                uint count = this.Stream.ReadUInt32();

                if (count >= 0x7FFFFF)
                {
                    throw new Exception("sanity check");
                }

                List<int> list = new List<int>();

                for (uint i = 0; i < count; i++)
                {
                    list.Add(this.Stream.ReadInt32());
                }

                values = list;
            }
            else
            {
                if (values == null)
                {
                    throw new ArgumentNullException("values");
                }

                this.Stream.WriteInt32(values.Count);
                foreach (int value in values)
                {
                    this.Stream.WriteInt32(value);
                }
            }
        }

        public void Serialize(ref List<uint> values)
        {
            if (this.Loading == true)
            {
                uint count = this.Stream.ReadUInt32();

                if (count >= 0x7FFFFF)
                {
                    throw new Exception("sanity check");
                }

                List<uint> list = new List<uint>();

                for (uint i = 0; i < count; i++)
                {
                    list.Add(this.Stream.ReadUInt32());
                }

                values = list;
            }
            else
            {
                if (values == null)
                {
                    throw new ArgumentNullException("values");
                }

                this.Stream.WriteInt32(values.Count);
                foreach (uint value in values)
                {
                    this.Stream.WriteUInt32(value);
                }
            }
        }

        public void Serialize(ref List<float> values)
        {
            if (this.Loading == true)
            {
                uint count = this.Stream.ReadUInt32();

                if (count >= 0x7FFFFF)
                {
                    throw new Exception("sanity check");
                }

                List<float> list = new List<float>();

                for (uint i = 0; i < count; i++)
                {
                    list.Add(this.Stream.ReadFloat());
                }

                values = list;
            }
            else
            {
                if (values == null)
                {
                    throw new ArgumentNullException("values");
                }

                this.Stream.WriteInt32(values.Count);
                foreach (float value in values)
                {
                    this.Stream.WriteFloat(value);
                }
            }
        }

        public void Serialize(ref List<string> values)
        {
            if (this.Loading == true)
            {
                uint count = this.Stream.ReadUInt32();

                if (count >= 0x7FFFFF)
                {
                    throw new Exception("sanity check");
                }

                List<string> list = new List<string>();

                for (uint i = 0; i < count; i++)
                {
                    list.Add(this.Stream.ReadUnrealString());
                }

                values = list;
            }
            else
            {
                if (values == null)
                {
                    throw new ArgumentNullException("values");
                }

                this.Stream.WriteInt32(values.Count);
                foreach (string value in values)
                {
                    this.Stream.WriteUnrealString(value, MEGame.ME2); // ME2 SPECIFIC
                }
            }
        }

        public void Serialize(ref List<Guid> values)
        {
            if (this.Loading == true)
            {
                uint count = this.Stream.ReadUInt32();

                if (count >= 0x7FFFFF)
                {
                    throw new Exception("sanity check");
                }

                List<Guid> list = new List<Guid>();

                for (uint i = 0; i < count; i++)
                {
                    list.Add(this.Stream.ReadGuid());
                }

                values = list;
            }
            else
            {
                if (values == null)
                {
                    throw new ArgumentNullException("values");
                }

                this.Stream.WriteInt32(values.Count);
                foreach (Guid value in values)
                {
                    this.Stream.WriteGuid(value);
                }
            }
        }

        public void Serialize(ref string[] array)
        {
            if (array == null)
            {
                throw new ArgumentNullException("array", "serializable array should not be null");
            }

            this.ReadBasicArray(array, r => (string)r.Stream.ReadUnrealString());
        }

        public void Serialize(ref int[] array)
        {
            if (array == null)
            {
                throw new ArgumentNullException("array", "serializable array should not be null");
            }

            this.ReadBasicArray(array, r => (int)r.Stream.ReadInt32());
        }

        public void Serialize(ref uint[] array)
        {
            if (array == null)
            {
                throw new ArgumentNullException("array", "serializable array should not be null");
            }

            this.ReadBasicArray(array, r => (uint)r.Stream.ReadUInt32());
        }

        public void Serialize(ref float[] array)
        {
            if (array == null)
            {
                throw new ArgumentNullException("array", "serializable array should not be null");
            }

            this.ReadBasicArray(array, r => (int)r.Stream.ReadFloat());
        }

        public void Serialize(ref bool[] array)
        {
            if (array == null)
            {
                throw new ArgumentNullException("array", "serializable array should not be null");
            }

            this.ReadBasicArray(array, r => (bool)r.Stream.ReadBoolByte());
        }

        private void ReadBasicArray<TType>(TType[] list, Func<IUnrealStream, TType> readValue)
        {
            var count = this.Stream.ReadUInt32();
            if (count >= 0x7FFFFF)
            {
                throw new FormatException("too many items in array");
            }

            for (uint i = 0; i < count; i++)
            {
                list[i] = readValue(this);
            }
        }





        public void Serialize(ref BitArray values)
        {
            if (this.Loading == true)
            {
                uint count = this.Stream.ReadUInt32();

                if (count >= 0x7FFFFF)
                {
                    throw new Exception("sanity check");
                }

                BitArray list = new BitArray((int)(count * 32));

                for (uint i = 0; i < count; i++)
                {
                    uint offset = i * 32;
                    int value = this.Stream.ReadInt32();

                    for (int bit = 0; bit < 32; bit++)
                    {
                        list.Set((int)(offset + bit), (value & (1 << bit)) != 0);
                    }
                }

                values = list;
            }
            else
            {
                if (values == null)
                {
                    throw new ArgumentNullException("values");
                }

                uint count = ((uint)values.Count + 31) / 32;
                this.Stream.WriteUInt32(count);

                for (uint i = 0; i < count; i++)
                {
                    uint offset = i * 32;
                    int value = 0;

                    for (int bit = 0; bit < 32 && offset + bit < values.Count; bit++)
                    {
                        value |= (values.Get((int)(offset + bit)) ? 1 : 0) << bit;
                    }

                    this.Stream.WriteInt32(value);
                }
            }
        }



        public void Serialize<TFormat>(ref TFormat value)
            where TFormat : IUnrealSerializable, new()
        {
            if (this.Loading == false && value == null)
            {
                throw new ArgumentNullException("value");
            }

            if (this.Loading == true)
            {
                value = new TFormat();
            }

            value.Serialize(this);
        }

        public void Serialize<TFormat>(ref List<TFormat> values)
            where TFormat : IUnrealSerializable, new()
        {
            if (this.Loading == true)
            {
                uint count = this.Stream.ReadUInt32();

                if (count >= 0x7FFFFF)
                {
                    throw new Exception("sanity check");
                }

                List<TFormat> list = new List<TFormat>();

                for (uint i = 0; i < count; i++)
                {
                    TFormat value = new TFormat();
                    value.Serialize(this);
                    list.Add(value);
                }

                values = list;
            }
            else
            {
                if (values == null)
                {
                    throw new ArgumentNullException("values");
                }

                this.Stream.WriteInt32(values.Count);
                foreach (TFormat value in values)
                {
                    value.Serialize(this);
                }
            }
        }

        public void Serialize<TFormat>(ref TFormat[] values)
            where TFormat : IUnrealSerializable, new()
        {
            if (this.Loading == true)
            {
                uint count = this.Stream.ReadUInt32();

                if (count >= 0x7FFFFF)
                {
                    throw new Exception("sanity check");
                }

                TFormat[] list = new TFormat[count];

                for (uint i = 0; i < count; i++)
                {
                    TFormat value = new TFormat();
                    value.Serialize(this);
                    list[i] = value;
                }

                values = list;
            }
            else
            {
                if (values == null)
                {
                    throw new ArgumentNullException("values");
                }

                this.Stream.WriteInt32(values.Length);
                foreach (TFormat value in values)
                {
                    value.Serialize(this);
                }
            }
        }
    }
}