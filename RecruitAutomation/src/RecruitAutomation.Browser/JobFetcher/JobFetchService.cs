using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using RecruitAutomation.Core.Models;

namespace RecruitAutomation.Browser.JobFetcher
{
    public class JobFetchService
    {
        private readonly BossJobPageReader _bossReader;
        private readonly ConcurrentDictionary<string, CancellationTokenSource> _runningTasks = new();

        public event EventHandler<JobFetchProgressEventArgs>? OnProgress;
        public event EventHandler<JobFetchResult>? OnCompleted;

        public JobFetchService()
        {
            _bossReader = new BossJobPageReader();
        }

        public void ConfigureAI(string apiKey, string baseUrl = "", string model = "")
        {
            _bossReader.UpdateVisualAIConfig(apiKey, baseUrl, model);
        }

        public async Task<JobFetchResult> FetchJobsFromAccountAsync(string accountId, bool useAI, CancellationToken ct = default)
        {
            var result = new JobFetchResult { AccountId = accountId, Success = false };
            
            try
            {
                ReportProgress(accountId, "正在获取浏览器实例...", 10);
                var instance = BrowserInstanceManager.Instance.Get(accountId);
                if (instance == null)
                {
                    result.ErrorMessage = "未找到运行中的浏览器实例";
                    return result;
                }

                ReportProgress(accountId, "正在通过 AI 图像识别读取岗位...", 30);
                
                // 强制使用视觉模式
                _bossReader.UseVisualMode = true;
                var jobs = await _bossReader.ReadOpenJobsAsync(instance, accountId, ct);

                if (jobs != null && jobs.Count > 0)
                {
                    result.Success = true;
                    result.Jobs = jobs;
                    ReportProgress(accountId, $"成功识别到 {jobs.Count} 个岗位", 100);
                }
                else
                {
                    result.ErrorMessage = "未识别到任何岗位，请检查页面是否正确加载";
                    ReportProgress(accountId, result.ErrorMessage, 100);
                }
            }
            catch (Exception ex)
            {
                result.ErrorMessage = $"读取失败: {ex.Message}";
                ReportProgress(accountId, result.ErrorMessage, 100);
            }

            OnCompleted?.Invoke(this, result);
            return result;
        }

        private void ReportProgress(string accountId, string message, int percentage)
        {
            OnProgress?.Invoke(this, new JobFetchProgressEventArgs 
            { 
                AccountId = accountId, 
                Message = message, 
                Percentage = percentage 
            });
        }
    }

    public class JobFetchProgressEventArgs : EventArgs
    {
        public string AccountId { get; set; }
        public string Message { get; set; }
        public int Percentage { get; set; }
    }

    public class JobFetchResult : EventArgs
    {
        public string AccountId { get; set; }
        public bool Success { get; set; }
        public string ErrorMessage { get; set; }
        public List<JobPosition> Jobs { get; set; } = new();
    }
}
