using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace MassEffectModManagerCore.modmanager.gameini
{

    public class RCWMod
    {


        public static List<RCWMod> ParseRCWMods(string me2mod)
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
                    m = new RCWMod(author, line.Substring("###MOD:".Length), lines, ref i);
                }
            }

            if (m!= null)
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
            while (linepos < lines.Count())
            {
                var line = lines[linepos];
                if (line.StartsWith("###File:"))
                {
                    if (currentFile != null)
                    {
                        Files.Add(currentFile);
                    }
                    currentFile = new CoalescedFile();
                    currentFile.FileName = line.Substring("###File:".Length);
                }
                if (line.StartsWith("###MOD:"))
                {
                    break;
                }
                linepos++;
            }
            if (currentFile != null)
            {
                Files.Add(currentFile);
            }
        }

        public string ModName;
        public string Author;

        public List<CoalescedFile> Files = new List<CoalescedFile>();

        public class CoalescedFile
        {
            public string FileName;
            public List<Section> Sections = new List<Section>();
        }

        public class Section
        {
            public string SectionName;
            public List<KeyValuePair<string, string>> KeysToDelete = new List<KeyValuePair<string, string>>();
            public List<KeyValuePair<string, string>> KeysToAdd = new List<KeyValuePair<string, string>>();

        }
    }
}
