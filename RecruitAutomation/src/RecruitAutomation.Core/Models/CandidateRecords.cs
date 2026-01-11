using System;
using System.Text.Json.Serialization;

namespace RecruitAutomation.Core.Models
{
    /// <summary>
    /// 教育经历
    /// </summary>
    public class EducationRecord
    {
        [JsonPropertyName("school")]
        public string School { get; set; } = string.Empty;

        [JsonPropertyName("major")]
        public string Major { get; set; } = string.Empty;

        [JsonPropertyName("degree")]
        public string Degree { get; set; } = string.Empty;

        [JsonPropertyName("startDate")]
        public string StartDate { get; set; } = string.Empty;

        [JsonPropertyName("endDate")]
        public string EndDate { get; set; } = string.Empty;

        [JsonPropertyName("is985")]
        public bool Is985 { get; set; }

        [JsonPropertyName("is211")]
        public bool Is211 { get; set; }

        [JsonPropertyName("isDoubleFirstClass")]
        public bool IsDoubleFirstClass { get; set; }

        [JsonPropertyName("isQS300")]
        public bool IsQS300 { get; set; }
    }

    /// <summary>
    /// 工作经历
    /// </summary>
    public class WorkExperience
    {
        [JsonPropertyName("company")]
        public string Company { get; set; } = string.Empty;

        [JsonPropertyName("position")]
        public string Position { get; set; } = string.Empty;

        [JsonPropertyName("department")]
        public string Department { get; set; } = string.Empty;

        [JsonPropertyName("startDate")]
        public string StartDate { get; set; } = string.Empty;

        [JsonPropertyName("endDate")]
        public string EndDate { get; set; } = string.Empty;

        [JsonPropertyName("description")]
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// 工作内容关键词
        /// </summary>
        [JsonPropertyName("keywords")]
        public string[] Keywords { get; set; } = Array.Empty<string>();
    }

    /// <summary>
    /// 项目经历
    /// </summary>
    public class ProjectExperience
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("role")]
        public string Role { get; set; } = string.Empty;

        [JsonPropertyName("startDate")]
        public string StartDate { get; set; } = string.Empty;

        [JsonPropertyName("endDate")]
        public string EndDate { get; set; } = string.Empty;

        [JsonPropertyName("description")]
        public string Description { get; set; } = string.Empty;

        [JsonPropertyName("technologies")]
        public string[] Technologies { get; set; } = Array.Empty<string>();
    }
}
