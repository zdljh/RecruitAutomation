using System;
using System.Collections.Generic;
using RecruitAutomation.Core.Automation.Commands;

namespace RecruitAutomation.Core.Automation.AI
{
    /// <summary>
    /// AI 决策输出（白图 AI 4.0 风格）
    /// 
    /// 设计原则：
    /// - AI 只输出结构化决策，不直接操作
    /// - 决策包含指令列表和推理过程
    /// - 所有决策都可追溯和审计
    /// </summary>
    public sealed class AIDecisionOutput
    {
        /// <summary>
        /// 决策 ID
        /// </summary>
        public string DecisionId { get; } = Guid.NewGuid().ToString("N");

        /// <summary>
        /// 决策时间
        /// </summary>
        public DateTime CreatedAt { get; } = DateTime.Now;

        /// <summary>
        /// 决策类型
        /// </summary>
        public DecisionType Type { get; set; }

        /// <summary>
        /// 置信度（0-1）
        /// </summary>
        public double Confidence { get; set; }

        /// <summary>
        /// 推理过程
        /// </summary>
        public string Reasoning { get; set; } = string.Empty;

        /// <summary>
        /// 生成的指令列表
        /// </summary>
        public List<AutomationCommand> Commands { get; } = new();

        /// <summary>
        /// 输入上下文
        /// </summary>
        public AIDecisionContext? Context { get; set; }

        /// <summary>
        /// 是否需要人工确认
        /// </summary>
        public bool RequireHumanConfirm { get; set; }

        /// <summary>
        /// 确认原因
        /// </summary>
        public string? ConfirmReason { get; set; }

        /// <summary>
        /// 附加数据
        /// </summary>
        public Dictionary<string, object> Metadata { get; } = new();
    }

    /// <summary>
    /// 决策类型
    /// </summary>
    public enum DecisionType
    {
        /// <summary>筛选候选人</summary>
        FilterCandidate,
        /// <summary>生成回复</summary>
        GenerateReply,
        /// <summary>发送招呼</summary>
        SendGreeting,
        /// <summary>收集简历</summary>
        CollectResume,
        /// <summary>跟进候选人</summary>
        FollowUp,
        /// <summary>无操作</summary>
        NoAction
    }

    /// <summary>
    /// AI 决策上下文
    /// </summary>
    public sealed class AIDecisionContext
    {
        /// <summary>
        /// 候选人信息
        /// </summary>
        public CandidateContext? Candidate { get; set; }

        /// <summary>
        /// 职位信息
        /// </summary>
        public JobContext? Job { get; set; }

        /// <summary>
        /// 消息历史
        /// </summary>
        public List<MessageContext> Messages { get; } = new();

        /// <summary>
        /// 账号 ID
        /// </summary>
        public string? AccountId { get; set; }

        /// <summary>
        /// 触发来源
        /// </summary>
        public string? TriggerSource { get; set; }
    }

    /// <summary>
    /// 候选人上下文
    /// </summary>
    public sealed class CandidateContext
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? Title { get; set; }
        public string? Company { get; set; }
        public string? Education { get; set; }
        public int? WorkYears { get; set; }
        public string? Location { get; set; }
        public string? ResumeText { get; set; }
    }

    /// <summary>
    /// 职位上下文
    /// </summary>
    public sealed class JobContext
    {
        public string Id { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? Requirements { get; set; }
        public string? SalaryRange { get; set; }
        public string? Location { get; set; }
    }

    /// <summary>
    /// 消息上下文
    /// </summary>
    public sealed class MessageContext
    {
        public string Content { get; set; } = string.Empty;
        public bool IsFromCandidate { get; set; }
        public DateTime SentAt { get; set; }
    }
}
