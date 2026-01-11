using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using RecruitAutomation.Core.Automation.Execution;

namespace RecruitAutomation.Core.Automation.AI
{
    /// <summary>
    /// AI 决策服务（白图 AI 4.0 风格）
    /// 
    /// 核心职责：
    /// 1. 管理所有 AI 决策器
    /// 2. 接收上下文，生成决策
    /// 3. 将决策转换为指令提交给调度器
    /// 4. 处理需要人工确认的决策
    /// 
    /// 设计原则：
    /// - 所有异常都被捕获，不会传播到 UI
    /// - 决策过程完全异步
    /// - 支持决策审计和追溯
    /// </summary>
    public sealed class AIDecisionService
    {
        private static readonly Lazy<AIDecisionService> _instance =
            new(() => new AIDecisionService(), LazyThreadSafetyMode.ExecutionAndPublication);

        public static AIDecisionService Instance => _instance.Value;

        private readonly ConcurrentDictionary<DecisionType, IAIDecisionMaker> _decisionMakers = new();
        private readonly ConcurrentQueue<AIDecisionOutput> _pendingConfirmations = new();

        // 事件
        public event EventHandler<DecisionMadeEventArgs>? DecisionMade;
        public event EventHandler<DecisionConfirmRequiredEventArgs>? ConfirmRequired;
        public event EventHandler<DecisionErrorEventArgs>? Error;

        private AIDecisionService()
        {
            // 注册默认决策器
            RegisterDecisionMaker(new ReplyGeneratorDecisionMaker());
            RegisterDecisionMaker(new CandidateFilterDecisionMaker());
        }

        /// <summary>
        /// 注册决策器
        /// </summary>
        public void RegisterDecisionMaker(IAIDecisionMaker maker)
        {
            foreach (var type in maker.SupportedTypes)
            {
                _decisionMakers[type] = maker;
            }
        }

        /// <summary>
        /// 请求决策
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
                    return new AIDecisionOutput
                    {
                        Type = DecisionType.NoAction,
                        Confidence = 0,
                        Reasoning = $"未找到决策类型 {type} 的决策器"
                    };
                }

                var decision = await maker.MakeDecisionAsync(context, ct);

                SafeRaiseEvent(() => DecisionMade?.Invoke(this, new DecisionMadeEventArgs(decision)));

                // 如果需要人工确认，加入待确认队列
                if (decision.RequireHumanConfirm)
                {
                    _pendingConfirmations.Enqueue(decision);
                    SafeRaiseEvent(() => ConfirmRequired?.Invoke(this, new DecisionConfirmRequiredEventArgs(decision)));
                }
                else
                {
                    // 自动提交指令到调度器
                    SubmitToDispatcher(decision);
                }

                return decision;
            }
            catch (Exception ex)
            {
                SafeRaiseEvent(() => Error?.Invoke(this, new DecisionErrorEventArgs(type, context, ex)));

                return new AIDecisionOutput
                {
                    Type = DecisionType.NoAction,
                    Confidence = 0,
                    Reasoning = $"决策异常: {ex.Message}"
                };
            }
        }

        /// <summary>
        /// 确认决策（人工确认后调用）
        /// </summary>
        public void ConfirmDecision(string decisionId, bool approved)
        {
            // 从待确认队列中找到决策
            var tempQueue = new ConcurrentQueue<AIDecisionOutput>();

            while (_pendingConfirmations.TryDequeue(out var decision))
            {
                if (decision.DecisionId == decisionId)
                {
                    if (approved)
                    {
                        SubmitToDispatcher(decision);
                    }
                    // 不管是否批准，都不再放回队列
                }
                else
                {
                    tempQueue.Enqueue(decision);
                }
            }

            // 把其他决策放回队列
            while (tempQueue.TryDequeue(out var d))
            {
                _pendingConfirmations.Enqueue(d);
            }
        }

        /// <summary>
        /// 获取待确认决策数量
        /// </summary>
        public int PendingConfirmationCount => _pendingConfirmations.Count;

        /// <summary>
        /// 提交决策到调度器
        /// </summary>
        private void SubmitToDispatcher(AIDecisionOutput decision)
        {
            try
            {
                if (decision.Commands.Count > 0)
                {
                    CommandDispatcher.Instance.SubmitBatch(decision.Commands);
                }
            }
            catch (Exception ex)
            {
                SafeRaiseEvent(() => Error?.Invoke(this, new DecisionErrorEventArgs(decision.Type, decision.Context, ex)));
            }
        }

        private void SafeRaiseEvent(Action action)
        {
            try { action(); } catch { }
        }
    }

    #region 事件参数

    public class DecisionMadeEventArgs : EventArgs
    {
        public AIDecisionOutput Decision { get; }
        public DecisionMadeEventArgs(AIDecisionOutput decision) => Decision = decision;
    }

    public class DecisionConfirmRequiredEventArgs : EventArgs
    {
        public AIDecisionOutput Decision { get; }
        public DecisionConfirmRequiredEventArgs(AIDecisionOutput decision) => Decision = decision;
    }

    public class DecisionErrorEventArgs : EventArgs
    {
        public DecisionType Type { get; }
        public AIDecisionContext? Context { get; }
        public Exception Exception { get; }

        public DecisionErrorEventArgs(DecisionType type, AIDecisionContext? context, Exception exception)
        {
            Type = type;
            Context = context;
            Exception = exception;
        }
    }

    #endregion
}
