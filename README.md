# 控制中心 / Control Center

一台台式机 + 一台笔记本共用一台显示器的场景下，把「切信号源、切投影、切音频输出、睡眠/关机」这几件每天要做很多次的事，收进一个窗口里一次提交。

单文件 C# WinForms，全部界面 GDI+ 自绘，用 Windows 自带的 `csc.exe` 就能编译，不装任何 SDK。

- 体积：编译后约 60 KB，无外部运行时依赖（.NET Framework 4.x 是 Windows 内置的）
- 明暗两套色板，自动跟随系统；整个界面按 DPI 真实缩放，高分屏不发虚
- 无安装、无注册表写入（开机自启仅写当前用户的 `Run` 键），拷到别的 Windows 机器直接跑

## 界面

顶栏右侧四个按钮：刷新 / 明暗主题 / 收到托盘 / 关闭。按住顶栏任意位置可拖动窗口。

**左列**

| 区块 | 说明 |
| --- | --- |
| ① 常用组合 | 一次性动作，点一下自动帮你选好下面各项。默认两条：笔电接管 = 信号源切到 HDMI-1 + 本机睡眠；主机接管 = 切到 DisplayPort + 睡眠。按完再去改别的选项不受它影响 |
| ② 显示器信号源 | DDC/CI 切换输入口：不切换 / 自动切换 / 各物理入口。启动时会读取显示器当前输入口并打一个「当前」标记——只是标记，不会自动选中，避免提交时把没打算改的信号源也重写 |
| ③ 投影方式 | 等同 `Win + P`：不更改 / 扩展 / 复制 / 仅电脑 / 仅第二屏 |

**右列**

| 区块 | 说明 |
| --- | --- |
| ④ 声音输出设备 | 列表里「当前默认」是系统正在用的设备；选别的会出现「待应用」，提交时同时设为默认设备 + 默认通信设备。音量条和静音同理。只有真正动过的项才会执行 |
| ⑤ 本机电源 | 不操作 / 睡眠 / 关机 / 重启。关机重启会先弹确认框，执行后预留 15 秒可撤销 |

底部：开机自启开关、重置、应用设置，下面一行状态提示（灰=空闲，绿=成功，黄=注意，红=失败，悬停看完整内容）。

`Esc` = 缩到托盘；点 `×` 同样是缩到托盘，右键托盘图标可打开 / 刷新 / 退出。

## 构建

```bat
build.bat
```

等价于：

```bat
%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe ^
  -nologo -target:winexe -out:"Control Center.exe" ^
  -r:System.Windows.Forms.dll -r:System.Drawing.dll ControlCenter.cs
```

产物是当前目录下的 `Control Center.exe`，双击即可运行。

## 依赖：winddcutil.exe

DDC/CI 部分调用 [scottaxcell/winddcutil](https://github.com/scottaxcell/winddcutil)。这个 7.6 MB 的二进制**已经随仓库一起分发**（MIT 许可，许可证全文与来源、校验值见 `third-party/winddcutil-LICENSE.txt`），clone 下来就能直接用，不用另外下载。

程序按 `Application.StartupPath` 找它，所以它必须和 `Control Center.exe` 在同一目录。

万一它缺失，其余功能照常：投影、音频、电源不受影响，只有信号源区块降级为不可用，底部状态行提示「未找到 winddcutil.exe，信号源切换不可用」。

## 信号源代码值

不同型号显示器的 VCP 输入源编码不一样，需要在显示器 OSD 里核对后用。程序里预置的切换命令是：

```
winddcutil setvcp 1 0x60 <代码>
```

常见取值：`0x0F` = DisplayPort，`0x11` = HDMI1，`0x12` = HDMI2。用 `winddcutil getvcp 1 0x60` 可以读回当前输入口验证。改这几处就在 `ControlCenter.cs` 里搜 `0x0F` / `0x11`。

## 常见问题

- **点了没反应 / 显示器黑屏**：先去显示器 OSD 菜单确认 DDC/CI 已开启（一般在 系统设置 / Setup 里）。最坏情况按显示器背后的物理按键切回。
- **切换无效**：DDC/CI 命令只能沿「当前正在显示的那根信号线」发给显示器。台式机在显示时只能在台式机上切；笔记本在显示时只能在笔记本上切。所以两台机器各放一份程序。
- **声音列表为空**：确认 `Windows Audio`（Audiosrv）服务在运行。
- **程序打不开**：同目录会留下 `crash.log`，里面是具体异常。

## 技术实现

| 功能 | 做法 |
| --- | --- |
| 界面 | 全部 GDI+ 自绘：卡片 / 胶囊选项 / 开关 / 滑块 / 拥有者绘制列表 / 线性图标。色板由 `Theme.Set(bool dark)` 提供，尺寸统一走 `Theme.S()` 按 DPI 缩放 |
| 明暗跟随 | 读 `HKCU\...\Themes\Personalize\AppsUseLightTheme` |
| 信号源 | `winddcutil`（DDC/CI over VCP） |
| 投影 | `EnumDisplaySettings` + `ChangeDisplaySettingsEx`，多屏用 `CDS_NORESET` 批量后统一提交 |
| 声音 | Core Audio COM：`MMDeviceEnumerator` / `IPropertyStore` / `IAudioEndpointVolume`；设默认设备用未公开但长期稳定的 `IPolicyConfig`（CLSID `{870AF99C-171D-4F9E-AF0D-E63DF40C2BC9}`） |
| 电源 | `SetSuspendState`；`shutdown.exe /s|/r /t 15`，撤销等同 `shutdown /a` |
| 窗口 | 无边框 + `CS_DROPSHADOW` 投影 + 圆角 Region，顶栏拖拽走 `WM_NCLBUTTONDOWN / HTCAPTION` |

## 调试参数

```bat
Control Center.exe --diag
```
把音频枚举结果写到 `audio-diag.txt`。

```bat
Control Center.exe --shot out.png dark
```
把界面渲染成 PNG（不需要屏幕可见，远程验收 / 无头环境下看效果用）。第二个参数省略则按当前系统主题。

## 许可

未指定。作者自用工具，随意取用。
