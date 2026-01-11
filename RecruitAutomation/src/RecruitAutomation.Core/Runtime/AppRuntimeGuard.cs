using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace RecruitAutomation.Core.Runtime
{
    /// <summary>
    /// 应用运行时守卫（白图 AI 4.0 风格）
    /// 
    /// 核心职责：
    /// 1. 统一管理所有模块的生命周期
    /// 2. 隔离模块异常，防止影响 UI
    /// 3. 提供模块状态查询
    /// 4. 统一日志记录
    /// 
    /// 设计原则：
    /// - 任一模块异常不影响其他模块
    /// - UI 层只通过事件接收状态变化
    /// - 所有后台操作都有超时保护
    /// </summary>
    public sealed class AppRuntimeGuard : IDisposable
    {
        private static readonly Lazy<AppRuntimeGuard> _instance = 
            new(() => new AppRuntimeGuard(), LazyThreadSafetyMode.ExecutionAndPublication);
        
        public static AppRuntimeGuard Instance => _instance.Value;

        private static readonly string LogDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "RecruitAutomation", "logs");

        // 模块状态
        private readonly ConcurrentDictionary<string, ModuleState> _moduleStates = new();
        
        // 运行状态
        private volatile bool _isRunning;
        private volatile bool _disposed;
        
        // 事件
        public event EventHandler<ModuleStateChangedEventArgs>? ModuleStateChanged;
        public event EventHandler<RuntimeLogEventArgs>? OnLog;
        public event EventHandler<RuntimeErrorEventArgs>? OnError;

        private AppRuntimeGuard()
        {
            EnsureLogDirectory();
            RegisterBuiltInModules();
        }

        #region 模块管理

        /// <summary>
        /// 注册内置模块
        /// </summary>
        private void RegisterBuiltInModules()
        {
            RegisterModule(ModuleNames.License, "授权模块");
            RegisterModule(ModuleNames.Browser, "浏览器模块");
            RegisterModule(ModuleNames.Automation, "自动化模块");
            RegisterModule(ModuleNames.AI, "AI 模块");
            RegisterModule(ModuleNames.Data, "数据模块");
        }

        /// <summary>
        /// 注册模块
        /// </summary>
        public void RegisterModule(string moduleId, string displayName)
        {
            _moduleStates.TryAdd(moduleId, new ModuleState
            {
                ModuleId = moduleId,
                DisplayName = displayName,
                Status = ModuleStatus.NotInitialized,
                LastError = null,
                LastUpdateTime = DateTime.UtcNow
            });
        }

        /// <summary>
        /// 获取模块状态
        /// </summary>
        public ModuleState? GetModuleState(string moduleId)
        {
            _moduleStates.TryGetValue(moduleId, out var state);
            return state;
        }

        /// <summary>
        /// 更新模块状态
        /// </summary>
        public void UpdateModuleState(string moduleId, ModuleStatus status, string? error = null)
        {
            if (_moduleStates.TryGetValue(moduleId, out var state))
            {
                var oldStatus = state.Status;
                state.Status = status;
                state.LastError = error;
                state.LastUpdateTime = DateTime.UtcNow;

                if (status == ModuleStatus.Error)
                {
                    state.ErrorCount++;
                    WriteLog($"[{moduleId}] 模块错误 ({state.ErrorCount}次): {error}");
                }

                // 触发状态变化事件
                if (oldStatus != status)
                {
                    SafeInvokeEvent(() => ModuleStateChanged?.Invoke(this, 
                        new ModuleStateChangedEventArgs(moduleId, oldStatus, status, error)));
                }
            }
        }

        /// <summary>
        /// 检查模块是否可用
        /// </summary>
        public bool IsModuleAvailable(string moduleId)
        {
            if (_moduleStates.TryGetValue(moduleId, out var state))
            {
                return state.Status == ModuleStatus.Running || state.Status == ModuleStatus.Ready;
            }
            return false;
        }

        #endregion

        #region 安全执行

        /// <summary>
        /// 安全执行模块操作（同步）
        /// </summary>
        public bool SafeExecute(string moduleId, Action action, string operationName = "")
        {
            try
            {
                WriteLog($"[{moduleId}] 开始执行: {operationName}");
                action();
                WriteLog($"[{moduleId}] 执行完成: {operationName}");
                return true;
            }
            catch (Exception ex)
            {
                var error = $"{operationName} 失败: {ex.Message}";
                UpdateModuleState(moduleId, ModuleStatus.Error, error);
                WriteError(moduleId, operationName, ex);
                return false;
            }
        }

        /// <summary>
        /// 安全执行模块操作（异步）
        /// </summary>
        public async Task<bool> SafeExecuteAsync(string moduleId, Func<Task> action, 
            string operationName = "", int timeoutMs = 30000)
        {
            try
            {
                WriteLog($"[{moduleId}] 开始执行: {operationName}");
                
                using var cts = new CancellationTokenSource(timeoutMs);
                var task = action();
                
                if (await Task.WhenAny(task, Task.Delay(timeoutMs, cts.Token)) == task)
                {
                    await task; // 确保异常被抛出
                    WriteLog($"[{moduleId}] 执行完成: {operationName}");
                    return true;
                }
                else
                {
                    var error = $"{operationName} 超时 ({timeoutMs}ms)";
                    UpdateModuleState(moduleId, ModuleStatus.Error, error);
                    WriteLog($"[{moduleId}] {error}");
                    return false;
                }
            }
            catch (OperationCanceledException)
            {
                WriteLog($"[{moduleId}] 操作已取消: {operationName}");
                return false;
            }
            catch (Exception ex)
            {
                var error = $"{operationName} 失败: {ex.Message}";
                UpdateModuleState(moduleId, ModuleStatus.Error, error);
                WriteError(moduleId, operationName, ex);
                return false;
            }
        }

        /// <summary>
        /// 安全执行模块操作（异步，带返回值）
        /// </summary>
        public async Task<(bool Success, T? Result)> SafeExecuteAsync<T>(string moduleId, 
            Func<Task<T>> action, string operationName = "", int timeoutMs = 30000)
        {
            try
            {
                WriteLog($"[{moduleId}] 开始执行: {operationName}");
                
                using var cts = new CancellationTokenSource(timeoutMs);
                var task = action();
                
                if (await Task.WhenAny(task, Task.Delay(timeoutMs, cts.Token)) == task)
                {
                    var result = await task;
                    WriteLog($"[{moduleId}] 执行完成: {operationName}");
                    return (true, result);
                }
                else
                {
                    var error = $"{operationName} 超时 ({timeoutMs}ms)";
                    UpdateModuleState(moduleId, ModuleStatus.Error, error);
                    WriteLog($"[{moduleId}] {error}");
                    return (false, default);
                }
            }
            catch (Exception ex)
            {
                var error = $"{operationName} 失败: {ex.Message}";
                UpdateModuleState(moduleId, ModuleStatus.Error, error);
                WriteError(moduleId, operationName, ex);
                return (false, default);
            }
        }

        #endregion

        #region 生命周期

        /// <summary>
        /// 启动运行时
        /// </summary>
        public void Start()
        {
            if (_isRunning) return;
            _isRunning = true;
            WriteLog("AppRuntimeGuard 已启动");
        }

        /// <summary>
        /// 停止运行时
        /// </summary>
        public void Stop()
        {
            if (!_isRunning) return;
            _isRunning = false;
            WriteLog("AppRuntimeGuard 已停止");
        }

        /// <summary>
        /// 运行时是否正在运行
        /// </summary>
        public bool IsRunning => _isRunning;

        #endregion

        #region 日志

        private void EnsureLogDirectory()
        {
            try
            {
                if (!Directory.Exists(LogDir))
                    Directory.CreateDirectory(LogDir);
            }
            catch { }
        }

        public void WriteLog(string message)
        {
            try
            {
                var logEntry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {message}";
                var logFile = Path.Combine(LogDir, $"runtime_{DateTime.Now:yyyyMMdd}.log");
                File.AppendAllText(logFile, logEntry + "\n");
                
                SafeInvokeEvent(() => OnLog?.Invoke(this, new RuntimeLogEventArgs(message)));
            }
            catch { }
        }

        private void WriteError(string moduleId, string operation, Exception ex)
        {
            try
            {
                var errorEntry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [{moduleId}] {operation}\n" +
                                 $"  异常类型: {ex.GetType().FullName}\n" +
                                 $"  消息: {ex.Message}\n" +
                                 $"  堆栈:\n{ex.StackTrace}\n";
                var errorFile = Path.Combine(LogDir, $"error_{DateTime.Now:yyyyMMdd}.log");
                File.AppendAllText(errorFile, errorEntry + "\n");
                
                SafeInvokeEvent(() => OnError?.Invoke(this, 
                    new RuntimeErrorEventArgs(moduleId, operation, ex)));
            }
            catch { }
        }

        private void SafeInvokeEvent(Action action)
        {
            try { action(); } catch { }
        }

        #endregion

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            Stop();
            _moduleStates.Clear();
        }
    }

    #region 模块名称常量

    /// <summary>
    /// 模块名称常量
    /// </summary>
    public static class ModuleNames
    {
        public const string License = "License";
        public const string Browser = "Browser";
        public const string Automation = "Automation";
        public const string AI = "AI";
        public const string Data = "Data";
    }

    #endregion

    #region 模块状态

    /// <summary>
    /// 模块状态
    /// </summary>
    public enum ModuleStatus
    {
        NotInitialized,
        Initializing,
        Ready,
        Running,
        Paused,
        Error,
        Disposed
    }

    /// <summary>
    /// 模块状态信息
    /// </summary>
    public class ModuleState
    {
        public string ModuleId { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public ModuleStatus Status { get; set; }
        public string? LastError { get; set; }
        public DateTime LastUpdateTime { get; set; }
        public int ErrorCount { get; set; }
    }

    #endregion

    #region 事件参数

    public class ModuleStateChangedEventArgs : EventArgs
    {
        public string ModuleId { get; }
        public ModuleStatus OldStatus { get; }
        public ModuleStatus NewStatus { get; }
        public string? Error { get; }

        public ModuleStateChangedEventArgs(string moduleId, ModuleStatus oldStatus, 
            ModuleStatus newStatus, string? error)
        {
            ModuleId = moduleId;
            OldStatus = oldStatus;
            NewStatus = newStatus;
            Error = error;
        }
    }

    public class RuntimeLogEventArgs : EventArgs
    {
        public string Message { get; }
        public DateTime Timestamp { get; }

        public RuntimeLogEventArgs(string message)
        {
            Message = message;
            Timestamp = DateTime.Now;
        }
    }

    public class RuntimeErrorEventArgs : EventArgs
    {
        public string ModuleId { get; }
        public string Operation { get; }
        public Exception Exception { get; }
        public DateTime Timestamp { get; }

        public RuntimeErrorEventArgs(string moduleId, string operation, Exception exception)
        {
            ModuleId = moduleId;
            Operation = operation;
            Exception = exception;
            Timestamp = DateTime.Now;
        }
    }

    #endregion
}
