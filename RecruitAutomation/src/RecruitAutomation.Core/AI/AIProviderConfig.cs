using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace RecruitAutomation.Core.AI
{
    /// <summary>
    /// AI 提供商类型
    /// </summary>
    public enum AIProviderType
    {
        /// <summary>通义千问</summary>
        Qwen = 0,
        /// <summary>Kimi (月之暗面)</summary>
        Kimi = 1,
        /// <summary>智谱 GLM</summary>
        Zhipu = 2,
        /// <summary>百度文心</summary>
        Wenxin = 3,
        /// <summary>讯飞星火</summary>
        Spark = 4
    }

    /// <summary>
    /// AI 配置
    /// </summary>
    public class AIProviderConfig
    {
        /// <summary>
        /// 是否启用 AI 回复
        /// </summary>
        [JsonPropertyName("enabled")]
        public bool Enabled { get; set; } = false;

        /// <summary>
        /// 当前使用的提供商
        /// </summary>
        [JsonPropertyName("currentProvider")]
        public AIProviderType CurrentProvider { get; set; } = AIProviderType.Qwen;

        /// <summary>
        /// 各提供商配置
        /// </summary>
        [JsonPropertyName("providers")]
        public Dictionary<AIProviderType, ProviderSettings> Providers { get; set; } = new()
        {
            [AIProviderType.Qwen] = new ProviderSettings
            {
                Name = "通义千问",
                BaseUrl = "https://dashscope.aliyuncs.com/compatible-mode/v1",
                Model = "qwen-turbo"
            },
            [AIProviderType.Kimi] = new ProviderSettings
            {
                Name = "Kimi",
                BaseUrl = "https://api.moonshot.cn/v1",
                Model = "moonshot-v1-8k"
            },
            [AIProviderType.Zhipu] = new ProviderSettings
            {
                Name = "智谱GLM",
                BaseUrl = "https://open.bigmodel.cn/api/paas/v4",
                Model = "glm-4-flash"
            }
        };

        /// <summary>
        /// 系统提示词
        /// </summary>
        [JsonPropertyName("systemPrompt")]
        public string SystemPrompt { get; set; } = DefaultSystemPrompt;

        /// <summary>
        /// 不回复判断提示词
        /// </summary>
        [JsonPropertyName("shouldReplyPrompt")]
        public string ShouldReplyPrompt { get; set; } = DefaultShouldReplyPrompt;

        /// <summary>
        /// 最大历史消息数
        /// </summary>
        [JsonPropertyName("maxHistoryMessages")]
        public int MaxHistoryMessages { get; set; } = 10;

        /// <summary>
        /// 温度参数
        /// </summary>
        [JsonPropertyName("temperature")]
        public float Temperature { get; set; } = 0.7f;

        /// <summary>
        /// 最大输出 token
        /// </summary>
        [JsonPropertyName("maxTokens")]
        public int MaxTokens { get; set; } = 500;

        /// <summary>
        /// 请求超时（秒）
        /// </summary>
        [JsonPropertyName("timeoutSeconds")]
        public int TimeoutSeconds { get; set; } = 30;

        public const string DefaultSystemPrompt = @"你是一位专业的HR招聘助手，正在与求职者沟通。请根据以下信息生成合适的回复：

【岗位信息】
{jobInfo}

【候选人简历】
{resumeInfo}

【沟通要求】
1. 语气友好专业，像真人HR一样沟通
2. 回复简洁，一般不超过100字
3. 根据候选人问题给出针对性回答
4. 适时引导候选人进入下一步（留电话、约面试等）
5. 如果候选人明确拒绝，礼貌结束对话";

        public const string DefaultShouldReplyPrompt = @"请判断是否应该自动回复这条消息。

【候选人消息】
{message}

【判断标准】
- 如果是正常的求职沟通问题（薪资、工作内容、公司情况等），回复 YES
- 如果是表情、单字回复（嗯、哦、好）等无实质内容，回复 NO
- 如果是明显的广告、骚扰、无关内容，回复 NO
- 如果是简单的打招呼（你好、在吗），回复 YES

只回复 YES 或 NO，不要其他内容。";

        /// <summary>
        /// 获取当前提供商配置
        /// </summary>
        public ProviderSettings GetCurrentProvider()
        {
            return Providers.TryGetValue(CurrentProvider, out var settings) 
                ? settings 
                : Providers[AIProviderType.Qwen];
        }
    }

    /// <summary>
    /// 单个提供商设置
    /// </summary>
    public class ProviderSettings
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("apiKey")]
        public string ApiKey { get; set; } = string.Empty;

        [JsonPropertyName("baseUrl")]
        public string BaseUrl { get; set; } = string.Empty;

        [JsonPropertyName("model")]
        public string Model { get; set; } = string.Empty;

        [JsonPropertyName("enabled")]
        public bool Enabled { get; set; } = true;
    }
}
