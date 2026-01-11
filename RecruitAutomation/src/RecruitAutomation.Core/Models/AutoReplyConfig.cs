using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace RecruitAutomation.Core.Models
{
    /// <summary>
    /// 自动回复配置
    /// </summary>
    public class AutoReplyConfig
    {
        /// <summary>
        /// 是否启用自动回复
        /// </summary>
        [JsonPropertyName("enabled")]
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// 关键词回复规则
        /// </summary>
        [JsonPropertyName("keywordRules")]
        public List<KeywordReplyRule> KeywordRules { get; set; } = new();

        /// <summary>
        /// 跟进配置
        /// </summary>
        [JsonPropertyName("followUpConfig")]
        public FollowUpConfig FollowUpConfig { get; set; } = new();

        /// <summary>
        /// 群发配置
        /// </summary>
        [JsonPropertyName("batchSendConfig")]
        public BatchSendConfig BatchSendConfig { get; set; } = new();

        /// <summary>
        /// 打招呼模板
        /// </summary>
        [JsonPropertyName("greetTemplates")]
        public List<GreetTemplate> GreetTemplates { get; set; } = new();

        /// <summary>
        /// 默认配置
        /// </summary>
        public static AutoReplyConfig Default => new()
        {
            Enabled = true,
            KeywordRules = GetDefaultKeywordRules(),
            FollowUpConfig = new FollowUpConfig(),
            BatchSendConfig = new BatchSendConfig(),
            GreetTemplates = GetDefaultGreetTemplates()
        };

        private static List<KeywordReplyRule> GetDefaultKeywordRules() => new()
        {
            new KeywordReplyRule
            {
                Name = "薪资询问",
                Keywords = new() { "薪资", "工资", "待遇", "薪酬", "多少钱", "薪水" },
                Reply = "您好，我们的薪资范围是{salary}，具体会根据您的能力和经验来定，方便电话沟通详细了解吗？",
                Priority = 10
            },
            new KeywordReplyRule
            {
                Name = "工作地点",
                Keywords = new() { "地点", "地址", "在哪", "哪里上班", "工作地" },
                Reply = "我们公司在{location}，交通很方便，您方便过来面试吗？",
                Priority = 9
            },
            new KeywordReplyRule
            {
                Name = "岗位职责",
                Keywords = new() { "做什么", "工作内容", "职责", "负责什么" },
                Reply = "这个岗位主要负责{jobDesc}，您之前的经验很匹配，方便详细聊聊吗？",
                Priority = 8
            },
            new KeywordReplyRule
            {
                Name = "表示兴趣",
                Keywords = new() { "感兴趣", "可以", "好的", "行", "没问题", "OK", "ok" },
                Reply = "太好了！方便留个电话吗？我这边安排HR跟您联系，或者您也可以加我微信{wechat}详聊",
                Priority = 7
            },
            new KeywordReplyRule
            {
                Name = "询问公司",
                Keywords = new() { "公司", "什么公司", "哪家公司", "公司名" },
                Reply = "我们是{company}，主要做{business}，团队氛围很好，期待您的加入！",
                Priority = 6
            },
            new KeywordReplyRule
            {
                Name = "暂不考虑",
                Keywords = new() { "不考虑", "不合适", "不感兴趣", "算了", "不用了" },
                Reply = "好的，感谢您的回复！如果以后有合适的机会，希望还能联系您~",
                Priority = 5,
                EndConversation = true
            }
        };

        private static List<GreetTemplate> GetDefaultGreetTemplates() => new()
        {
            new GreetTemplate
            {
                Name = "通用打招呼",
                Content = "{name}您好，看到您的简历很感兴趣，我们正在招聘{position}，想和您聊聊，方便吗？",
                IsDefault = true
            },
            new GreetTemplate
            {
                Name = "技术岗位",
                Content = "Hi {name}，看到您在{skill}方面经验丰富，我们团队正在找这方面人才，有兴趣了解下吗？"
            },
            new GreetTemplate
            {
                Name = "高匹配度",
                Content = "{name}您好，您的背景和我们岗位非常匹配！我们是{company}，{position}岗位，期待与您沟通！"
            }
        };
    }

    /// <summary>
    /// 关键词回复规则
    /// </summary>
    public class KeywordReplyRule
    {
        /// <summary>
        /// 规则名称
        /// </summary>
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// 触发关键词（包含任一即触发）
        /// </summary>
        [JsonPropertyName("keywords")]
        public List<string> Keywords { get; set; } = new();

        /// <summary>
        /// 排除关键词（包含则不触发）
        /// </summary>
        [JsonPropertyName("excludeKeywords")]
        public List<string> ExcludeKeywords { get; set; } = new();

        /// <summary>
        /// 回复内容（支持变量）
        /// </summary>
        [JsonPropertyName("reply")]
        public string Reply { get; set; } = string.Empty;

        /// <summary>
        /// 优先级（数字越大优先级越高）
        /// </summary>
        [JsonPropertyName("priority")]
        public int Priority { get; set; }

        /// <summary>
        /// 是否启用
        /// </summary>
        [JsonPropertyName("enabled")]
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// 是否结束会话
        /// </summary>
        [JsonPropertyName("endConversation")]
        public bool EndConversation { get; set; }

        /// <summary>
        /// 回复延迟（秒）
        /// </summary>
        [JsonPropertyName("delaySeconds")]
        public int DelaySeconds { get; set; } = 3;
    }

    /// <summary>
    /// 跟进配置
    /// </summary>
    public class FollowUpConfig
    {
        /// <summary>
        /// 是否启用自动跟进
        /// </summary>
        [JsonPropertyName("enabled")]
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// 最大跟进次数
        /// </summary>
        [JsonPropertyName("maxFollowUpCount")]
        public int MaxFollowUpCount { get; set; } = 3;

        /// <summary>
        /// 首次跟进间隔（小时）
        /// </summary>
        [JsonPropertyName("firstFollowUpHours")]
        public int FirstFollowUpHours { get; set; } = 24;

        /// <summary>
        /// 后续跟进间隔（小时）
        /// </summary>
        [JsonPropertyName("subsequentFollowUpHours")]
        public int SubsequentFollowUpHours { get; set; } = 48;

        /// <summary>
        /// 跟进消息模板
        /// </summary>
        [JsonPropertyName("followUpMessages")]
        public List<string> FollowUpMessages { get; set; } = new()
        {
            "{name}您好，之前给您发的消息看到了吗？我们这边{position}岗位还在招聘中，期待您的回复~",
            "{name}，再次打扰了，不知道您对我们的岗位有没有兴趣呢？方便的话可以聊聊~",
            "{name}您好，最后再问一次，如果您暂时不考虑也没关系，祝您工作顺利！"
        };

        /// <summary>
        /// 仅跟进已读未回的
        /// </summary>
        [JsonPropertyName("onlyFollowUpRead")]
        public bool OnlyFollowUpRead { get; set; } = true;
    }

    /// <summary>
    /// 群发配置
    /// </summary>
    public class BatchSendConfig
    {
        /// <summary>
        /// 跳过已发送N条以上的会话
        /// </summary>
        [JsonPropertyName("skipIfSentMoreThan")]
        public int SkipIfSentMoreThan { get; set; } = 5;

        /// <summary>
        /// 跳过已回复的会话
        /// </summary>
        [JsonPropertyName("skipIfReplied")]
        public bool SkipIfReplied { get; set; } = true;

        /// <summary>
        /// 跳过已忽略的会话
        /// </summary>
        [JsonPropertyName("skipIfIgnored")]
        public bool SkipIfIgnored { get; set; } = true;

        /// <summary>
        /// 每批发送数量
        /// </summary>
        [JsonPropertyName("batchSize")]
        public int BatchSize { get; set; } = 50;

        /// <summary>
        /// 发送间隔（毫秒）
        /// </summary>
        [JsonPropertyName("intervalMs")]
        public int IntervalMs { get; set; } = 3000;

        /// <summary>
        /// 每日最大发送数
        /// </summary>
        [JsonPropertyName("dailyLimit")]
        public int DailyLimit { get; set; } = 200;
    }

    /// <summary>
    /// 打招呼模板
    /// </summary>
    public class GreetTemplate
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("content")]
        public string Content { get; set; } = string.Empty;

        [JsonPropertyName("isDefault")]
        public bool IsDefault { get; set; }
    }
}
