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
    /// 群发数据仓储
    /// 持久化存储群发联系人记录
    /// </summary>
    public class BatchSendRepository
    {
        private readonly string _accountId;
        private readonly string _dataDir;
        private readonly string _recordsFile;
        private readonly string _templatesFile;
        private readonly SemaphoreSlim _lock = new(1, 1);

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        public BatchSendRepository(string accountId)
        {
            _accountId = accountId;
            _dataDir = Path.Combine(AppConstants.DataRootPath, "accounts", accountId);
            _recordsFile = Path.Combine(_dataDir, "batch_send_records.json");
            _templatesFile = Path.Combine(_dataDir, "batch_send_templates.json");
            
            EnsureDirectoryExists();
        }

        private void EnsureDirectoryExists()
        {
            if (!Directory.Exists(_dataDir))
                Directory.CreateDirectory(_dataDir);
        }

        #region 联系人记录

        /// <summary>
        /// 获取所有联系人记录
        /// </summary>
        public async Task<List<ContactSendRecord>> GetAllRecordsAsync()
        {
            await _lock.WaitAsync();
            try
            {
                return await LoadRecordsUnsafeAsync();
            }
            finally
            {
                _lock.Release();
            }
        }

        /// <summary>
        /// 获取联系人记录
        /// </summary>
        public async Task<ContactSendRecord?> GetRecordAsync(string contactId)
        {
            var all = await GetAllRecordsAsync();
            return all.FirstOrDefault(r => r.ContactId == contactId);
        }

        /// <summary>
        /// 保存联系人记录
        /// </summary>
        public async Task SaveRecordAsync(ContactSendRecord record)
        {
            await _lock.WaitAsync();
            try
            {
                var all = await LoadRecordsUnsafeAsync();
                var index = all.FindIndex(r => r.ContactId == record.ContactId);
                
                record.UpdatedAt = DateTime.UtcNow;
                
                if (index >= 0)
                {
                    all[index] = record;
                }
                else
                {
                    record.CreatedAt = DateTime.UtcNow;
                    all.Add(record);
                }

                await SaveRecordsUnsafeAsync(all);
            }
            finally
            {
                _lock.Release();
            }
        }

        /// <summary>
        /// 批量保存联系人记录
        /// </summary>
        public async Task SaveRecordsAsync(IEnumerable<ContactSendRecord> records)
        {
            await _lock.WaitAsync();
            try
            {
                var all = await LoadRecordsUnsafeAsync();
                
                foreach (var record in records)
                {
                    record.UpdatedAt = DateTime.UtcNow;
                    var index = all.FindIndex(r => r.ContactId == record.ContactId);
                    
                    if (index >= 0)
                    {
                        all[index] = record;
                    }
                    else
                    {
                        record.CreatedAt = DateTime.UtcNow;
                        all.Add(record);
                    }
                }

                await SaveRecordsUnsafeAsync(all);
            }
            finally
            {
                _lock.Release();
            }
        }

        /// <summary>
        /// 获取可群发的联系人
        /// </summary>
        /// <param name="maxSentCount">最大已发送数量（超过则跳过）</param>
        /// <param name="skipReplied">是否跳过已回复的</param>
        /// <param name="limit">返回数量限制</param>
        public async Task<List<ContactSendRecord>> GetEligibleContactsAsync(
            int maxSentCount = 5, 
            bool skipReplied = true, 
            int limit = 50)
        {
            var all = await GetAllRecordsAsync();

            return all.Where(r =>
            {
                // 跳过已标记跳过的
                if (r.IsSkipped)
                    return false;

                // 跳过发送次数达上限的
                if (r.SentCount >= maxSentCount)
                    return false;

                // 跳过已回复的
                if (skipReplied && r.HasReplied)
                    return false;

                return true;
            })
            .OrderBy(r => r.LastBatchSendTime ?? DateTime.MinValue) // 优先发送最久未发送的
            .ThenByDescending(r => r.CreatedAt)
            .Take(limit)
            .ToList();
        }

        /// <summary>
        /// 更新发送记录
        /// </summary>
        public async Task UpdateSentAsync(string contactId)
        {
            await _lock.WaitAsync();
            try
            {
                var all = await LoadRecordsUnsafeAsync();
                var record = all.FirstOrDefault(r => r.ContactId == contactId);
                
                if (record != null)
                {
                    record.SentCount++;
                    record.LastBatchSendTime = DateTime.UtcNow;
                    record.UpdatedAt = DateTime.UtcNow;
                    await SaveRecordsUnsafeAsync(all);
                }
            }
            finally
            {
                _lock.Release();
            }
        }

        /// <summary>
        /// 标记联系人已回复
        /// </summary>
        public async Task MarkRepliedAsync(string contactId)
        {
            await _lock.WaitAsync();
            try
            {
                var all = await LoadRecordsUnsafeAsync();
                var record = all.FirstOrDefault(r => r.ContactId == contactId);
                
                if (record != null)
                {
                    record.HasReplied = true;
                    record.IsSkipped = true;
                    record.SkipReason = SkipReason.AlreadyReplied;
                    record.LastReplyTime = DateTime.UtcNow;
                    record.UpdatedAt = DateTime.UtcNow;
                    await SaveRecordsUnsafeAsync(all);
                }
            }
            finally
            {
                _lock.Release();
            }
        }

        /// <summary>
        /// 标记联系人跳过
        /// </summary>
        public async Task MarkSkippedAsync(string contactId, SkipReason reason)
        {
            await _lock.WaitAsync();
            try
            {
                var all = await LoadRecordsUnsafeAsync();
                var record = all.FirstOrDefault(r => r.ContactId == contactId);
                
                if (record != null)
                {
                    record.IsSkipped = true;
                    record.SkipReason = reason;
                    record.UpdatedAt = DateTime.UtcNow;
                    await SaveRecordsUnsafeAsync(all);
                }
            }
            finally
            {
                _lock.Release();
            }
        }

        /// <summary>
        /// 获取统计信息
        /// </summary>
        public async Task<BatchSendStats> GetStatsAsync()
        {
            var all = await GetAllRecordsAsync();
            var today = DateTime.UtcNow.Date;

            return new BatchSendStats
            {
                TotalContacts = all.Count,
                TodaySent = all.Count(r => r.LastBatchSendTime?.Date == today),
                SkippedBySentLimit = all.Count(r => r.SkipReason == SkipReason.SentLimitReached),
                SkippedByReplied = all.Count(r => r.SkipReason == SkipReason.AlreadyReplied),
                SkippedBySystem = all.Count(r => r.SkipReason == SkipReason.SystemMarked),
                EligibleCount = all.Count(r => !r.IsSkipped && r.SentCount < 5 && !r.HasReplied)
            };
        }

        #endregion

        #region 消息模板

        /// <summary>
        /// 获取所有模板
        /// </summary>
        public async Task<List<BatchSendTemplate>> GetTemplatesAsync()
        {
            if (!File.Exists(_templatesFile))
                return GetDefaultTemplates();

            try
            {
                var json = await File.ReadAllTextAsync(_templatesFile);
                return JsonSerializer.Deserialize<List<BatchSendTemplate>>(json, JsonOptions) 
                       ?? GetDefaultTemplates();
            }
            catch
            {
                return GetDefaultTemplates();
            }
        }

        /// <summary>
        /// 保存模板
        /// </summary>
        public async Task SaveTemplatesAsync(List<BatchSendTemplate> templates)
        {
            var json = JsonSerializer.Serialize(templates, JsonOptions);
            await File.WriteAllTextAsync(_templatesFile, json);
        }

        /// <summary>
        /// 获取随机模板
        /// </summary>
        public async Task<BatchSendTemplate?> GetRandomTemplateAsync()
        {
            var templates = await GetTemplatesAsync();
            if (templates.Count == 0)
                return null;

            var random = new Random();
            return templates[random.Next(templates.Count)];
        }

        private static List<BatchSendTemplate> GetDefaultTemplates() => new()
        {
            new BatchSendTemplate
            {
                Name = "通用群发",
                Content = "你好 {姓名}，我这边刚看了你的情况，觉得和我们 {岗位名称} 方向挺匹配的，方便简单聊下吗？",
                IsDefault = true
            },
            new BatchSendTemplate
            {
                Name = "技术岗位",
                Content = "Hi {姓名}，看到您的技术背景很不错，我们 {公司名} 正在招聘 {岗位名称}，有兴趣了解一下吗？"
            },
            new BatchSendTemplate
            {
                Name = "跟进消息",
                Content = "{姓名}您好，之前给您发的消息看到了吗？我们这边岗位还在招聘中，期待您的回复~"
            }
        };

        #endregion

        #region 私有方法

        private async Task<List<ContactSendRecord>> LoadRecordsUnsafeAsync()
        {
            if (!File.Exists(_recordsFile))
                return new List<ContactSendRecord>();

            var json = await File.ReadAllTextAsync(_recordsFile);
            return JsonSerializer.Deserialize<List<ContactSendRecord>>(json, JsonOptions) 
                   ?? new List<ContactSendRecord>();
        }

        private async Task SaveRecordsUnsafeAsync(List<ContactSendRecord> records)
        {
            var json = JsonSerializer.Serialize(records, JsonOptions);
            await File.WriteAllTextAsync(_recordsFile, json);
        }

        #endregion
    }

    /// <summary>
    /// 群发统计
    /// </summary>
    public class BatchSendStats
    {
        public int TotalContacts { get; set; }
        public int TodaySent { get; set; }
        public int SkippedBySentLimit { get; set; }
        public int SkippedByReplied { get; set; }
        public int SkippedBySystem { get; set; }
        public int EligibleCount { get; set; }
    }
}
