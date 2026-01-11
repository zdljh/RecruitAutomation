using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using RecruitAutomation.Core.Models;

namespace RecruitAutomation.Core.Services
{
    /// <summary>
    /// 打招呼消息模板
    /// </summary>
    public class GreetMessageTemplate
    {
        /// <summary>
        /// 模板名称
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// 模板内容（支持变量）
        /// </summary>
        public string Content { get; set; } = string.Empty;

        /// <summary>
        /// 适用场景
        /// </summary>
        public string Scenario { get; set; } = string.Empty;

        /// <summary>
        /// 预设模板
        /// </summary>
        public static List<GreetMessageTemplate> Presets => new()
        {
            new GreetMessageTemplate
            {
                Name = "通用招呼",
                Content = "您好，看到您的简历很感兴趣，我们正在招聘{position}，想和您聊聊，方便吗？",
                Scenario = "通用"
            },
            new GreetMessageTemplate
            {
                Name = "技术岗位",
                Content = "Hi {name}，看到您在{skill}方面有丰富经验，我们团队正在寻找这方面的人才，有兴趣了解一下吗？",
                Scenario = "技术"
            },
            new GreetMessageTemplate
            {
                Name = "高匹配度",
                Content = "您好{name}，您的背景和我们的岗位非常匹配！我们是{company}，正在招聘{position}，薪资{salary}，期待与您进一步沟通！",
                Scenario = "高匹配"
            },
            new GreetMessageTemplate
            {
                Name = "名校背景",
                Content = "您好，看到您是{school}毕业的，背景很优秀！我们公司正在招聘{position}，想邀请您了解一下，方便聊聊吗？",
                Scenario = "名校"
            },
            new GreetMessageTemplate
            {
                Name = "简洁版",
                Content = "您好，对您的简历很感兴趣，方便聊聊吗？",
                Scenario = "简洁"
            }
        };

        /// <summary>
        /// 渲染模板
        /// </summary>
        public static string Render(string template, CandidateInfo candidate, Dictionary<string, string>? extraVars = null)
        {
            var result = template;

            // 候选人变量
            result = result.Replace("{name}", candidate.Name ?? "您");
            result = result.Replace("{school}", candidate.School ?? "贵校");
            result = result.Replace("{position}", candidate.ExpectedPosition ?? "相关岗位");
            result = result.Replace("{company}", candidate.CurrentCompany ?? "");
            result = result.Replace("{years}", candidate.WorkYears.ToString());
            
            // 技能（取第一个）
            var skill = candidate.Skills.Count > 0 ? candidate.Skills[0] : "相关领域";
            result = result.Replace("{skill}", skill);

            // 额外变量
            if (extraVars != null)
            {
                foreach (var kv in extraVars)
                {
                    result = result.Replace($"{{{kv.Key}}}", kv.Value);
                }
            }

            // 清理未替换的变量
            result = Regex.Replace(result, @"\{[^}]+\}", "");

            return result.Trim();
        }

        /// <summary>
        /// 根据候选人选择最佳模板
        /// </summary>
        public static GreetMessageTemplate SelectBestTemplate(CandidateInfo candidate, List<GreetMessageTemplate> templates)
        {
            // 名校背景
            if (candidate.Is985 || candidate.Is211 || candidate.IsQS300)
            {
                var t = templates.Find(t => t.Scenario == "名校");
                if (t != null) return t;
            }

            // 高分候选人
            if (candidate.FilterScore >= 80)
            {
                var t = templates.Find(t => t.Scenario == "高匹配");
                if (t != null) return t;
            }

            // 技术岗位
            if (candidate.Skills.Count > 0)
            {
                var t = templates.Find(t => t.Scenario == "技术");
                if (t != null) return t;
            }

            // 默认通用
            return templates.Find(t => t.Scenario == "通用") ?? templates[0];
        }
    }
}
