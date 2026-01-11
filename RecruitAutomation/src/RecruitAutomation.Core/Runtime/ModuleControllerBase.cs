using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace RecruitAutomation.Core.Runtime
{
    /// <summary>
    /// 模块控制器基类（白图 AI 4.0 风格）
    /// 
    /// 提供通用的模块生命周期管理和异常处理
    /// </summary>
    public abstract class ModuleControllerBase : IModuleController
    {
        private static readonly string LogDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "RecruitAutomation", "logs");

        private ModuleStatus _status = ModuleStatus.NotInitialized;
        private string? _lastError;
        private bool _disposed;

        public abstract string ModuleId { get; }
        public abstract string DisplayName { get; }

        public ModuleStatus Status
        {
            get => _status;
            protected set
            {
                if (_status != value)
                {
                    var oldStatus = _status;
                    _status = value;
                    OnStatusChanged(oldStatus, value);
                }
            }
        }

        public string? LastError
        {
            get => _lastError;
            protected set => _lastError = value;
        }

        public event EventHandler<ModuleStatusEventArgs>? StatusChanged;

        #region 生命周期

        public async Task<bool> InitializeAsync(CancellationToken ct = default)
        {
            if (_disposed) return false;
            if (Status != ModuleStatus.NotInitialized && Status != ModuleStatus.Error)
                return Status == ModuleStatus.Ready || Status == ModuleStatus.Running;

            try
            {
                Status = ModuleStatus.Initializing;
                WriteLog($"开始初始化...");

                var result = await DoInitializeAsync(ct);

                if (result)
                {
                    Status = ModuleStatus.Ready;
                    LastError = null;
                    WriteLog($"初始化成功");
                }
                else
                {
                    Status = ModuleStatus.Error;
                    WriteLog($"初始化失败: {LastError}");
                }

                return result;
            }
            catch (OperationCanceledException)
            {
                Status = ModuleStatus.NotInitialized;
                WriteLog($"初始化已取消");
                return false;
            }
            catch (Exception ex)
            {
                LastError = ex.Message;
                Status = ModuleStatus.Error;
                WriteError("初始化", ex);
                return false;
            }
        }

        public async Task<bool> StartAsync(CancellationToken ct = default)
        {
            if (_disposed) return false;
            
            // 如果未初始化，先初始化
            if (Status == ModuleStatus.NotInitialized || Status == ModuleStatus.Error)
            {
                if (!await InitializeAsync(ct))
                    return false;
            }

            if (Status == ModuleStatus.Running)
                return true;

            try
            {
                WriteLog($"开始启动...");
                var result = await DoStartAsync(ct);

                if (result)
                {
                    Status = ModuleStatus.Running;
                    LastError = null;
                    WriteLog($"启动成功");
                }
                else
                {
                    WriteLog($"启动失败: {LastError}");
                }

                return result;
            }
            catch (OperationCanceledException)
            {
                WriteLog($"启动已取消");
                return false;
            }
            catch (Exception ex)
            {
                LastError = ex.Message;
                Status = ModuleStatus.Error;
                WriteError("启动", ex);
                return false;
            }
        }

        public async Task StopAsync()
        {
            if (_disposed) return;
            if (Status != ModuleStatus.Running && Status != ModuleStatus.Paused)
                return;

            try
            {
                WriteLog($"开始停止...");
                await DoStopAsync();
                Status = ModuleStatus.Ready;
                WriteLog($"已停止");
            }
            catch (Exception ex)
            {
                WriteError("停止", ex);
                // 停止时的异常不改变状态为 Error
            }
        }

        public async Task<bool> ResetAsync()
        {
            if (_disposed) return false;

            try
            {
                WriteLog($"开始重置...");
                await DoResetAsync();
                Status = ModuleStatus.NotInitialized;
                LastError = null;
                WriteLog($"重置完成");
                return true;
            }
            catch (Exception ex)
            {
                WriteError("重置", ex);
                return false;
            }
        }

        #endregion

        #region 子类实现

        /// <summary>
        /// 执行初始化（子类实现）
        /// </summary>
        protected abstract Task<bool> DoInitializeAsync(CancellationToken ct);

        /// <summary>
        /// 执行启动（子类实现）
        /// </summary>
        protected virtual Task<bool> DoStartAsync(CancellationToken ct)
        {
            return Task.FromResult(true);
        }

        /// <summary>
        /// 执行停止（子类实现）
        /// </summary>
        protected virtual Task DoStopAsync()
        {
            return Task.CompletedTask;
        }

        /// <summary>
        /// 执行重置（子类实现）
        /// </summary>
        protected virtual Task DoResetAsync()
        {
            return Task.CompletedTask;
        }

        #endregion

        #region 日志

        protected void WriteLog(string message)
        {
            try
            {
                var logEntry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [{ModuleId}] {message}";
                EnsureLogDirectory();
                var logFile = Path.Combine(LogDir, $"module_{ModuleId.ToLower()}_{DateTime.Now:yyyyMMdd}.log");
                File.AppendAllText(logFile, logEntry + "\n");
                
                // 同时写入运行时守卫
                AppRuntimeGuard.Instance.WriteLog($"[{ModuleId}] {message}");
            }
            catch { }
        }

        protected void WriteError(string operation, Exception ex)
        {
            try
            {
                var errorEntry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [{ModuleId}] {operation}\n" +
                                 $"  异常类型: {ex.GetType().FullName}\n" +
                                 $"  消息: {ex.Message}\n" +
                                 $"  堆栈:\n{ex.StackTrace}\n";
                EnsureLogDirectory();
                var errorFile = Path.Combine(LogDir, $"module_{ModuleId.ToLower()}_error_{DateTime.Now:yyyyMMdd}.log");
                File.AppendAllText(errorFile, errorEntry + "\n");
            }
            catch { }
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

        #endregion

        #region 事件

        protected virtual void OnStatusChanged(ModuleStatus oldStatus, ModuleStatus newStatus)
        {
            try
            {
                StatusChanged?.Invoke(this, new ModuleStatusEventArgs(oldStatus, newStatus));
                
                // 同步到运行时守卫
                AppRuntimeGuard.Instance.UpdateModuleState(ModuleId, newStatus, LastError);
            }
            catch { }
        }

        #endregion

        #region IDisposable

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            try
            {
                DoDispose();
                Status = ModuleStatus.Disposed;
                WriteLog($"已释放");
            }
            catch (Exception ex)
            {
                WriteError("释放", ex);
            }
        }

        protected virtual void DoDispose() { }

        #endregion
    }
}
