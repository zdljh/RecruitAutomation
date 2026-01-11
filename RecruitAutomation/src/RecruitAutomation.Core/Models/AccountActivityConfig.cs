using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace RecruitAutomation.Core.Models
{
    /// <summary>
    /// 账号活跃度配置
    /// </summary>
    public class AccountActivityConfig
    {
        /// <summary>
        /// 是否启用自动活跃
        /// </summary>
        [JsonPropertyName("enabled")]
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// 活跃时间段（24小时制）
        /// </summary>
        [JsonPropertyName("activeHours")]
        public ActiveTimeRange ActiveHours { get; set; } = new();

        /// <summary>
        /// 岗位刷新配置
        /// </summary>
        [JsonPropertyName("jobRefresh")]
        public JobRefreshConfig JobRefresh { get; set; } = new();

        /// <summary>
        /// 简历浏览配置
        /// </summary>
        [JsonPropertyName("resumeBrowse")]
        public ResumeBrowseConfig ResumeBrowse { get; set; } = new();

        /// <summary>
        /// 消息检查配置
        /// </summary>
        [JsonPropertyName("messageCheck")]
        public MessageCheckConfig MessageCheck { get; set; } = new();

        /// <summary>
        /// 随机行为配置
        /// </summary>
        [JsonPropertyName("randomBehavior")]
        public RandomBehaviorConfig RandomBehavior { get; set; } = new();

        /// <summary>
        /// 默认配置
        /// </summary>
        public static AccountActivityConfig Default => new();
    }

    /// <summary>
    /// 活跃时间段
    /// </summary>
    public class ActiveTimeRange
    {
        /// <summary>
        /// 开始小时（0-23）
        /// </summary>
        [JsonPropertyName("startHour")]
        public int StartHour { get; set; } = 9;

        /// <summary>
        /// 结束小时（0-23）
        /// </summary>
        [JsonPropertyName("endHour")]
        public int EndHour { get; set; } = 21;

        /// <summary>
        /// 午休开始
        /// </summary>
        [JsonPropertyName("lunchStartHour")]
        public int LunchStartHour { get; set; } = 12;

        /// <summary>
        /// 午休结束
        /// </summary>
        [JsonPropertyName("lunchEndHour")]
        public int LunchEndHour { get; set; } = 14;

        /// <summary>
        /// 是否跳过午休
        /// </summary>
        [JsonPropertyName("skipLunch")]
        public bool SkipLunch { get; set; } = true;

        /// <summary>
        /// 工作日（0=周日，1=周一...）
        /// </summary>
        [JsonPropertyName("workDays")]
        public List<int> WorkDays { get; set; } = new() { 1, 2, 3, 4, 5 };

        /// <summary>
        /// 判断当前是否在活跃时间
        /// </summary>
        public bool IsActiveNow()
        {
            var now = DateTime.Now;
            var dayOfWeek = (int)now.DayOfWeek;
            var hour = now.Hour;

            // 检查工作日
            if (!WorkDays.Contains(dayOfWeek))
                return false;

            // 检查时间范围
            if (hour < StartHour || hour >= EndHour)
                return false;

            // 检查午休
            if (SkipLunch && hour >= LunchStartHour && hour < LunchEndHour)
                return false;

            return true;
        }
    }

    /// <summary>
    /// 岗位刷新配置
    /// </summary>
    public class JobRefreshConfig
    {
        /// <summary>
        /// 是否启用
        /// </summary>
        [JsonPropertyName("enabled")]
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// 刷新间隔（小时）
        /// </summary>
        [JsonPropertyName("intervalHours")]
        public int IntervalHours { get; set; } = 4;

        /// <summary>
        /// 每次刷新延迟（毫秒）
        /// </summary>
        [JsonPropertyName("delayBetweenMs")]
        public int DelayBetweenMs { get; set; } = 5000;
    }

    /// <summary>
    /// 简历浏览配置
    /// </summary>
    public class ResumeBrowseConfig
    {
        /// <summary>
        /// 是否启用
        /// </summary>
        [JsonPropertyName("enabled")]
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// 每次浏览数量
        /// </summary>
        [JsonPropertyName("countPerSession")]
        public int CountPerSession { get; set; } = 20;

        /// <summary>
        /// 浏览间隔（分钟）
        /// </summary>
        [JsonPropertyName("intervalMinutes")]
        public int IntervalMinutes { get; set; } = 30;

        /// <summary>
        /// 每份简历停留时间（秒）
        /// </summary>
        [JsonPropertyName("viewDurationSeconds")]
        public int ViewDurationSeconds { get; set; } = 5;
    }

    /// <summary>
    /// 消息检查配置
    /// </summary>
    public class MessageCheckConfig
    {
        /// <summary>
        /// 是否启用
        /// </summary>
        [JsonPropertyName("enabled")]
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// 检查间隔（分钟）
        /// </summary>
        [JsonPropertyName("intervalMinutes")]
        public int IntervalMinutes { get; set; } = 5;

        /// <summary>
        /// 是否自动回复
        /// </summary>
        [JsonPropertyName("autoReply")]
        public bool AutoReply { get; set; } = true;
    }

    /// <summary>
    /// 随机行为配置
    /// </summary>
    public class RandomBehaviorConfig
    {
        /// <summary>
        /// 是否启用
        /// </summary>
        [JsonPropertyName("enabled")]
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// 随机滚动概率（0-100）
        /// </summary>
        [JsonPropertyName("scrollProbability")]
        public int ScrollProbability { get; set; } = 30;

        /// <summary>
        /// 随机点击概率（0-100）
        /// </summary>
        [JsonPropertyName("clickProbability")]
        public int ClickProbability { get; set; } = 20;

        /// <summary>
        /// 随机暂停概率（0-100）
        /// </summary>
        [JsonPropertyName("pauseProbability")]
        public int PauseProbability { get; set; } = 40;

        /// <summary>
        /// 暂停时长范围（秒）
        /// </summary>
        [JsonPropertyName("pauseDurationMin")]
        public int PauseDurationMin { get; set; } = 30;

        [JsonPropertyName("pauseDurationMax")]
        public int PauseDurationMax { get; set; } = 180;
    }

    /// <summary>
    /// 活跃度统计
    /// </summary>
    public class ActivityStatistics
    {
        [JsonPropertyName("date")]
        public DateTime Date { get; set; } = DateTime.Today;

        [JsonPropertyName("jobRefreshCount")]
        public int JobRefreshCount { get; set; }

        [JsonPropertyName("resumeBrowseCount")]
        public int ResumeBrowseCount { get; set; }

        [JsonPropertyName("messageCheckCount")]
        public int MessageCheckCount { get; set; }

        [JsonPropertyName("greetSentCount")]
        public int GreetSentCount { get; set; }

        [JsonPropertyName("replySentCount")]
        public int ReplySentCount { get; set; }

        [JsonPropertyName("totalActiveMinutes")]
        public int TotalActiveMinutes { get; set; }

        [JsonPropertyName("lastActiveAt")]
        public DateTime? LastActiveAt { get; set; }
    }
}
