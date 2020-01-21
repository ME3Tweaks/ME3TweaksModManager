using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Input;

namespace MassEffectModManagerCore.modmanager.gameini
{
    [Localizable(false)]
    public class DuplicatingIni
    {
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
                if (trimmed.Length == 0) continue; //blank line
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
                //if (isFirst)
                //{
                //    isFirst = false;
                //}
                //else
                //{
                //    sb.AppendLine(); //add a newline between headers.
                //}
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
