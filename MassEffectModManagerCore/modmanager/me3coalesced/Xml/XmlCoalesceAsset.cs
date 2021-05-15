using MassEffectModManagerCore.modmanager.helpers;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using LegendaryExplorerCore.Gammtek.Extensions.Collections.Generic;

namespace MassEffect3.Coalesce.Xml
{
    [Localizable(false)]
	public class XmlCoalesceAsset : CoalesceAsset
	{
		//private static readonly Regex WhitespacePattern = new Regex("[\r\n\t]+", RegexOptions.Compiled);
		//private static readonly Regex WhitespacePattern = new Regex(@"\s+", RegexOptions.Compiled);
		private static readonly Regex SpecialCharactersPattern = new Regex(@"[\r\n\t]+", RegexOptions.Compiled);

		public XmlCoalesceAsset(string name = "", CoalesceSections sections = default, IList<CoalesceInclude> includes = null)
			: base(name, sections)
		{
			Includes = includes ?? new List<CoalesceInclude>();
		}

		public string BaseUri { get; protected set; }

		public string LogicalSourcePath { get; protected set; }

		public string SourceDirectory { get; protected set; }

		public string SourcePath { get; protected set; }

		public IList<CoalesceInclude> Includes { get; set; }

        public static XmlCoalesceAsset LoadFromMemory(string text)
        {
            var sourcePath = "virtualized";
            var doc = XDocument.Parse(text);

            var root = doc.Root;
            var id = (string)root.Attribute("id");
            var name = (string)root.Attribute("name");
            var source = (string)root.Attribute("source");

            var result = new XmlCoalesceAsset(name)
            {
                //BaseUri = (doc.BaseUri != "") ? doc.BaseUri : new Uri(sourcePath).AbsoluteUri,
                Id = id,
                Source = source,
                SourcePath = sourcePath
                //SourceDirectory = Path.GetDirectoryName(sourcePath)
            };

            // Read includes before the sections
            //result.ReadIncludes(root);
            result.ReadSections(root);

            return result;
        }
		public static XmlCoalesceAsset Load(string path)
		{
			if (string.IsNullOrEmpty(path))
			{
				throw new ArgumentNullException(nameof(path));
			}

			var sourcePath = Path.GetFullPath(path);

			if (!File.Exists(sourcePath))
			{
				Console.WriteLine(@"Warning: {0} not found!", path);
				return null;
			}

			var doc = XDocument.Load(path);

			var root = doc.Root;

			if (root == null)
			{
				return null;
			}

			var id = (string) root.Attribute("id");
			var name = (string) root.Attribute("name");
			var source = (string) root.Attribute("source");

			var result = new XmlCoalesceAsset(name)
			{
				BaseUri = (doc.BaseUri != "") ? doc.BaseUri : new Uri(sourcePath).AbsoluteUri,
				Id = id,
				Source = source,
				SourcePath = sourcePath,
				SourceDirectory = Path.GetDirectoryName(sourcePath)
			};

			// Read includes before the sections
			result.ReadIncludes(root);
			result.ReadSections(root);

			return result;
		}

		public void ReadIncludes(XElement root)
		{
			if (root == null)
			{
				throw new ArgumentNullException(nameof(root));
			}

			var includesElement = root.Element("Includes");

			if (includesElement == null)
			{
				return;
			}

			var includes = from el in includesElement.Elements("Include") select el;

			foreach (var include in includes)
			{
				var source = (string) include.Attribute("source");

				if (string.IsNullOrEmpty(source))
				{
					continue;
				}

				var sourcePath = Path.Combine(SourceDirectory, source);
				sourcePath = Path.GetFullPath(sourcePath);

				Includes.Add(new CoalesceInclude(sourcePath));
			}

			foreach (var include in Includes)
			{
				var asset = Load(include.Source);

				if (asset == null)
				{
					continue;
				}

				Combine(asset);
			}
		}

