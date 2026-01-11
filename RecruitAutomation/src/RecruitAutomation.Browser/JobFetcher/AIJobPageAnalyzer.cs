using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using RecruitAutomation.Core.Models;

namespace RecruitAutomation.Browser.JobFetcher
{
    /// <summary>
    /// AI岗位页面分析器
    /// 使用国内免费AI（智谱GLM-4-Flash）分析页面内容，模拟人工理解
    /// </summary>
    public class AIJobPageAnalyzer
    {
        private readonly HttpClient _httpClient;
        private readonly AIAnalyzerConfig _config;
        private readonly Random _random = new();

        public AIJobPageAnalyzer(AIAnalyzerConfig? config = null)
        {
            _config = config ?? AIAnalyzerConfig.Default;
            _httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(30)
            };
        }

        /// <summary>
        /// 更新配置
        /// </summary>
        public void UpdateConfig(AIAnalyzerConfig config)
        {
            _config.ApiKey = config.ApiKey;
            _config.BaseUrl = config.BaseUrl;
            _config.Model = config.Model;
        }

        /// <summary>
        /// AI分析页面HTML，提取岗位列表
        /// </summary>
        public async Task<List<AIExtractedJob>> AnalyzeJobListPageAsync(
            string pageHtml,
            RecruitPlatform platform,
            CancellationToken ct = default)
        {
            // 模拟人工阅读延迟
            await SimulateHumanReadingDelay(ct);

            var prompt = BuildJobListPrompt(pageHtml, platform);
            var response = await CallAIAsync(prompt, ct);

            if (!response.Success || string.IsNullOrEmpty(response.Content))
            {
                return new List<AIExtractedJob>();
            }

            return ParseJobListResponse(response.Content);
        }

        /// <summary>
        /// AI分析岗位详情页面
        /// </summary>
        public async Task<AIJobDetail?> AnalyzeJobDetailPageAsync(
            string pageHtml,
            RecruitPlatform platform,
            CancellationToken ct = default)
        {
            await SimulateHumanReadingDelay(ct);

            var prompt = BuildJobDetailPrompt(pageHtml, platform);
            var response = await CallAIAsync(prompt, ct);

            if (!response.Success || string.IsNullOrEmpty(response.Content))
            {
                return null;
            }

            return ParseJobDetailResponse(response.Content);
        }

        /// <summary>
        /// AI判断页面是否加载完成
        /// </summary>
        public async Task<AIPageStatus> AnalyzePageStatusAsync(
            string pageHtml,
            string currentUrl,
            CancellationToken ct = default)
        {
            var prompt = $@"分析以下网页内容，判断页面状态。

【当前URL】
{currentUrl}

【页面HTML片段】
{TruncateHtml(pageHtml, 3000)}

请判断：
1. 页面是否加载完成（是/否）
2. 是否需要登录（是/否）
3. 是否是岗位列表页（是/否）
4. 是否有岗位数据（是/否）
5. 页面类型（登录页/岗位列表/岗位详情/其他）

只返回JSON格式：
{{""loaded"":true,""needLogin"":false,""isJobList"":true,""hasJobs"":true,""pageType"":""岗位列表""}}";

            var response = await CallAIAsync(prompt, ct);
            return ParsePageStatusResponse(response.Content ?? "{}");
        }

        /// <summary>
        /// AI判断岗位是否为开放状态
        /// </summary>
        public async Task<bool> IsJobOpenAsync(
            string jobElementHtml,
            CancellationToken ct = default)
        {
            var prompt = @"分析以下岗位HTML元素，判断该岗位是否处于开放中/招聘中状态。

【岗位HTML】
" + TruncateHtml(jobElementHtml, 1000) + @"

判断标准：
- 如果显示开放中、招聘中、在线、发布中等，返回 YES
- 如果显示已关闭、已下线、暂停、审核中、草稿等，返回 NO
- 如果无法判断，返回 UNKNOWN

只返回 YES、NO 或 UNKNOWN，不要其他内容。";

            var response = await CallAIAsync(prompt, ct);
            var result = response.Content?.Trim().ToUpper() ?? "UNKNOWN";
            return result == "YES";
        }

