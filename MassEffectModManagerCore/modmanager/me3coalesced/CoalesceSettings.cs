using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace MassEffect3.Coalesce
{
    [Localizable(false)]
	public class CoalesceSettings
	{
		public CoalesceSettings(IEnumerable<int> compileTypes = null, int overrideCompileValueTypes = -1)
		{
			CompileTypes = compileTypes ?? new []{0, 1, 2, 3, 4};
			OverrideCompileValueTypes = overrideCompileValueTypes;
		}

		public IEnumerable<int> CompileTypes { get; set; }

		public int OverrideCompileValueTypes { get; set; }

		public static CoalesceSettings FromDictionary(Dictionary<string, string> settings)
		{
			if (settings == null)
			{
				throw new ArgumentNullException(nameof(settings));
			}

			var result = new CoalesceSettings();

			result.Parse(settings);

			return result;
		}

		public static CoalesceSettings FromXml(IEnumerable<XElement> settings)
		{
			if (settings == null)
			{
				throw new ArgumentNullException(nameof(settings));
			}

			var result = new CoalesceSettings();

			result.Parse(settings);

			return result;
		}

		public void Parse(Dictionary<string, string> settings)
		{
			if (settings == null)
			{
				throw new ArgumentNullException(nameof(settings));
			}

			foreach (var setting in settings)
			{
				SetValue(setting.Key, setting.Value);
			}
		}

		public void Parse(IEnumerable<XElement> settings)
		{
			if (settings == null)
			{
				throw new ArgumentNullException(nameof(settings));
			}

			foreach (var setting in settings)
			{
				if (!setting.Name.LocalName.Equals("Setting"))
				{
					continue;
				}

				if (!setting.HasAttributes)
				{
					continue;
				}

				var name = (string)setting.Attribute("name");

				if (string.IsNullOrEmpty(name))
				{
					continue;
				}

				var value = (string)setting.Attribute("value") ?? setting.Value;
					
				SetValue(name, value);
			}
		}

		public void SetValue(string name, object value)
		{
			if (string.IsNullOrEmpty(name))
			{
				throw new ArgumentNullException(nameof(name));
			}

			if (value == null)
			{
				throw new ArgumentNullException(nameof(value));
			}

			switch (name)
			{
				case "CompileTypes":
				{
					var str = (value as string);

					if (str == null)
					{
						return;
					}

					str = Regex.Replace(str, @"\s+", "");

					//CompileTypes = str.Split(',').Select(n => Convert.ToInt32(n)).ToArray();
					CompileTypes = str.Split(new []{ ',' }, StringSplitOptions.RemoveEmptyEntries).Select(n => Convert.ToInt32(n)).ToArray();

					break;
				}
				case "OverrideCompileValueTypes":
				{
					OverrideCompileValueTypes = Convert.ToInt32(value);

					break;
				}
			}
		}

		public bool TrySetValue(string name, object value)
		{
			if (string.IsNullOrEmpty(name))
			{
				throw new ArgumentNullException(nameof(name));
			}

			if (value == null)
			{
				throw new ArgumentNullException(nameof(value));
			}

			switch (name)
			{
				case "CompileTypes":
				{
					var str = (value as string);

					if (str == null)
					{
						return false;
					}

					str = Regex.Replace(str, @"\s+", "");

					//CompileTypes = str.Split(',').Select(n => Convert.ToInt32(n)).ToArray(); 
					CompileTypes = str.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Select(n => Convert.ToInt32(n)).ToArray(); 
					
					break;
				}
				case "OverrideCompileValueTypes":
				{
					OverrideCompileValueTypes = Convert.ToInt32(value);

					break;
				}
				default:
				{
					return false;
				}
			}

			return true;
		}
	}
}
