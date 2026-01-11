using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using RecruitAutomation.Core.License;
using RecruitAutomation.Core.Runtime;

namespace RecruitAutomation.App.Runtime
{
    /// <summary>
    /// 应用启动服务（白图 AI 4.0 风格）
    /// 
    /// 核心职责：
    /// 1. 统一管理应用启动流程
    /// 2. 将所有启动逻辑从 MainWindow 构造函数移出
    /// 3. 提供启动状态和进度反馈
    /// 4. 处理启动失败的降级策略
    /// 
    /// 设计原则：
    /// - MainWindow 构造函数只调用 InitializeComponent()
    /// - 所有初始化都在 Window_Loaded 后异步执行
    /// - 任何模块初始化失败不影响 UI 显示
    /// </summary>
    public sealed class AppStartupService
    {
        private static readonly Lazy<AppStartupService> _instance =
            new(() => new AppStartupService(), LazyThreadSafetyMode.ExecutionAndPublication);

        public static AppStartupService Instance => _instance.Value;

        private static readonly string LogDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "RecruitAutomation", "logs");

        // 启动状态
        private volatile bool _isStarted;
        private volatile bool _isStarting;
        private StartupResult? _lastResult;

        // 事件
        public event EventHandler<StartupProgressEventArgs>? ProgressChanged;
        public event EventHandler<StartupCompletedEventArgs>? Completed;

        private AppStartupService()
        {
            EnsureLogDirectory();
        }

        /// <summary>
        /// 是否已启动
        /// </summary>
        public bool IsStarted => _isStarted;

        /// <summary>
        /// 最后启动结果
        /// </summary>
        public StartupResult? LastResult => _lastResult;

        /// <summary>
        /// 执行启动流程（在 Window_Loaded 中调用）
        /// </summary>
        public async Task<StartupResult> StartAsync(CancellationToken ct = default)
        {
            if (_isStarted) return _lastResult ?? StartupResult.Success();
            if (_isStarting) return StartupResult.Failed("启动正在进行中");

            _isStarting = true;
            var result = new StartupResult();

            try
            {
                WriteLog("========== 应用启动流程开始 ==========");

                // 步骤 1: 启动运行时守卫
                ReportProgress("正在初始化运行时...", 10);
                AppRuntimeGuard.Instance.Start();
                WriteLog("运行时守卫已启动");

                // 步骤 2: 验证授权
                ReportProgress("正在验证授权...", 20);
                var licenseResult = await ValidateLicenseAsync(ct);
                result.LicenseValid = licenseResult.IsValid;
                result.LicenseMessage = licenseResult.Message;
                WriteLog($"授权验证: {(licenseResult.IsValid ? "通过" : "失败")} - {licenseResult.Message}");

                if (!licenseResult.IsValid)
                {
                    // 授权失败不阻止启动，但记录状态
                    AppRuntimeGuard.Instance.UpdateModuleState(ModuleNames.License, ModuleStatus.Error, licenseResult.Message);
                }
                else
                {
                    AppRuntimeGuard.Instance.UpdateModuleState(ModuleNames.License, ModuleStatus.Ready);
                }

                // 步骤 3: 初始化浏览器模块（延迟，不阻塞）
                ReportProgress("正在准备浏览器模块...", 40);
                _ = InitializeBrowserModuleAsync(ct); // 后台执行，不等待
                WriteLog("浏览器模块初始化已启动（后台）");

                // 步骤 4: 初始化数据模块
                ReportProgress("正在加载数据...", 60);
                var dataResult = await InitializeDataModuleAsync(ct);
                result.DataLoaded = dataResult;
                WriteLog($"数据模块: {(dataResult ? "成功" : "失败")}");

                // 步骤 5: 启动自动化编排器（不启动任务，只准备就绪）
                ReportProgress("正在准备自动化引擎...", 80);
                // AutomationOrchestrator 不在启动时 Start，等用户点击启动按钮
                AppRuntimeGuard.Instance.UpdateModuleState(ModuleNames.Automation, ModuleStatus.Ready);
                WriteLog("自动化编排器已就绪");

                // 步骤 6: 完成
                ReportProgress("启动完成", 100);
                result.IsSuccess = true;
                _isStarted = true;

                WriteLog("========== 应用启动流程完成 ==========");
            }
            catch (OperationCanceledException)
            {
                result.IsSuccess = false;
                result.ErrorMessage = "启动已取消";
                WriteLog("启动已取消");
            }
            catch (Exception ex)
            {
                result.IsSuccess = false;
                result.ErrorMessage = ex.Message;
                WriteError("启动流程", ex);
            }
            finally
            {
                _isStarting = false;
                _lastResult = result;
                SafeInvokeEvent(() => Completed?.Invoke(this, new StartupCompletedEventArgs(result)));
            }

            return result;
        }

