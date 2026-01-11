using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using RecruitAutomation.Core.Models;

namespace RecruitAutomation.Core.Services
{
    /// <summary>
    /// 群发服务
    /// 批量发送消息，支持跳过已发送多条/已回复的会话
    /// 包含风控逻辑和人工行为模拟
    /// </summary>
    public class BatchSendService
    {
        private readonly BatchSendRepository _repository;
        private readonly Random _random = new();
        private BatchSendConfig _config;
        private BatchSendTaskStatus _status = new();
        private bool _isPaused;

        /// <summary>
        /// 发送消息委托（由平台模块设置）
        /// </summary>
        public Func<string, string, CancellationToken, Task<bool>>? SendMessageHandler { get; set; }

        /// <summary>
        /// 打开会话委托
        /// </summary>
        public Func<string, CancellationToken, Task<bool>>? OpenConversationHandler { get; set; }

        /// <summary>
        /// 检查是否有新回复委托
        /// </summary>
        public Func<string, Task<bool>>? CheckHasReplyHandler { get; set; }

        /// <summary>
        /// 检查是否有警告委托
        /// </summary>
        public Func<Task<bool>>? CheckHasWarningHandler { get; set; }

        /// <summary>
        /// 单条发送完成事件
        /// </summary>
        public event EventHandler<BatchSendItemResult>? OnItemSent;

        /// <summary>
        /// 状态更新事件
        /// </summary>
        public event EventHandler<BatchSendTaskStatus>? OnStatusChanged;

        /// <summary>
        /// 日志事件
        /// </summary>
        public event EventHandler<string>? OnLog;

        /// <summary>
        /// 批量发送完成事件
        /// </summary>
        public event EventHandler<BatchSendSummary>? OnBatchCompleted;

        /// <summary>
        /// 变量替换器
        /// </summary>
        public Dictionary<string, string> Variables { get; set; } = new();

        public BatchSendService(string accountId)
        {
            _repository = new BatchSendRepository(accountId);
            _config = new BatchSendConfig();
            _status.AccountId = accountId;
        }

        /// <summary>
        /// 加载配置
        /// </summary>
        public void LoadConfig(BatchSendConfig config)
        {
            _config = config;
        }

        /// <summary>
        /// 获取当前状态
        /// </summary>
        public BatchSendTaskStatus GetStatus() => _status;

        /// <summary>
        /// 暂停群发
        /// </summary>
        public void Pause()
        {
            _isPaused = true;
            Log("群发已暂停");
        }

        /// <summary>
        /// 恢复群发
        /// </summary>
        public void Resume()
        {
            _isPaused = false;
            Log("群发已恢复");
        }

        /// <summary>
        /// 同步联系人到本地记录
        /// </summary>
        public async Task SyncContactsAsync(IEnumerable<ContactInfo> contacts)
        {
            var records = new List<ContactSendRecord>();
            
            foreach (var contact in contacts)
            {
                var existing = await _repository.GetRecordAsync(contact.ContactId);
                
                if (existing == null)
                {
                    records.Add(new ContactSendRecord
                    {
                        AccountId = _status.AccountId,
                        ContactId = contact.ContactId,
                        ContactName = contact.Name,
                        Avatar = contact.Avatar
                    });
                }
                else
                {
                    // 更新联系人信息
                    existing.ContactName = contact.Name;
                    existing.Avatar = contact.Avatar;
                    records.Add(existing);
                }
            }

            await _repository.SaveRecordsAsync(records);
            Log($"已同步 {records.Count} 个联系人");
        }

