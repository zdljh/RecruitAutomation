using System;
using System.Collections.Generic;
using System.Linq;
using RecruitAutomation.Core.Data;
using RecruitAutomation.Core.Models;

namespace RecruitAutomation.Core.Services
{
    /// <summary>
    /// 候选人筛选服务
    /// </summary>
    public class CandidateFilterService
    {
        private readonly FilterConfig _config;

        public CandidateFilterService(FilterConfig config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
        }

        /// <summary>
        /// 筛选结果
        /// </summary>
        public class FilterResult
        {
            public bool Passed { get; set; }
            public double Score { get; set; }
            public bool ShouldAutoGreet { get; set; }
            public List<string> PassReasons { get; set; } = new();
            public List<string> RejectReasons { get; set; } = new();
            public Dictionary<string, double> ScoreDetails { get; set; } = new();
        }

        /// <summary>
        /// 筛选候选人
        /// </summary>
        public FilterResult Filter(CandidateInfo candidate)
        {
            var result = new FilterResult();

            // 1. 硬性条件检查
            if (!CheckHardRequirements(candidate, result))
            {
                result.Passed = false;
                result.Score = 0;
                return result;
            }

            // 2. 计算评分
            result.Score = CalculateScore(candidate, result);
            result.ScoreDetails["总分"] = result.Score;

            // 3. 判断是否通过
            result.Passed = result.Score >= _config.PassScore;
            result.ShouldAutoGreet = result.Score >= _config.AutoGreetScore;

            if (result.Passed)
            {
                result.PassReasons.Add($"综合评分 {result.Score:F1} 分，达到通过线 {_config.PassScore} 分");
            }
            else
            {
                result.RejectReasons.Add($"综合评分 {result.Score:F1} 分，未达到通过线 {_config.PassScore} 分");
            }

            return result;
        }

