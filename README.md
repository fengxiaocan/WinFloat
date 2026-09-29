# WinFloat

WinFloat 是一个面向 Windows 10/11 x64 的轻量性能悬浮监控工具，按需求文档实现“看一眼就知道电脑当前状态”的 MVP。

## 已实现

- 下载 / 上传速度：读取 Windows 网卡累计字节数，自动选择当前有实际流量的物理网卡，最近 3 次采样做轻量平均。
- CPU 总使用率：使用 `GetSystemTimes` 计算整机负载。
- 内存总使用率：使用 `GlobalMemoryStatusEx`。
- GPU 使用率、CPU/GPU 温度：通过 LibreHardwareMonitorLib 独立读取；传感器不可用时只隐藏对应项目，不影响其他指标。
- 纵向 / 横向布局、标准 / 极简模式、主题、透明度、字体大小、圆角、负载颜色提示。
- Always On Top、无任务栏图标、拖动、位置保存、多显示器边界修正、鼠标穿透、锁定位置。
- 系统托盘菜单、用户级开机启动、监控项目开关、网卡/GPU 选择、500/1000/2000ms 刷新率。
- 配置写入 `%LOCALAPPDATA%\WinFloat\config.json`；仅在用户修改、拖动结束或退出时保存，不持续写盘。

## 项目结构

```text
App.xaml(.cs)                  应用生命周期、依赖组装
Models/                        配置模型和监控快照
Services/                      采集、配置、托盘、开机启动、Win32 适配
Views/MainWindow.xaml(.cs)     悬浮窗和展示逻辑
Views/SettingsWindow.xaml(.cs) 设置窗口
Assets/                        应用图标 PNG 源图与多尺寸 ICO
app.manifest                   asInvoker + PerMonitorV2 DPI 声明
```

## 构建

需要 .NET 8 SDK 和 Windows Desktop Runtime：

```powershell
dotnet restore
dotnet build -c Release
```

发布 x64 绿色版：

```powershell
dotnet publish -c Release -r win-x64 --self-contained false `
  -p:PublishSingleFile=true `
  -p:IncludeNativeLibrariesForSelfExtract=true `
  -o artifacts/win-x64
```

程序不要求管理员权限。部分主板或显卡温度传感器可能受驱动权限限制，读取不到时会自动隐藏温度。

- 应用图标：`Assets/WinFloat.ico`（16/32/48/64/128/256 多尺寸）和 `Assets/WinFloat.png` 源图，桌面图标与系统托盘共用。

## 打包 EXE 安装包

项目根目录下提供了自动化打包脚本，可一键完成编译、发布与 Inno Setup 安装包打包生成。

### 1. 前置准备

- **.NET 8 SDK**：用于编译项目源码。
- **Inno Setup 6**：用于打包生成 Windows 标准 EXE 安装程序。可以通过以下命令快速安装：
  ```powershell
  winget install JRSoftware.InnoSetup
  ```
  *（也可前往 [Inno Setup 官网](https://jrsoftware.org/isdl.php) 手动下载安装）*

### 2. 一键打包

在项目根目录下，执行以下任一方式：

- **PowerShell 命令行**：
  ```powershell
  .\build-installer.ps1
  ```
- **双击运行批处理**：
  直接双击根目录下的 `build-installer.bat`。

打包成功后，输出的安装包位于 `artifacts/` 目录下，文件格式形如：
`artifacts/WinFloat-Setup-v1.0.0.exe`（采用 LZMA2 极限压缩，体积仅约 4 MB）。

### 3. 高级打包参数

`build-installer.ps1` 支持以下常用参数：

| 参数 | 说明 | 示例 |
| --- | --- | --- |
| `-SelfContained` | 打包独立运行库（目标电脑无需预装 .NET 8 运行时，体积约 40~60 MB） | `.\build-installer.ps1 -SelfContained` |
| `-Version <版本号>` | 自定义安装包版本（默认自动读取 `WinFloat.csproj` 中的 `<Version>`） | `.\build-installer.ps1 -Version "1.0.1"` |
| `-Configuration` | 构建配置，默认为 `Release` | `.\build-installer.ps1 -Configuration Debug` |
| `-SkipPublish` | 跳过 `dotnet publish` 步骤，直接使用已有构建产物打包 | `.\build-installer.ps1 -SkipPublish` |

### 4. 安装包特性

- **语言支持**：原生内置简体中文与英文安装界面。
- **安装权限**：优先用户级免提权安装（默认路径 `%LOCALAPPDATA%\Programs\WinFloat`），同时支持 UAC 提权安装给整机所有用户。
- **快捷方式与自启**：支持自定义勾选创建桌面快捷方式、开始菜单快捷方式及开机启动项。
- **运行中进程感知**：安装、更新或卸载时会自动检测并优雅退出正在运行的 WinFloat 实例，避免文件占用冲突。
- **完整卸载支持**：自动注册至 Windows 系统“应用和功能”及控制面板，卸载时干净清理注册表自启项与程序文件。

## 依赖说明

温度和 GPU 传感器使用 `LibreHardwareMonitorLib 0.9.6`。该库为 MPL-2.0，发布安装包或绿色版时应随包保留相应第三方许可证与声明。
