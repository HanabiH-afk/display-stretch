using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Media;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

namespace DisplayStretch
{
    internal static class AppInfo
    {
        public const string Version = "1.0.0";
        public const string RunValueName = "DisplayStretch";

        public static string Dir
        {
            get
            {
                string p = Path.GetDirectoryName(Application.ExecutablePath);
                if (string.IsNullOrEmpty(p)) p = Environment.CurrentDirectory;
                return p;
            }
        }

        public static string ConfigPath { get { return Path.Combine(Dir, "DisplayStretch.ini"); } }
        public static string LogPath { get { return Path.Combine(Dir, "DisplayStretch.log"); } }
    }

    internal static class Log
    {
        public static void Write(string message)
        {
            try
            {
                File.AppendAllText(AppInfo.LogPath,
                    DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "  " + message + "\r\n",
                    Encoding.UTF8);
            }
            catch { }
        }
    }

    internal sealed class AppConfig
    {
        public int TargetWidth = 1280;
        public int TargetHeight = 960;
        public int TargetRefresh = 0;
        public int NativeWidth = 0;
        public int NativeHeight = 0;
        public int NativeRefresh = 0;
        public string NativeDevice = "";
        public string Hotkey = "F9";

        public static AppConfig Load()
        {
            var c = new AppConfig();
            try
            {
                if (!File.Exists(AppInfo.ConfigPath)) return c;
                foreach (string raw in File.ReadAllLines(AppInfo.ConfigPath, Encoding.UTF8))
                {
                    string line = raw.Trim();
                    if (line.Length == 0 || line.StartsWith("#") || line.StartsWith(";")) continue;
                    int eq = line.IndexOf('=');
                    if (eq <= 0) continue;
                    string k = line.Substring(0, eq).Trim().ToLowerInvariant();
                    string v = line.Substring(eq + 1).Trim();
                    switch (k)
                    {
                        case "targetwidth": c.TargetWidth = ToInt(v, c.TargetWidth); break;
                        case "targetheight": c.TargetHeight = ToInt(v, c.TargetHeight); break;
                        case "targetrefresh": c.TargetRefresh = ToInt(v, c.TargetRefresh); break;
                        case "nativewidth": c.NativeWidth = ToInt(v, c.NativeWidth); break;
                        case "nativeheight": c.NativeHeight = ToInt(v, c.NativeHeight); break;
                        case "nativerefresh": c.NativeRefresh = ToInt(v, c.NativeRefresh); break;
                        case "nativedevice": c.NativeDevice = v; break;
                        case "hotkey": if (v.Length > 0) c.Hotkey = v; break;
                    }
                }
            }
            catch (Exception ex) { Log.Write("读取配置失败: " + ex.Message); }
            return c;
        }

        public void Save()
        {
            try
            {
                var lines = new List<string>();
                lines.Add("# DisplayStretch 配置（可手动编辑，保存后重启程序生效）");
                lines.Add("TargetWidth=" + TargetWidth);
                lines.Add("TargetHeight=" + TargetHeight);
                lines.Add("TargetRefresh=" + TargetRefresh);
                lines.Add("NativeWidth=" + NativeWidth);
                lines.Add("NativeHeight=" + NativeHeight);
                lines.Add("NativeRefresh=" + NativeRefresh);
                lines.Add("NativeDevice=" + NativeDevice);
                lines.Add("Hotkey=" + Hotkey);
                File.WriteAllLines(AppInfo.ConfigPath, lines.ToArray(), Encoding.UTF8);
            }
            catch (Exception ex) { Log.Write("保存配置失败: " + ex.Message); }
        }

        private static int ToInt(string s, int fallback)
        {
            int v;
            if (int.TryParse(s, out v)) return v;
            return fallback;
        }
    }

    internal sealed class DisplayDevice
    {
        public string Name = "";
        public string Adapter = "";
        public bool Primary;
        public int Width;
        public int Height;
        public int Hz;
    }

    internal struct DisplayMode
    {
        public int Width;
        public int Height;
        public int Hz;
    }

    internal sealed class SwitchResult
    {
        public bool Ok;
        public string Message = "";

        public static SwitchResult Fail(string message)
        {
            var r = new SwitchResult();
            r.Ok = false;
            r.Message = message;
            return r;
        }

        public static SwitchResult Success(string message)
        {
            var r = new SwitchResult();
            r.Ok = true;
            r.Message = message;
            return r;
        }
    }

