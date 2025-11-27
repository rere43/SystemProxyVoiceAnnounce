# ProxyMonitor 技术文档

## 项目概述

ProxyMonitor 是一个基于 .NET 10.0 和 WPF 的 Windows 桌面应用程序，用于监控系统代理设置的变化并通过语音通知用户。该应用程序具有现代化的深色主题界面，支持文本转语音 (TTS) 和自定义音频文件两种通知方式。

## 系统架构


### 整体架构图

```
+-------------------+
|    App.xaml.cs    | <- 应用程序入口，处理启动逻辑
+-------------------+
         |
         v
+-------------------+
|  MainWindow.xaml  | <- 用户界面
+-------------------+
         |
         v
+-------------------+     +------------------+
| MainWindow.xaml.cs|<--->|  ConfigService   | <- 配置管理
+-------------------+     +------------------+
         |                        |
         v                        v
+-------------------+     +------------------+
|  MonitorService   |<----|   AppConfig      | <- 配置模型
+-------------------+     +------------------+
         |
         v
+-------------------+
| System.Speech API | <- 语音合成
+-------------------+
```

## 核心模块详解

### 1. 应用程序入口 (App.xaml.cs)

负责应用程序的初始化和启动逻辑：

```csharp
private void Application_Startup(object sender, StartupEventArgs e)
{
    // 单实例逻辑：终止其他实例
    Process current = Process.GetCurrentProcess();
    foreach (Process process in Process.GetProcessesByName(current.ProcessName))
    {
        if (process.Id != current.Id)
        {
            try
            {
                process.Kill();
            }
            catch
            {
                // 忽略权限错误
            }
        }
    }

    // 初始化服务
    _configService = new ConfigService();
    _monitorService = new MonitorService(_configService);

    // 解析启动参数
    bool isSilent = e.Args.Contains("--silent");

    // 启动监控服务
    _monitorService.Start();

    // 根据参数决定是否显示界面
    if (!isSilent)
    {
        MainWindow mainWindow = new MainWindow(_configService, _monitorService);
        mainWindow.Show();
    }
}
```

### 2. 配置管理服务 (ConfigService.cs)

负责应用程序配置的持久化管理：

```csharp
public class AppConfig
{
    public string TtsText { get; set; } = "系统代理开启";        // 开启时代理提示文本
    public string TtsTextDisabled { get; set; } = "系统代理关闭"; // 关闭时代理提示文本
    public int Volume { get; set; } = 100;                        // 语音音量
    public int Rate { get; set; } = 0;                            // 语音语速
    public string? VoiceName { get; set; }                        // 语音角色名称
    public string? AudioFileEnabled { get; set; }                 // 开启时音频文件路径
    public string? AudioFileDisabled { get; set; }                // 关闭时音频文件路径
    public bool UseAudioFile { get; set; } = false;               // 是否使用音频文件
    public bool RunOnStartup { get; set; } = false;               // 是否开机自启
}

public class ConfigService
{
    private readonly string _configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings.json");
    public AppConfig CurrentConfig { get; private set; }

    public void Load() { ... }  // 加载配置
    public void Save() { ... }   // 保存配置
}
```

### 3. 代理监控服务 (MonitorService.cs)

核心功能模块，负责监控系统代理设置并触发通知：

```csharp
public class MonitorService
{
    private readonly ConfigService _configService;
    private bool _isRunning;
    private int _lastProxyState = -1; // -1: 未知, 0: 关闭, 1: 开启

    public void Start() { ... }       // 启动监控
    public void Stop() { ... }        // 停止监控

    private void MonitorLoop() { ... }     // 监控循环
    private void CheckProxy() { ... }      // 检查代理状态
    private void PlayNotification(bool isEnabled) { ... } // 播放通知
}
```

### 4. 用户界面 (MainWindow.xaml / MainWindow.xaml.cs)

提供现代化的设置界面，采用深色主题设计：

#### 主要界面组件：

1. **语音内容设置区域**
   - 使用音频文件切换开关
   - 开启/关闭时的文本输入框
   - 开启/关闭时的音频文件选择器

