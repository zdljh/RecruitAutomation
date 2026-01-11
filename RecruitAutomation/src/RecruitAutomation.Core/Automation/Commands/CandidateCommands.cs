using System;
using System.Collections.Generic;

namespace RecruitAutomation.Core.Automation.Commands
{
    /// <summary>
    /// 筛选候选人指令
    /// </summary>
    public sealed class FilterCandidateCommand : AutomationCommand
    {
        public override CommandType Type => CommandType.FilterCandidate;

        /// <summary>
        /// 候选人 ID
        /// </summary>
        public string CandidateId { get; set; } = string.Empty;

        /// <summary>
        /// 筛选结果
        /// </summary>
        public FilterDecision Decision { get; set; }

        /// <summary>
        /// 筛选理由
        /// </summary>
        public string Reason { get; set; } = string.Empty;

        /// <summary>
        /// 匹配分数（0-100）
        /// </summary>
        public int MatchScore { get; set; }

        /// <summary>
        /// 匹配的条件
        /// </summary>
        public List<string> MatchedCriteria { get; } = new();

        /// <summary>
        /// 不匹配的条件
        /// </summary>
        public List<string> UnmatchedCriteria { get; } = new();
    }

    /// <summary>
    /// 筛选决策
    /// </summary>
    public enum FilterDecision
    {
        /// <summary>通过</summary>
        Pass,
        /// <summary>拒绝</summary>
        Reject,
        /// <summary>待定</summary>
        Pending,
        /// <summary>需人工审核</summary>
        NeedReview
    }

    /// <summary>
    /// 收集简历指令
    /// </summary>
    public sealed class CollectResumeCommand : AutomationCommand
    {
        public override CommandType Type => CommandType.CollectResume;

        /// <summary>
        /// 候选人 ID
        /// </summary>
        public string CandidateId { get; set; } = string.Empty;

        /// <summary>
        /// 简历来源
        /// </summary>
        public string Source { get; set; } = string.Empty;

        /// <summary>
        /// 保存路径
        /// </summary>
        public string? SavePath { get; set; }
    }

    /// <summary>
    /// 标记候选人指令
    /// </summary>
    public sealed class MarkCandidateCommand : AutomationCommand
    {
        public override CommandType Type => CommandType.MarkCandidate;

        /// <summary>
        /// 候选人 ID
        /// </summary>
        public string CandidateId { get; set; } = string.Empty;

        /// <summary>
        /// 标记类型
        /// </summary>
        public string MarkType { get; set; } = string.Empty;

        /// <summary>
        /// 标记值
        /// </summary>
        public string MarkValue { get; set; } = string.Empty;

        /// <summary>
        /// 备注
        /// </summary>
        public string? Note { get; set; }
    }
}
