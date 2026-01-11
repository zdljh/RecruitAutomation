using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace RecruitAutomation.Core.AI.Providers
{
    /// <summary>
    /// OpenAI 兼容接口基类
    /// 通义千问、Kimi、智谱等都兼容 OpenAI 接口格式
    /// </summary>
    public abstract class BaseOpenAICompatibleProvider : IAIProvider
    {
        protected HttpClient _httpClient = null!;
        protected ProviderSettings _settings = null!;

        public abstract AIProviderType ProviderType { get; }
        public abstract string Name { get; }

        public virtual void Initialize(ProviderSettings settings)
        {
            _settings = settings;
            _httpClient = new HttpClient
            {
                BaseAddress = new Uri(settings.BaseUrl.TrimEnd('/')),
                Timeout = TimeSpan.FromSeconds(60)
            };
            _httpClient.DefaultRequestHeaders.Authorization = 
                new AuthenticationHeaderValue("Bearer", settings.ApiKey);
            _httpClient.DefaultRequestHeaders.Accept.Add(
                new MediaTypeWithQualityHeaderValue("application/json"));
        }

        public virtual async Task<AIResponse> ChatAsync(
            List<ChatMessageItem> messages,
            float temperature = 0.7f,
            int maxTokens = 500,
            CancellationToken ct = default)
        {
            var response = new AIResponse();

            try
            {
                var request = new OpenAIChatRequest
                {
                    Model = _settings.Model,
                    Messages = messages.ConvertAll(m => new OpenAIMessage
                    {
                        Role = m.Role,
                        Content = m.Content
                    }),
                    Temperature = temperature,
                    MaxTokens = maxTokens
                };

                var json = JsonSerializer.Serialize(request, JsonOptions);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var httpResponse = await _httpClient.PostAsync("/chat/completions", content, ct);
                var responseJson = await httpResponse.Content.ReadAsStringAsync(ct);

                if (!httpResponse.IsSuccessStatusCode)
                {
                    response.Success = false;
                    response.ErrorMessage = $"HTTP {(int)httpResponse.StatusCode}: {responseJson}";
                    return response;
                }

                var result = JsonSerializer.Deserialize<OpenAIChatResponse>(responseJson, JsonOptions);
                if (result?.Choices?.Count > 0)
                {
                    response.Success = true;
                    response.Content = result.Choices[0].Message?.Content ?? string.Empty;
                    response.PromptTokens = result.Usage?.PromptTokens ?? 0;
                    response.CompletionTokens = result.Usage?.CompletionTokens ?? 0;
                    response.TotalTokens = result.Usage?.TotalTokens ?? 0;
                }
                else
                {
                    response.Success = false;
                    response.ErrorMessage = "无有效响应";
                }
            }
            catch (Exception ex)
            {
                response.Success = false;
                response.ErrorMessage = ex.Message;
            }

            return response;
        }

        public virtual async Task<bool> TestConnectionAsync(CancellationToken ct = default)
        {
            try
            {
                var messages = new List<ChatMessageItem>
                {
                    ChatMessageItem.User("你好")
                };
                var response = await ChatAsync(messages, 0.1f, 10, ct);
                return response.Success;
            }
            catch
            {
                return false;
            }
        }

        protected static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };
    }


    #region OpenAI 兼容数据结构

    public class OpenAIChatRequest
    {
        [JsonPropertyName("model")]
        public string Model { get; set; } = string.Empty;

        [JsonPropertyName("messages")]
        public List<OpenAIMessage> Messages { get; set; } = new();

        [JsonPropertyName("temperature")]
        public float Temperature { get; set; } = 0.7f;

        [JsonPropertyName("max_tokens")]
        public int MaxTokens { get; set; } = 500;

        [JsonPropertyName("stream")]
        public bool Stream { get; set; } = false;
    }

    public class OpenAIMessage
    {
        [JsonPropertyName("role")]
        public string Role { get; set; } = string.Empty;

        [JsonPropertyName("content")]
        public string Content { get; set; } = string.Empty;
    }

    public class OpenAIChatResponse
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("choices")]
        public List<OpenAIChoice>? Choices { get; set; }

        [JsonPropertyName("usage")]
        public OpenAIUsage? Usage { get; set; }
    }

    public class OpenAIChoice
    {
        [JsonPropertyName("index")]
        public int Index { get; set; }

        [JsonPropertyName("message")]
        public OpenAIMessage? Message { get; set; }

        [JsonPropertyName("finish_reason")]
        public string? FinishReason { get; set; }
    }

    public class OpenAIUsage
    {
        [JsonPropertyName("prompt_tokens")]
        public int PromptTokens { get; set; }

        [JsonPropertyName("completion_tokens")]
        public int CompletionTokens { get; set; }

        [JsonPropertyName("total_tokens")]
        public int TotalTokens { get; set; }
    }

    #endregion
}