2. **语音角色设置区域**
   - 系统语音角色下拉选择
   - 试听按钮
   - 音量和语速滑块

3. **系统设置区域**
   - 开机自启开关

4. **操作按钮**
   - 保存并隐藏运行按钮

#### 界面交互逻辑：

```csharp
private void ChkUseAudioFile_Checked(object sender, RoutedEventArgs e)
{
    UpdateControlVisibility(true);   // 显示音频文件选择器
    AdjustWindowSize();              // 调整窗口大小
}

private void BtnTest_Click(object sender, RoutedEventArgs e)
{
    // 异步播放测试语音
    Task.Run(() => {
        using (SpeechSynthesizer synth = new SpeechSynthesizer())
        {
            synth.Volume = (int)SldVolume.Value;
            synth.Rate = (int)SldRate.Value;
            synth.Speak(TxtContent.Text);
        }
    });
}
```

## 技术实现细节

### 1. 代理状态监控

通过轮询方式检查 Windows 注册表中的代理设置：

```csharp
private void CheckProxy()
{
    using (RegistryKey? key = Registry.CurrentUser.OpenSubKey(
        @"Software\Microsoft\Windows\CurrentVersion\Internet Settings"))
    {
        if (key != null)
        {
            object? val = key.GetValue("ProxyEnable");
            if (val != null && val is int intVal)
            {
                // 检测状态变化并触发通知
                if (_lastProxyState != -1)
                {
                    if (_lastProxyState == 0 && intVal == 1)
                        PlayNotification(true);   // 代理开启
                    else if (_lastProxyState == 1 && intVal == 0)
                        PlayNotification(false);  // 代理关闭
                }
                _lastProxyState = intVal;
            }
        }
    }
}
```

### 2. 语音通知实现

支持两种通知方式的切换：

```csharp
private void PlayNotification(bool isEnabled)
{
    try
    {
        // 检查是否使用音频文件模式
        string? audioFilePath = isEnabled ?
            _configService.CurrentConfig.AudioFileEnabled :
            _configService.CurrentConfig.AudioFileDisabled;

        if (_configService.CurrentConfig.UseAudioFile &&
            !string.IsNullOrEmpty(audioFilePath) &&
            System.IO.File.Exists(audioFilePath))
        {
            // 播放音频文件
            using (System.Media.SoundPlayer player =
                   new System.Media.SoundPlayer(audioFilePath))
            {
                player.PlaySync();
            }
        }
        else
        {
            // 使用文本转语音
            using (SpeechSynthesizer synth = new SpeechSynthesizer())
            {
                synth.Volume = _configService.CurrentConfig.Volume;
                synth.Rate = _configService.CurrentConfig.Rate;

                string textToSpeak = isEnabled ?
                    _configService.CurrentConfig.TtsText :
                    _configService.CurrentConfig.TtsTextDisabled;

                synth.Speak(textToSpeak);
            }
        }
    }
    catch
    {
        // 忽略播放错误
    }
}
```

### 3. 界面主题和样式

采用深色主题设计，自定义控件样式：

```xml
<!-- 自定义切换开关样式 -->
<Style x:Key="ToggleSwitchStyle" TargetType="CheckBox">
    <Setter Property="Template">
        <Setter.Value>
            <ControlTemplate TargetType="CheckBox">
                <Grid Background="Transparent">
                    <Grid.ColumnDefinitions>
                        <ColumnDefinition Width="*" />
                        <ColumnDefinition Width="Auto" />
                    </Grid.ColumnDefinitions>
                    <ContentPresenter Grid.Column="0" VerticalAlignment="Center" />
                    <Border Grid.Column="1" Width="40" Height="20" CornerRadius="10"
                            Background="{Binding IsChecked, RelativeSource={RelativeSource TemplatedParent},
                                         Converter={StaticResource BooleanToColorConverter}}">
                        <Ellipse Width="16" Height="16" Fill="White"
                                 HorizontalAlignment="{Binding IsChecked, RelativeSource={RelativeSource TemplatedParent},
                                              Converter={StaticResource BooleanToAlignmentConverter}}"
                                 Margin="2" />
                    </Border>
                </Grid>
            </ControlTemplate>
        </Setter.Value>
    </Setter>
</Style>
```

