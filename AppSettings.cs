using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using static Trimly.AppSettings;

namespace Trimly
{
    using System;
    using System.IO;
    using System.Text.Json;
    using System.Windows;

    public class AppSettings
    {
        public string Path { get; set; }
        public bool MergeAudioTracks { get; set; }
        public bool AutoMetaSet { get; set; }
        public byte Codec { get; set; }
        public byte Type { get; set; }
        public byte SampleRates { get; set; }
        public byte Channels { get; set; }
        public short Width { get; set; }
        public short Height { get; set; }
        public short FPS { get; set; }
        public int VideoBitrate { get; set; }
        public int AudioBitrate { get; set; }
        public short BufferFPS { get; set; }
        public short BufferWidth { get; set; }
        public short BufferHeight { get; set; }
        public int BufferVideoBitrate { get; set; }
        public int BufferAudioBitrate { get; set; }
        public short BufferSampleRates { get; set; }
        public short BufferChannels { get; set; }
        public short WindowWidth { get; set; }
        public short WindowHeight { get; set; }

        private static readonly string SettingsFilePath = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Trimly", "appsettings.json");

        public static AppSettings Load()
        {
            if (File.Exists(SettingsFilePath))
            {
                var json = File.ReadAllText(SettingsFilePath);
                return JsonSerializer.Deserialize<AppSettings>(json);
            }
            return new AppSettings();
        }

        public void Save()
        {
            // Ensure the directory exists
            var directory = System.IO.Path.GetDirectoryName(SettingsFilePath);
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(SettingsFilePath, json);
        }
    }
}
