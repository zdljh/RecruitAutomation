using System;
using System.Threading;
using System.Threading.Tasks;

namespace RecruitAutomation.Core.Automation.AI
{
    /// <summary>
    /// AI 决策器接口（白图 AI 4.0 风格）
    /// 
    /// 设计原则：
    /// - 决策器只负责生成决策，不执行
    /// - 所有异常都被捕获，返回安全的默认决策
    /// - 决策过程可追溯
    /// </summary>
    public interface IAIDecisionMaker
    {
        /// <summary>
        /// 决策器名称
        /// </summary>
        string Name { get; }

        /// <summary>
        /// 支持的决策类型
        /// </summary>
        DecisionType[] SupportedTypes { get; }

        /// <summary>
        /// 生成决策
        /// </summary>
        Task<AIDecisionOutput> MakeDecisionAsync(AIDecisionContext context, CancellationToken ct = default);
    }

    /// <summary>
    /// AI 决策器基类
    /// </summary>
    public abstract class AIDecisionMakerBase : IAIDecisionMaker
    {
        public abstract string Name { get; }
        public abstract DecisionType[] SupportedTypes { get; }

        public async Task<AIDecisionOutput> MakeDecisionAsync(AIDecisionContext context, CancellationToken ct = default)
        {
            try
            {
                return await MakeDecisionCoreAsync(context, ct);
            }
            catch (OperationCanceledException)
            {
                return CreateNoActionDecision("决策已取消");
            }
            catch (Exception ex)
            {
                return CreateNoActionDecision($"决策异常: {ex.Message}");
            }
        }

        /// <summary>
        /// 核心决策逻辑（子类实现）
        /// </summary>
        protected abstract Task<AIDecisionOutput> MakeDecisionCoreAsync(AIDecisionContext context, CancellationToken ct);

        /// <summary>
        /// 创建无操作决策
        /// </summary>
        protected AIDecisionOutput CreateNoActionDecision(string reason)
        {
            return new AIDecisionOutput
            {
                Type = DecisionType.NoAction,
                Confidence = 1.0,
                Reasoning = reason
            };
        }
    }
}
