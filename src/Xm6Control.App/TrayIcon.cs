using System.Runtime.InteropServices;

namespace Xm6Control.App;

/// <summary>Minimal Win32 notification-area icon for the unpackaged WinUI app.</summary>
internal sealed class TrayIcon : IDisposable
{
    private const uint WM_TRAY = 0x8001;
    private const uint WM_COMMAND = 0x0111;
    private const int WM_LBUTTONUP = 0x0202;
    private const int WM_LBUTTONDBLCLK = 0x0203;
    private const int WM_RBUTTONUP = 0x0205;
    private const int WM_CONTEXTMENU = 0x007B;
    private const uint NIM_ADD = 0;
    private const uint NIM_DELETE = 2;
    private const uint NIF_MESSAGE = 1;
    private const uint NIF_ICON = 2;
    private const uint NIF_TIP = 4;
    private const uint MF_STRING = 0;
    private const uint MF_SEPARATOR = 0x0800;
    private const uint TPM_RIGHTBUTTON = 0x0002;
    private const uint TPM_RETURNCMD = 0x0100;
    private const uint TPM_NONOTIFY = 0x0080;
    private const int IDI_APPLICATION = 32512;
    private const uint IMAGE_ICON = 1;
    private const uint LR_LOADFROMFILE = 0x0010;
    private const uint LR_DEFAULTSIZE = 0x0040;
    private const int ID_OVERVIEW = 1;
    private const int ID_LISTENING = 2;
    private const int ID_EXIT = 3;

    private readonly string _className = $"Xm6ControlTray_{Guid.NewGuid():N}";
    private readonly WndProcDelegate _wndProc;
    private IntPtr _window;
    private IntPtr _iconHandle;
    private bool _ownsIcon;
    private bool _iconAdded;

    public TrayIcon()
    {
        _wndProc = WindowProcedure;
        try
        {
            _iconHandle = LoadTrayIcon();
            CreateMessageWindow();
            AddIcon();
            IsAvailable = _window != IntPtr.Zero && _iconAdded;
        }
        catch
        {
            Dispose();
        }
    }

    public event Action? OverviewRequested;
    public event Action? ListeningRequested;
    public event Action? ExitRequested;

    public bool IsAvailable { get; private set; }

    public void Dispose()
    {
        if (_iconAdded)
        {
            var data = CreateIconData();
            Shell_NotifyIconW(NIM_DELETE, ref data);
            _iconAdded = false;
        }

        if (_window != IntPtr.Zero)
        {
            DestroyWindow(_window);
            _window = IntPtr.Zero;
        }

        UnregisterClassW(_className, GetModuleHandleW(null));
        if (_ownsIcon && _iconHandle != IntPtr.Zero)
        {
            DestroyIcon(_iconHandle);
        }

        _iconHandle = IntPtr.Zero;
        _ownsIcon = false;
        IsAvailable = false;
    }

    private IntPtr LoadTrayIcon()
    {
        var iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "app.ico");
        if (File.Exists(iconPath))
        {
            var handle = LoadImageW(
                IntPtr.Zero,
                iconPath,
                IMAGE_ICON,
                0,
                0,
                LR_LOADFROMFILE | LR_DEFAULTSIZE);
            if (handle != IntPtr.Zero)
            {
                _ownsIcon = true;
                return handle;
            }
        }

