using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace RecruitAutomation.Core.Runtime
{
    /// <summary>
    /// 自动化编排器（白图 AI 4.0 风格）
    /// 
    /// 核心职责：
    /// 1. 管理所有自动化任务的执行
    /// 2. 任务队列和优先级调度
    /// 3. 任务状态跟踪
    /// 4. 异常隔离和恢复
    /// 
    /// 设计原则：
    /// - 任务执行与 UI 完全解耦
    /// - 单个任务失败不影响其他任务
    /// - 支持任务暂停/恢复/取消
    /// </summary>
    public sealed class AutomationOrchestrator : IDisposable
    {
        private static readonly Lazy<AutomationOrchestrator> _instance =
            new(() => new AutomationOrchestrator(), LazyThreadSafetyMode.ExecutionAndPublication);

        public static AutomationOrchestrator Instance => _instance.Value;

        private static readonly string LogDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "RecruitAutomation", "logs");

        // 任务队列
        private readonly ConcurrentQueue<AutomationTask> _taskQueue = new();
        private readonly ConcurrentDictionary<string, AutomationTask> _runningTasks = new();
        private readonly ConcurrentDictionary<string, AutomationTask> _completedTasks = new();

        // 控制
        private CancellationTokenSource? _globalCts;
        private volatile bool _isRunning;
        private volatile bool _isPaused;
        private volatile bool _disposed;
        private readonly SemaphoreSlim _concurrencyLimiter;
        private Task? _processingTask;

        // 配置
        public int MaxConcurrentTasks { get; set; } = 3;
        public int TaskTimeoutMs { get; set; } = 300000; // 5分钟

        // 事件
        public event EventHandler<TaskStateChangedEventArgs>? TaskStateChanged;
        public event EventHandler<TaskProgressEventArgs>? TaskProgress;
        public event EventHandler<TaskCompletedEventArgs>? TaskCompleted;
        public event EventHandler<OrchestratorStateChangedEventArgs>? StateChanged;

        private AutomationOrchestrator()
        {
            _concurrencyLimiter = new SemaphoreSlim(MaxConcurrentTasks);
            EnsureLogDirectory();
        }

        #region 任务管理

        /// <summary>
        /// 提交任务
        /// </summary>
        public string SubmitTask(AutomationTask task)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(AutomationOrchestrator));

            task.Id = Guid.NewGuid().ToString("N");
            task.Status = TaskStatus.Queued;
            task.SubmittedAt = DateTime.UtcNow;

            _taskQueue.Enqueue(task);
            WriteLog($"任务已提交: {task.Id} - {task.Name}");

            SafeInvokeEvent(() => TaskStateChanged?.Invoke(this,
                new TaskStateChangedEventArgs(task.Id, TaskStatus.Queued, null)));

            return task.Id;
        }

        /// <summary>
        /// 取消任务
        /// </summary>
        public bool CancelTask(string taskId)
        {
            if (_runningTasks.TryGetValue(taskId, out var task))
            {
                task.CancellationTokenSource?.Cancel();
                WriteLog($"任务已取消: {taskId}");
                return true;
            }
            return false;
        }

        /// <summary>
        /// 获取任务状态
        /// </summary>
        public AutomationTask? GetTask(string taskId)
        {
            if (_runningTasks.TryGetValue(taskId, out var running))
                return running;
            if (_completedTasks.TryGetValue(taskId, out var completed))
                return completed;
            return _taskQueue.FirstOrDefault(t => t.Id == taskId);
        }

        /// <summary>
        /// 获取所有运行中的任务
        /// </summary>
        public IEnumerable<AutomationTask> GetRunningTasks()
        {
            return _runningTasks.Values.ToList();
        }

        /// <summary>
        /// 获取队列中的任务数
        /// </summary>
        public int QueuedTaskCount => _taskQueue.Count;

        /// <summary>
        /// 获取运行中的任务数
        /// </summary>
        public int RunningTaskCount => _runningTasks.Count;

        #endregion

        #region 编排控制

        /// <summary>
        /// 启动编排器
        /// </summary>
        public void Start()
        {
            if (_disposed || _isRunning) return;

            _globalCts = new CancellationTokenSource();
            _isRunning = true;
            _isPaused = false;

            _processingTask = Task.Run(ProcessTasksAsync);

            WriteLog("编排器已启动");
            SafeInvokeEvent(() => StateChanged?.Invoke(this,
                new OrchestratorStateChangedEventArgs(OrchestratorState.Running)));
        }

        /// <summary>
        /// 停止编排器
        /// </summary>
        public async Task StopAsync()
        {
            if (!_isRunning) return;

            _isRunning = false;
            _globalCts?.Cancel();

            // 等待处理任务完成
            if (_processingTask != null)
            {
                try
                {
                    await Task.WhenAny(_processingTask, Task.Delay(5000));
                }
                catch { }
            }

            WriteLog("编排器已停止");
            SafeInvokeEvent(() => StateChanged?.Invoke(this,
                new OrchestratorStateChangedEventArgs(OrchestratorState.Stopped)));
        }

        /// <summary>
        /// 暂停编排器
        /// </summary>
        public void Pause()
        {
            if (!_isRunning || _isPaused) return;
            _isPaused = true;
            WriteLog("编排器已暂停");
            SafeInvokeEvent(() => StateChanged?.Invoke(this,
                new OrchestratorStateChangedEventArgs(OrchestratorState.Paused)));
        }

        /// <summary>
        /// 恢复编排器
        /// </summary>
        public void Resume()
        {
            if (!_isRunning || !_isPaused) return;
            _isPaused = false;
            WriteLog("编排器已恢复");
            SafeInvokeEvent(() => StateChanged?.Invoke(this,
                new OrchestratorStateChangedEventArgs(OrchestratorState.Running)));
        }

        /// <summary>
        /// 编排器是否正在运行
        /// </summary>
        public bool IsRunning => _isRunning && !_isPaused;

        #endregion

        #region 任务处理

        private async Task ProcessTasksAsync()
        {
            while (_isRunning && !_globalCts!.Token.IsCancellationRequested)
            {
                try
                {
                    // 暂停时等待
                    while (_isPaused && _isRunning)
                    {
                        await Task.Delay(500, _globalCts.Token);
                    }

                    // 尝试获取任务
                    if (_taskQueue.TryDequeue(out var task))
                    {
                        // 等待并发槽位
                        await _concurrencyLimiter.WaitAsync(_globalCts.Token);

                        // 启动任务执行
                        _ = ExecuteTaskAsync(task);
                    }
                    else
                    {
                        // 队列为空，等待
                        await Task.Delay(100, _globalCts.Token);
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    WriteError("ProcessTasksAsync", ex);
                    await Task.Delay(1000);
                }
            }
        }

        private async Task ExecuteTaskAsync(AutomationTask task)
        {
            task.CancellationTokenSource = new CancellationTokenSource();
            task.Status = TaskStatus.Running;
            task.StartedAt = DateTime.UtcNow;
            _runningTasks.TryAdd(task.Id, task);

            WriteLog($"任务开始执行: {task.Id} - {task.Name}");
            SafeInvokeEvent(() => TaskStateChanged?.Invoke(this,
                new TaskStateChangedEventArgs(task.Id, TaskStatus.Running, null)));

            try
            {
                // 设置超时
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                    task.CancellationTokenSource.Token, _globalCts!.Token);
                linkedCts.CancelAfter(TaskTimeoutMs);

                // 执行任务
                if (task.ExecuteAsync != null)
                {
                    task.Result = await task.ExecuteAsync(
                        new TaskExecutionContext
                        {
                            TaskId = task.Id,
                            CancellationToken = linkedCts.Token,
                            ReportProgress = (msg, pct) => ReportTaskProgress(task.Id, msg, pct)
                        });
                }

                task.Status = TaskStatus.Completed;
                task.CompletedAt = DateTime.UtcNow;
                WriteLog($"任务完成: {task.Id}");
            }
            catch (OperationCanceledException)
            {
                task.Status = TaskStatus.Cancelled;
                task.CompletedAt = DateTime.UtcNow;
                WriteLog($"任务已取消: {task.Id}");
            }
            catch (Exception ex)
            {
                task.Status = TaskStatus.Failed;
                task.Error = ex.Message;
                task.CompletedAt = DateTime.UtcNow;
                WriteError($"任务失败: {task.Id}", ex);
            }
            finally
            {
                _runningTasks.TryRemove(task.Id, out _);
                _completedTasks.TryAdd(task.Id, task);
                _concurrencyLimiter.Release();

                SafeInvokeEvent(() => TaskCompleted?.Invoke(this,
                    new TaskCompletedEventArgs(task.Id, task.Status, task.Result, task.Error)));
            }
        }

        private void ReportTaskProgress(string taskId, string message, int percentage)
        {
            SafeInvokeEvent(() => TaskProgress?.Invoke(this,
                new TaskProgressEventArgs(taskId, message, percentage)));
        }

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

        private void WriteLog(string message)
        {
            try
            {
                var logEntry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [Orchestrator] {message}";
                var logFile = Path.Combine(LogDir, $"orchestrator_{DateTime.Now:yyyyMMdd}.log");
                File.AppendAllText(logFile, logEntry + "\n");
            }
            catch { }
        }

        private void WriteError(string operation, Exception ex)
        {
            try
            {
                var errorEntry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [Orchestrator] {operation}\n" +
                                 $"  异常: {ex.Message}\n{ex.StackTrace}\n";
                var errorFile = Path.Combine(LogDir, $"orchestrator_error_{DateTime.Now:yyyyMMdd}.log");
                File.AppendAllText(errorFile, errorEntry + "\n");
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

            _globalCts?.Cancel();
            _concurrencyLimiter.Dispose();
        }
    }

    #region 任务相关类型

    /// <summary>
    /// 自动化任务
    /// </summary>
    public class AutomationTask
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public TaskStatus Status { get; set; }
        public DateTime SubmittedAt { get; set; }
        public DateTime? StartedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public string? Error { get; set; }
        public object? Result { get; set; }
        public CancellationTokenSource? CancellationTokenSource { get; set; }

        /// <summary>
        /// 任务执行委托
        /// </summary>
        public Func<TaskExecutionContext, Task<object?>>? ExecuteAsync { get; set; }
    }

    /// <summary>
    /// 任务执行上下文
    /// </summary>
    public class TaskExecutionContext
    {
        public string TaskId { get; set; } = string.Empty;
        public CancellationToken CancellationToken { get; set; }
        public Action<string, int>? ReportProgress { get; set; }
    }

    /// <summary>
    /// 任务状态
    /// </summary>
    public enum TaskStatus
    {
        Queued,
        Running,
        Completed,
        Failed,
        Cancelled
    }

    /// <summary>
    /// 编排器状态
    /// </summary>
    public enum OrchestratorState
    {
        Stopped,
        Running,
        Paused
    }

    #endregion

    #region 事件参数

    public class TaskStateChangedEventArgs : EventArgs
    {
        public string TaskId { get; }
        public TaskStatus Status { get; }
        public string? Message { get; }

        public TaskStateChangedEventArgs(string taskId, TaskStatus status, string? message)
        {
            TaskId = taskId;
            Status = status;
            Message = message;
        }
    }

    public class TaskProgressEventArgs : EventArgs
    {
        public string TaskId { get; }
        public string Message { get; }
        public int Percentage { get; }

        public TaskProgressEventArgs(string taskId, string message, int percentage)
        {
            TaskId = taskId;
            Message = message;
            Percentage = percentage;
        }
    }

    public class TaskCompletedEventArgs : EventArgs
    {
        public string TaskId { get; }
        public TaskStatus Status { get; }
        public object? Result { get; }
        public string? Error { get; }

        public TaskCompletedEventArgs(string taskId, TaskStatus status, object? result, string? error)
        {
            TaskId = taskId;
            Status = status;
            Result = result;
            Error = error;
        }
    }

    public class OrchestratorStateChangedEventArgs : EventArgs
    {
        public OrchestratorState State { get; }

        public OrchestratorStateChangedEventArgs(OrchestratorState state)
        {
            State = state;
        }
    }

    #endregion
}
