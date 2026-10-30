# Sound Connect for Windows

Windows 11 x64 本机控制 Sony WH-1000XM6 的桌面工具。

通过耳机本机的 LE Tandem 控制通道工作，不使用云服务，不上传设备数据。

## 下载

Windows x64 自包含版本请在 [Releases](https://github.com/ThomasOffice/Sound-Connect-for-Windows/releases) 下载：

```text
Xm6Control-1.0.0-win-x64.zip
```

解压后运行：

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
- **多点连接**：开启 / 关闭
- **自动关机**：取下耳机后 / 关闭
- **系统托盘**：概览、聆听模式、退出
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

## 说明

- 本项目是个人用途的互操作性实现，不是 Sony 官方产品。
- 不使用账号、云同步、遥测或广告。
- 不包含固件更新或恢复功能。
- 协议实现基于本机设备行为与公开的 Tandem 控制消息结构，未包含 Sony 的反编译源码、资源或固件。
- 设备功能会受到耳机固件、当前连接状态和 Windows 蓝牙栈的影响。
