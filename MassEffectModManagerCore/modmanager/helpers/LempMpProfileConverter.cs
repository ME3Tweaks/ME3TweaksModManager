using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace ME3TweaksModManager.modmanager.helpers
{
    /// <summary>
    /// Converts a LEMP (not LMP) mp_save.asi text save to MilanHost's local MP_Profile format.
    /// LEMP stores the same SFXOnlineSaveGameMP strings ME3 did, one key=value per line.
    /// </summary>
    public static class LempMpProfileConverter
    {
        // Lines with no MP_Profile record. Anything else unknown is rejected so data isn't silently lost.
        private static readonly HashSet<string> IgnoredKeys = new(StringComparer.Ordinal) { "selected", "gaw" };

        public static byte[] Convert(string text)
        {
            var lines = new Dictionary<string, List<string>>(StringComparer.Ordinal);
            using (var reader = new StringReader(text))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    if (line.Length == 0 || line.StartsWith('#'))
                    {
                        continue;
                    }
                    var split = line.IndexOf('=');
                    if (split <= 0)
                    {
                        throw new InvalidDataException("The LEMP save has a line that isn't key=value.");
                    }
                    var key = line[..split];
                    if (!lines.TryGetValue(key, out var values))
                    {
                        lines[key] = values = new List<string>();
                    }
                    values.Add(line[(split + 1)..]);
                }
            }

            if (Single(lines, "version") != "1")
            {
                throw new InvalidDataException("Expected a version 1 LEMP multiplayer save.");
            }

            foreach (var key in lines.Keys)
            {
                if (key is not ("version" or "base" or "class" or "character" or "facecodes" or "reinforcements" or "challenge") && !IgnoredKeys.Contains(key))
                {
                    throw new InvalidDataException($"The LEMP save has an unknown '{key}' record.");
                }
            }

            var classes = Many(lines, "class");
            var characters = Many(lines, "character");
            if (classes.Count > 8 || characters.Count > 78)
            {
                throw new InvalidDataException("The LEMP save has more slots than this LMP profile format supports.");
            }

            // Order: available flag, completion, progress, rewardLevels, current reward (int), completionTimes 1-3
            var challenges = Many(lines, "challenge");
            if (challenges.Count != 8)
            {
                throw new InvalidDataException($"Expected 8 challenge records in the LEMP save, found {challenges.Count}.");
            }
            if (!int.TryParse(challenges[4], NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var currentReward))
            {
                throw new InvalidDataException("The LEMP save's current reward value is not a 32-bit integer.");
            }

            var baseData = Single(lines, "base");
            if (string.IsNullOrWhiteSpace(baseData))
            {
                throw new InvalidDataException("The LEMP save has no multiplayer base profile data.");
            }

            var settings = new SortedDictionary<int, object> { [0] = baseData };
            for (int i = 0; i < classes.Count; i++)
            {
                AddSlot(1 + i, classes[i]);
            }
            for (int i = 0; i < characters.Count; i++)
            {
                AddSlot(9 + i, characters[i]);
            }
            settings[87] = Single(lines, "facecodes");
            settings[88] = Single(lines, "reinforcements");
            settings[89] = challenges[1];
            settings[90] = challenges[2];
            settings[91] = challenges[3];
            settings[92] = currentReward;
            settings[93] = challenges[5];
            settings[94] = challenges[6];
            settings[95] = challenges[7];
            return Me3MpProfileConverter.Serialize(settings);

            // Same as the ME3 converter: empty slots aren't written
            void AddSlot(int id, string value)
            {
                if (!string.IsNullOrEmpty(value))
                {
                    settings[id] = value;
                }
            }
        }

        private static List<string> Many(Dictionary<string, List<string>> lines, string key) =>
            lines.TryGetValue(key, out var values) ? values : new List<string>();

        private static string Single(Dictionary<string, List<string>> lines, string key)
        {
            var values = Many(lines, key);
            if (values.Count != 1)
            {
                throw new InvalidDataException($"Expected one '{key}' record in the LEMP save, found {values.Count}.");
            }
            return values[0];
        }
    }
}
