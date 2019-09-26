using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using MassEffectModManager;
using MassEffectModManager.GameDirectories;
using MassEffectModManager.modmanager;
using MassEffectModManager.modmanager.helpers;
using ME3Explorer.Packages;
using Newtonsoft.Json;

namespace ME3Explorer.Unreal
{
    public static class UnrealObjectInfo
    {
        public static bool IsImmutable(string structType, Mod.MEGame game)
        {
            switch (game)
            {
                case Mod.MEGame.ME1:
                    return ME1UnrealObjectInfo.IsImmutableStruct(structType);
                case Mod.MEGame.ME2:
                    return ME2UnrealObjectInfo.IsImmutableStruct(structType);
                case Mod.MEGame.ME3:
                    return ME3UnrealObjectInfo.IsImmutableStruct(structType);
                default:
                    return false;
            }
        }

        public static bool InheritsFrom(this IEntry entry, string baseClass)
        {
            switch (entry.FileRef.Game)
            {
                case Mod.MEGame.ME1:
                    return ME1UnrealObjectInfo.InheritsFrom(entry, baseClass);
                case Mod.MEGame.ME2:
                    return ME2UnrealObjectInfo.InheritsFrom(entry, baseClass);
                case Mod.MEGame.ME3:
                    return ME3UnrealObjectInfo.InheritsFrom(entry, baseClass);
                default:
                    return false;
            }
        }

        public static string GetEnumType(Mod.MEGame game, string propName, string typeName, ClassInfo nonVanillaClassInfo = null)
        {
            switch (game)
            {
                case Mod.MEGame.ME1:
                    return ME1UnrealObjectInfo.getEnumTypefromProp(typeName, propName, nonVanillaClassInfo: nonVanillaClassInfo);
                case Mod.MEGame.ME2:
                    return ME2UnrealObjectInfo.getEnumTypefromProp(typeName, propName, nonVanillaClassInfo: nonVanillaClassInfo);
                case Mod.MEGame.ME3:
                    var enumType = ME3UnrealObjectInfo.getEnumTypefromProp(typeName, propName);
                    return enumType;
            }
            return null;
        }

        public static List<NameReference> GetEnumValues(Mod.MEGame game, string enumName, bool includeNone = false)
        {
            switch (game)
            {
                case Mod.MEGame.ME1:
                    return ME1UnrealObjectInfo.getEnumValues(enumName, includeNone);
                case Mod.MEGame.ME2:
                    return ME2UnrealObjectInfo.getEnumValues(enumName, includeNone);
                case Mod.MEGame.ME3:
                    return ME3UnrealObjectInfo.getEnumValues(enumName, includeNone);
            }
            return null;
        }

        /// <summary>
        /// Gets the type of an array
        /// </summary>
        /// <param name="game">What game we are looking info for</param>
        /// <param name="propName">Name of the array property</param>
        /// <param name="className">Name of the class that should contain the information. If contained in a struct, this will be the name of the struct type</param>
        /// <param name="parsingEntry">Entry that is being parsed. Used for dynamic lookup if it's not in the DB</param>
        /// <returns></returns>
        public static ArrayType GetArrayType(Mod.MEGame game, string propName, string className, IEntry parsingEntry = null)
        {
            switch (game)
            {
                case Mod.MEGame.ME1:
                    return ME1UnrealObjectInfo.getArrayType(className, propName, export: parsingEntry as ExportEntry);
                case Mod.MEGame.ME2:
                    var res2 = ME2UnrealObjectInfo.getArrayType(className, propName, export: parsingEntry as ExportEntry);
#if DEBUG
                    //For debugging only!
                    if (res2 == ArrayType.Int && ME2UnrealObjectInfo.ArrayTypeLookupJustFailed)
                    {
                        ME2UnrealObjectInfo.ArrayTypeLookupJustFailed = false;
                        Debug.WriteLine("[ME2] Array type lookup failed for " + propName + " in class " + className + " in export " + parsingEntry.FileRef.GetEntryString(parsingEntry.UIndex));
                    }
#endif
                    return res2;
                case Mod.MEGame.ME3:
                    var res = ME3UnrealObjectInfo.getArrayType(className, propName, export: parsingEntry as ExportEntry);
#if DEBUG
                    //For debugging only!
                    if (res == ArrayType.Int && ME3UnrealObjectInfo.ArrayTypeLookupJustFailed)
                    {
                        Debug.WriteLine("[ME3] Array type lookup failed for " + propName + " in class " + className + " in export " + parsingEntry.FileRef.GetEntryString(parsingEntry.UIndex));
                        ME3UnrealObjectInfo.ArrayTypeLookupJustFailed = false;
                    }
#endif
                    return res;
            }
            return ArrayType.Int;
        }

