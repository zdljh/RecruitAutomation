using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using RecruitAutomation.Core.Constants;
using RecruitAutomation.Core.Models;

namespace RecruitAutomation.Core.AI
{
    /// <summary>
    /// AI 智能回复服务
    /// </summary>
    public class AIReplyService
    {
        private readonly string _configFile;
        private AIProviderConfig _config;
        private IAIProvider? _currentProvider;

        /// <summary>
        /// 发送消息事件（由平台模块订阅实现）
        /// </summary>
        public event Func<string, string, CancellationToken, Task<bool>>? OnSendMessage;

        /// <summary>
        /// AI 回复完成事件
        /// </summary>
        public event EventHandler<AIReplyResult>? OnAIReplyCompleted;

        /// <summary>
        /// 当前岗位信息（用于构建上下文）
        /// </summary>
        public JobInfo? CurrentJob { get; set; }

        public AIReplyService(string accountId)
        {
            var dataDir = Path.Combine(AppConstants.DataRootPath, "accounts", accountId);
            _configFile = Path.Combine(dataDir, "ai_config.json");
            _config = new AIProviderConfig();

            if (!Directory.Exists(dataDir))
                Directory.CreateDirectory(dataDir);
        }

        /// <summary>
        /// AI 回复结果
        /// </summary>
        public class AIReplyResult
        {
            public string CandidateId { get; set; } = string.Empty;
            public string ReceivedMessage { get; set; } = string.Empty;
            public string? AIReply { get; set; }
            public bool ShouldReply { get; set; }
            public bool Success { get; set; }
            public string ErrorMessage { get; set; } = string.Empty;
            public int TokensUsed { get; set; }
            public DateTime Timestamp { get; set; } = DateTime.Now;
        }

        /// <summary>
        /// 加载配置并初始化提供商
        /// </summary>
        public async Task LoadConfigAsync()
        {
            if (File.Exists(_configFile))
            {
                try
                {
                    var json = await File.ReadAllTextAsync(_configFile);
                    _config = JsonSerializer.Deserialize<AIProviderConfig>(json, JsonOptions) 
                              ?? new AIProviderConfig();
                }
                catch
                {
                    _config = new AIProviderConfig();
                }
            }

            InitializeProvider();
        }

        /// <summary>
        /// 保存配置
        /// </summary>
        public async Task SaveConfigAsync()
        {
            var json = JsonSerializer.Serialize(_config, JsonOptions);
            await File.WriteAllTextAsync(_configFile, json);
        }

        /// <summary>
        /// 初始化当前提供商
        /// </summary>
        private void InitializeProvider()
        {
            if (!_config.Enabled)
            {
                _currentProvider = null;
                return;
            }

            var settings = _config.GetCurrentProvider();
            if (string.IsNullOrEmpty(settings.ApiKey))
            {
                _currentProvider = null;
                return;
            }

            try
            {
                _currentProvider = AIProviderFactory.Create(_config.CurrentProvider, settings);
            }
            catch
            {
                _currentProvider = null;
            }
        }

        /// <summary>
        /// 处理收到的消息，判断是否需要 AI 回复
        /// </summary>
        public async Task<AIReplyResult> ProcessMessageAsync(
            CandidateInfo candidate,
            List<ChatMessage> historyMessages,
            string currentMessage,
            CancellationToken ct = default)
        {
            var result = new AIReplyResult
            {
                CandidateId = candidate.Id,
                ReceivedMessage = currentMessage
            };

            try
            {
                // 检查是否启用
                if (!_config.Enabled || _currentProvider == null)
                {
                    result.ErrorMessage = "AI 回复未启用";
                    return result;
                }

                // 1. 先判断是否应该回复
                result.ShouldReply = await ShouldReplyAsync(currentMessage, ct);
                if (!result.ShouldReply)
                {
                    result.ErrorMessage = "消息不需要自动回复";
                    return result;
                }

                // 2. 构建上下文
                var context = new AIReplyContext
                {
                    Job = CurrentJob,
                    Candidate = candidate,
                    HistoryMessages = historyMessages,
                    CurrentMessage = currentMessage
                };

                // 3. 生成回复
                var reply = await GenerateReplyAsync(context, ct);
                if (!reply.Success)
                {
                    result.ErrorMessage = reply.ErrorMessage;
                    return result;
                }

                result.AIReply = reply.Content;
                result.TokensUsed = reply.TotalTokens;
                result.Success = true;

                OnAIReplyCompleted?.Invoke(this, result);
            }
            catch (Exception ex)
            {
                result.ErrorMessage = $"AI 回复异常: {ex.Message}";
            }

            return result;
        }

