using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using RecruitAutomation.Core.Automation.Execution;

namespace RecruitAutomation.Core.Automation.AI
{
    /// <summary>
    /// AI 决策服务 - 白图 AI 4.0 规范重构版
    /// 1. 决策与执行彻底解耦
    /// 2. 异常全捕获，不影响 UI 稳定性
    /// 3. 结构化指令 (DTO) 输出
    /// </summary>
    public sealed class AIDecisionService
    {
        private static readonly Lazy<AIDecisionService> _instance =
            new(() => new AIDecisionService(), LazyThreadSafetyMode.ExecutionAndPublication);

        public static AIDecisionService Instance => _instance.Value;

        private readonly ConcurrentDictionary<DecisionType, IAIDecisionMaker> _decisionMakers = new();

        public event EventHandler<AIDecisionOutput>? DecisionMade;

        private AIDecisionService()
        {
            RegisterDecisionMaker(new ReplyGeneratorDecisionMaker());
            RegisterDecisionMaker(new CandidateFilterDecisionMaker());
        }

        public void RegisterDecisionMaker(IAIDecisionMaker maker)
        {
            foreach (var type in maker.SupportedTypes)
            {
                _decisionMakers[type] = maker;
            }
        }

        /// <summary>
        /// 请求 AI 决策并返回结构化指令
        /// </summary>
        public async Task<AIDecisionOutput> RequestDecisionAsync(
            DecisionType type,
            AIDecisionContext context,
            CancellationToken ct = default)
        {
            try
            {
                if (!_decisionMakers.TryGetValue(type, out var maker))
                {
                    return CreateEmptyDecision(type, "未找到对应的决策器");
                }

                // 执行决策逻辑
                var decision = await maker.MakeDecisionAsync(context, ct);
                
                // 触发事件供 UI 监听
                DecisionMade?.Invoke(this, decision);

                // 如果不需要人工确认，则自动提交指令
                if (!decision.RequireHumanConfirm && decision.Commands.Count > 0)
                {
                    _ = Task.Run(() => CommandDispatcher.Instance.SubmitBatch(decision.Commands), ct);
                }

                return decision;
            }
            catch (Exception ex)
            {
                // 异常隔离：记录日志并返回安全降级结果
                return CreateEmptyDecision(type, $"决策异常: {ex.Message}");
            }
        }

        private AIDecisionOutput CreateEmptyDecision(DecisionType type, string reason)
        {
            return new AIDecisionOutput
            {
                Type = type,
                Confidence = 0,
                Reasoning = reason,
                RequireHumanConfirm = true // 异常时强制人工介入
            };
        }
    }
}
