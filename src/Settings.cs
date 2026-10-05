using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace DressmakerAutomation.SettingsUI
{
    public sealed class Settings
    {
        public bool InfiniteMoney = true;
        public int GoldFloor = 999999999;
        public bool AutoGrainAlignment = true;
        public bool FindValidPosition = true;
        public bool AutoMannequinSizing = true;

        public static bool TryGoldFloor(string text, out int value)
        {
            return int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out value) && value >= 0;
        }

        public void Save(string path)
        {
            if (GoldFloor < 0) throw new ArgumentOutOfRangeException(nameof(GoldFloor));
            string fullPath = Path.GetFullPath(path);
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
            string previous = File.Exists(fullPath) ? File.ReadAllText(fullPath) : string.Empty;
            string temporary = fullPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                File.WriteAllText(temporary, Rewrite(previous), new UTF8Encoding(false));
                if (File.Exists(fullPath)) File.Replace(temporary, fullPath, fullPath + ".ui-backup", true);
                else File.Move(temporary, fullPath);
            }
            finally
            {
                if (File.Exists(temporary)) File.Delete(temporary);
            }
        }

        public string Rewrite(string original)
        {
            var values = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["InfiniteMoney"] = InfiniteMoney ? "true" : "false",
                ["GoldFloor"] = GoldFloor.ToString(CultureInfo.InvariantCulture),
                ["AutoGrainAlignment"] = AutoGrainAlignment ? "true" : "false",
                ["FindValidPosition"] = FindValidPosition ? "true" : "false",
                ["AutoMannequinSizing"] = AutoMannequinSizing ? "true" : "false"
            };
            var emitted = new HashSet<string>(StringComparer.Ordinal);
            var result = new List<string>();
            using (var reader = new StringReader(original ?? string.Empty))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    int equals = line.IndexOf('=');
                    string key = equals > 0 ? line.Substring(0, equals).Trim() : string.Empty;
                    if (key == "InfiniteGold") key = "InfiniteMoney";
                    if (key == "AutoMannequin") key = "AutoMannequinSizing";
                    if (values.ContainsKey(key))
                    {
                        if (emitted.Add(key)) result.Add(key + "=" + values[key]);
                    }
                    else if (line.IndexOf("# Restart the game after changing values.", StringComparison.Ordinal) >= 0)
                        result.Add("# Settings saved from the in-game panel apply without restarting.");
                    else result.Add(line);
                }
            }
            if (result.Count == 0) result.Add("# Dressmaker Automation - in-game settings");
            foreach (var pair in values.Where(pair => !emitted.Contains(pair.Key)))
                result.Add(pair.Key + "=" + pair.Value);
            return string.Join("\n", result) + "\n";
        }
    }
}