        /// <summary>
        /// 判断是否应该回复
        /// </summary>
        public async Task<bool> ShouldReplyAsync(string message, CancellationToken ct = default)
        {
            if (_currentProvider == null)
                return false;

            // 空消息或太短的消息不回复
            if (string.IsNullOrWhiteSpace(message) || message.Length < 2)
                return false;

            // 纯表情不回复
            if (IsOnlyEmoji(message))
                return false;

            // 使用 AI 判断
            var prompt = _config.ShouldReplyPrompt.Replace("{message}", message);
            var messages = new List<ChatMessageItem>
            {
                ChatMessageItem.User(prompt)
            };

            var response = await _currentProvider.ChatAsync(messages, 0.1f, 10, ct);
            if (!response.Success)
                return true; // 判断失败时默认回复

            var answer = response.Content.Trim().ToUpper();
            return answer.Contains("YES");
        }

        /// <summary>
        /// 生成 AI 回复
        /// </summary>
        public async Task<AIResponse> GenerateReplyAsync(
            AIReplyContext context,
            CancellationToken ct = default)
        {
            if (_currentProvider == null)
            {
                return new AIResponse
                {
                    Success = false,
                    ErrorMessage = "AI 提供商未初始化"
                };
            }

            // 构建消息列表
            var messages = new List<ChatMessageItem>();

            // 系统提示词
            var systemPrompt = context.BuildSystemPrompt(_config.SystemPrompt);
            messages.Add(ChatMessageItem.System(systemPrompt));

            // 历史消息
            var history = context.BuildHistoryMessageItems(_config.MaxHistoryMessages);
            messages.AddRange(history);

            // 当前消息
            messages.Add(ChatMessageItem.User(context.CurrentMessage));

            // 调用 AI
            return await _currentProvider.ChatAsync(
                messages,
                _config.Temperature,
                _config.MaxTokens,
                ct);
        }

        /// <summary>
        /// 判断是否只有表情
        /// </summary>
        private static bool IsOnlyEmoji(string text)
        {
            // 简单判断：去掉空格后长度很短，且包含常见表情字符
            var trimmed = text.Replace(" ", "");
            if (trimmed.Length > 10)
                return false;

            // 检查是否全是表情或特殊字符
            foreach (var c in trimmed)
            {
                if (char.IsLetterOrDigit(c) && c < 128)
                    return false;
            }
            return true;
        }

        /// <summary>
        /// 切换 AI 提供商
        /// </summary>
        public async Task SwitchProviderAsync(AIProviderType type)
        {
            _config.CurrentProvider = type;
            InitializeProvider();
            await SaveConfigAsync();
        }

        /// <summary>
        /// 设置 API Key
        /// </summary>
        public async Task SetApiKeyAsync(AIProviderType type, string apiKey)
        {
            if (_config.Providers.TryGetValue(type, out var settings))
            {
                settings.ApiKey = apiKey;
            }
            else
            {
                _config.Providers[type] = new ProviderSettings { ApiKey = apiKey };
            }

            if (type == _config.CurrentProvider)
            {
                InitializeProvider();
            }

            await SaveConfigAsync();
        }

        /// <summary>
        /// 设置启用状态
        /// </summary>
        public async Task SetEnabledAsync(bool enabled)
        {
            _config.Enabled = enabled;
            if (enabled)
            {
                InitializeProvider();
            }
            else
            {
                _currentProvider = null;
            }
            await SaveConfigAsync();
        }

        /// <summary>
        /// 测试当前提供商连接
        /// </summary>
        public async Task<bool> TestConnectionAsync(CancellationToken ct = default)
        {
            if (_currentProvider == null)
                return false;

            return await _currentProvider.TestConnectionAsync(ct);
        }

        /// <summary>
        /// 获取配置
        /// </summary>
        public AIProviderConfig GetConfig() => _config;

        /// <summary>
        /// 更新系统提示词
        /// </summary>
        public async Task UpdateSystemPromptAsync(string prompt)
        {
            _config.SystemPrompt = prompt;
            await SaveConfigAsync();
        }

        /// <summary>
        /// 更新判断提示词
        /// </summary>
        public async Task UpdateShouldReplyPromptAsync(string prompt)
        {
            _config.ShouldReplyPrompt = prompt;
            await SaveConfigAsync();
        }

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
    }
}
