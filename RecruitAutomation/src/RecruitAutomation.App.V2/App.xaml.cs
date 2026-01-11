using System;
using System.IO;
using System.Runtime.ExceptionServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using CefSharp;
using CefSharp.Wpf;

namespace RecruitAutomation.App.V2
{
    /// <summary>
    /// App.V2 应用程序入口
    /// 关键：CefSharp 必须在 Main 函数中最早初始化！
    /// </summary>
    public partial class App : Application
    {
        private static readonly string DataRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "RecruitAutomation");

        [STAThread]
        public static void Main()
        {
            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
            {
                WriteCrashLog("UnhandledException", e.ExceptionObject?.ToString() ?? "Unknown");
            };

            try
            {
                WriteCrashLog("Main", "========== V2 程序启动 ==========");
                EnsureDataDirectoryExists();
                
                WriteCrashLog("Main", "开始初始化 CefSharp...");
                InitializeCefSharp();
                WriteCrashLog("Main", "CefSharp 初始化完成");

                WriteCrashLog("Main", "启动 WPF 应用程序...");
                var app = new App();
                app.InitializeComponent();
                app.Run();
            }
            catch (Exception ex)
            {
                WriteCrashLog("Main", $"程序启动失败: {ex}");
                MessageBox.Show($"程序启动失败:\n{ex.Message}", "启动错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private static void InitializeCefSharp()
        {
            if (Cef.IsInitialized) return;

            var appDir = AppDomain.CurrentDomain.BaseDirectory;
            var cachePath = Path.Combine(DataRoot, "browser", "cache");
            var logPath = Path.Combine(DataRoot, "logs", "cef_v2.log");
            var subprocessPath = Path.Combine(appDir, "CefSharp.BrowserSubprocess.exe");
            var localesPath = Path.Combine(appDir, "locales");

            if (!Directory.Exists(cachePath)) Directory.CreateDirectory(cachePath);
            if (!Directory.Exists(Path.GetDirectoryName(logPath)!)) Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);

            var settings = new CefSettings
            {
                CachePath = cachePath,
                RootCachePath = cachePath,
                LogSeverity = LogSeverity.Warning,
                LogFile = logPath,
                MultiThreadedMessageLoop = true,
                Locale = "zh-CN",
                AcceptLanguageList = "zh-CN,zh,en-US,en",
                UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/119.0.0.0 Safari/537.36",
                BrowserSubprocessPath = subprocessPath,
                ResourcesDirPath = appDir,
                LocalesDirPath = localesPath,
            };

            settings.CefCommandLineArgs.Add("disable-gpu", "1");
            settings.CefCommandLineArgs.Add("disable-gpu-compositing", "1");
            settings.CefCommandLineArgs.Add("no-sandbox", "1");
            settings.CefCommandLineArgs.Add("enable-begin-frame-scheduling", "1");
            settings.CefCommandLineArgs.Add("disable-extensions", "1");
            settings.CefCommandLineArgs.Add("ignore-certificate-errors", "1");

            if (!Cef.Initialize(settings, performDependencyCheck: false, browserProcessHandler: null))
                throw new InvalidOperationException("Cef.Initialize 返回 false");
        }

        private static void EnsureDataDirectoryExists()
        {
            if (!Directory.Exists(DataRoot)) Directory.CreateDirectory(DataRoot);
            var logsDir = Path.Combine(DataRoot, "logs");
            if (!Directory.Exists(logsDir)) Directory.CreateDirectory(logsDir);
            var browserDir = Path.Combine(DataRoot, "browser");
            if (!Directory.Exists(browserDir)) Directory.CreateDirectory(browserDir);
        }

        private static void WriteCrashLog(string source, string message)
        {
            try
            {
                var crashLog = Path.Combine(DataRoot, "crash_v2.log");
                var dir = Path.GetDirectoryName(crashLog);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
                File.AppendAllText(crashLog, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{source}] {message}\n");
            }
            catch { }
        }

        private void Application_Startup(object sender, StartupEventArgs e)
        {
            DispatcherUnhandledException += (s, args) =>
            {
                WriteCrashLog("DispatcherUnhandledException", args.Exception.ToString());
                MessageBox.Show($"程序发生错误:\n{args.Exception.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                args.Handled = true;
            };
            
            // 显示主窗口
            var mainWindow = new MainWindow();
            mainWindow.Show();
        }

        private void Application_Exit(object sender, ExitEventArgs e)
        {
            try
            {
                Browser.BrowserInstanceManager.Instance.Dispose();
                if (Cef.IsInitialized) Cef.Shutdown();
            }
            catch { }
            base.OnExit(e);
        }
    }
}
