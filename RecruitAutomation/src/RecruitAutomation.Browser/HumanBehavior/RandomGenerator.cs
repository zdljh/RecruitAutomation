using System;
using System.Security.Cryptography;

namespace RecruitAutomation.Browser.HumanBehavior
{
    /// <summary>
    /// 安全随机数生成器
    /// 使用加密级随机数，避免被预测
    /// </summary>
    public static class RandomGenerator
    {
        private static readonly RandomNumberGenerator _rng = RandomNumberGenerator.Create();
        private static readonly Random _random = new(GetSecureSeed());

        private static int GetSecureSeed()
        {
            var bytes = new byte[4];
            _rng.GetBytes(bytes);
            return BitConverter.ToInt32(bytes, 0);
        }

        /// <summary>
        /// 生成指定范围内的随机整数 [min, max]
        /// </summary>
        public static int Next(int min, int max)
        {
            if (min >= max) return min;
            lock (_random)
            {
                return _random.Next(min, max + 1);
            }
        }

        /// <summary>
        /// 生成 0-1 之间的随机小数
        /// </summary>
        public static double NextDouble()
        {
            lock (_random)
            {
                return _random.NextDouble();
            }
        }

        /// <summary>
        /// 生成指定范围内的随机小数
        /// </summary>
        public static double NextDouble(double min, double max)
        {
            return min + NextDouble() * (max - min);
        }

        /// <summary>
        /// 根据概率返回是否命中
        /// </summary>
        /// <param name="probability">概率 0-1</param>
        public static bool Chance(double probability)
        {
            return NextDouble() < probability;
        }

        /// <summary>
        /// 生成高斯分布随机数（更自然的分布）
        /// </summary>
        public static double NextGaussian(double mean = 0, double stdDev = 1)
        {
            // Box-Muller 变换
            double u1, u2;
            lock (_random)
            {
                u1 = 1.0 - _random.NextDouble();
                u2 = 1.0 - _random.NextDouble();
            }
            double randStdNormal = Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Sin(2.0 * Math.PI * u2);
            return mean + stdDev * randStdNormal;
        }

        /// <summary>
        /// 生成高斯分布的整数（限制在范围内）
        /// </summary>
        public static int NextGaussianInt(int min, int max)
        {
            double mean = (min + max) / 2.0;
            double stdDev = (max - min) / 6.0; // 99.7% 在范围内
            int result = (int)Math.Round(NextGaussian(mean, stdDev));
            return Math.Clamp(result, min, max);
        }
    }
}
