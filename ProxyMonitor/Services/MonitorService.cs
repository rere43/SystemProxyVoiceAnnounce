using Microsoft.Win32;
using System;
using System.Speech.Synthesis;
using System.Threading;
using System.Threading.Tasks;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using System.Net.NetworkInformation;
using System.Linq;

namespace ProxyMonitor.Services
{
    public class MonitorService
    {
        private readonly ConfigService _configService;
        private bool _isRunning;
        private int _lastProxyState = -1; // -1: unknown, 0: off, 1: on
        private bool _lastTunState = false; // false: TUN off, true: TUN on

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
                            PlayNotification(true, "proxy");
                        }
                    }
                }
            }

            // Initialize TUN state
            if (_configService.CurrentConfig.EnableTunMonitoring)
            {
                _lastTunState = IsTunActive();
                if (_lastTunState)
                {
                    PlayNotification(true, "tun");
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

                    if (_configService.CurrentConfig.EnableTunMonitoring)
                    {
                        CheckTun();
                    }
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
                                PlayNotification(true, "proxy");
                            }
                            // Transition 1 -> 0 (Disabled)
                            else if (_lastProxyState == 1 && intVal == 0)
                            {
                                PlayNotification(false, "proxy");
                            }
                        }
                        _lastProxyState = intVal;
                    }
                }
            }
        }

        private void CheckTun()
        {
            bool currentTunState = IsTunActive();

            // State changed: TUN off -> on
            if (!_lastTunState && currentTunState)
            {
                PlayNotification(true, "tun");
            }
            // State changed: TUN on -> off
            else if (_lastTunState && !currentTunState)
            {
                PlayNotification(false, "tun");
            }

            _lastTunState = currentTunState;
        }

        private bool IsTunActive()
        {
            try
            {
                var interfaces = NetworkInterface.GetAllNetworkInterfaces();
                var keywords = _configService.CurrentConfig.TunKeywords;

                foreach (var iface in interfaces)
                {
                    // 检查接口描述或名称是否包含TUN关键词
                    bool isTunInterface = keywords.Any(keyword =>
                        iface.Description.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0 ||
                        iface.Name.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0);

                    // 如果是TUN接口且状态为Up,返回true
                    if (isTunInterface && iface.OperationalStatus == OperationalStatus.Up)
                    {
                        return true;
                    }
                }

                return false;
            }
            catch
            {
                return false;
            }
        }

        private void PlayNotification(bool isEnabled, string notificationType = "proxy")
        {
            try
            {
                // 根据通知类型选择文本
                string textToSpeak;
                if (notificationType == "tun")
                {
                    textToSpeak = isEnabled ? _configService.CurrentConfig.TunEnabledText : _configService.CurrentConfig.TunDisabledText;
                }
                else // proxy
                {
                    textToSpeak = isEnabled ? _configService.CurrentConfig.TtsText : _configService.CurrentConfig.TtsTextDisabled;
                }

                // Check if user wants to use audio file AND file exists
                string? audioFilePath = null;
                float audioVolume = 1.0f;

                if (_configService.CurrentConfig.UseAudioFile)
                {
                    if (notificationType == "tun")
                    {
                        audioFilePath = isEnabled ? _configService.CurrentConfig.AudioFileTunEnabled : _configService.CurrentConfig.AudioFileTunDisabled;
                        audioVolume = (isEnabled ? _configService.CurrentConfig.AudioTunEnabledVolume : _configService.CurrentConfig.AudioTunDisabledVolume) / 100.0f;
                    }
                    else // proxy
                    {
                        audioFilePath = isEnabled ? _configService.CurrentConfig.AudioFileEnabled : _configService.CurrentConfig.AudioFileDisabled;
                        audioVolume = (isEnabled ? _configService.CurrentConfig.AudioEnabledVolume : _configService.CurrentConfig.AudioDisabledVolume) / 100.0f;
                    }
                }

                if (_configService.CurrentConfig.UseAudioFile && !string.IsNullOrEmpty(audioFilePath) && System.IO.File.Exists(audioFilePath))
                {
                    // Play audio file with volume control
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
