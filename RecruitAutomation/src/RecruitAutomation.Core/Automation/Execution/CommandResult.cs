using System;
using RecruitAutomation.Core.Automation.Commands;

namespace RecruitAutomation.Core.Automation.Execution
{
    /// <summary>
    /// 指令执行结果
    /// </summary>
    public sealed class CommandResult
    {
        /// <summary>
        /// 对应的指令 ID
        /// </summary>
        public string CommandId { get; }

        /// <summary>
        /// 指令类型
        /// </summary>
        public CommandType CommandType { get; }

        /// <summary>
        /// 是否成功
        /// </summary>
        public bool IsSuccess { get; }

        /// <summary>
        /// 错误消息（失败时）
        /// </summary>
        public string? ErrorMessage { get; }

        /// <summary>
        /// 异常（失败时）
        /// </summary>
        public Exception? Exception { get; }

        /// <summary>
        /// 执行耗时（毫秒）
        /// </summary>
        public long ElapsedMs { get; }

        /// <summary>
        /// 返回数据
        /// </summary>
        public object? Data { get; }

        /// <summary>
        /// 执行时间
        /// </summary>
        public DateTime ExecutedAt { get; }

        /// <summary>
        /// 重试次数
        /// </summary>
        public int RetryCount { get; }

        private CommandResult(
            string commandId,
            CommandType commandType,
            bool isSuccess,
            string? errorMessage,
            Exception? exception,
            long elapsedMs,
            object? data,
            int retryCount)
        {
            CommandId = commandId;
            CommandType = commandType;
            IsSuccess = isSuccess;
            ErrorMessage = errorMessage;
            Exception = exception;
            ElapsedMs = elapsedMs;
            Data = data;
            ExecutedAt = DateTime.Now;
            RetryCount = retryCount;
        }

        /// <summary>
        /// 创建成功结果
        /// </summary>
        public static CommandResult Success(AutomationCommand command, long elapsedMs, object? data = null)
            => new(command.CommandId, command.Type, true, null, null, elapsedMs, data, 0);

        /// <summary>
        /// 创建成功结果（带重试次数）
        /// </summary>
        public static CommandResult Success(AutomationCommand command, long elapsedMs, int retryCount, object? data = null)
            => new(command.CommandId, command.Type, true, null, null, elapsedMs, data, retryCount);

        /// <summary>
        /// 创建失败结果
        /// </summary>
        public static CommandResult Failed(AutomationCommand command, string errorMessage, long elapsedMs)
            => new(command.CommandId, command.Type, false, errorMessage, null, elapsedMs, null, 0);

        /// <summary>
        /// 创建失败结果（带异常）
        /// </summary>
        public static CommandResult Failed(AutomationCommand command, Exception ex, long elapsedMs, int retryCount = 0)
            => new(command.CommandId, command.Type, false, ex.Message, ex, elapsedMs, null, retryCount);

        /// <summary>
        /// 创建超时结果
        /// </summary>
        public static CommandResult Timeout(AutomationCommand command, long elapsedMs)
            => new(command.CommandId, command.Type, false, "执行超时", null, elapsedMs, null, 0);

        /// <summary>
        /// 创建取消结果
        /// </summary>
        public static CommandResult Cancelled(AutomationCommand command, long elapsedMs)
            => new(command.CommandId, command.Type, false, "执行已取消", null, elapsedMs, null, 0);
    }
}
