# ProxyMonitor 代理监控器

ProxyMonitor 是一个用于监控 Windows 系统代理设置变化并提供语音通知的 WPF 应用程序。当系统代理开启或关闭时，它会通过文本转语音 (TTS) 或预设的音频文件进行通知。

## 功能特性

- **实时监控**: 持续监控 Windows 系统代理设置变化
- **语音通知**: 支持文本转语音 (TTS) 和 WAV 音频文件两种通知方式
- **自定义配置**: 可配置开启/关闭时的语音内容、音量、语速等参数
- **开机自启**: 支持设置为开机自动启动（静默运行）
- **单实例**: 确保同一时间只有一个实例在运行

## 技术架构

### 项目结构
```
ProxyMonitor/
├── Services/
│   ├── ConfigService.cs      # 配置管理服务
│   └── MonitorService.cs     # 代理监控服务
├── MainWindow.xaml          # 主界面布局
├── MainWindow.xaml.cs       # 主界面逻辑
├── App.xaml                 # 应用程序入口
├── App.xaml.cs              # 应用程序启动逻辑
└── ProxyMonitor.csproj      # 项目配置文件
```

### 核心组件

1. **ConfigService**: 负责应用程序配置的加载和保存
2. **MonitorService**: 监控系统代理设置变化并触发通知
3. **MainWindow**: 提供用户界面用于配置参数
4. **App**: 应用程序入口，处理单实例逻辑和启动参数

## 使用说明

### 安装与运行

1. 确保系统已安装 .NET 10.0 运行时
2. 编译项目或直接运行生成的可执行文件
3. 应用程序首次运行时会创建默认配置文件 `settings.json`

### 界面操作

#### 语音内容设置
- **使用音频文件**: 切换开关启用音频文件模式
- **开启语音内容**: 系统代理开启时播放的文本内容
- **关闭语音内容**: 系统代理关闭时播放的文本内容
- **开启音频文件**: 系统代理开启时播放的 WAV 音频文件
- **关闭音频文件**: 系统代理关闭时播放的 WAV 音频文件

#### 语音角色设置
- **语音角色**: 从系统已安装的语音角色中选择
- **试听**: 测试当前选择的语音角色和设置
- **音量**: 调整语音播放音量 (0-100)
- **语速**: 调整语音播放速度 (-10 到 10)

#### 系统设置
- **开机自动启动**: 设置应用程序是否开机自动启动（静默运行）

### 配置文件

应用程序使用 `settings.json` 文件存储配置信息：

```json
{
  "TtsText": "系统代理开启",
  "TtsTextDisabled": "系统代理关闭",
  "Volume": 60,
  "Rate": 0,
  "VoiceName": "Microsoft Tracy",
  "AudioFileEnabled": "path/to/开启.wav",
  "AudioFileDisabled": "path/to/关闭.wav",
  "UseAudioFile": true,
  "RunOnStartup": true
}
```

### 启动参数

- **无参数**: 正常启动，显示配置界面
- **--silent**: 静默启动，不显示配置界面，直接在后台运行

## 工作原理

1. **应用程序启动**:
   - 检查并终止其他实例
   - 加载配置文件
   - 初始化监控服务
   - 根据启动参数决定是否显示界面

2. **代理监控**:
   - 每秒检查一次注册表项 `HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Internet Settings\ProxyEnable`
   - 检测到状态变化时触发相应通知

3. **语音通知**:
   - 根据配置选择使用音频文件或 TTS
   - 音频文件模式: 播放指定的 WAV 文件
   - TTS 模式: 使用系统语音合成器朗读配置的文本

## 开发环境

- **开发语言**: C# 10.0
- **框架**: .NET 10.0 Windows
- **UI 框架**: WPF (Windows Presentation Foundation)
- **依赖库**: System.Speech (用于语音合成)

## 注意事项

1. 应用程序需要访问 Windows 注册表以监控代理设置
2. 使用音频文件时仅支持 WAV 格式
3. 静默运行后可通过任务管理器结束进程来退出程序
4. 开机自启功能会将启动项添加到注册表 `HKEY_CURRENT_USER\SOFTWARE\Microsoft\Windows\CurrentVersion\Run`

## 许可证

[在此处添加许可证信息]