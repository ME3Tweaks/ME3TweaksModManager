using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using MassEffectModManagerCore;
using Newtonsoft.Json;
using ME3Explorer.Packages;

using MassEffectModManagerCore.GameDirectories;
using MassEffectModManagerCore.modmanager;
using Serilog;

namespace ME3Explorer.Unreal
{
    public static class ME1UnrealObjectInfo
    {
        public static Dictionary<string, ClassInfo> Classes = new Dictionary<string, ClassInfo>();
        public static Dictionary<string, ClassInfo> Structs = new Dictionary<string, ClassInfo>();
        public static Dictionary<string, List<NameReference>> Enums = new Dictionary<string, List<NameReference>>();

        private static readonly string jsonPath = Path.Combine(Utilities.GetObjectInfoFolder(), "ME1ObjectInfo.json");
        public static void loadfromJSON()
        {
            string path = jsonPath;

            try
            {
                if (File.Exists(path))
                {
                    string raw = File.ReadAllText(path);
                    var blob = JsonConvert.DeserializeAnonymousType(raw, new { Classes, Structs, Enums });
                    Classes = blob.Classes;
                    Structs = blob.Structs;
                    Enums = blob.Enums;
                    foreach ((string className, ClassInfo classInfo) in Classes)
                    {
                        classInfo.ClassName = className;
                    }
                    foreach ((string className, ClassInfo classInfo) in Structs)
                    {
                        classInfo.ClassName = className;
                    }
                }
                else
                {
                    Log.Error(@"Cannot load ME1ObjectInfo: JsonFile is missing.");
                }
            }
            catch (Exception ex)
            {
                Log.Error(@"Error loading ME1ObjectInfo: " + ex.Message);
            }
        }

        private static readonly string[] ImmutableStructs = { "Vector", "Color", "LinearColor", "TwoVectors", "Vector4", "Vector2D", "Rotator", "Guid", "Plane", "Box",
            "Quat", "Matrix", "IntPoint", "ActorReference", "PolyReference", "AimTransform","BioRwBox", "BioMask4Property", "RwVector2", "RwVector3", "RwVector4",
            "BioRwBox44" };

        public static bool IsImmutableStruct(string structName)
        {
            return ImmutableStructs.Contains(structName);
        }

        public static string getEnumTypefromProp(string className, string propName, bool inStruct = false, ClassInfo nonVanillaClassInfo = null)
        {
            PropertyInfo p = getPropertyInfo(className, propName, inStruct, nonVanillaClassInfo);
            if (p == null && !inStruct)
            {
                p = getPropertyInfo(className, propName, true, nonVanillaClassInfo);
            }
            return p?.Reference;
        }

        public static List<NameReference> getEnumfromProp(string className, string propName, bool inStruct = false)
        {
            Dictionary<string, ClassInfo> temp = inStruct ? Structs : Classes;
            if (temp.ContainsKey(className))
            {
                ClassInfo info = temp[className];
                //look in class properties
                if (info.properties.TryGetValue(propName, out var propInfo))
                {
                    if (Enums.ContainsKey(propInfo.Reference))
                    {
                        return Enums[propInfo.Reference];
                    }
                }
                //look in structs
                else
                {
                    foreach (PropertyInfo p in info.properties.Values())
                    {
                        if (p.Type == PropertyType.StructProperty || p.Type == PropertyType.ArrayProperty)
                        {
                            List<NameReference> vals = getEnumfromProp(p.Reference, propName, true);
                            if (vals != null)
                            {
                                return vals;
                            }
                        }
                    }
                }
                //look in base class
                if (temp.ContainsKey(info.baseClass))
                {
                    List<NameReference> vals = getEnumfromProp(info.baseClass, propName, inStruct);
                    if (vals != null)
                    {
                        return vals;
                    }
                }
            }
            return null;
        }

        public static List<NameReference> getEnumValues(string enumName, bool includeNone = false)
        {
            if (Enums.ContainsKey(enumName))
            {
                var values = new List<NameReference>(Enums[enumName]);
                if (includeNone)
                {
                    values.Insert(0, "None");
                }
                return values;
            }
            return null;
        }

