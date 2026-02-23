using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

namespace ASIORecAndPlay
{
    internal enum ChannelType
    {
        Input,
        Output
    }

    internal static class Asio
    {
        public static string[] GetDevices()
        {
            return AsioOut.GetDriverNames();
        }

        public static void ShowControlPanel(string device)
        {
            using (var asio = new AsioOut(device))
            {
                asio.ShowControlPanel();
                asio.Dispose();
            }
        }

        public static string[] GetChannelNames(string device, ChannelType channelType)
        {
            using (var asio = new AsioOut(device))
            {
                int count = asio.DriverOutputChannelCount;
                Func<int, string> getName = (i) => asio.AsioOutputChannelName(i);
                if (channelType == ChannelType.Input)
                {
                    count = asio.DriverInputChannelCount;
                    getName = (i) => asio.AsioInputChannelName(i);
                }

                var nameList = new List<string>();
                for (int i = 0; i < count; ++i)
                {
                    nameList.Add(getName(i));
                }

                asio.Dispose();

                return nameList.ToArray();
            }
        }


        public static int[] GetSampleRateSupported(string device)
        {
            // https://en.wikipedia.org/wiki/Sampling_(signal_processing)
            int[] sampleRateCheckList = new int[] { 8000, 11025, 16000, 22050, 32000, 37800, 44056, 44100, 47250, 48000, 50000, 50400, 64000, 88200, 96000, 176400, 192000, 352800, 2822400, 5644800, 11289600, 22579200 };

            using (var asio = new AsioOut(device))
            {
                var sampleRateSupportedList = new List<int>();
                for (int i = 0; i < sampleRateCheckList.Length; ++i)
                {
                    if (asio.IsSampleRateSupported(sampleRateCheckList[i]))
                    {
                        sampleRateSupportedList.Add(sampleRateCheckList[i]);
                    }
                }

                asio.Dispose();

                return sampleRateSupportedList.ToArray();
            }
        }
        
        public static void GetDeviceData(string device)
        {
            int[] sampleRateSupportedList = GetSampleRateSupported(device);

            for (int i = 0; i < sampleRateSupportedList.Length; ++i)
            {
                using (var asio = new AsioOut(device))
                {
                    Debug.WriteLine($"Supported sample rate: {sampleRateSupportedList[i]}");

                    try
                    {
                        asio.InitRecordAndPlayback(null, 2, sampleRateSupportedList[i]);
                        AsioAudioAvailableEventArgs args = null;
                        asio.AudioAvailable += (s, e) => args = e;
                        asio.Play();
                        Thread.Sleep(1000); // wait a little to get some samples
                        asio.Stop();


                        if (args != null)
                        {
                            Debug.WriteLine($"Sample format: {args.AsioSampleType}");
                        }
                        else
                        {
                            Debug.WriteLine("Sample format not detected");
                            ;
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Exception: {ex.Message}");
                        Debug.WriteLine($"Exception type: {ex.GetType()}");
                        Debug.WriteLine($"Exception stack: {ex.StackTrace}");
                    }


                    asio.Dispose();
                }
            }
        }

    }
}