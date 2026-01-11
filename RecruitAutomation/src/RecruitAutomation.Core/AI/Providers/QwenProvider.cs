namespace RecruitAutomation.Core.AI.Providers
{
    /// <summary>
    /// 通义千问提供商
    /// 使用 DashScope 兼容 OpenAI 接口
    /// </summary>
    public class QwenProvider : BaseOpenAICompatibleProvider
    {
        public override AIProviderType ProviderType => AIProviderType.Qwen;
        public override string Name => "通义千问";

        /// <summary>
        /// 可用模型列表
        /// </summary>
        public static readonly string[] AvailableModels = new[]
        {
            "qwen-turbo",       // 速度快，成本低
            "qwen-plus",        // 平衡性能
            "qwen-max",         // 最强能力
            "qwen-max-longcontext"  // 长上下文
        };
    }
}
