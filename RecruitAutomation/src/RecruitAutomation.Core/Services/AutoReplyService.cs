using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using RecruitAutomation.Core.Models;

namespace RecruitAutomation.Core.Services
{
    /// <summary>
    /// 自动回复服务
    /// 根据关键词规则自动回复消息
    /// </summary>
    public class AutoReplyService
    {
        private readonly MessageRepository _messageRepo;
        private AutoReplyConfig _config;

        /// <summary>
        /// 发送消息事件（由平台模块订阅实现）
        /// </summary>
        public event Func<string, string, CancellationToken, Task<bool>>? OnSendMessage;

        /// <summary>
        /// 自动回复完成事件
        /// </summary>
        public event EventHandler<AutoReplyResult>? OnAutoReplyCompleted;

        /// <summary>
        /// 变量替换器（用于替换模板中的变量）
        /// </summary>
        public Dictionary<string, string> Variables { get; set; } = new();

        public AutoReplyService(MessageRepository messageRepo)
        {
            _messageRepo = messageRepo;
            _config = AutoReplyConfig.Default;
        }

        /// <summary>
        /// 加载配置
        /// </summary>
        public async Task LoadConfigAsync()
        {
            _config = await _messageRepo.GetAutoReplyConfigAsync();
        }

        /// <summary>
        /// 自动回复结果
        /// </summary>
        public class AutoReplyResult
        {
            public string CandidateId { get; set; } = string.Empty;
            public string ReceivedMessage { get; set; } = string.Empty;
            public string? MatchedRule { get; set; }
            public string? ReplyMessage { get; set; }
            public bool Success { get; set; }
            public string Message { get; set; } = string.Empty;
            public DateTime Timestamp { get; set; } = DateTime.Now;
        }

        /// <summary>
        /// 处理收到的消息，判断是否需要自动回复
        /// </summary>
        public async Task<AutoReplyResult> ProcessIncomingMessageAsync(
            string candidateId,
            string candidateName,
            string message,
            CancellationToken ct = default)
        {
            var result = new AutoReplyResult
            {
                CandidateId = candidateId,
                ReceivedMessage = message
            };

            try
            {
                // 检查是否启用
                if (!_config.Enabled)
                {
                    result.Message = "自动回复未启用";
                    return result;
                }

                // 获取或创建会话
                var conversation = await _messageRepo.GetConversationAsync(candidateId);
                if (conversation == null)
                {
                    result.Message = "会话不存在";
                    return result;
                }

                // 记录收到的消息
                var incomingMsg = new ChatMessage
                {
                    IsSent = false,
                    Content = message,
                    Type = MessageType.Text,
                    Timestamp = DateTime.UtcNow
                };
                await _messageRepo.AddMessageAsync(candidateId, incomingMsg);

                // 匹配关键词规则
                var matchedRule = MatchKeywordRule(message);
                if (matchedRule == null)
                {
                    result.Message = "无匹配规则";
                    return result;
                }

                result.MatchedRule = matchedRule.Name;

                // 生成回复内容
                var replyContent = ReplaceVariables(matchedRule.Reply, candidateName);
                result.ReplyMessage = replyContent;

                // 延迟发送（模拟人工）
                if (matchedRule.DelaySeconds > 0)
                {
                    await Task.Delay(matchedRule.DelaySeconds * 1000, ct);
                }

                // 发送回复
                if (OnSendMessage != null)
                {
                    result.Success = await OnSendMessage(candidateId, replyContent, ct);
                }

                if (result.Success)
                {
                    // 记录发送的消息
                    var outgoingMsg = new ChatMessage
                    {
                        IsSent = true,
                        Content = replyContent,
                        Type = MessageType.Text,
                        Timestamp = DateTime.UtcNow,
                        IsAuto = true,
                        TriggerRule = matchedRule.Name
                    };
                    await _messageRepo.AddMessageAsync(candidateId, outgoingMsg);

                    // 如果规则标记结束会话
                    if (matchedRule.EndConversation)
                    {
                        conversation.Status = ConversationStatus.Closed;
                        await _messageRepo.SaveConversationAsync(conversation);
                    }

                    result.Message = "自动回复成功";
                }
                else
                {
                    result.Message = "发送失败";
                }

                OnAutoReplyCompleted?.Invoke(this, result);
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Message = $"自动回复异常: {ex.Message}";
            }

            return result;
        }

        /// <summary>
        /// 匹配关键词规则
        /// </summary>
        private KeywordReplyRule? MatchKeywordRule(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
                return null;

            var normalizedMsg = message.ToLower().Trim();

            // 按优先级排序
            var rules = _config.KeywordRules
                .Where(r => r.Enabled)
                .OrderByDescending(r => r.Priority)
                .ToList();

            foreach (var rule in rules)
            {
                // 检查排除关键词
                if (rule.ExcludeKeywords.Any(k => 
                    normalizedMsg.Contains(k.ToLower())))
                    continue;

                // 检查触发关键词
                if (rule.Keywords.Any(k => 
                    normalizedMsg.Contains(k.ToLower())))
                {
                    return rule;
                }
            }

            return null;
        }

        /// <summary>
        /// 替换模板变量
        /// </summary>
        private string ReplaceVariables(string template, string candidateName)
        {
            var result = template;

            // 替换候选人姓名
            result = result.Replace("{name}", candidateName);

            // 替换自定义变量
            foreach (var kv in Variables)
            {
                result = result.Replace($"{{{kv.Key}}}", kv.Value);
            }

            return result;
        }

        /// <summary>
        /// 添加关键词规则
        /// </summary>
        public async Task AddKeywordRuleAsync(KeywordReplyRule rule)
        {
            _config.KeywordRules.Add(rule);
            await _messageRepo.SaveAutoReplyConfigAsync(_config);
        }

        /// <summary>
        /// 移除关键词规则
        /// </summary>
        public async Task RemoveKeywordRuleAsync(string ruleName)
        {
            _config.KeywordRules.RemoveAll(r => r.Name == ruleName);
            await _messageRepo.SaveAutoReplyConfigAsync(_config);
        }

        /// <summary>
        /// 更新关键词规则
        /// </summary>
        public async Task UpdateKeywordRuleAsync(KeywordReplyRule rule)
        {
            var index = _config.KeywordRules.FindIndex(r => r.Name == rule.Name);
            if (index >= 0)
            {
                _config.KeywordRules[index] = rule;
                await _messageRepo.SaveAutoReplyConfigAsync(_config);
            }
        }

        /// <summary>
        /// 获取所有规则
        /// </summary>
        public List<KeywordReplyRule> GetAllRules() => _config.KeywordRules;

        /// <summary>
        /// 设置启用状态
        /// </summary>
        public async Task SetEnabledAsync(bool enabled)
        {
            _config.Enabled = enabled;
            await _messageRepo.SaveAutoReplyConfigAsync(_config);
        }
    }
}
