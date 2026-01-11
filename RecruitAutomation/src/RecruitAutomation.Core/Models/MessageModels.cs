using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace RecruitAutomation.Core.Models
{
    /// <summary>
    /// 会话信息
    /// </summary>
    public class Conversation
    {
        /// <summary>
        /// 会话ID（平台+候选人ID）
        /// </summary>
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// 平台
        /// </summary>
        [JsonPropertyName("platform")]
        public string Platform { get; set; } = string.Empty;

        /// <summary>
        /// 候选人ID
        /// </summary>
        [JsonPropertyName("candidateId")]
        public string CandidateId { get; set; } = string.Empty;

        /// <summary>
        /// 候选人姓名
        /// </summary>
        [JsonPropertyName("candidateName")]
        public string CandidateName { get; set; } = string.Empty;

        /// <summary>
        /// 候选人头像
        /// </summary>
        [JsonPropertyName("candidateAvatar")]
        public string CandidateAvatar { get; set; } = string.Empty;

        /// <summary>
        /// 会话状态
        /// </summary>
        [JsonPropertyName("status")]
        public ConversationStatus Status { get; set; } = ConversationStatus.New;

        /// <summary>
        /// 我方发送消息数
        /// </summary>
        [JsonPropertyName("sentCount")]
        public int SentCount { get; set; }

        /// <summary>
        /// 对方回复消息数
        /// </summary>
        [JsonPropertyName("receivedCount")]
        public int ReceivedCount { get; set; }

        /// <summary>
        /// 最后一条消息内容
        /// </summary>
        [JsonPropertyName("lastMessage")]
        public string LastMessage { get; set; } = string.Empty;

        /// <summary>
        /// 最后消息时间
        /// </summary>
        [JsonPropertyName("lastMessageAt")]
        public DateTime? LastMessageAt { get; set; }

        /// <summary>
        /// 最后我方发送时间
        /// </summary>
        [JsonPropertyName("lastSentAt")]
        public DateTime? LastSentAt { get; set; }

        /// <summary>
        /// 最后对方回复时间
        /// </summary>
        [JsonPropertyName("lastReceivedAt")]
        public DateTime? LastReceivedAt { get; set; }

        /// <summary>
        /// 是否已读（对方是否已读我方消息）
        /// </summary>
        [JsonPropertyName("isRead")]
        public bool IsRead { get; set; }

        /// <summary>
        /// 跟进次数
        /// </summary>
        [JsonPropertyName("followUpCount")]
        public int FollowUpCount { get; set; }

        /// <summary>
        /// 最后跟进时间
        /// </summary>
        [JsonPropertyName("lastFollowUpAt")]
        public DateTime? LastFollowUpAt { get; set; }

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

        /// <summary>
        /// 消息列表
        /// </summary>
        [JsonPropertyName("messages")]
        public List<ChatMessage> Messages { get; set; } = new();

        /// <summary>
        /// 标签
        /// </summary>
        [JsonPropertyName("tags")]
        public List<string> Tags { get; set; } = new();

        /// <summary>
        /// 备注
        /// </summary>
        [JsonPropertyName("notes")]
        public string Notes { get; set; } = string.Empty;
    }

    /// <summary>
    /// 会话状态
    /// </summary>
    public enum ConversationStatus
    {
        /// <summary>新会话</summary>
        New = 0,
        /// <summary>已打招呼</summary>
        Greeted = 1,
        /// <summary>等待回复</summary>
        WaitingReply = 2,
        /// <summary>已回复</summary>
        Replied = 3,
        /// <summary>沟通中</summary>
        InProgress = 4,
        /// <summary>已约面</summary>
        Scheduled = 5,
        /// <summary>已结束</summary>
        Closed = 6,
        /// <summary>已忽略</summary>
        Ignored = 7
    }

    /// <summary>
    /// 聊天消息
    /// </summary>
    public class ChatMessage
    {
        /// <summary>
        /// 消息ID
        /// </summary>
        [JsonPropertyName("id")]
        public string Id { get; set; } = Guid.NewGuid().ToString("N");

        /// <summary>
        /// 是否我方发送
        /// </summary>
        [JsonPropertyName("isSent")]
        public bool IsSent { get; set; }

        /// <summary>
        /// 消息内容
        /// </summary>
        [JsonPropertyName("content")]
        public string Content { get; set; } = string.Empty;

        /// <summary>
        /// 消息类型
        /// </summary>
        [JsonPropertyName("type")]
        public MessageType Type { get; set; } = MessageType.Text;

        /// <summary>
        /// 发送时间
        /// </summary>
        [JsonPropertyName("timestamp")]
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// 是否已读
        /// </summary>
        [JsonPropertyName("isRead")]
        public bool IsRead { get; set; }

        /// <summary>
        /// 是否自动发送
        /// </summary>
        [JsonPropertyName("isAuto")]
        public bool IsAuto { get; set; }

        /// <summary>
        /// 触发规则（如果是自动回复）
        /// </summary>
        [JsonPropertyName("triggerRule")]
        public string TriggerRule { get; set; } = string.Empty;
    }

    /// <summary>
    /// 消息类型
    /// </summary>
    public enum MessageType
    {
        Text = 0,
        Image = 1,
        File = 2,
        Link = 3,
        System = 4
    }
}
