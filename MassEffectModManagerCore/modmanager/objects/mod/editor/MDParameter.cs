using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using LegendaryExplorerCore.Misc;

namespace ME3TweaksModManager.modmanager.objects.mod.editor
{

    /// <summary>
    /// A single parameter mapping that is used for various items in moddesc.ini 
    /// </summary>
    [DebuggerDisplay("MDParameter {Key}={Value}")]
    public class MDParameter
    {
        public MDParameter()
        {

        }

        /// <summary>
        /// If the editor for this value accepts newlines or not. Used by moddesc descriptor
        /// </summary>
        public bool AcceptsNewLines { get; set; }

        public MDParameter(string type, string key, string value, IEnumerable<string> allowedValues = null, string unsetValue = null)
        {
            ValueType = type;
            Key = key;
            Value = value;
            if (allowedValues != null)
            {
                AllowedValues.ReplaceAll(allowedValues);
                UsesSetValuesList = true;
                UnsetValueItem = unsetValue ?? throw new Exception(@"unsetValue can't be null if using a SetValuesList!");
            }
        }

        /// <summary>
        /// The type of parameter. The value must be able to be parsed into this type, for example, "bool" must be true or false. "bool?" can be true, false, or Not Set.
        /// </summary>
        public string ValueType { get; set; }
        /// <summary>
        /// The descriptor/key for the value
        /// </summary>
        public string Key { get; set; }
        /// <summary>
        /// The value of this parameter
        /// </summary>
        public string Value { get; set; }
        /// <summary>
        /// The human name of this parameter
        /// </summary>
        public string HumanName { get; set; }

        /// <summary>
        /// If the editor should display a list of predefined values.
        /// </summary>
        public bool UsesSetValuesList { get; set; } = false;
        
        /// <summary>
        /// The list of allowed values. Requires UsesSetValuesList to be true.
        /// </summary>
        public ObservableCollectionExtended<string> AllowedValues { get; } = new();
        
        /// <summary>
        /// The item in AllowedValues that represents blank.
        /// </summary>
        public string UnsetValueItem { get; set; }

        /// <summary>
        /// Which header this descriptor is under
        /// </summary>
        public string Header { get; set; }
        
        /// <summary>
        /// Maps a mapping of strings to strings into a list of MDParameter objects.
        /// </summary>
        /// <param name="parameterDictionary"></param>
        /// <param name="header"></param>
        /// <returns></returns>
        public static List<MDParameter> MapIntoParameterMap(Dictionary<string, object> parameterDictionary, string header = null)
        {
            var parammap = new List<MDParameter>();
            foreach (var p in parameterDictionary)
            {
                if (p.Value is MDParameter param)
                {
                    // Premade. Use that one instead.
                    parammap.Add(param);
                    continue;
                }
                
                // Generate one
                param = new MDParameter(GetMDType(p), p.Key, GetValue(p));
                param.Header = header;
                if (header == @"ModInfo" && p.Key == @"moddesc")
                {
                    param.AcceptsNewLines = true;
                }
                parammap.Add(param);
            }

            return parammap;
        }

        private static string GetMDType(in KeyValuePair<string, object> keyValuePair)
        {
            if (keyValuePair.Value is bool)
            {
                return @"bool";
            }

            return @"string";
            //if (keyValuePair.Value is IEnumerable)
            //{
            //    return "string"; // it's some sort of list. We just want to parse it as a string
            //}
        }

        private static string GetValue(in KeyValuePair<string, object> keyValuePair)
        {
            if (keyValuePair.Value is IEnumerable enumerable && !(enumerable is string))
            {
                // Is enumerable object
                string str = "";
                foreach (var v in enumerable)
                {
                    if (str.Length != 0)
                    {
                        str += @";";
                    }

                    str += v.ToString();
                }
                return str;
            }
            else
            {
                return keyValuePair.Value?.ToString();
            }
        }
    }
}
