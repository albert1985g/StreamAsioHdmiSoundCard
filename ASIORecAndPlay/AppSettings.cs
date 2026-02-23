using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Text.Json;
using System.IO;
using System.Reflection;

namespace ASIORecAndPlay
{
    internal class AppSettings
    {
        internal class EmptyModel
        {
            public string ToJson()
            {
                return JsonSerializer.Serialize(this, GetType(), new JsonSerializerOptions
                {
                    WriteIndented = true
                });
            }

            public void FromJson(string json)
            {
                var obj = JsonSerializer.Deserialize(json, GetType());
                if (obj != null)
                {
                    foreach (var property in GetType().GetProperties())
                    {
                        property.SetValue(this, property.GetValue(obj));
                    }
                }
            }
        }

        internal class SettingsData : EmptyModel
        {
            internal enum DriverType
            {
                ASIO,
                WASAPI
            }

            internal class RecordDeviceSettings : EmptyModel
            {
                public string DeviceName { get; set; }
                public Int32 SampleRate { get; set; }
            }

            internal class PlaybackDeviceSettings : EmptyModel
            {
                public string Driver { get; set; }
                public string DeviceName { get; set; }
                public string ChannelLayout { get; set; }
                public int WasapiBufferSize { get; set; }
                public bool WasapiExclusiveMode { get; set; }
                public bool WasapiPullMode { get; set; }

                public void SetDriverType(bool IsAsioDriver)
                {
                    if (IsAsioDriver)
                        Driver = DriverType.ASIO.ToString();
                    else
                        Driver = DriverType.WASAPI.ToString();
                }

                public bool IsAsioDriver()
                {
                    return Driver == DriverType.ASIO.ToString();
                }

                public void SetChannelLayout(ChannelLayout? layout = null)
                {
                    ChannelLayout = layout.ToString();
                }

                public ChannelLayout GetChannelLayout()
                {
                    if (Enum.TryParse(ChannelLayout, out ChannelLayout parsedLayout))
                    {
                        return parsedLayout;
                    }
                    
                    return ASIORecAndPlay.ChannelLayout.Mono;
                }
            }

            public RecordDeviceSettings RecordDevice { get; set; }
            public PlaybackDeviceSettings PlaybackDevice { get; set; }
            public Dictionary<int, int> ChannelMapping { get; set; }

            public SettingsData()
            {
                RecordDevice = new RecordDeviceSettings();
                PlaybackDevice = new PlaybackDeviceSettings();
                ChannelMapping = new Dictionary<int, int>();
            }
        }

        public SettingsData Data { get; set; }
        private string SettingsFilePath { get; set; }

        public AppSettings()
        {
            string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

            string appName = Assembly.GetExecutingAssembly().GetName().Name;
            string appFolder = Path.Combine(appDataPath, appName);
            Directory.CreateDirectory(appFolder);

            SettingsFilePath = Path.Combine(appFolder, "settings.json");

            if (!File.Exists(SettingsFilePath))
            {
                File.WriteAllText(SettingsFilePath, "{}");
            }

            Data = new SettingsData();
            Load();
            Save();
        }

        private void Load()
        {
            Data.FromJson(File.ReadAllText(SettingsFilePath));
        }

        public void Save()
        {
            File.WriteAllText(SettingsFilePath, Data.ToJson());
        }

    }

}
