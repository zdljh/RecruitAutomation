using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using RecruitAutomation.Core.Automation.Commands;

namespace RecruitAutomation.Core.Automation.Execution
{
    /// <summary>
    /// 指令调度器（白图 AI 4.0 风格）
    /// 
    /// 核心职责：
    /// 1. 接收 AI 输出的指令
    /// 2. 分发给对应的执行器
    /// 3. 收集执行结果
    /// 4. 通知 UI 更新状态
    /// 
    /// 设计原则：
    /// - 调度器不做业务决策
    /// - 所有异常都被捕获，不会传播到 UI
    /// - 支持指令队列和优先级
    /// </summary>
    public sealed class CommandDispatcher
    {
        private static readonly Lazy<CommandDispatcher> _instance =
            new(() => new CommandDispatcher(), LazyThreadSafetyMode.ExecutionAndPublication);

        public static CommandDispatcher Instance => _instance.Value;

        private readonly ConcurrentDictionary<CommandType, ICommandExecutor> _executors = new();
        private readonly ConcurrentQueue<AutomationCommand> _commandQueue = new();
        private readonly object _lock = new();

        private volatile bool _isRunning;
        private CancellationTokenSource? _cts;
        private Task? _dispatchTask;

        // 事件
        public event EventHandler<CommandQueuedEventArgs>? CommandQueued;
        public event EventHandler<CommandExecutingEventArgs>? CommandExecuting;
        public event EventHandler<CommandCompletedEventArgs>? CommandCompleted;
        public event EventHandler<DispatcherErrorEventArgs>? Error;

        private CommandDispatcher() { }

        /// <summary>
        /// 注册执行器
        /// </summary>
        public void RegisterExecutor(ICommandExecutor executor)
        {
            foreach (var type in executor.SupportedTypes)
            {
                _executors[type] = executor;
            }
        }

        /// <summary>
        /// 提交指令（加入队列）
        /// </summary>
        public void Submit(AutomationCommand command)
        {
            _commandQueue.Enqueue(command);
            SafeRaiseEvent(() => CommandQueued?.Invoke(this, new CommandQueuedEventArgs(command)));
        }

        /// <summary>
        /// 批量提交指令
        /// </summary>
        public void SubmitBatch(IEnumerable<AutomationCommand> commands)
        {
            foreach (var cmd in commands)
            {
                Submit(cmd);
            }
        }

        /// <summary>
        /// 立即执行指令（不入队列）
        /// </summary>
        public async Task<CommandResult> ExecuteImmediateAsync(AutomationCommand command, CancellationToken ct = default)
        {
            try
            {
                if (!_executors.TryGetValue(command.Type, out var executor))
                {
                    return CommandResult.Failed(command, $"未找到指令类型 {command.Type} 的执行器", 0);
                }

                SafeRaiseEvent(() => CommandExecuting?.Invoke(this, new CommandExecutingEventArgs(command)));

                var result = await executor.ExecuteAsync(command, ct);

                SafeRaiseEvent(() => CommandCompleted?.Invoke(this, new CommandCompletedEventArgs(command, result)));

                return result;
            }
            catch (Exception ex)
            {
                SafeRaiseEvent(() => Error?.Invoke(this, new DispatcherErrorEventArgs(command, ex)));
                return CommandResult.Failed(command, ex, 0);
            }
        }

        /// <summary>
        /// 启动调度器
        /// </summary>
        public void Start()
        {
            lock (_lock)
            {
                if (_isRunning) return;

                _cts = new CancellationTokenSource();
                _isRunning = true;
                _dispatchTask = Task.Run(() => DispatchLoopAsync(_cts.Token));
            }
        }

        /// <summary>
        /// 停止调度器
        /// </summary>
        public void Stop()
        {
            lock (_lock)
            {
                if (!_isRunning) return;

                _cts?.Cancel();
                _isRunning = false;
            }
        }

        /// <summary>
        /// 清空队列
        /// </summary>
        public void ClearQueue()
        {
            while (_commandQueue.TryDequeue(out _)) { }
        }

        /// <summary>
        /// 队列长度
        /// </summary>
        public int QueueLength => _commandQueue.Count;

        private async Task DispatchLoopAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    if (_commandQueue.TryDequeue(out var command))
                    {
                        await ExecuteImmediateAsync(command, ct);
                    }
                    else
                    {
                        await Task.Delay(100, ct);
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    SafeRaiseEvent(() => Error?.Invoke(this, new DispatcherErrorEventArgs(null, ex)));
                    await Task.Delay(1000, ct); // 出错后等待一秒再继续
                }
            }
        }

        private void SafeRaiseEvent(Action action)
        {
            try { action(); } catch { }
        }
    }

    #region 事件参数

    public class CommandQueuedEventArgs : EventArgs
    {
        public AutomationCommand Command { get; }
        public CommandQueuedEventArgs(AutomationCommand command) => Command = command;
    }

    public class CommandExecutingEventArgs : EventArgs
    {
        public AutomationCommand Command { get; }
        public CommandExecutingEventArgs(AutomationCommand command) => Command = command;
    }

    public class CommandCompletedEventArgs : EventArgs
    {
        public AutomationCommand Command { get; }
        public CommandResult Result { get; }
        public CommandCompletedEventArgs(AutomationCommand command, CommandResult result)
        {
            Command = command;
            Result = result;
        }
    }

    public class DispatcherErrorEventArgs : EventArgs
    {
        public AutomationCommand? Command { get; }
        public Exception Exception { get; }
        public DispatcherErrorEventArgs(AutomationCommand? command, Exception exception)
        {
            Command = command;
            Exception = exception;
        }
    }

    #endregion
}
