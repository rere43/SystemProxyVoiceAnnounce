using Microsoft.Win32;
using System;
using System.Speech.Synthesis;
using System.Threading;
using System.Threading.Tasks;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace ProxyMonitor.Services
{
    public class MonitorService
    {
        private readonly ConfigService _configService;
        private bool _isRunning;
        private int _lastProxyState = -1; // -1: unknown, 0: off, 1: on

        public MonitorService(ConfigService configService)
        {
            _configService = configService;
        }

        public void Start()
        {
            if (_isRunning) return;
            _isRunning = true;
            InitializeState();
            Task.Run(MonitorLoop);
        }

        public void Stop()
        {
            _isRunning = false;
        }

        private void InitializeState()
        {
            using (RegistryKey? key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Internet Settings"))
            {
                if (key != null)
                {
                    object? val = key.GetValue("ProxyEnable");
                    if (val != null && val is int intVal)
                    {
                        _lastProxyState = intVal;
                        // If proxy is already ON at startup, play notification
                        if (_lastProxyState == 1)
                        {
                            PlayNotification(true);
                        }
                    }
                }
            }
        }

        private void MonitorLoop()
        {
            while (_isRunning)
            {
                try
                {
                    CheckProxy();
                }
                catch
                {
                    // Ignore errors in loop
                }
                Thread.Sleep(1000);
            }
        }

        private void CheckProxy()
        {
            using (RegistryKey? key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Internet Settings"))
            {
                if (key != null)
                {
                    object? val = key.GetValue("ProxyEnable");
                    if (val != null && val is int intVal)
                    {
                        if (_lastProxyState != -1)
                        {
                            // Transition 0 -> 1 (Enabled)
                            if (_lastProxyState == 0 && intVal == 1)
                            {
                                PlayNotification(true);
                            }
                            // Transition 1 -> 0 (Disabled)
                            else if (_lastProxyState == 1 && intVal == 0)
                            {
                                PlayNotification(false);
                            }
                        }
                        _lastProxyState = intVal;
                    }
                }
            }
        }

        private void PlayNotification(bool isEnabled)
        {
            try
            {
                // Check if user wants to use audio file AND file exists
                string? audioFilePath = isEnabled ? _configService.CurrentConfig.AudioFileEnabled : _configService.CurrentConfig.AudioFileDisabled;

                if (_configService.CurrentConfig.UseAudioFile && !string.IsNullOrEmpty(audioFilePath) && System.IO.File.Exists(audioFilePath))
                {
                    // Play audio file with volume control
                    float audioVolume = isEnabled ? _configService.CurrentConfig.AudioEnabledVolume : _configService.CurrentConfig.AudioDisabledVolume;
                    audioVolume = audioVolume / 100.0f;

                    using (var audioFile = new AudioFileReader(audioFilePath))
                    {
                        var volumeSampleProvider = new VolumeSampleProvider(audioFile) { Volume = audioVolume };
                        using (var outputDevice = new WaveOutEvent())
                        {
                            outputDevice.Init(volumeSampleProvider);
                            outputDevice.Play();

                            // 等待播放完成
                            while (outputDevice.PlaybackState == PlaybackState.Playing)
                            {
                                Thread.Sleep(100);
                            }
                        }
                    }
                }
                else
                {
                    // Fallback to TTS
                    using (SpeechSynthesizer synth = new SpeechSynthesizer())
                    {
                        synth.Volume = _configService.CurrentConfig.Volume;
                        synth.Rate = _configService.CurrentConfig.Rate;

                        if (!string.IsNullOrEmpty(_configService.CurrentConfig.VoiceName))
                        {
                            try
                            {
                                synth.SelectVoice(_configService.CurrentConfig.VoiceName);
                            }
                            catch
                            {
                                // Fallback to default if voice not found
                            }
                        }

                        string textToSpeak = isEnabled ? _configService.CurrentConfig.TtsText : _configService.CurrentConfig.TtsTextDisabled;
                        if (!string.IsNullOrEmpty(textToSpeak))
                        {
                            synth.Speak(textToSpeak);
                        }
                    }
                }
            }
            catch
            {
                // Ignore errors
            }
        }
    }
}
