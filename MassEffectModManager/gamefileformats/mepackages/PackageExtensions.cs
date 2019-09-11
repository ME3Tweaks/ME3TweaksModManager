using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MassEffectModManager.modmanager.helpers;
using ME3Explorer.Unreal;

namespace ME3Explorer.Packages
{
    public static class MEPackageExtensions
    {

        public static string GetEntryString(this IMEPackage pcc, int index)
        {
            if (index == 0)
            {
                return "Null";
            }
            string retStr = "Entry not found";
            IEntry coreRefEntry = pcc.getEntry(index);
            if (coreRefEntry != null)
            {
                retStr = coreRefEntry is ImportEntry ? "[I] " : "[E] ";
                retStr += coreRefEntry.GetIndexedFullPath;
            }
            return retStr;
        }

        public static string FollowLink(this IMEPackage pcc, int uIndex)
        {
            if (pcc.isUExport(uIndex))
            {
                ExportEntry parent = pcc.getUExport(uIndex);
                return pcc.FollowLink(parent.idxLink) + parent.ObjectName + ".";
            }
            if (pcc.isUImport(uIndex))
            {
                ImportEntry parent = pcc.getUImport(uIndex);
                return pcc.FollowLink(parent.idxLink) + parent.ObjectName + ".";
            }
            return "";
        }

    }
    public static class ExportEntryExtensions
    {

        public static T GetProperty<T>(this ExportEntry export, string name) where T : UProperty
        {
            return export.GetProperties().GetProp<T>(name);
        }

        public static void WriteProperty(this ExportEntry export, UProperty prop)
        {
            var props = export.GetProperties();
            props.AddOrReplaceProp(prop);
            export.WriteProperties(props);
        }

        public static bool RemoveProperty(this ExportEntry export, string propname)
        {
            var props = export.GetProperties();
            UProperty propToRemove = null;
            foreach (UProperty prop in props)
            {
                if (prop.Name.Name == propname)
                {
                    propToRemove = prop;
                    break;
                }
            }

            //outside for concurrent collection modification
            if (propToRemove != null)
            {
                props.Remove(propToRemove);
                export.WriteProperties(props);
                return true;
            }

            return false;
        }
    }

    public static class IEntryExtensions
    {
        public static bool IsTrash(this IEntry entry)
        {
            return entry.ObjectName == UnrealPackageFile.TrashPackageName || entry.Parent?.ObjectName == UnrealPackageFile.TrashPackageName;
        }

        public static bool IsTexture(this IEntry entry)
        {
            return entry.ClassName == "Texture2D" ||
                   entry.ClassName == "LightMapTexture2D" ||
                   entry.ClassName == "ShadowMapTexture2D" ||
                   entry.ClassName == "TerrainWeightMapTexture" ||
                   entry.ClassName == "TextureFlipBook";
        }

        public static bool IsDescendantOf(this IEntry entry, IEntry ancestor)
        {
            while (entry.HasParent)
            {
                entry = entry.Parent;
                if (entry == ancestor)
                {
                    return true;
                }
            }
            return false;
        }

        public static List<IEntry> GetChildren(this IEntry entry)
        {
            var kids = new List<IEntry>();
            kids.AddRange(entry.FileRef.Exports.Where(export => export.idxLink == entry.UIndex));
            kids.AddRange(entry.FileRef.Imports.Where(import => import.idxLink == entry.UIndex));
            return kids;
        }

        public static List<IEntry> GetAllDescendants(this IEntry entry)
        {
            var kids = new List<IEntry>();
            kids.AddRange(entry.FileRef.Exports.Where(export => export.IsDescendantOf(entry)));
            kids.AddRange(entry.FileRef.Imports.Where(import => import.IsDescendantOf(entry)));
            return kids;
        }
    }
}