        return LoadIconW(IntPtr.Zero, (IntPtr)IDI_APPLICATION);
    }

    private void CreateMessageWindow()
    {
        var instance = GetModuleHandleW(null);
        var windowClass = new WNDCLASSEX
        {
            cbSize = (uint)Marshal.SizeOf<WNDCLASSEX>(),
            lpfnWndProc = Marshal.GetFunctionPointerForDelegate(_wndProc),
            hInstance = instance,
            lpszClassName = _className,
        };

        if (RegisterClassExW(ref windowClass) == 0)
        {
            return;
        }

        _window = CreateWindowExW(
            0,
            _className,
            string.Empty,
            0,
            0,
            0,
            0,
            0,
            IntPtr.Zero,
            IntPtr.Zero,
            instance,
            IntPtr.Zero);
    }

    private void AddIcon()
    {
        if (_window == IntPtr.Zero)
        {
            return;
        }

        var data = CreateIconData();
        _iconAdded = Shell_NotifyIconW(NIM_ADD, ref data);
    }

    private NOTIFYICONDATA CreateIconData()
    {
        return new NOTIFYICONDATA
        {
            cbSize = (uint)Marshal.SizeOf<NOTIFYICONDATA>(),
            hWnd = _window,
            uID = 1,
            uFlags = NIF_MESSAGE | NIF_ICON | NIF_TIP,
            uCallbackMessage = WM_TRAY,
            hIcon = _iconHandle,
            szTip = "XM6 Control",
            szInfo = string.Empty,
            szInfoTitle = string.Empty,
        };
    }

    private IntPtr WindowProcedure(IntPtr hWnd, uint message, IntPtr wParam, IntPtr lParam)
    {
        if (message == WM_TRAY)
        {
            var mouseMessage = (int)(lParam.ToInt64() & 0xFFFF);
            switch (mouseMessage)
            {
                case WM_LBUTTONUP:
                case WM_LBUTTONDBLCLK:
                    OverviewRequested?.Invoke();
                    return IntPtr.Zero;
                case WM_RBUTTONUP:
                case WM_CONTEXTMENU:
                    ShowContextMenu();
                    return IntPtr.Zero;
            }
        }

        if (message == WM_COMMAND)
        {
            HandleCommand((int)(wParam.ToInt64() & 0xFFFF));
            return IntPtr.Zero;
        }

        return DefWindowProcW(hWnd, message, wParam, lParam);
    }

    private void ShowContextMenu()
    {
        var menu = CreatePopupMenu();
        if (menu == IntPtr.Zero)
        {
            return;
        }

        try
        {
            AppendMenuW(menu, MF_STRING, ID_OVERVIEW, "概览");
            AppendMenuW(menu, MF_STRING, ID_LISTENING, "聆听模式");
            AppendMenuW(menu, MF_SEPARATOR, 0, null);
            AppendMenuW(menu, MF_STRING, ID_EXIT, "退出");

            GetCursorPos(out var point);
            SetForegroundWindow(_window);
            var command = TrackPopupMenuEx(
                menu,
                TPM_RIGHTBUTTON | TPM_RETURNCMD | TPM_NONOTIFY,
                point.X,
                point.Y,
                _window,
                IntPtr.Zero);
            PostMessageW(_window, 0, IntPtr.Zero, IntPtr.Zero);
            HandleCommand(command);
        }
        finally
        {
            DestroyMenu(menu);
        }
    }

    private void HandleCommand(int command)
    {
        switch (command)
        {
            case ID_OVERVIEW:
                OverviewRequested?.Invoke();
                break;
            case ID_LISTENING:
                ListeningRequested?.Invoke();
                break;
            case ID_EXIT:
                ExitRequested?.Invoke();
                break;
        }
    }

    private delegate IntPtr WndProcDelegate(IntPtr hWnd, uint message, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WNDCLASSEX
    {
        public uint cbSize;
        public uint style;
        public IntPtr lpfnWndProc;
        public int cbClsExtra;
        public int cbWndExtra;
        public IntPtr hInstance;
        public IntPtr hIcon;
        public IntPtr hCursor;
        public IntPtr hbrBackground;
        public string? lpszMenuName;
        public string lpszClassName;
        public IntPtr hIconSm;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NOTIFYICONDATA
    {
        public uint cbSize;
        public IntPtr hWnd;
        public uint uID;
        public uint uFlags;
        public uint uCallbackMessage;
        public IntPtr hIcon;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string szTip;

        public uint dwState;
        public uint dwStateMask;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string szInfo;

        public uint uTimeoutOrVersion;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
        public string szInfoTitle;

        public uint dwInfoFlags;
        public Guid guidItem;
        public IntPtr hBalloonIcon;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X;
        public int Y;
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern ushort RegisterClassExW(ref WNDCLASSEX windowClass);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr CreateWindowExW(
        uint exStyle,
        string className,
        string windowName,
        uint style,
        int x,
        int y,
        int width,
        int height,
        IntPtr parent,
        IntPtr menu,
        IntPtr instance,
        IntPtr parameter);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyWindow(IntPtr hWnd);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool UnregisterClassW(string className, IntPtr instance);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr DefWindowProcW(IntPtr hWnd, uint message, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr LoadIconW(IntPtr instance, IntPtr iconName);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr LoadImageW(
        IntPtr instance,
        string name,
        uint type,
        int width,
        int height,
        uint flags);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(IntPtr icon);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out POINT point);

    [DllImport("user32.dll")]
    private static extern IntPtr CreatePopupMenu();

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern bool AppendMenuW(IntPtr menu, uint flags, int id, string? text);

    [DllImport("user32.dll")]
    private static extern int TrackPopupMenuEx(
        IntPtr menu,
        uint flags,
        int x,
        int y,
        IntPtr hWnd,
        IntPtr parameters);

    [DllImport("user32.dll")]
    private static extern bool DestroyMenu(IntPtr menu);

    [DllImport("user32.dll")]
    private static extern bool PostMessageW(IntPtr hWnd, uint message, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr GetModuleHandleW(string? moduleName);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool Shell_NotifyIconW(uint message, ref NOTIFYICONDATA data);
}