        /// <summary>
        /// 执行群发
        /// </summary>
        public async Task<BatchSendSummary> ExecuteBatchSendAsync(
            int targetCount,
            CancellationToken ct = default)
        {
            var summary = new BatchSendSummary
            {
                StartTime = DateTime.Now
            };

            _status = new BatchSendTaskStatus
            {
                AccountId = _status.AccountId,
                IsRunning = true,
                TargetCount = targetCount,
                StartTime = DateTime.Now
            };
            NotifyStatusChanged();

            try
            {
                Log($"开始群发，目标数量: {targetCount}");

                // 获取可群发的联系人
                var eligibleContacts = await _repository.GetEligibleContactsAsync(
                    _config.SkipIfSentMoreThan,
                    _config.SkipIfReplied,
                    targetCount * 2); // 多取一些以应对跳过

                summary.TotalCount = eligibleContacts.Count;
                Log($"找到 {eligibleContacts.Count} 个可群发联系人");

                // 获取随机消息模板
                var template = await _repository.GetRandomTemplateAsync();
                if (template == null)
                {
                    Log("没有可用的消息模板");
                    return summary;
                }

                var sentCount = 0;
                var index = 0;

                foreach (var contact in eligibleContacts)
                {
                    if (ct.IsCancellationRequested)
                        break;

                    // 检查是否达到目标数量
                    if (sentCount >= targetCount)
                    {
                        Log($"已达到目标数量 {targetCount}，停止群发");
                        break;
                    }

                    // 检查暂停状态
                    while (_isPaused && !ct.IsCancellationRequested)
                    {
                        await Task.Delay(1000, ct);
                    }

                    index++;
                    _status.CurrentContact = contact.ContactName;
                    NotifyStatusChanged();

                    // 检查是否需要跳过
                    var skipResult = await CheckShouldSkipAsync(contact);
                    if (skipResult.ShouldSkip)
                    {
                        _status.SkippedCount++;
                        UpdateSkipStats(skipResult.Reason);
                        
                        summary.SkippedCount++;
                        Log($"跳过 [{contact.ContactName}]: {GetSkipReasonText(skipResult.Reason)}");
                        
                        await _repository.MarkSkippedAsync(contact.ContactId, skipResult.Reason);
                        NotifyStatusChanged();
                        continue;
                    }

                    // 检查页面是否有警告
                    if (CheckHasWarningHandler != null && await CheckHasWarningHandler())
                    {
                        _status.NeedSlowDown = true;
                        _status.DelayStatus = "检测到频繁操作警告，降速中...";
                        NotifyStatusChanged();
                        
                        Log("检测到频繁操作警告，暂停30秒");
                        await Task.Delay(30000, ct);
                        
                        _status.NeedSlowDown = false;
                    }

                    // 执行发送
                    var itemResult = await SendToContactAsync(contact, template.Content, index, eligibleContacts.Count, ct);
                    summary.Results.Add(itemResult);

                    if (itemResult.Success)
                    {
                        sentCount++;
                        _status.SentCount = sentCount;
                        summary.SuccessCount++;
                        
                        await _repository.UpdateSentAsync(contact.ContactId);
                        Log($"[{sentCount}/{targetCount}] 发送给 [{contact.ContactName}] 成功");
                    }
                    else
                    {
                        summary.FailedCount++;
                        Log($"发送给 [{contact.ContactName}] 失败: {itemResult.ErrorMessage}");
                    }

                    OnItemSent?.Invoke(this, itemResult);
                    NotifyStatusChanged();

                    // 随机延迟（模拟人工）
                    if (sentCount < targetCount)
                    {
                        var delay = GenerateRandomDelay();
                        _status.DelayStatus = $"等待 {delay / 1000.0:F1} 秒...";
                        NotifyStatusChanged();
                        
                        await Task.Delay(delay, ct);
                        _status.DelayStatus = "";
                    }
                }
            }
            catch (OperationCanceledException)
            {
                Log("群发已取消");
            }
            catch (Exception ex)
            {
                Log($"群发出错: {ex.Message}");
            }
            finally
            {
                _status.IsRunning = false;
                _status.EndTime = DateTime.Now;
                _status.CurrentContact = "";
                _status.DelayStatus = "";
                NotifyStatusChanged();
            }

            summary.EndTime = DateTime.Now;
            summary.Duration = summary.EndTime - summary.StartTime;

            OnBatchCompleted?.Invoke(this, summary);
            Log($"群发完成: 成功 {summary.SuccessCount}, 失败 {summary.FailedCount}, 跳过 {summary.SkippedCount}");

            return summary;
        }

        /// <summary>
        /// 检查是否应该跳过
        /// </summary>
        private async Task<(bool ShouldSkip, SkipReason Reason)> CheckShouldSkipAsync(ContactSendRecord contact)
        {
            // 规则一：已发送 >= 5 条
            if (contact.SentCount >= _config.SkipIfSentMoreThan)
            {
                return (true, SkipReason.SentLimitReached);
            }

            // 规则二：对方已回复
            if (_config.SkipIfReplied && contact.HasReplied)
            {
                return (true, SkipReason.AlreadyReplied);
            }

            // 检查是否有新回复
            if (CheckHasReplyHandler != null)
            {
                var hasReply = await CheckHasReplyHandler(contact.ContactId);
                if (hasReply)
                {
                    await _repository.MarkRepliedAsync(contact.ContactId);
                    return (true, SkipReason.AlreadyReplied);
                }
            }

            // 规则三：系统标记跳过
            if (contact.IsSkipped && contact.SkipReason == SkipReason.SystemMarked)
            {
                return (true, SkipReason.SystemMarked);
            }

            return (false, SkipReason.None);
        }