        /// <summary>
        /// 检查硬性条件
        /// </summary>
        private bool CheckHardRequirements(CandidateInfo candidate, FilterResult result)
        {
            // 检查排除关键词
            if (_config.ExcludePositionKeywords.Count > 0)
            {
                var position = candidate.ExpectedPosition + " " + candidate.CurrentPosition;
                foreach (var keyword in _config.ExcludePositionKeywords)
                {
                    if (position.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                    {
                        result.RejectReasons.Add($"岗位包含排除关键词: {keyword}");
                        return false;
                    }
                }
            }

            // 检查工作年限
            if (_config.MinWorkYears.HasValue && candidate.WorkYears < _config.MinWorkYears.Value)
            {
                result.RejectReasons.Add($"工作年限 {candidate.WorkYears} 年，低于要求 {_config.MinWorkYears} 年");
                return false;
            }
            if (_config.MaxWorkYears.HasValue && candidate.WorkYears > _config.MaxWorkYears.Value)
            {
                result.RejectReasons.Add($"工作年限 {candidate.WorkYears} 年，超过要求 {_config.MaxWorkYears} 年");
                return false;
            }

            // 检查年龄
            if (candidate.Age.HasValue)
            {
                if (_config.MinAge.HasValue && candidate.Age < _config.MinAge)
                {
                    result.RejectReasons.Add($"年龄 {candidate.Age} 岁，低于要求 {_config.MinAge} 岁");
                    return false;
                }
                if (_config.MaxAge.HasValue && candidate.Age > _config.MaxAge)
                {
                    result.RejectReasons.Add($"年龄 {candidate.Age} 岁，超过要求 {_config.MaxAge} 岁");
                    return false;
                }
            }

            // 检查学历
            if (!string.IsNullOrEmpty(_config.MinEducation))
            {
                if (!CheckEducationLevel(candidate.Education, _config.MinEducation))
                {
                    result.RejectReasons.Add($"学历 {candidate.Education}，低于要求 {_config.MinEducation}");
                    return false;
                }
            }

            // 检查名校要求
            if (!CheckTopSchoolRequirement(candidate, result))
            {
                return false;
            }

            // 检查必须技能
            if (_config.RequiredSkills.Count > 0)
            {
                var candidateSkills = string.Join(" ", candidate.Skills) + " " + 
                                      candidate.SelfIntroduction + " " +
                                      string.Join(" ", candidate.WorkExperienceList.Select(w => w.Description));
                
                foreach (var skill in _config.RequiredSkills)
                {
                    if (!candidateSkills.Contains(skill, StringComparison.OrdinalIgnoreCase))
                    {
                        result.RejectReasons.Add($"缺少必须技能: {skill}");
                        return false;
                    }
                }
            }

            // 检查薪资要求
            if (_config.MaxExpectedSalary.HasValue && candidate.ExpectedSalaryMin.HasValue)
            {
                if (candidate.ExpectedSalaryMin > _config.MaxExpectedSalary)
                {
                    result.RejectReasons.Add($"期望薪资 {candidate.ExpectedSalaryMin}K，超过预算 {_config.MaxExpectedSalary}K");
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// 检查名校要求
        /// </summary>
        private bool CheckTopSchoolRequirement(CandidateInfo candidate, FilterResult result)
        {
            bool need985 = _config.Require985;
            bool need211 = _config.Require211;
            bool needDoubleFirstClass = _config.RequireDoubleFirstClass;
            bool needQS300 = _config.RequireQS300;

            // 没有名校要求
            if (!need985 && !need211 && !needDoubleFirstClass && !needQS300)
                return true;

            bool has985 = candidate.Is985 || SchoolDatabase.Is985(candidate.School);
            bool has211 = candidate.Is211 || SchoolDatabase.Is211(candidate.School);
            bool hasDoubleFirstClass = candidate.IsDoubleFirstClass || SchoolDatabase.IsDoubleFirstClass(candidate.School);
            bool hasQS300 = candidate.IsQS300 || SchoolDatabase.IsQS300(candidate.School);

            // 满足任一即可
            if (_config.TopSchoolAnyMatch)
            {
                bool anyMatch = false;
                if (need985 && has985) anyMatch = true;
                if (need211 && has211) anyMatch = true;
                if (needDoubleFirstClass && hasDoubleFirstClass) anyMatch = true;
                if (needQS300 && hasQS300) anyMatch = true;

                // 如果有要求但都不满足
                if (!anyMatch && (need985 || need211 || needDoubleFirstClass || needQS300))
                {
                    var requirements = new List<string>();
                    if (need985) requirements.Add("985");
                    if (need211) requirements.Add("211");
                    if (needDoubleFirstClass) requirements.Add("双一流");
                    if (needQS300) requirements.Add("QS300");
                    result.RejectReasons.Add($"学校 {candidate.School} 不满足名校要求: {string.Join("/", requirements)}");
                    return false;
                }
            }
            else
            {
                // 必须全部满足
                if (need985 && !has985)
                {
                    result.RejectReasons.Add($"学校 {candidate.School} 非985院校");
                    return false;
                }
                if (need211 && !has211)
                {
                    result.RejectReasons.Add($"学校 {candidate.School} 非211院校");
                    return false;
                }
                if (needDoubleFirstClass && !hasDoubleFirstClass)
                {
                    result.RejectReasons.Add($"学校 {candidate.School} 非双一流院校");
                    return false;
                }
                if (needQS300 && !hasQS300)
                {
                    result.RejectReasons.Add($"学校 {candidate.School} 非QS300院校");
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// 计算综合评分
        /// </summary>
        private double CalculateScore(CandidateInfo candidate, FilterResult result)
        {
            double totalScore = 0;

            // 1. 学历评分
            double educationScore = CalculateEducationScore(candidate);
            result.ScoreDetails["学历"] = educationScore;
            totalScore += educationScore * _config.EducationWeight;

            // 2. 工作经验评分
            double experienceScore = CalculateExperienceScore(candidate);
            result.ScoreDetails["经验"] = experienceScore;
            totalScore += experienceScore * _config.ExperienceWeight;

            // 3. 技能匹配评分
            double skillScore = CalculateSkillScore(candidate);
            result.ScoreDetails["技能"] = skillScore;
            totalScore += skillScore * _config.SkillWeight;

            // 4. 活跃度评分
            double activityScore = CalculateActivityScore(candidate);
            result.ScoreDetails["活跃度"] = activityScore;
            totalScore += activityScore * _config.ActivityWeight;

            // 5. 名校加分
            double topSchoolScore = CalculateTopSchoolScore(candidate);
            result.ScoreDetails["名校"] = topSchoolScore;
            totalScore += topSchoolScore * _config.TopSchoolWeight;

            return Math.Round(totalScore, 1);
        }

        /// <summary>
        /// 学历评分
        /// </summary>
        private double CalculateEducationScore(CandidateInfo candidate)
        {
            return candidate.Education switch
            {
                "博士" => 100,
                "硕士" => 90,
                "本科" => 75,
                "大专" => 55,
                "高中" or "中专" => 30,
                _ => 50
            };
        }

        /// <summary>
        /// 工作经验评分
        /// </summary>
        private double CalculateExperienceScore(CandidateInfo candidate)
        {
            double score = 50; // 基础分

            // 工作年限评分
            int years = candidate.WorkYears;
            if (years >= 1 && years <= 3) score += 20;
            else if (years >= 4 && years <= 6) score += 35;
            else if (years >= 7 && years <= 10) score += 40;
            else if (years > 10) score += 30; // 年限过长略减分

            // 工作内容匹配
            if (_config.WorkContentKeywords.Count > 0)
            {
                var workContent = string.Join(" ", candidate.WorkExperienceList.Select(w => w.Description));
                int matchCount = _config.WorkContentKeywords.Count(k => 
                    workContent.Contains(k, StringComparison.OrdinalIgnoreCase));
                score += (matchCount * 10.0 / _config.WorkContentKeywords.Count);
            }

            return Math.Min(100, score);
        }

        /// <summary>
        /// 技能匹配评分
        /// </summary>
        private double CalculateSkillScore(CandidateInfo candidate)
        {
            if (_config.PreferredSkills.Count == 0 && _config.PositionKeywords.Count == 0)
                return 70; // 无要求给基础分

            double score = 50;
            var allText = string.Join(" ", candidate.Skills) + " " +
                          candidate.SelfIntroduction + " " +
                          candidate.ExpectedPosition + " " +
                          string.Join(" ", candidate.WorkExperienceList.Select(w => w.Description));

            // 岗位关键词匹配
            if (_config.PositionKeywords.Count > 0)
            {
                int matchCount = _config.PositionKeywords.Count(k =>
                    allText.Contains(k, StringComparison.OrdinalIgnoreCase));
                score += (matchCount * 25.0 / _config.PositionKeywords.Count);
            }

            // 加分技能匹配
            if (_config.PreferredSkills.Count > 0)
            {
                int matchCount = _config.PreferredSkills.Count(k =>
                    allText.Contains(k, StringComparison.OrdinalIgnoreCase));
                score += (matchCount * 25.0 / _config.PreferredSkills.Count);
            }

            return Math.Min(100, score);
        }

        /// <summary>
        /// 活跃度评分
        /// </summary>
        private double CalculateActivityScore(CandidateInfo candidate)
        {
            double score = 50;

            // 活跃度评分
            score += candidate.ActivityScore * 8; // 1-5 分，最高加40分

            // 回复率评分
            if (candidate.ReplyRate.HasValue)
            {
                score += candidate.ReplyRate.Value * 0.3; // 最高加30分
            }

            // 回复时长评分
            if (candidate.AvgReplyMinutes.HasValue)
            {
                if (candidate.AvgReplyMinutes <= 30) score += 20;
                else if (candidate.AvgReplyMinutes <= 60) score += 15;
                else if (candidate.AvgReplyMinutes <= 120) score += 10;
            }

            return Math.Min(100, score);
        }

        /// <summary>
        /// 名校加分
        /// </summary>
        private double CalculateTopSchoolScore(CandidateInfo candidate)
        {
            double score = 50;

            bool is985 = candidate.Is985 || SchoolDatabase.Is985(candidate.School);
            bool is211 = candidate.Is211 || SchoolDatabase.Is211(candidate.School);
            bool isDoubleFirstClass = candidate.IsDoubleFirstClass || SchoolDatabase.IsDoubleFirstClass(candidate.School);
            bool isQS300 = candidate.IsQS300 || SchoolDatabase.IsQS300(candidate.School);

            if (is985) score += 30;
            else if (is211) score += 20;
            else if (isDoubleFirstClass) score += 15;
            
            if (isQS300) score += 20;

            return Math.Min(100, score);
        }

        /// <summary>
        /// 检查学历等级
        /// </summary>
        private bool CheckEducationLevel(string actual, string required)
        {
            var levels = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                { "博士", 5 },
                { "硕士", 4 },
                { "本科", 3 },
                { "大专", 2 },
                { "高中", 1 },
                { "中专", 1 }
            };

            int actualLevel = levels.GetValueOrDefault(actual, 0);
            int requiredLevel = levels.GetValueOrDefault(required, 0);

            return actualLevel >= requiredLevel;
        }
    }
}