## 部署和配置

### 系统要求

- Windows 7 或更高版本
- .NET 10.0 运行时
- 系统语音合成支持

### 配置说明

配置文件 `settings.json` 位于应用程序目录下：

```json
{
  "TtsText": "系统代理开启",
  "TtsTextDisabled": "系统代理关闭",
  "Volume": 60,
  "Rate": 0,
  "VoiceName": "Microsoft Tracy",
  "AudioFileEnabled": "K:\\开启.wav",
  "AudioFileDisabled": "K:\\关闭.wav",
  "UseAudioFile": true,
  "RunOnStartup": true
}
```

### 开机自启实现

通过修改注册表实现开机自启：

```csharp
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
                // 添加 --silent 参数以静默启动
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
        MessageBox.Show($"设置开机启动失败: {ex.Message}");
    }
}
```

## 安全性和权限

### 权限要求

1. **注册表访问权限**: 读取和写入注册表项
2. **文件系统权限**: 读取配置文件和音频文件
3. **进程管理权限**: 终止其他实例进程

### 安全考虑

1. **单实例机制**: 防止多个实例同时运行
2. **异常处理**: 对所有外部调用进行异常捕获
3. **权限最小化**: 仅请求必要的系统权限

## 扩展性设计

### 可扩展功能

1. **多语言支持**: 通过资源文件实现界面本地化
2. **更多通知方式**: 可扩展支持系统通知、邮件等
3. **代理详细信息**: 可显示代理服务器地址等详细信息
4. **日志记录**: 添加操作日志和错误日志功能

### 插件架构

可通过以下方式扩展功能：

1. **通知插件**: 实现 `INotificationProvider` 接口
2. **监控插件**: 实现 `IMonitorProvider` 接口
3. **配置插件**: 扩展 `AppConfig` 类

## 性能优化

### 资源管理

1. **定时器优化**: 使用 1 秒间隔的轮询，平衡实时性和系统负载
2. **内存管理**: 及时释放语音合成器和音频播放器资源
3. **线程管理**: 使用后台线程执行耗时操作，避免阻塞 UI

### 启动优化

1. **延迟加载**: 界面元素按需加载
2. **异步初始化**: 耗时操作使用异步方式执行
3. **缓存机制**: 缓存语音角色列表等不变数据

## 故障排除

### 常见问题

1. **语音不播放**
   - 检查系统是否安装了语音合成器
   - 确认音量设置不为 0
   - 检查音频文件路径是否正确

2. **开机不自启**
   - 检查注册表项是否正确创建
   - 确认杀毒软件是否阻止了自启设置

3. **界面显示异常**
   - 检查 .NET 运行时是否正确安装
   - 确认系统支持 WPF 框架

### 日志记录

建议添加日志记录功能以便于问题诊断：

```csharp
// 可添加到关键位置
Debug.WriteLine($"Proxy state changed: {_lastProxyState} -> {intVal}");
```

## 版本历史

### v1.0.0
- 基本功能实现
- 支持文本转语音通知
- 支持自定义音频文件
- 支持开机自启
- 现代化深色主题界面

### v1.1.0
- 添加音量和语速调节
- 优化界面交互体验
- 增强错误处理机制

## 贡献指南

欢迎提交 Issue 和 Pull Request 来改进这个项目。

### 开发环境搭建

1. 安装 Visual Studio 2022 或更高版本
2. 安装 .NET 10.0 SDK
3. 克隆项目代码
4. 使用 NuGet 还原依赖包

### 编码规范

遵循 Microsoft C# 编码规范，使用以下约定：

1. 使用 PascalCase 命名公共成员
2. 使用 camelCase 命名私有成员
3. 添加必要的 XML 文档注释
4. 保持代码简洁和可读性

## 许可证

[在此处添加许可证信息]