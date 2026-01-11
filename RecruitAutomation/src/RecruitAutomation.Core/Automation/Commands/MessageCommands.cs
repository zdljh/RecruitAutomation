using System;

namespace RecruitAutomation.Core.Automation.Commands
{
    /// <summary>
    /// 发送消息指令
    /// </summary>
    public sealed class SendMessageCommand : AutomationCommand
    {
        public override CommandType Type => CommandType.SendMessage;

        /// <summary>
        /// 目标候选人 ID
        /// </summary>
        public string CandidateId { get; set; } = string.Empty;

        /// <summary>
        /// 消息内容
        /// </summary>
        public string Content { get; set; } = string.Empty;

        /// <summary>
        /// 是否使用 AI 生成
        /// </summary>
        public bool UseAIGenerated { get; set; }

        /// <summary>
        /// AI 生成的上下文
        /// </summary>
        public string? AIContext { get; set; }
    }

    /// <summary>
    /// 发送招呼指令
    /// </summary>
    public sealed class SendGreetingCommand : AutomationCommand
    {
        public override CommandType Type => CommandType.SendGreeting;

        /// <summary>
        /// 目标候选人 ID
        /// </summary>
        public string CandidateId { get; set; } = string.Empty;

        /// <summary>
        /// 招呼模板 ID
        /// </summary>
        public string? TemplateId { get; set; }

        /// <summary>
        /// 招呼内容（如果不使用模板）
        /// </summary>
        public string? Content { get; set; }

        /// <summary>
        /// 职位 ID
        /// </summary>
        public string? JobId { get; set; }
    }

    /// <summary>
    /// 回复消息指令
    /// </summary>
    public sealed class ReplyMessageCommand : AutomationCommand
    {
        public override CommandType Type => CommandType.ReplyMessage;

        /// <summary>
        /// 会话 ID
        /// </summary>
        public string ConversationId { get; set; } = string.Empty;

        /// <summary>
        /// 回复内容
        /// </summary>
        public string Content { get; set; } = string.Empty;

        /// <summary>
        /// 原始消息（用于上下文）
        /// </summary>
        public string? OriginalMessage { get; set; }

        /// <summary>
        /// AI 决策理由
        /// </summary>
        public string? AIReasoning { get; set; }
    }
}
