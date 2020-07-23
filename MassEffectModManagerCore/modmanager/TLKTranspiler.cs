using ME3Explorer;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Xml.Linq;
using MassEffectModManagerCore.gamefileformats;
using MassEffectModManagerCore.modmanager.localizations;
using Serilog;

namespace MassEffectModManagerCore.modmanager
{
    /// <summary>
    /// Compiles and decompiles TLK to the tankmaster format (this program does not include the original code for Tankmaster TLK tool)
    /// </summary>
    public class TLKTranspiler
    {
        public static void CompileTLKManifest(string manifestFile, XElement rootElement, Action<string> exceptionCompilingCallback)
        {
            //Thread.Sleep(5000);
            try
            {
                var includes = rootElement.Descendants(@"Include");
                var tlkname = rootElement.Attribute(@"source").Value;
                var rootDir = Directory.GetParent(manifestFile);
                List<TalkFileME1.TLKStringRef> strings = new List<TalkFileME1.TLKStringRef>();

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
                            strings.Add(new TalkFileME1.TLKStringRef(id, position, data));
                            position++;
                        }
                    }
                    catch (Exception e)
                    {
                        Log.Error($@"Error compiling TLK submodule {subxmlfile}:");
                        Log.Error(App.FlattenException(e));
                        exceptionCompilingCallback?.Invoke(M3L.GetString(M3L.string_interp_exceptionOccuredWhileCompilingTLKSubfileTankmaster, sourcefile, e.Message));
                    }
                }

                var tlk = Path.Combine(rootDir.FullName, tlkname);
                Log.Information(@"Saving TLK file: " + tlk);
                HuffmanCompressionME2ME3.SaveToTlkFile(tlk, strings);
                Log.Information(@"Saved TLK file: " + tlk);
            }
            catch (Exception e)
            {
                Log.Error(@"Error compiling TLK:");
                Log.Error(App.FlattenException(e));
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
            List<TalkFileME1.TLKStringRef> strings = new List<TalkFileME1.TLKStringRef>();

            var substrings = rootElement.Descendants(@"String");
            var position = 0;
            foreach (var substr in substrings)
            {
                var id = int.Parse(substr.Attribute(@"id").Value);
                var data = substr.Value;
                if (id > 0) data += '\0';
                strings.Add(new TalkFileME1.TLKStringRef(id, position, data));
                position++;
            }

            var tlk = Path.Combine(rootDir.FullName, Path.GetFileNameWithoutExtension(xmlfile) + @".tlk");
            Log.Information(@"Saving TLK file: " + tlk);
            try
            {
                HuffmanCompressionME2ME3.SaveToTlkFile(tlk, strings);
                Log.Information(@"Saved TLK file: " + tlk);
            }
            catch (Exception e)
            {
                Log.Error(@"Error compiling TLK:");
                Log.Error(App.FlattenException(e));
                exceptionCompilingCallback?.Invoke(M3L.GetString(M3L.string_interp_exceptionOccuredWhileCompilingTLKFileME3Exp, e.Message));
            }
        }

        internal static void CompileTLKManifestStrings(string filename, XElement rootElement, Action<string> exceptionCompilingCallback)
        {
            //Thread.Sleep(5000);
            var tlkname = Path.GetFileNameWithoutExtension(filename) + @".tlk";
            var rootDir = Directory.GetParent(filename);
            List<TalkFileME1.TLKStringRef> strings = new List<TalkFileME1.TLKStringRef>();

            var substrings = rootElement.Descendants(@"String");
            var position = 0;
            foreach (var substr in substrings)
            {
                var id = int.Parse(substr.Attribute(@"id").Value);
                var data = substr.Value;
                if (id > 0) data += '\0';
                strings.Add(new TalkFileME1.TLKStringRef(id, position, data));
                position++;
            }

            var tlk = Path.Combine(rootDir.FullName, tlkname);
            Log.Information(@"Saving TLK file: " + tlk);
            try
            {
                HuffmanCompressionME2ME3.SaveToTlkFile(tlk, strings);
                Log.Information(@"Saved TLK file: " + tlk);
            }
            catch (Exception e)
            {
                Log.Error(@"Error compiling TLK:");
                Log.Error(App.FlattenException(e));
                exceptionCompilingCallback?.Invoke(M3L.GetString(M3L.string_interp_exceptionOccuredWhileCompilingTLKFileME3Exp, e.Message));
            }
        }
    }
}
