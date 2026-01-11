using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using RecruitAutomation.Core.Automation.Commands;

namespace RecruitAutomation.Core.Automation.AI
{
    /// <summary>
    /// 回复生成决策器（占位实现）
    /// 
    /// 后续接入真实 AI 时，只需修改 MakeDecisionCoreAsync 的实现
    /// </summary>
    public sealed class ReplyGeneratorDecisionMaker : AIDecisionMakerBase
    {
        public override string Name => "ReplyGenerator";

        public override DecisionType[] SupportedTypes => new[] { DecisionType.GenerateReply };

        protected override Task<AIDecisionOutput> MakeDecisionCoreAsync(AIDecisionContext context, CancellationToken ct)
        {
            var output = new AIDecisionOutput
            {
                Type = DecisionType.GenerateReply,
                Context = context
            };

            // 获取最后一条候选人消息
            var lastMessage = context.Messages
                .Where(m => m.IsFromCandidate)
                .OrderByDescending(m => m.SentAt)
                .FirstOrDefault();

            if (lastMessage == null)
            {
                output.Confidence = 0;
                output.Reasoning = "没有需要回复的消息";
                return Task.FromResult(output);
            }

            // 占位逻辑：生成简单回复
            var replyContent = GeneratePlaceholderReply(lastMessage.Content, context);

            var replyCmd = new ReplyMessageCommand
            {
                ConversationId = context.Candidate?.Id ?? string.Empty,
                Content = replyContent,
                OriginalMessage = lastMessage.Content,
                AIReasoning = "基于模板生成的占位回复",
                AccountId = context.AccountId
            };

            output.Commands.Add(replyCmd);
            output.Confidence = 0.7;
            output.Reasoning = "使用占位模板生成回复";

            // 如果消息包含敏感词，需要人工确认
            if (ContainsSensitiveWords(lastMessage.Content))
            {
                output.RequireHumanConfirm = true;
                output.ConfirmReason = "消息可能包含敏感内容，建议人工确认";
            }

            return Task.FromResult(output);
        }

        private string GeneratePlaceholderReply(string originalMessage, AIDecisionContext context)
        {
            // 占位实现：简单模板回复
            var candidateName = context.Candidate?.Name ?? "您";

            if (originalMessage.Contains("薪资") || originalMessage.Contains("工资"))
            {
                return $"{candidateName}您好，关于薪资问题，我们会根据您的经验和能力综合评估，具体可以在面试时详细沟通。";
            }

            if (originalMessage.Contains("面试") || originalMessage.Contains("时间"))
            {
                return $"{candidateName}您好，请问您方便的面试时间是什么时候？我们可以安排线上或线下面试。";
            }

            return $"{candidateName}您好，感谢您的回复。请问您还有什么想了解的吗？";
        }

        private bool ContainsSensitiveWords(string content)
        {
            var sensitiveWords = new[] { "投诉", "举报", "法律", "律师" };
            return sensitiveWords.Any(w => content.Contains(w));
        }
    }
}
