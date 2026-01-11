using System;
using System.Threading;
using System.Threading.Tasks;

namespace RecruitAutomation.Browser.HumanBehavior
{
    /// <summary>
    /// 操作节奏调度器
    /// 控制自动化操作的执行节奏，模拟人类操作间隔
    /// </summary>
    public class ActionScheduler
    {
        private readonly HumanBehaviorConfig _config;
        private DateTime _lastActionTime = DateTime.MinValue;
        private int _consecutiveActions;

        public ActionScheduler(HumanBehaviorConfig? config = null)
        {
            _config = config ?? HumanBehaviorConfig.Default;
        }

        /// <summary>
        /// 等待下一次操作（自动计算延迟）
        /// </summary>
        public async Task WaitForNextAction(CancellationToken cancellationToken = default)
        {
            int delay = CalculateNextDelay();
            await Task.Delay(delay, cancellationToken);
            _lastActionTime = DateTime.Now;
            _consecutiveActions++;
        }

        /// <summary>
        /// 计算下一次操作的延迟
        /// </summary>
        private int CalculateNextDelay()
        {
            // 基础延迟
            int baseDelay = RandomGenerator.NextGaussianInt(
                _config.ActionIntervalMin, 
                _config.ActionIntervalMax);

            // 连续操作后增加延迟（模拟疲劳）
            if (_consecutiveActions > 10)
            {
                baseDelay = (int)(baseDelay * 1.2);
            }
            if (_consecutiveActions > 20)
            {
                baseDelay = (int)(baseDelay * 1.5);
            }

            // 随机长暂停
            if (RandomGenerator.Chance(_config.LongPauseRate))
            {
                baseDelay += _config.LongPauseDuration;
                _consecutiveActions = 0; // 重置计数
            }

            return baseDelay;
        }

        /// <summary>
        /// 等待指定范围内的随机时间
        /// </summary>
        public async Task WaitRandom(int minMs, int maxMs, CancellationToken cancellationToken = default)
        {
            int delay = RandomGenerator.NextGaussianInt(minMs, maxMs);
            await Task.Delay(delay, cancellationToken);
        }

        /// <summary>
        /// 等待短暂时间（用于操作间的微小间隔）
        /// </summary>
        public async Task WaitShort(CancellationToken cancellationToken = default)
        {
            await WaitRandom(50, 200, cancellationToken);
        }

        /// <summary>
        /// 等待中等时间（用于页面加载等）
        /// </summary>
        public async Task WaitMedium(CancellationToken cancellationToken = default)
        {
            await WaitRandom(500, 1500, cancellationToken);
        }

        /// <summary>
        /// 等待较长时间（模拟阅读/思考）
        /// </summary>
        public async Task WaitLong(CancellationToken cancellationToken = default)
        {
            await WaitRandom(2000, 5000, cancellationToken);
        }

        /// <summary>
        /// 模拟阅读页面内容
        /// </summary>
        /// <param name="contentLength">内容长度（字符数）</param>
        public async Task SimulateReading(int contentLength, CancellationToken cancellationToken = default)
        {
            // 假设平均阅读速度 300-500 字/分钟
            int readingSpeed = RandomGenerator.Next(300, 500);
            int readingTimeMs = (int)((contentLength / (double)readingSpeed) * 60 * 1000);
            
            // 限制最大阅读时间
            readingTimeMs = Math.Min(readingTimeMs, 30000);
            readingTimeMs = Math.Max(readingTimeMs, 1000);

            // 添加随机波动
            readingTimeMs = (int)(readingTimeMs * RandomGenerator.NextDouble(0.8, 1.2));

            await Task.Delay(readingTimeMs, cancellationToken);
        }

        /// <summary>
        /// 重置操作计数
        /// </summary>
        public void Reset()
        {
            _consecutiveActions = 0;
            _lastActionTime = DateTime.MinValue;
        }
    }
}