        /// <summary>
        /// AI生成下一步操作建议
        /// </summary>
        public async Task<AIActionSuggestion> SuggestNextActionAsync(
            string pageHtml,
            string currentUrl,
            string targetAction,
            CancellationToken ct = default)
        {
            var prompt = $@"你是一个模拟人工操作的助手。分析当前页面，建议下一步操作。

【当前URL】
{currentUrl}

【目标操作】
{targetAction}

【页面HTML片段】
{TruncateHtml(pageHtml, 2000)}

请分析页面，返回下一步操作建议，JSON格式：
{{
  ""action"": ""click/scroll/wait/navigate/done"",
  ""selector"": ""CSS选择器（如果需要点击）"",
  ""url"": ""目标URL（如果需要导航）"",
  ""waitMs"": 等待毫秒数,
  ""reason"": ""操作原因""
}}";

            var response = await CallAIAsync(prompt, ct);
            return ParseActionSuggestion(response.Content ?? "{}");
        }

        #region 私有方法

        private async Task<AIResponse> CallAIAsync(string prompt, CancellationToken ct)
        {
            var response = new AIResponse();

            try
            {
                if (string.IsNullOrEmpty(_config.ApiKey))
                {
                    response.ErrorMessage = "未配置AI API Key";
                    return response;
                }

                var request = new
                {
                    model = _config.Model,
                    messages = new[]
                    {
                        new { role = "system", content = "你是一个专业的网页分析助手，擅长从HTML中提取结构化信息。请始终返回JSON格式的结果。" },
                        new { role = "user", content = prompt }
                    },
                    temperature = 0.1f,
                    max_tokens = 2000
                };

                var json = JsonSerializer.Serialize(request);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                _httpClient.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", _config.ApiKey);

                var httpResponse = await _httpClient.PostAsync(
                    $"{_config.BaseUrl.TrimEnd('/')}/chat/completions",
                    content, ct);

                var responseJson = await httpResponse.Content.ReadAsStringAsync(ct);

                if (!httpResponse.IsSuccessStatusCode)
                {
                    response.ErrorMessage = $"AI请求失败: {httpResponse.StatusCode}";
                    return response;
                }

                var result = JsonSerializer.Deserialize<OpenAIChatResponse>(responseJson);
                if (result?.Choices?.Count > 0)
                {
                    response.Success = true;
                    response.Content = result.Choices[0].Message?.Content ?? "";
                }
            }
            catch (Exception ex)
            {
                response.ErrorMessage = ex.Message;
            }

            return response;
        }

