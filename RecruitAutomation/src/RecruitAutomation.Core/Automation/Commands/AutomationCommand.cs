using System;
using System.Collections.Generic;

namespace RecruitAutomation.Core.Automation.Commands
{
    /// <summary>
    /// 自动化指令基类（白图 AI 4.0 风格）
    /// 
    /// 设计原则：
    /// - AI 只输出结构化指令，不直接操作 UI
    /// - Automation 只执行指令，不做决策
    /// - UI 只展示状态，不参与业务逻辑
    /// </summary>
    public abstract class AutomationCommand
    {
        /// <summary>
        /// 指令唯一 ID
        /// </summary>
        public string CommandId { get; } = Guid.NewGuid().ToString("N");

        /// <summary>
        /// 指令类型
        /// </summary>
        public abstract CommandType Type { get; }

        /// <summary>
        /// 创建时间
        /// </summary>
        public DateTime CreatedAt { get; } = DateTime.Now;

        /// <summary>
        /// 优先级（0-100，越大越优先）
        /// </summary>
        public int Priority { get; set; } = 50;

        /// <summary>
        /// 目标账号 ID（可选）
        /// </summary>
        public string? AccountId { get; set; }

        /// <summary>
        /// 超时时间（毫秒）
        /// </summary>
        public int TimeoutMs { get; set; } = 30000;

        /// <summary>
        /// 重试次数
        /// </summary>
        public int MaxRetries { get; set; } = 3;

        /// <summary>
        /// 附加参数
        /// </summary>
        public Dictionary<string, object> Metadata { get; } = new();
    }

    /// <summary>
    /// 指令类型枚举
    /// </summary>
    public enum CommandType
    {
        // 消息类
        SendMessage,
        SendGreeting,
        ReplyMessage,

        // 候选人类
        FilterCandidate,
        CollectResume,
        MarkCandidate,

        // 浏览器类
        NavigateTo,
        ClickElement,
        InputText,
        ScrollPage,
        WaitElement,

        // 系统类
        Delay,
        Log,
        Checkpoint
    }
}
