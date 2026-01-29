using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using StickBrain.Core;
using StickBrain.Data;

namespace StickBrain.App;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    private IHost? _host;
    private HotKeyWindow? _hotKeyWindow;

    public App()
    {
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
    }

    private static void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        ReportException(e.Exception);
        e.Handled = true;
        Application.Current.Shutdown(1);
    }

    private static void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
        {
            ReportException(ex);
        }
    }

    private static void ReportException(Exception ex)
    {
        var message = ex.ToString();
        try
        {
            var logDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "StickBrain");
            Directory.CreateDirectory(logDir);
            var logPath = Path.Combine(logDir, "error.log");
            File.WriteAllText(logPath, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss}\r\n{message}\r\n\r\n");
        }
        catch { /* ignore */ }

        MessageBox.Show(
            $"오류가 발생해 앱을 종료합니다.\r\n\r\n{ex.Message}\r\n\r\n자세한 내용: %LocalAppData%\\StickBrain\\error.log",
            "StickBrain 오류",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        try
        {
            _host = Host.CreateDefaultBuilder(e.Args)
                .ConfigureLogging(builder =>
                {
                    builder.ClearProviders();
                    builder.AddDebug();
                })
                .ConfigureServices(ConfigureServices)
                .Build();

            _host.Start();

            // StickyWallWindow를 메인으로 띄우고, 전역 단축키용 보조 히든 윈도우 생성
            var services = _host.Services;
            var wall = services.GetRequiredService<StickyWallWindow>();
            wall.Show();

            _hotKeyWindow = new HotKeyWindow(services);
            _hotKeyWindow.Show();
            _hotKeyWindow.Hide(); // 사용자는 보지 못하게 숨김
        }
        catch (Exception ex)
        {
            ReportException(ex);
            Shutdown(1);
        }
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        var dbPath = System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "StickBrain",
            "stickbrain.db");

        System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(dbPath)!);

        services.AddLogging();

        services.AddSingleton<INoteRepository>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<SqliteNoteRepository>>();
            return new SqliteNoteRepository(dbPath, logger);
        });

        services.AddSingleton<INoteStackGrouper, SimpleTokenBasedNoteStackGrouper>();

        // WPF 윈도우/뷰모델
        services.AddSingleton<StickyWallWindow>();
        // TODO: Search & Stack 윈도우 구현 후 DI에 등록
        services.AddTransient<CaptureWindow>();
        services.AddSingleton<StickyWallViewModel>();
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        if (_hotKeyWindow is not null)
        {
            _hotKeyWindow.Dispose();
        }

        if (_host is not null)
        {
            await _host.StopAsync(TimeSpan.FromSeconds(2));
            _host.Dispose();
        }

        base.OnExit(e);
    }
}

