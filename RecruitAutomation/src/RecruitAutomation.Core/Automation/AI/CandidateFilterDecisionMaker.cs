using System;
using System.Threading;
using System.Threading.Tasks;
using RecruitAutomation.Core.Automation.Commands;

namespace RecruitAutomation.Core.Automation.AI
{
    /// <summary>
    /// 候选人筛选决策器（占位实现）
    /// 
    /// 后续接入真实 AI 时，只需修改 MakeDecisionCoreAsync 的实现
    /// </summary>
    public sealed class CandidateFilterDecisionMaker : AIDecisionMakerBase
    {
        public override string Name => "CandidateFilter";

        public override DecisionType[] SupportedTypes => new[] { DecisionType.FilterCandidate };

        protected override Task<AIDecisionOutput> MakeDecisionCoreAsync(AIDecisionContext context, CancellationToken ct)
        {
            var output = new AIDecisionOutput
            {
                Type = DecisionType.FilterCandidate,
                Context = context
            };

            // 占位逻辑：简单规则筛选
            var candidate = context.Candidate;
            if (candidate == null)
            {
                output.Confidence = 0;
                output.Reasoning = "缺少候选人信息";
                return Task.FromResult(output);
            }

            // 生成筛选指令
            var filterCmd = new FilterCandidateCommand
            {
                CandidateId = candidate.Id,
                AccountId = context.AccountId
            };

            // 占位规则：工作年限 >= 3 年通过
            if (candidate.WorkYears.HasValue && candidate.WorkYears.Value >= 3)
            {
                filterCmd.Decision = FilterDecision.Pass;
                filterCmd.Reason = $"工作年限 {candidate.WorkYears} 年，符合要求";
                filterCmd.MatchScore = 80;
                filterCmd.MatchedCriteria.Add("工作年限");
                output.Confidence = 0.8;
            }
            else
            {
                filterCmd.Decision = FilterDecision.Pending;
                filterCmd.Reason = "工作年限不足，待人工审核";
                filterCmd.MatchScore = 50;
                filterCmd.UnmatchedCriteria.Add("工作年限");
                output.Confidence = 0.5;
                output.RequireHumanConfirm = true;
                output.ConfirmReason = "工作年限不足，建议人工确认";
            }

            output.Commands.Add(filterCmd);
            output.Reasoning = $"基于规则筛选: {filterCmd.Reason}";

            return Task.FromResult(output);
        }
    }
}
