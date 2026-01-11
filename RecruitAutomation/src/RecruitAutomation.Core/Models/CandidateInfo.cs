using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace RecruitAutomation.Core.Models
{
    /// <summary>
    /// 求职者信息
    /// </summary>
    public class CandidateInfo
    {
        /// <summary>
        /// 唯一标识（平台+平台ID）
        /// </summary>
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// 来源平台
        /// </summary>
        [JsonPropertyName("platform")]
        public string Platform { get; set; } = string.Empty;

        /// <summary>
        /// 平台用户ID
        /// </summary>
        [JsonPropertyName("platformUserId")]
        public string PlatformUserId { get; set; } = string.Empty;

        /// <summary>
        /// 姓名
        /// </summary>
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// 性别
        /// </summary>
        [JsonPropertyName("gender")]
        public string Gender { get; set; } = string.Empty;

        /// <summary>
        /// 年龄
        /// </summary>
        [JsonPropertyName("age")]
        public int? Age { get; set; }

        /// <summary>
        /// 头像URL
        /// </summary>
        [JsonPropertyName("avatarUrl")]
        public string AvatarUrl { get; set; } = string.Empty;

        /// <summary>
        /// 手机号（如有）
        /// </summary>
        [JsonPropertyName("phone")]
        public string Phone { get; set; } = string.Empty;

        /// <summary>
        /// 邮箱（如有）
        /// </summary>
        [JsonPropertyName("email")]
        public string Email { get; set; } = string.Empty;

        #region 教育信息

        /// <summary>
        /// 最高学历
        /// </summary>
        [JsonPropertyName("education")]
        public string Education { get; set; } = string.Empty;

        /// <summary>
        /// 毕业院校
        /// </summary>
        [JsonPropertyName("school")]
        public string School { get; set; } = string.Empty;

        /// <summary>
        /// 专业
        /// </summary>
        [JsonPropertyName("major")]
        public string Major { get; set; } = string.Empty;

        /// <summary>
        /// 是否985
        /// </summary>
        [JsonPropertyName("is985")]
        public bool Is985 { get; set; }

        /// <summary>
        /// 是否211
        /// </summary>
        [JsonPropertyName("is211")]
        public bool Is211 { get; set; }

        /// <summary>
        /// 是否双一流
        /// </summary>
        [JsonPropertyName("isDoubleFirstClass")]
        public bool IsDoubleFirstClass { get; set; }

        /// <summary>
        /// 是否QS300
        /// </summary>
        [JsonPropertyName("isQS300")]
        public bool IsQS300 { get; set; }

        /// <summary>
        /// 教育经历列表
        /// </summary>
        [JsonPropertyName("educationList")]
        public List<EducationRecord> EducationList { get; set; } = new();

        #endregion

        #region 工作信息

        /// <summary>
        /// 工作年限
        /// </summary>
        [JsonPropertyName("workYears")]
        public int WorkYears { get; set; }

        /// <summary>
        /// 当前/最近公司
        /// </summary>
        [JsonPropertyName("currentCompany")]
        public string CurrentCompany { get; set; } = string.Empty;

        /// <summary>
        /// 当前/最近职位
        /// </summary>
        [JsonPropertyName("currentPosition")]
        public string CurrentPosition { get; set; } = string.Empty;

        /// <summary>
        /// 工作经历列表
        /// </summary>
        [JsonPropertyName("workExperienceList")]
        public List<WorkExperience> WorkExperienceList { get; set; } = new();

        /// <summary>
        /// 项目经历列表
        /// </summary>
        [JsonPropertyName("projectList")]
        public List<ProjectExperience> ProjectList { get; set; } = new();

        #endregion

        #region 求职意向

        /// <summary>
        /// 期望岗位
        /// </summary>
        [JsonPropertyName("expectedPosition")]
        public string ExpectedPosition { get; set; } = string.Empty;

        /// <summary>
        /// 期望城市
        /// </summary>
        [JsonPropertyName("expectedCity")]
        public string ExpectedCity { get; set; } = string.Empty;

        /// <summary>
        /// 期望薪资（K）
        /// </summary>
        [JsonPropertyName("expectedSalaryMin")]
        public int? ExpectedSalaryMin { get; set; }

        [JsonPropertyName("expectedSalaryMax")]
        public int? ExpectedSalaryMax { get; set; }

        /// <summary>
        /// 求职状态
        /// </summary>
        [JsonPropertyName("jobStatus")]
        public string JobStatus { get; set; } = string.Empty;

        #endregion

        #region 技能与标签

        /// <summary>
        /// 技能列表
        /// </summary>
        [JsonPropertyName("skills")]
        public List<string> Skills { get; set; } = new();

        /// <summary>
        /// 标签
        /// </summary>
        [JsonPropertyName("tags")]
        public List<string> Tags { get; set; } = new();

        /// <summary>
        /// 自我介绍
        /// </summary>
        [JsonPropertyName("selfIntroduction")]
        public string SelfIntroduction { get; set; } = string.Empty;

        #endregion

        #region 活跃度信息

        /// <summary>
        /// 最后活跃时间描述
        /// </summary>
        [JsonPropertyName("lastActiveText")]
        public string LastActiveText { get; set; } = string.Empty;

        /// <summary>
        /// 活跃度评分（1-5）
        /// </summary>
        [JsonPropertyName("activityScore")]
        public int ActivityScore { get; set; }

        /// <summary>
        /// 回复率（0-100）
        /// </summary>
        [JsonPropertyName("replyRate")]
        public int? ReplyRate { get; set; }

        /// <summary>
        /// 平均回复时长（分钟）
        /// </summary>
        [JsonPropertyName("avgReplyMinutes")]
        public int? AvgReplyMinutes { get; set; }

        #endregion

        #region 系统字段

        /// <summary>
        /// 采集时间
        /// </summary>
        [JsonPropertyName("collectedAt")]
        public DateTime CollectedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// 更新时间
        /// </summary>
        [JsonPropertyName("updatedAt")]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// 处理状态
        /// </summary>
        [JsonPropertyName("status")]
        public CandidateStatus Status { get; set; } = CandidateStatus.New;

        /// <summary>
        /// 筛选得分
        /// </summary>
        [JsonPropertyName("filterScore")]
        public double FilterScore { get; set; }

        /// <summary>
        /// 是否已打招呼
        /// </summary>
        [JsonPropertyName("hasGreeted")]
        public bool HasGreeted { get; set; }

        /// <summary>
        /// 打招呼时间
        /// </summary>
        [JsonPropertyName("greetedAt")]
        public DateTime? GreetedAt { get; set; }

        /// <summary>
        /// 备注
        /// </summary>
        [JsonPropertyName("notes")]
        public string Notes { get; set; } = string.Empty;

        /// <summary>
        /// 简历原始URL
        /// </summary>
        [JsonPropertyName("resumeUrl")]
        public string ResumeUrl { get; set; } = string.Empty;

        #endregion
    }

    /// <summary>
    /// 候选人状态
    /// </summary>
    public enum CandidateStatus
    {
        /// <summary>新采集</summary>
        New = 0,
        /// <summary>筛选通过</summary>
        Passed = 1,
        /// <summary>筛选未通过</summary>
        Rejected = 2,
        /// <summary>已打招呼</summary>
        Greeted = 3,
        /// <summary>已回复</summary>
        Replied = 4,
        /// <summary>已沟通</summary>
        Contacted = 5,
        /// <summary>已标记</summary>
        Marked = 6,
        /// <summary>已忽略</summary>
        Ignored = 7
    }
}