        private string BuildJobListPrompt(string pageHtml, RecruitPlatform platform)
        {
            var platformName = platform switch
            {
                RecruitPlatform.Boss => "BOSS直聘",
                RecruitPlatform.Zhilian => "智联招聘",
                RecruitPlatform.Job51 => "前程无忧",
                RecruitPlatform.Liepin => "猎聘",
                _ => "招聘平台"
            };

            return $@"分析以下{platformName}的岗位列表页面HTML，提取所有岗位信息。

【页面HTML】
{TruncateHtml(pageHtml, 8000)}

请提取每个岗位的以下信息：
- platformJobId: 平台岗位ID
- title: 岗位名称
- salaryText: 薪资文本
- location: 工作地点
- experienceRequired: 经验要求
- educationRequired: 学历要求
- pageUrl: 岗位详情页URL
- isOpen: 是否开放中（true/false）
- statusText: 状态文本

只返回JSON数组格式，示例：
[{{""platformJobId"":""123"",""title"":""Java开发"",""salaryText"":""15-25K"",""location"":""北京"",""experienceRequired"":""3-5年"",""educationRequired"":""本科"",""pageUrl"":""https://..."",""isOpen"":true,""statusText"":""开放中""}}]

如果没有找到岗位，返回空数组 []";
        }

        private string BuildJobDetailPrompt(string pageHtml, RecruitPlatform platform)
        {
            return $@"分析以下岗位详情页面HTML，提取详细信息。

【页面HTML】
{TruncateHtml(pageHtml, 6000)}

请提取以下信息：
- description: 岗位描述/职责
- requirements: 任职要求
- address: 详细工作地址
- tags: 技能标签数组
- benefits: 福利待遇数组
- companyName: 公司名称
- companySize: 公司规模
- companyIndustry: 所属行业

返回JSON格式：
{{""description"":""..."",""requirements"":""..."",""address"":""..."",""tags"":[],""benefits"":[],""companyName"":""..."",""companySize"":""..."",""companyIndustry"":""...""}}";
        }

        private List<AIExtractedJob> ParseJobListResponse(string content)
        {
            try
            {
                // 提取JSON部分
                var jsonStart = content.IndexOf('[');
                var jsonEnd = content.LastIndexOf(']');
                if (jsonStart >= 0 && jsonEnd > jsonStart)
                {
                    var json = content.Substring(jsonStart, jsonEnd - jsonStart + 1);
                    return JsonSerializer.Deserialize<List<AIExtractedJob>>(json) 
                           ?? new List<AIExtractedJob>();
                }
            }
            catch { }
            return new List<AIExtractedJob>();
        }

        private AIJobDetail? ParseJobDetailResponse(string content)
        {
            try
            {
                var jsonStart = content.IndexOf('{');
                var jsonEnd = content.LastIndexOf('}');
                if (jsonStart >= 0 && jsonEnd > jsonStart)
                {
                    var json = content.Substring(jsonStart, jsonEnd - jsonStart + 1);
                    return JsonSerializer.Deserialize<AIJobDetail>(json);
                }
            }
            catch { }
            return null;
        }

        private AIPageStatus ParsePageStatusResponse(string content)
        {
            try
            {
                var jsonStart = content.IndexOf('{');
                var jsonEnd = content.LastIndexOf('}');
                if (jsonStart >= 0 && jsonEnd > jsonStart)
                {
                    var json = content.Substring(jsonStart, jsonEnd - jsonStart + 1);
                    return JsonSerializer.Deserialize<AIPageStatus>(json) ?? new AIPageStatus();
                }
            }
            catch { }
            return new AIPageStatus();
        }

        private AIActionSuggestion ParseActionSuggestion(string content)
        {
            try
            {
                var jsonStart = content.IndexOf('{');
                var jsonEnd = content.LastIndexOf('}');
                if (jsonStart >= 0 && jsonEnd > jsonStart)
                {
                    var json = content.Substring(jsonStart, jsonEnd - jsonStart + 1);
                    return JsonSerializer.Deserialize<AIActionSuggestion>(json) 
                           ?? new AIActionSuggestion();
                }
            }
            catch { }
            return new AIActionSuggestion { Action = "wait", WaitMs = 1000 };
        }

        private async Task SimulateHumanReadingDelay(CancellationToken ct)
        {
            // 模拟人工阅读页面的随机延迟（500-1500ms）
            var delay = _random.Next(500, 1500);
            await Task.Delay(delay, ct);
        }

        private string TruncateHtml(string html, int maxLength)
        {
            if (string.IsNullOrEmpty(html))
                return "";

            // 移除script和style标签内容
            html = System.Text.RegularExpressions.Regex.Replace(
                html, @"<script[^>]*>[\s\S]*?</script>", "", 
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            html = System.Text.RegularExpressions.Regex.Replace(
                html, @"<style[^>]*>[\s\S]*?</style>", "",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            if (html.Length > maxLength)
            {
                return html.Substring(0, maxLength) + "...[截断]";
            }
            return html;
        }

        #endregion
    }


    /// <summary>
    /// AI分析器配置
    /// </summary>
    public class AIAnalyzerConfig
    {
        /// <summary>
        /// API Key
        /// </summary>
        public string ApiKey { get; set; } = string.Empty;

        /// <summary>
        /// API基础URL
        /// </summary>
        public string BaseUrl { get; set; } = "https://open.bigmodel.cn/api/paas/v4";

        /// <summary>
        /// 模型名称（智谱GLM-4-Flash免费）
        /// </summary>
        public string Model { get; set; } = "glm-4-flash";

        /// <summary>
        /// 默认配置（使用智谱免费模型）
        /// </summary>
        public static AIAnalyzerConfig Default => new()
        {
            BaseUrl = "https://open.bigmodel.cn/api/paas/v4",
            Model = "glm-4-flash"
        };

        /// <summary>
        /// 通义千问配置
        /// </summary>
        public static AIAnalyzerConfig Qwen => new()
        {
            BaseUrl = "https://dashscope.aliyuncs.com/compatible-mode/v1",
            Model = "qwen-turbo"
        };
    }

    /// <summary>
    /// AI提取的岗位信息
    /// </summary>
    public class AIExtractedJob
    {
        [JsonPropertyName("platformJobId")]
        public string PlatformJobId { get; set; } = string.Empty;

        [JsonPropertyName("title")]
        public string Title { get; set; } = string.Empty;

        [JsonPropertyName("salaryText")]
        public string SalaryText { get; set; } = string.Empty;

        [JsonPropertyName("location")]
        public string Location { get; set; } = string.Empty;

        [JsonPropertyName("experienceRequired")]
        public string ExperienceRequired { get; set; } = string.Empty;

        [JsonPropertyName("educationRequired")]
        public string EducationRequired { get; set; } = string.Empty;

        [JsonPropertyName("pageUrl")]
        public string PageUrl { get; set; } = string.Empty;

        [JsonPropertyName("isOpen")]
        public bool IsOpen { get; set; }

        [JsonPropertyName("statusText")]
        public string StatusText { get; set; } = string.Empty;
    }

    /// <summary>
    /// AI提取的岗位详情
    /// </summary>
    public class AIJobDetail
    {
        [JsonPropertyName("description")]
        public string Description { get; set; } = string.Empty;

        [JsonPropertyName("requirements")]
        public string Requirements { get; set; } = string.Empty;

        [JsonPropertyName("address")]
        public string Address { get; set; } = string.Empty;

        [JsonPropertyName("tags")]
        public List<string> Tags { get; set; } = new();

        [JsonPropertyName("benefits")]
        public List<string> Benefits { get; set; } = new();

        [JsonPropertyName("companyName")]
        public string CompanyName { get; set; } = string.Empty;

        [JsonPropertyName("companySize")]
        public string CompanySize { get; set; } = string.Empty;

        [JsonPropertyName("companyIndustry")]
        public string CompanyIndustry { get; set; } = string.Empty;
    }

    /// <summary>
    /// AI分析的页面状态
    /// </summary>
    public class AIPageStatus
    {
        [JsonPropertyName("loaded")]
        public bool Loaded { get; set; }

        [JsonPropertyName("needLogin")]
        public bool NeedLogin { get; set; }

        [JsonPropertyName("isJobList")]
        public bool IsJobList { get; set; }

        [JsonPropertyName("hasJobs")]
        public bool HasJobs { get; set; }

        [JsonPropertyName("pageType")]
        public string PageType { get; set; } = string.Empty;
    }

    /// <summary>
    /// AI建议的操作
    /// </summary>
    public class AIActionSuggestion
    {
        [JsonPropertyName("action")]
        public string Action { get; set; } = "wait";

        [JsonPropertyName("selector")]
        public string Selector { get; set; } = string.Empty;

        [JsonPropertyName("url")]
        public string Url { get; set; } = string.Empty;

        [JsonPropertyName("waitMs")]
        public int WaitMs { get; set; } = 1000;

        [JsonPropertyName("reason")]
        public string Reason { get; set; } = string.Empty;
    }

    /// <summary>
    /// AI响应
    /// </summary>
    internal class AIResponse
    {
        public bool Success { get; set; }
        public string Content { get; set; } = string.Empty;
        public string ErrorMessage { get; set; } = string.Empty;
    }

    /// <summary>
    /// OpenAI兼容响应格式
    /// </summary>
    internal class OpenAIChatResponse
    {
        [JsonPropertyName("choices")]
        public List<OpenAIChoice>? Choices { get; set; }
    }

    internal class OpenAIChoice
    {
        [JsonPropertyName("message")]
        public OpenAIMessage? Message { get; set; }
    }

    internal class OpenAIMessage
    {
        [JsonPropertyName("content")]
        public string Content { get; set; } = string.Empty;
    }
}
