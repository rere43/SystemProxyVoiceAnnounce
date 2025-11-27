using Microsoft.Win32;
using ProxyMonitor.Services;
using System;
using System.Diagnostics;
using System.Speech.Synthesis;
using System.Windows;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Data;
using System.Globalization;
using System.Collections.Generic;
using System.Windows.Media;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace ProxyMonitor
{
    // Converters for Toggle Switch
    public class BooleanToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return (bool)value ? new SolidColorBrush(Color.FromRgb(0, 122, 204)) : new SolidColorBrush(Color.FromRgb(62, 62, 66)); // #007ACC vs #3E3E42
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }

    public class BooleanToAlignmentConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return (bool)value ? HorizontalAlignment.Right : HorizontalAlignment.Left;
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }

    public class VoiceItem
    {
        public string Id { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
    }

    public partial class MainWindow : Window
    {
        private readonly ConfigService _configService;
        private readonly MonitorService _monitorService;

        public MainWindow(ConfigService configService, MonitorService monitorService)
        {
            // Register Converters in Resources (Code-behind approach to avoid XAML namespace issues if any)
            this.Resources.Add("BooleanToColorConverter", new BooleanToColorConverter());
            this.Resources.Add("BooleanToAlignmentConverter", new BooleanToAlignmentConverter());

            InitializeComponent();
            _configService = configService;
            _monitorService = monitorService;
            LoadSettings();
        }

        private void LoadSettings()
        {
            TxtContent.Text = _configService.CurrentConfig.TtsText;
            TxtContentDisabled.Text = _configService.CurrentConfig.TtsTextDisabled;
            TxtAudioEnabled.Text = _configService.CurrentConfig.AudioFileEnabled ?? string.Empty;
            TxtAudioDisabled.Text = _configService.CurrentConfig.AudioFileDisabled ?? string.Empty;
            SldVolume.Value = _configService.CurrentConfig.Volume;
            SldRate.Value = _configService.CurrentConfig.Rate;
            SldAudioEnabledVolume.Value = _configService.CurrentConfig.AudioEnabledVolume;
            SldAudioDisabledVolume.Value = _configService.CurrentConfig.AudioDisabledVolume;
            ChkStartup.IsChecked = _configService.CurrentConfig.RunOnStartup;
            ChkUseAudioFile.IsChecked = _configService.CurrentConfig.UseAudioFile;

            // Load TUN settings
            ChkEnableTunMonitoring.IsChecked = _configService.CurrentConfig.EnableTunMonitoring;
            TxtTunEnabled.Text = _configService.CurrentConfig.TunEnabledText;
            TxtTunDisabled.Text = _configService.CurrentConfig.TunDisabledText;
            TxtAudioTunEnabled.Text = _configService.CurrentConfig.AudioFileTunEnabled ?? string.Empty;
            TxtAudioTunDisabled.Text = _configService.CurrentConfig.AudioFileTunDisabled ?? string.Empty;
            SldAudioTunEnabledVolume.Value = _configService.CurrentConfig.AudioTunEnabledVolume;
            SldAudioTunDisabledVolume.Value = _configService.CurrentConfig.AudioTunDisabledVolume;

            // Set initial visibility based on UseAudioFile  
            UpdateControlVisibility(_configService.CurrentConfig.UseAudioFile);
            UpdateTunControlVisibility(_configService.CurrentConfig.EnableTunMonitoring);

            // Load Voices with Language Info
            using (SpeechSynthesizer synth = new SpeechSynthesizer())
            {
                var voices = synth.GetInstalledVoices()
                    .Where(v => v.Enabled)
                    .Select(v => new VoiceItem
                    {
                        Id = v.VoiceInfo.Name,
                        DisplayName = $"{v.VoiceInfo.Name} ({v.VoiceInfo.Culture.Name})"
                    })
                    .ToList();

                CmbVoices.ItemsSource = voices;

                if (!string.IsNullOrEmpty(_configService.CurrentConfig.VoiceName))
                {
                    var selected = voices.FirstOrDefault(v => v.Id == _configService.CurrentConfig.VoiceName);
                    if (selected != null)
                    {
                        CmbVoices.SelectedItem = selected;
                    }
                    else if (voices.Count > 0)
                    {
                        CmbVoices.SelectedIndex = 0;
                    }
                }
                else if (voices.Count > 0)
                {
                    CmbVoices.SelectedIndex = 0;
                }
            }
        }

        private void BtnTest_Click(object sender, RoutedEventArgs e)
        {
            string text = TxtContent.Text;
            if (string.IsNullOrWhiteSpace(text)) text = "测试语音";

            int volume = (int)SldVolume.Value;
            int rate = (int)SldRate.Value;
            string? voiceName = CmbVoices.SelectedValue as string;

            Task.Run(() =>
            {
                try
                {
                    using (SpeechSynthesizer synth = new SpeechSynthesizer())
                    {
                        synth.Volume = volume;
                        synth.Rate = rate;

                        if (!string.IsNullOrEmpty(voiceName))
                        {
                            synth.SelectVoice(voiceName);
                        }

                        synth.Speak(text);
                    }
                }
                catch (Exception ex)
                {
                    Dispatcher.Invoke(() => MessageBox.Show($"试听失败: {ex.Message}"));
                }
            });
        }

        private void BtnSelectAudioEnabled_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog dialog = new OpenFileDialog
            {
                Filter = "WAV音频文件 (*.wav)|*.wav",
                Title = "选择开启语音音频文件"
            };

            if (dialog.ShowDialog() == true)
            {
                TxtAudioEnabled.Text = dialog.FileName;
            }
        }

        private void BtnSelectAudioDisabled_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog dialog = new OpenFileDialog
            {
                Filter = "WAV音频文件 (*.wav)|*.wav",
                Title = "选择关闭语音音频文件"
            };

            if (dialog.ShowDialog() == true)
            {
                TxtAudioDisabled.Text = dialog.FileName;
            }
        }

        private void BtnTestAudioEnabled_Click(object sender, RoutedEventArgs e)
        {
            string audioFilePath = TxtAudioEnabled.Text;
            if (string.IsNullOrWhiteSpace(audioFilePath) || !System.IO.File.Exists(audioFilePath))
            {
                MessageBox.Show("请选择有效的音频文件", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            float volume = (float)SldAudioEnabledVolume.Value / 100.0f;

            Task.Run(() =>
            {
                try
                {
                    using (var audioFile = new AudioFileReader(audioFilePath))
                    {
                        var volumeSampleProvider = new VolumeSampleProvider(audioFile) { Volume = volume };
                        using (var outputDevice = new WaveOutEvent())
                        {
                            outputDevice.Init(volumeSampleProvider);
                            outputDevice.Play();

                            // 等待播放完成
                            while (outputDevice.PlaybackState == PlaybackState.Playing)
                            {
                                System.Threading.Thread.Sleep(100);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Dispatcher.Invoke(() => MessageBox.Show($"试听失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error));
                }
            });
        }

        private void BtnTestAudioDisabled_Click(object sender, RoutedEventArgs e)
        {
            string audioFilePath = TxtAudioDisabled.Text;
            if (string.IsNullOrWhiteSpace(audioFilePath) || !System.IO.File.Exists(audioFilePath))
            {
                MessageBox.Show("请选择有效的音频文件", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            float volume = (float)SldAudioDisabledVolume.Value / 100.0f;

            Task.Run(() =>
            {
                try
                {
                    using (var audioFile = new AudioFileReader(audioFilePath))
                    {
                        var volumeSampleProvider = new VolumeSampleProvider(audioFile) { Volume = volume };
                        using (var outputDevice = new WaveOutEvent())
                        {
                            outputDevice.Init(volumeSampleProvider);
                            outputDevice.Play();

                            // 等待播放完成
                            while (outputDevice.PlaybackState == PlaybackState.Playing)
                            {
                                System.Threading.Thread.Sleep(100);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Dispatcher.Invoke(() => MessageBox.Show($"试听失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error));
                }
            });
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            // Update Config
            _configService.CurrentConfig.TtsText = TxtContent.Text;
            _configService.CurrentConfig.TtsTextDisabled = TxtContentDisabled.Text;
            _configService.CurrentConfig.AudioFileEnabled = string.IsNullOrWhiteSpace(TxtAudioEnabled.Text) ? null : TxtAudioEnabled.Text;
            _configService.CurrentConfig.AudioFileDisabled = string.IsNullOrWhiteSpace(TxtAudioDisabled.Text) ? null : TxtAudioDisabled.Text;
            _configService.CurrentConfig.Volume = (int)SldVolume.Value;
            _configService.CurrentConfig.Rate = (int)SldRate.Value;
            _configService.CurrentConfig.AudioEnabledVolume = (int)SldAudioEnabledVolume.Value;
            _configService.CurrentConfig.AudioDisabledVolume = (int)SldAudioDisabledVolume.Value;
            _configService.CurrentConfig.VoiceName = CmbVoices.SelectedValue as string;
            _configService.CurrentConfig.UseAudioFile = ChkUseAudioFile.IsChecked == true;
            _configService.CurrentConfig.RunOnStartup = ChkStartup.IsChecked == true;

            // Save TUN settings
            _configService.CurrentConfig.EnableTunMonitoring = ChkEnableTunMonitoring.IsChecked == true;
            _configService.CurrentConfig.TunEnabledText = TxtTunEnabled.Text;
            _configService.CurrentConfig.TunDisabledText = TxtTunDisabled.Text;
            _configService.CurrentConfig.AudioFileTunEnabled = string.IsNullOrWhiteSpace(TxtAudioTunEnabled.Text) ? null : TxtAudioTunEnabled.Text;
            _configService.CurrentConfig.AudioFileTunDisabled = string.IsNullOrWhiteSpace(TxtAudioTunDisabled.Text) ? null : TxtAudioTunDisabled.Text;
            _configService.CurrentConfig.AudioTunEnabledVolume = (int)SldAudioTunEnabledVolume.Value;
            _configService.CurrentConfig.AudioTunDisabledVolume = (int)SldAudioTunDisabledVolume.Value;

            // Save to file
            _configService.Save();

            // Set Startup
            SetStartup(ChkStartup.IsChecked == true);

            // Hide Window
            this.Hide();

            // Ensure Monitor is running
            _monitorService.Start();
        }

        private void SetStartup(bool enable)
        {
            try
            {
                string runKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(runKey, true))
                {
                    if (key == null) return;

                    string appName = "ProxyMonitor";
                    if (enable)
                    {
                        string exePath = Process.GetCurrentProcess().MainModule.FileName;
                        // Add --silent argument
                        key.SetValue(appName, $"\"{exePath}\" --silent");
                    }
                    else
                    {
                        if (key.GetValue(appName) != null)
                        {
                            key.DeleteValue(appName);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"设置开机启动失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ChkUseAudioFile_Checked(object sender, RoutedEventArgs e)
        {
            UpdateControlVisibility(true);
            AdjustWindowSize();
        }

        private void ChkUseAudioFile_Unchecked(object sender, RoutedEventArgs e)
        {
            UpdateControlVisibility(false);
            AdjustWindowSize();
        }

        private void UpdateControlVisibility(bool useAudioFile)
        {
            if (useAudioFile)
            {
                // Hide TTS panels, show Audio panels
                PanelTtsContent.Visibility = Visibility.Collapsed;
                PanelAudioContent.Visibility = Visibility.Visible;
                BorderVoiceSettings.Visibility = Visibility.Collapsed;
            }
            else
            {
                // Show TTS panels, hide Audio panels
                PanelTtsContent.Visibility = Visibility.Visible;
                PanelAudioContent.Visibility = Visibility.Collapsed;
                BorderVoiceSettings.Visibility = Visibility.Visible;
            }
        }

        private void AdjustWindowSize()
        {
            // Temporarily set SizeToContent to adjust height
            SizeToContent = SizeToContent.Height;
            UpdateLayout();

            // Reset to Manual to allow user resizing
            Task.Delay(50).ContinueWith(_ =>
            {
                Dispatcher.Invoke(() => SizeToContent = SizeToContent.Manual);
            });
        }

        private void ChkEnableTunMonitoring_Checked(object sender, RoutedEventArgs e)
        {
            UpdateTunControlVisibility(true);
            AdjustWindowSize();
        }

        private void ChkEnableTunMonitoring_Unchecked(object sender, RoutedEventArgs e)
        {
            UpdateTunControlVisibility(false);
            AdjustWindowSize();
        }

        private void UpdateTunControlVisibility(bool enableTunMonitoring)
        {
            // Update visibility of TUN sub-panels in both TTS and Audio modes
            PanelTunTtsContent.Visibility = enableTunMonitoring ? Visibility.Visible : Visibility.Collapsed;
            PanelTunAudioContent.Visibility = enableTunMonitoring ? Visibility.Visible : Visibility.Collapsed;
        }

        private void BtnTestTunEnabled_Click(object sender, RoutedEventArgs e)
        {
            string text = TxtTunEnabled.Text;
            if (string.IsNullOrWhiteSpace(text)) text = "TUN模式开启";

            int volume = (int)SldVolume.Value;
            int rate = (int)SldRate.Value;
            string? voiceName = CmbVoices.SelectedValue as string;

            Task.Run(() =>
            {
                try
                {
                    using (SpeechSynthesizer synth = new SpeechSynthesizer())
                    {
                        synth.Volume = volume;
                        synth.Rate = rate;

                        if (!string.IsNullOrEmpty(voiceName))
                        {
                            synth.SelectVoice(voiceName);
                        }

                        synth.Speak(text);
                    }
                }
                catch (Exception ex)
                {
                    Dispatcher.Invoke(() => MessageBox.Show($"试听失败: {ex.Message}"));
                }
            });
        }

        private void BtnTestTunDisabled_Click(object sender, RoutedEventArgs e)
        {
            string text = TxtTunDisabled.Text;
            if (string.IsNullOrWhiteSpace(text)) text = "TUN模式关闭";

            int volume = (int)SldVolume.Value;
            int rate = (int)SldRate.Value;
            string? voiceName = CmbVoices.SelectedValue as string;

            Task.Run(() =>
            {
                try
                {
                    using (SpeechSynthesizer synth = new SpeechSynthesizer())
                    {
                        synth.Volume = volume;
                        synth.Rate = rate;

                        if (!string.IsNullOrEmpty(voiceName))
                        {
                            synth.SelectVoice(voiceName);
                        }

                        synth.Speak(text);
                    }
                }
                catch (Exception ex)
                {
                    Dispatcher.Invoke(() => MessageBox.Show($"试听失败: {ex.Message}"));
                }
            });
        }

        private void BtnSelectAudioTunEnabled_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog dialog = new OpenFileDialog
            {
                Filter = "WAV音频文件 (*.wav)|*.wav",
                Title = "选择TUN开启音频文件"
            };

            if (dialog.ShowDialog() == true)
            {
                TxtAudioTunEnabled.Text = dialog.FileName;
            }
        }

        private void BtnSelectAudioTunDisabled_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog dialog = new OpenFileDialog
            {
                Filter = "WAV音频文件 (*.wav)|*.wav",
                Title = "选择TUN关闭音频文件"
            };

            if (dialog.ShowDialog() == true)
            {
                TxtAudioTunDisabled.Text = dialog.FileName;
            }
        }

        private void BtnTestAudioTunEnabled_Click(object sender, RoutedEventArgs e)
        {
            string audioFilePath = TxtAudioTunEnabled.Text;
            if (string.IsNullOrWhiteSpace(audioFilePath) || !System.IO.File.Exists(audioFilePath))
            {
                MessageBox.Show("请选择有效的音频文件", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            float volume = (float)SldAudioTunEnabledVolume.Value / 100.0f;

            Task.Run(() =>
            {
                try
                {
                    using (var audioFile = new AudioFileReader(audioFilePath))
                    {
                        var volumeSampleProvider = new VolumeSampleProvider(audioFile) { Volume = volume };
                        using (var outputDevice = new WaveOutEvent())
                        {
                            outputDevice.Init(volumeSampleProvider);
                            outputDevice.Play();

                            // 等待播放完成
                            while (outputDevice.PlaybackState == PlaybackState.Playing)
                            {
                                System.Threading.Thread.Sleep(100);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Dispatcher.Invoke(() => MessageBox.Show($"试听失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error));
                }
            });
        }

        private void BtnTestAudioTunDisabled_Click(object sender, RoutedEventArgs e)
        {
            string audioFilePath = TxtAudioTunDisabled.Text;
            if (string.IsNullOrWhiteSpace(audioFilePath) || !System.IO.File.Exists(audioFilePath))
            {
                MessageBox.Show("请选择有效的音频文件", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            float volume = (float)SldAudioTunDisabledVolume.Value / 100.0f;

            Task.Run(() =>
            {
                try
                {
                    using (var audioFile = new AudioFileReader(audioFilePath))
                    {
                        var volumeSampleProvider = new VolumeSampleProvider(audioFile) { Volume = volume };
                        using (var outputDevice = new WaveOutEvent())
                        {
                            outputDevice.Init(volumeSampleProvider);
                            outputDevice.Play();

                            // 等待播放完成
                            while (outputDevice.PlaybackState == PlaybackState.Playing)
                            {
                                System.Threading.Thread.Sleep(100);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Dispatcher.Invoke(() => MessageBox.Show($"试听失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error));
                }
            });
        }
    }
}