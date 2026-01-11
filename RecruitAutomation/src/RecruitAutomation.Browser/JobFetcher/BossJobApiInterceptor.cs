using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using CefSharp;
using CefSharp.Handler;

namespace RecruitAutomation.Browser.JobFetcher
{
    /// <summary>
    /// Boss直聘岗位接口拦截器
    /// 通过拦截网络请求获取岗位JSON数据，而不是从DOM读取
    /// </summary>
    public class BossJobApiInterceptor : IDisposable
    {
        // 存储拦截到的岗位数据（accountId -> jobs）
        private readonly ConcurrentDictionary<string, List<BossJobApiData>> _interceptedJobs = new();
        
        // 等待数据的信号（accountId -> TaskCompletionSource）
        private readonly ConcurrentDictionary<string, TaskCompletionSource<List<BossJobApiData>>> _waitHandles = new();
        
        // 日志路径
        private static readonly string LogPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "RecruitAutomation", "logs", "job_interceptor.log");

        public BossJobApiInterceptor()
        {
            EnsureLogDirectory();
        }

        /// <summary>
        /// 开始监听指定账号的岗位接口
        /// </summary>
        public void StartListening(string accountId)
        {
            _interceptedJobs[accountId] = new List<BossJobApiData>();
            _waitHandles[accountId] = new TaskCompletionSource<List<BossJobApiData>>();
            Log($"[{accountId}] 开始监听岗位接口");
        }

        /// <summary>
        /// 停止监听
        /// </summary>
        public void StopListening(string accountId)
        {
            _interceptedJobs.TryRemove(accountId, out _);
            if (_waitHandles.TryRemove(accountId, out var tcs))
            {
                tcs.TrySetCanceled();
            }
            Log($"[{accountId}] 停止监听岗位接口");
        }

