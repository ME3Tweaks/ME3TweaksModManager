using ME3Explorer;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Xml.Linq;
using Serilog;

namespace MassEffectModManagerCore.modmanager
{
    /// <summary>
    /// Compiles and decompiles TLK to the tankmaster format (this program does not include the original code for Tankmaster TLK tool)
    /// </summary>
    public class TLKTranspiler
    {
        public static void CompileTLKManifest(string manifestFile, XElement rootElement)
        {
            //Thread.Sleep(5000);
            var includes = rootElement.Descendants(@"Include");
            var tlkname = rootElement.Attribute(@"source").Value;
            var rootDir = Directory.GetParent(manifestFile);
            List<HuffmanCompressionME2ME3.TLKEntry> strings = new List<HuffmanCompressionME2ME3.TLKEntry>();

            foreach (var i in includes)
            {
                var sourcefile = i.Attribute(@"source").Value;
                var subxmlfile = Path.Combine(rootDir.FullName, sourcefile);
                XDocument subdoc = XDocument.Load(subxmlfile);
                var substrings = subdoc.Root.Descendants(@"String");
                var position = 0;
                foreach (var substr in substrings)
                {
                    var id = int.Parse(substr.Attribute(@"id").Value);
                    var data = substr.Value;
                    if (id > 0) data += '\0';
                    strings.Add(new HuffmanCompressionME2ME3.TLKEntry(id, position, data));
                    position++;
                }
            }

            var tlk = Path.Combine(rootDir.FullName, tlkname);
            Log.Information(@"Saving TLK file: " + tlk);
            new HuffmanCompressionME2ME3().SaveToTlkFile(tlk, strings);
            Log.Information(@"Saved TLK file: " + tlk);
        }
    }
}
