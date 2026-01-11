using System.Collections.Generic;
using System.Text;
using RecruitAutomation.Core.Models;

namespace RecruitAutomation.Core.AI
{
    /// <summary>
    /// AI 回复上下文构造器
    /// 将简历、岗位、历史消息组装成 AI 输入
    /// </summary>
    public class AIReplyContext
    {
        /// <summary>
        /// 岗位信息
        /// </summary>
        public JobInfo? Job { get; set; }

        /// <summary>
        /// 候选人信息
        /// </summary>
        public CandidateInfo? Candidate { get; set; }

        /// <summary>
        /// 历史消息
        /// </summary>
        public List<ChatMessage> HistoryMessages { get; set; } = new();

        /// <summary>
        /// 当前收到的消息
        /// </summary>
        public string CurrentMessage { get; set; } = string.Empty;

        /// <summary>
        /// 构建岗位信息文本
        /// </summary>
        public string BuildJobInfoText()
        {
            if (Job == null)
                return "暂无岗位信息";

            var sb = new StringBuilder();
            sb.AppendLine($"岗位名称：{Job.Title}");
            
            if (!string.IsNullOrEmpty(Job.Department))
                sb.AppendLine($"所属部门：{Job.Department}");
            
            if (!string.IsNullOrEmpty(Job.SalaryRange))
                sb.AppendLine($"薪资范围：{Job.SalaryRange}");
            
            if (!string.IsNullOrEmpty(Job.Location))
                sb.AppendLine($"工作地点：{Job.Location}");
            
            if (!string.IsNullOrEmpty(Job.Requirements))
                sb.AppendLine($"岗位要求：{Job.Requirements}");
            
            if (!string.IsNullOrEmpty(Job.Description))
                sb.AppendLine($"工作内容：{Job.Description}");
            
            if (!string.IsNullOrEmpty(Job.Benefits))
                sb.AppendLine($"福利待遇：{Job.Benefits}");

            return sb.ToString();
        }

        /// <summary>
        /// 构建简历信息文本
        /// </summary>
        public string BuildResumeInfoText()
        {
            if (Candidate == null)
                return "暂无简历信息";

            var sb = new StringBuilder();
            sb.AppendLine($"姓名：{Candidate.Name}");
            
            if (!string.IsNullOrEmpty(Candidate.CurrentPosition))
                sb.AppendLine($"当前职位：{Candidate.CurrentPosition}");
            
            if (!string.IsNullOrEmpty(Candidate.CurrentCompany))
                sb.AppendLine($"当前公司：{Candidate.CurrentCompany}");
            
            if (Candidate.WorkYears > 0)
                sb.AppendLine($"工作年限：{Candidate.WorkYears}年");
            
            if (!string.IsNullOrEmpty(Candidate.Education))
                sb.AppendLine($"学历：{Candidate.Education}");
            
            if (!string.IsNullOrEmpty(Candidate.School))
                sb.AppendLine($"毕业院校：{Candidate.School}");
            
            if (!string.IsNullOrEmpty(Candidate.Major))
                sb.AppendLine($"专业：{Candidate.Major}");
            
            if (!string.IsNullOrEmpty(Candidate.ExpectedPosition))
                sb.AppendLine($"期望岗位：{Candidate.ExpectedPosition}");
            
            if (Candidate.ExpectedSalaryMin.HasValue || Candidate.ExpectedSalaryMax.HasValue)
                sb.AppendLine($"期望薪资：{Candidate.ExpectedSalaryMin ?? 0}-{Candidate.ExpectedSalaryMax ?? 0}K");
            
            if (!string.IsNullOrEmpty(Candidate.ExpectedCity))
                sb.AppendLine($"期望城市：{Candidate.ExpectedCity}");
            
            if (Candidate.Skills?.Count > 0)
                sb.AppendLine($"技能标签：{string.Join("、", Candidate.Skills)}");

            return sb.ToString();
        }

        /// <summary>
        /// 构建历史消息列表（用于 AI 上下文）
        /// </summary>
        public List<ChatMessageItem> BuildHistoryMessageItems(int maxCount = 10)
        {
            var items = new List<ChatMessageItem>();
            
            // 取最近的消息
            var startIndex = HistoryMessages.Count > maxCount 
                ? HistoryMessages.Count - maxCount 
                : 0;

            for (var i = startIndex; i < HistoryMessages.Count; i++)
            {
                var msg = HistoryMessages[i];
                items.Add(new ChatMessageItem
                {
                    Role = msg.IsSent ? "assistant" : "user",
                    Content = msg.Content
                });
            }

            return items;
        }

        /// <summary>
        /// 构建完整的系统提示词
        /// </summary>
        public string BuildSystemPrompt(string template)
        {
            return template
                .Replace("{jobInfo}", BuildJobInfoText())
                .Replace("{resumeInfo}", BuildResumeInfoText());
        }
    }

    /// <summary>
    /// 岗位信息
    /// </summary>
    public class JobInfo
    {
        public string Title { get; set; } = string.Empty;
        public string Department { get; set; } = string.Empty;
        public string SalaryRange { get; set; } = string.Empty;
        public string Location { get; set; } = string.Empty;
        public string Requirements { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Benefits { get; set; } = string.Empty;
        public string Company { get; set; } = string.Empty;
    }
}
