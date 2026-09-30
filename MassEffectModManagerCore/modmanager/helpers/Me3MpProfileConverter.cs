using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using LegendaryExplorerCore;
using LegendaryExplorerCore.Save;
using Newtonsoft.Json.Linq;

namespace ME3TweaksModManager.modmanager.helpers
{
    /// <summary>Converts the version 2 ME3ProfileDump lobby export to MilanHost's local MP_Profile format.</summary>
    public static class Me3MpProfileConverter
    {
        public static byte[] Convert(string json)
        {
            var root = JObject.Parse(json);
            if ((string)root["format"] != "ME3-MP-profile-export" ||
                (int?)root["version"] != 2 || (string)root["source"] != "ME3-OT" ||
                (string)root["trigger"] != "mpLobbyCachedSave")
                throw new InvalidDataException("Expected a version 2 ME3 multiplayer lobby profile export.");

            var save = root["save"] as JObject ?? throw new InvalidDataException("The export has no save data.");
            var classes = save["classes"] as JArray ?? throw new InvalidDataException("The export has no class slots.");
            var characters = save["characters"] as JArray ?? throw new InvalidDataException("The export has no character slots.");
            if (classes.Count > 8 || characters.Count > 78)
                throw new InvalidDataException("The export has more slots than this LMP profile format supports.");

            var effective = root["effectiveSettings"] as JArray ?? throw new InvalidDataException("The export has no effective settings.");
            var byId = new Dictionary<int, JToken>();
            foreach (var row in effective.OfType<JObject>())
            {
                var id = (int?)row["id"] ?? throw new InvalidDataException("A setting has no numeric ID.");
                if (!byId.TryAdd(id, row))
                    throw new InvalidDataException($"Duplicate profile setting ID {id}.");
            }

            var accomplishments = root["accomplishments"] as JObject;
            var settings = new SortedDictionary<int, object>();
            for (int id = 0; id <= 90; id++)
            {
                string fallback = id switch
                {
                    0 => RequiredString(save, "base"),
                    >= 1 and <= 8 => Slot(classes, id - 1),
                    >= 9 and <= 86 => Slot(characters, id - 9),
                    87 => RequiredString(save, "faceCodes"),
                    88 => RequiredString(save, "newReinforcements"),
                    89 => OptionalString(accomplishments, "completion"),
                    90 => OptionalString(accomplishments, "progress"),
                    _ => null
                };
                var value = EffectiveString(byId, id);
                if (string.IsNullOrEmpty(value)) value = fallback;
                if (id == 0 && string.IsNullOrWhiteSpace(value))
                    throw new InvalidDataException("The export has no multiplayer base profile data.");
                if (!string.IsNullOrEmpty(value) || id is 0 or 87 or 88 or 89 or 90)
                    settings.Add(id, value ?? "");
            }

            // ME3 clears these storage strings after loading. MilanHost assigns their IDs explicitly.
            AddString(91, "rewardLevels");
            settings.Add(92, EffectiveInt(byId, 92));
            AddString(93, "completionTimes");
            AddString(94, "completionTimes2");
            AddString(95, "completionTimes3");

            using var stream = new MemoryStream();
            using var writer = new BinaryWriter(stream, Encoding.Unicode, true);
            WriteBigEndian(writer, settings.Count);
            foreach (var (id, value) in settings)
            {
                var type = value is int ? (byte)1 : (byte)4;
                writer.Write(type); // ID type and data type must match in MilanHost.
                WriteBigEndian(writer, id);
                writer.Write(type);
                if (value is int number)
                    WriteBigEndian(writer, number);
                else
                    WriteUnrealString(writer, (string)value);
                writer.Write((byte)0);
            }
            return stream.ToArray();

            void AddString(int id, string name)
            {
                var value = OptionalString(accomplishments, name);
                if (string.IsNullOrEmpty(value)) value = EffectiveString(byId, id);
                settings.Add(id, value ?? "");
            }
        }

        public static void ValidateFile(string path)
        {
            // LMP reads an uncompressed, versionless profile. Read it back with the shared parser.
            var profile = LocalProfile.DeserializeLocalProfile(path, MEGame.LMP, false, false);
            if (!profile.ProfileSettings.TryGetValue(0, out var baseSetting) ||
                baseSetting.DataType != ProfileSetting.EProfileSettingType.STRING ||
                string.IsNullOrWhiteSpace(baseSetting.DataAsString) ||
                !profile.ProfileSettings.TryGetValue(92, out var reward) ||
                reward.DataType != ProfileSetting.EProfileSettingType.INT)
                throw new InvalidDataException("The converted MP_Profile failed its readback check.");
        }

        private static string RequiredString(JObject obj, string name) =>
            obj[name]?.Type == JTokenType.String ? (string)obj[name] :
            throw new InvalidDataException($"The export has no {name} string.");

        private static string OptionalString(JObject obj, string name) =>
            obj?[name]?.Type == JTokenType.String ? (string)obj[name] : null;

        private static string Slot(JArray array, int index)
        {
            if (index >= array.Count) return null;
            if (array[index].Type != JTokenType.String)
                throw new InvalidDataException($"Profile slot {index} is not a string.");
            return (string)array[index];
        }

        private static string EffectiveString(Dictionary<int, JToken> settings, int id)
        {
            if (!settings.TryGetValue(id, out var row)) return null;
            if ((int?)row["dataType"] != 4)
                throw new InvalidDataException($"Setting {id} is not a string in the ME3 export.");
            return row["value"]?.Type == JTokenType.String ? (string)row["value"] : null;
        }

        private static int EffectiveInt(Dictionary<int, JToken> settings, int id)
        {
            if (!settings.TryGetValue(id, out var row))
                throw new InvalidDataException($"The export is missing integer setting {id}.");
            if ((int?)row["dataType"] != 1 || row["value"]?.Type != JTokenType.Integer)
                throw new InvalidDataException($"Setting {id} is not a 32-bit integer in the ME3 export.");
            return checked((int)row["value"]);
        }

        private static void WriteBigEndian(BinaryWriter writer, int value)
        {
            writer.Write((byte)(value >> 24));
            writer.Write((byte)(value >> 16));
            writer.Write((byte)(value >> 8));
            writer.Write((byte)value);
        }

        private static void WriteUnrealString(BinaryWriter writer, string value)
        {
            if (value.Length == 0)
            {
                WriteBigEndian(writer, 0);
                return;
            }
            WriteBigEndian(writer, checked(-(value.Length + 1)));
            writer.Write(Encoding.Unicode.GetBytes(value + '\0'));
        }
    }
}
