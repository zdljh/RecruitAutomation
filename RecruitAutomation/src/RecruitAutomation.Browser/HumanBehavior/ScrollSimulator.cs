using System;
using System.Collections.Generic;
using System.Text;

namespace RecruitAutomation.Browser.HumanBehavior
{
    /// <summary>
    /// 滚动行为模拟器
    /// 生成类人的页面滚动行为
    /// </summary>
    public class ScrollSimulator
    {
        private readonly HumanBehaviorConfig _config;

        public ScrollSimulator(HumanBehaviorConfig? config = null)
        {
            _config = config ?? HumanBehaviorConfig.Default;
        }

        /// <summary>
        /// 生成滚动序列
        /// </summary>
        /// <param name="totalDistance">总滚动距离（正数向下，负数向上）</param>
        /// <returns>每步滚动距离和延迟</returns>
        public List<(int Distance, int Delay)> GenerateScrollSequence(int totalDistance)
        {
            var sequence = new List<(int Distance, int Delay)>();
            int remaining = Math.Abs(totalDistance);
            int direction = totalDistance > 0 ? 1 : -1;

            while (remaining > 0)
            {
                // 随机滚动距离（使用高斯分布）
                int scrollAmount = RandomGenerator.NextGaussianInt(
                    _config.ScrollDistanceMin, 
                    _config.ScrollDistanceMax);
                
                scrollAmount = Math.Min(scrollAmount, remaining);
                
                // 随机延迟
                int delay = RandomGenerator.NextGaussianInt(
                    _config.ScrollDelayMin, 
                    _config.ScrollDelayMax);

                // 偶尔添加短暂停顿（模拟阅读）
                if (RandomGenerator.Chance(0.15))
                {
                    delay += RandomGenerator.Next(200, 800);
                }

                sequence.Add((scrollAmount * direction, delay));
                remaining -= scrollAmount;
            }

            return sequence;
        }

        /// <summary>
        /// 生成平滑滚动的 JavaScript 代码
        /// </summary>
        public string GenerateSmoothScrollScript(int totalDistance)
        {
            var sequence = GenerateScrollSequence(totalDistance);
            var script = new StringBuilder();

            script.AppendLine("(async function() {");
            script.AppendLine("  const sleep = ms => new Promise(r => setTimeout(r, ms));");
            
            foreach (var (distance, delay) in sequence)
            {
                // 分步滚动，更平滑
                int steps = _config.ScrollSteps;
                int stepDistance = distance / steps;
                int stepDelay = delay / steps;

                for (int i = 0; i < steps; i++)
                {
                    script.AppendLine($"  window.scrollBy({{top: {stepDistance}, behavior: 'auto'}});");
                    script.AppendLine($"  await sleep({stepDelay});");
                }
            }

            script.AppendLine("})();");
            return script.ToString();
        }

        /// <summary>
        /// 生成滚动到元素的 JavaScript 代码
        /// </summary>
        public string GenerateScrollToElementScript(string selector, bool smooth = true)
        {
            var delay = RandomGenerator.NextGaussianInt(_config.ScrollDelayMin, _config.ScrollDelayMax);
            
            return $@"
(async function() {{
    const sleep = ms => new Promise(r => setTimeout(r, ms));
    const element = document.querySelector('{selector}');
    if (element) {{
        await sleep({delay});
        element.scrollIntoView({{ behavior: '{(smooth ? "smooth" : "auto")}', block: 'center' }});
        await sleep({delay});
    }}
}})();";
        }

        /// <summary>
        /// 生成随机浏览滚动（模拟用户浏览页面）
        /// </summary>
        public string GenerateBrowseScrollScript(int pageHeight, int viewportHeight)
        {
            var script = new StringBuilder();
            script.AppendLine("(async function() {");
            script.AppendLine("  const sleep = ms => new Promise(r => setTimeout(r, ms));");
            
            int currentPosition = 0;
            int maxScroll = pageHeight - viewportHeight;

            while (currentPosition < maxScroll)
            {
                // 随机滚动距离
                int scrollAmount = RandomGenerator.NextGaussianInt(
                    viewportHeight / 3, 
                    viewportHeight * 2 / 3);
                
                scrollAmount = Math.Min(scrollAmount, maxScroll - currentPosition);
                
                // 分步滚动
                int steps = RandomGenerator.Next(3, 8);
                int stepAmount = scrollAmount / steps;
                
                for (int i = 0; i < steps; i++)
                {
                    int stepDelay = RandomGenerator.Next(20, 50);
                    script.AppendLine($"  window.scrollBy({{top: {stepAmount}, behavior: 'auto'}});");
                    script.AppendLine($"  await sleep({stepDelay});");
                }

                currentPosition += scrollAmount;

                // 阅读停顿
                int readDelay = RandomGenerator.NextGaussianInt(800, 2500);
                script.AppendLine($"  await sleep({readDelay});");

                // 偶尔向上滚动一点（模拟回看）
                if (RandomGenerator.Chance(0.2) && currentPosition > viewportHeight)
                {
                    int backScroll = RandomGenerator.Next(50, 150);
                    script.AppendLine($"  window.scrollBy({{top: -{backScroll}, behavior: 'smooth'}});");
                    script.AppendLine($"  await sleep({RandomGenerator.Next(300, 600)});");
                }
            }

            script.AppendLine("})();");
            return script.ToString();
        }
    }
}
