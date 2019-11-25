using MassEffectModManagerCore.modmanager.objects;
using Serilog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using static MassEffectModManagerCore.modmanager.gameini.DuplicatingIni;

namespace MassEffectModManagerCore.modmanager.gameini
{

    public class RCWMod
    {
        public static List<RCWMod> LoadRCWMods(string fname)
        {
            return ParseRCWMods(Path.GetFileNameWithoutExtension(fname), File.ReadAllText(fname));
        }

        public static List<RCWMod> ParseRCWMods(string fname, string me2mod)
        {
            var mods = new List<RCWMod>();
            var lines = Regex.Split(me2mod, "\r\n|\r|\n");
            var author = lines.FirstOrDefault(x => x.StartsWith("###Author:"));
            if (author == null)
            {
                Log.Error("RCW mod doesn't list author, returning empty list.");
                return new List<RCWMod>();
            }
            author = author.Substring("###Author:".Length);

            RCWMod m = null;
            for (int i = 0; i < lines.Count(); i++)
            {
                var line = lines[i];
                if (line.StartsWith("###MOD:"))
                {
                    if (m != null)
                    {
                        mods.Add(m);
                    }
                    m = new RCWMod(author, fname + " " + line.Substring("###MOD:".Length), lines, ref i);
                }
            }

            if (m != null)
            {
                mods.Add(m);
            }

            return mods;
        }
        public RCWMod(string author, string modname, string[] lines, ref int linepos)
        {
            Author = author;
            ModName = modname;
            Log.Information("Parsing RCW mod: " + ModName);
            CoalescedFile currentFile = null;
            while (linepos + 1 < lines.Count())
            {
                linepos++;
                var line = lines[linepos];
                if (line.StartsWith("###MOD:"))
                {
                    linepos--;
                    break;
                }
                if (line.StartsWith("###FILE:"))
                {
                    if (currentFile != null)
                    {
                        Files.Add(currentFile);
                    }
                    currentFile = new CoalescedFile(line.Substring("###FILE:".Length), lines, ref linepos);
                }

            }
            if (currentFile != null)
            {
                Files.Add(currentFile);
            }
        }

        public string ModName;
        public string Author;
        public List<CoalescedFile> Files = new List<CoalescedFile>();

        //public Mod ConvertAndImportIntoM3()
        //{

        //}

        public void ApplyToTarget(GameTarget target)
        {
            if (target.Game != Mod.MEGame.ME2)
            {
                throw new Exception("Cannot apply RCW mod to game target that is not ME2.");
            }
            var coalescedFile = Path.Combine(target.TargetPath, "BioGame", "Config", "PC", "Cooked", "Coalesced.ini");
            if (File.Exists(coalescedFile))
            {
                ME2Coalesced me2c = new ME2Coalesced(coalescedFile);
                foreach (var file in Files)
                {
                    if (me2c.Inis.TryGetValue(file.FileName, out var ini))
                    {
                        foreach (var section in file.Sections)
                        {
                            var targetIniSection = ini.Sections.FirstOrDefault(x => x.Header == section.SectionName);
                            if (targetIniSection != null)
                            {
                                for (int i = targetIniSection.Entries.Count; i > 0; i--)
                                {
                                    var entry = targetIniSection.Entries[i];

                                    var key = entry.Key;
                                }
                            }
                            else
                            {
                                Log.Error("Ini section to apply to not present in file: " + file.FileName + " " + section.SectionName);
                            }
                        }
                    }
                    else
                    {
                        Log.Error("Ini to apply to not present in coalesced: " + file.FileName);
                    }
                }
            }
            else
            {
                Log.Error("Game coalesced file was not found! Should be at " + coalescedFile);
            }
        }


        public class CoalescedFile
        {
            public string FileName;
            public List<Section> Sections = new List<Section>();
            public CoalescedFile(string filename, string[] lines, ref int linepos)
            {
                FileName = filename;
                while (linepos + 1 < lines.Count())
                {
                    linepos++;
                    var line = lines[linepos];
                    Section currentSection = null;
                    if (line.StartsWith("###"))
                    {
                        linepos--;
                        break;
                    }

                    if (line.StartsWith("[") && line.EndsWith("]"))
                    {
                        //section
                        if (currentSection != null)
                        {
                            Sections.Add(currentSection);
                        }
                        currentSection = new Section(line.Trim('[', ']'), lines, ref linepos);
                    }
                }
            }

            internal void WriteToStringBuilder(StringBuilder sb)
            {
                sb.AppendLine("###FILE:" + FileName);
                foreach(var section in Sections)
                {
                    section.WriteToStringBuilder(sb);
                }
            }
        }

        public class Section
        {
            public string SectionName;
            public List<IniEntry> KeysToDelete = new List<IniEntry>();
            public List<IniEntry> KeysToAdd = new List<IniEntry>();
            public Section(string sectionname, string[] lines, ref int linepos)
            {
                SectionName = sectionname;
                while (linepos + 1 < lines.Count())
                {
                    linepos++;
                    var line = lines[linepos];
                    if (line.StartsWith("###"))
                    {
                        linepos--;
                        break; //end of a anything
                    }
                    if (line.StartsWith("[") && line.EndsWith("]"))
                    {
                        linepos--;
                        break; //end of section
                    }
                    List<IniEntry> listToAddTo = null;
                    if (line.StartsWith("+"))
                    {
                        //add
                        listToAddTo = KeysToAdd;
                    }
                    if (line.StartsWith("-"))
                    {
                        //subtract
                        listToAddTo = KeysToDelete;
                    }
                    if (listToAddTo != null)
                    {
                        line = line.Substring(1); //slice off +/-
                        listToAddTo.Add(new IniEntry(line));
                    }
                }
            }

            internal void WriteToStringBuilder(StringBuilder sb)
            {
                sb.AppendLine($"[{SectionName}]");
                foreach(var value in KeysToDelete)
                {
                    sb.AppendLine($"-{value.RawText}");
                }
                foreach (var value in KeysToAdd)
                {
                    sb.AppendLine($"+{value.RawText}");
                }
            }
        }

        public void WriteToFile(string path)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("###Author:" + Author);
            sb.AppendLine("###MOD:" + ModName);
            foreach(var file in Files)
            {
                file.WriteToStringBuilder(sb);
            }
            File.WriteAllText(path, sb.ToString());
        }
    }
}