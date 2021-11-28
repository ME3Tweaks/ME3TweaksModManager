using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Linq;
using LegendaryExplorerCore.TLK;
using ME3TweaksModManager.modmanager.diagnostics;
using ME3TweaksModManager.modmanager.localizations;

namespace ME3TweaksModManager.modmanager
{
    /// <summary>
    /// Compiles and decompiles TLK to the tankmaster format (this program does not include the original code for Tankmaster TLK tool)
    /// </summary>
    public class TLKTranspiler
    {
        public static void CompileTLKManifest(string manifestFile, XElement rootElement, Action<string> exceptionCompilingCallback)
        {
            try
            {
                var includes = rootElement.Descendants(@"Include");
                var tlkname = rootElement.Attribute(@"source").Value;
                var rootDir = Directory.GetParent(manifestFile);
                List<TLKStringRef> strings = new List<TLKStringRef>();

                foreach (var i in includes)
                {

                    var sourcefile = i.Attribute(@"source").Value;
                    var subxmlfile = Path.Combine(rootDir.FullName, sourcefile);
                    try
                    {
                        XDocument subdoc = XDocument.Load(subxmlfile);
                        var substrings = subdoc.Root.Descendants(@"String");
                        var position = 0;
                        foreach (var substr in substrings)
                        {
                            var id = int.Parse(substr.Attribute(@"id").Value);
                            var data = substr.Value;
                            if (id > 0) data += '\0';
                            strings.Add(new TLKStringRef(id, position, data));
                            position++;
                        }
                    }
                    catch (Exception e)
                    {
                        M3Log.Error($@"Error compiling TLK submodule {subxmlfile}:");
                        M3Log.Error(App.FlattenException(e));
                        exceptionCompilingCallback?.Invoke(M3L.GetString(M3L.string_interp_exceptionOccuredWhileCompilingTLKSubfileTankmaster, sourcefile, e.Message));
                    }
                }

                var tlk = Path.Combine(rootDir.FullName, tlkname);
                M3Log.Information(@"Saving TLK file: " + tlk);
                LegendaryExplorerCore.TLK.ME2ME3.HuffmanCompression.SaveToTlkFile(tlk, strings);
                M3Log.Information(@"Saved TLK file: " + tlk);
            }
            catch (Exception e)
            {
                M3Log.Error(@"Error compiling TLK:");
                M3Log.Error(App.FlattenException(e));
                exceptionCompilingCallback?.Invoke(M3L.GetString(M3L.string_interp_exceptionOccuredWhileCompilingTLKFileME3Exp, e.Message));
            }
        }

        /// <summary>
        /// Compiles a ME3Explorer style TLK xml
        /// </summary>
        /// <param name="xmlfile"></param>
        /// <param name="rootElement"></param>

        public static void CompileTLKME3Explorer(string xmlfile, XElement rootElement, Action<string> exceptionCompilingCallback)
        {
            var rootDir = Directory.GetParent(xmlfile);
            List<TLKStringRef> strings = new List<TLKStringRef>();

            var substrings = rootElement.Descendants(@"String");
            var position = 0;
            foreach (var substr in substrings)
            {
                var id = int.Parse(substr.Attribute(@"id").Value);
                var data = substr.Value;
                if (id > 0) data += '\0';
                strings.Add(new TLKStringRef(id, position, data));
                position++;
            }

            var tlk = Path.Combine(rootDir.FullName, Path.GetFileNameWithoutExtension(xmlfile) + @".tlk");
            M3Log.Information(@"Saving TLK file: " + tlk);
            try
            {
                LegendaryExplorerCore.TLK.ME2ME3.HuffmanCompression.SaveToTlkFile(tlk, strings);
                M3Log.Information(@"Saved TLK file: " + tlk);
            }
            catch (Exception e)
            {
                M3Log.Error(@"Error compiling TLK:");
                M3Log.Error(App.FlattenException(e));
                exceptionCompilingCallback?.Invoke(M3L.GetString(M3L.string_interp_exceptionOccuredWhileCompilingTLKFileME3Exp, e.Message));
            }
        }

        internal static void CompileTLKManifestStrings(string filename, XElement rootElement, Action<string> exceptionCompilingCallback)
        {
            //Thread.Sleep(5000);
            var tlkname = Path.GetFileNameWithoutExtension(filename) + @".tlk";
            var rootDir = Directory.GetParent(filename);
            List<TLKStringRef> strings = new List<TLKStringRef>();

            var substrings = rootElement.Descendants(@"String");
            var position = 0;
            foreach (var substr in substrings)
            {
                var id = int.Parse(substr.Attribute(@"id").Value);
                var data = substr.Value;
                if (id > 0) data += '\0';
                strings.Add(new TLKStringRef(id, position, data));
                position++;
            }

            var tlk = Path.Combine(rootDir.FullName, tlkname);
            M3Log.Information(@"Saving TLK file: " + tlk);
            try
            {
                LegendaryExplorerCore.TLK.ME2ME3.HuffmanCompression.SaveToTlkFile(tlk, strings);
                M3Log.Information(@"Saved TLK file: " + tlk);
            }
            catch (Exception e)
            {
                M3Log.Error(@"Error compiling TLK:");
                M3Log.Error(App.FlattenException(e));
                exceptionCompilingCallback?.Invoke(M3L.GetString(M3L.string_interp_exceptionOccuredWhileCompilingTLKFileME3Exp, e.Message));
            }
        }
    }
}
