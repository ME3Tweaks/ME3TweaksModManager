using ME3Explorer.Packages;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Serilog;

namespace MassEffectModManagerCore.modmanager.helpers
{
    public static class ThreeWayPackageMerge
    {
        public static bool AttemptMerge(IMEPackage vanillaPackage, IMEPackage modifiedVanillaPackage, IMEPackage targetPackage)
        {
            PackageDelta vanillaToModifiedDelta = PackageDelta.CalculateDelta(vanillaPackage, modifiedVanillaPackage);
            PackageDelta vanillaToTargetDelta = PackageDelta.CalculateDelta(vanillaPackage, targetPackage);

            //Check merge conditions
            var nameConflicts = vanillaToModifiedDelta.NameDeltas.Keys.Intersect(vanillaToTargetDelta.NameDeltas.Keys).ToList();
            var importConflicts = vanillaToModifiedDelta.ImportDeltas.Keys.Intersect(vanillaToTargetDelta.ImportDeltas.Keys).ToList();
            var exportConflicts = vanillaToModifiedDelta.ExportDeltas.Keys.Intersect(vanillaToTargetDelta.ExportDeltas.Keys).ToList();

            //Name deltas

            if (nameConflicts.Count > 0)
            {
                //need to check if the conflicts result in same value, in this case it would not be a conflict.
                foreach (int nameIndex in nameConflicts)
                {
                    var modifiedName = modifiedVanillaPackage.Names[nameIndex];
                    var targetName = targetPackage.Names[nameIndex];
                    if (modifiedName != targetName)
                    {
                        //Differing names in same spots.
                        Log.Information($@"Cannot merge files: Name index {nameIndex} is different between modified and target.");
                        return false;
                    }
                }
            }

            if (importConflicts.Count > 0)
            {
                //todo
            }

            if (exportConflicts.Count > 0)
            {
                //hmm... this will be a tough one.
                foreach (int exportTableIndex in exportConflicts)
                {
                    //we will have to check sizes if we ever hope to have way to merge this
                    var modifiedData = modifiedVanillaPackage.Exports[exportTableIndex].Data;
                    var targetData = targetPackage.Exports[exportTableIndex].Data;

                    if (!modifiedData.SequenceEqual(targetData))
                    {
                        Log.Information($@"Cannot merge files: Export table index {exportTableIndex} data is different between modified and target.");
                        return false;
                    }

                    //We will have to ignore size here somehow...
                    //var modifiedHeader = modifiedVanillaPackage.Exports[exportTableIndex].Header;
                    //var targetHeader = targetPackage.Exports[exportTableIndex].Header;
                }
            }

            //Merge is OK to perform
            //Apply vanilla to modified delta to target package
            foreach (var nameDelta in vanillaToModifiedDelta.NameDeltas)
            {
                if (nameDelta.Key >= targetPackage.NameCount)
                {
                    //add it
                    targetPackage.addName(nameDelta.Value);
                }
                else
                {
                    targetPackage.replaceName(nameDelta.Key, nameDelta.Value);
                }
            }

            foreach (var exportDelta in vanillaToModifiedDelta.ExportDeltas)
            {
                if (exportDelta.Key >= targetPackage.ExportCount)
                {
                    //add it
                    targetPackage.addExport(exportDelta.Value); //not sure if this is possible
                }
                else
                {
                    //gonna need this reviewed, not entirely sure this is OK to do
                    targetPackage.Exports[exportDelta.Key].Data = exportDelta.Value.Data;
                    targetPackage.Exports[exportDelta.Key].Header = exportDelta.Value.Header;
                }
            }

            foreach (var importDelta in vanillaToModifiedDelta.ImportDeltas)
            {
                if (importDelta.Key >= targetPackage.ImportCount)
                {
                    //add it
                    targetPackage.addImport(importDelta.Value); //not sure if this is possible
                }
                else
                {
                    //gonna need this reviewed, not entirely sure this is OK to do
                    //targetPackage.Imports[importDelta.Key].Data = importDelta.Value.Data;
                    targetPackage.Imports[importDelta.Key].Header = importDelta.Value.Header;
                }
            }


            return true;
        }

