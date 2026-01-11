using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace RecruitAutomation.Browser.JobFetcher
{
    /// <summary>
    /// AI 视觉分析器
    /// 通过截图 + AI 视觉模型识别页面内容
    /// 支持：智谱GLM-4V、通义千问VL、DeepSeek-VL 等视觉模型
    /// </summary>
    public class AIVisionAnalyzer
    {
        private readonly HttpClient _httpClient;
        private string _apiKey = "";
        private string _baseUrl = "https://open.bigmodel.cn/api/paas/v4";
        private string _model = "glm-4v-flash";  // 智谱免费视觉模型

        public AIVisionAnalyzer()
        {
            _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
        }

        /// <summary>
        /// 更新配置
        /// </summary>
        public void UpdateConfig(string apiKey, string baseUrl, string model)
        {
            _apiKey = apiKey;
            if (!string.IsNullOrEmpty(baseUrl)) _baseUrl = baseUrl;
            if (!string.IsNullOrEmpty(model)) _model = model;
        }

        /// <summary>
        /// 分析岗位列表页面截图
        /// </summary>
        public async Task<JobListScreenAnalysis> AnalyzeJobListScreenAsync(byte[] screenshot, CancellationToken ct = default)
        {
            var result = new JobListScreenAnalysis();

            try
            {
                if (string.IsNullOrEmpty(_apiKey))
                {
                    result.ErrorMessage = "未配置AI API Key";
                    return result;
                }

                var base64Image = Convert.ToBase64String(screenshot);
                var prompt = BuildJobListVisionPrompt();

                var response = await CallVisionAIAsync(prompt, base64Image, ct);

                if (!response.Success)
                {
                    result.ErrorMessage = response.ErrorMessage;
                    return result;
                }

                // 解析AI返回的JSON
                result = ParseJobListResponse(response.Content);
                result.Success = true;
            }
            catch (Exception ex)
            {
                result.ErrorMessage = ex.Message;
            }

            return result;
        }

        /// <summary>
        /// 分析岗位详情页面截图
        /// </summary>
        public async Task<JobDetailScreenAnalysis?> AnalyzeJobDetailScreenAsync(byte[] screenshot, CancellationToken ct = default)
        {
            try
            {
                if (string.IsNullOrEmpty(_apiKey))
                    return null;

                var base64Image = Convert.ToBase64String(screenshot);
                var prompt = BuildJobDetailVisionPrompt();

                var response = await CallVisionAIAsync(prompt, base64Image, ct);

                if (!response.Success || string.IsNullOrEmpty(response.Content))
                    return null;

                return ParseJobDetailResponse(response.Content);
            }
            catch
            {
                return null;
            }
        }

        #region AI 调用

        private async Task<VisionAIResponse> CallVisionAIAsync(string prompt, string base64Image, CancellationToken ct)
        {
            var response = new VisionAIResponse();

            try
            {
                // 构建视觉模型请求
                var request = new
                {
                    model = _model,
                    messages = new object[]
                    {
                        new
                        {
                            role = "user",
                            content = new object[]
                            {
                                new { type = "text", text = prompt },
                                new
                                {
                                    type = "image_url",
                                    image_url = new { url = $"data:image/png;base64,{base64Image}" }
                                }
                            }
                        }
                    },
                    temperature = 0.1f,
                    max_tokens = 4000
                };

                var json = JsonSerializer.Serialize(request);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);

                var httpResponse = await _httpClient.PostAsync(
                    $"{_baseUrl.TrimEnd('/')}/chat/completions", content, ct);

                var responseJson = await httpResponse.Content.ReadAsStringAsync(ct);

                if (!httpResponse.IsSuccessStatusCode)
                {
                    response.ErrorMessage = $"AI请求失败: {httpResponse.StatusCode} - {responseJson}";
                    return response;
                }

                var result = JsonSerializer.Deserialize<VisionOpenAIChatResponse>(responseJson);
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

        #endregion

        #region Prompt 构建

        private string BuildJobListVisionPrompt()
        {
            return @"你是一个专业的招聘页面分析助手。请仔细观察这张BOSS直聘职位管理页面的截图。

【任务】
1. 识别页面中所有可见的岗位卡片
2. 对每个岗位卡片，提取以下信息：
   - 岗位名称
   - 薪资范围
   - 工作城市
   - 经验要求
   - 学历要求
   - 当前状态（开放中/已关闭/暂停等）
   - 卡片在屏幕中的大致位置（中心点坐标，假设屏幕宽1920高1080）

3. 判断页面状态：
   - 是否需要登录
   - 是否是岗位列表页
   - 是否有加载中的提示

【输出格式】
请严格按以下JSON格式返回，不要添加任何其他文字：
{
  ""needLogin"": false,
  ""isJobListPage"": true,
  ""isLoading"": false,
  ""jobCards"": [
    {
      ""title"": ""Java开发工程师"",
      ""salaryText"": ""15-25K·13薪"",
      ""location"": ""北京"",
      ""experience"": ""3-5年"",
      ""education"": ""本科"",
      ""statusText"": ""开放中"",
      ""centerX"": 500,
      ""centerY"": 300
    }
  ]
}

【注意】
- 只识别屏幕中可见的岗位
- 如果看不清某个字段，填空字符串
- 坐标是相对于屏幕左上角的像素位置
- 如果页面显示需要登录，needLogin设为true
- 如果没有看到任何岗位卡片，jobCards返回空数组";
        }

        private string BuildJobDetailVisionPrompt()
        {
            return @"你是一个专业的招聘页面分析助手。请仔细观察这张BOSS直聘岗位详情页面的截图。

【任务】
提取岗位的详细信息：
- 岗位描述/职责
- 任职要求
- 工作地点
- 经验要求
- 学历要求
- 技能标签
- 福利待遇
- 公司名称
- 公司规模
- 所属行业

【输出格式】
请严格按以下JSON格式返回：
{
  ""description"": ""岗位描述内容..."",
  ""requirements"": ""任职要求内容..."",
  ""location"": ""北京·朝阳区"",
  ""experience"": ""3-5年"",
  ""education"": ""本科"",
  ""tags"": [""Java"", ""Spring"", ""MySQL""],
  ""benefits"": [""五险一金"", ""年终奖""],
  ""companyName"": ""XX科技有限公司"",
  ""companySize"": ""100-499人"",
  ""industry"": ""互联网/IT""
}

【注意】
- 如果某个字段在页面中看不到，填空字符串或空数组
- 尽量完整提取岗位描述和任职要求的文字内容";
        }

        #endregion

        #region 响应解析

        private JobListScreenAnalysis ParseJobListResponse(string content)
        {
            var result = new JobListScreenAnalysis();

            try
            {
                // 提取JSON部分
                var jsonStart = content.IndexOf('{');
                var jsonEnd = content.LastIndexOf('}');
                if (jsonStart >= 0 && jsonEnd > jsonStart)
                {
                    var json = content.Substring(jsonStart, jsonEnd - jsonStart + 1);
                    var parsed = JsonSerializer.Deserialize<JobListScreenAnalysis>(json,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                    if (parsed != null)
                    {
                        result = parsed;
                        result.Success = true;
                    }
                }
            }
            catch (Exception ex)
            {
                result.ErrorMessage = $"解析响应失败: {ex.Message}";
            }

            return result;
        }

        private JobDetailScreenAnalysis? ParseJobDetailResponse(string content)
        {
            try
            {
                var jsonStart = content.IndexOf('{');
                var jsonEnd = content.LastIndexOf('}');
                if (jsonStart >= 0 && jsonEnd > jsonStart)
                {
                    var json = content.Substring(jsonStart, jsonEnd - jsonStart + 1);
                    return JsonSerializer.Deserialize<JobDetailScreenAnalysis>(json,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                }
            }
            catch { }
            return null;
        }

        #endregion
    }

    #region 数据模型

    /// <summary>
    /// 视觉AI响应
    /// </summary>
    internal class VisionAIResponse
    {
        public bool Success { get; set; }
        public string Content { get; set; } = "";
        public string ErrorMessage { get; set; } = "";
    }

    /// <summary>
    /// 岗位列表页面分析结果
    /// </summary>
    public class JobListScreenAnalysis
    {
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }

        [JsonPropertyName("needLogin")]
        public bool NeedLogin { get; set; }

        [JsonPropertyName("isJobListPage")]
        public bool IsJobListPage { get; set; }

        [JsonPropertyName("isLoading")]
        public bool IsLoading { get; set; }

        [JsonPropertyName("jobCards")]
        public List<VisualJobCard> JobCards { get; set; } = new();
    }

    /// <summary>
    /// 视觉识别的岗位卡片
    /// </summary>
    public class VisualJobCard
    {
        [JsonPropertyName("title")]
        public string Title { get; set; } = "";

        [JsonPropertyName("salaryText")]
        public string SalaryText { get; set; } = "";

        [JsonPropertyName("location")]
        public string? Location { get; set; }

        [JsonPropertyName("experience")]
        public string? Experience { get; set; }

        [JsonPropertyName("education")]
        public string? Education { get; set; }

        [JsonPropertyName("statusText")]
        public string? StatusText { get; set; }

        [JsonPropertyName("centerX")]
        public int CenterX { get; set; }

        [JsonPropertyName("centerY")]
        public int CenterY { get; set; }
    }

    /// <summary>
    /// 岗位详情页面分析结果
    /// </summary>
    public class JobDetailScreenAnalysis
    {
        [JsonPropertyName("description")]
        public string Description { get; set; } = "";

        [JsonPropertyName("requirements")]
        public string Requirements { get; set; } = "";

        [JsonPropertyName("location")]
        public string Location { get; set; } = "";

        [JsonPropertyName("experience")]
        public string Experience { get; set; } = "";

        [JsonPropertyName("education")]
        public string Education { get; set; } = "";

        [JsonPropertyName("tags")]
        public List<string> Tags { get; set; } = new();

        [JsonPropertyName("benefits")]
        public List<string> Benefits { get; set; } = new();

        [JsonPropertyName("companyName")]
        public string CompanyName { get; set; } = "";

        [JsonPropertyName("companySize")]
        public string CompanySize { get; set; } = "";

        [JsonPropertyName("industry")]
        public string Industry { get; set; } = "";
    }

    /// <summary>
    /// 视觉浏览结果
    /// </summary>
    public class VisualBrowseResult
    {
        public string AccountId { get; set; } = "";
        public BrowseStatus Status { get; set; } = BrowseStatus.Unknown;
        public string? Reason { get; set; }
        public string? Suggest { get; set; }
        public int TotalJobsFound { get; set; }
        public int OpenJobsCount { get; set; }
        public List<RecruitAutomation.Core.Models.JobPosition> Jobs { get; set; } = new();

        // 调试诊断信息
        public string Stage { get; set; } = "";
        public string? PageType { get; set; }
        public string? CurrentUrl { get; set; }
        public DiagnosticInfo? DiagnosticInfo { get; set; }
    }

    /// <summary>
    /// 诊断信息
    /// </summary>
    public class DiagnosticInfo
    {
        public string PageDiagnosis { get; set; } = "";
        public int VisibleJobCount { get; set; }
        public int AIDetectedCount { get; set; }
        public string? ScreenshotPath { get; set; }
        public string? PageTextSample { get; set; }
    }

    /// <summary>
    /// 浏览状态
    /// </summary>
    public enum BrowseStatus
    {
        Unknown,
        Success,
        PartialSuccess,
        NeedLogin,
        Blocked,
        Error,
        Cancelled
    }

    /// <summary>
    /// OpenAI兼容响应格式（视觉模型专用）
    /// </summary>
    internal class VisionOpenAIChatResponse
    {
        [JsonPropertyName("choices")]
        public List<VisionOpenAIChoice>? Choices { get; set; }
    }

    internal class VisionOpenAIChoice
    {
        [JsonPropertyName("message")]
        public VisionOpenAIMessage? Message { get; set; }
    }

    internal class VisionOpenAIMessage
    {
        [JsonPropertyName("content")]
        public string Content { get; set; } = "";
    }

    #endregion
}
