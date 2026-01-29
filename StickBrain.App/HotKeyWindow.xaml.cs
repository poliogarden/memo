using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using Microsoft.Extensions.DependencyInjection;

namespace StickBrain.App;

/// <summary>
/// Ctrl+Shift+Space 전역 단축키를 처리하기 위한 숨겨진 윈도우
/// </summary>
public partial class HotKeyWindow : Window, IDisposable
{
    private readonly IServiceProvider _services;
    private IntPtr _handle;
    private HwndSource? _source;
    private bool _registered;

    private const int WM_HOTKEY = 0x0312;
    private const int MOD_CONTROL = 0x0002;
    private const int MOD_SHIFT = 0x0004;
    private const int VK_SPACE = 0x20;
    private const int HOTKEY_ID = 0x1001;

    public HotKeyWindow(IServiceProvider services)
    {
        _services = services;
        InitializeComponent();
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        _handle = new WindowInteropHelper(this).Handle;
        _source = HwndSource.FromHwnd(_handle);
        if (_source != null)
        {
            _source.AddHook(WndProc);
        }

        RegisterHotKey();
    }

    private void RegisterHotKey()
    {
        if (_registered) return;

        if (!RegisterHotKey(_handle, HOTKEY_ID, MOD_CONTROL | MOD_SHIFT, VK_SPACE))
        {
            // TODO: 로깅/알림 추가 (중복 등록 등)
        }
        else
        {
            _registered = true;
        }
    }

    private void UnregisterHotKey()
    {
        if (!_registered) return;

        UnregisterHotKey(_handle, HOTKEY_ID);
        _registered = false;
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_HOTKEY && wParam.ToInt32() == HOTKEY_ID)
        {
            OnHotKeyPressed();
            handled = true;
        }

        return IntPtr.Zero;
    }

    private void OnHotKeyPressed()
    {
        // CaptureWindow를 새로 띄운다.
        var capture = _services.GetRequiredService<CaptureWindow>();
        capture.Show();
        capture.Activate();
    }

    public void Dispose()
    {
        UnregisterHotKey();
        if (_source != null)
        {
            _source.RemoveHook(WndProc);
        }
    }

    #region Win32

    [DllImport("user32.dll")]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, int fsModifiers, int vk);

    [DllImport("user32.dll")]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    #endregion
}

