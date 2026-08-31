# 奶猴桌宠

Windows 10/11 WPF 桌面宠物。猴子悬浮于桌面并依据顶层窗口边缘活动，支持透明区域鼠标穿透、拖拽、托盘控制、全屏自动隐藏和右键叫声。

## 功能

- 优先沿当前前台窗口或最近可见窗口的边缘活动。
- 水平边缘使用侧身朝向，垂直边缘使用攀附朝向与动作节奏。
- 左键拖拽，右键播放不叠加的随机约两秒叫声音频。
- 托盘菜单可显示/隐藏、暂停、打开设置或退出。
- 设置保存猴子大小、位置、音量、静音和启动偏好。

## 构建

需要 Windows 与 .NET 9 SDK：

```powershell
dotnet publish .\MonkeyPet.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o .\publish
```

生成的单文件程序位于 `publish\MonkeyPet.exe`。

## 资源说明

`Assets` 中的角色 PNG 与 `monkey_call.mp3` 是程序构建所需的私有资源。请勿将它们单独再发布或用于未经授权的用途。
# 奶猴桌宠

这是 Windows 10/11 的 C# / WPF 桌面宠物。程序使用透明置顶窗口、Win32 窗口枚举、托盘设置和本地音频。

## 当前动画支持

运行端已支持数据驱动的 8x6 动作图集：停留、侧爬、攀爬、倒挂、跳跃、睡觉。将 `Assets/monkey-atlas.png` 放入程序目录后会自动启用；没有图集时继续使用三视图回退模式。

图集尺寸、行定义和生成要求见 `Assets/ANIMATION_ASSETS.md`。

## 生成动作素材

动作素材生成脚本使用 Image API CLI。先在 Windows 当前用户环境变量中配置 `OPENAI_API_KEY`，再按图集规范生成并放置 `monkey-atlas.png`。密钥不会写入程序配置。

## 构建

```powershell
dotnet build .\MonkeyPet.csproj
dotnet publish .\MonkeyPet.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o .\publish
```
