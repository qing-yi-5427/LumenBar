# LumenBar 0.9.1 · WinUI 3

Windows 11 桌面余额胶囊，支持 Codex、DeepSeek、Xiaomi MiMo、OpenRouter。界面使用 **WinUI 3**，悬浮胶囊调用 **Windows 原生 Desktop Acrylic**，设置页使用不透明纯色背景。任务栏余额横条已移除，提供系统托盘入口。

## 运行

打开 `dist/LumenBar-WinUI3-win-x64/LumenBar.exe`。这是包含 .NET 与 Windows App SDK 的 x64 便携目录，请保留整个文件夹；不需要安装开发环境。旧 `dist/LumenBar-win-x64` 是旧版，不是本次交付。

- 按住鼠标左键约 0.2 秒后拖动胶囊，松手即停止；单击不会进入移动模式。在设置「悬浮窗」调整宽高。自动在纵向、横向、网格间排布，文字随尺寸缩放，不出现滚动条。窗口比例受内容约束，避免大量留白。
- 拖动结束、修改尺寸和退出时自动记住实际尺寸与位置；重启恢复原来的显示器位置。如果显示器已断开，将窗口移回可见区域。显示项目数量变化时仍会紧凑重排。
- 双击、右键或 **Ctrl + Alt + S** 打开设置。
- 设置重做为顶部四页导航：额度总览、账户连接、悬浮窗、偏好设置。总览显示额度卡片、剩余比例与服务商开关；悬浮窗页提供当前账户数据的即时预览。使用 WinUI 3 标准控件、中性背景和系统强调色。可选择服务商、账户、浅色 / 深色、悬浮窗宽高和刷新间隔。
- 有效更改自动保存并立即反映到悬浮窗，没有保存按钮；账户输入停顿 650 毫秒或离开输入框时自动加密保存。调整外观不会清空已经读取的余额。
- 在设置或右键菜单锁定，禁止拖动、缩放和点击，鼠标穿透。**Ctrl + Alt + L** 解锁。
- 快捷键被占用或找不到窗口时，**再次运行程序**会解锁并打开设置。
- 单击系统托盘中的 LumenBar 图标打开设置；右键提供设置、刷新、锁定 / 解锁和退出。悬停显示当前额度。关闭设置窗口后，托盘和悬浮窗继续运行；选择「退出 LumenBar」完全退出。
- 在「偏好设置 → 开机自启」开启后，登录 Windows 时自动显示悬浮窗，不弹出设置页。默认关闭，切换立即生效，仅作用于当前用户，不需要管理员权限。自启使用当前程序的完整路径，建议先把便携目录放到固定位置。
- 托盘图标由 Windows 管理，首次运行可能放在隐藏图标区域；可在系统的任务栏设置中调整可见性。

## 图标、托盘与开机自启

0.9.0 新增蓝绿色胶囊图标，已嵌入 EXE、窗口标题栏及托盘。源图为 `src/LumenBar.WinUI/Assets/LumenBar.png`，Windows 图标为同目录 `LumenBar.ico`，包含 16–256 像素的九种尺寸；生成提示词见 `design/icon-prompt.txt`。

