using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using RecruitAutomation.Core.Models;
using RecruitAutomation.Core.Services;

namespace RecruitAutomation.App.Helpers
{
    /// <summary>
    /// 浏览器帮助类 - 完全隔离 Browser 程序集的引用
    /// 所有方法都使用 NoInlining 确保延迟加载
    /// 【改造】所有方法不抛异常，改为返回状态或设置错误信息
    /// </summary>
    public class BrowserHelper : IDisposable
    {
        private static readonly string LogDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "RecruitAutomation", "logs");
        
        private object? _jobFetchService;
        private JobManagementService? _jobManagementService;
        private JobRepository? _jobRepository;
        private bool _initialized;
        private bool _disposed;
        private string _lastError = string.Empty;

        /// <summary>
        /// 进度事件（使用通用类型避免引用 Browser 程序集）
        /// </summary>
        public event Action<string, string, int>? OnProgress;

        /// <summary>
        /// 完成事件
        /// </summary>
        public event Action<BrowserJobFetchResult>? OnCompleted;

        public bool IsInitialized => _initialized;
        
        /// <summary>
        /// 最后一次错误信息
        /// </summary>
        public string LastError => _lastError;

        public JobManagementService? JobManagementService => _jobManagementService;

        /// <summary>
        /// 初始化浏览器服务（不抛异常版本）
        /// </summary>
        /// <returns>是否初始化成功</returns>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public bool Initialize()
        {
            if (_initialized)
                return true;

            try
            {
                WriteLog("BrowserHelper.Initialize 开始...");
                
                // 确保 CefSharp 已初始化
                App.EnsureCefInitialized();
                
                if (!CefSharp.Cef.IsInitialized)
                {
                    _lastError = "CefSharp 未初始化，请确保程序正确启动";
                    WriteLog($"初始化失败: {_lastError}");
                    return false;
                }

                _jobRepository = new JobRepository();
                _jobManagementService = new JobManagementService(_jobRepository);

                var service = new Browser.JobFetcher.JobFetchService();
                service.OnProgress += (s, e) => 
                {
                    try { OnProgress?.Invoke(e.AccountId, e.Message, e.Percentage); } catch { }
                };
                service.OnCompleted += (s, e) => 
                {
                    try 
                    { 
                        OnCompleted?.Invoke(new BrowserJobFetchResult
                        {
                            AccountId = e.AccountId,
                            Success = e.Success,
                            ErrorMessage = e.ErrorMessage,
                            Jobs = e.Jobs
                        }); 
                    } 
                    catch { }
                };
                _jobFetchService = service;

                _initialized = true;
                _lastError = string.Empty;
                WriteLog("BrowserHelper.Initialize 成功");
                return true;
            }
            catch (Exception ex)
            {
                _lastError = $"浏览器服务初始化失败: {ex.Message}";
                WriteLog($"BrowserHelper.Initialize 异常: {ex}");
                return false;
            }
        }

        /// <summary>
        /// 标记账号为已登录
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public void MarkAccountAsLoggedIn(string accountId, RecruitPlatform platform)
        {
            try
            {
                if (_jobFetchService is Browser.JobFetcher.JobFetchService service)
                {
                    service.MarkAccountAsLoggedIn(accountId, platform);
                }
            }
            catch (Exception ex)
            {
                WriteLog($"MarkAccountAsLoggedIn 异常: {ex.Message}");
            }
        }

        /// <summary>
        /// 获取可用账号列表
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public async Task<List<AvailableAccount>> GetAvailableAccountsAsync()
        {
            try
            {
                if (_jobFetchService is Browser.JobFetcher.JobFetchService service)
                {
                    return await service.GetAvailableAccountsAsync();
                }
            }
            catch (Exception ex)
            {
                WriteLog($"GetAvailableAccountsAsync 异常: {ex.Message}");
            }
            return new List<AvailableAccount>();
        }

