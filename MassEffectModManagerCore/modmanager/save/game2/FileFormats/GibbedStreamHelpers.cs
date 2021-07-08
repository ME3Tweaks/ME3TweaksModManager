using System;
using System.IO;
using LegendaryExplorerCore.Gammtek.IO;
using LegendaryExplorerCore.Helpers;

namespace MassEffectModManagerCore.modmanager.save.game2.FileFormats
{
    public static partial class StreamHelpers
    {

        /* Copyright (c) 2017 Rick (rick 'at' gibbed 'dot' us)
 * 
 * This software is provided 'as-is', without any express or implied
 * warranty. In no event will the authors be held liable for any damages
 * arising from the use of this software.
 * 
 * Permission is granted to anyone to use this software for any purpose,
 * including commercial applications, and to alter it and redistribute it
 * freely, subject to the following restrictions:
 * 
 * 1. The origin of this software must not be misrepresented; you must not
 *    claim that you wrote the original software. If you use this software
 *    in a product, an acknowledgment in the product documentation would
 *    be appreciated but is not required.
 * 
 * 2. Altered source versions must be plainly marked as such, and must not
 *    be misrepresented as being the original software.
 * 
 * 3. This notice may not be removed or altered from any source
 *    distribution.
 */


        #region Cache
        private static class EnumTypeCache
        {
            /*private static Dictionary<Type, EnumUnderlyingType> _Lookup;
            static EnumTypeCache()
            {
                _Lookup = new Dictionary<Type, EnumUnderlyingType>();
            }*/

            private static TypeCode TranslateType(Type type)
            {
                if (type.IsEnum == true)
                {
                    var underlyingType = Enum.GetUnderlyingType(type);
                    var underlyingTypeCode = Type.GetTypeCode(underlyingType);

                    switch (underlyingTypeCode)
                    {
                        case TypeCode.SByte:
                        case TypeCode.Byte:
                        case TypeCode.Int16:
                        case TypeCode.UInt16:
                        case TypeCode.Int32:
                        case TypeCode.UInt32:
                        case TypeCode.Int64:
                        case TypeCode.UInt64:
                            {
                                return underlyingTypeCode;
                            }
                    }
                }

                throw new ArgumentException("unknown enum type", "type");
            }

            public static TypeCode Get(Type type)
            {
                /*if (Lookup.ContainsKey(type) == true)
                {
                    return Lookup[type];
                }*/

                return /*Lookup[type] =*/ TranslateType(type);
            }
        }
        #endregion

        #region ReadValueEnum
        public static T ReadValueEnum<T>(this Stream stream, Endian endian)
        {
            var type = typeof(T);

            object value;
            switch (EnumTypeCache.Get(type))
            {
                case TypeCode.SByte:
                    {
                        value = (sbyte)stream.ReadByte();
                        break;
                    }

                case TypeCode.Byte:
                    {
                        value = stream.ReadByte();
                        break;
                    }

                case TypeCode.Int16:
                    {
                        value = stream.ReadInt16();
                        break;
                    }

                case TypeCode.UInt16:
                    {
                        value = stream.ReadUInt16();
                        break;
                    }

                case TypeCode.Int32:
                    {
                        value = stream.ReadInt32();
                        break;
                    }

                case TypeCode.UInt32:
                    {
                        value = stream.ReadUInt32();
                        break;
                    }

                case TypeCode.Int64:
                    {
                        value = stream.ReadInt64();
                        break;
                    }

                case TypeCode.UInt64:
                    {
                        value = stream.ReadUInt64();
                        break;
                    }

                default:
                    {
                        throw new NotSupportedException();
                    }
            }

            return (T)Enum.ToObject(type, value);
        }

        public static T ReadValueEnum<T>(this Stream stream)
        {
            return stream.ReadValueEnum<T>(Endian.Little);
        }
        #endregion

        #region WriteValueEnum
        public static void WriteValueEnum<T>(this Stream stream, object value, Endian endian)
        {
            var type = typeof(T);
            switch (EnumTypeCache.Get(type))
            {
                case TypeCode.SByte:
                    {
                        stream.WriteByte((byte)value);
                        break;
                    }

                case TypeCode.Byte:
                    {
                        stream.WriteByte((byte)value);
                        break;
                    }

                case TypeCode.Int16:
                    {
                        stream.WriteInt16((short)value);
                        break;
                    }

                case TypeCode.UInt16:
                    {
                        stream.WriteUInt16((ushort)value);
                        break;
                    }

                case TypeCode.Int32:
                    {
                        stream.WriteInt32((int)value);
                        break;
                    }

                case TypeCode.UInt32:
                    {
                        stream.WriteUInt32((uint)value);
                        break;
                    }

                case TypeCode.Int64:
                    {
                        stream.WriteInt64((long)value);
                        break;
                    }

                case TypeCode.UInt64:
                    {
                        stream.WriteUInt64((ulong)value);
                        break;
                    }

                default:
                    {
                        throw new NotSupportedException();
                    }
            }
        }

        public static void WriteValueEnum<T>(this Stream stream, object value)
        {
            stream.WriteValueEnum<T>(value, Endian.Little);
        }
        #endregion
    }
}
