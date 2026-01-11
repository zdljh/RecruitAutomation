using System;
using System.Collections.Concurrent;
using System.Threading;
using RecruitAutomation.Core.Automation.AI;
using RecruitAutomation.Core.Automation.Commands;
using RecruitAutomation.Core.Automation.Execution;

namespace RecruitAutomation.Core.Automation.UI
{
    /// <summary>
    /// 自动化状态通知器（白图 AI 4.0 风格）
    /// 
    /// 核心职责：
    /// 1. 收集自动化系统的状态变化
    /// 2. 转换为 UI 友好的状态对象
    /// 3. 通过事件通知 UI 更新
    /// 
    /// 设计原则：
    /// - UI 只订阅事件，不直接访问自动化系统
    /// - 所有状态更新都是线程安全的
    /// - 事件处理异常不影响自动化系统
    /// </summary>
    public sealed class AutomationStateNotifier
    {
        private static readonly Lazy<AutomationStateNotifier> _instance =
            new(() => new AutomationStateNotifier(), LazyThreadSafetyMode.ExecutionAndPublication);

        public static AutomationStateNotifier Instance => _instance.Value;

        private readonly ConcurrentDictionary<string, CommandExecutionState> _executionStates = new();
        private volatile AutomationSystemState _systemState = AutomationSystemState.Idle;

        // UI 订阅的事件
        public event EventHandler<SystemStateChangedEventArgs>? SystemStateChanged;
        public event EventHandler<CommandStateChangedEventArgs>? CommandStateChanged;
        public event EventHandler<DecisionNotificationEventArgs>? DecisionNotification;
        public event EventHandler<ErrorNotificationEventArgs>? ErrorNotification;

        private AutomationStateNotifier()
        {
            // 订阅调度器事件
            CommandDispatcher.Instance.CommandQueued += OnCommandQueued;
            CommandDispatcher.Instance.CommandExecuting += OnCommandExecuting;
            CommandDispatcher.Instance.CommandCompleted += OnCommandCompleted;
            CommandDispatcher.Instance.Error += OnDispatcherError;

            // 订阅 AI 决策服务事件
            AIDecisionService.Instance.DecisionMade += OnDecisionMade;
            // 注：ConfirmRequired 和 Error 事件在当前 AIDecisionService 中未实现
            // AIDecisionService.Instance.ConfirmRequired += OnConfirmRequired;
            // AIDecisionService.Instance.Error += OnDecisionError;
        }

        /// <summary>
        /// 当前系统状态
        /// </summary>
        public AutomationSystemState SystemState => _systemState;

        /// <summary>
        /// 更新系统状态
        /// </summary>
        public void UpdateSystemState(AutomationSystemState newState)
        {
            var oldState = _systemState;
            _systemState = newState;

            if (oldState != newState)
            {
                SafeRaiseEvent(() => SystemStateChanged?.Invoke(this,
                    new SystemStateChangedEventArgs(oldState, newState)));
            }
        }

        /// <summary>
        /// 获取指令执行状态
        /// </summary>
        public CommandExecutionState? GetCommandState(string commandId)
        {
            _executionStates.TryGetValue(commandId, out var state);
            return state;
        }

        #region 事件处理

        private void OnCommandQueued(object? sender, CommandQueuedEventArgs e)
        {
            var state = new CommandExecutionState
            {
                CommandId = e.Command.CommandId,
                CommandType = e.Command.Type,
                Status = ExecutionStatus.Queued,
                QueuedAt = DateTime.Now
            };

            _executionStates[e.Command.CommandId] = state;

            SafeRaiseEvent(() => CommandStateChanged?.Invoke(this,
                new CommandStateChangedEventArgs(state)));
        }

        private void OnCommandExecuting(object? sender, CommandExecutingEventArgs e)
        {
            if (_executionStates.TryGetValue(e.Command.CommandId, out var state))
            {
                state.Status = ExecutionStatus.Executing;
                state.StartedAt = DateTime.Now;

                SafeRaiseEvent(() => CommandStateChanged?.Invoke(this,
                    new CommandStateChangedEventArgs(state)));
            }
        }

