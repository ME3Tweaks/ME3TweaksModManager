using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

namespace MassEffectModManagerCore.modmanager.gameini
{
    [Localizable(false)]
    public class DuplicatingIni
    {
        public Section this[string sectionName]
        {
            get
            {
                var existingSection = Sections.FirstOrDefault(x => x.Header == sectionName);
                if (existingSection != null) return existingSection;
                var ns = new Section()
                {
                    Header = sectionName
                };
                Sections.Add(ns);
                return ns;
            }
            set
            {
                var sectionToReplace = Sections.FirstOrDefault(x => x.Header == sectionName);
                if (sectionToReplace != null)
                {
                    Sections.Remove(sectionToReplace);
                }
                Sections.Add(value);
            }
        }

        public List<Section> Sections = new List<Section>();

        public IniEntry GetValue(string sectionname, string key)
        {
            var section = GetSection(sectionname);
            return section?.GetValue(key);
        }

        public Section GetSection(string sectionname)
        {
            return Sections.FirstOrDefault(x => x.Header.Equals(sectionname, StringComparison.InvariantCultureIgnoreCase));
        }

        public Section GetSection(Section section)
        {
            return Sections.FirstOrDefault(x => x.Header.Equals(section.Header, StringComparison.InvariantCultureIgnoreCase));
        }

        /// <summary>
        /// Loads an ini file from disk
        /// </summary>
        /// <param name="iniFile"></param>
        /// <returns></returns>
        public static DuplicatingIni LoadIni(string iniFile)
        {
            return ParseIni(File.ReadAllText(iniFile));
        }

        public static DuplicatingIni ParseIni(string iniText)
        {
            DuplicatingIni di = new DuplicatingIni();
            var splits = iniText.Split('\n');
            Section currentSection = null;
            foreach (var line in splits)
            {
                string trimmed = line.Trim();
                if (string.IsNullOrWhiteSpace(trimmed)) continue; //blank line
                if (trimmed.StartsWith("[") && trimmed.EndsWith("]"))
                {
                    //New section
                    currentSection = new Section()
                    {
                        Header = trimmed.Trim('[', ']')
                    };
                    di.Sections.Add(currentSection);
                }
                else if (currentSection == null)
                {
                    continue; //this parser only supports section items
                }
                else
                {
                    currentSection.Entries.Add(new IniEntry(trimmed));
                }
            }
            return di;
        }


        /// <summary>
        /// Converts this DuplicatingIni object into an ini file as a string.
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            //bool isFirst = true;
            foreach (var section in Sections)
            {
                if (!section.Entries.Any())
                {
                    continue; //Do not write out empty sections.
                }
                sb.Append($"[{section.Header}]");
                sb.Append("\n"); //AppendLine does \r\n which we don't want.
                foreach (var line in section.Entries)
                {
                    if (line.HasValue)
                    {
                        sb.Append($"{line.Key}={line.Value}");
                        sb.Append("\n"); //AppendLine does \r\n which we don't want.
                    }
                    else
                    {
                        sb.Append(line.RawText);
                        sb.Append("\n"); //AppendLine does \r\n which we don't want.
                    }
                }
            }

            return sb.ToString();
        }

        [DebuggerDisplay("Ini Section [{Header}] with {Entries.Count} entries")]
        public class Section
        {
            public string Header;
            public List<IniEntry> Entries = new List<IniEntry>();

            public IniEntry GetValue(string key)
            {
                return Entries.FirstOrDefault(x => x.Key.Equals(key, StringComparison.InvariantCultureIgnoreCase));
            }

            public IniEntry this[string keyname]
            {
                get
                {
                    var firstExistingEntry = Entries.FirstOrDefault(x => x.Key == keyname);
                    if (firstExistingEntry != null) return firstExistingEntry;

                    var ne = new IniEntry(keyname, "");
                    Entries.Add(ne);
                    return ne;
                }
                set
                {
                    var keyToReplace = Entries.FirstOrDefault(x => x.Key == keyname);
                    if (keyToReplace != null)
                    {
                        Entries.Remove(keyToReplace);
                    }
                    Entries.Add(value);
                }
            }

            public void SetSingleEntry(string key, string value)
            {
                Entries.RemoveAll(x => x.Key == key);
                Entries.Add(new IniEntry(key, value));
            }

            public void SetSingleEntry(string key, int value)
            {
                Entries.RemoveAll(x => x.Key == key);
                Entries.Add(new IniEntry(key, value.ToString()));
            }
        }

        [DebuggerDisplay("IniEntry {Key} = {Value}")]

        public class IniEntry
        {
            public string RawText;

            public bool HasValue => Key != null && Value != null;

            public IniEntry(string line)
            {
                RawText = line;
                Key = KeyPair.Key;
                Value = KeyPair.Value;
            }
            public IniEntry(string key, string value)
            {
                RawText = $"{key}={value}";
                Key = KeyPair.Key;
                Value = KeyPair.Value;
            }

            public string Key { get; set; }

            public string Value { get; set; }

            public KeyValuePair<string, string> KeyPair
            {
                get
                {
                    var separator = RawText.IndexOf('=');
                    if (separator > 0)
                    {
                        string key = RawText.Substring(0, separator).Trim();
                        string value = RawText.Substring(separator + 1).Trim();
                        return new KeyValuePair<string, string>(key, value);
                    }
                    return new KeyValuePair<string, string>(null, null);
                }
            }
        }
    }
}
