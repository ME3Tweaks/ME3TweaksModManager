using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using ME3TweaksModManager.modmanager.save.game2.FileFormats.Save;

namespace ME3TweaksModManager.modmanager.save.game2.FileFormats
{
    public interface IUnrealStream
    {
        Stream Stream { get; }
        bool Loading { get; }
        uint Version { get; }

        // Basic
        void Serialize(ref bool value);
        void Serialize(ref byte value);
        void Serialize(ref int value);
        void Serialize(ref uint value);
        void Serialize(ref float value);
        void Serialize(ref string value);
        void Serialize(ref Guid value);

        // Lists
        void Serialize(ref List<bool> values);
        void Serialize(ref List<int> values);
        void Serialize(ref List<uint> values);
        void Serialize(ref List<float> values);
        void Serialize(ref List<string> values);
        void Serialize(ref List<Guid> values);
        void Serialize(ref BitArray values);

        // Arrays of primitive types
        void Serialize(ref bool[] values);
        void Serialize(ref int[] values);
        void Serialize(ref uint[] values);
        void Serialize(ref float[] values);
        void Serialize(ref string[] values);

        // Serializables
        void Serialize<TFormat>(ref TFormat value)
            where TFormat : IUnrealSerializable, new();
        
        // Serializable List
        void Serialize<TFormat>(ref List<TFormat> values)
            where TFormat : IUnrealSerializable, new();

        void Serialize<TType>(ref BindingList<TType> list) where TType : class, IUnrealSerializable, new();
        // void Serialize(ref IUnrealSerializable value);

        // Array
        void Serialize<TFormat>(ref TFormat[] values)
            where TFormat : IUnrealSerializable, new();

        // Enum
        void SerializeEnum<TEnum>(ref TEnum value);
        void SerializeEnum<TEnum>(ref TEnum value, Func<IUnrealStream, bool> condition, Func<TEnum> defaultValue);


        // Conditional serialization
        void Serialize(ref bool value, Func<IUnrealStream, bool> condition, Func<bool> defaultValue);
        void Serialize(ref byte value, Func<IUnrealStream, bool> condition, Func<byte> defaultValue);
        void Serialize(ref int value, Func<IUnrealStream, bool> condition, Func<int> defaultValue);
        void Serialize(ref uint value, Func<IUnrealStream, bool> condition, Func<uint> defaultValue);
        void Serialize(ref float value, Func<IUnrealStream, bool> condition, Func<float> defaultValue);
        void Serialize(ref string value, Func<IUnrealStream, bool> condition, Func<string> defaultValue);
        void Serialize(ref Guid value, Func<IUnrealStream, bool> condition, Func<Guid> defaultValue);


        void Serialize<TType>(ref TType value, Func<IUnrealStream, bool> condition, Func<TType> defaultValue)
            where TType : class, IUnrealSerializable, new();
        void Serialize(ref BitArray list, Func<IUnrealStream, bool> condition, Func<BitArray> defaultList);
        void Serialize(ref List<byte> list, Func<IUnrealStream, bool> condition, Func<List<byte>> defaultList);
        void Serialize(ref List<int> list, Func<IUnrealStream, bool> condition, Func<List<int>> defaultList);
        void Serialize(ref List<float> list, Func<IUnrealStream, bool> condition, Func<List<float>> defaultList);
        void Serialize(ref List<string> list, Func<IUnrealStream, bool> condition, Func<List<string>> defaultList);
        void Serialize(ref List<Guid> list, Func<IUnrealStream, bool> condition, Func<List<Guid>> defaultList);
        void SerializeEnum<TEnum>(ref List<TEnum> list, Func<IUnrealStream, bool> condition, Func<List<TEnum>> defaultList);
        void Serialize<TType>(ref List<TType> list, Func<IUnrealStream, bool> condition, Func<List<TType>> defaultList)
            where TType : class, IUnrealSerializable, new();
        void Serialize<TType>(ref BindingList<TType> list,
            Func<IUnrealStream, bool> condition,
            Func<BindingList<TType>> defaultList)
            where TType : class, IUnrealSerializable, new();
    }
}
