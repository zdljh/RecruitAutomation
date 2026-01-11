using System;
using System.Collections.Generic;
using RecruitAutomation.Core.AI.Providers;

namespace RecruitAutomation.Core.AI
{
    /// <summary>
    /// AI 提供商工厂
    /// </summary>
    public static class AIProviderFactory
    {
        private static readonly Dictionary<AIProviderType, Func<IAIProvider>> _creators = new()
        {
            [AIProviderType.Qwen] = () => new QwenProvider(),
            [AIProviderType.Kimi] = () => new KimiProvider(),
            [AIProviderType.Zhipu] = () => new ZhipuProvider()
        };

        /// <summary>
        /// 创建提供商实例
        /// </summary>
        public static IAIProvider Create(AIProviderType type, ProviderSettings settings)
        {
            if (!_creators.TryGetValue(type, out var creator))
            {
                throw new NotSupportedException($"不支持的 AI 提供商: {type}");
            }

            var provider = creator();
            provider.Initialize(settings);
            return provider;
        }

        /// <summary>
        /// 获取所有支持的提供商类型
        /// </summary>
        public static IEnumerable<AIProviderType> GetSupportedProviders()
        {
            return _creators.Keys;
        }

        /// <summary>
        /// 注册自定义提供商
        /// </summary>
        public static void Register(AIProviderType type, Func<IAIProvider> creator)
        {
            _creators[type] = creator;
        }
    }
}