        private class PackageDelta
        {
            public Dictionary<int, string> NameDeltas = new Dictionary<int, string>();
            public Dictionary<int, ImportEntry> ImportDeltas = new Dictionary<int, ImportEntry>();
            public Dictionary<int, ExportEntry> ExportDeltas = new Dictionary<int, ExportEntry>(); //includes header and data

            /// <summary>
            /// Compares two packages - vanilla vs modified - and returns the result. This MUST be a vanilla file as vanilla constraints are assumed.
            /// </summary>
            /// <param name="ancestorPackage">VANILLA FILE</param>
            /// <param name="modifiedPackage"></param>
            /// <returns></returns>
            public static PackageDelta CalculateDelta(IMEPackage ancestorPackage, IMEPackage modifiedPackage)
            {
                PackageDelta delta = new PackageDelta();
                #region Exports Comparison
                {
                    int numExportsToEnumerate = ancestorPackage.NameCount;

                    for (int i = 0; i < numExportsToEnumerate; i++)
                    {
                        ExportEntry exp1 = ancestorPackage.Exports[i];
                        ExportEntry exp2 = modifiedPackage.Exports[i];

                        //make data offset and data size the same, as the exports could be the same even if it was appended later.
                        //The datasize being different is a data difference not a true header difference so we won't list it here.
                        byte[] header1 = exp1.Header.TypedClone();
                        byte[] header2 = exp2.Header.TypedClone();
                        Buffer.BlockCopy(BitConverter.GetBytes((long)0), 0, header1, 32, sizeof(long)); //zero out offset
                        Buffer.BlockCopy(BitConverter.GetBytes((long)0), 0, header2, 32, sizeof(long)); //zero out offset

                        //if (!StructuralComparisons.StructuralEqualityComparer.Equals(header1, header2))
                        if (!header1.SequenceEqual(header2))
                        {
                            delta.ExportDeltas[i] = modifiedPackage.Exports[i];
                            continue;
                        }

                        if (!exp1.Data.SequenceEqual(exp2.Data))
                        {
                            delta.ExportDeltas[i] = modifiedPackage.Exports[i];
                        }
                    }

                    if (modifiedPackage.ExportCount > ancestorPackage.ExportCount)
                    {
                        for (int i = ancestorPackage.ExportCount; i < modifiedPackage.ExportCount; i++)
                        {
                            //added exports.
                            delta.ExportDeltas[i] = modifiedPackage.Exports[i];
                        }
                    }
                }
                #endregion

                #region Imports
                {
                    int numImportsToEnumerate = ancestorPackage.ImportCount;

                    for (int i = 0; i < numImportsToEnumerate; i++)
                    {
                        ImportEntry imp1 = ancestorPackage.Imports[i];
                        ImportEntry imp2 = modifiedPackage.Imports[i];
                        if (!imp1.Header.SequenceEqual(imp2.Header))
                        {
                            delta.ImportDeltas[i] = imp2; //0-based index
                        }
                    }

                    if (modifiedPackage.ImportCount > ancestorPackage.ImportCount)
                    {
                        //added imports
                        for (int i = ancestorPackage.ImportCount; i < modifiedPackage.ImportCount; i++)
                        {
                            delta.ImportDeltas[i] = modifiedPackage.Imports[i]; //0-based index
                        }
                    }

                }
                #endregion

                #region Names
                {
                    //you cannot delete names in packages. 
                    //as such target will always have at least same amount of names as vanilla.
                    int numNamesToEnumerate = ancestorPackage.NameCount;
                    for (int i = 0; i < numNamesToEnumerate; i++)
                    {
                        var name1 = ancestorPackage.Names[i];
                        var name2 = modifiedPackage.Names[i];

                        if (!name1.Equals(name2, StringComparison.InvariantCultureIgnoreCase))
                        {
                            delta.NameDeltas[i] = name2; // i => name 2
                        }
                    }

                    if (modifiedPackage.NameCount > ancestorPackage.NameCount)
                    {
                        for (int i = ancestorPackage.NameCount; i < modifiedPackage.NameCount; i++)
                        {
                            //added names.
                            delta.NameDeltas[i] = modifiedPackage.Names[i];
                        }
                    }
                }
                #endregion
                return delta;
            }
        }
    }
}
