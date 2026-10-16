// ============================================================
//  控制中心 Control Center  ·  Qoder 设计语言
//  单文件 C# / WinForms / .NET Framework 4.0（系统自带 csc 编译）
//
//  视觉：跟随系统的明暗双主题、矢量线性图标、发丝级描边、单一签名强调色
//  交互：所有功能先「选项」，最后点右下角【应用设置】统一执行
//    ① 常用组合    ② 显示器信号源（DDC/CI）   ③ 投影方式
//    ④ 声音输出设备 + 音量 / 静音              ⑤ 本机电源
//
//  编译：
//    csc.exe -nologo -target:winexe -out:"Control Center.exe" ControlCenter.cs
// ============================================================
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

namespace ControlCenter
{
    // ==================== 设计令牌 ====================
    static class Theme
    {
        public static float Scale = 1f;      // 系统 DPI 缩放（1.0 / 1.25 / 1.5 …）
        public static bool Dark = true;

        public static Color Window, Card, CardBorder, Inner, Chip, ChipHover, ChipBorder,
                            Accent, AccentHover, AccentSoft, Danger, DangerSoft, Warn,
                            Text, TextDim, TextFaint, Ok, Hover;

        public static Font FTitle, FCard, FBody, FBodyB, FCap, FSmall;

        public static int S(int v) { return (int)Math.Round(v * Scale); }
        public static float S(float v) { return v * Scale; }

        public static Rectangle R(int x, int y, int w, int h)
        {
            return new Rectangle(S(x), S(y), S(w), S(h));
        }

        public static Color A(int alpha, Color c)
        {
            return Color.FromArgb(alpha, c.R, c.G, c.B);
        }

        // 带 alpha 的颜色不能赋给 Control.BackColor（GDI+ 不支持窗口半透明），赋值前先压平
        public static Color Opaque(Color c, Color over)
        {
            if (c.A == 255) return c;
            float t = c.A / 255f;
            return Color.FromArgb(
                (int)(over.R + (c.R - over.R) * t),
                (int)(over.G + (c.G - over.G) * t),
                (int)(over.B + (c.B - over.B) * t));
        }

        public static Color Blend(Color a, Color b, float t)
        {
            return Color.FromArgb(
                (int)(a.R + (b.R - a.R) * t),
                (int)(a.G + (b.G - a.G) * t),
                (int)(a.B + (b.B - a.B) * t));
        }

        public static void InitFonts()
        {
            string fam = "Microsoft YaHei UI";
            FTitle = new Font(fam, 10.1f, FontStyle.Bold);   // 13.5px
            FCard = new Font(fam, 9.8f, FontStyle.Bold);     // 13px
            FBody = new Font(fam, 9.4f, FontStyle.Regular);  // 12.5px
            FBodyB = new Font(fam, 9.4f, FontStyle.Bold);
            FCap = new Font(fam, 8.3f, FontStyle.Regular);   // 11px
            FSmall = new Font(fam, 7.9f, FontStyle.Bold);    // 10.5px
        }

        public static void Set(bool dark)
        {
            Dark = dark;
            if (dark)
            {
                Window = Color.FromArgb(22, 24, 26);
                Card = Color.FromArgb(30, 33, 36);
                CardBorder = Color.FromArgb(46, 50, 55);
                Inner = Color.FromArgb(25, 27, 30);
                Chip = Color.FromArgb(38, 41, 46);
                ChipHover = Color.FromArgb(46, 50, 56);
                ChipBorder = Color.FromArgb(54, 58, 64);
                Accent = Color.FromArgb(59, 196, 140);
                AccentHover = Color.FromArgb(84, 214, 163);
                Danger = Color.FromArgb(229, 100, 94);
                Warn = Color.FromArgb(214, 164, 86);
                Text = Color.FromArgb(233, 236, 239);
                TextDim = Color.FromArgb(155, 164, 171);
                TextFaint = Color.FromArgb(111, 120, 127);
            }
            else
            {
                Window = Color.FromArgb(245, 246, 245);
                Card = Color.FromArgb(255, 255, 255);
                CardBorder = Color.FromArgb(230, 232, 230);
                Inner = Color.FromArgb(250, 251, 250);
                Chip = Color.FromArgb(243, 244, 243);
                ChipHover = Color.FromArgb(236, 238, 236);
                ChipBorder = Color.FromArgb(225, 228, 225);
                Accent = Color.FromArgb(27, 152, 88);
                AccentHover = Color.FromArgb(21, 130, 75);
                Danger = Color.FromArgb(199, 58, 58);
                Warn = Color.FromArgb(176, 122, 32);
                Text = Color.FromArgb(22, 25, 28);
                TextDim = Color.FromArgb(90, 98, 102);
                TextFaint = Color.FromArgb(139, 147, 152);
            }
            AccentSoft = Blend(Card, Accent, dark ? 0.17f : 0.10f);
            DangerSoft = Blend(Card, Danger, dark ? 0.17f : 0.10f);
            Ok = Accent;
            Hover = Blend(Card, Text, dark ? 0.10f : 0.06f);
        }

        public static bool SystemIsDark()
        {
            try
            {
                using (RegistryKey k = Registry.CurrentUser.OpenSubKey(
                    "Software\\Microsoft\\Windows\\CurrentVersion\\Themes\\Personalize"))
                {
                    if (k != null)
                    {
                        object v = k.GetValue("AppsUseLightTheme");
                        if (v is int) return (int)v == 0;
                    }
                }
            }
            catch { }
            return true;
        }
    }