托盘调用 Windows 原生 `Shell_NotifyIcon`，支持键盘激活、Explorer 重启后的重新注册和退出清理。参考 [Microsoft 通知区域文档](https://learn.microsoft.com/en-us/windows/win32/shell/notification-area)。

开机自启使用 `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` 下的 `LumenBar` 值，保存带引号的完整路径及 `--startup` 参数；关闭后删除该值，不修改其他启动项。参考 [Microsoft Run 注册项文档](https://learn.microsoft.com/en-us/windows/win32/setupapi/run-and-runonce-registry-keys)。诊断在隔离的非启动测试键验证写入、读取与删除，不更改用户的真实启动项；尚未以注销 / 重启系统验证登录启动。

## 胶囊与进度

悬浮窗有三种样式：胶囊填充、细进度条、仅数字。样式改变会立即作用于预览和桌面胶囊，并自动保存；默认保留胶囊填充。设置预览采用不透明背景，桌面胶囊使用系统 Acrylic。

Codex 分为 **Codex · 5h** 和 **Codex · 周** 两颗胶囊，独立显示剩余百分比与底色填充。严格按接口返回的时间窗口和账户额度组识别；**接口未返回 5h 时隐藏该胶囊并重新紧凑排布；返回 0% 时仍显示。** 周额度缺失时显示 `—`，不推测为满额。不使用 Spark 等其他额度组代替 Codex 主额度。

DeepSeek 显示第一种返回币种；MiMo 显示总余额；OpenRouter 显示 USD 余额。现金没有固定额度上限，因此默认只显示金额。可在「进度底色」填写参考金额：余额 60、参考金额 100，底色填充 60%。超过参考金额显示满格，数字保留真实值；不同币种分别保存。查询失败显示 `—`。

各窗口重置时间、其他币种、充值 / 赠送余额和错误详情放在「账户连接」，悬浮窗只保留名称与数值。

## 账户

| 服务商 | 连接方式 |
| --- | --- |
| Codex | 复用本机 Codex 的 ChatGPT 登录；自动寻找 codex.exe，也可填写路径。API Key 登录通常不提供订阅额度 |
| DeepSeek | 官方 API Key |
| MiMo | 控制台 Cookie；普通模型 API Key 不能读取控制台余额 |
| OpenRouter | 可读取账户 Credits 的 API Key |

MiMo：登录余额页 → F12 → 网络 → 刷新 → `balance` 请求 → 复制单行 Cookie 请求头。需要包含 `api-platform_serviceToken` 和 `userId`。失效后更新 Cookie。该控制台接口可能随平台变化。

API Key / Cookie 通过 Windows DPAPI 当前用户加密，保存于 `%LOCALAPPDATA%/LumenBar/settings.json`，上次配置备份为 `.bak`。迁移保留已有账户。请求只发送到固定 HTTPS 服务商地址，不跟随重定向，不记录凭据或响应原文；Codex 登录续期交给本地官方 CLI。

## Windows 原生玻璃

每颗胶囊使用原生 `SystemBackdropElement` + `DesktopAcrylicBackdrop`；窗口底层保持透明。玻璃与前景分别在自己的合成层绘制平滑圆角；不再使用 `SetWindowRgn` 圆形硬裁切，也不让玻璃继承前景的圆角裁切。设置页使用系统窗口圆角。胶囊文字已放大，名称与数字之间保留间距；长金额自动缩小以适配。已移除 LiquidGlassWinUI 着色器、桌面采样、静态壁纸模拟以及录屏排除设置。设置窗口不加载玻璃、模糊或背景采样。

0.8.1 补齐 HWND 底层透明处理：配置 DWM 客户区合成，并在 `WM_ERASEBKGND` 清除默认白色底图。检查通过实际窗口的擦背景消息，将内存中的白色测试位图清为 RGBA 零，覆盖浅色、深色、锁定与解锁。该检查不捕获桌面，也不等同于最终桌面观感验证。

0.8.2 显式为胶囊圆角裁切及玻璃合成层启用 `CompositionBorderMode.Soft`，由同一个外轮廓裁切玻璃与进度填充，移除玻璃内部重复的圆角裁切。保留原生 Acrylic 与窗口底层透明处理。像素检查覆盖默认、宽、网格、窄布局在 100%、150%、200% 渲染比例下的进度边缘，验证存在连续半透明过渡；原生 Acrylic 本身仍需桌面目视验收。参考 [Microsoft 的边缘模式说明](https://learn.microsoft.com/en-us/uwp/api/windows.ui.composition.compositionbordermode) 与 [SystemBackdropElement 实现](https://github.com/microsoft/microsoft-ui-xaml/blob/main/controls/dev/SystemBackdropElement/SystemBackdropElement.cpp)。

0.8.3 修正 0.8.2 只覆盖前景的问题：原生背景连接另有 [`ExternalBackdropBorderMode`](https://learn.microsoft.com/en-us/windows/windows-app-sdk/api/winrt/microsoft.ui.content.contentexternalbackdroplink.externalbackdropbordermode?view=windows-app-sdk-2.0-experimental)，实际读到的默认值是 Hard。现在短暂连接配置这一属性，再交回内置 `DesktopAcrylicBackdrop` 管理主题、焦点及系统策略；运行时读回确认是 Soft。该接口未包含在稳定版 C# 投影中，使用 QueryInterface 检查后调用，未引入实验版运行库。玻璃使用自己的 CornerRadius，前景使用独立圆角，避免跨合成器的祖先裁切。背景连接在界面线程上关闭，避免回收时发生原生输入资源崩溃。诊断覆盖移除、重新加载和强制回收；离屏像素检查仍不能代替桌面玻璃观感验收。

Windows 会根据系统透明效果、节能和辅助功能等策略决定 Acrylic 的实际呈现及纯色回退。旧版「跟随壁纸」设置自动转为「跟随系统」；已有账户和窗口位置保留。

## 源码和构建

- `src/LumenBar.WinUI`：当前界面和入口。
- `src/LumenBar.Core`：共享账户查询、额度解析、配置加密与排版计算。
- `src/LumenBar`：保留的 WPF 参考实现，不由默认构建脚本发布。

```powershell
dotnet run --project tests/LumenBar.Checks -c Release
./build.ps1
```

默认脚本发布 WinUI 3 便携目录。开发构建：

```powershell
dotnet build src/LumenBar.WinUI/LumenBar.WinUI.csproj -c Release
```

`LumenBar.exe --demo` 显示示例数据；`--probe` 自动检查原生 Acrylic 组件、胶囊排布、5h 隐藏 / 零额度显示、锁定样式、不透明设置页以及主题 / 宽度 / 服务商即时修改，然后退出。程序旁的 `previews/checks.txt` 保存结果；PNG 是应用内部布局导出，不是桌面截图，也不能证明 DWM 玻璃的实际观感。

已通过 24 项数据 / 安全 / 排版 / 拖动状态 / 位置持久化检查、WinUI 3 启动及上述界面检查。窗口诊断从隔离设置文件重新读取并恢复实际 HWND 尺寸和位置，验证显示器断开后的可见区域回退；不改写真实账户配置。拖动处理包含松手、取消、失去捕获和锁定，参考 [Microsoft 指针捕获文档](https://learn.microsoft.com/en-us/windows/windows-app-sdk/api/winrt/microsoft.ui.xaml.uielement.pointercapturelost)。实际鼠标拖动手感、玻璃观感和鼠标穿透仍需桌面交互验收。Codex 连接此前已验证；其他服务商需要真实凭据验证。

## 参考与许可

- [Codex Quota](https://apps.microsoft.com/detail/9p7rcfxcb468?hl=zh-CN&gl=CN)：参考其商店介绍中的紧凑读数、系统强调色及 Segoe UI Variable 字体方向；本程序保留余额胶囊和纯色设置页。
- [CodexBar](https://github.com/steipete/codexbar)
- [Microsoft：System Backdrops](https://learn.microsoft.com/en-us/windows/apps/develop/ui/system-backdrops)
- [WinUIEx TransparentTintBackdrop](https://github.com/dotMorten/WinUIEx/blob/main/src/WinUIEx/TransparentTintBackdrop.cs)：参考其 HWND 透明背景处理，MIT，见 `THIRD_PARTY_NOTICES.txt`。
- [Microsoft：XAML 主题资源](https://learn.microsoft.com/windows/apps/design/style/xaml-theme-resources)
- [Microsoft：Typography](https://learn.microsoft.com/windows/apps/design/signature-experiences/typography)
- [Codex app-server](https://learn.chatgpt.com/docs/app-server)、[DeepSeek 余额](https://api-docs.deepseek.com/api/get-user-balance/)、[OpenRouter Credits](https://openrouter.ai/docs/api/api-reference/credits/get-remaining-credits)
