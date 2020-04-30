using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using MassEffectModManagerCore.gamefileformats;
using MassEffectModManagerCore.modmanager;
using ME3Explorer.Unreal;
using Newtonsoft.Json;
using static ME3Explorer.Unreal.UnrealFlags;

namespace ME3Explorer.Packages
{

    public enum ArrayType
    {
        Object,
        Name,
        Enum,
        Struct,
        Bool,
        String,
        Float,
        Int,
        Byte,
    }

    [DebuggerDisplay("PropertyInfo | {Type} , parent: {Reference}, transient: {Transient}")]
    public class PropertyInfo : IEquatable<PropertyInfo>
    {
        public Unreal.PropertyType Type { get; }
        public string Reference { get; }
        public bool Transient { get; }

        public PropertyInfo(PropertyType type, string reference = null, bool transient = false)
        {
            Type = type;
            Reference = reference;
            Transient = transient;
        }

        public bool IsEnumProp() => Type == PropertyType.ByteProperty && Reference != null && Reference != "Class" && Reference != "Object";

        #region IEquatable

        public bool Equals(PropertyInfo other)
        {
            if (other is null) return false;
            if (ReferenceEquals(this, other)) return true;
            return Type == other.Type && string.Equals(Reference, other.Reference) && Transient == other.Transient;
        }

        public override bool Equals(object obj)
        {
            if (obj is null) return false;
            if (ReferenceEquals(this, obj)) return true;
            return obj.GetType() == this.GetType() && Equals((PropertyInfo) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = (int) Type;
                hashCode = (hashCode * 397) ^ (Reference != null ? Reference.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ Transient.GetHashCode();
                return hashCode;
            }
        }

        public static bool operator ==(PropertyInfo left, PropertyInfo right)
        {
            return Equals(left, right);
        }

        public static bool operator !=(PropertyInfo left, PropertyInfo right)
        {
            return !Equals(left, right);
        }

        #endregion
    }

    public class ClassInfo
    {
        [JsonIgnore]
        public string ClassName { get; set; }
        public OrderedMultiValueDictionary<string, PropertyInfo> properties = new OrderedMultiValueDictionary<string, PropertyInfo>();
        public string baseClass;
        //Relative to BIOGame
        public string pccPath;
        //0-based
        public int exportIndex;

        public bool TryGetPropInfo(string name, Mod.MEGame game, out PropertyInfo propInfo) =>
            properties.TryGetValue(name, out propInfo) || (UnrealObjectInfo.GetClassOrStructInfo(game, baseClass)?.TryGetPropInfo(name, game, out propInfo) ?? false);
    }

    public interface IMEPackage
    {
        EPackageFlags Flags { get; }
        bool IsCompressed { get; }
        bool CanReconstruct { get; }
        bool IsModified { get; }
        int NameCount { get; }
        int ExportCount { get; }
        int ImportCount { get; }
        int ImportOffset { get; }
        int ExportOffset { get; }
        int NameOffset { get; }
        Guid PackageGuid { get; set; }
        IReadOnlyList<ExportEntry> Exports { get; }
        IReadOnlyList<ImportEntry> Imports { get; }
        IReadOnlyList<string> Names { get; }
        Mod.MEGame Game { get; }
        string FilePath { get; }
        DateTime LastSaved { get; }
        long FileSize { get; }

        //reading
        bool isUExport(int index);
        bool isName(int index);
        bool isUImport(int index);
        bool isEntry(int uindex);
        /// <summary>
        ///     gets Export or Import entry, from unreal index
        /// </summary>
        /// <param name="index">unreal index</param>
        IEntry getEntry(int index);
        /// <summary>
        /// Gets an export based on it's 0 based index in the export list. (Not unreal indexing)
        /// </summary>
        /// <param name="index">0-based index in the export list</param>
        /// <returns></returns>
        ExportEntry getExport(int index);

        /// <summary>
        /// Gets an export based on it's unreal based index in the export list.
        /// </summary>
        /// <param name="uIndex">unreal-based index in the export list</param>
        ExportEntry getUExport(int uIndex);

        /// <summary>
        /// Gets an import based on it's unreal based index.
        /// </summary>
        /// <param name="uIndex">unreal-based index</param>
        ImportEntry getUImport(int uIndex);
        int findName(string nameToFind);
        /// <summary>
        ///     gets Export or Import name, from unreal index
        /// </summary>
        /// <param name="index">unreal index</param>
        string getObjectName(int index);
        string getNameEntry(int index);

        /// <summary>
        ///     gets Export or Import class, from unreal index
        /// </summary>
        /// <param name="index">unreal index</param>
        string getObjectClass(int index);

        //editing
        void addName(string name);
        int FindNameOrAdd(string name);
        void replaceName(int index, string newName);
        void addExport(ExportEntry exportEntry);
        void addImport(ImportEntry importEntry);
        /// <summary>
        ///     exposed so that the property import function can restore the namelist after a failure.
        ///     please don't use it anywhere else.
        /// </summary>
        void setNames(List<string> list);

        /// <summary>
        /// Removes trashed imports and exports if they are at the end of their respective lists
        /// can only remove from the end because doing otherwise would mess up the indexing
        /// </summary>
        void RemoveTrailingTrash();

        //saving
        void save(bool compress = false);
        MemoryStream saveToStream(bool compress = false, bool includeAdditionalPackagesToCook = true);
        void save(string path, bool compress = false);
        byte[] getHeader();
        //ObservableCollection<GenericWindow> Tools { get; }
        //void RegisterTool(GenericWindow tool);
        //void Release(System.Windows.Window wpfWindow = null, System.Windows.Forms.Form winForm = null);
        //event UnrealPackageFile.MEPackageEventHandler noLongerOpenInTools;
        //void RegisterUse();
        //event UnrealPackageFile.MEPackageEventHandler noLongerUsed;
        List<TalkFileME1> LocalTalkFiles { get; }
    }
}