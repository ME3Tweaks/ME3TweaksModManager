using MassEffectModManagerCore.modmanager.objects;
using Serilog;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using MassEffectModManagerCore.modmanager.helpers;
using static MassEffectModManagerCore.modmanager.gameini.DuplicatingIni;

namespace MassEffectModManagerCore.modmanager.gameini
{
    [Localizable(false)]
    [DebuggerDisplay("RCWMod {ModName} by {Author}, {Files.Count} files")]
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
            var author = lines.FirstOrDefault(x => x.StartsWith("###Author:", StringComparison.InvariantCultureIgnoreCase));
            if (author == null)
            {
                Log.Warning("RCW mod doesn't list author");
                author = "Unknown";
            }
            else
            {
                author = author.Substring("###Author:".Length).Trim();
            }

            var nummods = lines.Count(x => x.StartsWith("###MOD:", StringComparison.InvariantCultureIgnoreCase));
            if (nummods == 0)
            {
                //Check for file headers 
                var numfiles = lines.Count(x => x.StartsWith("###FILE:", StringComparison.InvariantCultureIgnoreCase));
                if (numfiles == 0)
                {
                    Log.Error("This does not appear to be RCW mod, as no ###MOD: or ###FILE: headers were found.");
                    return new List<RCWMod>();
                }
                nummods = 1;
            }
            RCWMod m = null;
            if (nummods > 1)
            {
                for (int i = 0; i < lines.Count(); i++)
                {
                    var line = lines[i];
                    if (line.StartsWith("###MOD:", StringComparison.InvariantCultureIgnoreCase))
                    {
                        if (m != null)
                        {
                            mods.Add(m);
                        }
                        m = new RCWMod(author, fname + " " + line.Substring("###MOD:".Length).Trim(), lines, ref i);
                    }
                }

                if (m != null)
                {
                    mods.Add(m);
                }
            }
            else
            {
                int pos = Array.FindIndex(lines, c => c.StartsWith("###MOD:")); //headers always pre-advance by 1. If mod is not found it'll return -1 which is OK.
                m = new RCWMod(author, fname, lines, ref pos);
                if (m.Files.Count > 0)
                {
                    mods.Add(m);
                }
                else
                {
                    Log.Error("This does not appear to be RCW mod, no files were found.");
                }
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
                if (line.StartsWith("###MOD:", StringComparison.InvariantCultureIgnoreCase))
                {
                    linepos--;
                    break;
                }
                if (line.StartsWith("###FILE:", StringComparison.InvariantCultureIgnoreCase))
                {
                    if (currentFile != null)
                    {
                        Files.Add(currentFile);
                    }
                    currentFile = new CoalescedFile(line.Substring("###FILE:".Length).Trim(), lines, ref linepos);
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


        [DebuggerDisplay("RCWMod CoalescedFile {FileName}, {Sections.Count} sections")]

        public class CoalescedFile
        {
            public string FileName;
            public List<Section> Sections = new List<Section>();
            public CoalescedFile(string filename, string[] lines, ref int linepos)
            {
                FileName = filename;
                Section currentSection = null;
                while (linepos + 1 < lines.Count())
                {
                    linepos++;
                    var line = lines[linepos];
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

                if (currentSection != null)
                {
                    Sections.Add(currentSection);
                }
            }

            internal void WriteToStringBuilder(StringBuilder sb)
            {
                sb.AppendLine("###FILE:" + FileName);
                foreach (var section in Sections)
                {
                    section.WriteToStringBuilder(sb);
                }
            }
        }

        [DebuggerDisplay("RCWMod Section [{SectionName}], {KeysToDelete.Count} keys to delete, {KeysToAdd.Count} keys to add")]

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
                foreach (var value in KeysToDelete)
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
            foreach (var file in Files)
            {
                file.WriteToStringBuilder(sb);
            }
            File.WriteAllText(path, sb.ToString());
        }
    }
}