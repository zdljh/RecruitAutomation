using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using CefSharp;
using CefSharp.Wpf;
using RecruitAutomation.Core.License;

namespace RecruitAutomation.App
{
    public partial class App : Application
    {
        private static string _logFile = "";
        private static string _runtimeLogFile = "";
        private static CefSettings? _cefSettings;
        private static bool _isCefInitialized = false;

        [STAThread]
        public static void Main()
        {
            var appDir = AppDomain.CurrentDomain.BaseDirectory;
            _logFile = Path.Combine(appDir, "startup.log");
            _runtimeLogFile = Path.Combine(appDir, "runtime.log");

            // 1. 立即注册全局异常处理
            RegisterGlobalExceptionHandlers();

            try
            {
                WriteLog("========== 程序启动 (白图 AI 4.0 规范) ==========");
                
                var app = new App();
                app.InitializeComponent();
                
                // 2. 异步初始化核心组件，不阻塞 UI 线程
                Task.Run(() => InitializeCoreComponentsAsync());

                app.Run();
            }
            catch (Exception ex)
            {
                WriteLog($"【FATAL】Main 崩溃: {ex}");
                MessageBox.Show($"程序启动失败: {ex.Message}", "致命错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private static void RegisterGlobalExceptionHandlers()
        {
            AppDomain.CurrentDomain.UnhandledException += (s, e) => 
                LogException("AppDomain", e.ExceptionObject as Exception);
            
            TaskScheduler.UnobservedTaskException += (s, e) => 
            {
                LogException("Task", e.Exception);
                e.SetObserved();
            };
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            this.DispatcherUnhandledException += (s, args) => 
            {
                LogException("Dispatcher", args.Exception);
                args.Handled = true; // 阻止闪退
            };
        }

        private static async Task InitializeCoreComponentsAsync()
        {
            try
            {
                WriteLog("开始异步初始化核心组件...");
                
                // 验证授权
                var result = LicenseGuard.Instance.Validate();
                WriteLog($"授权验证结果: {result.IsValid}");

                // 初始化 CefSharp
                _isCefInitialized = await Task.Run(() => InitializeCefSharpInternal());
                WriteLog($"CefSharp 初始化状态: {_isCefInitialized}");
            }
            catch (Exception ex)
            {
                WriteLog($"异步初始化异常: {ex.Message}");
            }
        }

        private static bool InitializeCefSharpInternal()
        {
            try
            {
                if (Cef.IsInitialized) return true;

                var appDir = AppDomain.CurrentDomain.BaseDirectory;
                var cachePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RecruitAutomation", "cache");
                
                _cefSettings = new CefSettings
                {
                    CachePath = cachePath,
                    MultiThreadedMessageLoop = true,
                    LogSeverity = LogSeverity.Disable,
                    BrowserSubprocessPath = Path.Combine(appDir, "CefSharp.BrowserSubprocess.exe")
                };

                _cefSettings.CefCommandLineArgs.Add("disable-gpu", "1");
                _cefSettings.CefCommandLineArgs.Add("no-sandbox", "1");

                return Cef.Initialize(_cefSettings, performDependencyCheck: true, browserProcessHandler: null);
            }
            catch (Exception ex)
            {
                WriteLog($"Cef.Initialize 崩溃: {ex.Message}");
                return false;
            }
        }

        public static void EnsureCefInitialized()
        {
            if (!_isCefInitialized)
            {
                InitializeCefSharpInternal();
            }
        }

        public static void WriteLog(string message)
        {
            try
            {
                var logMsg = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {message}{Environment.NewLine}";
                File.AppendAllText(_logFile, logMsg);
            }
            catch { }
        }

        private static void LogException(string source, Exception? ex)
        {
            var msg = $"【{source} 异常】{ex?.Message}{Environment.NewLine}{ex?.StackTrace}";
            WriteLog(msg);
            try { File.AppendAllText(_runtimeLogFile, msg + Environment.NewLine); } catch { }
        }
    }
}
