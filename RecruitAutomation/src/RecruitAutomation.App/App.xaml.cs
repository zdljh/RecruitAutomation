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
    /// <summary>
    /// 应用程序入口 - 防闪退终极版
    /// 【核心原则】
    /// 1. 三层全局异常捕获，所有异常只记录日志，绝不退出程序
    /// 2. CefSharp 生命周期锁定，防止 GC 回收
    /// 3. 安全模式支持，上次崩溃后进入安全模式
    /// </summary>
    public partial class App : Application
    {
        private static string _logFile = "";
        private static string _runtimeLogFile = "";
        private static string _crashFlagFile = "";
        private static bool _isSafeMode = false;
        
        // 【关键】静态引用防止 GC 回收
        private static CefSettings? _cefSettings;
        
        [STAThread]
        public static void Main()
        {
            // 1. 最早设置日志文件路径
            var appDir = AppDomain.CurrentDomain.BaseDirectory;
            _logFile = Path.Combine(appDir, "startup.log");
            _runtimeLogFile = Path.Combine(appDir, "runtime.log");
            _crashFlagFile = Path.Combine(appDir, "crash.flag");
            
            try 
            { 
                File.WriteAllText(_logFile, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [Main] ========== 程序启动 ==========\n"); 
                File.WriteAllText(_runtimeLogFile, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [Main] ========== 运行时日志 ==========\n"); 
            } 
            catch { }
            
            // 2. 【最关键】注册全局异常处理 - 必须在最早期
            AppDomain.CurrentDomain.UnhandledException += OnAppDomainUnhandledException;
            TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
            
            // 3. 检查是否需要进入安全模式
            _isSafeMode = CheckSafeMode();
            WriteLog($"安全模式检查: {_isSafeMode}");
            
            // 4. 设置崩溃标记（正常退出时会删除）
            SetCrashFlag();
            
            try
            {
                if (_isSafeMode)
                {
                    // 安全模式：不初始化 CefSharp，只显示安全模式窗口
                    WriteLog("进入安全模式...");
                    RunSafeMode();
                }
                else
                {
                    // 正常模式
                    RunNormalMode();
                }
            }
            catch (Exception ex)
            {
                WriteLog($"【FATAL】Main 异常: {ex}");
                WriteRuntimeLog($"【FATAL】Main 异常: {ex}");
                ShowFatalError($"程序启动失败:\n{ex.Message}");
            }
            finally
            {
                // 正常退出，删除崩溃标记
                ClearCrashFlag();
                SafeCleanup();
            }
        }
        
        /// <summary>
        /// 正常模式启动
        /// </summary>
        private static void RunNormalMode()
        {
            // 1. CefSharp 初始化
            WriteLog("步骤1: 初始化 CefSharp...");
            var cefResult = InitializeCefSharp();
            WriteLog($"步骤1: CefSharp 初始化结果: {cefResult}");
            
            if (!cefResult)
            {
                WriteLog("【警告】CefSharp 初始化失败，但继续启动程序");
            }
            
            // 2. 创建 WPF Application
            WriteLog("步骤2: 创建 WPF Application...");
            var app = new App();
            
            // 3. 注册 WPF 级别的异常处理
            app.DispatcherUnhandledException += OnDispatcherUnhandledException;
            
            // 4. 初始化 XAML 资源
            WriteLog("步骤3: 初始化 XAML 资源...");
            app.InitializeComponent();
            
            // 5. 创建主窗口
            WriteLog("步骤4: 创建主窗口...");
            Window startupWindow = CreateStartupWindow();
            
            if (startupWindow == null)
            {
                WriteLog("【致命】无法创建启动窗口，创建错误窗口");
                startupWindow = CreateErrorWindow("无法创建启动窗口");
            }
            
            // 6. 设置 MainWindow 并显示
            app.MainWindow = startupWindow;
            WriteLog("步骤5: 显示主窗口...");
            startupWindow.Show();
            WriteLog("步骤5: 主窗口已显示");
            
            // 7. 进入消息循环
            WriteLog("步骤6: 进入消息循环...");
            app.Run();
            
            WriteLog("程序正常退出");
        }
        
        /// <summary>
        /// 安全模式启动
        /// </summary>
        private static void RunSafeMode()
        {
            var app = new App();
            app.DispatcherUnhandledException += OnDispatcherUnhandledException;
            app.InitializeComponent();
            
            var safeWindow = CreateSafeModeWindow();
            app.MainWindow = safeWindow;
            safeWindow.Show();
            app.Run();
        }
        
        /// <summary>
        /// 检查是否需要进入安全模式
        /// </summary>
        private static bool CheckSafeMode()
        {
            try
            {
                // 检查崩溃标记文件
                if (File.Exists(_crashFlagFile))
                {
                    WriteLog("检测到崩溃标记文件，上次异常退出");
                    return true;
                }
                
                // 检查日志中是否有 FATAL 错误
                if (File.Exists(_logFile))
                {
                    var logContent = File.ReadAllText(_logFile);
                    if (logContent.Contains("【FATAL】"))
                    {
                        WriteLog("检测到日志中有 FATAL 错误");
                        return true;
                    }
                }
                
                return false;
            }
            catch
            {
                return false;
            }
        }
        
        /// <summary>
        /// 设置崩溃标记
        /// </summary>
        private static void SetCrashFlag()
        {
            try { File.WriteAllText(_crashFlagFile, DateTime.Now.ToString("o")); } catch { }
        }
        
        /// <summary>
        /// 清除崩溃标记
        /// </summary>
        private static void ClearCrashFlag()
        {
            try { if (File.Exists(_crashFlagFile)) File.Delete(_crashFlagFile); } catch { }
        }
        
        /// <summary>
        /// 创建安全模式窗口
        /// </summary>
        private static Window CreateSafeModeWindow()
        {
            var window = new Window
            {
                Title = "RecruitAutomation - 安全模式",
                Width = 500,
                Height = 350,
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
                Background = System.Windows.Media.Brushes.White
            };
            
            var panel = new System.Windows.Controls.StackPanel
            {
                Margin = new Thickness(30),
                VerticalAlignment = VerticalAlignment.Center
            };
            
            panel.Children.Add(new System.Windows.Controls.TextBlock
            {
                Text = "⚠️ 安全模式",
                FontSize = 24,
                FontWeight = FontWeights.Bold,
                Foreground = System.Windows.Media.Brushes.OrangeRed,
                Margin = new Thickness(0, 0, 0, 20)
            });
            
            panel.Children.Add(new System.Windows.Controls.TextBlock
            {
                Text = "程序检测到上次运行时发生了崩溃。\n为了保护您的数据，程序已进入安全模式。\n\n在安全模式下，浏览器和自动化功能已禁用。",
                FontSize = 14,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 20)
            });
            
            var btnPanel = new System.Windows.Controls.StackPanel
            {
                Orientation = System.Windows.Controls.Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            
            var btnNormal = new System.Windows.Controls.Button
            {
                Content = "尝试正常启动",
                Padding = new Thickness(20, 10, 20, 10),
                Margin = new Thickness(0, 0, 10, 0),
                Background = System.Windows.Media.Brushes.DodgerBlue,
                Foreground = System.Windows.Media.Brushes.White
            };
            btnNormal.Click += (s, e) =>
            {
                ClearCrashFlag();
                try { File.WriteAllText(_logFile, ""); } catch { } // 清空日志
                MessageBox.Show("崩溃标记已清除，请重新启动程序。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                window.Close();
            };
            
            var btnViewLog = new System.Windows.Controls.Button
            {
                Content = "查看日志",
                Padding = new Thickness(20, 10, 20, 10),
                Margin = new Thickness(0, 0, 10, 0)
            };
            btnViewLog.Click += (s, e) =>
            {
                try { System.Diagnostics.Process.Start("notepad.exe", _runtimeLogFile); } catch { }
            };
            
            var btnExit = new System.Windows.Controls.Button
            {
                Content = "退出",
                Padding = new Thickness(20, 10, 20, 10)
            };
            btnExit.Click += (s, e) => window.Close();
            
            btnPanel.Children.Add(btnNormal);
            btnPanel.Children.Add(btnViewLog);
            btnPanel.Children.Add(btnExit);
            panel.Children.Add(btnPanel);
            
            window.Content = panel;
            return window;
        }
        
        /// <summary>
        /// 创建启动窗口
        /// </summary>
        private static Window CreateStartupWindow()
        {
            try
            {
                WriteLog("验证授权...");
                LicenseValidationResult result;
                
                try
                {
                    result = LicenseGuard.Instance.Validate();
                }
                catch (Exception ex)
                {
                    WriteLog($"授权验证异常: {ex.Message}");
                    return new LicenseWindow();
                }
                
                WriteLog($"授权结果: {result.IsValid}");

                if (result.IsValid)
                {
                    WriteLog("创建 MainWindow...");
                    var mainWindow = new MainWindow();
                    WriteLog("MainWindow 创建成功");
                    return mainWindow;
                }
                else
                {
                    WriteLog("创建 LicenseWindow...");
                    var licenseWindow = new LicenseWindow();
                    WriteLog("LicenseWindow 创建成功");
                    return licenseWindow;
                }
            }
            catch (Exception ex)
            {
                WriteLog($"创建窗口异常: {ex}");
                return CreateErrorWindow($"创建窗口失败:\n{ex.Message}");
            }
        }
        
        /// <summary>
        /// 创建错误窗口
        /// </summary>
        private static Window CreateErrorWindow(string message)
        {
            return new Window
            {
                Title = "启动错误",
                Width = 600,
                Height = 400,
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
                Content = new System.Windows.Controls.TextBox
                {
                    Text = $"{message}\n\n详细日志请查看:\n{_logFile}\n{_runtimeLogFile}",
                    IsReadOnly = true,
                    TextWrapping = TextWrapping.Wrap,
                    VerticalScrollBarVisibility = System.Windows.Controls.ScrollBarVisibility.Auto,
                    Margin = new Thickness(10)
                }
            };
        }
        
        /// <summary>
        /// 显示致命错误
        /// </summary>
        private static void ShowFatalError(string message)
        {
            try
            {
                var errorApp = new Application();
                var errorWindow = CreateErrorWindow(message);
                errorApp.MainWindow = errorWindow;
                errorWindow.Show();
                errorApp.Run();
            }
            catch { }
        }
        
        /// <summary>
        /// 初始化 CefSharp - 【关键】使用静态引用防止 GC
        /// </summary>
        private static bool InitializeCefSharp()
        {
            try
            {
                if (Cef.IsInitialized)
                {
                    WriteLog("CefSharp 已初始化");
                    return true;
                }
                
                var appDir = AppDomain.CurrentDomain.BaseDirectory;
                var dataRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RecruitAutomation");
                var cachePath = Path.Combine(dataRoot, "browser", "cache");
                var logPath = Path.Combine(dataRoot, "logs", "cef.log");
                var localesPath = Path.Combine(appDir, "locales");
                
                // 创建目录
                try { Directory.CreateDirectory(cachePath); } catch { }
                try { Directory.CreateDirectory(Path.GetDirectoryName(logPath)!); } catch { }
                
                // 检查关键文件
                var libcefPath = Path.Combine(appDir, "libcef.dll");
                if (!File.Exists(libcefPath))
                {
                    WriteLog($"缺少 libcef.dll: {libcefPath}");
                    return false;
                }
                
                if (!Directory.Exists(localesPath))
                {
                    WriteLog($"缺少 locales 目录: {localesPath}");
                    return false;
                }
                
                // 【关键】使用静态字段保存 CefSettings，防止 GC 回收
                _cefSettings = new CefSettings
                {
                    CachePath = cachePath,
                    RootCachePath = cachePath,
                    LogSeverity = LogSeverity.Error,
                    LogFile = logPath,
                    MultiThreadedMessageLoop = true,
                    Locale = "zh-CN",
                    BrowserSubprocessPath = Path.Combine(appDir, "CefSharp.BrowserSubprocess.exe"),
                    ResourcesDirPath = appDir,
                    LocalesDirPath = localesPath,
                };

                // 禁用 GPU 避免兼容性问题
                _cefSettings.CefCommandLineArgs.Add("disable-gpu", "1");
                _cefSettings.CefCommandLineArgs.Add("disable-gpu-compositing", "1");
                _cefSettings.CefCommandLineArgs.Add("no-sandbox", "1");
                _cefSettings.CefCommandLineArgs.Add("disable-extensions", "1");
                
                WriteLog("调用 Cef.Initialize...");
                var result = Cef.Initialize(_cefSettings, performDependencyCheck: false, browserProcessHandler: null);
                WriteLog($"Cef.Initialize 返回: {result}");
                return result;
            }
            catch (Exception ex)
            {
                WriteLog($"CefSharp 初始化异常: {ex}");
                return false;
            }
        }
        
        /// <summary>
        /// 【关键】AppDomain 未处理异常 - 只记录，绝不退出
        /// </summary>
        private static void OnAppDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            var ex = e.ExceptionObject as Exception;
            var message = $"【AppDomain异常】\n" +
                          $"时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}\n" +
                          $"线程: {Thread.CurrentThread.ManagedThreadId}\n" +
                          $"IsTerminating: {e.IsTerminating}\n" +
                          $"异常类型: {ex?.GetType().FullName}\n" +
                          $"Message: {ex?.Message}\n" +
                          $"StackTrace:\n{ex?.StackTrace}\n" +
                          $"InnerException: {ex?.InnerException?.Message}\n";
            
            WriteLog(message);
            WriteRuntimeLog(message);
            
            // 【重要】不调用 Environment.Exit 或 Application.Shutdown
            // 让程序继续运行
        }
        
        /// <summary>
        /// 【关键】WPF Dispatcher 异常 - 只记录，绝不退出
        /// </summary>
        private static void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            var message = $"【Dispatcher异常】\n" +
                          $"时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}\n" +
                          $"线程: {Thread.CurrentThread.ManagedThreadId}\n" +
                          $"异常类型: {e.Exception.GetType().FullName}\n" +
                          $"Message: {e.Exception.Message}\n" +
                          $"StackTrace:\n{e.Exception.StackTrace}\n" +
                          $"InnerException: {e.Exception.InnerException?.Message}\n";
            
            WriteLog(message);
            WriteRuntimeLog(message);
            
            // 【重要】标记为已处理，防止程序退出
            e.Handled = true;
        }
        
        /// <summary>
        /// 【关键】Task 未观察异常 - 只记录，绝不退出
        /// </summary>
        private static void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
        {
            var message = $"【Task异常】\n" +
                          $"时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}\n" +
                          $"线程: {Thread.CurrentThread.ManagedThreadId}\n" +
                          $"异常类型: {e.Exception.GetType().FullName}\n" +
                          $"Message: {e.Exception.Message}\n" +
                          $"StackTrace:\n{e.Exception.StackTrace}\n" +
                          $"InnerExceptions: {string.Join("; ", e.Exception.InnerExceptions)}\n";
            
            WriteLog(message);
            WriteRuntimeLog(message);
            
            // 【重要】标记为已观察，防止程序退出
            e.SetObserved();
        }
        
        /// <summary>
        /// 安全清理资源
        /// 【防闪退改造】所有清理操作都包裹独立的 try-catch
        /// </summary>
        private static void SafeCleanup()
        {
            // 【防闪退】清理操作不应该在程序运行期间被调用
            // 只在程序正常退出时执行
            
            try 
            { 
                WriteLog("清理 BrowserInstanceManager...");
                try { Browser.BrowserInstanceManager.Instance.Dispose(); } catch { }
            } 
            catch { }
            
            try 
            { 
                if (Cef.IsInitialized) 
                {
                    WriteLog("关闭 CefSharp...");
                    try { Cef.Shutdown(); } catch { }
                }
            } 
            catch { }
            
            try 
            { 
                WriteLog("清理 LicenseGuard...");
                try { LicenseGuard.Instance.Dispose(); } catch { }
            } 
            catch { }
        }
        
        /// <summary>
        /// 写启动日志（详细格式）
        /// </summary>
        internal static void WriteLog(string message)
        {
            try 
            { 
                var logEntry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [Thread:{Thread.CurrentThread.ManagedThreadId}] {message}\n";
                File.AppendAllText(_logFile, logEntry); 
            } 
            catch { }
        }
        
        /// <summary>
        /// 写运行时日志（详细格式）
        /// </summary>
        internal static void WriteRuntimeLog(string message)
        {
            try 
            { 
                var logEntry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [Thread:{Thread.CurrentThread.ManagedThreadId}] {message}\n";
                File.AppendAllText(_runtimeLogFile, logEntry); 
            } 
            catch { }
        }
        
        /// <summary>
        /// 确保 CefSharp 已初始化
        /// </summary>
        internal static void EnsureCefInitialized()
        {
            if (!Cef.IsInitialized)
            {
                WriteLog("EnsureCefInitialized: CefSharp 未初始化，尝试初始化...");
                InitializeCefSharp();
            }
        }
        
        /// <summary>
        /// 是否处于安全模式
        /// </summary>
        internal static bool IsSafeMode => _isSafeMode;
    }
    
    /// <summary>
    /// 安全异步执行帮助类
    /// </summary>
    public static class SafeAsync
    {
        /// <summary>
        /// 安全执行异步任务，捕获所有异常
        /// </summary>
        public static async Task RunAsync(Func<Task> action, string operationName = "")
        {
            try
            {
                await action();
            }
            catch (Exception ex)
            {
                var message = $"【SafeAsync异常】操作: {operationName}\n" +
                              $"时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}\n" +
                              $"异常类型: {ex.GetType().FullName}\n" +
                              $"Message: {ex.Message}\n" +
                              $"StackTrace:\n{ex.StackTrace}\n";
                App.WriteRuntimeLog(message);
            }
        }
        
        /// <summary>
        /// 安全执行异步任务（带返回值）
        /// </summary>
        public static async Task<T?> RunAsync<T>(Func<Task<T>> action, string operationName = "")
        {
            try
            {
                return await action();
            }
            catch (Exception ex)
            {
                var message = $"【SafeAsync异常】操作: {operationName}\n" +
                              $"时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}\n" +
                              $"异常类型: {ex.GetType().FullName}\n" +
                              $"Message: {ex.Message}\n" +
                              $"StackTrace:\n{ex.StackTrace}\n";
                App.WriteRuntimeLog(message);
                return default;
            }
        }
        
        /// <summary>
        /// 在 UI 线程安全执行
        /// </summary>
        public static void RunOnUI(Action action, string operationName = "")
        {
            try
            {
                if (Application.Current?.Dispatcher == null)
                    return;
                    
                if (Application.Current.Dispatcher.CheckAccess())
                {
                    action();
                }
                else
                {
                    Application.Current.Dispatcher.BeginInvoke(() =>
                    {
                        try { action(); }
                        catch (Exception ex)
                        {
                            App.WriteRuntimeLog($"【UI异常】操作: {operationName}, 错误: {ex.Message}");
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                App.WriteRuntimeLog($"【RunOnUI异常】操作: {operationName}, 错误: {ex.Message}");
            }
        }
    }
}
