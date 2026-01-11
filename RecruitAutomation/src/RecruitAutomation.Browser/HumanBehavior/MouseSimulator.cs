using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace RecruitAutomation.Browser.HumanBehavior
{
    /// <summary>
    /// 鼠标行为模拟器
    /// 生成类人的鼠标移动轨迹
    /// </summary>
    public class MouseSimulator
    {
        private readonly HumanBehaviorConfig _config;

        public MouseSimulator(HumanBehaviorConfig? config = null)
        {
            _config = config ?? HumanBehaviorConfig.Default;
        }

        /// <summary>
        /// 生成从起点到终点的鼠标移动轨迹（贝塞尔曲线）
        /// </summary>
        /// <param name="startX">起始 X</param>
        /// <param name="startY">起始 Y</param>
        /// <param name="endX">目标 X</param>
        /// <param name="endY">目标 Y</param>
        /// <returns>轨迹点列表</returns>
        public List<(int X, int Y, int Delay)> GenerateMovePath(int startX, int startY, int endX, int endY)
        {
            var path = new List<(int X, int Y, int Delay)>();
            
            // 计算控制点（贝塞尔曲线）
            var (cp1X, cp1Y) = GenerateControlPoint(startX, startY, endX, endY, 0.3);
            var (cp2X, cp2Y) = GenerateControlPoint(startX, startY, endX, endY, 0.7);

            int steps = _config.MouseMoveSteps;
            
            for (int i = 0; i <= steps; i++)
            {
                double t = (double)i / steps;
                
                // 三次贝塞尔曲线
                var (x, y) = CalculateBezierPoint(t, startX, startY, cp1X, cp1Y, cp2X, cp2Y, endX, endY);
                
                // 添加微小随机偏移（模拟手抖）
                if (i > 0 && i < steps)
                {
                    x += RandomGenerator.Next(-_config.MousePathDeviation / 2, _config.MousePathDeviation / 2);
                    y += RandomGenerator.Next(-_config.MousePathDeviation / 2, _config.MousePathDeviation / 2);
                }

                // 随机延迟（使用高斯分布更自然）
                int delay = RandomGenerator.NextGaussianInt(_config.MouseMoveDelayMin, _config.MouseMoveDelayMax);
                
                path.Add((x, y, delay));
            }

            return path;
        }

        /// <summary>
        /// 生成贝塞尔曲线控制点
        /// </summary>
        private (int X, int Y) GenerateControlPoint(int startX, int startY, int endX, int endY, double position)
        {
            double range = _config.BezierControlPointRange;
            
            // 在直线上的基准点
            int baseX = (int)(startX + (endX - startX) * position);
            int baseY = (int)(startY + (endY - startY) * position);
            
            // 计算垂直于直线的偏移
            double distance = Math.Sqrt(Math.Pow(endX - startX, 2) + Math.Pow(endY - startY, 2));
            double offsetRange = distance * range;
            
            int offsetX = (int)(RandomGenerator.NextDouble(-offsetRange, offsetRange));
            int offsetY = (int)(RandomGenerator.NextDouble(-offsetRange, offsetRange));
            
            return (baseX + offsetX, baseY + offsetY);
        }

        /// <summary>
        /// 计算三次贝塞尔曲线上的点
        /// </summary>
        private (int X, int Y) CalculateBezierPoint(double t, 
            int p0X, int p0Y, int p1X, int p1Y, int p2X, int p2Y, int p3X, int p3Y)
        {
            double u = 1 - t;
            double tt = t * t;
            double uu = u * u;
            double uuu = uu * u;
            double ttt = tt * t;

            double x = uuu * p0X + 3 * uu * t * p1X + 3 * u * tt * p2X + ttt * p3X;
            double y = uuu * p0Y + 3 * uu * t * p1Y + 3 * u * tt * p2Y + ttt * p3Y;

            return ((int)Math.Round(x), (int)Math.Round(y));
        }

        /// <summary>
        /// 生成点击位置的随机偏移
        /// </summary>
        public (int OffsetX, int OffsetY) GenerateClickOffset()
        {
            int offset = _config.ClickPositionOffset;
            return (
                RandomGenerator.Next(-offset, offset),
                RandomGenerator.Next(-offset, offset)
            );
        }

        /// <summary>
        /// 生成点击前的延迟
        /// </summary>
        public int GenerateClickDelay()
        {
            return RandomGenerator.NextGaussianInt(_config.ClickDelayMin, _config.ClickDelayMax);
        }

        /// <summary>
        /// 生成鼠标按下持续时间
        /// </summary>
        public int GenerateClickHoldDuration()
        {
            return RandomGenerator.NextGaussianInt(_config.ClickHoldMin, _config.ClickHoldMax);
        }

        /// <summary>
        /// 生成鼠标移动的 JavaScript 代码
        /// </summary>
        public string GenerateMoveScript(int startX, int startY, int endX, int endY)
        {
            var path = GenerateMovePath(startX, startY, endX, endY);
            var script = new System.Text.StringBuilder();
            
            script.AppendLine("(async function() {");
            script.AppendLine("  const sleep = ms => new Promise(r => setTimeout(r, ms));");
            
            foreach (var (x, y, delay) in path)
            {
                script.AppendLine($"  document.dispatchEvent(new MouseEvent('mousemove', {{clientX: {x}, clientY: {y}, bubbles: true}}));");
                script.AppendLine($"  await sleep({delay});");
            }
            
            script.AppendLine("})();");
            return script.ToString();
        }
    }
}