        /// <summary>
        /// Gets property information for a property by name & containing class or struct name
        /// </summary>
        /// <param name="game">Game to lookup informatino from</param>
        /// <param name="propname">Name of property information to look up</param>
        /// <param name="containingClassOrStructName">Name of containing class or struct name</param>
        /// <param name="nonVanillaClassInfo">Dynamically built property info</param>
        /// <returns></returns>
        public static PropertyInfo GetPropertyInfo(Mod.MEGame game, string propname, string containingClassOrStructName, ClassInfo nonVanillaClassInfo = null)
        {
            bool inStruct = false;
            PropertyInfo p = null;
            switch (game)
            {
                case Mod.MEGame.ME1:
                    p = ME1UnrealObjectInfo.getPropertyInfo(containingClassOrStructName, propname, inStruct, nonVanillaClassInfo);
                    break;
                case Mod.MEGame.ME2:
                    p = ME2UnrealObjectInfo.getPropertyInfo(containingClassOrStructName, propname, inStruct, nonVanillaClassInfo);
                    break;
                case Mod.MEGame.ME3:
                    p = ME3UnrealObjectInfo.getPropertyInfo(containingClassOrStructName, propname, inStruct, nonVanillaClassInfo);
                    break;
            }
            if (p == null)
            {
                inStruct = true;
                switch (game)
                {
                    case Mod.MEGame.ME1:
                        p = ME1UnrealObjectInfo.getPropertyInfo(containingClassOrStructName, propname, inStruct);
                        break;
                    case Mod.MEGame.ME2:
                        p = ME2UnrealObjectInfo.getPropertyInfo(containingClassOrStructName, propname, inStruct);
                        break;
                    case Mod.MEGame.ME3:
                        p = ME3UnrealObjectInfo.getPropertyInfo(containingClassOrStructName, propname, inStruct);
                        break;
                }
            }
            return p;
        }

        /// <summary>
        /// Gets the default values for a struct
        /// </summary>
        /// <param name="game">Game to pull info from</param>
        /// <param name="typeName">Struct type name</param>
        /// <param name="stripTransients">Strip transients from the struct</param>
        /// <returns></returns>
        internal static PropertyCollection getDefaultStructValue(Mod.MEGame game, string typeName, bool stripTransients)
        {
            switch (game)
            {
                case Mod.MEGame.ME1:
                    return ME1UnrealObjectInfo.getDefaultStructValue(typeName, stripTransients);
                case Mod.MEGame.ME2:
                    return ME2UnrealObjectInfo.getDefaultStructValue(typeName, stripTransients);
                case Mod.MEGame.ME3:
                    return ME3UnrealObjectInfo.getDefaultStructValue(typeName, stripTransients);
            }
            return null;
        }

        public static OrderedMultiValueDictionary<string, PropertyInfo> GetAllProperties(Mod.MEGame game, string typeName)
        {
            var props = new OrderedMultiValueDictionary<string, PropertyInfo>();
            ClassInfo info = GetClassOrStructInfo(game, typeName);
            while (info != null)
            {
                props.AddRange(info.properties);
                info = GetClassOrStructInfo(game, info.baseClass);
            }

            return props;
        }

        public static ClassInfo GetClassOrStructInfo(Mod.MEGame game, string typeName)
        {
            ClassInfo result = null;
            switch (game)
            {
                case Mod.MEGame.ME1:
                    _ = ME1UnrealObjectInfo.Classes.TryGetValue(typeName, out result) || ME1UnrealObjectInfo.Structs.TryGetValue(typeName, out result);
                    break;
                case Mod.MEGame.ME2:
                    _ = ME2UnrealObjectInfo.Classes.TryGetValue(typeName, out result) || ME2UnrealObjectInfo.Structs.TryGetValue(typeName, out result);
                    break;
                case Mod.MEGame.ME3:
                    _ = ME3UnrealObjectInfo.Classes.TryGetValue(typeName, out result) || ME3UnrealObjectInfo.Structs.TryGetValue(typeName, out result);
                    break;
            }

            return result;
        }

        public static Dictionary<string, ClassInfo> GetClasses(Mod.MEGame game)
        {
            switch (game)
            {
                case Mod.MEGame.ME1:
                    return ME1UnrealObjectInfo.Classes;
                case Mod.MEGame.ME2:
                    return ME2UnrealObjectInfo.Classes;
                case Mod.MEGame.ME3:
                    return ME3UnrealObjectInfo.Classes;
                default:
                    return null;
            }
        }

        public static Dictionary<string, ClassInfo> GetStructs(Mod.MEGame game)
        {
            switch (game)
            {
                case Mod.MEGame.ME1:
                    return ME1UnrealObjectInfo.Structs;
                case Mod.MEGame.ME2:
                    return ME2UnrealObjectInfo.Structs;
                case Mod.MEGame.ME3:
                    return ME3UnrealObjectInfo.Structs;
                default:
                    return null;
            }
        }