		public CoalesceProperty ReadProperty(XElement propertyElement)
		{
			if (propertyElement == null)
			{
				throw new ArgumentNullException(nameof(propertyElement));
			}

			var propertyName = (string) propertyElement.Attribute("name");
			//var optionsString = ((string) propertyElement.Attribute("options")) ?? "";

			// Trim spaces from options
			//optionsString = Regex.Replace(optionsString, @"\s+", "");
			//var options = optionsString.Split(new []{ '|' }, StringSplitOptions.RemoveEmptyEntries);

			// Old, remove eventually
			var ignoreProperty = (bool?) propertyElement.Attribute("ignore") ?? false;
			//var allowDuplicates = (bool?) propertyElement.Attribute("allowDuplicates") ?? false;

			if (ignoreProperty || string.IsNullOrEmpty(propertyName))
			{
				return null;
			}

			var property = new CoalesceProperty(propertyName);

			if (propertyElement.HasElements)
			{
				var valueElements = from el in propertyElement.Elements("Value") select el;

				// Loop through the current properties values
				foreach (var valueElement in valueElements)
				{
					var value = valueElement.Value;
					var type = (int?) valueElement.Attribute("type");
					//var ignoreValue = (bool?) valueElement.Attribute("ignore") ?? false;

					if (!string.IsNullOrEmpty(value))
					{
						value = SpecialCharactersPattern.Replace(value.Trim(), "");
					}

					property.Add(new CoalesceValue(value, type));

					/*switch (type)
					{
						case -1:
						{
							property.Clear();
							property.Add(new CoalesceValue(value, type));

							break;
						}
						case 0:
						{
							property.Clear();
							property.Add(new CoalesceValue(value, type));

							break;
						}
						case 1:
						{
							property.Clear();
							property.Add(new CoalesceValue(value, type));

							break;
						}
						case 2:
						{
							property.Add(new CoalesceValue(value, type));

							break;
						}
						case 3:
						{
							if (!property.Any(v => v.Equals(value) && v.ValueType != 4))
							{
								property.Add(new CoalesceValue(value, type));
							}

							break;
						}
						case 4:
						{
							property.RemoveAll(v => v.Equals(value));

							property.Add(new CoalesceValue(value, type));

							break;
						}
					}*/
				}
			}
			else
			{
				var value = propertyElement.Value;
				var type = (int?) propertyElement.Attribute("type");

				if (!string.IsNullOrEmpty(value))
				{
					value = SpecialCharactersPattern.Replace(value.Trim(), "");
				}

				property.Add(new CoalesceValue(value, type));
			}

			return property;
		}

		public void ReadSections(XElement root)
		{
			if (root == null)
			{
				throw new ArgumentNullException(nameof(root));
			}

			var sectionsElement = root.Element("Sections");

			if (sectionsElement == null)
			{
				return;
			}

			var sectionElements = from el in sectionsElement.Elements("Section") select el;

			// Loop through the sections
			foreach (var sectionElement in sectionElements)
			{
                var currentSection = new CoalesceSection();
                var sectionName = (string) sectionElement.Attribute("name");
				
				// Make sure the section has a name
				if (string.IsNullOrEmpty(sectionName))
				{
					continue;
				}

				var propertyElements = from el in sectionElement.Elements("Property") select el;
				
				// Loop through the current sections properties
				foreach (var propertyElement in propertyElements)
				{
					var currentProperty = ReadProperty(propertyElement);

				    if (currentProperty == null)
					{
						continue;
					}

					if (!currentSection.TryGetValue(currentProperty.Name, out CoalesceProperty property))
					{
						property = new CoalesceProperty();
						currentSection.Add(currentProperty.Name, property);
					}

					property.AddRange(currentProperty);
					//currentSection[currentProperty.Name] = (CoalesceProperty) property.Concat(currentProperty);
					//properties.Add(property);

					// If the current section dictionary does not contain the property, add it
					// Else replace the property with this one
					/*if (!coalescedSection.ContainsKey(property.Name))
					{
						coalescedSection.Add(property.Name, property);
					}
					else
					{
						//coalescedSection[property.Name] = property;
						coalescedSection[property.Name].Combine(property);
					}*/
				}

				if (!Sections.TryGetValue(sectionName, out CoalesceSection section))
				{
					section = new CoalesceSection();
					Sections.Add(sectionName, section);
				}

				section.Combine(currentSection);

				// If the current asset dictionary does not contain the current section, add it
				// Else replace it with this one
				/*if (!Sections.ContainsKey(sectionName))
				{
					Sections.Add(sectionName, currentSection);
				}
				else
				{
					Sections[sectionName].Combine(currentSection);
				}*/
			}
		}

	}
}