        /// <summary>
        /// 发送到单个联系人
        /// </summary>
        private async Task<BatchSendItemResult> SendToContactAsync(
            ContactSendRecord contact,
            string messageTemplate,
            int index,
            int total,
            CancellationToken ct)
        {
            var result = new BatchSendItemResult
            {
                CandidateId = contact.ContactId,
                CandidateName = contact.ContactName,
                Index = index,
                Total = total
            };

            try
            {
                // 打开会话
                if (OpenConversationHandler != null)
                {
                    var opened = await OpenConversationHandler(contact.ContactId, ct);
                    if (!opened)
                    {
                        result.Success = false;
                        result.ErrorMessage = "无法打开会话";
                        return result;
                    }

                    // 等待会话加载
                    await Task.Delay(_random.Next(800, 1500), ct);
                }

                // 替换变量
                var message = ReplaceVariables(messageTemplate, contact.ContactName);
                result.Message = message;

                // 发送消息
                if (SendMessageHandler != null)
                {
                    result.Success = await SendMessageHandler(contact.ContactId, message, ct);
                }

                if (!result.Success)
                {
                    result.ErrorMessage = "发送失败";
                }
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.ErrorMessage = ex.Message;
            }

            return result;
        }

        /// <summary>
        /// 替换模板变量
        /// </summary>
        private string ReplaceVariables(string template, string contactName)
        {
            var result = template;
            
            // 内置变量
            result = result.Replace("{姓名}", contactName);
            result = result.Replace("{name}", contactName);

            // 自定义变量
            foreach (var kv in Variables)
            {
                result = result.Replace($"{{{kv.Key}}}", kv.Value);
            }

            return result;
        }

        /// <summary>
        /// 生成随机延迟（模拟人工）
        /// </summary>
        private int GenerateRandomDelay()
        {
            // 基础延迟 + 随机波动
            var baseDelay = _config.IntervalMs;
            var variation = (int)(baseDelay * 0.5); // 50% 波动
            
            return baseDelay + _random.Next(-variation, variation);
        }

        private void UpdateSkipStats(SkipReason reason)
        {
            switch (reason)
            {
                case SkipReason.SentLimitReached:
                    _status.SkipStats.SentLimitReached++;
                    break;
                case SkipReason.AlreadyReplied:
                    _status.SkipStats.AlreadyReplied++;
                    break;
                case SkipReason.SystemMarked:
                    _status.SkipStats.SystemMarked++;
                    break;
            }
        }

        private static string GetSkipReasonText(SkipReason reason) => reason switch
        {
            SkipReason.SentLimitReached => "发送次数已达上限",
            SkipReason.AlreadyReplied => "对方已回复",
            SkipReason.SystemMarked => "系统标记跳过",
            SkipReason.ManualSkip => "手动跳过",
            _ => "未知原因"
        };

        private void NotifyStatusChanged()
        {
            OnStatusChanged?.Invoke(this, _status);
        }

        private void Log(string message)
        {
            OnLog?.Invoke(this, message);
        }

        /// <summary>
        /// 获取统计信息
        /// </summary>
        public async Task<BatchSendStats> GetStatsAsync()
        {
            return await _repository.GetStatsAsync();
        }

        /// <summary>
        /// 获取消息模板
        /// </summary>
        public async Task<List<BatchSendTemplate>> GetTemplatesAsync()
        {
            return await _repository.GetTemplatesAsync();
        }

        /// <summary>
        /// 保存消息模板
        /// </summary>
        public async Task SaveTemplatesAsync(List<BatchSendTemplate> templates)
        {
            await _repository.SaveTemplatesAsync(templates);
        }

        /// <summary>
        /// 单条发送结果
        /// </summary>
        public class BatchSendItemResult
        {
            public string CandidateId { get; set; } = string.Empty;
            public string CandidateName { get; set; } = string.Empty;
            public string Message { get; set; } = string.Empty;
            public bool Success { get; set; }
            public string ErrorMessage { get; set; } = string.Empty;
            public int Index { get; set; }
            public int Total { get; set; }
        }

        /// <summary>
        /// 批量发送汇总
        /// </summary>
        public class BatchSendSummary
        {
            public int TotalCount { get; set; }
            public int SuccessCount { get; set; }
            public int FailedCount { get; set; }
            public int SkippedCount { get; set; }
            public TimeSpan Duration { get; set; }
            public DateTime StartTime { get; set; }
            public DateTime EndTime { get; set; }
            public List<BatchSendItemResult> Results { get; set; } = new();
        }
    }
}
