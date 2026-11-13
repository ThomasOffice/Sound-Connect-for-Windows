# Sound Connect for Windows

Windows 11 x64 本机控制 Sony WH-1000XM6 的桌面工具。

通过耳机本机的 LE Tandem 控制通道工作，不使用云服务，不上传设备数据。

## 下载

Windows x64 安装版或便携版请在 [Releases](https://github.com/ThomasOffice/Sound-Connect-for-Windows/releases) 下载：

```text
Xm6Control-1.0.3-win-x64-setup.exe   安装版（所有用户，需要管理员）
Xm6Control-1.0.3-win-x64.zip         便携版（解压即用）
```

安装版会装到 `C:\Program Files\Sound Connect for Windows`，创建开始菜单快捷方式，并可从“应用和功能”卸载。卸载会保留 `%LOCALAPPDATA%\Xm6Control` 下的设置和情景。

便携版解压后运行：

```text
Xm6Control.App.exe
```

自包含版本已包含 .NET 与 Windows App SDK 运行时，不需要另外安装 .NET。

## 功能

- **环境声音控制**：降噪、环境声、关闭
- **自动环境声音**：标准 / 高灵敏度 / 低灵敏度
- **关注语音**
- **均衡器**：内置预设、自定义 10 段频段
- **聆听模式**：标准、背景音乐（我的房间 / 客厅 / 咖啡馆）、电影
- **DSEE**：读取当前效果并切换自动 / 关（协议不提供强制开启）
- **编解码器**：概览中只读显示当前 SBC / AAC / LDAC / aptX / LC3
- **多点连接**：开启 / 关闭
- **自动关机**：取下耳机后 / 关闭
- **情景**：把环境声音、均衡器和聆听模式组合保存，一键应用
- **设置**：开机自启、启动最小化、窗口记忆、主题与背景、中文 / English、托盘行为、自动刷新与重连、目标设备
- **系统托盘**：概览、环境声音、均衡器、聆听模式、退出；提示中显示电量和当前模式
- **关闭行为**：直接关闭应用或隐藏到系统托盘
- **电量与固件**：只读显示

## 系统要求

- Windows 11 x64
- 支持 Bluetooth LE 的蓝牙适配器
- 已与 Windows 配对的 Sony WH-1000XM6
- 耳机与 Windows 的音频连接处于可用状态

## 构建

需要 .NET 10 SDK。

```powershell
dotnet build Xm6Control.slnx -c Release
dotnet test
```

发布自包含版本：

```powershell
dotnet publish src/Xm6Control.App/Xm6Control.App.csproj `
  -c Release -r win-x64 --self-contained true `
  -p:DebugType=None -p:DebugSymbols=false `
  -o artifacts/publish/win-x64
```

发布时会自动移除应用不会加载的 Windows App SDK AI/ML 与 Widgets 组件，以及中英文之外的语言资源：解压后约 168 MB，压缩后约 67 MB。

生成便携 zip 和 exe 安装包（需要 Inno Setup 6）：

```powershell
pwsh packaging/build-installer.ps1
```

脚本会输出到 `artifacts/release`：

```text
Xm6Control-1.0.3-win-x64.zip
Xm6Control-1.0.3-win-x64-setup.exe
SHA256SUMS.txt
```

## 说明

- 本项目是个人用途的互操作性实现，不是 Sony 官方产品。
- 不使用账号、云同步、遥测或广告。
- 不包含固件更新或恢复功能。
- 协议实现基于本机设备行为与公开的 Tandem 控制消息结构，未包含 Sony 的反编译源码、资源或固件。
- 设备功能会受到耳机固件、当前连接状态和 Windows 蓝牙栈的影响。
