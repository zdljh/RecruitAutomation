using System;
using System.Text.Json.Serialization;

namespace RecruitAutomation.Core.Models
{
    /// <summary>
    /// 群发联系人记录
    /// </summary>
    public class ContactSendRecord
    {
        /// <summary>
        /// 记录ID
        /// </summary>
        [JsonPropertyName("id")]
        public string Id { get; set; } = Guid.NewGuid().ToString("N");

        /// <summary>
        /// 账号ID
        /// </summary>
        [JsonPropertyName("accountId")]
        public string AccountId { get; set; } = string.Empty;

        /// <summary>
        /// 联系人ID（平台唯一标识）
        /// </summary>
        [JsonPropertyName("contactId")]
        public string ContactId { get; set; } = string.Empty;

        /// <summary>
        /// 联系人姓名
        /// </summary>
        [JsonPropertyName("contactName")]
        public string ContactName { get; set; } = string.Empty;

        /// <summary>
        /// 联系人头像
        /// </summary>
        [JsonPropertyName("avatar")]
        public string Avatar { get; set; } = string.Empty;

        /// <summary>
        /// 已发送消息数
        /// </summary>
        [JsonPropertyName("sentCount")]
        public int SentCount { get; set; }

        /// <summary>
        /// 是否已回复
        /// </summary>
        [JsonPropertyName("hasReplied")]
        public bool HasReplied { get; set; }

        /// <summary>
        /// 是否被跳过
        /// </summary>
        [JsonPropertyName("isSkipped")]
        public bool IsSkipped { get; set; }

        /// <summary>
        /// 跳过原因
        /// </summary>
        [JsonPropertyName("skipReason")]
        public SkipReason SkipReason { get; set; } = SkipReason.None;

        /// <summary>
        /// 最近一次群发时间
        /// </summary>
        [JsonPropertyName("lastBatchSendTime")]
        public DateTime? LastBatchSendTime { get; set; }

        /// <summary>
        /// 最近一次回复时间
        /// </summary>
        [JsonPropertyName("lastReplyTime")]
        public DateTime? LastReplyTime { get; set; }

        /// <summary>
        /// 创建时间
        /// </summary>
        [JsonPropertyName("createdAt")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// 更新时间
        /// </summary>
        [JsonPropertyName("updatedAt")]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// 跳过原因
    /// </summary>
    public enum SkipReason
    {
        /// <summary>无</summary>
        None = 0,
        /// <summary>发送次数已达上限</summary>
        SentLimitReached = 1,
        /// <summary>对方已回复</summary>
        AlreadyReplied = 2,
        /// <summary>系统标记跳过</summary>
        SystemMarked = 3,
        /// <summary>用户手动跳过</summary>
        ManualSkip = 4
    }

    /// <summary>
    /// 群发任务状态
    /// </summary>
    public class BatchSendTaskStatus
    {
        /// <summary>
        /// 账号ID
        /// </summary>
        public string AccountId { get; set; } = string.Empty;

        /// <summary>
        /// 是否正在运行
        /// </summary>
        public bool IsRunning { get; set; }

        /// <summary>
        /// 目标数量
        /// </summary>
        public int TargetCount { get; set; }

        /// <summary>
        /// 已发送数量
        /// </summary>
        public int SentCount { get; set; }

        /// <summary>
        /// 已跳过数量
        /// </summary>
        public int SkippedCount { get; set; }

        /// <summary>
        /// 跳过原因统计
        /// </summary>
        public SkipReasonStats SkipStats { get; set; } = new();

        /// <summary>
        /// 当前正在处理的联系人
        /// </summary>
        public string CurrentContact { get; set; } = string.Empty;

        /// <summary>
        /// 当前延迟状态
        /// </summary>
        public string DelayStatus { get; set; } = string.Empty;

        /// <summary>
        /// 是否需要降速
        /// </summary>
        public bool NeedSlowDown { get; set; }

        /// <summary>
        /// 开始时间
        /// </summary>
        public DateTime? StartTime { get; set; }

        /// <summary>
        /// 结束时间
        /// </summary>
        public DateTime? EndTime { get; set; }
    }

    /// <summary>
    /// 跳过原因统计
    /// </summary>
    public class SkipReasonStats
    {
        /// <summary>
        /// 发送次数达上限
        /// </summary>
        public int SentLimitReached { get; set; }

        /// <summary>
        /// 已回复
        /// </summary>
        public int AlreadyReplied { get; set; }

        /// <summary>
        /// 系统标记
        /// </summary>
        public int SystemMarked { get; set; }
    }

    /// <summary>
    /// 联系人信息（从平台读取）
    /// </summary>
    public class ContactInfo
    {
        /// <summary>
        /// 联系人ID
        /// </summary>
        public string ContactId { get; set; } = string.Empty;

        /// <summary>
        /// 联系人姓名
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// 头像URL
        /// </summary>
        public string Avatar { get; set; } = string.Empty;

        /// <summary>
        /// 最后消息内容
        /// </summary>
        public string LastMessage { get; set; } = string.Empty;

        /// <summary>
        /// 最后消息时间
        /// </summary>
        public DateTime? LastMessageTime { get; set; }

        /// <summary>
        /// 是否有未读消息
        /// </summary>
        public bool HasUnread { get; set; }

        /// <summary>
        /// 岗位名称
        /// </summary>
        public string JobTitle { get; set; } = string.Empty;

        /// <summary>
        /// 公司名称
        /// </summary>
        public string Company { get; set; } = string.Empty;
    }

    /// <summary>
    /// 群发消息模板
    /// </summary>
    public class BatchSendTemplate
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = Guid.NewGuid().ToString("N");

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("content")]
        public string Content { get; set; } = string.Empty;

        [JsonPropertyName("isDefault")]
        public bool IsDefault { get; set; }
    }
}
