using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace RecruitAutomation.Core.Models
{
    /// <summary>
    /// 筛选配置
    /// </summary>
    public class FilterConfig
    {
        /// <summary>
        /// 配置名称
        /// </summary>
        [JsonPropertyName("name")]
        public string Name { get; set; } = "默认筛选";

        /// <summary>
        /// 是否启用
        /// </summary>
        [JsonPropertyName("enabled")]
        public bool Enabled { get; set; } = true;

        #region 基本条件

        /// <summary>
        /// 期望岗位关键词（包含任一即可）
        /// </summary>
        [JsonPropertyName("positionKeywords")]
        public List<string> PositionKeywords { get; set; } = new();

        /// <summary>
        /// 排除岗位关键词
        /// </summary>
        [JsonPropertyName("excludePositionKeywords")]
        public List<string> ExcludePositionKeywords { get; set; } = new();

        /// <summary>
        /// 工作内容关键词（包含任一即可）
        /// </summary>
        [JsonPropertyName("workContentKeywords")]
        public List<string> WorkContentKeywords { get; set; } = new();

        /// <summary>
        /// 必须包含的技能
        /// </summary>
        [JsonPropertyName("requiredSkills")]
        public List<string> RequiredSkills { get; set; } = new();

        /// <summary>
        /// 加分技能
        /// </summary>
        [JsonPropertyName("preferredSkills")]
        public List<string> PreferredSkills { get; set; } = new();

        #endregion

        #region 工作年限

        /// <summary>
        /// 最小工作年限
        /// </summary>
        [JsonPropertyName("minWorkYears")]
        public int? MinWorkYears { get; set; }

        /// <summary>
        /// 最大工作年限
        /// </summary>
        [JsonPropertyName("maxWorkYears")]
        public int? MaxWorkYears { get; set; }

        #endregion

        #region 学历要求

        /// <summary>
        /// 最低学历
        /// </summary>
        [JsonPropertyName("minEducation")]
        public string MinEducation { get; set; } = string.Empty;

        /// <summary>
        /// 是否要求985
        /// </summary>
        [JsonPropertyName("require985")]
        public bool Require985 { get; set; }

        /// <summary>
        /// 是否要求211
        /// </summary>
        [JsonPropertyName("require211")]
        public bool Require211 { get; set; }

        /// <summary>
        /// 是否要求双一流
        /// </summary>
        [JsonPropertyName("requireDoubleFirstClass")]
        public bool RequireDoubleFirstClass { get; set; }

        /// <summary>
        /// 是否要求QS300
        /// </summary>
        [JsonPropertyName("requireQS300")]
        public bool RequireQS300 { get; set; }

        /// <summary>
        /// 985/211/双一流/QS300 满足任一即可
        /// </summary>
        [JsonPropertyName("topSchoolAnyMatch")]
        public bool TopSchoolAnyMatch { get; set; } = true;

        #endregion

        #region 活跃度要求

        /// <summary>
        /// 最低活跃度评分（1-5）
        /// </summary>
        [JsonPropertyName("minActivityScore")]
        public int? MinActivityScore { get; set; }

        /// <summary>
        /// 最低回复率（0-100）
        /// </summary>
        [JsonPropertyName("minReplyRate")]
        public int? MinReplyRate { get; set; }

        /// <summary>
        /// 最大回复时长（分钟）
        /// </summary>
        [JsonPropertyName("maxReplyMinutes")]
        public int? MaxReplyMinutes { get; set; }

        #endregion

        #region 薪资要求

        /// <summary>
        /// 期望薪资上限（K）
        /// </summary>
        [JsonPropertyName("maxExpectedSalary")]
        public int? MaxExpectedSalary { get; set; }

        #endregion

        #region 年龄要求

        /// <summary>
        /// 最小年龄
        /// </summary>
        [JsonPropertyName("minAge")]
        public int? MinAge { get; set; }

        /// <summary>
        /// 最大年龄
        /// </summary>
        [JsonPropertyName("maxAge")]
        public int? MaxAge { get; set; }

        #endregion

        #region 评分权重

        /// <summary>
        /// 学历权重
        /// </summary>
        [JsonPropertyName("educationWeight")]
        public double EducationWeight { get; set; } = 0.2;

        /// <summary>
        /// 工作经验权重
        /// </summary>
        [JsonPropertyName("experienceWeight")]
        public double ExperienceWeight { get; set; } = 0.3;

        /// <summary>
        /// 技能匹配权重
        /// </summary>
        [JsonPropertyName("skillWeight")]
        public double SkillWeight { get; set; } = 0.25;

        /// <summary>
        /// 活跃度权重
        /// </summary>
        [JsonPropertyName("activityWeight")]
        public double ActivityWeight { get; set; } = 0.15;

        /// <summary>
        /// 名校加分权重
        /// </summary>
        [JsonPropertyName("topSchoolWeight")]
        public double TopSchoolWeight { get; set; } = 0.1;

        #endregion

        #region 通过阈值

        /// <summary>
        /// 通过分数阈值（0-100）
        /// </summary>
        [JsonPropertyName("passScore")]
        public double PassScore { get; set; } = 60;

        /// <summary>
        /// 自动打招呼分数阈值
        /// </summary>
        [JsonPropertyName("autoGreetScore")]
        public double AutoGreetScore { get; set; } = 70;

        #endregion
    }
}