        /// <summary>
        /// 处理拦截到的响应数据
        /// </summary>
        public void OnResponseReceived(string accountId, string url, string responseBody)
        {
            if (string.IsNullOrEmpty(responseBody))
                return;

            // 检查是否是岗位相关接口
            if (!IsJobApiUrl(url))
                return;

            Log($"[{accountId}] 拦截到岗位接口: {url}");
            Log($"[{accountId}] 响应长度: {responseBody.Length}");

            try
            {
                var jobs = ParseJobResponse(responseBody);
                if (jobs.Count > 0)
                {
                    Log($"[{accountId}] 解析到 {jobs.Count} 个岗位");
                    
                    if (_interceptedJobs.TryGetValue(accountId, out var list))
                    {
                        list.AddRange(jobs);
                    }

                    // 通知等待者
                    if (_waitHandles.TryGetValue(accountId, out var tcs))
                    {
                        tcs.TrySetResult(jobs);
                    }
                }
            }
            catch (Exception ex)
            {
                Log($"[{accountId}] 解析岗位数据失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 等待岗位数据（带超时）
        /// </summary>
        public async Task<List<BossJobApiData>> WaitForJobsAsync(string accountId, int timeoutMs = 15000, CancellationToken ct = default)
        {
            if (!_waitHandles.TryGetValue(accountId, out var tcs))
            {
                StartListening(accountId);
                tcs = _waitHandles[accountId];
            }

            try
            {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                cts.CancelAfter(timeoutMs);

                var completedTask = await Task.WhenAny(tcs.Task, Task.Delay(timeoutMs, cts.Token));
                
                if (completedTask == tcs.Task)
                {
                    return await tcs.Task;
                }
            }
            catch (OperationCanceledException)
            {
                Log($"[{accountId}] 等待岗位数据超时");
            }

            // 超时后返回已拦截的数据
            if (_interceptedJobs.TryGetValue(accountId, out var jobs))
            {
                return jobs;
            }

            return new List<BossJobApiData>();
        }

        /// <summary>
        /// 获取已拦截的岗位数据
        /// </summary>
        public List<BossJobApiData> GetInterceptedJobs(string accountId)
        {
            if (_interceptedJobs.TryGetValue(accountId, out var jobs))
            {
                return new List<BossJobApiData>(jobs);
            }
            return new List<BossJobApiData>();
        }

        /// <summary>
        /// 清空已拦截的数据
        /// </summary>
        public void ClearInterceptedJobs(string accountId)
        {
            if (_interceptedJobs.TryGetValue(accountId, out var jobs))
            {
                jobs.Clear();
            }
            // 重置等待句柄
            _waitHandles[accountId] = new TaskCompletionSource<List<BossJobApiData>>();
        }

        /// <summary>
        /// 判断是否是岗位相关接口（精确匹配岗位列表接口）
        /// </summary>
        private bool IsJobApiUrl(string url)
        {
            if (string.IsNullOrEmpty(url))
                return false;

            // Boss直聘岗位列表接口特征（精确匹配）
            var isJobListApi = url.Contains("/wapi/zpboss/job/list") ||
                               url.Contains("/wapi/zpgeek/job/list") ||
                               url.Contains("/job/list") ||
                               url.Contains("/job/manage") ||
                               (url.Contains("job") && url.Contains("list") && !url.Contains("stat"));

            // 排除统计/初始化接口
            var isExcluded = url.Contains("/stat") ||
                             url.Contains("/count") ||
                             url.Contains("/summary") ||
                             url.Contains("/init") ||
                             url.Contains("/config");

            return isJobListApi && !isExcluded;
        }

        /// <summary>
        /// 解析岗位响应JSON
        /// </summary>
        private List<BossJobApiData> ParseJobResponse(string json)
        {
            var jobs = new List<BossJobApiData>();

            try
            {
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                // 尝试多种JSON结构
                JsonElement? jobArray = null;

                // 结构1: { "zpData": { "jobList": [...] } }
                if (root.TryGetProperty("zpData", out var zpData))
                {
                    if (zpData.TryGetProperty("jobList", out var list))
                        jobArray = list;
                    else if (zpData.TryGetProperty("list", out var list2))
                        jobArray = list2;
                    else if (zpData.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Array)
                        jobArray = data;
                }
                // 结构2: { "data": { "list": [...] } }
                else if (root.TryGetProperty("data", out var data))
                {
                    if (data.TryGetProperty("list", out var list))
                        jobArray = list;
                    else if (data.TryGetProperty("jobList", out var jobList))
                        jobArray = jobList;
                    else if (data.ValueKind == JsonValueKind.Array)
                        jobArray = data;
                }
                // 结构3: { "list": [...] }
                else if (root.TryGetProperty("list", out var list))
                {
                    jobArray = list;
                }
                // 结构4: 直接是数组
                else if (root.ValueKind == JsonValueKind.Array)
                {
                    jobArray = root;
                }

                if (jobArray.HasValue && jobArray.Value.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in jobArray.Value.EnumerateArray())
                    {
                        var job = ParseJobItem(item);
                        if (job != null && !string.IsNullOrEmpty(job.JobName))
                        {
                            jobs.Add(job);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log($"解析JSON失败: {ex.Message}");
            }

            return jobs;
        }

        /// <summary>
        /// 解析单个岗位项
        /// </summary>
        private BossJobApiData? ParseJobItem(JsonElement item)
        {
            try
            {
                var job = new BossJobApiData();

                // 岗位ID
                job.JobId = GetStringValue(item, "encryptJobId", "jobId", "id", "encryptId");
                
                // 岗位名称
                job.JobName = GetStringValue(item, "jobName", "positionName", "title", "name");
                
                // 薪资
                job.Salary = GetStringValue(item, "salaryDesc", "salary", "salaryRange");
                
                // 城市
                job.City = GetStringValue(item, "cityName", "city", "locationName", "areaDistrict");
                
                // 经验要求
                job.Experience = GetStringValue(item, "jobExperience", "experience", "workYear", "experienceName");
                
                // 学历要求
                job.Degree = GetStringValue(item, "jobDegree", "degree", "education", "degreeName");
                
                // 状态
                var status = GetStringValue(item, "jobStatus", "status", "jobStatusDesc");
                job.Status = status;
                job.IsOpen = string.IsNullOrEmpty(status) || 
                             status.Contains("开放") || 
                             status.Contains("在线") ||
                             status.Contains("招聘中") ||
                             status == "OPEN" ||
                             status == "1";

                // 如果状态明确是关闭的
                if (status != null && (status.Contains("关闭") || status.Contains("下线") || status == "CLOSE" || status == "0"))
                {
                    job.IsOpen = false;
                }

                // 公司名称
                job.CompanyName = GetStringValue(item, "brandName", "companyName", "company");
                
                // 岗位标签
                if (item.TryGetProperty("jobLabels", out var labels) && labels.ValueKind == JsonValueKind.Array)
                {
                    foreach (var label in labels.EnumerateArray())
                    {
                        if (label.ValueKind == JsonValueKind.String)
                        {
                            job.Labels.Add(label.GetString() ?? "");
                        }
                    }
                }

                // 岗位链接
                if (!string.IsNullOrEmpty(job.JobId))
                {
                    job.PageUrl = $"https://www.zhipin.com/job_detail/{job.JobId}.html";
                }

                return job;
            }
            catch
            {
                return null;
            }
        }

        private string GetStringValue(JsonElement element, params string[] keys)
        {
            foreach (var key in keys)
            {
                if (element.TryGetProperty(key, out var value))
                {
                    if (value.ValueKind == JsonValueKind.String)
                        return value.GetString() ?? "";
                    if (value.ValueKind == JsonValueKind.Number)
                        return value.ToString();
                }
            }
            return "";
        }

        private void Log(string message)
        {
            try
            {
                File.AppendAllText(LogPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}\n");
            }
            catch { }
        }

        private void EnsureLogDirectory()
        {
            try
            {
                var dir = Path.GetDirectoryName(LogPath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);
            }
            catch { }
        }

        public void Dispose()
        {
            _interceptedJobs.Clear();
            foreach (var tcs in _waitHandles.Values)
            {
                tcs.TrySetCanceled();
            }
            _waitHandles.Clear();
        }
    }

    /// <summary>
    /// Boss岗位接口数据模型
    /// </summary>
    public class BossJobApiData
    {
        public string JobId { get; set; } = "";
        public string JobName { get; set; } = "";
        public string Salary { get; set; } = "";
        public string City { get; set; } = "";
        public string Experience { get; set; } = "";
        public string Degree { get; set; } = "";
        public string Status { get; set; } = "";
        public bool IsOpen { get; set; } = true;
        public string CompanyName { get; set; } = "";
        public string PageUrl { get; set; } = "";
        public List<string> Labels { get; set; } = new();
    }
}
