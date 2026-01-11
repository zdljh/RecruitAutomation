using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using RecruitAutomation.Core.Models;

namespace RecruitAutomation.Core.Services
{
    /// <summary>
    /// 跟进服务
    /// 对已读未回的候选人进行多次跟进
    /// </summary>
    public class FollowUpService
    {
        private readonly MessageRepository _messageRepo;
        private FollowUpConfig _config;

        /// <summary>
        /// 发送消息事件（由平台模块订阅实现）
        /// </summary>
        public event Func<string, string, CancellationToken, Task<bool>>? OnSendMessage;

        /// <summary>
        /// 跟进完成事件
        /// </summary>
        public event EventHandler<FollowUpResult>? OnFollowUpCompleted;

        /// <summary>
        /// 变量替换器
        /// </summary>
        public Dictionary<string, string> Variables { get; set; } = new();

        public FollowUpService(MessageRepository messageRepo)
        {
            _messageRepo = messageRepo;
            _config = new FollowUpConfig();
        }

        /// <summary>
        /// 加载配置
        /// </summary>
        public async Task LoadConfigAsync()
        {
            var config = await _messageRepo.GetAutoReplyConfigAsync();
            _config = config.FollowUpConfig;
        }

        /// <summary>
        /// 跟进结果
        /// </summary>
        public class FollowUpResult
        {
            public string CandidateId { get; set; } = string.Empty;
            public string CandidateName { get; set; } = string.Empty;
            public int FollowUpCount { get; set; }
            public string Message { get; set; } = string.Empty;
            public bool Success { get; set; }
            public string ErrorMessage { get; set; } = string.Empty;
            public DateTime Timestamp { get; set; } = DateTime.Now;
        }

        /// <summary>
        /// 执行单次跟进检查和发送
        /// </summary>
        public async Task<List<FollowUpResult>> ExecuteFollowUpAsync(
            int maxCount = 50,
            int delayBetweenMs = 3000,
            CancellationToken ct = default)
        {
            var results = new List<FollowUpResult>();

            if (!_config.Enabled)
                return results;

            // 获取需要跟进的会话
            var pendingConversations = await _messageRepo.GetPendingFollowUpAsync(_config);

            var count = 0;
            foreach (var conversation in pendingConversations)
            {
                if (ct.IsCancellationRequested || count >= maxCount)
                    break;

                var result = await FollowUpConversationAsync(conversation, ct);
                results.Add(result);
                count++;

                // 延迟
                if (delayBetweenMs > 0 && count < pendingConversations.Count)
                {
                    await Task.Delay(delayBetweenMs, ct);
                }
            }

            return results;
        }

        /// <summary>
        /// 对单个会话进行跟进
        /// </summary>
        public async Task<FollowUpResult> FollowUpConversationAsync(
            Conversation conversation,
            CancellationToken ct = default)
        {
            var result = new FollowUpResult
            {
                CandidateId = conversation.CandidateId,
                CandidateName = conversation.CandidateName,
                FollowUpCount = conversation.FollowUpCount + 1
            };

            try
            {
                // 检查是否超过最大跟进次数
                if (conversation.FollowUpCount >= _config.MaxFollowUpCount)
                {
                    result.Success = false;
                    result.ErrorMessage = "已达最大跟进次数";
                    return result;
                }

                // 获取跟进消息模板
                var templateIndex = Math.Min(
                    conversation.FollowUpCount, 
                    _config.FollowUpMessages.Count - 1);
                
                var template = _config.FollowUpMessages.Count > 0
                    ? _config.FollowUpMessages[templateIndex]
                    : "{name}您好，之前的消息看到了吗？期待您的回复~";

                // 替换变量
                var message = ReplaceVariables(template, conversation.CandidateName);
                result.Message = message;

                // 发送消息
                if (OnSendMessage != null)
                {
                    result.Success = await OnSendMessage(conversation.CandidateId, message, ct);
                }

                if (result.Success)
                {
                    // 记录消息
                    var chatMsg = new ChatMessage
                    {
                        IsSent = true,
                        Content = message,
                        Type = MessageType.Text,
                        Timestamp = DateTime.UtcNow,
                        IsAuto = true,
                        TriggerRule = $"跟进第{result.FollowUpCount}次"
                    };
                    await _messageRepo.AddMessageAsync(conversation.CandidateId, chatMsg);

                    // 更新跟进状态
                    conversation.FollowUpCount++;
                    conversation.LastFollowUpAt = DateTime.UtcNow;
                    conversation.Status = ConversationStatus.WaitingReply;
                    await _messageRepo.SaveConversationAsync(conversation);
                }
                else
                {
                    result.ErrorMessage = "发送失败";
                }

                OnFollowUpCompleted?.Invoke(this, result);
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.ErrorMessage = $"跟进异常: {ex.Message}";
            }

            return result;
        }

        /// <summary>
        /// 替换模板变量
        /// </summary>
        private string ReplaceVariables(string template, string candidateName)
        {
            var result = template;
            result = result.Replace("{name}", candidateName);

            foreach (var kv in Variables)
            {
                result = result.Replace($"{{{kv.Key}}}", kv.Value);
            }

            return result;
        }

        /// <summary>
        /// 获取待跟进数量
        /// </summary>
        public async Task<int> GetPendingCountAsync()
        {
            var pending = await _messageRepo.GetPendingFollowUpAsync(_config);
            return pending.Count;
        }

        /// <summary>
        /// 更新配置
        /// </summary>
        public async Task UpdateConfigAsync(FollowUpConfig config)
        {
            _config = config;
            var fullConfig = await _messageRepo.GetAutoReplyConfigAsync();
            fullConfig.FollowUpConfig = config;
            await _messageRepo.SaveAutoReplyConfigAsync(fullConfig);
        }

        /// <summary>
        /// 设置启用状态
        /// </summary>
        public async Task SetEnabledAsync(bool enabled)
        {
            _config.Enabled = enabled;
            var fullConfig = await _messageRepo.GetAutoReplyConfigAsync();
            fullConfig.FollowUpConfig.Enabled = enabled;
            await _messageRepo.SaveAutoReplyConfigAsync(fullConfig);
        }

        /// <summary>
        /// 手动标记会话为已回复（停止跟进）
        /// </summary>
        public async Task MarkAsRepliedAsync(string candidateId)
        {
            var conversation = await _messageRepo.GetConversationAsync(candidateId);
            if (conversation != null)
            {
                conversation.Status = ConversationStatus.Replied;
                await _messageRepo.SaveConversationAsync(conversation);
            }
        }

        /// <summary>
        /// 手动标记会话为忽略（停止跟进）
        /// </summary>
        public async Task MarkAsIgnoredAsync(string candidateId)
        {
            var conversation = await _messageRepo.GetConversationAsync(candidateId);
            if (conversation != null)
            {
                conversation.Status = ConversationStatus.Ignored;
                await _messageRepo.SaveConversationAsync(conversation);
            }
        }
    }
}
