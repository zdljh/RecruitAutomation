using System;

namespace RecruitAutomation.Core.Automation.Commands
{
    /// <summary>
    /// 延迟指令
    /// </summary>
    public sealed class DelayCommand : AutomationCommand
    {
        public override CommandType Type => CommandType.Delay;

        /// <summary>
        /// 延迟时间（毫秒）
        /// </summary>
        public int DelayMs { get; set; }

        /// <summary>
        /// 是否随机化延迟
        /// </summary>
        public bool Randomize { get; set; } = true;

        /// <summary>
        /// 随机范围（±百分比）
        /// </summary>
        public int RandomRangePercent { get; set; } = 20;

        public DelayCommand() { }

        public DelayCommand(int delayMs)
        {
            DelayMs = delayMs;
        }
    }

    /// <summary>
    /// 日志指令
    /// </summary>
    public sealed class LogCommand : AutomationCommand
    {
        public override CommandType Type => CommandType.Log;

        /// <summary>
        /// 日志级别
        /// </summary>
        public LogLevel Level { get; set; } = LogLevel.Info;

        /// <summary>
        /// 日志消息
        /// </summary>
        public string Message { get; set; } = string.Empty;

        /// <summary>
        /// 附加数据
        /// </summary>
        public object? Data { get; set; }
    }

    /// <summary>
    /// 检查点指令（用于流程控制）
    /// </summary>
    public sealed class CheckpointCommand : AutomationCommand
    {
        public override CommandType Type => CommandType.Checkpoint;

        /// <summary>
        /// 检查点名称
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// 是否需要确认
        /// </summary>
        public bool RequireConfirm { get; set; }

        /// <summary>
        /// 检查点描述
        /// </summary>
        public string? Description { get; set; }
    }

    /// <summary>
    /// 日志级别
    /// </summary>
    public enum LogLevel
    {
        Debug,
        Info,
        Warning,
        Error
    }
}
