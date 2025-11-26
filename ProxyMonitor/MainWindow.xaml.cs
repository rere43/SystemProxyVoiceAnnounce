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
            SldVolume.Value = _configService.CurrentConfig.Volume;
            SldRate.Value = _configService.CurrentConfig.Rate;
            ChkStartup.IsChecked = _configService.CurrentConfig.RunOnStartup;

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

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            // Update Config
            _configService.CurrentConfig.TtsText = TxtContent.Text;
            _configService.CurrentConfig.TtsTextDisabled = TxtContentDisabled.Text;
            _configService.CurrentConfig.Volume = (int)SldVolume.Value;
            _configService.CurrentConfig.Rate = (int)SldRate.Value;
            _configService.CurrentConfig.VoiceName = CmbVoices.SelectedValue as string;
            _configService.CurrentConfig.RunOnStartup = ChkStartup.IsChecked == true;
            _configService.Save();

            // Handle Startup Registry
            SetStartup(_configService.CurrentConfig.RunOnStartup);

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
    }
}