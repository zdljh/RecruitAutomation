using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using RecruitAutomation.Core.Constants;
using RecruitAutomation.Core.Models;

namespace RecruitAutomation.Core.Services
{
    /// <summary>
    /// 消息数据仓储
    /// 使用 JSON 文件存储，按账号隔离
    /// </summary>
    public class MessageRepository
    {
        private readonly string _accountId;
        private readonly string _dataDir;
        private readonly string _conversationsFile;
        private readonly string _configFile;
        private readonly SemaphoreSlim _lock = new(1, 1);

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        public MessageRepository(string accountId)
        {
            _accountId = accountId;
            _dataDir = Path.Combine(AppConstants.DataRootPath, "accounts", accountId);
            _conversationsFile = Path.Combine(_dataDir, "conversations.json");
            _configFile = Path.Combine(_dataDir, "auto_reply_config.json");
            
            EnsureDirectoryExists();
        }

        private void EnsureDirectoryExists()
        {
            if (!Directory.Exists(_dataDir))
                Directory.CreateDirectory(_dataDir);
        }

        #region 会话管理

        /// <summary>
        /// 获取所有会话
        /// </summary>
        public async Task<List<Conversation>> GetAllConversationsAsync()
        {
            await _lock.WaitAsync();
            try
            {
                return await LoadConversationsUnsafeAsync();
            }
            finally
            {
                _lock.Release();
            }
        }

        /// <summary>
        /// 获取会话
        /// </summary>
        public async Task<Conversation?> GetConversationAsync(string candidateId)
        {
            var all = await GetAllConversationsAsync();
            return all.FirstOrDefault(c => c.CandidateId == candidateId);
        }

        /// <summary>
        /// 保存会话
        /// </summary>
        public async Task SaveConversationAsync(Conversation conversation)
        {
            await _lock.WaitAsync();
            try
            {
                var all = await LoadConversationsUnsafeAsync();
                
                if (string.IsNullOrEmpty(conversation.Id))
                {
                    conversation.Id = $"{conversation.Platform}_{conversation.CandidateId}";
                }

                var index = all.FindIndex(c => c.Id == conversation.Id);
                if (index >= 0)
                {
                    conversation.UpdatedAt = DateTime.UtcNow;
                    all[index] = conversation;
                }
                else
                {
                    conversation.CreatedAt = DateTime.UtcNow;
                    conversation.UpdatedAt = DateTime.UtcNow;
                    all.Add(conversation);
                }

                await SaveConversationsUnsafeAsync(all);
            }
            finally
            {
                _lock.Release();
            }
        }

        /// <summary>
        /// 添加消息到会话
        /// </summary>
        public async Task AddMessageAsync(string candidateId, ChatMessage message)
        {
            await _lock.WaitAsync();
            try
            {
                var all = await LoadConversationsUnsafeAsync();
                var conversation = all.FirstOrDefault(c => c.CandidateId == candidateId);
                
                if (conversation == null)
                    return;

                conversation.Messages.Add(message);
                conversation.LastMessage = message.Content;
                conversation.LastMessageAt = message.Timestamp;
                conversation.UpdatedAt = DateTime.UtcNow;

                if (message.IsSent)
                {
                    conversation.SentCount++;
                    conversation.LastSentAt = message.Timestamp;
                }
                else
                {
                    conversation.ReceivedCount++;
                    conversation.LastReceivedAt = message.Timestamp;
                    conversation.Status = ConversationStatus.Replied;
                }

                await SaveConversationsUnsafeAsync(all);
            }
            finally
            {
                _lock.Release();
            }
        }

        /// <summary>
        /// 查询需要跟进的会话
        /// </summary>
        public async Task<List<Conversation>> GetPendingFollowUpAsync(FollowUpConfig config)
        {
            var all = await GetAllConversationsAsync();
            var now = DateTime.UtcNow;

            return all.Where(c =>
            {
                // 已回复或已结束的不跟进
                if (c.Status == ConversationStatus.Replied ||
                    c.Status == ConversationStatus.InProgress ||
                    c.Status == ConversationStatus.Closed ||
                    c.Status == ConversationStatus.Ignored)
                    return false;

                // 超过最大跟进次数
                if (c.FollowUpCount >= config.MaxFollowUpCount)
                    return false;

                // 仅跟进已读未回
                if (config.OnlyFollowUpRead && !c.IsRead)
                    return false;

                // 检查时间间隔
                var lastContact = c.LastSentAt ?? c.CreatedAt;
                var hours = c.FollowUpCount == 0 
                    ? config.FirstFollowUpHours 
                    : config.SubsequentFollowUpHours;

                return (now - lastContact).TotalHours >= hours;
            })
            .OrderBy(c => c.LastSentAt)
            .ToList();
        }

        /// <summary>
        /// 查询可群发的会话
        /// </summary>
        public async Task<List<Conversation>> GetBatchSendCandidatesAsync(BatchSendConfig config, int limit)
        {
            var all = await GetAllConversationsAsync();

            return all.Where(c =>
            {
                // 跳过已发送超过N条的
                if (c.SentCount >= config.SkipIfSentMoreThan)
                    return false;

                // 跳过已回复的
                if (config.SkipIfReplied && c.ReceivedCount > 0)
                    return false;

                // 跳过已忽略的
                if (config.SkipIfIgnored && c.Status == ConversationStatus.Ignored)
                    return false;

                // 跳过已结束的
                if (c.Status == ConversationStatus.Closed)
                    return false;

                return true;
            })
            .OrderByDescending(c => c.CreatedAt)
            .Take(limit)
            .ToList();
        }

        #endregion

        #region 配置管理

        /// <summary>
        /// 获取自动回复配置
        /// </summary>
        public async Task<AutoReplyConfig> GetAutoReplyConfigAsync()
        {
            if (!File.Exists(_configFile))
                return AutoReplyConfig.Default;

            try
            {
                var json = await File.ReadAllTextAsync(_configFile);
                return JsonSerializer.Deserialize<AutoReplyConfig>(json, JsonOptions) 
                       ?? AutoReplyConfig.Default;
            }
            catch
            {
                return AutoReplyConfig.Default;
            }
        }

        /// <summary>
        /// 保存自动回复配置
        /// </summary>
        public async Task SaveAutoReplyConfigAsync(AutoReplyConfig config)
        {
            var json = JsonSerializer.Serialize(config, JsonOptions);
            await File.WriteAllTextAsync(_configFile, json);
        }

        #endregion

        #region 统计

        /// <summary>
        /// 获取今日发送数量
        /// </summary>
        public async Task<int> GetTodaySentCountAsync()
        {
            var all = await GetAllConversationsAsync();
            var today = DateTime.UtcNow.Date;

            return all.Sum(c => c.Messages.Count(m => 
                m.IsSent && m.Timestamp.Date == today));
        }

        #endregion

        private async Task<List<Conversation>> LoadConversationsUnsafeAsync()
        {
            if (!File.Exists(_conversationsFile))
                return new List<Conversation>();

            var json = await File.ReadAllTextAsync(_conversationsFile);
            return JsonSerializer.Deserialize<List<Conversation>>(json, JsonOptions) 
                   ?? new List<Conversation>();
        }

        private async Task SaveConversationsUnsafeAsync(List<Conversation> conversations)
        {
            var json = JsonSerializer.Serialize(conversations, JsonOptions);
            await File.WriteAllTextAsync(_conversationsFile, json);
        }
    }
}
