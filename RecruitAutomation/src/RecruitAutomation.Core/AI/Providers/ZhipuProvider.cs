using System;
using System.Net.Http.Headers;

namespace RecruitAutomation.Core.AI.Providers
{
    /// <summary>
    /// 智谱 GLM 提供商
    /// </summary>
    public class ZhipuProvider : BaseOpenAICompatibleProvider
    {
        public override AIProviderType ProviderType => AIProviderType.Zhipu;
        public override string Name => "智谱GLM";

        /// <summary>
        /// 可用模型列表
        /// </summary>
        public static readonly string[] AvailableModels = new[]
        {
            "glm-4-flash",   // 免费，速度快
            "glm-4-air",     // 性价比高
            "glm-4",         // 旗舰模型
            "glm-4-plus"     // 增强版
        };

        public override void Initialize(ProviderSettings settings)
        {
            _settings = settings;
            _httpClient = new System.Net.Http.HttpClient
            {
                BaseAddress = new Uri(settings.BaseUrl.TrimEnd('/')),
                Timeout = TimeSpan.FromSeconds(60)
            };
            // 智谱使用 API Key 直接作为 Bearer Token
            _httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", settings.ApiKey);
            _httpClient.DefaultRequestHeaders.Accept.Add(
                new MediaTypeWithQualityHeaderValue("application/json"));
        }
    }
}
