using System;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;

namespace DumpMiner.Infrastructure
{
    public class SettingsManager
    {
        public const string SymbolCache = "SymbolCache";
        public const string DefaultTimeout = "DefaultTimeout";
        public const string Theme = "Theme";
        public const string AccentColor = "AccentColor";


        private readonly string _settingsFilePath;
        internal static readonly SettingsManager Instance = new SettingsManager();
        private SettingsManager()
        {
            var sourceFilePath = GetSourceFilePath();
            var settingsDir = Path.Combine(sourceFilePath, "..", "..", "..", "App Settings");
            if (!Directory.Exists(settingsDir))
            {
                Directory.CreateDirectory(settingsDir);
            }

            _settingsFilePath = Path.Combine(settingsDir, "settings.txt");
            if (!File.Exists(_settingsFilePath))
            {
                File.Create(_settingsFilePath);
            }
        }

        public static string GetSourceFilePath([CallerFilePath] string sourceFilePath = null)
        {
            return sourceFilePath ?? throw new InvalidOperationException("Can't obtain settings file path");
        }

        internal void SaveSettings(string property, string value)
        {
            int indexToUpdate = -1;
            var lines = File.ReadAllLines(_settingsFilePath);
            for (int i = 0; i < lines.Length; i++)
            {
                if (lines[i].StartsWith(property))
                {
                    indexToUpdate = i;
                    break;
                }
            }

            if (indexToUpdate >= 0)
            {
                lines[indexToUpdate] = $"{property}={value}";
                File.WriteAllLines(_settingsFilePath, lines);
            }
            else
            {
                File.AppendAllLines(_settingsFilePath, new[] { $"{property}={value}" });
            }
        }

        internal string ReadSettingValue(string property)
        {
            var setting = File.ReadLines(_settingsFilePath).FirstOrDefault(l => l.StartsWith(property));
            if (setting == null)
            {
                return null;
            }

            var equalIndex = setting.IndexOf('=');
            if (equalIndex == -1)
            {
                throw new ArgumentException(property);
            }

            return setting.Substring(equalIndex + 1);
        }
    }
}