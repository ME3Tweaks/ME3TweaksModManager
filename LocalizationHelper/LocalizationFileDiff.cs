using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;

namespace LocalizationHelper
{
    public static class LocalizationFileDiff
    {
        public static string generateDiff(string oldfile, string newfile)
        {
            StringBuilder sb = new StringBuilder();
            XNamespace xnamespace = "clr-namespace:System;assembly=System.Runtime";
            XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";

            XDocument olddoc = XDocument.Parse(File.ReadAllText(oldfile));
            foreach (var v in olddoc.Descendants(xnamespace + "String").ToList())
            {
                Debug.WriteLine(v.Attribute(x + "Key").Value);
            }
            var oldStrings = olddoc.Descendants(xnamespace + "String").ToDictionary(x => x.Attributes().First(y => y.Name.LocalName == "Key").Value, x => x.Value);

            XDocument newdoc = XDocument.Parse(File.ReadAllText(newfile));
            var newStrings = newdoc.Descendants(xnamespace + "String").ToDictionary(x => x.Attributes().First(y => y.Name.LocalName == "Key").Value, x => x.Value);

            foreach (var v in newStrings)
            {
                if (oldStrings.TryGetValue(v.Key, out var oldstr))
                {
                    if (oldstr != v.Value)
                    {
                        sb.AppendLine("String changed: " + v.Key);
                    }
                }
                else
                {
                    sb.AppendLine("New string: " + v.Key);
                }
            }

            return sb.ToString();
        }
    }
}