    internal static class Display
    {
        private const int ENUM_CURRENT_SETTINGS = -1;
        private const uint DISPLAY_DEVICE_ATTACHED_TO_DESKTOP = 0x00000001;
        private const uint DISPLAY_DEVICE_PRIMARY_DEVICE = 0x00000004;
        private const int CDS_UPDATEREGISTRY = 0x00000001;
        private const int CDS_TEST = 0x00000002;
        private const int DM_BITSPERPEL = 0x00040000;
        private const int DM_PELSWIDTH = 0x00080000;
        private const int DM_PELSHEIGHT = 0x00100000;
        private const int DM_DISPLAYFREQUENCY = 0x00400000;
        private const int DM_DISPLAYFIXEDOUTPUT = 0x20000000;
        private const int DMDFO_DEFAULT = 0;
        private const int DMDFO_STRETCH = 1;
        private const uint QDC_ONLY_ACTIVE_PATHS = 0x00000002;

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        internal struct DISPLAY_DEVICE
        {
            public int cb;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string DeviceName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string DeviceString;
            public int StateFlags;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string DeviceID;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string DeviceKey;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        internal struct DEVMODE
        {
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string dmDeviceName;
            public short dmSpecVersion;
            public short dmDriverVersion;
            public short dmSize;
            public short dmDriverExtra;
            public int dmFields;
            public int dmPositionX;
            public int dmPositionY;
            public int dmDisplayOrientation;
            public int dmDisplayFixedOutput;
            public short dmColor;
            public short dmDuplex;
            public short dmYResolution;
            public short dmTTOption;
            public short dmCollate;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string dmFormName;
            public short dmLogPixels;
            public int dmBitsPerPel;
            public int dmPelsWidth;
            public int dmPelsHeight;
            public int dmDisplayFlags;
            public int dmDisplayFrequency;
            public int dmICMMethod;
            public int dmICMIntent;
            public int dmMediaType;
            public int dmDitherType;
            public int dmReserved1;
            public int dmReserved2;
            public int dmPanningWidth;
            public int dmPanningHeight;
        }

        [StructLayout(LayoutKind.Sequential)] internal struct LUID { public uint LowPart; public int HighPart; }
        [StructLayout(LayoutKind.Sequential)] internal struct RATIONAL { public uint Num; public uint Den; }
        [StructLayout(LayoutKind.Sequential)] internal struct REGION2D { public uint cx; public uint cy; }
        [StructLayout(LayoutKind.Sequential)] internal struct POINTL { public int x; public int y; }
        [StructLayout(LayoutKind.Sequential)] internal struct RECTL { public int left, top, right, bottom; }

        [StructLayout(LayoutKind.Sequential)]
        internal struct PATH_SOURCE_INFO
        {
            public LUID adapterId;
            public uint id;
            public uint modeInfoIdx;
            public uint statusFlags;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct PATH_TARGET_INFO
        {
            public LUID adapterId;
            public uint id;
            public uint modeInfoIdx;
            public uint outputTechnology;
            public uint rotation;
            public uint scaling;
            public RATIONAL refreshRate;
            public uint scanLineOrdering;
            public int targetAvailable;
            public uint statusFlags;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct PATH_INFO
        {
            public PATH_SOURCE_INFO sourceInfo;
            public PATH_TARGET_INFO targetInfo;
            public uint flags;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct VIDEO_SIGNAL_INFO
        {
            public ulong pixelRate;
            public RATIONAL hSyncFreq;
            public RATIONAL vSyncFreq;
            public REGION2D activeSize;
            public REGION2D totalSize;
            public uint videoStandard;
            public uint scanLineOrdering;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct TARGET_MODE { public VIDEO_SIGNAL_INFO targetVideoSignalInfo; }

        [StructLayout(LayoutKind.Sequential)]
        internal struct SOURCE_MODE
        {
            public uint width;
            public uint height;
            public uint pixelFormat;
            public POINTL position;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct DESKTOP_IMAGE_INFO
        {
            public POINTL pathSourceSize;
            public RECTL desktopImageRegion;
            public RECTL desktopImageClip;
        }

        [StructLayout(LayoutKind.Explicit)]
        internal struct MODE_INFO
        {
            [FieldOffset(0)] public uint infoType;
            [FieldOffset(4)] public uint id;
            [FieldOffset(8)] public LUID adapterId;
            [FieldOffset(16)] public TARGET_MODE targetMode;
            [FieldOffset(16)] public SOURCE_MODE sourceMode;
            [FieldOffset(16)] public DESKTOP_IMAGE_INFO desktopImageInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct DEVICE_INFO_HEADER
        {
            public uint type;
            public uint size;
            public LUID adapterId;
            public uint id;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        internal struct SOURCE_DEVICE_NAME
        {
            public DEVICE_INFO_HEADER header;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string viewGdiDeviceName;
        }

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern bool EnumDisplayDevices(string lpDevice, uint iDevNum, ref DISPLAY_DEVICE lpDisplayDevice, uint dwFlags);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern bool EnumDisplaySettings(string lpszDeviceName, int iModeNum, ref DEVMODE lpDevMode);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int ChangeDisplaySettingsEx(string lpszDeviceName, ref DEVMODE lpDevMode, IntPtr hwnd, int dwflags, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern int GetDisplayConfigBufferSizes(uint flags, out uint numPathArrayElements, out uint numModeInfoArrayElements);

        [DllImport("user32.dll")]
        private static extern int QueryDisplayConfig(uint flags, ref uint numPathArrayElements, [Out] PATH_INFO[] pathInfoArray, ref uint numModeInfoArrayElements, [Out] MODE_INFO[] modeInfoArray, IntPtr currentTopologyId);

        [DllImport("user32.dll")]
        private static extern int DisplayConfigGetDeviceInfo(ref SOURCE_DEVICE_NAME requestPacket);

        private static DEVMODE NewDevMode()
        {
            var dm = new DEVMODE();
            dm.dmSize = (short)Marshal.SizeOf(typeof(DEVMODE));
            return dm;
        }

        public static List<DisplayDevice> GetActiveDisplays()
        {
            var list = new List<DisplayDevice>();
            for (uint i = 0; i < 64; i++)
            {
                var dd = new DISPLAY_DEVICE();
                dd.cb = Marshal.SizeOf(typeof(DISPLAY_DEVICE));
                if (!EnumDisplayDevices(null, i, ref dd, 0)) break;
                if ((dd.StateFlags & DISPLAY_DEVICE_ATTACHED_TO_DESKTOP) == 0) continue;
                var dm = NewDevMode();
                if (!EnumDisplaySettings(dd.DeviceName, ENUM_CURRENT_SETTINGS, ref dm)) continue;
                var d = new DisplayDevice();
                d.Name = dd.DeviceName;
                d.Adapter = dd.DeviceString;
                d.Primary = (dd.StateFlags & DISPLAY_DEVICE_PRIMARY_DEVICE) != 0;
                d.Width = dm.dmPelsWidth;
                d.Height = dm.dmPelsHeight;
                d.Hz = dm.dmDisplayFrequency;
                list.Add(d);
            }
            return list;
        }

        public static DisplayDevice Primary()
        {
            List<DisplayDevice> list = GetActiveDisplays();
            foreach (DisplayDevice d in list) if (d.Primary) return d;
            return list.Count > 0 ? list[0] : null;
        }

        public static List<DisplayMode> GetModes(string device)
        {
            var result = new List<DisplayMode>();
            var seen = new HashSet<string>();
            for (int i = 0; i < 4000; i++)
            {
                var dm = NewDevMode();
                if (!EnumDisplaySettings(device, i, ref dm)) break;
                if (dm.dmBitsPerPel != 32) continue;
                string key = dm.dmPelsWidth + "x" + dm.dmPelsHeight + "@" + dm.dmDisplayFrequency;
                if (!seen.Add(key)) continue;
                var m = new DisplayMode();
                m.Width = dm.dmPelsWidth;
                m.Height = dm.dmPelsHeight;
                m.Hz = dm.dmDisplayFrequency;
                result.Add(m);
            }
            return result;
        }

        public static bool TryGetCurrent(string device, out DisplayMode mode)
        {
            mode = new DisplayMode();
            var dm = NewDevMode();
            if (!EnumDisplaySettings(device, ENUM_CURRENT_SETTINGS, ref dm)) return false;
            mode.Width = dm.dmPelsWidth;
            mode.Height = dm.dmPelsHeight;
            mode.Hz = dm.dmDisplayFrequency;
            return true;
        }

        public static int ApplyMode(string device, int width, int height, int refresh, bool stretch, out bool stretchUnsupported)
        {
            stretchUnsupported = false;
            var dm = NewDevMode();
            if (!EnumDisplaySettings(device, ENUM_CURRENT_SETTINGS, ref dm)) return -99;

            dm.dmPelsWidth = width;
            dm.dmPelsHeight = height;
            dm.dmBitsPerPel = 32;
            dm.dmFields = DM_PELSWIDTH | DM_PELSHEIGHT | DM_BITSPERPEL;
            if (refresh > 0)
            {
                dm.dmDisplayFrequency = refresh;
                dm.dmFields |= DM_DISPLAYFREQUENCY;
            }
            if (stretch)
            {
                dm.dmDisplayFixedOutput = DMDFO_STRETCH;
                dm.dmFields |= DM_DISPLAYFIXEDOUTPUT;
            }

            int test = ChangeDisplaySettingsEx(device, ref dm, IntPtr.Zero, CDS_TEST, IntPtr.Zero);
            if (test != 0 && stretch)
            {
                dm.dmDisplayFixedOutput = DMDFO_DEFAULT;
                dm.dmFields &= ~DM_DISPLAYFIXEDOUTPUT;
                int test2 = ChangeDisplaySettingsEx(device, ref dm, IntPtr.Zero, CDS_TEST, IntPtr.Zero);
                if (test2 == 0)
                {
                    stretchUnsupported = true;
                    test = 0;
                }
            }
            if (test != 0) return test;

            return ChangeDisplaySettingsEx(device, ref dm, IntPtr.Zero, CDS_UPDATEREGISTRY, IntPtr.Zero);
        }

        public static int GetScaling(string device)
        {
            uint pathCount = 0;
            uint modeCount = 0;
            int err = GetDisplayConfigBufferSizes(QDC_ONLY_ACTIVE_PATHS, out pathCount, out modeCount);
            if (err != 0 || pathCount == 0) return -1;
            var paths = new PATH_INFO[pathCount];
            var modes = new MODE_INFO[modeCount];
            err = QueryDisplayConfig(QDC_ONLY_ACTIVE_PATHS, ref pathCount, paths, ref modeCount, modes, IntPtr.Zero);
            if (err != 0) return -1;
            for (int i = 0; i < pathCount; i++)
            {
                var request = new SOURCE_DEVICE_NAME();
                request.header.type = 1;
                request.header.size = (uint)Marshal.SizeOf(typeof(SOURCE_DEVICE_NAME));
                request.header.adapterId = paths[i].sourceInfo.adapterId;
                request.header.id = paths[i].sourceInfo.id;
                if (DisplayConfigGetDeviceInfo(ref request) != 0) continue;
                if (string.Equals(request.viewGdiDeviceName, device, StringComparison.OrdinalIgnoreCase))
                    return (int)paths[i].targetInfo.scaling;
            }
            return -1;
        }

        public static string ScalingText(int scaling)
        {
            switch (scaling)
            {
                case 1: return "原始（不缩放）";
                case 2: return "居中（1:1）";
                case 3: return "全屏拉伸";
                case 4: return "等比（黑边）";
                case 5: return "自定义";
                default: return "未知";
            }
        }

        public static string ErrorText(int code)
        {
            switch (code)
            {
                case 0: return "成功";
                case 1: return "需要重启才能生效";
                case -1: return "驱动拒绝切换";
                case -2: return "该分辨率/刷新率不受支持";
                case -3: return "注册表写入失败";
                case -4: return "调用标志无效";
                case -5: return "参数无效";
                case -6: return "双屏模式不支持";
                default: return "错误码 " + code;
            }
        }
    }

    internal static class Switch
    {
        private static readonly int[][] PreferredTargets = new int[][]
        {
            new int[] { 1280, 960 },
            new int[] { 1440, 1080 },
            new int[] { 1024, 768 },
            new int[] { 1152, 864 },
            new int[] { 1600, 1200 },
            new int[] { 1920, 1440 },
            new int[] { 2048, 1536 }
        };

        public static SwitchResult Toggle(AppConfig cfg, DisplayDevice disp)
        {
            DisplayMode cur;
            if (!Display.TryGetCurrent(disp.Name, out cur)) return SwitchResult.Fail("无法读取当前显示模式");
            if (cur.Width == cfg.TargetWidth && cur.Height == cfg.TargetHeight) return Restore(cfg, disp);
            return To43(cfg, disp);
        }

        public static SwitchResult To43(AppConfig cfg, DisplayDevice disp)
        {
            List<DisplayMode> modes = Display.GetModes(disp.Name);
            if (modes.Count == 0) return SwitchResult.Fail("无法枚举显示器 " + disp.Name + " 的分辨率");
            int refresh = PickRefresh(modes, cfg.TargetWidth, cfg.TargetHeight, cfg.TargetRefresh);
            if (refresh == 0)
                return SwitchResult.Fail(string.Format("该显示器不支持 {0}×{1}，请右键托盘菜单另选一个 4:3 分辨率", cfg.TargetWidth, cfg.TargetHeight));

            DisplayMode cur;
            if (!Display.TryGetCurrent(disp.Name, out cur)) return SwitchResult.Fail("无法读取当前显示模式");
            double aspect = (double)cur.Width / Math.Max(1, cur.Height);
            if (aspect >= 1.5 || cfg.NativeWidth <= 0)
            {
                cfg.NativeWidth = cur.Width;
                cfg.NativeHeight = cur.Height;
                cfg.NativeRefresh = cur.Hz;
                cfg.NativeDevice = disp.Name;
            }

            bool fallback;
            int ret = Display.ApplyMode(disp.Name, cfg.TargetWidth, cfg.TargetHeight, refresh, true, out fallback);
            if (ret != 0) return SwitchResult.Fail("切换失败：" + Display.ErrorText(ret));

            DisplayMode now;
            if (!Display.TryGetCurrent(disp.Name, out now)) return SwitchResult.Fail("切换后无法读取显示模式");
            if (now.Width != cfg.TargetWidth || now.Height != cfg.TargetHeight)
            {
                Restore(cfg, disp);
                return SwitchResult.Fail("目标模式未生效，已恢复原分辨率");
            }

            cfg.TargetRefresh = refresh;
            cfg.Save();

            int scaling = Display.GetScaling(disp.Name);
            string msg = string.Format("已切换：{0}×{1} · {2}Hz", now.Width, now.Height, now.Hz);
            if (scaling == 3) msg += " · 真实拉伸已开启";
            else if (scaling > 0) msg += "\n缩放模式：" + Display.ScalingText(scaling) + "（可能不是全屏）";
            else if (fallback) msg += "\n该刷新率不支持强制拉伸，可能显示黑边";
            Log.Write(msg.Replace("\n", " "));
            return SwitchResult.Success(msg);
        }

        public static SwitchResult Restore(AppConfig cfg, DisplayDevice disp)
        {
            List<DisplayMode> modes = Display.GetModes(disp.Name);
            if (modes.Count == 0) return SwitchResult.Fail("无法枚举显示器的分辨率");

            int w = cfg.NativeWidth;
            int h = cfg.NativeHeight;
            int hz = cfg.NativeRefresh;

            if (w <= 0 || h <= 0 || PickRefresh(modes, w, h, hz) == 0)
            {
                DisplayMode best = BestMode(modes);
                if (best.Width <= 0) return SwitchResult.Fail("找不到可恢复的分辨率");
                w = best.Width;
                h = best.Height;
                hz = best.Hz;
            }

            int refresh = PickRefresh(modes, w, h, hz);
            if (refresh == 0) refresh = hz;

            bool fallback;
            int ret = Display.ApplyMode(disp.Name, w, h, refresh, false, out fallback);
            if (ret != 0) return SwitchResult.Fail("恢复失败：" + Display.ErrorText(ret));

            DisplayMode now;
            if (!Display.TryGetCurrent(disp.Name, out now)) return SwitchResult.Fail("恢复后无法读取显示模式");

            cfg.NativeWidth = now.Width;
            cfg.NativeHeight = now.Height;
            cfg.NativeRefresh = now.Hz;
            cfg.NativeDevice = disp.Name;
            cfg.Save();

            string msg = string.Format("已恢复：{0}×{1} · {2}Hz", now.Width, now.Height, now.Hz);
            Log.Write(msg);
            return SwitchResult.Success(msg);
        }

        public static DisplayMode SuggestTarget(List<DisplayMode> modes, DisplayDevice disp)
        {
            foreach (int[] t in PreferredTargets)
            {
                if (PickRefresh(modes, t[0], t[1], 0) != 0)
                {
                    var m = new DisplayMode();
                    m.Width = t[0];
                    m.Height = t[1];
                    m.Hz = PickRefresh(modes, t[0], t[1], 0);
                    return m;
                }
            }
            DisplayMode best = new DisplayMode();
            long area = -1;
            foreach (DisplayMode m in modes)
            {
                double a = (double)m.Width / Math.Max(1, m.Height);
                if (Math.Abs(a - 4.0 / 3.0) > 0.02) continue;
                long ar = (long)m.Width * m.Height;
                if (ar > area) { area = ar; best = m; }
            }
            return best;
        }

        private static DisplayMode BestMode(List<DisplayMode> modes)
        {
            var best = new DisplayMode();
            long area = -1;
            foreach (DisplayMode m in modes)
            {
                long a = (long)m.Width * m.Height;
                if (a > area || (a == area && m.Hz > best.Hz))
                {
                    area = a;
                    best = m;
                }
            }
            return best;
        }

        public static int PickRefresh(List<DisplayMode> modes, int w, int h, int preferred)
        {
            int best = 0;
            bool found = false;
            foreach (DisplayMode m in modes)
            {
                if (m.Width != w || m.Height != h) continue;
                found = true;
                if (preferred > 0 && m.Hz == preferred) return preferred;
                if (m.Hz > best) best = m.Hz;
            }
            return found ? best : 0;
        }
    }

    internal sealed class HotkeyWindow : NativeWindow, IDisposable
    {
        private const int WM_HOTKEY = 0x0312;
        private const int MOD_NOREPEAT = 0x4000;
        private const int HotkeyId = 0x0B43;

        private bool _registered;

        public event Action HotkeyPressed;

        [DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, int fsModifiers, int vk);

        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        public HotkeyWindow()
        {
            var cp = new CreateParams();
            cp.Caption = "DisplayStretchHotkey";
            CreateHandle(cp);
        }

        public bool TryRegister(int virtualKey)
        {
            Unregister();
            _registered = RegisterHotKey(Handle, HotkeyId, MOD_NOREPEAT, virtualKey);
            if (!_registered) _registered = RegisterHotKey(Handle, HotkeyId, 0, virtualKey);
            return _registered;
        }

        private void Unregister()
        {
            if (_registered)
            {
                UnregisterHotKey(Handle, HotkeyId);
                _registered = false;
            }
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_HOTKEY && m.WParam.ToInt32() == HotkeyId)
            {
                Action handler = HotkeyPressed;
                if (handler != null) handler();
                return;
            }
            base.WndProc(ref m);
        }

        public void Dispose()
        {
            Unregister();
            if (Handle != IntPtr.Zero) DestroyHandle();
        }
    }

    internal sealed class TrayApp : ApplicationContext
    {
        private readonly AppConfig _cfg;
        private readonly NotifyIcon _tray;
        private readonly ContextMenu _menu;
        private readonly HotkeyWindow _hotkey;
        private readonly Icon _icon;
        private bool _busy;

        public TrayApp()
        {
            _cfg = AppConfig.Load();

            _icon = CreateTrayIcon();
            _tray = new NotifyIcon();
            _tray.Icon = _icon;
            _tray.Text = "分辨率切换 · " + _cfg.Hotkey + " 一键 4:3 拉伸";
            _menu = new ContextMenu();
            _menu.Popup += OnMenuPopup;
            _tray.ContextMenu = _menu;
            _tray.DoubleClick += OnDoubleClick;
            _tray.Visible = true;

            _hotkey = new HotkeyWindow();
            _hotkey.HotkeyPressed += OnHotkey;

            NormalizeState();
            bool hotkeyOk = RegisterHotkey();
            Log.Write("启动 v" + AppInfo.Version + " 热键 " + _cfg.Hotkey + " 注册" + (hotkeyOk ? "成功" : "失败") +
                " 当前 " + _cfg.NativeWidth + "x" + _cfg.NativeHeight + " 目标 " + _cfg.TargetWidth + "x" + _cfg.TargetHeight);
            if (!hotkeyOk)
                Balloon("快捷键 " + _cfg.Hotkey + " 注册失败（可能被占用），请用托盘菜单操作", false);
            else
                Balloon("已启动 · 按 " + _cfg.Hotkey + " 切换 " + _cfg.TargetWidth + "×" + _cfg.TargetHeight + " 拉伸", true);
        }

        private void NormalizeState()
        {
            DisplayDevice d = Display.Primary();
            if (d == null) return;

            List<DisplayMode> modes = Display.GetModes(d.Name);
            if (Switch.PickRefresh(modes, _cfg.TargetWidth, _cfg.TargetHeight, 0) == 0)
            {
                DisplayMode pick = Switch.SuggestTarget(modes, d);
                if (pick.Width > 0)
                {
                    _cfg.TargetWidth = pick.Width;
                    _cfg.TargetHeight = pick.Height;
                    _cfg.TargetRefresh = pick.Hz;
                }
            }

            DisplayMode cur;
            if (Display.TryGetCurrent(d.Name, out cur))
            {
                double aspect = (double)cur.Width / Math.Max(1, cur.Height);
                bool atTarget = cur.Width == _cfg.TargetWidth && cur.Height == _cfg.TargetHeight;
                if (aspect >= 1.5 && !atTarget)
                {
                    _cfg.NativeWidth = cur.Width;
                    _cfg.NativeHeight = cur.Height;
                    _cfg.NativeRefresh = cur.Hz;
                    _cfg.NativeDevice = d.Name;
                }
            }
            _cfg.Save();
        }

        private bool RegisterHotkey()
        {
            int vk = 0x78;
            try { vk = (int)(Keys)Enum.Parse(typeof(Keys), _cfg.Hotkey, true); }
            catch { vk = 0x78; }
            return _hotkey.TryRegister(vk);
        }

        private void OnHotkey()
        {
            Log.Write("收到热键 " + _cfg.Hotkey);
            Toggle();
        }

        private void OnDoubleClick(object sender, EventArgs e) { Toggle(); }

        private void Toggle()
        {
            if (_busy) return;
            _busy = true;
            try
            {
                DisplayDevice d = Display.Primary();
                if (d == null) { Balloon("未检测到可用显示器", false); return; }
                SwitchResult r = Switch.Toggle(_cfg, d);
                Balloon(r.Message, r.Ok);
                if (r.Ok) { try { SystemSounds.Asterisk.Play(); } catch { } }
            }
            catch (Exception ex)
            {
                Log.Write("切换出错: " + ex);
                Balloon("切换出错：" + ex.Message, false);
            }
            finally { _busy = false; }
        }

        private void OnMenuPopup(object sender, EventArgs e)
        {
            _menu.MenuItems.Clear();

            DisplayDevice d = Display.Primary();
            string state = d == null
                ? "未检测到显示器"
                : string.Format("当前：{0}×{1} @ {2}Hz", d.Width, d.Height, d.Hz);
            var header = new MenuItem(state);
            header.Enabled = false;
            _menu.MenuItems.Add(header);

            var toggle = new MenuItem(string.Format("切换 {0}×{1} 拉伸 ({2})", _cfg.TargetWidth, _cfg.TargetHeight, _cfg.Hotkey), OnToggleClick);
            toggle.DefaultItem = true;
            _menu.MenuItems.Add(toggle);

            var resMenu = new MenuItem("4:3 分辨率");
            BuildResolutionMenu(resMenu, d);
            _menu.MenuItems.Add(resMenu);

            _menu.MenuItems.Add(new MenuItem("-"));

            var auto = new MenuItem("开机自动启动", OnStartupClick);
            auto.Checked = IsStartupEnabled();
            _menu.MenuItems.Add(auto);

            _menu.MenuItems.Add(new MenuItem("使用说明", OnHelpClick));
            _menu.MenuItems.Add(new MenuItem("-"));
            _menu.MenuItems.Add(new MenuItem("退出（恢复原分辨率）", OnExitClick));
        }

        private void BuildResolutionMenu(MenuItem parent, DisplayDevice d)
        {
            List<DisplayMode> modes = d == null ? new List<DisplayMode>() : Display.GetModes(d.Name);
            var resolutions = new List<DisplayMode>();
            var seen = new HashSet<string>();
            foreach (DisplayMode m in modes)
            {
                double a = (double)m.Width / Math.Max(1, m.Height);
                if (Math.Abs(a - 4.0 / 3.0) > 0.02) continue;
                string key = m.Width + "x" + m.Height;
                if (!seen.Add(key)) continue;
                resolutions.Add(m);
            }

            if (resolutions.Count == 0)
            {
                var none = new MenuItem("（该显示器没有 4:3 模式）");
                none.Enabled = false;
                parent.MenuItems.Add(none);
                return;
            }

            resolutions.Sort(delegate(DisplayMode x, DisplayMode y)
            {
                int c = (y.Width * y.Height).CompareTo(x.Width * x.Height);
                if (c != 0) return c;
                return x.Width.CompareTo(y.Width);
            });

            foreach (DisplayMode m in resolutions)
            {
                var item = new MenuItem(string.Format("{0}×{1}", m.Width, m.Height), OnResolutionClick);
                item.RadioCheck = true;
                item.Checked = m.Width == _cfg.TargetWidth && m.Height == _cfg.TargetHeight;
                item.Tag = m;
                parent.MenuItems.Add(item);
            }
        }

        private void OnResolutionClick(object sender, EventArgs e)
        {
            var item = sender as MenuItem;
            if (item == null || item.Tag == null) return;
            var m = (DisplayMode)item.Tag;
            _cfg.TargetWidth = m.Width;
            _cfg.TargetHeight = m.Height;
            _cfg.TargetRefresh = 0;
            _cfg.Save();

            DisplayDevice d = Display.Primary();
            DisplayMode cur;
            if (d != null && Display.TryGetCurrent(d.Name, out cur))
            {
                double aspect = (double)cur.Width / Math.Max(1, cur.Height);
                if (aspect < 1.5) { Toggle(); return; }
            }
            Balloon(string.Format("已选择 {0}×{1}，按 {2} 切换", m.Width, m.Height, _cfg.Hotkey), true);
        }

        private void OnToggleClick(object sender, EventArgs e) { Toggle(); }

        private void OnStartupClick(object sender, EventArgs e)
        {
            try
            {
                bool enable = !IsStartupEnabled();
                SetStartup(enable);
                Balloon(enable ? "已加入开机自启" : "已取消开机自启", true);
            }
            catch (Exception ex)
            {
                Balloon("设置开机自启失败：" + ex.Message, false);
            }
        }

        private void OnHelpClick(object sender, EventArgs e)
        {
            string tip =
                "分辨率切换 v" + AppInfo.Version + "\n\n" +
                "· 按 " + _cfg.Hotkey + " 在「" + _cfg.TargetWidth + "×" + _cfg.TargetHeight + " 4:3 真实拉伸」和原分辨率之间切换。\n" +
                "· 切换到 4:3 时会把画面全屏拉伸到显示器，不留黑边。\n" +
                "· 玩瓦洛兰特：先按 " + _cfg.Hotkey + " 切到 4:3，游戏里分辨率也选同样的 " + _cfg.TargetWidth + "×" + _cfg.TargetHeight + "、显示模式选「全屏」。\n" +
                "· 退出游戏后按 " + _cfg.Hotkey + " 恢复桌面分辨率。\n" +
                "· 如果切到 4:3 后是黑边而不是拉伸：在 NVIDIA 控制面板 →「调整桌面尺寸和位置」里把缩放模式设为「全屏」。\n" +
                "· 右键托盘图标可以换其他 4:3 分辨率、设置开机自启。\n" +
                "· 配置文件：DisplayStretch.ini（可改热键，如 F8 / F10）。";
            MessageBox.Show(tip, "分辨率切换 使用说明", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void OnExitClick(object sender, EventArgs e)
        {
            try
            {
                DisplayDevice d = Display.Primary();
                DisplayMode cur;
                if (d != null && Display.TryGetCurrent(d.Name, out cur))
                {
                    double aspect = (double)cur.Width / Math.Max(1, cur.Height);
                    if (aspect < 1.5) Switch.Restore(_cfg, d);
                }
            }
            catch (Exception ex) { Log.Write("退出恢复失败: " + ex.Message); }

            _tray.Visible = false;
            _hotkey.Dispose();
            _tray.Dispose();
            ExitThread();
        }

        private void Balloon(string text, bool ok)
        {
            try
            {
                _tray.BalloonTipTitle = "分辨率切换";
                _tray.BalloonTipText = text;
                _tray.BalloonTipIcon = ok ? ToolTipIcon.Info : ToolTipIcon.Warning;
                _tray.ShowBalloonTip(3500);
            }
            catch { }
        }

        private static bool IsStartupEnabled()
        {
            try
            {
                using (RegistryKey k = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", false))
                {
                    if (k == null) return false;
                    string v = k.GetValue(AppInfo.RunValueName) as string;
                    return !string.IsNullOrEmpty(v);
                }
            }
            catch { return false; }
        }

        private static void SetStartup(bool enable)
        {
            using (RegistryKey k = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true))
            {
                if (k == null) return;
                if (enable) k.SetValue(AppInfo.RunValueName, "\"" + Application.ExecutablePath + "\"");
                else k.DeleteValue(AppInfo.RunValueName, false);
            }
        }

        private static Icon CreateTrayIcon()
        {
            using (var bmp = new Bitmap(32, 32))
            {
                using (Graphics g = Graphics.FromImage(bmp))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.Clear(Color.Transparent);

                    var rect = new Rectangle(3, 8, 26, 18);
                    using (var fill = new SolidBrush(Color.FromArgb(235, 30, 110, 220)))
                        g.FillRectangle(fill, rect);
                    using (var pen = new Pen(Color.White, 2f))
                        g.DrawRectangle(pen, rect);

                    using (var pen = new Pen(Color.White, 2f))
                    {
                        g.DrawLine(pen, 9, 17, 23, 17);
                        g.DrawLine(pen, 9, 17, 12, 14);
                        g.DrawLine(pen, 9, 17, 12, 20);
                        g.DrawLine(pen, 23, 17, 20, 14);
                        g.DrawLine(pen, 23, 17, 20, 20);
                    }
                }
                IntPtr h = bmp.GetHicon();
                try
                {
                    using (Icon tmp = Icon.FromHandle(h))
                        return (Icon)tmp.Clone();
                }
                finally
                {
                    DestroyIcon(h);
                }
            }
        }

        [DllImport("user32.dll")]
        private static extern bool DestroyIcon(IntPtr handle);
    }

    internal static class Cli
    {
        public static int Run(string[] args)
        {
            string action = args[0].Trim().ToLowerInvariant();
            AppConfig cfg = AppConfig.Load();
            DisplayDevice d = Display.Primary();
            if (d == null)
            {
                Log.Write("cli: 未检测到显示器");
                return 2;
            }

            SwitchResult r;
            switch (action)
            {
                case "--toggle":
                    r = Switch.Toggle(cfg, d);
                    break;
                case "--4x3":
                case "--stretch":
                    r = Switch.To43(cfg, d);
                    break;
                case "--restore":
                case "--native":
                    r = Switch.Restore(cfg, d);
                    break;
                case "--status":
                    DisplayMode cur;
                    if (!Display.TryGetCurrent(d.Name, out cur)) return 1;
                    Log.Write(string.Format("status: {0} 当前 {1}x{2}@{3} scaling={4}",
                        d.Name, cur.Width, cur.Height, cur.Hz, Display.GetScaling(d.Name)));
                    return 0;
                default:
                    Log.Write("cli: 未知参数 " + action);
                    return 3;
            }

            Log.Write("cli " + action + " => " + (r.Ok ? "OK" : "FAIL") + " " + r.Message.Replace("\n", " "));
            return r.Ok ? 0 : 1;
        }
    }

    internal static class Program
    {
        [STAThread]
        private static void Main(string[] args)
        {
            if (args.Length > 0)
            {
                Environment.Exit(Cli.Run(args));
                return;
            }

            bool createdNew;
            using (var mutex = new Mutex(true, @"Local\DisplayStretch_4x3_Toggle", out createdNew))
            {
                if (!createdNew) return;

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.ThreadException += delegate(object s, ThreadExceptionEventArgs e) { Log.Write("UI 异常: " + e.Exception); };
                AppDomain.CurrentDomain.UnhandledException += delegate(object s, UnhandledExceptionEventArgs e) { Log.Write("未处理异常: " + e.ExceptionObject); };
                Application.Run(new TrayApp());
                GC.KeepAlive(mutex);
            }
        }
    }
}