    static class Util
    {
        public static GraphicsPath Round(Rectangle r, int radius)
        {
            GraphicsPath p = new GraphicsPath();
            int d = radius * 2;
            if (d <= 0 || d > r.Width || d > r.Height)
            {
                d = Math.Min(d, Math.Min(r.Width, r.Height));
                if (d <= 0) { p.AddRectangle(r); p.CloseFigure(); return p; }
            }
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        public static GraphicsPath RoundF(RectangleF r, float radius)
        {
            GraphicsPath p = new GraphicsPath();
            float d = radius * 2;
            if (d <= 0) { p.AddRectangle(r); p.CloseFigure(); return p; }
            if (d > r.Width) d = r.Width;
            if (d > r.Height) d = r.Height;
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        public static void Prep(Graphics g)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        }

        public static int W(Graphics g, string s, Font f)
        {
            if (string.IsNullOrEmpty(s)) return 0;
            Size sz = TextRenderer.MeasureText(g, s, f, new Size(4000, 4000),
                TextFormatFlags.NoPrefix);
            return sz.Width;
        }

        public static void Text(Graphics g, string s, Font f, Color c, Rectangle r)
        {
            TextRenderer.DrawText(g, s, f, r, c,
                TextFormatFlags.VerticalCenter | TextFormatFlags.Left |
                TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
        }

        public static void Text(Graphics g, string s, Font f, Color c, Rectangle r, ContentAlignment align)
        {
            TextFormatFlags fl = TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix;
            if (align == ContentAlignment.MiddleRight || align == ContentAlignment.TopRight || align == ContentAlignment.BottomRight)
                fl |= TextFormatFlags.Right;
            else if (align == ContentAlignment.MiddleCenter || align == ContentAlignment.TopCenter || align == ContentAlignment.BottomCenter)
                fl |= TextFormatFlags.HorizontalCenter;
            else
                fl |= TextFormatFlags.Left;
            TextRenderer.DrawText(g, s, f, r, c, fl);
        }

        public static void TextRight(Graphics g, string s, Font f, Color c, Rectangle r)
        {
            TextRenderer.DrawText(g, s, f, r, c,
                TextFormatFlags.VerticalCenter | TextFormatFlags.Right |
                TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
        }

        public static void TextCenter(Graphics g, string s, Font f, Color c, Rectangle r)
        {
            TextRenderer.DrawText(g, s, f, r, c,
                TextFormatFlags.VerticalCenter | TextFormatFlags.HorizontalCenter |
                TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
        }

        // 药丸标签：返回它占用的宽度
        public static int Pill(Graphics g, string s, Font f, Color fg, Color bg,
                               int right, int cy, int h)
        {
            int w = W(g, s, f) + Theme.S(12);
            Rectangle r = new Rectangle(right - w, cy - h / 2, w, h);
            using (GraphicsPath p = Round(r, h / 2))
            using (SolidBrush b = new SolidBrush(bg))
                g.FillPath(b, p);
            TextCenter(g, s, f, fg, r);
            return w;
        }
    }

    // ==================== 线性图标（20×20 设计栅格） ====================
    static class Gly
    {
        public static void Draw(Graphics g, string name, Rectangle box, Color c, float strokePx)
        {
            GraphicsState st = g.Save();
            float s = box.Width / 20f;
            g.TranslateTransform(box.X, box.Y);
            g.ScaleTransform(s, s);
            float sw = strokePx / s;

            if (name == "bolt")
            {
                GraphicsPath fill = new GraphicsPath();
                PointF[] pts = new PointF[] {
                    new PointF(11.6f, 1.4f), new PointF(4.4f, 11.2f),
                    new PointF(9.2f, 11.2f), new PointF(8.4f, 18.6f),
                    new PointF(15.6f, 8.6f), new PointF(10.8f, 8.6f) };
                fill.AddPolygon(pts);
                using (SolidBrush b = new SolidBrush(c)) g.FillPath(b, fill);
                fill.Dispose();
                g.Restore(st);
                return;
            }
            if (name == "speaker" || name == "mute")
            {
                GraphicsPath sp = new GraphicsPath();
                PointF[] pts = new PointF[] {
                    new PointF(2.6f, 7.6f), new PointF(6.4f, 7.6f),
                    new PointF(11f, 3.2f), new PointF(11f, 16.8f),
                    new PointF(6.4f, 12.4f), new PointF(2.6f, 12.4f) };
                sp.AddPolygon(pts);
                using (SolidBrush b = new SolidBrush(c)) g.FillPath(b, sp);
                sp.Dispose();
                if (name == "speaker")
                {
                    using (Pen p = new Pen(c, sw))
                    {
                        p.StartCap = LineCap.Round; p.EndCap = LineCap.Round;
                        g.DrawArc(p, 12.6f, 6.6f, 5f, 6.8f, -58f, 116f);
                        g.DrawArc(p, 14.4f, 4.2f, 8f, 11.6f, -50f, 100f);
                    }
                }
                else
                {
                    using (Pen p = new Pen(c, sw * 1.15f))
                    {
                        p.StartCap = LineCap.Round; p.EndCap = LineCap.Round;
                        g.DrawLine(p, 13.4f, 6.4f, 17.6f, 13.6f);
                        g.DrawLine(p, 17.6f, 6.4f, 13.4f, 13.6f);
                    }
                }
                g.Restore(st);
                return;
            }

            using (Pen p = new Pen(c, sw))
            {
                p.StartCap = LineCap.Round;
                p.EndCap = LineCap.Round;
                p.LineJoin = LineJoin.Round;

                if (name == "monitor")
                {
                    using (GraphicsPath b = Util.RoundF(new RectangleF(1.8f, 3.2f, 16.4f, 11f), 2f)) g.DrawPath(p, b);
                    g.DrawLine(p, 10f, 14.2f, 10f, 17.2f);
                    g.DrawLine(p, 6.2f, 17.6f, 13.8f, 17.6f);
                }
                else if (name == "project")
                {
                    using (GraphicsPath b = Util.RoundF(new RectangleF(1.4f, 3.2f, 11.4f, 8.4f), 1.8f)) g.DrawPath(p, b);
                    using (GraphicsPath b = Util.RoundF(new RectangleF(7.2f, 8.4f, 11.4f, 8.4f), 1.8f)) g.DrawPath(p, b);
                }
                else if (name == "headphone")
                {
                    g.DrawArc(p, 3.6f, 4.2f, 12.8f, 12.8f, 180f, 180f);
                    using (GraphicsPath b = Util.RoundF(new RectangleF(1.6f, 10.2f, 4.6f, 7.2f), 2.2f)) g.DrawPath(p, b);
                    using (GraphicsPath b = Util.RoundF(new RectangleF(13.8f, 10.2f, 4.6f, 7.2f), 2.2f)) g.DrawPath(p, b);
                }
                else if (name == "power")
                {
                    g.DrawArc(p, 3.6f, 4f, 12.8f, 12.8f, 62f, 296f);
                    g.DrawLine(p, 10f, 2.2f, 10f, 9.6f);
                }
                else if (name == "check")
                {
                    g.DrawLines(p, new PointF[] { new PointF(4f, 10.4f), new PointF(8.2f, 14.4f), new PointF(16f, 5.4f) });
                }
                else if (name == "refresh")
                {
                    g.DrawArc(p, 3.6f, 3.6f, 12.8f, 12.8f, 55f, 250f);
                    g.DrawLines(p, new PointF[] { new PointF(15.6f, 4.4f), new PointF(16.6f, 8.4f), new PointF(12.4f, 8.8f) });
                }
                else if (name == "theme")
                {
                    g.DrawEllipse(p, 3.6f, 3.6f, 12.8f, 12.8f);
                    using (SolidBrush b = new SolidBrush(c))
                        g.FillPie(b, 3.6f, 3.6f, 12.8f, 12.8f, 270f, 180f);
                }
                else if (name == "clock")
                {
                    g.DrawEllipse(p, 3.2f, 3.2f, 13.6f, 13.6f);
                    g.DrawLines(p, new PointF[] { new PointF(10f, 6.4f), new PointF(10f, 10.2f), new PointF(13f, 11.8f) });
                }
                else if (name == "undo")
                {
                    g.DrawArc(p, 4f, 4.6f, 12f, 11.4f, 300f, 240f);
                    g.DrawLines(p, new PointF[] { new PointF(3.4f, 4.6f), new PointF(4.2f, 8.8f), new PointF(8.2f, 7.6f) });
                }
                else if (name == "minus")
                {
                    g.DrawLine(p, 5f, 10f, 15f, 10f);
                }
                else if (name == "close")
                {
                    g.DrawLine(p, 5.4f, 5.4f, 14.6f, 14.6f);
                    g.DrawLine(p, 14.6f, 5.4f, 5.4f, 14.6f);
                }
                else if (name == "dot")
                {
                    using (SolidBrush b = new SolidBrush(c)) g.FillEllipse(b, 7f, 7f, 6f, 6f);
                }
            }
            g.Restore(st);
        }
    }

    // ==================== Win32 ====================
    static class Native
    {
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct DISPLAY_DEVICE
        {
            public int cb;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string DeviceName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string DeviceString;
            public int StateFlags;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string DeviceID;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string DeviceKey;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct DEVMODE
        {
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string dmDeviceName;
            public short dmSpecVersion, dmDriverVersion;
            public short dmSize, dmDriverExtra;
            public int dmFields;
            public int dmPositionX, dmPositionY, dmDisplayOrientation, dmDisplayFixedOutput;
            public short dmColor, dmDuplex, dmYResolution, dmTTOption, dmCollate;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string dmFormName;
            public short dmLogPixels;
            public int dmBitsPerPel;
            public int dmPelsWidth, dmPelsHeight;
            public int dmDisplayFlags;
            public int dmDisplayFrequency;
            public int dmICMMethod, dmICMIntent, dmMediaType, dmDitherType;
            public int dmReserved1, dmReserved2, dmPanningWidth, dmPanningHeight;
        }

        public const int ENUM_CURRENT_SETTINGS = 0;
        public const int CDS_UPDATEREGISTRY = 0x01;
        public const int CDS_NORESET = 0x100000;
        public const int CDS_SET_PRIMARY = 0x10;
        public const int DISP_CHANGE_SUCCESSFUL = 0;
        public const int DISPLAY_DEVICE_ACTIVE = 0x01;
        public const int DISPLAY_DEVICE_PRIMARY_DEVICE = 0x04;
        public const int DM_POSITION = 0x00000020;

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern bool EnumDisplayDevices(string lpDevice, int iDevNum, ref DISPLAY_DEVICE lpdd, int dwFlags);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern bool EnumDisplaySettings(string name, int modeNum, ref DEVMODE dm);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern int ChangeDisplaySettings(ref DEVMODE dm, int flags);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern int ChangeDisplaySettingsEx(string name, ref DEVMODE dm, IntPtr hwnd, int flags, IntPtr param);

        [DllImport("powrprof.dll")]
        public static extern bool SetSuspendState(bool hibernate, bool critical, bool disableWakeEvent);

        [DllImport("user32.dll")]
        public static extern bool ReleaseCapture();
        [DllImport("user32.dll")]
        public static extern int SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);
        [DllImport("dwmapi.dll")]
        public static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);
        [DllImport("user32.dll")]
        public static extern bool SetLayeredWindowAttributes(IntPtr hwnd, int crKey, byte bAlpha, int dwFlags);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern IntPtr FindWindow(string lpClassName, string lpWindowName);
        [DllImport("user32.dll")]
        public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
        [DllImport("user32.dll")]
        public static extern bool SetForegroundWindow(IntPtr hWnd);
        [DllImport("user32.dll")]
        public static extern bool SetProcessDPIAware();
        [DllImport("user32.dll")]
        public static extern uint GetDpiForSystem();
        [DllImport("user32.dll")]
        public static extern bool PrintWindow(IntPtr hwnd, IntPtr hdcBlt, int nFlags);
    }

    // ==================== 信号源 DDC/CI ====================
    static class Ddc
    {
        public const int DP = 0x0F, HDMI1 = 0x11, HDMI2 = 0x12;

        public static string ExePath
        {
            get { return System.IO.Path.Combine(Application.StartupPath, "winddcutil.exe"); }
        }
        public static bool Available { get { return System.IO.File.Exists(ExePath); } }

        static string Run(string args)
        {
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo(ExePath, args);
                psi.UseShellExecute = false;
                psi.CreateNoWindow = true;
                psi.RedirectStandardOutput = true;
                psi.RedirectStandardError = true;
                Process p = Process.Start(psi);
                string o = p.StandardOutput.ReadToEnd();
                string e2 = p.StandardError.ReadToEnd();
                p.WaitForExit(5000);
                return (o + e2).Trim();
            }
            catch (Exception e) { return "ERROR " + e.Message; }
        }

        public static int GetCurrentInput()
        {
            string s = Run("getvcp 1 0x60");
            System.Text.RegularExpressions.Match m =
                System.Text.RegularExpressions.Regex.Match(s, @"VCP\s*0x60\s*(\d+)");
            if (m.Success) return int.Parse(m.Groups[1].Value);
            return -1;
        }

        public static bool SetInput(int code)
        {
            string s = Run("setvcp 1 0x60 0x" + code.ToString("X2"));
            return !s.StartsWith("ERROR") && !s.Contains("Failed");
        }

        public static string Name(int code)
        {
            if (code == DP) return "DisplayPort";
            if (code == HDMI1) return "HDMI-1";
            if (code == HDMI2) return "HDMI-2";
            return "未知";
        }
    }

    // ==================== Core Audio ====================
    [Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
    [ComImport]
    class MMDeviceEnumeratorComObject { }

    [Guid("886d8eeb-8cf2-4446-8d02-cdba1dbdcf99")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [ComImport]
    interface IPropertyStore
    {
        int GetCount(out int cProps);
        int GetAt(int iProp, out PropertyKey key);
        int GetValue(ref PropertyKey key, out PropVariant pv);
        int SetValue(ref PropertyKey key, ref PropVariant value);
        int Commit();
    }

    // PROPERTYKEY = GUID fmtid + ULONGLONG pid（x64 下共 24 字节）
    [StructLayout(LayoutKind.Sequential)]
    struct PropertyKey
    {
        public Guid fmtid;
        public ulong pid;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct PropVariant
    {
        public ushort vt;
        public ushort w1, w2, w3;
        public IntPtr p;
        public int r1, r2;
        public string AsString()
        {
            return (vt == 31 && p != IntPtr.Zero) ? Marshal.PtrToStringUni(p) : "";
        }
    }

    [Guid("D666063F-1587-4E43-81F1-B948E807363F")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [ComImport]
    interface IMMDevice
    {
        int Activate(ref Guid iid, int clsCtx, IntPtr extra, [MarshalAs(UnmanagedType.IUnknown)] out object iface);
        int OpenPropertyStore(int access, out IPropertyStore props);
        int GetId([MarshalAs(UnmanagedType.LPWStr)] out string id);
        int GetState(out int state);
    }

    [Guid("0BD7A1BE-7A1A-44DB-8397-CC5392387B5E")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [ComImport]
    interface IMMDeviceCollection
    {
        int GetCount(out int count);
        int Item(int index, out IMMDevice device);
    }

    [Guid("A95664D2-9614-4F35-A746-DE8DB63617E6")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [ComImport]
    interface IMMDeviceEnumerator
    {
        int EnumAudioEndpoints(int dataFlow, int stateMask, out IMMDeviceCollection devices);
        int GetDefaultAudioEndpoint(int dataFlow, int role, out IMMDevice endpoint);
        int GetDevice([MarshalAs(UnmanagedType.LPWStr)] string id, out IMMDevice device);
        int RegisterEndpointNotificationCallback(IntPtr client);
        int UnregisterEndpointNotificationCallback(IntPtr client);
    }

    [Guid("5CDF2C82-841E-4546-9722-0CF74078229A")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [ComImport]
    interface IAudioEndpointVolume
    {
        int RegisterControlChangeNotify(IntPtr cb);
        int UnregisterControlChangeNotify(IntPtr cb);
        int GetChannelCount(out int count);
        int SetMasterVolumeLevel(float db, ref Guid iid);
        int SetMasterVolumeLevelScalar(float s, ref Guid iid);
        int GetMasterVolumeLevel(out float db);
        int GetMasterVolumeLevelScalar(out float s);
        int SetChannelVolumeLevel(uint ch, float db, ref Guid iid);
        int SetChannelVolumeLevelScalar(uint ch, float s, ref Guid iid);
        int GetChannelVolumeLevel(uint ch, out float db);
        int GetChannelVolumeLevelScalar(uint ch, out float s);
        int SetMute([MarshalAs(UnmanagedType.Bool)] bool m, ref Guid iid);
        int GetMute(out bool m);
    }

    [Guid("F8679F50-850A-41CF-9C72-430F290290C8")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [ComImport]
    interface IPolicyConfig
    {
        [PreserveSig] int GetMixFormat([MarshalAs(UnmanagedType.LPWStr)] string id, out IntPtr p);
        [PreserveSig] int GetDeviceFormat([MarshalAs(UnmanagedType.LPWStr)] string id, int def, out IntPtr p);
        [PreserveSig] int ResetDeviceFormat([MarshalAs(UnmanagedType.LPWStr)] string id);
        [PreserveSig] int SetDeviceFormat([MarshalAs(UnmanagedType.LPWStr)] string id, IntPtr a, IntPtr b);
        [PreserveSig] int GetProcessingPeriod([MarshalAs(UnmanagedType.LPWStr)] string id, out int p);
        [PreserveSig] int SetProcessingPeriod([MarshalAs(UnmanagedType.LPWStr)] string id, int period);
        [PreserveSig] int GetShareMode([MarshalAs(UnmanagedType.LPWStr)] string id, out IntPtr p);
        [PreserveSig] int SetShareMode([MarshalAs(UnmanagedType.LPWStr)] string id, IntPtr p);
        [PreserveSig] int GetPropertyValue([MarshalAs(UnmanagedType.LPWStr)] string id, ref PropertyKey k, out PropVariant v);
        [PreserveSig] int SetPropertyValue([MarshalAs(UnmanagedType.LPWStr)] string id, ref PropertyKey k, ref PropVariant v);
        [PreserveSig] int SetDefaultEndpoint([MarshalAs(UnmanagedType.LPWStr)] string id, int role);
        [PreserveSig] int SetEndpointVisibility([MarshalAs(UnmanagedType.LPWStr)] string id, int visible);
    }

    static class AudioApi
    {
        public class Endpoint
        {
            public string Id;
            public string Name;
            public bool IsDefault;
            public override string ToString() { return Name; }
        }

        static readonly Guid IID_Volume = new Guid("5CDF2C82-841E-4546-9722-0CF74078229A");
        static readonly Guid CLSID_Policy = new Guid("870AF99C-171D-4F9E-AF0D-E63DF40C2BC9");
        static readonly Guid GUID_System = new Guid("00000000-0000-0000-C000-000000000046");
        static readonly Guid FMTID_DevProp = new Guid("a45c254e-df1c-4efd-8020-67d146a850e0");
        const ulong PID_DEVICE_FRIENDLY = 14;
        const ulong PID_INTERFACE_FRIENDLY = 2;

        static IMMDeviceEnumerator En()
        {
            return (IMMDeviceEnumerator)(object)new MMDeviceEnumeratorComObject();
        }

        public static string Diagnose()
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            try
            {
                IMMDeviceEnumerator en = En();
                sb.AppendLine("cast ok");
                IMMDevice def;
                int hr = en.GetDefaultAudioEndpoint(0, 0, out def);
                sb.AppendLine("GetDefault hr=" + hr + " null=" + (def == null));
                string defId = null;
                if (def != null) { def.GetId(out defId); sb.AppendLine("defaultId=" + defId); }
                IMMDeviceCollection coll;
                int h2 = en.EnumAudioEndpoints(0, 1, out coll);
                sb.AppendLine("Enum hr=" + h2 + " collNull=" + (coll == null));
                if (coll != null)
                {
                    int n; coll.GetCount(out n);
                    sb.AppendLine("n=" + n);
                    for (int i = 0; i < n; i++)
                    {
                        IMMDevice d;
                        if (coll.Item(i, out d) != 0 || d == null) { sb.AppendLine("item" + i + " fail"); continue; }
                        string id; d.GetId(out id);
                        int st; d.GetState(out st);
                        sb.AppendLine("[" + i + "] " + ReadName(d, "?") + " state=" + st + (id == defId ? " <=DEFAULT" : ""));
                        IAudioEndpointVolume v = Vol(d);
                        if (v != null)
                        {
                            float s; bool m;
                            v.GetMasterVolumeLevelScalar(out s);
                            v.GetMute(out m);
                            sb.AppendLine("      vol=" + s + " mute=" + m);
                        }
                        else sb.AppendLine("      vol activate failed");
                    }
                }
            }
            catch (Exception e) { sb.AppendLine("EX " + e); }
            return sb.ToString();
        }

        public static List<Endpoint> List()
        {
            List<Endpoint> list = new List<Endpoint>();
            try
            {
                IMMDeviceEnumerator en = En();
                string defId = null;
                IMMDevice def;
                if (en.GetDefaultAudioEndpoint(0, 0, out def) == 0 && def != null) def.GetId(out defId);

                IMMDeviceCollection coll;
                if (en.EnumAudioEndpoints(0, 1, out coll) != 0 || coll == null) return list;
                int n; coll.GetCount(out n);
                for (int i = 0; i < n; i++)
                {
                    IMMDevice d;
                    if (coll.Item(i, out d) != 0 || d == null) continue;
                    string id; d.GetId(out id);
                    Endpoint ep = new Endpoint();
                    ep.Id = id;
                    ep.Name = ReadName(d, id);
                    ep.IsDefault = (defId != null && defId == id);
                    list.Add(ep);
                }
            }
            catch { }
            return list;
        }

        static string ReadName(IMMDevice dev, string fallback)
        {
            try
            {
                IPropertyStore ps;
                if (dev.OpenPropertyStore(0, out ps) != 0 || ps == null) return fallback;
                int n; ps.GetCount(out n);
                string friendly = null, sep = null;
                for (int i = 0; i < n; i++)
                {
                    PropertyKey k; PropVariant v;
                    if (ps.GetAt(i, out k) != 0) continue;
                    if (!k.fmtid.Equals(FMTID_DevProp)) continue;
                    if (ps.GetValue(ref k, out v) != 0) continue;
                    string s = v.AsString();
                    if (s == "") continue;
                    if (k.pid == PID_DEVICE_FRIENDLY) friendly = s;
                    else if (k.pid == PID_INTERFACE_FRIENDLY && sep == null) sep = s;
                }
                if (friendly != null) return friendly;
                if (sep != null) return sep;
            }
            catch { }
            return fallback;
        }

        public static bool SetDefault(Endpoint ep)
        {
            if (ep == null || ep.Id == null) return false;
            try
            {
                Type t = Type.GetTypeFromCLSID(CLSID_Policy);
                if (t == null) return false;
                object o = Activator.CreateInstance(t);
                IPolicyConfig pc = o as IPolicyConfig;
                if (pc == null) { Marshal.FinalReleaseComObject(o); return false; }
                int r0 = pc.SetDefaultEndpoint(ep.Id, 0); // eConsole
                int r1 = pc.SetDefaultEndpoint(ep.Id, 1); // eMultimedia
                Marshal.FinalReleaseComObject(o);
                return r0 == 0 || r1 == 0;
            }
            catch { return false; }
        }

        static IAudioEndpointVolume Vol(Endpoint ep)
        {
            try
            {
                IMMDevice d;
                if (En().GetDevice(ep.Id, out d) != 0 || d == null) return null;
                return Vol(d);
            }
            catch { return null; }
        }

        static IAudioEndpointVolume Vol(IMMDevice d)
        {
            try
            {
                if (d == null) return null;
                object o;
                Guid iidVol = IID_Volume;
                if (d.Activate(ref iidVol, 1, IntPtr.Zero, out o) != 0) return null;
                return (IAudioEndpointVolume)o;
            }
            catch { return null; }
        }

        public static bool GetVolume(Endpoint ep, out float scalar, out bool mute)
        {
            scalar = 0; mute = false;
            IAudioEndpointVolume v = Vol(ep);
            if (v == null) return false;
            v.GetMasterVolumeLevelScalar(out scalar);
            v.GetMute(out mute);
            return true;
        }

        public static bool SetVolume(Endpoint ep, float scalar)
        {
            IAudioEndpointVolume v = Vol(ep);
            if (v == null) return false;
            Guid g = GUID_System;
            return v.SetMasterVolumeLevelScalar(scalar, ref g) == 0;
        }

        public static bool SetMute(Endpoint ep, bool mute)
        {
            IAudioEndpointVolume v = Vol(ep);
            if (v == null) return false;
            Guid g = GUID_System;
            return v.SetMute(mute, ref g) == 0;
        }
    }

    // ==================== 投影方式 ====================
    static class Projection
    {
        public enum Mode { Extend, Duplicate, PCOnly, SecondOnly }

        public static List<Native.DISPLAY_DEVICE> Active()
        {
            List<Native.DISPLAY_DEVICE> all = new List<Native.DISPLAY_DEVICE>();
            List<string> seen = new List<string>();
            Native.DISPLAY_DEVICE adapter = new Native.DISPLAY_DEVICE();
            adapter.cb = Marshal.SizeOf(typeof(Native.DISPLAY_DEVICE));
            for (int a = 0; Native.EnumDisplayDevices(null, a, ref adapter, 0); a++)
            {
                Native.DISPLAY_DEVICE mon = new Native.DISPLAY_DEVICE();
                mon.cb = Marshal.SizeOf(typeof(Native.DISPLAY_DEVICE));
                for (int m = 0; Native.EnumDisplayDevices(adapter.DeviceName, m, ref mon, 0); m++)
                {
                    if ((mon.StateFlags & Native.DISPLAY_DEVICE_ACTIVE) != 0 && !seen.Contains(mon.DeviceName))
                    {
                        seen.Add(mon.DeviceName);
                        all.Add(mon);
                    }
                    mon = new Native.DISPLAY_DEVICE();
                    mon.cb = Marshal.SizeOf(typeof(Native.DISPLAY_DEVICE));
                }
                adapter = new Native.DISPLAY_DEVICE();
                adapter.cb = Marshal.SizeOf(typeof(Native.DISPLAY_DEVICE));
            }
            return all;
        }

        public static string Label(Mode m)
        {
            if (m == Mode.Extend) return "扩展";
            if (m == Mode.Duplicate) return "复制";
            if (m == Mode.PCOnly) return "仅电脑屏幕";
            return "仅第二屏幕";
        }

        static Native.DEVMODE Cur(string name)
        {
            Native.DEVMODE dm = new Native.DEVMODE();
            dm.dmSize = (short)Marshal.SizeOf(typeof(Native.DEVMODE));
            Native.EnumDisplaySettings(name, Native.ENUM_CURRENT_SETTINGS, ref dm);
            return dm;
        }

        static void Off(ref Native.DEVMODE dm)
        {
            Native.DEVMODE baseDm = Cur(null);
            dm.dmFields = Native.DM_POSITION;
            dm.dmPositionX = 0; dm.dmPositionY = 0;
            dm.dmPelsWidth = 0; dm.dmPelsHeight = 0;
            dm.dmBitsPerPel = baseDm.dmBitsPerPel;
            dm.dmDisplayFrequency = baseDm.dmDisplayFrequency;
        }

        public static string Apply(Mode mode)
        {
            List<Native.DISPLAY_DEVICE> mons = Active();
            if (mons.Count < 2)
                return "仅 " + mons.Count + " 个活动显示器，投影方式无需更改";

            Native.DEVMODE main = Cur(null);
            int pri = 0;
            for (int i = 0; i < mons.Count; i++)
                if ((mons[i].StateFlags & Native.DISPLAY_DEVICE_PRIMARY_DEVICE) != 0) pri = i;
            int sec = (pri == 0) ? 1 : 0;

            Native.DEVMODE[] dms = new Native.DEVMODE[mons.Count];
            int[] flags = new int[mons.Count];
            for (int i = 0; i < mons.Count; i++)
            {
                dms[i] = Cur(mons[i].DeviceName);
                dms[i].dmFields = Native.DM_POSITION;
                dms[i].dmPositionX = 0; dms[i].dmPositionY = 0;
                flags[i] = Native.CDS_UPDATEREGISTRY | Native.CDS_NORESET;
            }

            switch (mode)
            {
                case Mode.Extend:
                    dms[sec].dmPositionX = main.dmPelsWidth;
                    break;
                case Mode.Duplicate:
                    break;
                case Mode.PCOnly:
                    Off(ref dms[sec]);
                    break;
                case Mode.SecondOnly:
                    flags[sec] |= Native.CDS_SET_PRIMARY;
                    Off(ref dms[pri]);
                    break;
            }

            for (int i = 0; i < mons.Count; i++)
            {
                int hr = Native.ChangeDisplaySettingsEx(mons[i].DeviceName, ref dms[i], IntPtr.Zero, flags[i], IntPtr.Zero);
                if (hr != Native.DISP_CHANGE_SUCCESSFUL)
                    return "投影「" + Label(mode) + "」失败（显示器 " + mons[i].DeviceName + " 返回 " + hr + "），可改用 Win+P";
            }
            Native.DEVMODE dummy = new Native.DEVMODE();
            dummy.dmSize = (short)Marshal.SizeOf(typeof(Native.DEVMODE));
            int hr2 = Native.ChangeDisplaySettings(ref dummy, 0);
            if (hr2 != Native.DISP_CHANGE_SUCCESSFUL)
                return "投影设置提交失败（" + hr2 + "）";
            return "投影已切换为「" + Label(mode) + "」";
        }
    }

    // ==================== 自绘控件 ====================
    class Card : Panel
    {
        public string Title = "";
        public string Note = "";
        public string Icon = "dot";
        public int Radius;
        public int ContentTop;

        public Card(string icon, string title, string note)
        {
            Icon = icon; Title = title; Note = note;
            Radius = Theme.S(12);
            ContentTop = Theme.S(68);
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint
                     | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        }

        protected override void OnSizeChanged(EventArgs e)
        {
            base.OnSizeChanged(e);
            if (Width > 0 && Height > 0)
            {
                using (GraphicsPath p = Util.Round(new Rectangle(0, 0, Width, Height), Radius))
                    Region = new Region(p);
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            Util.Prep(g);
            Rectangle r = new Rectangle(0, 0, Width - 1, Height - 1);
            using (GraphicsPath p = Util.Round(r, Radius))
            using (SolidBrush b = new SolidBrush(Theme.Card))
            using (Pen pen = new Pen(Theme.CardBorder))
            {
                g.FillPath(b, p);
                g.DrawPath(pen, p);
            }
            int pad = Theme.S(16);
            int box = Theme.S(26);
            Rectangle ib = new Rectangle(pad, pad - Theme.S(2), box, box);
            using (GraphicsPath p = Util.Round(ib, Theme.S(8)))
            using (SolidBrush b = new SolidBrush(Theme.AccentSoft))
                g.FillPath(b, p);
            Gly.Draw(g, Icon, new Rectangle(ib.X + Theme.S(4), ib.Y + Theme.S(4),
                       Theme.S(18), Theme.S(18)), Theme.Accent, Theme.S(1.5f));

            int tx = ib.Right + Theme.S(10);
            Util.Text(g, Title, Theme.FCard, Theme.Text,
                new Rectangle(tx, ib.Y, Width - tx - pad, box), ContentAlignment.MiddleLeft);
            if (Note.Length > 0)
                Util.Text(g, Note, Theme.FCap, Theme.TextFaint,
                    new Rectangle(pad, ib.Bottom + Theme.S(7), Width - pad * 2, Theme.S(16)),
                    ContentAlignment.MiddleLeft);
        }
    }

    class Chip : Control
    {
        public string Key = "";
        public bool Selected;
        public bool Danger;
        public bool Current;
        public string Sub = "";
        public Action<bool> OnChange;
        bool hover, down;
        int radius;

        public Chip(string text)
        {
            Text = text;
            Key = text;
            Height = Theme.S(34);
            radius = Theme.S(9);
            Cursor = Cursors.Hand;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint
                     | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        }

        public void SetSelected(bool v)
        {
            if (Selected == v) return;
            Selected = v;
            Invalidate();
        }

        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; down = false; Invalidate(); }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;
            down = true; Invalidate();
            Selected = true;
            if (OnChange != null) OnChange(Selected);
        }

        protected override void OnMouseUp(MouseEventArgs e) { down = false; Invalidate(); }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            Util.Prep(g);
            Color accent = Danger ? Theme.Danger : Theme.Accent;
            Color soft = Danger ? Theme.DangerSoft : Theme.AccentSoft;

            Rectangle r = new Rectangle(0, 0, Width - 1, Height - 1);
            Color fill = Selected ? soft : (hover ? Theme.ChipHover : Theme.Chip);
            if (Selected && down) fill = Theme.Blend(soft, accent, 0.35f);
            Color border = Selected ? accent : Theme.ChipBorder;

            using (GraphicsPath p = Util.Round(r, radius))
            using (SolidBrush b = new SolidBrush(fill))
            {
                g.FillPath(b, p);
                using (Pen pen = new Pen(border, Selected ? Theme.S(1.5f) : 1f))
                    g.DrawPath(pen, p);
            }

            int pad = Theme.S(11);
            int right = Width - pad;

            if (Current)
            {
                int h = Theme.S(18);
                int w = Util.W(g, "当前", Theme.FSmall) + Theme.S(12);
                Rectangle pr = new Rectangle(right - w, (Height - h) / 2, w, h);
                using (GraphicsPath p = Util.Round(pr, h / 2))
                using (SolidBrush b = new SolidBrush(Theme.A(Theme.Dark ? 40 : 28, Theme.Ok)))
                    g.FillPath(b, p);
                Util.TextCenter(g, "当前", Theme.FSmall, Theme.Ok, pr);
                right -= w + Theme.S(7);
            }
            if (Selected)
            {
                int s = Theme.S(14);
                Rectangle cr = new Rectangle(right - s, (Height - s) / 2, s, s);
                Gly.Draw(g, "check", cr, accent, Theme.S(1.9f));
                right -= s + Theme.S(7);
            }
            Util.Text(g, Text, Theme.FBody, Selected ? Theme.Text : Theme.TextDim,
                new Rectangle(pad, 0, right - pad, Height), ContentAlignment.MiddleLeft);
        }
    }

    // 一次性动作按钮（图标 + 标题 + 右侧说明）
    class ActionRow : Control
    {
        public string Icon = "bolt";
        public string Sub = "";
        public Action OnClickAction;
        bool hover, down;

        public ActionRow(string text, string icon, string sub)
        {
            Text = text; Icon = icon; Sub = sub;
            Height = Theme.S(40);
            Cursor = Cursors.Hand;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint
                     | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        }

        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; down = false; Invalidate(); }
        protected override void OnMouseDown(MouseEventArgs e) { down = true; Invalidate(); }
        protected override void OnMouseUp(MouseEventArgs e)
        {
            bool inside = ClientRectangle.Contains(e.Location);
            down = false; Invalidate();
            if (inside && OnClickAction != null) OnClickAction();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            Util.Prep(g);
            Rectangle r = new Rectangle(0, 0, Width - 1, Height - 1);
            Color fill = down ? Theme.Blend(Theme.Chip, Theme.Accent, 0.18f)
                              : (hover ? Theme.Blend(Theme.Chip, Theme.Accent, 0.09f) : Theme.Chip);
            using (GraphicsPath p = Util.Round(r, Theme.S(9)))
            using (SolidBrush b = new SolidBrush(fill))
            {
                g.FillPath(b, p);
                using (Pen pen = new Pen(hover ? Theme.A(Theme.Dark ? 120 : 90, Theme.Accent) : Theme.ChipBorder))
                    g.DrawPath(pen, p);
            }
            int pad = Theme.S(11);
            int s = Theme.S(16);
            Gly.Draw(g, Icon, new Rectangle(pad, (Height - s) / 2, s, s), Theme.Accent, Theme.S(1.6f));
            int tx = pad + s + Theme.S(9);
            Util.Text(g, Text, Theme.FBodyB, Theme.Text,
                new Rectangle(tx, 0, Theme.S(90), Height), ContentAlignment.MiddleLeft);
            if (Sub.Length > 0)
                Util.TextRight(g, Sub, Theme.FCap, Theme.TextFaint,
                    new Rectangle(tx, 0, Width - pad - tx, Height));
        }
    }

    class PushButton : Control
    {
        public bool Primary;
        public bool Disabled;
        public string Icon = "";
        public Action OnClickAction;
        bool hover, down;

        public PushButton(string text)
        {
            Text = text;
            Height = Theme.S(40);
            Cursor = Cursors.Hand;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint
                     | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        }

        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; down = false; Invalidate(); }
        protected override void OnMouseDown(MouseEventArgs e) { if (!Disabled) { down = true; Invalidate(); } }
        protected override void OnMouseUp(MouseEventArgs e)
        {
            bool inside = ClientRectangle.Contains(e.Location);
            if (!Disabled && down && inside && OnClickAction != null) OnClickAction();
            down = false; Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            Util.Prep(g);
            Rectangle r = new Rectangle(0, 0, Width - 1, Height - 1);
            Color baseColor = Primary ? Theme.Accent : Theme.Chip;
            Color fill;
            if (Disabled) fill = Theme.Blend(Theme.Chip, Theme.Window, 0.45f);
            else if (down) fill = Theme.Blend(baseColor, Color.Black, 0.14f);
            else if (hover) fill = Theme.Blend(baseColor, Color.White, Primary ? 0.10f : 0.12f);
            else fill = baseColor;

            using (GraphicsPath p = Util.Round(r, Theme.S(9)))
            using (SolidBrush b = new SolidBrush(fill))
            {
                g.FillPath(b, p);
                if (!Primary)
                    using (Pen pen = new Pen(Theme.ChipBorder)) g.DrawPath(pen, p);
            }
            Color tc = Disabled ? Theme.TextFaint : (Primary ? (Theme.Dark ? Color.FromArgb(14, 22, 18) : Color.White) : Theme.Text);
            int cx = r.Width / 2;
            int s = Theme.S(15);
            int tw = Util.W(g, Text, Theme.FBodyB);
            int gap = Icon.Length > 0 ? Theme.S(7) : 0;
            int x0 = cx - (tw + gap + s) / 2;
            if (Icon.Length > 0)
            {
                Gly.Draw(g, Icon, new Rectangle(x0, (Height - s) / 2, s, s), tc, Theme.S(1.7f));
                x0 += s + gap;
            }
            Util.Text(g, Text, Theme.FBodyB, tc, new Rectangle(x0, 0, tw + Theme.S(4), Height), ContentAlignment.MiddleLeft);
        }
    }

    class Toggle : Control
    {
        public bool Checked;
        public string Label = "";
        public Action<bool> OnChange;
        bool hover;

        public Toggle(string label)
        {
            Label = label; Text = label;
            Height = Theme.S(24);
            Cursor = Cursors.Hand;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint
                     | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        }

        public void SetChecked(bool v)
        {
            if (Checked == v) return;
            Checked = v;
            Invalidate();
        }

        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); }
        protected override void OnMouseDown(MouseEventArgs e)
        {
            Checked = !Checked;
            Invalidate();
            if (OnChange != null) OnChange(Checked);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            Util.Prep(g);
            int tw = Util.W(g, Label, Theme.FBody);
            Util.Text(g, Label, Theme.FBody, Checked ? Theme.Text : Theme.TextDim,
                new Rectangle(0, 0, tw, Height), ContentAlignment.MiddleLeft);
            int sw = Theme.S(38), sh = Theme.S(22);
            Rectangle tr = new Rectangle(tw + Theme.S(9), (Height - sh) / 2, sw, sh);
            Color rail = Checked ? Theme.Accent : (hover ? Theme.ChipHover : Theme.Chip);
            using (GraphicsPath p = Util.Round(tr, sh / 2))
            using (SolidBrush b = new SolidBrush(rail))
            {
                g.FillPath(b, p);
                using (Pen pen = new Pen(Checked ? Theme.Accent : Theme.ChipBorder)) g.DrawPath(pen, p);
            }
            int kd = sh - Theme.S(4);
            Rectangle kr = new Rectangle(Checked ? tr.Right - kd - Theme.S(2) : tr.X + Theme.S(2),
                                         tr.Y + Theme.S(2), kd, kd);
            using (SolidBrush b = new SolidBrush(Checked ? (Theme.Dark ? Color.FromArgb(14, 22, 18) : Color.White) : Theme.TextDim))
                g.FillEllipse(b, kr);
        }
    }

    class IconButton : Control
    {
        public string IconName = "speaker";
        public bool Active;
        public bool Danger;
        public Action OnClickAction;
        bool hover;

        public IconButton(string icon)
        {
            IconName = icon;
            Height = Theme.S(30);
            Width = Theme.S(30);
            Cursor = Cursors.Hand;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint
                     | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        }

        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); }
        protected override void OnMouseDown(MouseEventArgs e) { if (OnClickAction != null) OnClickAction(); }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            Util.Prep(g);
            Rectangle r = new Rectangle(0, 0, Width - 1, Height - 1);
            if (hover || Active)
                using (GraphicsPath p = Util.Round(r, Theme.S(8)))
                using (SolidBrush b = new SolidBrush(hover ? Theme.Hover : Theme.AccentSoft))
                    g.FillPath(b, p);
            Color c = Active && Danger ? Theme.Danger : (Active ? Theme.Accent : Theme.TextDim);
            int s = Theme.S(17);
            Gly.Draw(g, IconName, new Rectangle((Width - s) / 2, (Height - s) / 2, s, s), c, Theme.S(1.6f));
        }
    }

