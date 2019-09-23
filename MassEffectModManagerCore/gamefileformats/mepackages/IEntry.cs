using System.ComponentModel;
using System.Diagnostics;
using MassEffectModManager.modmanager;
using ME3Explorer.Unreal;

namespace ME3Explorer.Packages
{
    public interface IEntry
    {
        event PropertyChangedEventHandler PropertyChanged;
        bool EntryHasPendingChanges { get; set; } //used to signal that this entry has uncommited changes
        bool HeaderChanged { get; set; }
        int Index { get; set; }
        int indexValue { get; set; }
        int UIndex { get; }
        byte[] Header { get; set; }
        IMEPackage FileRef { get; }
        Mod.MEGame Game { get; }
        int idxLink { get; set; }
        int idxObjectName { get; set; }
        string ClassName { get; }
        string GetFullPath { get; }
        string GetInstancedFullPath { get; }
        string GetIndexedFullPath { get; }
        string ObjectName { get; }
        string PackageFullName { get; }
        string PackageFullNameInstanced { get; }
        string PackageName { get; }
        string PackageNameInstanced { get; }
        byte[] GetHeader(); //returns clone
        bool HasParent { get; }
        IEntry Parent { get; set; }
    }
}
