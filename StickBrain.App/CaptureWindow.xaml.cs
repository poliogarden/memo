using System;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Input;
using Microsoft.Extensions.Logging;
using StickBrain.Core;

namespace StickBrain.App;

/// <summary>
/// 빠른 1~3줄 메모 캡처 창
/// </summary>
public partial class CaptureWindow : Window
{
    private readonly INoteRepository _repository;
    private readonly ILogger<CaptureWindow> _logger;
    private readonly StickyWallViewModel _wall;

    public CaptureWindow(INoteRepository repository, ILogger<CaptureWindow> logger, StickyWallViewModel wall)
    {
        _repository = repository;
        _logger = logger;
        _wall = wall;
        InitializeComponent();
    }

    private async void SaveAndCloseAsync()
    {
        var text = InputBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(text))
        {
            Close();
            return;
        }

        var (appName, windowTitle) = GetActiveWindowInfo();

        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var note = new Note
        {
            Text = text,
            CreatedAt = now,
            UpdatedAt = now,
            AppName = appName,
            WindowTitle = windowTitle
        };

        try
        {
            await _repository.AddAsync(note);
            // 메인 월에도 즉시 반영
            _wall.AddNote(note);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "캡처 노트 저장 중 오류 발생");
        }

        Close();
    }

    private void InputBox_OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && (Keyboard.Modifiers & ModifierKeys.Shift) == 0)
        {
            e.Handled = true;
            SaveAndCloseAsync();
        }
        else if (e.Key == Key.Escape)
        {
            e.Handled = true;
            Close();
        }
    }

    protected override void OnContentRendered(EventArgs e)
    {
        base.OnContentRendered(e);
        InputBox.Focus();
    }

    private static (string? AppName, string? WindowTitle) GetActiveWindowInfo()
    {
        try
        {
            var hWnd = GetForegroundWindow();
            if (hWnd == IntPtr.Zero) return (null, null);

            var titleBuilder = new StringBuilder(1024);
            _ = GetWindowText(hWnd, titleBuilder, titleBuilder.Capacity);
            var windowTitle = titleBuilder.ToString();

            uint processId;
            _ = GetWindowThreadProcessId(hWnd, out processId);

            using var process = System.Diagnostics.Process.GetProcessById((int)processId);
            var appName = process.ProcessName;

            return (appName, windowTitle);
        }
        catch
        {
            // 메타데이터는 선택 사항이므로 실패해도 무시
            return (null, null);
        }
    }

    #region Win32

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    #endregion
}

