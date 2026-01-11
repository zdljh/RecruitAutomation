using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace RecruitAutomation.Core.AI
{
    /// <summary>
    /// AI 提供商接口
    /// </summary>
    public interface IAIProvider
    {
        /// <summary>
        /// 提供商类型
        /// </summary>
        AIProviderType ProviderType { get; }

        /// <summary>
        /// 提供商名称
        /// </summary>
        string Name { get; }

        /// <summary>
        /// 初始化
        /// </summary>
        void Initialize(ProviderSettings settings);

        /// <summary>
        /// 发送聊天请求
        /// </summary>
        Task<AIResponse> ChatAsync(
            List<ChatMessageItem> messages,
            float temperature = 0.7f,
            int maxTokens = 500,
            CancellationToken ct = default);

        /// <summary>
        /// 测试连接
        /// </summary>
        Task<bool> TestConnectionAsync(CancellationToken ct = default);
    }

    /// <summary>
    /// 聊天消息项
    /// </summary>
    public class ChatMessageItem
    {
        public string Role { get; set; } = "user";
        public string Content { get; set; } = string.Empty;

        public static ChatMessageItem System(string content) => new() { Role = "system", Content = content };
        public static ChatMessageItem User(string content) => new() { Role = "user", Content = content };
        public static ChatMessageItem Assistant(string content) => new() { Role = "assistant", Content = content };
    }

    /// <summary>
    /// AI 响应
    /// </summary>
    public class AIResponse
    {
        public bool Success { get; set; }
        public string Content { get; set; } = string.Empty;
        public string ErrorMessage { get; set; } = string.Empty;
        public int PromptTokens { get; set; }
        public int CompletionTokens { get; set; }
        public int TotalTokens { get; set; }
    }
}
