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
    public class AIVisionAnalyzer
    {
        private readonly HttpClient _httpClient;
        private string _apiKey = "";
        private string _baseUrl = "https://open.bigmodel.cn/api/paas/v4";
        private string _model = "glm-4v-flash";

        public AIVisionAnalyzer()
        {
            _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
        }

        public void UpdateConfig(string apiKey, string baseUrl, string model)
        {
            _apiKey = apiKey;
            if (!string.IsNullOrEmpty(baseUrl)) _baseUrl = baseUrl;
            if (!string.IsNullOrEmpty(model)) _model = model;
        }

        public async Task<JobListScreenAnalysis> AnalyzeJobListScreenAsync(byte[] screenshot, CancellationToken ct = default)
        {
            var result = new JobListScreenAnalysis();
            if (string.IsNullOrEmpty(_apiKey))
            {
                result.ErrorMessage = "未配置 AI API Key";
                return result;
            }

            try
            {
                var base64Image = Convert.ToBase64String(screenshot);
                var prompt = @"你是一个专业的招聘数据提取助手。请分析这张 BOSS 直聘职位管理页面的截图。
提取页面中所有可见的岗位条目，并严格按以下 JSON 格式返回：
{
  ""jobCards"": [
    {
      ""title"": ""岗位名称"",
      ""salaryText"": ""薪资范围"",
      ""location"": ""工作地点"",
      ""experience"": ""经验要求"",
      ""education"": ""学历要求""
    }
  ]
}
注意：
1. 只提取截图里真实显示的岗位。
2. 薪资、地点、经验、学历必须准确。
3. 不要输出任何解释文字，只输出 JSON。";

                var request = new
                {
                    model = _model,
                    messages = new[]
                    {
                        new
                        {
                            role = "user",
                            content = new object[]
                            {
                                new { type = "text", text = prompt },
                                new { type = "image_url", image_url = new { url = $"data:image/png;base64,{base64Image}" } }
                            }
                        }
                    },
                    temperature = 0.1
                };

                var json = JsonSerializer.Serialize(request);
                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
                var response = await _httpClient.PostAsync($"{_baseUrl.TrimEnd('/')}/chat/completions", 
                    new StringContent(json, Encoding.UTF8, "application/json"), ct);

                var responseJson = await response.Content.ReadAsStringAsync(ct);
                if (!response.IsSuccessStatusCode)
                {
                    result.ErrorMessage = $"AI 请求失败: {response.StatusCode}";
                    return result;
                }

                var chatResponse = JsonSerializer.Deserialize<JsonElement>(responseJson);
                var content = chatResponse.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString();
                
                return ParseResponse(content);
            }
            catch (Exception ex)
            {
                result.ErrorMessage = $"视觉分析异常: {ex.Message}";
                return result;
            }
        }

        private JobListScreenAnalysis ParseResponse(string content)
        {
            try
            {
                var start = content.IndexOf('{');
                var end = content.LastIndexOf('}');
                if (start >= 0 && end > start)
                {
                    var json = content.Substring(start, end - start + 1);
                    var analysis = JsonSerializer.Deserialize<JobListScreenAnalysis>(json, 
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    analysis.Success = true;
                    return analysis;
                }
            }
            catch { }
            return new JobListScreenAnalysis { Success = false, ErrorMessage = "无法解析 AI 返回的 JSON" };
        }
    }

    public class JobListScreenAnalysis
    {
        public bool Success { get; set; }
        public string ErrorMessage { get; set; }
        public List<VisualJobCard> JobCards { get; set; } = new();
    }

    public class VisualJobCard
    {
        public string Title { get; set; }
        public string SalaryText { get; set; }
        public string Location { get; set; }
        public string Experience { get; set; }
        public string Education { get; set; }
    }
}
