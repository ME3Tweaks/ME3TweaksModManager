using System;
using System.Collections.Generic;
using System.IO;
using MassEffectModManagerCore.modmanager;
using MassEffectModManagerCore.modmanager.helpers;
using ME3Explorer.Packages;
using Newtonsoft.Json;

namespace ME3Explorer.Unreal
{
    public readonly struct NameReference : IEquatable<NameReference>
    {
        public string Name { get; }
        public int Number { get; }

        public NameReference(string name, int number = 0)
        {
            Name = name;
            Number = number;
        }

        //https://api.unrealengine.com/INT/API/Runtime/Core/UObject/FName/index.html
        [JsonIgnore]
        public string InstancedString => Number > 0 ? $"{Name}_{Number - 1}" : Name;

        public static implicit operator NameReference(string s)
        {
            return new NameReference(s);
        }

        public static implicit operator string(NameReference n)
        {
            return n.Name;
        }

        public override string ToString()
        {
            return Name ?? string.Empty;
        }

        public static bool operator ==(NameReference r, string s)
        {
            return s == r.Name;
        }

        public static bool operator !=(NameReference r, string s)
        {
            return s != r.Name;
        }

        #region IEquatable
        public static bool operator ==(NameReference n1, NameReference n2)
        {
            return n1.Equals(n2);
        }

        public static bool operator !=(NameReference n1, NameReference n2)
        {
            return !n1.Equals(n2);
        }
        public bool Equals(NameReference other)
        {
            return string.Equals(Name, other.Name) && Number == other.Number;
        }

        public override bool Equals(object obj)
        {
            return obj is NameReference other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (Name.GetHashCode() * 397) ^ Number;
            }
        }
        #endregion
    }


    public struct ScriptDelegate : IEquatable<ScriptDelegate>
    {
        public int Object { get; }
        public NameReference FunctionName { get; }

        public ScriptDelegate(int _object, NameReference functionName)
        {
            Object = _object;
            FunctionName = functionName;
        }

        #region IEquatable

        public bool Equals(ScriptDelegate other)
        {
            return Object == other.Object && FunctionName.Equals(other.FunctionName);
        }

        public override bool Equals(object obj)
        {
            return obj is ScriptDelegate other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (Object * 397) ^ FunctionName.GetHashCode();
            }
        }

        public static bool operator ==(ScriptDelegate left, ScriptDelegate right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(ScriptDelegate left, ScriptDelegate right)
        {
            return !left.Equals(right);
        }

        #endregion
    }
    public enum PropertyType
    {
        Unknown = -1,
        None = 0,
        StructProperty = 1,
        IntProperty = 2,
        FloatProperty = 3,
        ObjectProperty = 4,
        NameProperty = 5,
        BoolProperty = 6,
        ByteProperty = 7,
        ArrayProperty = 8,
        StrProperty = 9,
        StringRefProperty = 10,
        DelegateProperty = 11,
        BioMask4Property
    }

    [Obsolete("Use ExportEntry's GetProperties() instead")]
    public static class PropertyReader
    {
        public class Property
        {
            public int Name;
            public PropertyType TypeVal;
            public int Size;
            public int offsetval;
            public int offend;
            public PropertyValue Value;
            public byte[] raw;
        }

        public struct PropertyValue
        {
            public int len;
            public string StringValue;
            public int IntValue;
            public float FloatValue;
            public NameReference NameValue;
            public List<PropertyValue> Array;
        }

        public static string TypeToString(int type)
        {
            switch (type)
            {
                case 1: return "Struct Property";
                case 2: return "Integer Property";
                case 3: return "Float Property";
                case 4: return "Object Property";
                case 5: return "Name Property";
                case 6: return "Bool Property";
                case 7: return "Byte Property";
                case 8: return "Array Property";
                case 9: return "String Property";
                case 10: return "String Ref Property";
                default: return "Unknown/None";
            }
        }

        //TODO: Remove this
        public static List<Property> ReadProp(IMEPackage pcc, byte[] raw, int start)
        {
            Property p;
            PropertyValue v;
            int sname;
            List<Property> result = new List<Property>();
            int pos = start;
            if (raw.Length - pos < 8)
                return result;
            //int name = (int)BitConverter.ToInt64(raw, pos);
            int name = (int)BitConverter.ToInt32(raw, pos);

            if (!pcc.isName(name))
                return result;
            string t = pcc.getNameEntry(name);
            p = new Property();
            p.Name = name;
            //Debug.WriteLine(t +" at "+start);
            if (t == "None")
            {
                p.TypeVal = PropertyType.None;
                p.offsetval = pos;
                p.Size = 8;
                p.Value = new PropertyValue();
                p.raw = BitConverter.GetBytes((long)name);
                p.offend = pos + 8;
                result.Add(p);
                return result;
            }
            //int type = (int)BitConverter.ToInt64(raw, pos + 8);            
            int type = (int)BitConverter.ToInt32(raw, pos + 8);
            if (!pcc.isName(type))
            {
                return result;
            }
            p.Size = BitConverter.ToInt32(raw, pos + 16);
            if (p.Size < 0 || p.Size >= raw.Length)
            {
                return result;
            }
            string tp = pcc.getNameEntry(type);
            switch (tp)
            {

                case "DelegateProperty":
                    p.TypeVal = PropertyType.DelegateProperty;
                    p.offsetval = pos + 24;
                    v = new PropertyValue();
                    v.IntValue = BitConverter.ToInt32(raw, pos + 28);
                    v.len = p.Size;
                    v.Array = new List<PropertyValue>();
                    pos += 24;
                    for (int i = 0; i < p.Size; i++)
                    {
                        PropertyValue v2 = new PropertyValue();
                        if (pos < raw.Length)
                            v2.IntValue = raw[pos];
                        v.Array.Add(v2);
                        pos++;
                    }
                    p.Value = v;
                    break;
                case "ArrayProperty":
                    int count = BitConverter.ToInt32(raw, pos + 24);
                    p.TypeVal = PropertyType.ArrayProperty;
                    p.offsetval = pos + 24;
                    v = new PropertyValue();
                    v.IntValue = type;
                    v.len = p.Size - 4;
                    count = v.len;//TODO can be other objects too
                    v.Array = new List<PropertyValue>();
                    pos += 28;
                    for (int i = 0; i < count; i++)
                    {
                        PropertyValue v2 = new PropertyValue();
                        if (pos < raw.Length)
                            v2.IntValue = raw[pos];
                        v.Array.Add(v2);
                        pos++;
                    }
                    p.Value = v;
                    break;
                case "StrProperty":
                    count = BitConverter.ToInt32(raw, pos + 24);
                    p.TypeVal = PropertyType.StrProperty;
                    p.offsetval = pos + 24;
                    v = new PropertyValue();
                    v.IntValue = type;
                    v.len = count;
                    pos += 28;
                    string s = "";
                    if (count < 0)
                    {
                        count *= -1;
                        for (int i = 1; i < count; i++)
                        {
                            s += (char)raw[pos];
                            pos += 2;
                        }
                        pos += 2;
                    }
                    else if (count > 0)
                    {
                        for (int i = 1; i < count; i++)
                        {
                            s += (char)raw[pos];
                            pos++;
                        }
                        pos++;
                    }
                    v.StringValue = s;
                    p.Value = v;
                    break;
                case "StructProperty":
                    sname = BitConverter.ToInt32(raw, pos + 24);
                    p.TypeVal = PropertyType.StructProperty;
                    p.offsetval = pos + 24;
                    v = new PropertyValue();
                    v.IntValue = sname;
                    v.len = p.Size;
                    v.Array = new List<PropertyValue>();
                    pos += 32;
                    for (int i = 0; i < p.Size; i++)
                    {
                        PropertyValue v2 = new PropertyValue();
                        if (pos < raw.Length)
                            v2.IntValue = raw[pos];
                        v.Array.Add(v2);
                        pos++;
                    }
                    p.Value = v;
                    break;
                case "BioMask4Property":
                    p.TypeVal = PropertyType.ByteProperty;
                    p.offsetval = pos + 24;
                    v = new PropertyValue();
                    v.len = p.Size;
                    pos += 24;
                    v.IntValue = raw[pos];
                    pos += p.Size;
                    p.Value = v;
                    break;
                case "ByteProperty":
                    sname = BitConverter.ToInt32(raw, pos + 24);
                    p.TypeVal = PropertyType.ByteProperty;
                    v = new PropertyValue();
                    v.len = p.Size;
                    if (pcc.Game == Mod.MEGame.ME3)
                    {
                        p.offsetval = pos + 32;
                        v.StringValue = pcc.getNameEntry(sname);
                        pos += 32;
                        if (p.Size == 8)
                        {
                            v.IntValue = BitConverter.ToInt32(raw, pos);
                        }
                        else
                        {
                            v.IntValue = raw[pos];
                        }
                        pos += p.Size;
                    }
                    else
                    {
                        p.offsetval = pos + 24;
                        if (p.Size != 1)
                        {
                            v.StringValue = pcc.getNameEntry(sname);
                            v.IntValue = sname;
                            pos += 32;
                        }
                        else
                        {
                            v.StringValue = "";
                            v.IntValue = raw[pos + 24];
                            pos += 25;
                        }
                    }
                    p.Value = v;
                    break;
                case "FloatProperty":
                    sname = BitConverter.ToInt32(raw, pos + 24);
                    p.TypeVal = PropertyType.FloatProperty;
                    p.offsetval = pos + 24;
                    v = new PropertyValue();
                    v.FloatValue = BitConverter.ToSingle(raw, pos + 24);
                    v.len = p.Size;
                    pos += 28;
                    p.Value = v;
                    break;
                case "BoolProperty":
                    p = new Property();
                    p.Name = name;
                    p.TypeVal = PropertyType.BoolProperty;
                    p.offsetval = pos + 24;
                    v = new PropertyValue();
                    v.IntValue = raw[pos + 24];
                    if (pcc.Game == Mod.MEGame.ME3) //THIS NEEDS TESTED!!! From when merging UDK
                    {
                        v.len = 1;
                    }
                    else
                    {
                        v.len = 4;
                    }
                    pos += v.len + 24;
                    p.Value = v;
                    break;
                default:
                    p.TypeVal = getType(pcc, type);
                    p.offsetval = pos + 24;
                    p.Value = ReadValue(pcc, raw, pos + 24, type);
                    pos += p.Value.len + 24;
                    break;
            }
            p.raw = new byte[pos - start];
            p.offend = pos;
            if (pos < raw.Length)
                for (int i = 0; i < pos - start; i++)
                    p.raw[i] = raw[start + i];
            result.Add(p);
            if (pos != start) result.AddRange(ReadProp(pcc, raw, pos));
            return result;
        }

        static PropertyType getType(IMEPackage pcc, int type)
        {
            switch (pcc.getNameEntry(type))
            {
                case "None": return PropertyType.None;
                case "StructProperty": return PropertyType.StructProperty;
                case "IntProperty": return PropertyType.IntProperty;
                case "FloatProperty": return PropertyType.FloatProperty;
                case "ObjectProperty": return PropertyType.ObjectProperty;
                case "NameProperty": return PropertyType.NameProperty;
                case "BoolProperty": return PropertyType.BoolProperty;
                case "ByteProperty": return PropertyType.ByteProperty;
                case "ArrayProperty": return PropertyType.ArrayProperty;
                case "DelegateProperty": return PropertyType.DelegateProperty;
                case "StrProperty": return PropertyType.StrProperty;
                case "StringRefProperty": return PropertyType.StringRefProperty;
                default:
                    return PropertyType.Unknown;
            }
        }

        static PropertyValue ReadValue(IMEPackage pcc, byte[] raw, int start, int type)
        {
            PropertyValue v = new PropertyValue();
            switch (pcc.getNameEntry(type))
            {
                case "IntProperty":
                case "ObjectProperty":
                case "StringRefProperty":
                    v.IntValue = BitConverter.ToInt32(raw, start);
                    v.len = 4;
                    break;
                case "NameProperty":
                    v.IntValue = BitConverter.ToInt32(raw, start);
                    v.NameValue = new NameReference(pcc.getNameEntry(v.IntValue), BitConverter.ToInt32(raw, start + 4));
                    v.StringValue = v.NameValue.InstancedString;
                    v.len = 8;
                    break;
            }
            return v;
        }

        public static int detectStart(IMEPackage pcc, byte[] raw, UnrealFlags.EObjectFlags flags)
        {
            if ((flags & UnrealFlags.EObjectFlags.HasStack) != 0)
            {
                if (pcc.Game != Mod.MEGame.ME3)
                {
                    return 32;
                }
                return 30;
            }
            int result = 8;
            int test1 = BitConverter.ToInt32(raw, 4);
            int test2 = BitConverter.ToInt32(raw, 8);
            if (pcc.isName(test1) && test2 == 0)
                result = 4;
            if (pcc.isName(test1) && pcc.isName(test2) && test2 != 0)
                result = 8;
            return result;
        }

        public static void WritePropHeader(this Stream stream, IMEPackage pcc, string propName, PropertyType type, int size)
        {
            stream.WriteInt32(pcc.FindNameOrAdd(propName));
            stream.WriteInt32(0);
            stream.WriteInt32(pcc.FindNameOrAdd(type.ToString()));
            stream.WriteInt32(0);
            stream.WriteInt32(size);
            stream.WriteInt32(0);
        }

        public static void WriteNoneProperty(this Stream stream, IMEPackage pcc)
        {
            //Debug.WriteLine("Writing none property at 0x" + stream.Position.ToString("X6"));

            stream.WriteInt32(pcc.FindNameOrAdd("None"));
            stream.WriteInt32(0);
        }

        public static void WriteStructProperty(this Stream stream, IMEPackage pcc, string propName, string structName, MemoryStream value)
        {
            //Debug.WriteLine("Writing struct property " + propName + ", value: " + value + " at 0x" + stream.Position.ToString("X6"));

            stream.WritePropHeader(pcc, propName, PropertyType.StructProperty, (int)value.Length);
            stream.WriteInt32(pcc.FindNameOrAdd(structName));
            stream.WriteInt32(0);
            stream.WriteStream(value);
        }

        public static void WriteStructProperty(this Stream stream, IMEPackage pcc, string propName, string structName, Func<MemoryStream> func)
        {
            stream.WriteStructProperty(pcc, propName, structName, func());
        }

        public static void WriteIntProperty(this Stream stream, IMEPackage pcc, string propName, int value)
        {
            //Debug.WriteLine("Writing int property " + propName + ", value: " + value + " at 0x" + stream.Position.ToString("X6"));

            stream.WritePropHeader(pcc, propName, PropertyType.IntProperty, 4);
            stream.WriteInt32(value);
        }

        public static void WriteFloatProperty(this Stream stream, IMEPackage pcc, string propName, float value)
        {
            //Debug.WriteLine("Writing float property " + propName + ", value: " + value + " at 0x" + stream.Position.ToString("X6"));

            stream.WritePropHeader(pcc, propName, PropertyType.FloatProperty, 4);
            stream.WriteFloat(value);
        }

        public static void WriteObjectProperty(this Stream stream, IMEPackage pcc, string propName, int value)
        {
            //Debug.WriteLine("Writing bool property " + propName + ", value: " + value + " at 0x" + stream.Position.ToString("X6"));

            stream.WritePropHeader(pcc, propName, PropertyType.ObjectProperty, 4);
            stream.WriteInt32(value);
        }

        public static void WriteNameProperty(this Stream stream, IMEPackage pcc, string propName, NameReference value)
        {
            //Debug.WriteLine("Writing name property " + propName + ", value: " + value + " at 0x" + stream.Position.ToString("X6"));

            stream.WritePropHeader(pcc, propName, PropertyType.NameProperty, 8);
            stream.WriteInt32(pcc.FindNameOrAdd(value.Name));
            stream.WriteInt32(value.Number);
        }

        public static void WriteBoolProperty(this Stream stream, IMEPackage pcc, string propName, bool value)
        {
            //Debug.WriteLine("Writing bool property " + propName + ", value: " + value + " at 0x" + stream.Position.ToString("X6"));

            stream.WritePropHeader(pcc, propName, PropertyType.BoolProperty, 0);
            if (pcc.Game == Mod.MEGame.ME3)
            {
                stream.WriteBoolByte(value);
            }
            else
            {
                stream.WriteBoolInt(value);
            }
        }

        public static void WriteByteProperty(this Stream stream, IMEPackage pcc, string propName, byte value)
        {
            //Debug.WriteLine("Writing byte property " + propName + ", value: " + value + " at 0x" + stream.Position.ToString("X6"));
            stream.WritePropHeader(pcc, propName, PropertyType.ByteProperty, 1);
            if (pcc.Game == Mod.MEGame.ME3)
            {
                stream.WriteInt32(pcc.FindNameOrAdd("None"));
                stream.WriteInt32(0);
            }
            stream.WriteByte(value);
        }

        public static void WriteEnumProperty(this Stream stream, IMEPackage pcc, string propName, NameReference enumName, NameReference enumValue)
        {
            stream.WritePropHeader(pcc, propName, PropertyType.ByteProperty, 8);
            if (pcc.Game == Mod.MEGame.ME3)
            {
                stream.WriteInt32(pcc.FindNameOrAdd(enumName.Name));
                stream.WriteInt32(enumName.Number);
            }
            stream.WriteInt32(pcc.FindNameOrAdd(enumValue.Name));
            stream.WriteInt32(enumValue.Number);
        }

        public static void WriteArrayProperty(this Stream stream, IMEPackage pcc, string propName, int count, MemoryStream value)
        {
            //Debug.WriteLine("Writing array property " + propName + ", count: " + count + " at 0x" + stream.Position.ToString("X6")+", length: "+value.Length);
            stream.WritePropHeader(pcc, propName, PropertyType.ArrayProperty, 4 + (int)value.Length);
            stream.WriteInt32(count);
            stream.WriteStream(value);
        }

        public static void WriteArrayProperty(this Stream stream, IMEPackage pcc, string propName, int count, Func<MemoryStream> func)
        {
            stream.WriteArrayProperty(pcc, propName, count, func());
        }

        public static void WriteStringProperty(this Stream stream, IMEPackage pcc, string propName, string value)
        {
            //Debug.WriteLine("Writing string property " + propName + ", value: " + value + " at 0x" + stream.Position.ToString("X6"));
            int strLen = value.Length == 0 ? 0 : value.Length + 1;
            if (pcc.Game == Mod.MEGame.ME3)
            {
                if (propName != null)
                {
                    stream.WritePropHeader(pcc, propName, PropertyType.StrProperty, (strLen * 2) + 4);
                }
                stream.WriteUnrealStringUnicode(value);
            }
            else
            {
                stream.WritePropHeader(pcc, propName, PropertyType.StrProperty, strLen + 4);
                stream.WriteUnrealStringASCII(value);
            }
        }

        public static void WriteStringRefProperty(this Stream stream, IMEPackage pcc, string propName, int value)
        {
            //Debug.WriteLine("Writing stringref property " + propName + ", value: " + value + " at 0x" + stream.Position.ToString("X6"));

            stream.WritePropHeader(pcc, propName, PropertyType.StringRefProperty, 4);
            stream.WriteInt32(value);
        }

        public static void WriteDelegateProperty(this Stream stream, IMEPackage pcc, string propName, ScriptDelegate value)
        {
            stream.WritePropHeader(pcc, propName, PropertyType.DelegateProperty, 12);
            stream.WriteInt32(value.Object);
            stream.WriteInt32(pcc.FindNameOrAdd(value.FunctionName.Name));
            stream.WriteInt32(value.FunctionName.Number);
        }

        public static StructProperty ToGuidStructProp(this Guid guid, NameReference propName)
        {
            byte[] guidBytes = guid.ToByteArray();
            return new StructProperty("Guid", new PropertyCollection
            {
                new IntProperty(BitConverter.ToInt32(guidBytes, 0)),
                new IntProperty(BitConverter.ToInt32(guidBytes, 4)),
                new IntProperty(BitConverter.ToInt32(guidBytes, 8)),
                new IntProperty(BitConverter.ToInt32(guidBytes, 12))
            }, propName, true);
        }
    }
}