        private void OnCommandCompleted(object? sender, CommandCompletedEventArgs e)
        {
            if (_executionStates.TryGetValue(e.Command.CommandId, out var state))
            {
                state.Status = e.Result.IsSuccess ? ExecutionStatus.Completed : ExecutionStatus.Failed;
                state.CompletedAt = DateTime.Now;
                state.ErrorMessage = e.Result.ErrorMessage;
                state.ElapsedMs = e.Result.ElapsedMs;

                SafeRaiseEvent(() => CommandStateChanged?.Invoke(this,
                    new CommandStateChangedEventArgs(state)));
            }
        }

        private void OnDispatcherError(object? sender, DispatcherErrorEventArgs e)
        {
            SafeRaiseEvent(() => ErrorNotification?.Invoke(this,
                new ErrorNotificationEventArgs("调度器错误", e.Exception.Message, e.Command?.CommandId)));
        }

        private void OnDecisionMade(object? sender, AIDecisionOutput? e)
        {
            if (e == null) return;
            SafeRaiseEvent(() => DecisionNotification?.Invoke(this,
                new DecisionNotificationEventArgs(
                    e.DecisionId,
                    e.Type,
                    e.Reasoning,
                    e.Confidence,
                    e.RequireHumanConfirm)));
        }

        // 注：以下事件处理器保留供未来扩展使用
        // private void OnConfirmRequired(object? sender, ...) { }
        // private void OnDecisionError(object? sender, ...) { }

        #endregion

        private void SafeRaiseEvent(Action action)
        {
            try { action(); } catch { }
        }
    }

    #region 状态类型

    /// <summary>
    /// 自动化系统状态
    /// </summary>
    public enum AutomationSystemState
    {
        Idle,
        Running,
        Paused,
        Error,
        Stopping
    }

    /// <summary>
    /// 执行状态
    /// </summary>
    public enum ExecutionStatus
    {
        Queued,
        Executing,
        Completed,
        Failed,
        Cancelled
    }

    /// <summary>
    /// 指令执行状态
    /// </summary>
    public class CommandExecutionState
    {
        public string CommandId { get; set; } = string.Empty;
        public CommandType CommandType { get; set; }
        public ExecutionStatus Status { get; set; }
        public DateTime QueuedAt { get; set; }
        public DateTime? StartedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public string? ErrorMessage { get; set; }
        public long ElapsedMs { get; set; }
    }

    #endregion

    #region 事件参数

    public class SystemStateChangedEventArgs : EventArgs
    {
        public AutomationSystemState OldState { get; }
        public AutomationSystemState NewState { get; }

        public SystemStateChangedEventArgs(AutomationSystemState oldState, AutomationSystemState newState)
        {
            OldState = oldState;
            NewState = newState;
        }
    }

    public class CommandStateChangedEventArgs : EventArgs
    {
        public CommandExecutionState State { get; }
        public CommandStateChangedEventArgs(CommandExecutionState state) => State = state;
    }

    public class DecisionNotificationEventArgs : EventArgs
    {
        public string DecisionId { get; }
        public DecisionType Type { get; }
        public string Message { get; }
        public double Confidence { get; }
        public bool RequireConfirm { get; }

        public DecisionNotificationEventArgs(string decisionId, DecisionType type, string message, double confidence, bool requireConfirm)
        {
            DecisionId = decisionId;
            Type = type;
            Message = message;
            Confidence = confidence;
            RequireConfirm = requireConfirm;
        }
    }

    public class ErrorNotificationEventArgs : EventArgs
    {
        public string Source { get; }
        public string Message { get; }
        public string? CommandId { get; }

        public ErrorNotificationEventArgs(string source, string message, string? commandId)
        {
            Source = source;
            Message = message;
            CommandId = commandId;
        }
    }

    #endregion
}