    class Slider : Control
    {
        public int Minimum = 0;
        public int Maximum = 100;
        int value = 50;
        bool drag, hover;
        public Action OnChanged;

        public Slider()
        {
            Height = Theme.S(28);
            Cursor = Cursors.Hand;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint
                     | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        }

        public int Value
        {
            get { return value; }
            set { this.value = Math.Max(Minimum, Math.Min(Maximum, value)); Invalidate(); }
        }

        Rectangle Track
        {
            get { int th = Theme.S(6); return new Rectangle(Theme.S(10), Height / 2 - th / 2, Width - Theme.S(20), th); }
        }

        int PosToValue(int x)
        {
            Rectangle t = Track;
            float f = (x - t.X) / (float)t.Width;
            f = Math.Max(0f, Math.Min(1f, f));
            return Minimum + (int)Math.Round(f * (Maximum - Minimum));
        }

        protected override void OnMouseDown(MouseEventArgs e) { drag = true; Value = PosToValue(e.X); if (OnChanged != null) OnChanged(); }
        protected override void OnMouseMove(MouseEventArgs e) { if (drag) { Value = PosToValue(e.X); if (OnChanged != null) OnChanged(); } }
        protected override void OnMouseUp(MouseEventArgs e) { drag = false; }
        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            Util.Prep(g);
            Rectangle t = Track;
            float f = (value - Minimum) / (float)(Maximum - Minimum);
            using (GraphicsPath p = Util.Round(t, t.Height / 2))
            using (SolidBrush b = new SolidBrush(Theme.ChipBorder))
                g.FillPath(b, p);
            if (f > 0.004f)
            {
                Rectangle ft = new Rectangle(t.X, t.Y, Math.Max(t.Height, (int)(t.Width * f)), t.Height);
                using (GraphicsPath p = Util.Round(ft, t.Height / 2))
                using (SolidBrush b = new SolidBrush(Theme.Accent))
                    g.FillPath(b, p);
            }
            int kx = t.X + (int)(t.Width * f);
            int kr = Theme.S(hover || drag ? 9 : 8);
            Rectangle k = new Rectangle(kx - kr, Height / 2 - kr, kr * 2, kr * 2);
            using (SolidBrush sh = new SolidBrush(Theme.A(60, Color.Black)))
                g.FillEllipse(sh, k.X + Theme.S(1), k.Y + Theme.S(2), k.Width, k.Height);
            using (SolidBrush kb = new SolidBrush(Theme.Dark ? Color.FromArgb(238, 241, 243) : Color.White))
                g.FillEllipse(kb, k);
            using (Pen pen = new Pen(Theme.Accent, Theme.S(2f)))
                g.DrawEllipse(pen, k);
        }
    }

    class ListFrame : Panel
    {
        public ListFrame(Control inner)
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint
                     | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            Controls.Add(inner);
        }

        protected override void OnSizeChanged(EventArgs e)
        {
            base.OnSizeChanged(e);
            int i = Theme.S(1);
            if (Controls.Count > 0)
            {
                Control inner = Controls[0];
                inner.SetBounds(i, i, Width - i * 2, Height - i * 2);
                using (GraphicsPath p = Util.Round(new Rectangle(0, 0, inner.Width, inner.Height), Theme.S(9)))
                    inner.Region = new Region(p);
            }
            using (GraphicsPath p = Util.Round(new Rectangle(0, 0, Width, Height), Theme.S(10)))
                Region = new Region(p);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            Util.Prep(g);
            Rectangle r = new Rectangle(0, 0, Width - 1, Height - 1);
            using (GraphicsPath p = Util.Round(r, Theme.S(10)))
            using (SolidBrush b = new SolidBrush(Theme.Inner))
            using (Pen pen = new Pen(Theme.CardBorder))
            {
                g.FillPath(b, p);
                g.DrawPath(pen, p);
            }
        }
    }

    class DeviceList : ListBox
    {
        int hot = -1;
        public Func<int, string> IconOf;
        public Func<int, string[]> TailOf;

        public DeviceList()
        {
            DrawMode = DrawMode.OwnerDrawFixed;
            ItemHeight = Theme.S(34);
            BorderStyle = BorderStyle.None;
            BackColor = Theme.Inner;
            ForeColor = Theme.Text;
            IntegralHeight = false;
            Font = Theme.FBody;
            // 注意：ListBox 不能开 UserPaint，否则条目自绘会失效
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            int i = IndexFromPoint(e.Location);
            if (i != hot)
            {
                if (hot >= 0 && hot < Items.Count) Invalidate(GetItemRectangle(hot));
                hot = i;
                if (hot >= 0 && hot < Items.Count) Invalidate(GetItemRectangle(hot));
            }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (hot >= 0 && hot < Items.Count) Invalidate(GetItemRectangle(hot));
            hot = -1;
        }

        protected override void OnDrawItem(DrawItemEventArgs e)
        {
            Graphics g = e.Graphics;
            Util.Prep(g);
            using (SolidBrush bg = new SolidBrush(Theme.Inner)) g.FillRectangle(bg, e.Bounds);
            if (e.Index < 0 || e.Index >= Items.Count) return;

            bool sel = (e.State & DrawItemState.Selected) != 0;
            bool hovered = e.Index == hot;
            AudioApi.Endpoint ep = Items[e.Index] as AudioApi.Endpoint;
            string label = Items[e.Index].ToString();

            Rectangle r = new Rectangle(e.Bounds.X + Theme.S(4), e.Bounds.Y + Theme.S(2),
                                        e.Bounds.Width - Theme.S(10), e.Bounds.Height - Theme.S(4));
            if (sel || hovered)
            {
                using (GraphicsPath p = Util.Round(r, Theme.S(7)))
                using (SolidBrush b = new SolidBrush(sel ? Theme.AccentSoft : Theme.Hover))
                    g.FillPath(b, p);
                if (sel)
                    using (GraphicsPath p = Util.Round(r, Theme.S(7)))
                    using (Pen pen = new Pen(Theme.Accent, Theme.S(1.2f)))
                        g.DrawPath(pen, p);
            }

            int pad = Theme.S(10);
            int s = Theme.S(16);
            Color iconC = sel ? Theme.Accent : Theme.TextFaint;
            string icon = IconOf != null ? IconOf(e.Index) : "speaker";
            Gly.Draw(g, icon, new Rectangle(r.X + pad, r.Y + (r.Height - s) / 2, s, s), iconC, Theme.S(1.5f));

            int right = r.Right - pad;
            string tail = TailOf != null ? TailOf(e.Index)[0] : "";
            string tailKind = TailOf != null ? TailOf(e.Index)[1] : "";
            if (tail.Length > 0)
            {
                Color tc = tailKind == "pending" ? Theme.Accent : Theme.Ok;
                Color bc = Theme.A(Theme.Dark ? 40 : 26, tc);
                int h = Theme.S(18);
                int w = Util.W(g, tail, Theme.FSmall) + Theme.S(11);
                Rectangle pr = new Rectangle(right - w, r.Y + (r.Height - h) / 2, w, h);
                using (GraphicsPath p = Util.Round(pr, h / 2))
                using (SolidBrush b = new SolidBrush(bc))
                    g.FillPath(b, p);
                Util.TextCenter(g, tail, Theme.FSmall, tc, pr);
                right -= w + Theme.S(8);
            }
            Color textC = sel ? Theme.Text : (hovered ? Theme.Text : Theme.TextDim);
            Util.Text(g, label, Theme.FBody, textC,
                new Rectangle(r.X + pad + s + Theme.S(8), r.Y, right - (r.X + pad + s + Theme.S(8)), r.Height),
                ContentAlignment.MiddleLeft);
        }
    }

    class StatusLine : Control
    {
        public string Text2 = "";
        public string Kind = "idle";   // idle | ok | warn | err

        public StatusLine()
        {
            Height = Theme.S(24);
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint
                     | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw
                     | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
        }

        public void Set(string kind, string s)
        {
            Kind = kind;
            Text = s;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            Util.Prep(g);
            Color c = Kind == "ok" ? Theme.Ok
                    : Kind == "err" ? Theme.Danger
                    : Kind == "warn" ? Theme.Warn : Theme.TextFaint;
            int s = Theme.S(12);
            Gly.Draw(g, "dot", new Rectangle(0, (Height - s) / 2, s, s), c, 1f);
            Util.Text(g, Text, Theme.FCap, Kind == "idle" ? Theme.TextFaint : Theme.TextDim,
                new Rectangle(s + Theme.S(7), 0, Width - s - Theme.S(7), Height), ContentAlignment.MiddleLeft);
        }
    }

    class Header : Panel
    {
        public Action OnMinimize;
        public Action OnClose;
        public Action OnRefresh;
        public Action OnTheme;
        Rectangle refreshBtn, themeBtn, minBtn, closeBtn;
        int hoverIdx = -1;

        public Header()
        {
            Height = Theme.S(48);
            BackColor = Theme.Window;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint
                     | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        }

        protected override void OnSizeChanged(EventArgs e)
        {
            base.OnSizeChanged(e);
            int bs = Theme.S(28), gap = Theme.S(4);
            int x = Width - Theme.S(12) - bs;
            closeBtn = new Rectangle(x, Theme.S(10), bs, bs); x -= bs + gap;
            minBtn = new Rectangle(x, Theme.S(10), bs, bs); x -= bs + gap;
            themeBtn = new Rectangle(x, Theme.S(10), bs, bs); x -= bs + gap;
            refreshBtn = new Rectangle(x, Theme.S(10), bs, bs);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            int i = -1;
            if (closeBtn.Contains(e.Location)) i = 3;
            else if (minBtn.Contains(e.Location)) i = 2;
            else if (themeBtn.Contains(e.Location)) i = 1;
            else if (refreshBtn.Contains(e.Location)) i = 0;
            if (i != hoverIdx) { hoverIdx = i; Invalidate(); }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;
            if (closeBtn.Contains(e.Location)) { if (OnClose != null) OnClose(); return; }
            if (minBtn.Contains(e.Location)) { if (OnMinimize != null) OnMinimize(); return; }
            if (themeBtn.Contains(e.Location)) { if (OnTheme != null) OnTheme(); return; }
            if (refreshBtn.Contains(e.Location)) { if (OnRefresh != null) OnRefresh(); return; }
            Native.ReleaseCapture();
            Form top = FindForm();
            Native.SendMessage(top == null ? Handle : top.Handle, 0x00A1, 2, 0); // WM_NCLBUTTONDOWN / HTCAPTION
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            hoverIdx = -1;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            Util.Prep(g);
            g.Clear(Theme.Window);

            int box = Theme.S(24);
            Rectangle mark = new Rectangle(Theme.S(14), (Height - box) / 2, box, box);
            using (GraphicsPath p = Util.Round(mark, Theme.S(7)))
            using (SolidBrush b = new SolidBrush(Theme.Accent))
                g.FillPath(b, p);
            int gs = Theme.S(14);
            Gly.Draw(g, "monitor", new Rectangle(mark.X + (box - gs) / 2, mark.Y + (box - gs) / 2, gs, gs),
                     Theme.Dark ? Color.FromArgb(14, 22, 18) : Color.White, Theme.S(1.7f));

            int tx = mark.Right + Theme.S(9);
            Util.Text(g, "控制中心", Theme.FTitle, Theme.Text,
                new Rectangle(tx, 0, Theme.S(90), Height), ContentAlignment.MiddleLeft);
            tx += Util.W(g, "控制中心", Theme.FTitle) + Theme.S(10);
            using (Pen pen = new Pen(Theme.CardBorder))
                g.DrawLine(pen, tx, Height / 2 - Theme.S(7), tx, Height / 2 + Theme.S(7));
            tx += Theme.S(10);
            Util.Text(g, "显示器 · 投影 · 声音 · 电源", Theme.FCap, Theme.TextFaint,
                new Rectangle(tx, 0, Theme.S(220), Height), ContentAlignment.MiddleLeft);

            using (Pen pen = new Pen(Theme.CardBorder))
                g.DrawLine(pen, 0, Height - 1, Width, Height - 1);

            Glyph(g, refreshBtn, hoverIdx == 0, "refresh", false);
            Glyph(g, themeBtn, hoverIdx == 1, "theme", false);
            Glyph(g, minBtn, hoverIdx == 2, "minus", false);
            Glyph(g, closeBtn, hoverIdx == 3, "close", true);
        }

        void Glyph(Graphics g, Rectangle r, bool hover, string icon, bool isClose)
        {
            if (hover)
            {
                using (GraphicsPath p = Util.Round(r, Theme.S(8)))
                using (SolidBrush b = new SolidBrush(isClose ? Theme.DangerSoft : Theme.Hover))
                    g.FillPath(b, p);
            }
            Color c = hover && isClose ? Theme.Danger : (hover ? Theme.Text : Theme.TextDim);
            int s = Theme.S(15);
            Gly.Draw(g, icon, new Rectangle(r.X + (r.Width - s) / 2, r.Y + (r.Height - s) / 2, s, s), c, Theme.S(1.6f));
        }
    }

    // ==================== 主窗体 ====================
    class MainForm : Form
    {
        NotifyIcon tray;
        Icon appIcon;
        Header header;
        StatusLine status;
        ToolTip tip;
        PushButton applyBtn, resetBtn;
        Toggle autoStart;
        DeviceList lst;
        Slider slider;
        IconButton muteBtn;
        Label volVal;
        bool forceClose;

        Card cPreset, cSource, cProj, cAudio, cPower;
        List<Chip> sourceChips = new List<Chip>();
        List<Chip> projChips = new List<Chip>();
        List<Chip> powerChips = new List<Chip>();
        List<ActionRow> presetRows = new List<ActionRow>();

        bool audioTouched, volTouched;
        bool suppressEvents;
        int baseVolume = -1;
        bool baseMute;

        // 设计尺寸（96dpi 基准），运行时按 DPI 缩放
        const int DW = 760;
        const int DM = 14;
        const int DGAP = 14;
        const int DHEADER = 48;
        const int DFOOTER = 88;
        const int DPAD = 16;
        const int DCONTENT = 72;
        const int DCHIP = 34;
        const int DCHIPPAD = 8;

        int W, M, GAP, PAD, CONTENT, CHIP, CHIPPAD;

        public MainForm()
        {
            W = Theme.S(DW);
            M = Theme.S(DM);
            GAP = Theme.S(DGAP);
            PAD = Theme.S(DPAD);
            CONTENT = Theme.S(DCONTENT);
            CHIP = Theme.S(DCHIP);
            CHIPPAD = Theme.S(DCHIPPAD);

            Text = "控制中心";
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Theme.Window;
            Font = Theme.FBody;
            ShowInTaskbar = true;
            MaximizeBox = false;
            KeyPreview = true;
            appIcon = BuildIcon();
            if (appIcon != null) Icon = appIcon;

            int colW = (W - M * 2 - GAP) / 2;
            int colL = M;
            int colR = M + colW + GAP;
            int top = Theme.S(DHEADER) + M;

            int rowH = Theme.S(40);
            int hPreset = CONTENT + 2 * (rowH + CHIPPAD) - CHIPPAD + PAD;
            int hThree = CONTENT + 3 * CHIP + 2 * CHIPPAD + PAD;
            int hTwo = CONTENT + 2 * CHIP + CHIPPAD + PAD;
            int leftH = hPreset + GAP + hThree + GAP + hThree;
            int hAudio = leftH - GAP - hTwo;

            // ---------- ① 常用组合 ----------
            cPreset = new Card("bolt", "常用组合", "点一下自动选好下面的各项选项");
            cPreset.SetBounds(colL, top, colW, hPreset);
            AddPresetRow(cPreset, "笔电接管", "HDMI-1 + 睡眠", delegate { ApplyPreset("0x11"); });
            AddPresetRow(cPreset, "主机接管", "DisplayPort + 睡眠", delegate { ApplyPreset("0x0F"); });
            Controls.Add(cPreset);

            // ---------- ② 信号源 ----------
            cSource = new Card("monitor", "显示器信号源", "通过 DDC/CI 切换显示器输入口");
            cSource.SetBounds(colL, top + hPreset + GAP, colW, hThree);
            AddChips(cSource, sourceChips,
                new string[] { "不切换", "自动切换", "主机 DP", "笔电 HDMI1", "HDMI2（备用）" },
                new string[] { "none", "auto", "0x0F", "0x11", "0x12" }, 2);
            Controls.Add(cSource);

            // ---------- ③ 投影方式 ----------
            cProj = new Card("project", "投影方式", "Windows 多显示器模式，等同 Win + P");
            cProj.SetBounds(colL, top + hPreset + GAP + hThree + GAP, colW, hThree);
            AddChips(cProj, projChips,
                new string[] { "不更改", "扩展", "复制", "仅电脑", "仅第二屏" },
                new string[] { "none", "extend", "duplicate", "pconly", "secondonly" }, 2);
            Controls.Add(cProj);

            // ---------- ④ 声音输出 ----------
            cAudio = new Card("speaker", "声音输出设备", "选中即待应用，可与音量、静音一起提交");
            cAudio.SetBounds(colR, top, colW, hAudio);
            lst = new DeviceList();
            lst.IconOf = delegate(int i) { return DeviceIcon(lst.Items[i] as AudioApi.Endpoint); };
            lst.TailOf = delegate(int i)
            {
                AudioApi.Endpoint ep = lst.Items[i] as AudioApi.Endpoint;
                if (ep != null && ep.IsDefault) return new string[] { "当前默认", "ok" };
                if (audioTouched && i == lst.SelectedIndex) return new string[] { "待应用", "pending" };
                return new string[] { "", "" };
            };
            lst.SetBounds(Theme.S(1), Theme.S(1), 10, 10);
            lst.SelectedIndexChanged += delegate
            {
                if (suppressEvents) return;
                audioTouched = true;
                LoadVolume();
                lst.Invalidate();
            };
            ListFrame frame = new ListFrame(lst);
            frame.SetBounds(PAD, CONTENT, colW - PAD * 2, hAudio - CONTENT - Theme.S(52) - PAD);
            cAudio.Controls.Add(frame);

            int volY = frame.Bottom + Theme.S(12);
            muteBtn = new IconButton("speaker");
            muteBtn.SetBounds(PAD, volY, Theme.S(30), Theme.S(30));
            muteBtn.Danger = true;
            muteBtn.OnClickAction = delegate
            {
                muteBtn.Active = !muteBtn.Active;
                muteBtn.IconName = muteBtn.Active ? "mute" : "speaker";
                muteBtn.Invalidate();
                volTouched = true;
                UpdateVolLabel();
            };
            cAudio.Controls.Add(muteBtn);

            slider = new Slider();
            slider.SetBounds(PAD + Theme.S(38), volY + Theme.S(1), colW - PAD * 2 - Theme.S(38) - Theme.S(48), Theme.S(28));
            slider.OnChanged = delegate { volTouched = true; UpdateVolLabel(); };
            cAudio.Controls.Add(slider);

            volVal = new Label();
            volVal.Text = "—";
            volVal.Font = Theme.FBodyB;
            volVal.ForeColor = Theme.TextDim;
            volVal.BackColor = Color.Transparent;
            volVal.TextAlign = ContentAlignment.MiddleRight;
            volVal.SetBounds(colW - PAD - Theme.S(46), volY + Theme.S(3), Theme.S(46), Theme.S(24));
            cAudio.Controls.Add(volVal);
            Controls.Add(cAudio);

            // ---------- ⑤ 电源 ----------
            cPower = new Card("power", "本机电源", "关机 / 重启预留 15 秒，执行后可撤销");
            cPower.SetBounds(colR, top + hAudio + GAP, colW, hTwo);
            AddChips(cPower, powerChips,
                new string[] { "不操作", "睡眠", "关机", "重启" },
                new string[] { "none", "sleep", "shutdown", "restart" }, 2);
            powerChips[2].Danger = true;
            powerChips[3].Danger = true;
            Controls.Add(cPower);

            // ---------- 底部操作栏 ----------
            int bottomY = top + leftH + M;
            Panel bar = new Panel();
            bar.SetBounds(0, bottomY, W, Theme.S(DFOOTER));
            bar.BackColor = Theme.Card;
            bar.Paint += delegate(object s, PaintEventArgs e)
            {
                using (Pen pen = new Pen(Theme.CardBorder))
                    e.Graphics.DrawLine(pen, 0, 0, bar.Width, 0);
            };
            Controls.Add(bar);

            autoStart = new Toggle("开机自启");
            autoStart.SetBounds(PAD, Theme.S(12), Theme.S(150), Theme.S(24));
            autoStart.OnChange = delegate(bool on) { SetAutoStart(on); };
            bar.Controls.Add(autoStart);

            int btnH = Theme.S(40);
            int applyW = Theme.S(164);
            int resetW = Theme.S(108);
            applyBtn = new PushButton("应用设置");
            applyBtn.Primary = true;
            applyBtn.Icon = "check";
            applyBtn.SetBounds(bar.Width - PAD - applyW, Theme.S(4), applyW, btnH);
            applyBtn.OnClickAction = delegate { ApplyAll(); };
            bar.Controls.Add(applyBtn);

            resetBtn = new PushButton("重置");
            resetBtn.Primary = false;
            resetBtn.Icon = "undo";
            resetBtn.SetBounds(applyBtn.Left - Theme.S(10) - resetW, Theme.S(4), resetW, btnH);
            resetBtn.OnClickAction = delegate { ResetOptions(); };
            bar.Controls.Add(resetBtn);

            status = new StatusLine();
            status.SetBounds(PAD, Theme.S(52), bar.Width - PAD * 2, Theme.S(22));
            status.Text = "选择各项选项后，点右下角「应用设置」统一执行";
            bar.Controls.Add(status);

            tip = new ToolTip();
            tip.BackColor = Theme.Card;
            tip.ForeColor = Theme.Text;

            ClientSize = new Size(W, bottomY + Theme.S(DFOOTER));
            ApplyRoundedRegion();

            header = new Header();
            header.SetBounds(0, 0, W, Theme.S(DHEADER));
            header.OnMinimize = delegate { MinimizeToTray(); };
            header.OnClose = delegate { forceClose = true; Close(); };
            header.OnRefresh = delegate { RefreshAll(); status.Set("idle", "已刷新设备与信号源"); };
            header.OnTheme = delegate { ToggleTheme(); };
            Controls.Add(header);
            header.BringToFront();

            tray = new NotifyIcon();
            tray.Icon = appIcon;
            tray.Text = "控制中心（双击打开）";
            tray.Visible = true;
            ContextMenuStrip menu = new ContextMenuStrip();
            menu.Items.Add("打开主窗口", null, delegate { RestoreFromTray(); });
            menu.Items.Add("-");
            menu.Items.Add("刷新设备与信号源", null, delegate { RefreshAll(); });
            menu.Items.Add("-");
            menu.Items.Add("退出", null, delegate { forceClose = true; tray.Visible = false; Close(); });
            tray.ContextMenuStrip = menu;
            tray.MouseDoubleClick += delegate { RestoreFromTray(); };

            KeyDown += delegate(object s, KeyEventArgs e)
            {
                if (e.KeyCode == Keys.Escape) { e.Handled = true; MinimizeToTray(); }
            };
            FormClosing += delegate(object s, FormClosingEventArgs e)
            {
                if (!forceClose && e.CloseReason == CloseReason.UserClosing)
                {
                    e.Cancel = true;
                    MinimizeToTray();
                }
            };
            Load += delegate
            {
                autoStart.SetChecked(IsAutoStartOn());
                RefreshAll();
            };
        }

        // ---------- 主题切换 ----------
        void ToggleTheme()
        {
            Theme.Set(!Theme.Dark);
            Recolor();
        }

        void Recolor()
        {
            BackColor = Theme.Window;
            Font = Theme.FBody;
            if (lst != null) { lst.BackColor = Theme.Inner; lst.ForeColor = Theme.Text; }
            if (volVal != null) volVal.ForeColor = Theme.TextDim;
            if (tray != null) { Icon old = appIcon; appIcon = BuildIcon(); tray.Icon = appIcon; if (old != null) old.Dispose(); }
            if (Icon != null) Icon.Dispose();
            Icon = appIcon;
            Invalidate(true);
            if (header != null) header.Invalidate(true);
        }

        // ---------- 布局辅助 ----------
        void AddChips(Card c, List<Chip> target, string[] labels, string[] keys, int cols)
        {
            int gx = CHIPPAD, gy = CHIPPAD;
            int w = (c.Width - PAD * 2 - (cols - 1) * gx) / cols;
            for (int i = 0; i < labels.Length; i++)
            {
                Chip ch = new Chip(labels[i]);
                ch.Key = keys[i];
                ch.SetBounds(PAD + (i % cols) * (w + gx), CONTENT + (i / cols) * (CHIP + gy), w, CHIP);
                int idx = i;
                ch.OnChange = delegate { SelectExclusive(target, target[idx]); };
                target.Add(ch);
                c.Controls.Add(ch);
            }
            target[0].SetSelected(true);
        }

        void AddPresetRow(Card c, string label, string sub, Action act)
        {
            ActionRow row = new ActionRow(label, "bolt", sub);
            int h = Theme.S(40);
            row.SetBounds(PAD, CONTENT + c.Controls.Count * (h + CHIPPAD), c.Width - PAD * 2, h);
            row.OnClickAction = act;
            presetRows.Add(row);
            c.Controls.Add(row);
        }

        void SelectExclusive(List<Chip> group, Chip picked)
        {
            foreach (Chip ch in group) ch.SetSelected(ch == picked);
        }

        string KeyOf(List<Chip> group)
        {
            foreach (Chip ch in group) if (ch.Selected) return ch.Key;
            return "none";
        }

        void ApplyRoundedRegion()
        {
            using (GraphicsPath p = Util.Round(new Rectangle(0, 0, ClientSize.Width, ClientSize.Height), Theme.S(14)))
                Region = new Region(p);
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ClassStyle |= 0x00020000;   // CS_DROPSHADOW：无边框窗口也要投影
                return cp;
            }
        }

        Icon BuildIcon()
        {
            try
            {
                Bitmap bmp = new Bitmap(32, 32);
                using (Graphics g = Graphics.FromImage(bmp))
                {
                    Util.Prep(g);
                    using (GraphicsPath p = Util.Round(new Rectangle(1, 1, 30, 30), 8))
                    using (SolidBrush b = new SolidBrush(Theme.Accent))
                        g.FillPath(b, p);
                    Gly.Draw(g, "monitor", new Rectangle(6, 6, 20, 20),
                             Theme.Dark ? Color.FromArgb(14, 22, 18) : Color.White, 1.7f);
                }
                Icon ic = Icon.FromHandle(bmp.GetHicon());
                bmp.Dispose();
                return ic;
            }
            catch { return null; }
        }

        void RestoreFromTray()
        {
            Show();
            WindowState = FormWindowState.Normal;
            Activate();
        }

        void MinimizeToTray()
        {
            Hide();
            tray.ShowBalloonTip(1200, "控制中心", "已最小化到托盘，双击图标重新打开。", ToolTipIcon.Info);
        }

        public void ExitForced()
        {
            forceClose = true;
            if (tray != null) tray.Visible = false;
            Close();
        }

        // ---------- 数据刷新 ----------
        void RefreshAll()
        {
            RefreshAudioList();
            if (Ddc.Available)
            {
                int cur = Ddc.GetCurrentInput();
                if (cur > 0)
                {
                    // 只标记「当前」，不自动选中，避免点应用设置时把用户没打算改的信号源也重写一遍
                    string key = "0x" + cur.ToString("X2");
                    foreach (Chip ch in sourceChips)
                    {
                        bool isCur = ch.Key.ToLower() == key.ToLower();
                        if (ch.Current != isCur) { ch.Current = isCur; ch.Invalidate(); }
                    }
                    SetStatus("idle", "当前信号源：" + Ddc.Name(cur) + "　·　当前默认输出：" + CurrentDefaultName());
                }
                else SetStatus("warn", "读取信号源失败（显示器需开启 DDC/CI 且由本机输出画面）　·　默认输出：" + CurrentDefaultName());
            }
            else SetStatus("warn", "未找到 winddcutil.exe，信号源切换不可用");
        }

        string CurrentDefaultName()
        {
            foreach (object o in lst.Items)
            {
                AudioApi.Endpoint ep = o as AudioApi.Endpoint;
                if (ep != null && ep.IsDefault) return ep.Name;
            }
            return "无";
        }

        void RefreshAudioList()
        {
            suppressEvents = true;
            string keepId = null;
            AudioApi.Endpoint cur = lst.SelectedItem as AudioApi.Endpoint;
            if (cur != null) keepId = cur.Id;
            lst.Items.Clear();
            List<AudioApi.Endpoint> eps = AudioApi.List();
            int def = -1, keep = -1;
            for (int i = 0; i < eps.Count; i++)
            {
                lst.Items.Add(eps[i]);
                if (eps[i].IsDefault) def = i;
                if (keepId != null && eps[i].Id == keepId) keep = i;
            }
            int pick = keep >= 0 ? keep : (def >= 0 ? def : 0);
            if (eps.Count > 0) lst.SelectedIndex = pick;
            suppressEvents = false;
            if (eps.Count > 0) LoadVolume();
            lst.Invalidate();
        }

        static string DeviceIcon(AudioApi.Endpoint ep)
        {
            if (ep == null || ep.Name == null) return "speaker";
            string n = ep.Name.ToLower();
            if (n.Contains("headphone") || n.Contains("earbud") || ep.Name.Contains("耳机")) return "headphone";
            // 走 HDMI/DP 进显示器的音频：用显示器图标，方便和音箱区分
            if (n.Contains("nvidia high definition")
                || n.Contains("intel display audio") || n.Contains("amd hdmi")) return "monitor";
            return "speaker";
        }

        void LoadVolume()
        {
            AudioApi.Endpoint ep = lst.SelectedItem as AudioApi.Endpoint;
            if (ep == null) { baseVolume = -1; return; }
            float s; bool m;
            if (AudioApi.GetVolume(ep, out s, out m))
            {
                baseVolume = (int)Math.Round(s * 100);
                baseMute = m;
                slider.Value = baseVolume;
                muteBtn.Active = m;
                muteBtn.IconName = m ? "mute" : "speaker";
                muteBtn.Invalidate();
                UpdateVolLabel();
            }
        }

        void UpdateVolLabel()
        {
            volVal.Text = slider.Value + "%";
            tip.SetToolTip(slider, "音量 " + slider.Value + "%" + (muteBtn.Active ? "（已静音）" : ""));
        }

        // ---------- 开机自启 ----------
        const string RunKey = "Software\\Microsoft\\Windows\\CurrentVersion\\Run";
        const string RunValue = "ControlCenter";

        bool IsAutoStartOn()
        {
            try
            {
                using (RegistryKey k = Registry.CurrentUser.OpenSubKey(RunKey))
                    return k != null && k.GetValue(RunValue) != null;
            }
            catch { return false; }
        }

        void SetAutoStart(bool on)
        {
            try
            {
                using (RegistryKey k = Registry.CurrentUser.CreateSubKey(RunKey))
                {
                    if (k == null) { SetStatus("err", "写入开机自启失败"); return; }
                    if (on) k.SetValue(RunValue, "\"" + Application.ExecutablePath + "\"");
                    else k.DeleteValue(RunValue, false);
                }
                SetStatus("ok", on ? "已开启开机自启" : "已关闭开机自启");
            }
            catch (Exception e) { SetStatus("err", "开机自启设置失败：" + e.Message); }
        }

        // ---------- 选项 / 状态 ----------
        void SetStatus(string kind, string s)
        {
            if (status == null) return;
            status.Set(kind, s);
            tip.SetToolTip(status, s);
        }

        void ApplyPreset(string sourceKey)
        {
            foreach (Chip ch in sourceChips)
                if (ch.Key == sourceKey) { SelectExclusive(sourceChips, ch); break; }
            foreach (Chip ch in powerChips)
                if (ch.Key == "sleep") { SelectExclusive(powerChips, ch); break; }
            audioTouched = false;
            volTouched = false;
            lst.Invalidate();
            SetStatus("idle", "已选组合：切到 " + CodeName(sourceKey) + " 并让本机睡眠　·　点「应用设置」执行");
        }

        void ResetOptions()
        {
            SelectExclusive(sourceChips, sourceChips[0]);
            SelectExclusive(projChips, projChips[0]);
            SelectExclusive(powerChips, powerChips[0]);
            audioTouched = false;
            volTouched = false;
            PickDefault();
            SetStatus("idle", "选项已重置");
        }

        void PickDefault()
        {
            suppressEvents = true;
            for (int i = 0; i < lst.Items.Count; i++)
            {
                AudioApi.Endpoint ep = lst.Items[i] as AudioApi.Endpoint;
                if (ep != null && ep.IsDefault) { lst.SelectedIndex = i; break; }
            }
            suppressEvents = false;
            LoadVolume();
            lst.Invalidate();
        }

        bool AnyOption()
        {
            if (KeyOf(sourceChips) != "none") return true;
            if (KeyOf(projChips) != "none") return true;
            if (KeyOf(powerChips) != "none") return true;
            if (audioTouched || volTouched) return true;
            return false;
        }

        string Summary()
        {
            List<string> parts = new List<string>();
            string src = KeyOf(sourceChips);
            if (src != "none") parts.Add("信号源 → " + CodeName(src));
            string prj = KeyOf(projChips);
            if (prj != "none") parts.Add("投影 → " + ChipText(projChips));
            if (audioTouched)
            {
                AudioApi.Endpoint ep = lst.SelectedItem as AudioApi.Endpoint;
                parts.Add("声音输出 → " + (ep == null ? "未选择" : ep.Name));
            }
            if (volTouched)
                parts.Add("音量 → " + slider.Value + "%" + (muteBtn.Active ? "（静音）" : ""));
            string pw = KeyOf(powerChips);
            if (pw != "none") parts.Add("电源 → " + ChipText(powerChips));
            if (parts.Count == 0) return "（未选择任何更改）";
            return string.Join("\n", parts.ToArray());
        }

        string ChipText(List<Chip> group)
        {
            foreach (Chip ch in group) if (ch.Selected) return ch.Text;
            return "";
        }

        static string CodeName(string key)
        {
            if (key == "auto") return "自动切换";
            if (key == "0x0F") return "DisplayPort（主机）";
            if (key == "0x11") return "HDMI-1（笔记本）";
            if (key == "0x12") return "HDMI-2";
            return key;
        }

        // ---------- 统一执行 ----------
        void ApplyAll()
        {
            if (!AnyOption())
            {
                SetStatus("warn", "请先选择要执行的选项");
                return;
            }
            string power = KeyOf(powerChips);
            if (power != "none" && power != "sleep")
            {
                if (MessageBox.Show(this,
                    "即将执行以下操作：\n\n" + Summary() + "\n\n确认继续？",
                    "确认" + ChipText(powerChips), MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
                {
                    SetStatus("idle", "已取消，未执行任何操作");
                    return;
                }
            }

            applyBtn.Disabled = true;
            applyBtn.Invalidate();
            List<string> log = new List<string>();
            try
            {
                // 1) 声音输出设备
                if (audioTouched)
                {
                    AudioApi.Endpoint ep = lst.SelectedItem as AudioApi.Endpoint;
                    if (ep == null) log.Add("声音：未选择设备");
                    else if (ep.IsDefault) log.Add("声音：已是默认（" + ep.Name + "）");
                    else log.Add(AudioApi.SetDefault(ep) ? "声音：已切到 " + ep.Name : "声音：切换失败 " + ep.Name);
                    audioTouched = false;
                    RefreshAudioList();
                }
                // 2) 音量
                if (volTouched)
                {
                    AudioApi.Endpoint ep = lst.SelectedItem as AudioApi.Endpoint;
                    if (ep != null)
                    {
                        bool a = AudioApi.SetVolume(ep, slider.Value / 100f);
                        bool b = AudioApi.SetMute(ep, muteBtn.Active);
                        log.Add("音量：" + (a || b ? slider.Value + "% " + (muteBtn.Active ? "已静音" : "已设置") : "音量设置失败"));
                    }
                    volTouched = false;
                }
                // 3) 投影
                string prj = KeyOf(projChips);
                if (prj != "none")
                {
                    Projection.Mode m = Projection.Mode.Extend;
                    if (prj == "duplicate") m = Projection.Mode.Duplicate;
                    else if (prj == "pconly") m = Projection.Mode.PCOnly;
                    else if (prj == "secondonly") m = Projection.Mode.SecondOnly;
                    log.Add("投影：" + Projection.Apply(m));
                }
                // 4) 信号源
                string src = KeyOf(sourceChips);
                if (src != "none")
                {
                    if (!Ddc.Available) log.Add("信号源：缺少 winddcutil.exe");
                    else
                    {
                        int code;
                        if (src == "auto")
                        {
                            int cur = Ddc.GetCurrentInput();
                            code = (cur == Ddc.DP) ? Ddc.HDMI1 : Ddc.DP;
                        }
                        else code = Convert.ToInt32(src, 16);
                        log.Add("信号源：" + (Ddc.SetInput(code) ? "已切到 " + Ddc.Name(code) : "切换失败（DDC/CI 无响应）"));
                    }
                }
                // 5) 电源（最后）
                if (power == "sleep") log.Add("电源：1 秒后进入睡眠");
                else if (power == "shutdown") { Start("shutdown.exe", "/s /t 15"); log.Add("电源：已计划关机（15 秒后，可撤销）"); }
                else if (power == "restart") { Start("shutdown.exe", "/r /t 15"); log.Add("电源：已计划重启（15 秒后，可撤销）"); }
            }
            finally
            {
                applyBtn.Disabled = false;
                applyBtn.Invalidate();
            }

            string result = string.Join("　·　", log.ToArray());
            bool bad = result.Contains("失败");
            SetStatus(bad ? "err" : "ok", result);
            if (tray != null) tray.ShowBalloonTip(2500, "控制中心", result, ToolTipIcon.Info);

            if (power == "sleep")
            {
                Application.DoEvents();
                Thread.Sleep(1000);
                try { Native.SetSuspendState(false, false, false); }
                catch (Exception e) { SetStatus("err", "睡眠失败：" + e.Message); }
            }
            else if (power != "none")
            {
                if (MessageBox.Show(this,
                    result + "\n\n如需取消，可点下方按钮（或再次点「应用设置」前先撤销）。",
                    "已计划" + ChipText(powerChips), MessageBoxButtons.OKCancel, MessageBoxIcon.Question) == DialogResult.OK)
                {
                    Start("shutdown.exe", "/a");
                    SetStatus("ok", "已撤销计划的" + ChipText(powerChips));
                }
            }
            else RefreshAll();
        }

        static void Start(string file, string args)
        {
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo(file, args);
                psi.UseShellExecute = false;
                psi.CreateNoWindow = true;
                Process.Start(psi);
            }
            catch (Exception e)
            {
                MessageBox.Show("执行失败：" + e.Message, "控制中心", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
    }

    // ==================== 入口 ====================
    static class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            bool shot = args.Length > 0 && args[0] == "--shot";
            if (args.Length > 0 && args[0] == "--diag")
            {
                System.IO.File.WriteAllText(
                    System.IO.Path.Combine(Application.StartupPath, "audio-diag.txt"),
                    AudioApi.Diagnose());
                return;
            }
            Mutex single = null;
            if (!shot)
            {
                bool created;
                single = new Mutex(true, "ControlCenterSingleInstance", out created);
                if (!created)
                {
                    // 已有一个实例：把它的窗口唤到前台（最小化到托盘时会自动还原）
                    IntPtr h = Native.FindWindow(null, "控制中心");
                    if (h != IntPtr.Zero)
                    {
                        Native.ShowWindow(h, 9);      // SW_RESTORE
                        Native.SetForegroundWindow(h);
                    }
                    return;
                }
            }

            try { Native.SetProcessDPIAware(); } catch { }
            // 屏幕 DC 的 DpiX 在现代 Windows 上恒为 96，只有 GetDpiForSystem 给的是真实缩放
            float dpi = 96f;
            try { uint d = Native.GetDpiForSystem(); if (d > 0) dpi = (float)d; } catch { }
            float scale = dpi / 96f;
            if (scale < 1f || scale > 4f) scale = 1f;

            Theme.Scale = scale;
            Theme.InitFonts();
            bool dark = Theme.SystemIsDark();
            if (args.Length > 2 && args[2] == "light") dark = false;
            if (args.Length > 2 && args[2] == "dark") dark = true;
            Theme.Set(dark);

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            if (shot)
            {
                // 调试用：把界面渲染成图片，便于远程验收（--shot 路径 [light|dark]）
                string path = args.Length > 1 ? args[1]
                    : System.IO.Path.Combine(Application.StartupPath, "ui-shot.png");
                MainForm f = new MainForm();
                f.Show();
                Application.DoEvents();
                System.Threading.Thread.Sleep(400);
                Application.DoEvents();
                using (Bitmap bmp = new Bitmap(f.ClientSize.Width, f.ClientSize.Height))
                {
                    using (Graphics gg = Graphics.FromImage(bmp))
                    {
                        IntPtr hdc = gg.GetHdc();
                        Native.PrintWindow(f.Handle, hdc, 2);   // PW_RENDERFULLCONTENT：自绘子控件也能抓到
                        gg.ReleaseHdc(hdc);
                    }
                    bmp.Save(path, System.Drawing.Imaging.ImageFormat.Png);
                }
                f.ExitForced();
                return;
            }

            try
            {
                Application.Run(new MainForm());
            }
            catch (Exception e)
            {
                // 无边框窗口崩了不会有痕迹，留一份日志方便回看
                System.IO.File.WriteAllText(
                    System.IO.Path.Combine(Application.StartupPath, "crash.log"), e.ToString());
                MessageBox.Show("启动失败，详情见同目录 crash.log：\n\n" + e.Message,
                    "控制中心", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            GC.KeepAlive(single);
        }
    }
}
