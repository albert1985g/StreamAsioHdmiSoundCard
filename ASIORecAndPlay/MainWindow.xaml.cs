// Simple program to route audio between ASIO devices
// Copyright(C) 2017-2019 LAGonauta

// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.

// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.See the
// GNU General Public License for more details.

// You should have received a copy of the GNU General Public License
// along with this program.If not, see<http://www.gnu.org/licenses/>.

using AudioVUMeter;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows;
using System.Windows.Controls;

namespace ASIORecAndPlay
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// Based on work by Mark Heath on NAudio ASIO PatchBay
    /// </summary>
    public partial class MainWindow : Window
    {

        private AppSettings appSettings;
        private RecAndPlay asioRecAndPlay;
        private bool running;
        private bool eventProcessingDisabled;

        private System.Windows.Forms.NotifyIcon tray_icon;

        private void DispatchStatusText(object buffer)
        {
            Application.Current.Dispatcher.Invoke(new Action(() => UpdateText($"Buffered time: {((RecAndPlay)buffer).BufferedDuration().TotalMilliseconds.ToString()} ms.")));
        }

        private void DispatchPlaybackMeters(object buffer)
        {
            Application.Current.Dispatcher.Invoke(new Action(() => UpdateMeter(((RecAndPlay)buffer).PlaybackAudioValue)));
        }

        private void UpdateText(string message)
        {
            if (WindowState != WindowState.Minimized)
            {
                status_text.Text = message;
            }
        }

        private void UpdateMeter(VolumeMeterChannels values)
        {
            if (WindowState != WindowState.Minimized)
            {
                playBack_left.NewSampleValues(1, new VUValue[] { values.Left });
                playBack_right.NewSampleValues(1, new VUValue[] { values.Right });
                playBack_center.NewSampleValues(1, new VUValue[] { values.Center });
                playBack_bl.NewSampleValues(1, new VUValue[] { values.BackLeft });
                playBack_br.NewSampleValues(1, new VUValue[] { values.BackRight });
                playBack_sl.NewSampleValues(1, new VUValue[] { values.SideLeft });
                playBack_sr.NewSampleValues(1, new VUValue[] { values.SideRight });
                playBack_sw.NewSampleValues(1, new VUValue[] { values.Sub });
            }
        }

        private void UI_WasapiLatency_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (eventProcessingDisabled) return;

            UI_WasapiLatencyText.Text = $"Buffer size: {(sender as Slider).Value.ToString("F0")} ms";
        }

        public MainWindow()
        {
            InitializeComponent();

            var icon = System.Drawing.Icon.ExtractAssociatedIcon(Assembly.GetEntryAssembly().ManifestModule.Name);

            tray_icon = new System.Windows.Forms.NotifyIcon()
            {
                Visible = true,
                Text = Title,
                Icon = icon
            };

            tray_icon.DoubleClick +=
              delegate (object sender, EventArgs e)
              {
                  Show();
                  WindowState = WindowState.Normal;
              };

            appSettings = new AppSettings();
            SetInterfaceSettings();
            PopulateDevicesList();
            SetInterfaceSettings();
            Closing += (sender, args) => Stop();
        }

        private void PopulateDriver()
        {
            if (UI_AsioRadioButton.IsChecked.GetValueOrDefault(true))
            {
                UI_AsioPlayBackSettings.Visibility = Visibility.Visible;
                UI_WasapiPlaybackSettings.Visibility = Visibility.Collapsed;
                UI_WasapiChannelConfigPanel.Visibility = Visibility.Collapsed;
                Grid.SetColumnSpan(UI_PlaybackDeviceCombobox, 2);
            }
            else
            {
                UI_AsioPlayBackSettings.Visibility = Visibility.Collapsed;
                UI_WasapiPlaybackSettings.Visibility = Visibility.Visible;
                UI_WasapiChannelConfigPanel.Visibility = Visibility.Visible;
                Grid.SetColumnSpan(UI_PlaybackDeviceCombobox, 1);
            }
            PopulatePlaybackDevicesList();
        }

        private void PopulateDevicesList()
        {
            PopulatePlaybackDevicesList();
            PopulateRecordingDeviceList();
        }

        private void PopulatePlaybackDevicesList()
        {
            UI_PlaybackDevices.Items.Clear();
            var IsAsioDriver = UI_AsioRadioButton.IsChecked.GetValueOrDefault(true);
            var playbackDeviceList = IsAsioDriver ? Asio.GetDevices() : Wasapi.Endpoints(DataFlow.Render, DeviceState.Active).Select(e => e.FriendlyName);
            foreach (var device in playbackDeviceList)
            {
                UI_PlaybackDevices.Items.Add(device);
            }

            if (UI_PlaybackDevices.Items.Count > 0)
            {
                UI_PlaybackDevices.SelectedIndex = 0;
            }
        }

        private void PopulateRecordingDeviceList()
        {
            UI_RecordDevices.Items.Clear();

            foreach (var device in Asio.GetDevices())
            {
                UI_RecordDevices.Items.Add(device);
            }

            if (UI_RecordDevices.Items.Count > 0)
            {
                UI_RecordDevices.SelectedIndex = 0;
            }
        }

        private void OnDeviceComboBoxStateChanged(object sender, SelectionChangedEventArgs e)
        {
            if (eventProcessingDisabled) return;

            if (UI_ChannelMapping != null && (sender == UI_RecordDevices || sender == UI_PlaybackDevices || sender == UI_WasapiChannelConfig))
            {
                LoadChannels();
            }
        }

        private void LoadChannels()
        {
            UI_ChannelMapping.Children.Clear();
            List<string> input = new List<string>();
            if (UI_RecordDevices.SelectedItem != null)
            {
                input.Add("None");
                input.AddRange(Asio.GetChannelNames(UI_RecordDevices.SelectedItem.ToString(), ChannelType.Input));

                int[] sampleRateList = Asio.GetSampleRateSupported(UI_RecordDevices.SelectedItem.ToString());
                UI_RecordDeviceSampleRate.Items.Clear();
                for (int i = 0; i < sampleRateList.Length; ++i)
                {
                    UI_RecordDeviceSampleRate.Items.Add(sampleRateList[sampleRateList.Length - i - 1].ToString());
                }
                if (UI_RecordDeviceSampleRate.Items.Count > 0)
                {
                    UI_RecordDeviceSampleRate.SelectedIndex = 0;
                }

                //Asio.GetDeviceData(UI_RecordDevices.SelectedItem.ToString());
            }

            string[] output = new string[0];
            if (UI_PlaybackDevices.SelectedItem != null)
            {
                if (UI_AsioRadioButton.IsChecked.GetValueOrDefault(true))
                {
                    output = Asio.GetChannelNames(UI_PlaybackDevices.SelectedItem.ToString(), ChannelType.Output);
                }
                else
                {
                    output = Wasapi.GetChannelNames((ChannelLayout)UI_WasapiChannelConfig.SelectedIndex);
                }
            }

            for (int i = 0; i < output.Length; ++i)
            {
                var text = new TextBlock { Text = output[i] };
                var comboBox = new ComboBox
                {
                    ItemsSource = input,
                    Margin = new Thickness { Bottom = 1, Top = 1, Left = 0, Right = 0 },
                    SelectedIndex = input.Count > 1 ? i % (input.Count - 1) + 1 : 0
                };

                UI_ChannelMapping.Children.Add(text);
                UI_ChannelMapping.Children.Add(comboBox);
            }
        }

        private void OnDeviceSampleRateComboBoxStateChanged(object sender, SelectionChangedEventArgs e)
        {
            if (eventProcessingDisabled) return;
        }

        protected override void OnStateChanged(EventArgs e)
        {
            if (eventProcessingDisabled) return;

            if (WindowState == WindowState.Minimized)
            {
                Hide();
                if (asioRecAndPlay != null)
                {
                    asioRecAndPlay.CalculateRMS = false;
                }
            }
            else
            {
                if (asioRecAndPlay != null)
                {
                    asioRecAndPlay.CalculateRMS = true;
                }
            }

            base.OnStateChanged(e);
        }

        private void Window_Closing(object sender, EventArgs e)
        {
            if (eventProcessingDisabled) return;
            tray_icon.Visible = false;
        }

        private void OnButtonCPClick(object sender, RoutedEventArgs e)
        {
            if (eventProcessingDisabled) return;

            string device = string.Empty;
            if (sender == UI_AsioPlaybackControlPanel)
            {
                device = UI_PlaybackDevices.Text;
            }
            else if (sender == buttonRecCP)
            {
                device = UI_RecordDevices.Text;
            }

            if (!string.IsNullOrWhiteSpace(device))
            {
                if (asioRecAndPlay != null && asioRecAndPlay.Valid)
                {
                    asioRecAndPlay.ShowControlPanel(device);
                }
                else
                {
                    Asio.ShowControlPanel(device);
                }
            }
        }

        private void OnPlaybackDriverChanged(object sender, RoutedEventArgs e)
        {
            if (eventProcessingDisabled) return;
            PopulateDriver();
        }
        
        private Timer statusTextTimer;
        private Timer audioMeterTimer;

        private void OnButtonBeginClick(object sender, RoutedEventArgs e)
        {
            if (eventProcessingDisabled) return;

            if (!running)
            {
                if (UI_AsioRadioButton.IsChecked.GetValueOrDefault(true) == false || UI_RecordDevices.SelectedIndex != UI_PlaybackDevices.SelectedIndex)
                {
                    running = true;

                    GetInterfaceSettings();
                    appSettings.Save();

                    var mapping = new ChannelMapping();
                    {
                        foreach (var item in appSettings.Data.ChannelMapping)
                        {
                            mapping.Add(item.Key - 1, item.Value - 1);
                        }
                    }

                    asioRecAndPlay = new RecAndPlay(
                        new AsioOut(appSettings.Data.RecordDevice.DeviceName),
                        appSettings.Data.PlaybackDevice.IsAsioDriver() ? new AsioOut(appSettings.Data.PlaybackDevice.DeviceName) : (IWavePlayer)new WasapiOut(
                            Wasapi.Endpoints(DataFlow.Render, DeviceState.Active).First(endpoint => endpoint.FriendlyName == appSettings.Data.PlaybackDevice.DeviceName),
                            appSettings.Data.PlaybackDevice.WasapiExclusiveMode ? AudioClientShareMode.Exclusive : AudioClientShareMode.Shared,
                            appSettings.Data.PlaybackDevice.WasapiPullMode,
                            appSettings.Data.PlaybackDevice.WasapiBufferSize
                        ),
                        mapping,
                        appSettings.Data.PlaybackDevice.GetChannelLayout(),
                        appSettings.Data.RecordDevice.SampleRate
                    );

                    BatchChangeElements(false);

                    UI_ButtonBegin.Content = "Stop";

                    asioRecAndPlay.CalculateRMS = true;
                    asioRecAndPlay.Play();

                    statusTextTimer = new Timer(new TimerCallback(DispatchStatusText), asioRecAndPlay, 0, 1000);
                    audioMeterTimer = new Timer(new TimerCallback(DispatchPlaybackMeters), asioRecAndPlay, 0, 300);
                }
                else
                {
                    // When using the same ASIO device we must use other type of logic, which is not implemented here.
                    // The basis of this program, Mark Heath's NAudio ASIO PatchBay, has a proper solution for that.
                    MessageBox.Show("ASIO devices must not be the same");
                }
            }
            else
            {
                statusTextTimer.Dispose();
                audioMeterTimer.Dispose();
                Application.Current.Dispatcher.Invoke(new Action(() => UpdateText("Stopped.")));
                Stop();
            }
        }

        private void Stop()
        {
            if (running)
            {
                statusTextTimer.Dispose();
                Application.Current.Dispatcher.Invoke(new Action(() => UpdateText("Stopped.")));

                audioMeterTimer.Dispose();
                Application.Current.Dispatcher.Invoke(new Action(() => UpdateMeter(new VolumeMeterChannels())));

                running = false;
                UI_ButtonBegin.Content = "Start";
                BatchChangeElements(true);

                asioRecAndPlay.Dispose();
            }
        }

        private void BatchChangeElements(bool value)
        {
            UI_RecordDevices.IsEnabled = value;
            UI_RecordDeviceSampleRate.IsEnabled = value;
            UI_PlaybackDeviceSelection.IsEnabled = value;
            UI_ChannelMappingBox.IsEnabled = value;
            UI_PlaybackDriver.IsEnabled = value;
            UI_WasapiPlaybackSettings.IsEnabled = value;
            UI_WasapiChannelConfigPanel.IsEnabled = value;
        }

        private void GetInterfaceSettings()
        {
            //Record device
            appSettings.Data.RecordDevice.DeviceName = UI_RecordDevices.Text;
            appSettings.Data.RecordDevice.SampleRate = Int32.Parse(UI_RecordDeviceSampleRate.Text);
            //Playback device
            appSettings.Data.PlaybackDevice.SetDriverType(UI_AsioRadioButton.IsChecked.GetValueOrDefault(true));
            appSettings.Data.PlaybackDevice.DeviceName = UI_PlaybackDevices.Text;
            appSettings.Data.PlaybackDevice.WasapiExclusiveMode = UI_WasapiExclusiveMode.IsChecked.GetValueOrDefault(true);
            appSettings.Data.PlaybackDevice.WasapiPullMode = UI_WasapiPullMode.IsChecked.GetValueOrDefault(true);
            appSettings.Data.PlaybackDevice.WasapiBufferSize = (int)UI_WasapiLatency.Value;
            appSettings.Data.PlaybackDevice.SetChannelLayout(UI_WasapiChannelConfig.IsVisible ? (ChannelLayout?)UI_WasapiChannelConfig.SelectedIndex : null);
            //Channel mapping
            appSettings.Data.ChannelMapping.Clear();
            int outputChannel = 0;
            foreach (var inputBox in UI_ChannelMapping.Children.OfType<ComboBox>())
            {
                if (inputBox.SelectedIndex > 0 && !appSettings.Data.ChannelMapping.ContainsKey(inputBox.SelectedIndex))
                {
                    appSettings.Data.ChannelMapping.Add(inputBox.SelectedIndex, outputChannel + 1);
                }
                else
                {
                    inputBox.SelectedIndex = 0;
                }

                ++outputChannel;
            }
        }

        private void SetInterfaceSettings()
        {
            eventProcessingDisabled = true;

            //Set friver
            UI_AsioRadioButton.IsChecked = appSettings.Data.PlaybackDevice.IsAsioDriver();
            UI_WasapiRadioButton.IsChecked = !appSettings.Data.PlaybackDevice.IsAsioDriver();
            PopulateDriver();
            
            //Set playback device
            bool playbackDeviceFound = false;
            for (int i = 0; i < UI_PlaybackDevices.Items.Count; i++)
            {
                if (UI_PlaybackDevices.Items[i].ToString().Equals(appSettings.Data.PlaybackDevice.DeviceName, StringComparison.OrdinalIgnoreCase))
                {
                    Debug.WriteLine($"Setting playback device to {appSettings.Data.PlaybackDevice.DeviceName}");
                    UI_PlaybackDevices.SelectedIndex = i;
                    playbackDeviceFound = true;
                    break;
                }
            }

            //Set record device
            bool recordDeviceFound = false;
            for (int i = 0; i < UI_RecordDevices.Items.Count; i++)
            {
                Debug.WriteLine($"Item: {UI_RecordDevices.Items[i].ToString()} with settings: {appSettings.Data.RecordDevice.DeviceName}");

                if (UI_RecordDevices.Items[i].ToString().Equals(appSettings.Data.RecordDevice.DeviceName, StringComparison.OrdinalIgnoreCase))
                {
                    Debug.WriteLine($"Setting record device to {appSettings.Data.RecordDevice.DeviceName}");
                    UI_RecordDevices.SelectedIndex = i;
                    recordDeviceFound = true;
                    break;
                }
            }

            if (recordDeviceFound)
            {
                UI_RecordDeviceSampleRate.Text = appSettings.Data.RecordDevice.SampleRate.ToString();
            }

            if (playbackDeviceFound && !appSettings.Data.PlaybackDevice.IsAsioDriver())
            {
                UI_WasapiChannelConfig.SelectedIndex = (int)appSettings.Data.PlaybackDevice.GetChannelLayout();
                UI_WasapiExclusiveMode.IsChecked = appSettings.Data.PlaybackDevice.WasapiExclusiveMode;
                UI_WasapiPullMode.IsChecked = appSettings.Data.PlaybackDevice.WasapiPullMode;
                UI_WasapiLatency.Value = appSettings.Data.PlaybackDevice.WasapiBufferSize;
                UI_WasapiLatencyText.Text = $"Buffer size: {UI_WasapiLatency.Value.ToString("F0")} ms";
            }

            //Load channels
            LoadChannels();

            if (playbackDeviceFound && recordDeviceFound)
            {
                foreach (var inputBox in UI_ChannelMapping.Children.OfType<ComboBox>())
                {
                    if (appSettings.Data.ChannelMapping.TryGetValue(inputBox.SelectedIndex, out int outputChannel))
                    {
                        inputBox.SelectedIndex = outputChannel;
                    }
                    else
                    {
                        inputBox.SelectedIndex = 0;
                    }
                }
            }

            eventProcessingDisabled = false;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            
        }
    }
}