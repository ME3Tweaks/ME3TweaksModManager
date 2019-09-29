using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MassEffectModManagerCore.modmanager.helpers;

namespace ME3Explorer.Packages
{
    public abstract class UnrealPackageFile : NotifyPropertyChangedBase
    {
        protected const uint packageTag = 0x9E2A83C1;
        public string FilePath { get; protected set; }

        public bool IsModified
        {
            get
            {
                return exports.Any(entry => entry.DataChanged || entry.HeaderChanged) || imports.Any(entry => entry.HeaderChanged) || namesAdded > 0;
            }
        }
        public abstract int NameCount { get; protected set; }
        public abstract int ExportCount { get; protected set; }
        public abstract int ImportCount { get; protected set; }

        #region Names
        protected uint namesAdded;
        protected List<string> names = new List<string>();
        public IReadOnlyList<string> Names => names;

        public bool isName(int index) => index >= 0 && index < names.Count;

        public string getNameEntry(int index) => isName(index) ? names[index] : "";

        public int FindNameOrAdd(string name)
        {
            for (int i = 0; i < names.Count; i++)
            {
                if (names[i] == name)
                    return i;
            }

            addName(name);
            return names.Count - 1;
        }

        public void addName(string name)
        {
            if (name == null)
            {
                throw new Exception("Cannot add a null name to the list of names for a package file.\nThis is a bug in ME3Explorer.");
            }
            if (!names.Contains(name))
            {
                names.Add(name);
                namesAdded++;
                NameCount = names.Count;

                OnPropertyChanged(nameof(NameCount));
            }
        }

        public void replaceName(int idx, string newName)
        {
            if (isName(idx))
            {
                names[idx] = newName;
            }
        }

        /// <summary>
        /// Checks whether a name exists in the PCC and returns its index
        /// If it doesn't exist returns -1
        /// </summary>
        /// <param name="nameToFind">The name of the string to find</param>
        /// <returns></returns>
        public int findName(string nameToFind)
        {
            for (int i = 0; i < names.Count; i++)
            {
                if (String.Compare(nameToFind, getNameEntry(i)) == 0)
                    return i;
            }
            return -1;
        }

        public void setNames(List<string> list) => names = list;

        #endregion

        #region Exports
        protected List<ExportEntry> exports = new List<ExportEntry>();
        public IReadOnlyList<ExportEntry> Exports => exports;

        public bool isUExport(int uindex) => uindex > 0 && uindex <= exports.Count;

        public void addExport(ExportEntry exportEntry)
        {
            if (exportEntry.FileRef != this)
                throw new Exception("Cannot add an export entry from another package file");

            exportEntry.DataChanged = true;
            exportEntry.Index = exports.Count;
            exports.Add(exportEntry);
            ExportCount = exports.Count;

            OnPropertyChanged(nameof(ExportCount));
        }

        public ExportEntry getExport(int index) => exports[index];
        public ExportEntry getUExport(int uindex) => exports[uindex - 1];

        #endregion

        #region Imports
        protected List<ImportEntry> imports = new List<ImportEntry>();
        public IReadOnlyList<ImportEntry> Imports => imports;

        public bool isUImport(int uindex) => (uindex < 0 && Math.Abs(uindex) <= ImportCount);

        public void addImport(ImportEntry importEntry)
        {
            if (importEntry.FileRef != this)
                throw new Exception("you cannot add a new import entry from another package file, it has invalid references!");

            importEntry.Index = imports.Count;
            imports.Add(importEntry);
            importEntry.EntryHasPendingChanges = true;
            ImportCount = imports.Count;

            OnPropertyChanged(nameof(ImportCount));
        }

        public ImportEntry getUImport(int uindex) => imports[Math.Abs(uindex) - 1];

        #endregion

        #region IEntry
        /// <summary>
        ///     gets Export or Import name
        /// </summary>
        /// <param name="uIndex">unreal index</param>
        public string getObjectName(int uIndex)
        {
            if (isEntry(uIndex))
                return getEntry(uIndex).ObjectName;
            if (uIndex == 0)
                return "Class";
            return "";
        }

        /// <summary>
        ///     gets Export or Import class
        /// </summary>
        /// <param name="uIndex">unreal index</param>
        public string getObjectClass(int uIndex)
        {
            if (isEntry(uIndex))
                return getEntry(uIndex).ClassName;
            return "";
        }