        public static ArrayType getArrayType(string className, string propName, ExportEntry export = null)
        {
            PropertyInfo p = getPropertyInfo(className, propName, false, containingExport: export)
                          ?? getPropertyInfo(className, propName, true, containingExport: export);
            if (p == null && export != null)
            {
                if (export.ClassName != "Class" && export.idxClass > 0)
                {
                    export = export.FileRef.Exports[export.idxClass - 1]; //make sure you get actual class
                }
                if (export.ClassName == "Class")
                {
                    ClassInfo currentInfo = generateClassInfo(export);
                    currentInfo.baseClass = export.SuperClassName;
                    p = getPropertyInfo(className, propName, false, currentInfo, containingExport: export)
                     ?? getPropertyInfo(className, propName, true, currentInfo, containingExport: export);
                }
            }
            return getArrayType(p);
        }

        public static ArrayType getArrayType(PropertyInfo p)
        {
            if (p != null)
            {
                if (p.Reference == "NameProperty")
                {
                    return ArrayType.Name;
                }
                else if (Enums.ContainsKey(p.Reference))
                {
                    return ArrayType.Enum;
                }
                else if (p.Reference == "BoolProperty")
                {
                    return ArrayType.Bool;
                }
                else if (p.Reference == "ByteProperty")
                {
                    return ArrayType.Byte;
                }
                else if (p.Reference == "StrProperty")
                {
                    return ArrayType.String;
                }
                else if (p.Reference == "FloatProperty")
                {
                    return ArrayType.Float;
                }
                else if (p.Reference == "IntProperty")
                {
                    return ArrayType.Int;
                }
                else if (Structs.ContainsKey(p.Reference))
                {
                    return ArrayType.Struct;
                }
                else
                {
                    return ArrayType.Object;
                }
            }
            else
            {
                return ArrayType.Int;
            }
        }

        public static PropertyInfo getPropertyInfo(string className, string propName, bool inStruct = false, ClassInfo nonVanillaClassInfo = null, bool reSearch = true, ExportEntry containingExport = null)
        {
            if (className.StartsWith("Default__"))
            {
                className = className.Substring(9);
            }
            Dictionary<string, ClassInfo> temp = inStruct ? Structs : Classes;
            bool infoExists = temp.TryGetValue(className, out ClassInfo info);
            if (!infoExists && nonVanillaClassInfo != null)
            {
                info = nonVanillaClassInfo;
                infoExists = true;
            }
            if (infoExists) //|| (temp = !inStruct ? Structs : Classes).ContainsKey(className))
            {
                //look in class properties
                if (info.properties.TryGetValue(propName, out var propInfo))
                {
                    return propInfo;
                }
                //look in structs
                else
                {
                    foreach (PropertyInfo p in info.properties.Values())
                    {
                        if ((p.Type == PropertyType.StructProperty || p.Type == PropertyType.ArrayProperty) && reSearch)
                        {
                            PropertyInfo val = getPropertyInfo(p.Reference, propName, true, nonVanillaClassInfo, reSearch: false);
                            if (val != null)
                            {
                                return val;
                            }
                        }
                    }
                }
                //look in base class
                if (temp.ContainsKey(info.baseClass))
                {
                    PropertyInfo val = getPropertyInfo(info.baseClass, propName, inStruct, nonVanillaClassInfo, reSearch: true);
                    if (val != null)
                    {
                        return val;
                    }
                }
                else
                {
                    //Baseclass may be modified as well...
                    if (containingExport != null && containingExport.idxSuperClass > 0)
                    {
                        //Class parent is in this file. Generate class parent info and attempt refetch
                        ExportEntry parentExport = containingExport.FileRef.getUExport(containingExport.idxSuperClass);
                        return getPropertyInfo(parentExport.SuperClassName, propName, inStruct, generateClassInfo(parentExport), reSearch: true, parentExport);
                    }
                }
            }

            //if (reSearch)
            //{
            //    PropertyInfo reAttempt = getPropertyInfo(className, propName, !inStruct, nonVanillaClassInfo, reSearch: false);
            //    return reAttempt; //will be null if not found.
            //}
            return null;
        }

