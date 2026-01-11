using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace RecruitAutomation.Core.Models
{
    /// <summary>
    /// 招聘平台类型
    /// </summary>
    public enum RecruitPlatform
    {
        /// <summary>BOSS直聘</summary>
        Boss = 0,
        /// <summary>智联招聘</summary>
        Zhilian = 1,
        /// <summary>前程无忧</summary>
        Job51 = 2,
        /// <summary>猎聘</summary>
        Liepin = 3
    }

    /// <summary>
    /// 岗位状态（仅从页面读取"开放中"状态）
    /// </summary>
    public enum JobStatus
    {
        /// <summary>开放中（唯一允许读取的状态）</summary>
        Open = 1,
        /// <summary>已关闭</summary>
        Closed = 2,
        /// <summary>审核中</summary>
        Reviewing = 3,
        /// <summary>已下线</summary>
        Offline = 4,
        /// <summary>草稿</summary>
        Draft = 5
    }

    /// <summary>
    /// 岗位类型
    /// </summary>
    public enum JobType
    {
        /// <summary>全职</summary>
        FullTime = 0,
        /// <summary>兼职</summary>
        PartTime = 1,
        /// <summary>实习</summary>
        Intern = 2,
        /// <summary>外包</summary>
        Outsource = 3
    }

    /// <summary>
    /// 岗位执行状态
    /// </summary>
    public enum JobExecutionStatus
    {
        /// <summary>待执行</summary>
        Pending = 0,
        /// <summary>执行中</summary>
        Running = 1,
        /// <summary>已完成</summary>
        Completed = 2,
        /// <summary>已暂停</summary>
        Paused = 3,
        /// <summary>执行失败</summary>
        Failed = 4
    }

    /// <summary>
    /// 岗位信息（从真实页面读取）
    /// </summary>
    public class JobPosition
    {
        /// <summary>
        /// 岗位唯一ID（平台_账号ID_平台岗位ID）
        /// </summary>
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// 招聘平台
        /// </summary>
        [JsonPropertyName("platform")]
        public RecruitPlatform Platform { get; set; }

        /// <summary>
        /// 绑定的账号ID（浏览器实例ID）
        /// </summary>
        [JsonPropertyName("accountId")]
        public string AccountId { get; set; } = string.Empty;

        /// <summary>
        /// 平台原始岗位ID
        /// </summary>
        [JsonPropertyName("platformJobId")]
        public string PlatformJobId { get; set; } = string.Empty;

        /// <summary>
        /// 岗位名称
        /// </summary>
        [JsonPropertyName("title")]
        public string Title { get; set; } = string.Empty;

        /// <summary>
        /// 岗位类型
        /// </summary>
        [JsonPropertyName("jobType")]
        public JobType JobType { get; set; } = JobType.FullTime;

        /// <summary>
        /// 薪资下限（K）
        /// </summary>
        [JsonPropertyName("salaryMin")]
        public int SalaryMin { get; set; }

        /// <summary>
        /// 薪资上限（K）
        /// </summary>
        [JsonPropertyName("salaryMax")]
        public int SalaryMax { get; set; }

        /// <summary>
        /// 薪资月数
        /// </summary>
        [JsonPropertyName("salaryMonths")]
        public int SalaryMonths { get; set; } = 12;

        /// <summary>
        /// 薪资显示文本（原始）
        /// </summary>
        [JsonPropertyName("salaryText")]
        public string SalaryText { get; set; } = string.Empty;

        /// <summary>
        /// 工作年限要求
        /// </summary>
        [JsonPropertyName("experienceRequired")]
        public string ExperienceRequired { get; set; } = string.Empty;

        /// <summary>
        /// 学历要求
        /// </summary>
        [JsonPropertyName("educationRequired")]
        public string EducationRequired { get; set; } = string.Empty;

        /// <summary>
        /// 工作地点（城市）
        /// </summary>
        [JsonPropertyName("location")]
        public string Location { get; set; } = string.Empty;

        /// <summary>
        /// 详细地址
        /// </summary>
        [JsonPropertyName("address")]
        public string Address { get; set; } = string.Empty;

        /// <summary>
        /// 岗位描述（JD全文）
        /// </summary>
        [JsonPropertyName("description")]
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// 岗位要求
        /// </summary>
        [JsonPropertyName("requirements")]
        public string Requirements { get; set; } = string.Empty;

        /// <summary>
        /// 岗位关键词（从JD+标签自动提取）
        /// </summary>
        [JsonPropertyName("keywords")]
        public List<string> Keywords { get; set; } = new();

        /// <summary>
        /// 岗位标签
        /// </summary>
        [JsonPropertyName("tags")]
        public List<string> Tags { get; set; } = new();

        /// <summary>
        /// 岗位状态（仅"开放中"才会被读取）
        /// </summary>
        [JsonPropertyName("status")]
        public JobStatus Status { get; set; } = JobStatus.Open;

        /// <summary>
        /// 岗位页面URL
        /// </summary>
        [JsonPropertyName("pageUrl")]
        public string PageUrl { get; set; } = string.Empty;

        /// <summary>
        /// 读取时间
        /// </summary>
        [JsonPropertyName("fetchedAt")]
        public DateTime FetchedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// 最后更新时间
        /// </summary>
        [JsonPropertyName("updatedAt")]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        #region 自动化执行相关

        /// <summary>
        /// 是否启用自动化
        /// </summary>
        [JsonPropertyName("automationEnabled")]
        public bool AutomationEnabled { get; set; }

        /// <summary>
        /// 执行状态
        /// </summary>
        [JsonPropertyName("executionStatus")]
        public JobExecutionStatus ExecutionStatus { get; set; } = JobExecutionStatus.Pending;

        /// <summary>
        /// 已完成匹配次数
        /// </summary>
        [JsonPropertyName("matchedCount")]
        public int MatchedCount { get; set; }

        /// <summary>
        /// 目标匹配次数
        /// </summary>
        [JsonPropertyName("targetMatchCount")]
        public int TargetMatchCount { get; set; } = 100;

        /// <summary>
        /// 已打招呼次数
        /// </summary>
        [JsonPropertyName("greetedCount")]
        public int GreetedCount { get; set; }

        /// <summary>
        /// 已采集简历数
        /// </summary>
        [JsonPropertyName("resumeCollectedCount")]
        public int ResumeCollectedCount { get; set; }

        /// <summary>
        /// 最后执行时间
        /// </summary>
        [JsonPropertyName("lastExecutedAt")]
        public DateTime? LastExecutedAt { get; set; }

        #endregion
    }

    /// <summary>
    /// 岗位读取结果
    /// </summary>
    public class JobFetchResult
    {
        /// <summary>
        /// 是否成功
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// 错误消息
        /// </summary>
        public string ErrorMessage { get; set; } = string.Empty;

        /// <summary>
        /// 错误类型
        /// </summary>
        public JobFetchErrorType ErrorType { get; set; } = JobFetchErrorType.None;

        /// <summary>
        /// 读取到的岗位列表
        /// </summary>
        public List<JobPosition> Jobs { get; set; } = new();

        /// <summary>
        /// 账号ID
        /// </summary>
        public string AccountId { get; set; } = string.Empty;

        /// <summary>
        /// 平台
        /// </summary>
        public RecruitPlatform Platform { get; set; }

        /// <summary>
        /// 读取耗时（毫秒）
        /// </summary>
        public long ElapsedMs { get; set; }

        public static JobFetchResult Fail(string message, JobFetchErrorType errorType = JobFetchErrorType.Unknown)
        {
            return new JobFetchResult
            {
                Success = false,
                ErrorMessage = message,
                ErrorType = errorType
            };
        }

        public static JobFetchResult Ok(List<JobPosition> jobs)
        {
            return new JobFetchResult
            {
                Success = true,
                Jobs = jobs
            };
        }
    }

    /// <summary>
    /// 岗位读取错误类型
    /// </summary>
    public enum JobFetchErrorType
    {
        None = 0,
        /// <summary>账号未启动</summary>
        AccountNotStarted = 1,
        /// <summary>账号登录失效</summary>
        LoginExpired = 2,
        /// <summary>页面加载失败</summary>
        PageLoadFailed = 3,
        /// <summary>页面结构变化</summary>
        PageStructureChanged = 4,
        /// <summary>无开放岗位</summary>
        NoOpenJobs = 5,
        /// <summary>网络错误</summary>
        NetworkError = 6,
        /// <summary>未知错误</summary>
        Unknown = 99
    }

    /// <summary>
    /// 岗位操作记录
    /// </summary>
    public class JobOperationLog
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = Guid.NewGuid().ToString("N");

        [JsonPropertyName("jobId")]
        public string JobId { get; set; } = string.Empty;

        [JsonPropertyName("accountId")]
        public string AccountId { get; set; } = string.Empty;

        [JsonPropertyName("operation")]
        public JobOperation Operation { get; set; }

        [JsonPropertyName("timestamp")]
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        [JsonPropertyName("success")]
        public bool Success { get; set; }

        [JsonPropertyName("message")]
        public string Message { get; set; } = string.Empty;
    }

    /// <summary>
    /// 岗位操作类型
    /// </summary>
    public enum JobOperation
    {
        /// <summary>读取岗位</summary>
        Fetch = 0,
        /// <summary>启用自动化</summary>
        EnableAutomation = 1,
        /// <summary>禁用自动化</summary>
        DisableAutomation = 2,
        /// <summary>开始执行</summary>
        StartExecution = 3,
        /// <summary>暂停执行</summary>
        PauseExecution = 4,
        /// <summary>完成执行</summary>
        CompleteExecution = 5,
        /// <summary>删除岗位</summary>
        Delete = 6
    }

    /// <summary>
    /// 账号登录状态
    /// </summary>
    public enum AccountLoginStatus
    {
        /// <summary>未知</summary>
        Unknown = 0,
        /// <summary>已登录</summary>
        LoggedIn = 1,
        /// <summary>未登录</summary>
        NotLoggedIn = 2,
        /// <summary>登录过期</summary>
        Expired = 3
    }

    /// <summary>
    /// 可用于岗位读取的账号信息
    /// </summary>
    public class AvailableAccount
    {
        /// <summary>
        /// 账号ID
        /// </summary>
        public string AccountId { get; set; } = string.Empty;

        /// <summary>
        /// 平台
        /// </summary>
        public RecruitPlatform Platform { get; set; }

        /// <summary>
        /// 是否已启动浏览器实例
        /// </summary>
        public bool IsStarted { get; set; }

        /// <summary>
        /// 登录状态
        /// </summary>
        public AccountLoginStatus LoginStatus { get; set; }

        /// <summary>
        /// 账号显示名称
        /// </summary>
        public string DisplayName { get; set; } = string.Empty;

        /// <summary>
        /// 是否可用于岗位读取（已启动 + 已登录）
        /// </summary>
        public bool IsAvailable => IsStarted && LoginStatus == AccountLoginStatus.LoggedIn;
    }
}