        /// <summary>
        /// 从单个账号读取岗位
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public async Task<BrowserJobFetchResult> FetchJobsFromAccountAsync(string accountId, bool useAI, CancellationToken ct)
        {
            try
            {
                if (_jobFetchService is Browser.JobFetcher.JobFetchService service)
                {
                    var result = await service.FetchJobsFromAccountAsync(accountId, useAI, ct);
                    return new BrowserJobFetchResult
                    {
                        AccountId = result.AccountId,
                        Success = result.Success,
                        ErrorMessage = result.ErrorMessage,
                        Jobs = result.Jobs
                    };
                }
            }
            catch (Exception ex)
            {
                WriteLog($"FetchJobsFromAccountAsync 异常: {ex.Message}");
                return new BrowserJobFetchResult { Success = false, ErrorMessage = ex.Message };
            }
            return new BrowserJobFetchResult { Success = false, ErrorMessage = "服务未初始化" };
        }

        /// <summary>
        /// 从多个账号读取岗位
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public async Task<List<BrowserJobFetchResult>> FetchJobsFromMultipleAccountsAsync(
            IEnumerable<string> accountIds, bool useAI, CancellationToken ct)
        {
            try
            {
                if (_jobFetchService is Browser.JobFetcher.JobFetchService service)
                {
                    var results = await service.FetchJobsFromMultipleAccountsAsync(accountIds, useAI, ct);
                    return results.ConvertAll(r => new BrowserJobFetchResult
                    {
                        AccountId = r.AccountId,
                        Success = r.Success,
                        ErrorMessage = r.ErrorMessage,
                        Jobs = r.Jobs
                    });
                }
            }
            catch (Exception ex)
            {
                WriteLog($"FetchJobsFromMultipleAccountsAsync 异常: {ex.Message}");
            }
            return new List<BrowserJobFetchResult>();
        }

        /// <summary>
        /// 取消所有操作
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public void CancelAll()
        {
            try
            {
                if (_jobFetchService is Browser.JobFetcher.JobFetchService service)
                {
                    service.CancelAll();
                }
            }
            catch (Exception ex)
            {
                WriteLog($"CancelAll 异常: {ex.Message}");
            }
        }

        /// <summary>
        /// 关闭浏览器实例
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public void CloseBrowserInstance(string accountId)
        {
            try
            {
                Browser.BrowserInstanceManager.Instance.Close(accountId);
            }
            catch (Exception ex)
            {
                WriteLog($"CloseBrowserInstance 异常: {ex.Message}");
            }
        }

        /// <summary>
        /// 启动浏览器实例
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public Task<bool> StartBrowserInstanceAsync(string accountId, RecruitPlatform platform)
        {
            try
            {
                var startUrl = platform switch
                {
                    RecruitPlatform.Boss => "https://www.zhipin.com/web/geek/job",
                    RecruitPlatform.Zhilian => "https://www.zhaopin.com/",
                    RecruitPlatform.Job51 => "https://www.51job.com/",
                    RecruitPlatform.Liepin => "https://www.liepin.com/",
                    _ => "about:blank"
                };
                
                var instance = Browser.BrowserInstanceManager.Instance.GetOrCreate(accountId, startUrl);
                return Task.FromResult(instance != null);
            }
            catch (Exception ex)
            {
                WriteLog($"StartBrowserInstanceAsync 异常: {ex.Message}");
                return Task.FromResult(false);
            }
        }

        /// <summary>
        /// 检查浏览器实例是否运行中
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public bool IsBrowserRunning(string accountId)
        {
            try
            {
                return Browser.BrowserInstanceManager.Instance.IsRunning(accountId);
            }
            catch (Exception ex)
            {
                WriteLog($"IsBrowserRunning 异常: {ex.Message}");
                return false;
            }
        }
        
        private void WriteLog(string message)
        {
            try
            {
                if (!Directory.Exists(LogDir))
                    Directory.CreateDirectory(LogDir);
                var logFile = Path.Combine(LogDir, "browser_helper.log");
                File.AppendAllText(logFile, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {message}\n");
            }
            catch { }
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
        }
    }

    /// <summary>
    /// 浏览器岗位读取结果（避免直接引用 Browser 程序集类型）
    /// </summary>
    public class BrowserJobFetchResult
    {
        public string AccountId { get; set; } = string.Empty;
        public bool Success { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
        public List<JobPosition> Jobs { get; set; } = new();
    }
}