        public static PropertyCollection getDefaultStructValue(string className, bool stripTransients)
        {
            bool isImmutable = UnrealObjectInfo.IsImmutable(className, Mod.MEGame.ME1);
            if (Structs.ContainsKey(className))
            {
                ClassInfo info = Structs[className];
                try
                {
                    PropertyCollection structProps = new PropertyCollection();
                    ClassInfo tempInfo = info;
                    while (tempInfo != null)
                    {
                        foreach ((string propName, PropertyInfo propInfo) in tempInfo.properties)
                        {
                            if (stripTransients && propInfo.Transient)
                            {
                                continue;
                            }
                            if (getDefaultProperty(propName, propInfo, stripTransients, isImmutable) is UProperty uProp)
                            {
                                structProps.Add(uProp);
                            }
                        }
                        if (!Structs.TryGetValue(tempInfo.baseClass, out tempInfo))
                        {
                            tempInfo = null;
                        }
                    }
                    structProps.Add(new NoneProperty());

                    string filepath = Path.Combine(ME1Directory.gamePath, "BioGame", info.pccPath);
                    if (File.Exists(info.pccPath))
                    {
                        filepath = info.pccPath; //Used for dynamic lookup
                    }
                    if (File.Exists(filepath))
                    {
                        IMEPackage importPCC = MEPackageHandler.OpenMEPackage(filepath);
                        var exportToRead = importPCC.getUExport(info.exportIndex);
                        byte[] buff = exportToRead.Data.Skip(0x30).ToArray();
                        PropertyCollection defaults = PropertyCollection.ReadProps(exportToRead, new MemoryStream(buff), className);
                        foreach (var prop in defaults)
                        {
                            structProps.TryReplaceProp(prop);
                        }

                    }
                    return structProps;
                }
                catch
                {
                    return null;
                }
            }
            return null;
        }

        public static UProperty getDefaultProperty(string propName, PropertyInfo propInfo, bool stripTransients = true, bool isImmutable = false)
        {
            switch (propInfo.Type)
            {
                case PropertyType.IntProperty:
                    return new IntProperty(0, propName);
                case PropertyType.FloatProperty:
                    return new FloatProperty(0f, propName);
                case PropertyType.DelegateProperty:
                    return new DelegateProperty(0, "None");
                case PropertyType.ObjectProperty:
                    return new ObjectProperty(0, propName);
                case PropertyType.NameProperty:
                    return new NameProperty("None", propName);
                case PropertyType.BoolProperty:
                    return new BoolProperty(false, propName);
                case PropertyType.ByteProperty when propInfo.IsEnumProp():
                    return new EnumProperty(propInfo.Reference, Mod.MEGame.ME1, propName);
                case PropertyType.ByteProperty:
                    return new ByteProperty(0, propName);
                case PropertyType.StrProperty:
                    return new StrProperty("", propName);
                case PropertyType.StringRefProperty:
                    return new StringRefProperty(propName);
                case PropertyType.BioMask4Property:
                    return new BioMask4Property(0, propName);
                case PropertyType.ArrayProperty:
                    switch (getArrayType(propInfo))
                    {
                        case ArrayType.Object:
                            return new ArrayProperty<ObjectProperty>(propName);
                        case ArrayType.Name:
                            return new ArrayProperty<NameProperty>(propName);
                        case ArrayType.Enum:
                            return new ArrayProperty<EnumProperty>(propName);
                        case ArrayType.Struct:
                            return new ArrayProperty<StructProperty>(propName);
                        case ArrayType.Bool:
                            return new ArrayProperty<BoolProperty>(propName);
                        case ArrayType.String:
                            return new ArrayProperty<StrProperty>(propName);
                        case ArrayType.Float:
                            return new ArrayProperty<FloatProperty>(propName);
                        case ArrayType.Int:
                            return new ArrayProperty<IntProperty>(propName);
                        case ArrayType.Byte:
                            return new ArrayProperty<ByteProperty>(propName);
                        default:
                            return null;
                    }
                case PropertyType.StructProperty:
                    isImmutable = isImmutable || UnrealObjectInfo.IsImmutable(propInfo.Reference, Mod.MEGame.ME1);
                    return new StructProperty(propInfo.Reference, getDefaultStructValue(propInfo.Reference, stripTransients), propName, isImmutable);
                case PropertyType.None:
                case PropertyType.Unknown:
                default:
                    return null;
            }
        }

        public static bool InheritsFrom(string className, string baseClass)
        {
            while (Classes.ContainsKey(className))
            {
                if (className == baseClass)
                {
                    return true;
                }
                className = Classes[className].baseClass;
            }
            return false;
        }

