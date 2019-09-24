using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;
using Gammtek.Conduit.Extensions.IO;
using MassEffectModManager.modmanager;
using ME3Explorer.Packages;
using ME3Explorer.Unreal;
using MassEffectModManager.modmanager.helpers;
namespace ME3Explorer
{
    public class SerializingContainer2
    {
        public readonly Stream ms;
        public readonly bool IsLoading;
        public readonly IMEPackage Pcc;
        public readonly int startOffset;

        public bool IsSaving => !IsLoading;

        public int FileOffset => startOffset + (int)ms.Position;
        public Mod.MEGame Game => Pcc.Game;

        public SerializingContainer2(Stream stream, IMEPackage pcc, bool isLoading = false, int offset = 0)
        {
            ms = stream;
            IsLoading = isLoading;
            Pcc = pcc;
            startOffset = offset;
        }
    }

    public static partial class SCExt
    {
        public static void Serialize(this SerializingContainer2 sc, ref int val)
        {
            if (sc.IsLoading)
                val = sc.ms.ReadInt32();
            else
                sc.ms.WriteInt32(val);
        }

        public static void Serialize(this SerializingContainer2 sc, ref uint val)
        {
            if (sc.IsLoading)
                val = sc.ms.ReadUInt32();
            else
                sc.ms.WriteUInt32(val);
        }

        public static void Serialize(this SerializingContainer2 sc, ref short val)
        {
            if (sc.IsLoading)
                val = sc.ms.ReadInt16();
            else
                sc.ms.WriteInt16(val);
        }

        public static void Serialize(this SerializingContainer2 sc, ref ushort val)
        {
            if (sc.IsLoading)
                val = sc.ms.ReadUInt16();
            else
                sc.ms.WriteUInt16(val);
        }

        public static void Serialize(this SerializingContainer2 sc, ref long val)
        {
            if (sc.IsLoading)
                val = sc.ms.ReadInt64();
            else
                sc.ms.WriteInt64(val);
        }

        public static void Serialize(this SerializingContainer2 sc, ref ulong val)
        {
            if (sc.IsLoading)
                val = sc.ms.ReadUInt64();
            else
                sc.ms.WriteUInt64(val);
        }

        public static void Serialize(this SerializingContainer2 sc, ref byte val)
        {
            if (sc.IsLoading)
                val = (byte)sc.ms.ReadByte();
            else
                sc.ms.WriteByte(val);
        }

        public static void Serialize(this SerializingContainer2 sc, ref string val)
        {
            if (sc.IsLoading)
                val = sc.ms.ReadUnrealString();
            else
                sc.ms.WriteUnrealString(val, sc.Game);
        }

        public static void Serialize(this SerializingContainer2 sc, ref float val)
        {
            if (sc.IsLoading)
                val = sc.ms.ReadFloat();
            else
                sc.ms.WriteFloat(val);
        }

        public static void Serialize(this SerializingContainer2 sc, ref double val)
        {
            if (sc.IsLoading)
                val = sc.ms.ReadDouble();
            else
                sc.ms.WriteDouble(val);
        }

        public static void Serialize(this SerializingContainer2 sc, ref bool val)
        {
            if (sc.IsLoading)
                val = sc.ms.ReadBoolInt();
            else
                sc.ms.WriteBoolInt(val);
        }

        public static void Serialize(this SerializingContainer2 sc, ref byte[] buffer, int size)
        {
            if (sc.IsLoading)
            {
                buffer = sc.ms.ReadToBuffer(size);
            }
            else
            {
                sc.ms.WriteFromBuffer(buffer);
            }
        }

        public static void Serialize(this SerializingContainer2 sc, ref Guid guid)
        {
            if (sc.IsLoading)
                guid = sc.ms.ReadGuid();
            else
                sc.ms.WriteGuid(guid);
        }

        public delegate void SerializeDelegate<T>(SerializingContainer2 sc, ref T item);

        public static void Serialize<T>(this SerializingContainer2 sc, ref List<T> arr, SerializeDelegate<T> serialize)
        {
            int count = arr?.Count ?? 0;
            sc.Serialize(ref count);
            if (sc.IsLoading)
            {
                arr = new List<T>(count);
                for (int i = 0; i < count; i++)
                {
                    T tmp = default;
                    serialize(sc, ref tmp);
                    arr.Add(tmp);
                }
            }
            else
            {
                for (int i = 0; i < count; i++)
                {
                    T tmp = arr[i];
                    serialize(sc, ref tmp);
                }
            }
        }

        public static void Serialize<T>(this SerializingContainer2 sc, ref T[] arr, SerializeDelegate<T> serialize)
        {
            int count = arr?.Length ?? 0;
            sc.Serialize(ref count);
            if (sc.IsLoading)
            {
                arr = new T[count];
            }

            for (int i = 0; i < count; i++)
            {
                serialize(sc, ref arr[i]);
            }
        }
        public static void Serialize<TKey, TValue>(this SerializingContainer2 sc, ref OrderedMultiValueDictionary<TKey, TValue> dict, SerializeDelegate<TKey> serializeKey, SerializeDelegate<TValue> serializeValue)
        {
            int count = dict?.Count ?? 0;
            sc.Serialize(ref count);
            if (sc.IsLoading)
            {
                dict = new OrderedMultiValueDictionary<TKey, TValue>(count);
                for (int i = 0; i < count; i++)
                {
                    TKey key = default;
                    serializeKey(sc, ref key);
                    TValue value = default;
                    serializeValue(sc, ref value);
                    dict.Add(key, value);
                }
            }
            else
            {
                for (int i = 0; i < count; i++)
                {
                    var key = dict[i].Key;
                    serializeKey(sc, ref key);
                    var value = dict[i].Value;
                    serializeValue(sc, ref value);
                }
            }

        }
        public static void BulkSerialize<T>(this SerializingContainer2 sc, ref T[] arr, SerializeDelegate<T> serialize, int elementSize)
        {
            sc.Serialize(ref elementSize);
            int count = arr?.Length ?? 0;
            sc.Serialize(ref count);
            if (sc.IsLoading)
            {
                arr = new T[count];
            }

            for (int i = 0; i < count; i++)
            {
                serialize(sc, ref arr[i]);
            }
        }
        public static void SerializeBulkData<T>(this SerializingContainer2 sc, ref T[] arr, SerializeDelegate<T> serialize)
        {
            int bulkdataflags = 0;
            sc.Serialize(ref bulkdataflags);
            int elementCount = arr?.Length ?? 0;
            sc.Serialize(ref elementCount);
            int sizeOnDisk = 0;
            long sizeOnDiskPosition = sc.ms.Position;
            sc.Serialize(ref sizeOnDisk); //when saving, come back and rewrite this after writing arr
            int offsetInFile = sc.FileOffset + 4;
            sc.Serialize(ref offsetInFile);
            if (sc.IsLoading)
            {
                arr = new T[elementCount];
            }

            for (int i = 0; i < elementCount; i++)
            {
                serialize(sc, ref arr[i]);
            }

            if (sc.IsSaving)
            {
                long curPos = sc.ms.Position;
                sc.ms.JumpTo(sizeOnDiskPosition);
                sc.ms.WriteInt32((int)(curPos - (sizeOnDiskPosition + 8)));
                sc.ms.JumpTo(curPos);
            }
        }

        public static void Serialize(this SerializingContainer2 sc, ref NameReference name)
        {
            if (sc.IsLoading)
            {
                name = sc.ms.ReadNameReference(sc.Pcc);
            }
            else
            {
                sc.ms.WriteNameReference(name, sc.Pcc);
            }
        }
    }
}
