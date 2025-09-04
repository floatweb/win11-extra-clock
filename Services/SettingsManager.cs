using System;
using System.IO;
using System.Text.Json;
using Win11_Extra_Clock.Models;


namespace Win11_Extra_Clock.Services
{
    public sealed class AppSettings
    {
        public Position Position { get; set; } = Position.TopRight;
    }


    public static class SettingsManager
    {
        private static readonly string Dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Win11_Extra_Clock");
        private static readonly string FilePath = Path.Combine(Dir, "settings.json");
        private static readonly JsonSerializerOptions PrettyJson = new() { WriteIndented = true };


        public static AppSettings Settings { get; private set; } = new AppSettings();


        public static void Load()
        {
            try
            {
                if (File.Exists(FilePath))
                {
                    var txt = File.ReadAllText(FilePath);
                    var s = JsonSerializer.Deserialize<AppSettings>(txt);
                    if (s != null) Settings = s;
                }
            }
            catch {  }
        }


        public static void Save()
        {
            try
            {
                Directory.CreateDirectory(Dir);
                var txt = JsonSerializer.Serialize(Settings, PrettyJson);
                File.WriteAllText(FilePath, txt);
            }
            catch {  }
        }

    }
}