        public static Dictionary<string, List<NameReference>> GetEnums(Mod.MEGame game)
        {
            switch (game)
            {
                case Mod.MEGame.ME1:
                    return ME1UnrealObjectInfo.Enums;
                case Mod.MEGame.ME2:
                    return ME2UnrealObjectInfo.Enums;
                case Mod.MEGame.ME3:
                    return ME3UnrealObjectInfo.Enums;
                default:
                    return null;
            }
        }

        public static ClassInfo generateClassInfo(ExportEntry export, bool isStruct = false)
        {
            switch (export.FileRef.Game)
            {
                case Mod.MEGame.ME1:
                    return ME1UnrealObjectInfo.generateClassInfo(export, isStruct);
                case Mod.MEGame.ME2:
                    return ME2UnrealObjectInfo.generateClassInfo(export, isStruct);
                case Mod.MEGame.ME3:
                    return ME3UnrealObjectInfo.generateClassInfo(export, isStruct);
            }

            return null;
        }

        public static UProperty getDefaultProperty(Mod.MEGame game, string propName, PropertyInfo propInfo, bool stripTransients = true, bool isImmutable = false)
        {
            switch (game)
            {
                case Mod.MEGame.ME1:
                    return ME1UnrealObjectInfo.getDefaultProperty(propName, propInfo, stripTransients, isImmutable);
                case Mod.MEGame.ME2:
                    return ME2UnrealObjectInfo.getDefaultProperty(propName, propInfo, stripTransients, isImmutable);
                case Mod.MEGame.ME3:
                    return ME3UnrealObjectInfo.getDefaultProperty(propName, propInfo, stripTransients, isImmutable);
            }

            return null;
        }
    }

    public static class ME3UnrealObjectInfo
    {

        public class SequenceObjectInfo
        {
            public List<string> inputLinks;

            public SequenceObjectInfo()
            {
                inputLinks = new List<string>();
            }
        }

        public static Dictionary<string, ClassInfo> Classes = new Dictionary<string, ClassInfo>();
        public static Dictionary<string, ClassInfo> Structs = new Dictionary<string, ClassInfo>();
        public static Dictionary<string, SequenceObjectInfo> SequenceObjects = new Dictionary<string, SequenceObjectInfo>();
        public static Dictionary<string, List<NameReference>> Enums = new Dictionary<string, List<NameReference>>();

        private static readonly string[] ImmutableStructs = { "Vector", "Color", "LinearColor", "TwoVectors", "Vector4", "Vector2D", "Rotator", "Guid", "Plane", "Box",
            "Quat", "Matrix", "IntPoint", "ActorReference", "ActorReference", "ActorReference", "PolyReference", "AimTransform", "AimTransform", "AimOffsetProfile", "FontCharacter",
            "CoverReference", "CoverInfo", "CoverSlot", "BioRwBox", "BioMask4Property", "RwVector2", "RwVector3", "RwVector4", "BioRwBox44" };

        private static readonly string jsonPath = Path.Combine(Utilities.GetObjectInfoFolder(), "ME3ObjectInfo.json");

        public static bool IsImmutableStruct(string structName)
        {
            return ImmutableStructs.Contains(structName);
        }

        public static void loadfromJSON()
        {

            try
            {
                if (File.Exists(jsonPath))
                {
                    string raw = File.ReadAllText(jsonPath);
                    var blob = JsonConvert.DeserializeAnonymousType(raw, new { SequenceObjects, Classes, Structs, Enums });
                    SequenceObjects = blob.SequenceObjects;
                    Classes = blob.Classes;
                    Structs = blob.Structs;
                    Enums = blob.Enums;
                }
            }
            catch (Exception)
            {
            }
        }

        public static SequenceObjectInfo getSequenceObjectInfo(string objectName)
        {
            if (objectName.StartsWith("Default__"))
            {
                objectName = objectName.Substring(9);
            }
            if (SequenceObjects.ContainsKey(objectName))
            {
                if (SequenceObjects[objectName].inputLinks != null && SequenceObjects[objectName].inputLinks.Count > 0)
                {
                    return SequenceObjects[objectName];
                }

                return getSequenceObjectInfo(Classes[objectName].baseClass);
            }
            return null;
        }

        public static string getEnumTypefromProp(string className, string propName)
        {
            PropertyInfo p = getPropertyInfo(className, propName);
            if (p == null)
            {
                p = getPropertyInfo(className, propName, true);
            }
            return p?.Reference;
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
            PropertyInfo p = getPropertyInfo(className, propName, false, containingExport: export);
            if (p == null)
            {
                p = getPropertyInfo(className, propName, true, containingExport: export);
            }
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
                    p = getPropertyInfo(className, propName, false, currentInfo, containingExport: export);
                    if (p == null)
                    {
                        p = getPropertyInfo(className, propName, true, currentInfo, containingExport: export);
                    }
                }
            }
            return getArrayType(p);
        }

