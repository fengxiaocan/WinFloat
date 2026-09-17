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

## 依赖说明

温度和 GPU 传感器使用 `LibreHardwareMonitorLib 0.9.6`。该库为 MPL-2.0，发布安装包或绿色版时应随包保留相应第三方许可证与声明。
