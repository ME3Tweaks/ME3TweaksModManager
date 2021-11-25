using System;
using System.Collections.Generic;
using System.Xml.Linq;
using System.Xml.XPath;
using LegendaryExplorerCore.Helpers;

namespace MassEffectModManagerCore.modmanager.gameini
{
    public class Game3CoalescedValueEntry
    {
        public string Section { get; init; }
        public string Name { get; init; }
        public int Type { get; init; } = -1;
        public List<string> Values { get; init; } = new List<string>(1);
        public string Value
        {
            get => Values.Any() ? Values[0] : null;
            set
            {
                Values.Clear();
                Values.Add(value);
            }
        }
    }

    // Does not work right now. Not sure how to handle array typings

    public class Game3CoalescedHelper
    {
        public static void AddTypeEntry(XDocument coalescedXml, Game3CoalescedValueEntry entry)
        {
            if (entry.Type > 4 || entry.Type < 0)
                throw new Exception(@"Type must be between 1 and 4");

            // We don't need startup package for game 3. But leaving this here for later
            //var coal = Path.Combine(cookedDir, $@"Default_{MERGE_DLC_FOLDERNAME}.bin");
            //using var fs = File.OpenRead(coal);
            //var coalFiles = CoalescedConverter.DecompileGame3ToMemory(fs);
            //fs.Close();

            //var bioEngine = XDocument.Parse(coalFiles[@"BioEngine.xml"]);
            ///*
            // Section name="engine.startuppackages">
            //  <Property name="dlcstartuppackage" type="3">Startup_HEN_PR</Property>
            //  <Property name="dlcstartuppackagename" type="0">Startup_HEN_PR</Property>
            //  <Property name="package" type="3">PlotManagerAutoDLC_HEN_PR</Property>
            //</Section>
            // */
            var targetSection = coalescedXml.XPathSelectElement($@"/CoalesceAsset/Sections/Section[@name='{entry.Section}']");
            if (targetSection == null)
            {
                targetSection = new XElement(@"Section");
                targetSection.SetAttributeValue(@"name", entry.Section);
            }

            var prop = new XElement(@"Property");
            prop.SetAttributeValue(@"name", entry.Name);

            if (entry.Values.Count > 1)
            {
                foreach (var entryVal in entry.Values)
                {
                    var valueProp = new XElement(@"Value");
                    valueProp.SetAttributeValue(@"type", entry.Type.ToString());
                    prop.Add(valueProp);
                }
            }
            else
            {
                prop.SetAttributeValue(@"type", entry.Type.ToString());
                prop.Value = entry.Value;
            }

            targetSection.Add(prop);
        }

        public static Game3CoalescedValueEntry GetSingleEntry(XDocument coalescedXml, Game3CoalescedValueEntry entry)
        {
            var targetProp = coalescedXml.XPathSelectElement($@"/CoalesceAsset/Sections/Section[@name='{entry.Section}']/Property[@name='{entry.Name}']");
            if (targetProp == null) return null;
            int type = -1;
            int.TryParse(targetProp.Attributes(@"type").ToString(), out type);
            return new Game3CoalescedValueEntry()
            {
                Name = entry.Name,
                Section = entry.Section,
                Value = targetProp.Value,
                Type = type
            };
        }

        public static List<Game3CoalescedValueEntry> GetArrayEntry(XDocument coalescedXml, Game3CoalescedValueEntry entry)
        {
            var entries = new List<Game3CoalescedValueEntry>();

            var targetProp = coalescedXml.XPathSelectElement($@"/CoalesceAsset/Sections/Section[@name='{entry.Section}']/Property[@name='{entry.Name}']");
            if (targetProp == null) return null;

            if (targetProp.Attribute(@"type") != null)
            {
                // It's a one item list?
                int type = -1;
                int.TryParse(targetProp.Attribute(@"type").ToString(), out type);
                entries.Add(new Game3CoalescedValueEntry()
                {
                    Name = entry.Name,
                    Section = entry.Section,
                    Value = targetProp.Value,
                    Type = type
                });
            }
            else
            {
                var values = targetProp.Elements(@"Value");
                foreach (var val in values)
                {
                    int type = -1;
                    int.TryParse(val.Attribute(@"type").ToString(), out type);
                    entries.Add(new Game3CoalescedValueEntry()
                    {
                        Name = entry.Name,
                        Section = entry.Section,
                        Value = val.Value,
                        Type = type
                    });
                }
            }
            return entries;
        }

        public static void AddArrayEntry(XDocument coalescedXml, Game3CoalescedValueEntry entry)
        {
            if (entry.Type > 4 || entry.Type < 0)
                throw new Exception(@"Type must be between 1 and 4");

            // We don't need startup package for game 3. But leaving this here for later
            //var coal = Path.Combine(cookedDir, $@"Default_{MERGE_DLC_FOLDERNAME}.bin");
            //using var fs = File.OpenRead(coal);
            //var coalFiles = CoalescedConverter.DecompileGame3ToMemory(fs);
            //fs.Close();

            //var bioEngine = XDocument.Parse(coalFiles[@"BioEngine.xml"]);
            ///*
            // Section name="engine.startuppackages">
            //  <Property name="dlcstartuppackage" type="3">Startup_HEN_PR</Property>
            //  <Property name="dlcstartuppackagename" type="0">Startup_HEN_PR</Property>
            //  <Property name="package" type="3">PlotManagerAutoDLC_HEN_PR</Property>
            //</Section>
            // */

            var sections = coalescedXml.XPathSelectElement(@"/CoalesceAsset/Sections");
            var targetSection = sections.XPathSelectElement($@"/Section[@name='{entry.Section}']");
            if (targetSection == null)
            {
                targetSection = new XElement(@"Section");
                targetSection.SetAttributeValue(@"name", entry.Section);
                sections.Add(targetSection);
            }

            var prop = targetSection.XPathSelectElement($@"/Property[@name='{entry.Name}']");
            if (prop == null)
            {
                prop = new XElement(@"Property");
                prop.SetAttributeValue(@"name", entry.Name);
                targetSection.Add(prop);
            }

            foreach (var entryVal in entry.Values)
            {
                var valueProp = new XElement(@"Value", entryVal);
                valueProp.SetAttributeValue(@"type", entry.Type.ToString());
                prop.Add(valueProp);
            }
        }
    }
}
