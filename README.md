# 奶猴桌宠

一个面向 Windows 10/11 的 3D 卡通小猴桌面宠物。程序以透明置顶窗口运行，会把可见应用窗口的边缘当作游乐设施，让小猴在桌面和窗口边缘之间移动、攀爬和休息。

## 下载运行

在 [Releases](https://github.com/winter646258/naihou-desktop-pet/releases) 下载最新的 `MonkeyPet.exe`，双击即可运行。

这是 .NET 9 的 Windows 自包含单文件发布版，目标平台为 `win-x64`，普通用户不需要另外安装 .NET Runtime。

## 当前功能

- 8×6 动画图集，共 48 个动画帧。
- 待机、侧爬、垂直攀爬、倒挂、跳跃和睡觉动作。
- 枚举可见 Windows 窗口，随机选择窗口上、下、左、右边缘作为移动目标。
- 识别真正覆盖显示器的全屏窗口并暂时隐藏，退出全屏后自动恢复。
- 左键拖拽小猴，右键播放随机片段猴叫；播放期间不会叠加多个声音。
- 系统托盘菜单：显示/隐藏、暂停动作、设置和退出。
- 设置猴子大小、位置、音量、静音、开机启动和启动后最小化到托盘。
- 没有图集时自动回退到三视图 PNG，不会因素材缺失而崩溃。

## 操作

| 操作 | 效果 |
| --- | --- |
| 左键拖拽 | 移动小猴并保存位置 |
| 右键 | 播放猴叫 |
| 托盘双击 | 显示或隐藏小猴 |
| 托盘右键 | 打开完整控制菜单 |

## 构建发布

开发环境需要：

- Windows 10/11
- .NET 9 SDK
- `win-x64` Windows SDK 组件

在仓库根目录执行：

```powershell
dotnet build .\MonkeyPet.csproj
dotnet publish .\MonkeyPet.csproj `
  -c Release `
  -r win-x64 `
  --self-contained true `
  -p:PublishSingleFile=true `
  -p:IncludeNativeLibrariesForSelfExtract=true `
  -o .\publish
```

输出文件为 `publish\MonkeyPet.exe`。

## 动画素材

程序使用 `Assets/monkey-atlas.png` 和 `Assets/monkey-actions.json`。图集规格为 8 列 × 6 行，每格 512×512：

| 行 | 动作 | 默认帧率 |
| ---: | --- | ---: |
| 0 | idle | 4 fps |
| 1 | crawl | 8 fps |
| 2 | climb | 8 fps |
| 3 | hang | 5 fps |
| 4 | jump | 10 fps |
| 5 | sleep | 2 fps |

原始 4×2 动作图、透明化素材、生成提示词和确定性合成脚本位于 `Assets/Source` 与 `tools/build_animation_atlas.py`。修改源图后，可重新生成图集和联系表：

```powershell
python .\tools\build_animation_atlas.py `
  --source-dir .\Assets\Source\transparent `
  --output .\Assets\monkey-atlas.png `
  --contact-sheet .\animation-qa\monkey-atlas-contact.jpg
```

## 隐私与权限

- 不联网，不上传截图、窗口标题、Cookie、密码或 API Key。
- 只使用 Win32 窗口枚举获取位置和可见性，用于规划移动边缘。
- API Key 仅用于开发阶段生成图片，不会写入源码、EXE 或运行时配置。
- 音频和角色素材随程序本地打包。

## 许可说明

当前仓库未附带 `LICENSE` 文件。除非另行取得授权，代码、角色图片和音频的著作权仍归原作者或相应权利人所有；如需商用或再分发，请先补充明确的开源许可证和素材授权说明。

## 项目地址

<https://github.com/winter646258/naihou-desktop-pet>