        #region 启动步骤

        private Task<LicenseValidationResult> ValidateLicenseAsync(CancellationToken ct)
        {
            return Task.Run(() =>
            {
                try
                {
                    return LicenseGuard.Instance.Validate();
                }
                catch (Exception ex)
                {
                    // 返回一个表示验证失败的结果
                    return LicenseValidationResult.Error($"验证异常: {ex.Message}");
                }
            }, ct);
        }

        private async Task InitializeBrowserModuleAsync(CancellationToken ct)
        {
            try
            {
                // 在 UI 线程上初始化（CefSharp 要求）
                await Application.Current.Dispatcher.InvokeAsync(async () =>
                {
                    var success = await BrowserModuleController.Instance.InitializeAsync(ct);
                    if (success)
                    {
                        AppRuntimeGuard.Instance.UpdateModuleState(ModuleNames.Browser, ModuleStatus.Ready);
                    }
                    else
                    {
                        AppRuntimeGuard.Instance.UpdateModuleState(ModuleNames.Browser, ModuleStatus.Error,
                            BrowserModuleController.Instance.LastError);
                    }
                });
            }
            catch (Exception ex)
            {
                AppRuntimeGuard.Instance.UpdateModuleState(ModuleNames.Browser, ModuleStatus.Error, ex.Message);
                WriteError("浏览器模块初始化", ex);
            }
        }

        private Task<bool> InitializeDataModuleAsync(CancellationToken ct)
        {
            return Task.Run(() =>
            {
                try
                {
                    // 数据模块初始化（加载配置、缓存等）
                    AppRuntimeGuard.Instance.UpdateModuleState(ModuleNames.Data, ModuleStatus.Ready);
                    return true;
                }
                catch (Exception ex)
                {
                    AppRuntimeGuard.Instance.UpdateModuleState(ModuleNames.Data, ModuleStatus.Error, ex.Message);
                    WriteError("数据模块初始化", ex);
                    return false;
                }
            }, ct);
        }

        #endregion

        #region 辅助方法

        private void ReportProgress(string message, int percentage)
        {
            WriteLog($"[{percentage}%] {message}");
            SafeInvokeEvent(() => ProgressChanged?.Invoke(this,
                new StartupProgressEventArgs(message, percentage)));
        }

        private void EnsureLogDirectory()
        {
            try
            {
                if (!Directory.Exists(LogDir))
                    Directory.CreateDirectory(LogDir);
            }
            catch { }
        }

        private void WriteLog(string message)
        {
            try
            {
                var logEntry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [Startup] {message}";
                var logFile = Path.Combine(LogDir, $"startup_{DateTime.Now:yyyyMMdd}.log");
                File.AppendAllText(logFile, logEntry + "\n");
            }
            catch { }
        }

        private void WriteError(string operation, Exception ex)
        {
            try
            {
                var errorEntry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [Startup] {operation}\n" +
                                 $"  异常: {ex.Message}\n{ex.StackTrace}\n";
                var errorFile = Path.Combine(LogDir, $"startup_error_{DateTime.Now:yyyyMMdd}.log");
                File.AppendAllText(errorFile, errorEntry + "\n");
            }
            catch { }
        }

        private void SafeInvokeEvent(Action action)
        {
            try { action(); } catch { }
        }

        #endregion
    }

    #region 启动相关类型

    /// <summary>
    /// 启动结果
    /// </summary>
    public class StartupResult
    {
        public bool IsSuccess { get; set; }
        public string? ErrorMessage { get; set; }
        public bool LicenseValid { get; set; }
        public string? LicenseMessage { get; set; }
        public bool DataLoaded { get; set; }

        public static StartupResult Success() => new() { IsSuccess = true };
        public static StartupResult Failed(string error) => new() { IsSuccess = false, ErrorMessage = error };
    }

    /// <summary>
    /// 启动进度事件参数
    /// </summary>
    public class StartupProgressEventArgs : EventArgs
    {
        public string Message { get; }
        public int Percentage { get; }

        public StartupProgressEventArgs(string message, int percentage)
        {
            Message = message;
            Percentage = percentage;
        }
    }

    /// <summary>
    /// 启动完成事件参数
    /// </summary>
    public class StartupCompletedEventArgs : EventArgs
    {
        public StartupResult Result { get; }

        public StartupCompletedEventArgs(StartupResult result)
        {
            Result = result;
        }
    }

    #endregion
}