        /// <summary>
        ///     gets Export or Import entry
        /// </summary>
        /// <param name="uindex">unreal index</param>
        public IEntry getEntry(int uindex)
        {
            if (isUExport(uindex))
                return exports[uindex - 1];
            if (isUImport(uindex))
                return imports[-uindex - 1];
            return null;
        }
        public bool isEntry(int uindex) => (uindex > 0 && uindex <= ExportCount) || (uindex < 0 && -uindex <= ImportCount);

        public void RemoveTrailingTrash()
        {
            ExportEntry trashPackage = exports.FirstOrDefault(exp => exp.ObjectName == TrashPackageName);
            if (trashPackage == null)
            {
                return;
            }
            int trashPackageUIndex = trashPackage.UIndex;
            //make sure the first trashed export is the trashpackage
            foreach (ExportEntry exp in exports)
            {
                if (exp == trashPackage)
                {
                    //trashpackage is the first trashed export, so we're good
                    break;
                }
                if (exp.idxLink == trashPackageUIndex)
                {
                    //turn this into trashpackage, turn old trashpackage into regular Trash, and point all trash entries to the new trashpackage
                    exp.ObjectName = TrashPackageName;
                    exp.idxLink = 0;
                    exp.PackageGUID = TrashPackageGuid;

                    trashPackage.ObjectName = "Trash";
                    trashPackage.idxLink = exp.UIndex;
                    trashPackage.PackageGUID = Guid.Empty;

                    foreach (IEntry entry in trashPackage.GetChildren())
                    {
                        entry.idxLink = exp.UIndex;
                    }

                    trashPackage = exp;
                    trashPackageUIndex = trashPackage.UIndex;
                    break;
                }
            }


            //remove imports
            for (int i = ImportCount - 1; i >= 0; i--)
            {
                ImportEntry lastImport = imports[i];
                if (lastImport.idxLink != trashPackageUIndex)
                {
                    //non-trash import, so stop removing
                    break;
                }

                imports.RemoveAt(i);
            }
            if (ImportCount != imports.Count)
            {
                ImportCount = imports.Count;
                OnPropertyChanged(nameof(ImportCount));
            }

            //remove exports
            for (int i = ExportCount - 1; i >= 0; i--)
            {
                ExportEntry lastExport = exports[i];
                if (lastExport.idxLink != trashPackageUIndex)
                {
                    //non-trash export, so stop removing
                    break;
                }

                exports.RemoveAt(i);
            }
            if (ExportCount != exports.Count)
            {
                ExportCount = exports.Count;
                OnPropertyChanged(nameof(ExportCount));
            }
            //if there are no more trashed imports or exports, and if the TrashPackage is the last export, remove it
            if (exports.LastOrDefault() is ExportEntry finalExport && finalExport == trashPackage && trashPackage.GetChildren().IsEmpty())
            {
                exports.Remove(trashPackage);
            }
            if (ExportCount != exports.Count)
            {
                ExportCount = exports.Count;
                OnPropertyChanged(nameof(ExportCount));
            }
        }

        #endregion

        private DateTime? lastSaved;
        public DateTime LastSaved
        {
            get
            {
                if (lastSaved.HasValue)
                {
                    return lastSaved.Value;
                }

                if (File.Exists(FilePath))
                {
                    return (new FileInfo(FilePath)).LastWriteTime;
                }

                return DateTime.MinValue;
            }
        }

        public long FileSize => File.Exists(FilePath) ? (new FileInfo(FilePath)).Length : 0;

        protected virtual void AfterSave()
        {
            //We do if checks here to prevent firing tons of extra events as we can't prevent firing change notifications if 
            //it's not really a change due to the side effects of suppressing that.
            foreach (var export in exports)
            {
                if (export.DataChanged)
                {
                    export.DataChanged = false;
                }
                if (export.HeaderChanged)
                {
                    export.HeaderChanged = false;
                }
                if (export.EntryHasPendingChanges)
                {
                    export.EntryHasPendingChanges = false;
                }
            }
            foreach (var import in imports)
            {
                if (import.HeaderChanged)
                {
                    import.HeaderChanged = false;
                }
                if (import.EntryHasPendingChanges)
                {
                    import.EntryHasPendingChanges = false;
                }
            }
            namesAdded = 0;

            lastSaved = DateTime.Now;
            OnPropertyChanged(nameof(LastSaved));
            OnPropertyChanged(nameof(FileSize));
            OnPropertyChanged(nameof(IsModified));
        }

        public const string TrashPackageName = "ME3ExplorerTrashPackage";
        public static Guid TrashPackageGuid = "ME3ExpTrashPackage".ToGuid(); //DO NOT EDIT!!
    }
}
