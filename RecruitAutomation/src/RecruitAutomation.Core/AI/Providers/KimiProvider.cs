namespace RecruitAutomation.Core.AI.Providers
{
    /// <summary>
    /// Kimi (月之暗面) 提供商
    /// </summary>
    public class KimiProvider : BaseOpenAICompatibleProvider
    {
        public override AIProviderType ProviderType => AIProviderType.Kimi;
        public override string Name => "Kimi";

        /// <summary>
        /// 可用模型列表
        /// </summary>
        public static readonly string[] AvailableModels = new[]
        {
            "moonshot-v1-8k",    // 8K 上下文
            "moonshot-v1-32k",   // 32K 上下文
            "moonshot-v1-128k"   // 128K 上下文
        };
    }
}