        public static ClassInfo generateClassInfo(ExportEntry export, bool isStruct = false)
        {
            IMEPackage pcc = export.FileRef;
            ClassInfo info = new ClassInfo
            {
                baseClass = export.SuperClassName,
                exportIndex = export.UIndex
            };
            if (pcc.FilePath.Contains("BioGame"))
            {
                info.pccPath = new string(pcc.FilePath.Skip(pcc.FilePath.LastIndexOf("BioGame") + 8).ToArray());
            }
            else
            {
                info.pccPath = pcc.FilePath; //used for dynamic resolution of files outside the game directory.
            }

            int nextExport = BitConverter.ToInt32(export.Data, isStruct ? 0x18 : 0x10);
            while (nextExport > 0)
            {
                var entry = pcc.getUExport(nextExport);
                if (entry.ClassName != "ScriptStruct" && entry.ClassName != "Enum"
                    && entry.ClassName != "Function" && entry.ClassName != "Const" && entry.ClassName != "State")
                {
                    if (!info.properties.ContainsKey(entry.ObjectName))
                    {
                        PropertyInfo p = getProperty(entry);
                        if (p != null)
                        {
                            info.properties.Add(entry.ObjectName, p);
                        }
                    }
                }
                nextExport = BitConverter.ToInt32(entry.Data, 0x10);
            }
            return info;
        }

        private static void generateEnumValues(ExportEntry export)
        {
            string enumName = export.ObjectName;
            if (!Enums.ContainsKey(enumName))
            {
                var values = new List<NameReference>();
                byte[] buff = export.Data;
                int count = BitConverter.ToInt32(buff, 20);
                for (int i = 0; i < count; i++)
                {
                    int enumValIndex = 24 + i * 8;
                    values.Add(new NameReference(export.FileRef.Names[BitConverter.ToInt32(buff, enumValIndex)], BitConverter.ToInt32(buff, enumValIndex + 4)));
                }
                Enums.Add(enumName, values);
            }
        }

        private static PropertyInfo getProperty(ExportEntry entry)
        {
            IMEPackage pcc = entry.FileRef;

            string reference = null;
            PropertyType type;
            switch (entry.ClassName)
            {
                case "IntProperty":
                    type = PropertyType.IntProperty;
                    break;
                case "StringRefProperty":
                    type = PropertyType.StringRefProperty;
                    break;
                case "FloatProperty":
                    type = PropertyType.FloatProperty;
                    break;
                case "BoolProperty":
                    type = PropertyType.BoolProperty;
                    break;
                case "StrProperty":
                    type = PropertyType.StrProperty;
                    break;
                case "NameProperty":
                    type = PropertyType.NameProperty;
                    break;
                case "DelegateProperty":
                    type = PropertyType.DelegateProperty;
                    break;
                case "ObjectProperty":
                case "ClassProperty":
                case "ComponentProperty":
                    type = PropertyType.ObjectProperty;
                    reference = pcc.getObjectName(BitConverter.ToInt32(entry.Data, entry.Data.Length - 4));
                    break;
                case "StructProperty":
                    type = PropertyType.StructProperty;
                    reference = pcc.getObjectName(BitConverter.ToInt32(entry.Data, entry.Data.Length - 4));
                    break;
                case "BioMask4Property":
                case "ByteProperty":
                    type = PropertyType.ByteProperty;
                    reference = pcc.getObjectName(BitConverter.ToInt32(entry.Data, entry.Data.Length - 4));
                    break;
                case "ArrayProperty":
                    type = PropertyType.ArrayProperty;
                    PropertyInfo arrayTypeProp = getProperty(pcc.getUExport(BitConverter.ToInt32(entry.Data, 44)));
                    if (arrayTypeProp != null)
                    {
                        switch (arrayTypeProp.Type)
                        {
                            case PropertyType.ObjectProperty:
                            case PropertyType.StructProperty:
                            case PropertyType.ArrayProperty:
                                reference = arrayTypeProp.Reference;
                                break;
                            case PropertyType.ByteProperty:
                                if (arrayTypeProp.Reference == "Class")
                                    reference = arrayTypeProp.Type.ToString();
                                else
                                    reference = arrayTypeProp.Reference;
                                break;
                            case PropertyType.IntProperty:
                            case PropertyType.FloatProperty:
                            case PropertyType.NameProperty:
                            case PropertyType.BoolProperty:
                            case PropertyType.StrProperty:
                            case PropertyType.StringRefProperty:
                            case PropertyType.DelegateProperty:
                                reference = arrayTypeProp.Type.ToString();
                                break;
                            case PropertyType.None:
                            case PropertyType.Unknown:
                            default:
                                System.Diagnostics.Debugger.Break();
                                return null;
                        }
                    }
                    else
                    {
                        return null;
                    }
                    break;
                case "InterfaceProperty":
                default:
                    return null;
            }

            bool transient = (BitConverter.ToUInt64(entry.Data, 24) & 0x0000000000002000) != 0;
            return new PropertyInfo(type, reference, transient);
        }
    }
}