#if DEBUG
        public static bool ArrayTypeLookupJustFailed;
#endif

        public static ArrayType getArrayType(PropertyInfo p)
        {
            if (p != null)
            {
                if (p.Reference == "NameProperty")
                {
                    return ArrayType.Name;
                }

                if (Enums.ContainsKey(p.Reference))
                {
                    return ArrayType.Enum;
                }

                if (p.Reference == "BoolProperty")
                {
                    return ArrayType.Bool;
                }

                if (p.Reference == "ByteProperty")
                {
                    return ArrayType.Byte;
                }

                if (p.Reference == "StrProperty")
                {
                    return ArrayType.String;
                }

                if (p.Reference == "FloatProperty")
                {
                    return ArrayType.Float;
                }

                if (p.Reference == "IntProperty")
                {
                    return ArrayType.Int;
                }

                if (Structs.ContainsKey(p.Reference))
                {
                    return ArrayType.Struct;
                }

                return ArrayType.Object;
            }
#if DEBUG
            ArrayTypeLookupJustFailed = true;
#endif
            Debug.WriteLine("ME3 Array type lookup failed due to no info provided, defaulting to int");
            return ArrayType.Int;
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

                if (inStruct)
                {
                    foreach (PropertyInfo p in info.properties.Values())
                    {
                        if ((p.Type == PropertyType.StructProperty || p.Type == PropertyType.ArrayProperty) && reSearch)
                        {
                            PropertyInfo val = getPropertyInfo(p.Reference, propName, true, nonVanillaClassInfo);
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
                    PropertyInfo val = getPropertyInfo(info.baseClass, propName, inStruct, nonVanillaClassInfo);
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

        public static PropertyCollection getDefaultStructValue(string structName, bool stripTransients)
        {
            bool isImmutable = UnrealObjectInfo.IsImmutable(structName, Mod.MEGame.ME3);
            if (Structs.ContainsKey(structName))
            {
                try
                {
                    PropertyCollection structProps = new PropertyCollection();
                    ClassInfo info = Structs[structName];
                    while (info != null)
                    {
                        foreach ((string propName, PropertyInfo propInfo) in info.properties)
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
                        string filepath = Path.Combine(ME3Directory.gamePath, "BIOGame", info.pccPath);
                        if (File.Exists(info.pccPath))
                        {
                            filepath = info.pccPath; //Used for dynamic lookup
                        }
                        if (File.Exists(filepath))
                        {
                            IMEPackage importPCC = MEPackageHandler.OpenMEPackage(filepath);
                            var exportToRead = importPCC.getUExport(info.exportIndex);
                            byte[] buff = exportToRead.Data.Skip(0x24).ToArray();
                            PropertyCollection defaults = PropertyCollection.ReadProps(exportToRead, new MemoryStream(buff), structName);
                            foreach (var prop in defaults)
                            {
                                structProps.TryReplaceProp(prop);
                            }

                        }

                        Structs.TryGetValue(info.baseClass, out info);
                    }
                    structProps.Add(new NoneProperty());

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
                    return new EnumProperty(propInfo.Reference, Mod.MEGame.ME3, propName);
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
                    isImmutable = isImmutable || UnrealObjectInfo.IsImmutable(propInfo.Reference, Mod.MEGame.ME3);
                    return new StructProperty(propInfo.Reference, getDefaultStructValue(propInfo.Reference, stripTransients), propName, isImmutable);
                case PropertyType.None:
                case PropertyType.Unknown:
                default:
                    return null;
            }
        }

        public static bool InheritsFrom(IEntry entry, string baseClass)
        {
            string className = entry.ClassName;
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

        #region Generating
        public static ClassInfo generateClassInfo(ExportEntry export, bool isStruct = false)
        {
            IMEPackage pcc = export.FileRef;
            ClassInfo info = new ClassInfo
            {
                baseClass = export.SuperClassName,
                exportIndex = export.UIndex
            };
            if (pcc.FilePath.Contains("BIOGame"))
            {
                info.pccPath = new string(pcc.FilePath.Skip(pcc.FilePath.LastIndexOf("BIOGame") + 8).ToArray());
            }
            else
            {
                info.pccPath = pcc.FilePath; //used for dynamic resolution of files outside the game directory.
            }
            int nextExport = BitConverter.ToInt32(export.Data, isStruct ? 0x14 : 0xC);
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
                                Debugger.Break();
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

            bool transient = ((UnrealFlags.EPropertyFlags)BitConverter.ToUInt64(entry.Data, 24)).HasFlag(UnrealFlags.EPropertyFlags.Transient);
            return new PropertyInfo(type, reference, transient);
        }
        #endregion
    }
}
