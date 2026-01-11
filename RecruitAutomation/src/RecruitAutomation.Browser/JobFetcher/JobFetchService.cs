using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using RecruitAutomation.Browser.HumanBehavior;
using RecruitAutomation.Core.Models;

namespace RecruitAutomation.Browser.JobFetcher
{
    /// <summary>
    /// 岗位读取服务
    /// 核心职责：从已启动且已登录的浏览器实例中读取岗位数据
    /// 支持AI模式：使用国内免费AI（智谱GLM-4-Flash）模拟人工操作，防止封号
    /// </summary>
    public class JobFetchService
    {
        private readonly Dictionary<RecruitPlatform, IJobPageReader> _readers;
        private readonly HumanBehaviorSimulator _humanBehavior;
        private readonly AIJobPageAnalyzer _aiAnalyzer;
        private readonly ConcurrentDictionary<string, CancellationTokenSource> _runningTasks;
        
        // 账号登录状态缓存（accountId -> 登录状态）
        private readonly ConcurrentDictionary<string, AccountLoginInfo> _loginStatusCache;

        /// <summary>
        /// AI配置
        /// </summary>
        public AIAnalyzerConfig AIConfig { get; private set; } = AIAnalyzerConfig.Default;

        /// <summary>
        /// 是否启用AI模式（默认启用）
        /// </summary>
        public bool UseAIMode { get; set; } = true;

        /// <summary>
        /// 是否启用调试模式
        /// </summary>
        public bool DebugMode { get; set; } = false;

        /// <summary>
        /// 调试模式下是否暂停等待手动确认
        /// </summary>
        public bool PauseForManualObservation { get; set; } = false;

        /// <summary>
        /// 岗位读取进度事件
        /// </summary>
        public event EventHandler<JobFetchProgressEventArgs>? OnProgress;

        /// <summary>
        /// 岗位读取完成事件
        /// </summary>
        public event EventHandler<JobFetchResult>? OnCompleted;

        public JobFetchService()
        {
            _humanBehavior = new HumanBehaviorSimulator();
            _aiAnalyzer = new AIJobPageAnalyzer();
            _runningTasks = new ConcurrentDictionary<string, CancellationTokenSource>();
            _loginStatusCache = new ConcurrentDictionary<string, AccountLoginInfo>();

            // 注册各平台读取器（支持AI模式）
            _readers = new Dictionary<RecruitPlatform, IJobPageReader>
            {
                { RecruitPlatform.Boss, new BossJobPageReader(_humanBehavior, _aiAnalyzer) },
                { RecruitPlatform.Zhilian, new ZhilianJobPageReader(_humanBehavior, _aiAnalyzer) }
            };
        }

        /// <summary>
        /// 配置AI（使用国内免费AI）
        /// </summary>
        /// <param name="apiKey">API Key</param>
        /// <param name="provider">AI提供商（默认智谱GLM-4V-Flash免费视觉版）</param>
        public void ConfigureAI(string apiKey, AIProviderType provider = AIProviderType.Zhipu)
        {
            AIConfig = provider switch
            {
                AIProviderType.Qwen => new AIAnalyzerConfig
                {
                    ApiKey = apiKey,
                    BaseUrl = "https://dashscope.aliyuncs.com/compatible-mode/v1",
                    Model = "qwen-vl-plus"  // 通义千问视觉模型
                },
                AIProviderType.Kimi => new AIAnalyzerConfig
                {
                    ApiKey = apiKey,
                    BaseUrl = "https://api.moonshot.cn/v1",
                    Model = "moonshot-v1-8k"
                },
                _ => new AIAnalyzerConfig
                {
                    ApiKey = apiKey,
                    BaseUrl = "https://open.bigmodel.cn/api/paas/v4",
                    Model = "glm-4v-flash" // 智谱免费视觉模型
                }
            };

            _aiAnalyzer.UpdateConfig(AIConfig);

            // 更新所有读取器的AI配置（包括视觉模式）
            foreach (var reader in _readers.Values)
            {
                if (reader is AIJobPageReaderBase aiReader)
                {
                    aiReader.UpdateAIConfig(AIConfig);
                }
                
                // 更新视觉模式配置
                if (reader is BossJobPageReader bossReader)
                {
                    bossReader.UpdateVisualAIConfig(apiKey, AIConfig.BaseUrl, AIConfig.Model);
                    bossReader.UseVisualMode = UseAIMode;
                    bossReader.DebugMode = DebugMode;
                    bossReader.PauseForManualObservation = PauseForManualObservation;
                }
            }
        }

        /// <summary>
        /// AI提供商类型
        /// </summary>
        public enum AIProviderType
        {
            /// <summary>智谱GLM（推荐，有免费额度）</summary>
            Zhipu,
            /// <summary>通义千问</summary>
            Qwen,
            /// <summary>Kimi</summary>
            Kimi
        }

        /// <summary>
        /// 获取可用于岗位读取的账号列表
        /// 唯一数据源：BrowserInstanceManager 中真实运行的浏览器实例
        /// </summary>
        public async Task<List<AvailableAccount>> GetAvailableAccountsAsync(
            RecruitPlatform? platform = null)
        {
            var accounts = new List<AvailableAccount>();
            var manager = BrowserInstanceManager.Instance;

            // 从 BrowserInstanceManager 获取所有运行中的账号
            foreach (var accountId in manager.RunningAccountIds)
            {
                var instance = manager.Get(accountId);
                if (instance == null || !instance.IsInitialized)
                    continue;

                // 检测平台（从当前URL判断）
                var detectedPlatform = DetectPlatform(instance.CurrentUrl);
                if (platform.HasValue && detectedPlatform != platform.Value)
                    continue;

                // 检查登录状态（实时检测）
                var loginStatus = await CheckLoginStatusAsync(instance, detectedPlatform);
                
                // 更新缓存
                _loginStatusCache[accountId] = new AccountLoginInfo
                {
                    AccountId = accountId,
                    Platform = detectedPlatform,
                    LoginStatus = loginStatus,
                    LastChecked = DateTime.UtcNow
                };

                accounts.Add(new AvailableAccount
                {
                    AccountId = accountId,
                    Platform = detectedPlatform,
                    IsStarted = true, // 从 BrowserInstanceManager 获取的一定是已启动的
                    LoginStatus = loginStatus,
                    DisplayName = accountId
                });
            }

            return accounts;
        }

        /// <summary>
        /// 标记账号为已登录状态
        /// </summary>
        public void MarkAccountAsLoggedIn(string accountId, RecruitPlatform platform)
        {
            _loginStatusCache[accountId] = new AccountLoginInfo
            {
                AccountId = accountId,
                Platform = platform,
                LoginStatus = AccountLoginStatus.LoggedIn,
                LastChecked = DateTime.UtcNow
            };
        }

        /// <summary>
        /// 从单个账号读取岗位
        /// 【核心要求】必须使用已登录的浏览器实例，禁止新建
        /// </summary>
        public async Task<JobFetchResult> FetchJobsFromAccountAsync(
            string accountId,
            bool fetchDetails = true,
            CancellationToken ct = default)
        {
            var sw = Stopwatch.StartNew();
            var result = new JobFetchResult { AccountId = accountId };

            try
            {
                // 【关键日志】记录开始读取
                Debug.WriteLine($"[JobFetchService] ========== 开始读取岗位 ==========");
                Debug.WriteLine($"[JobFetchService] 账号ID: {accountId}");
                
                // 1. 验证账号状态 - 必须从 BrowserInstanceManager 获取
                var instance = BrowserInstanceManager.Instance.Get(accountId);
                
                Debug.WriteLine($"[JobFetchService] 浏览器实例: {(instance == null ? "NULL" : "存在")}");
                
                if (instance == null)
                {
                    Debug.WriteLine($"[JobFetchService] 错误: 浏览器实例不存在");
                    return JobFetchResult.Fail(
                        "账号浏览器实例未启动，请先添加账号并完成登录", 
                        JobFetchErrorType.AccountNotStarted);
                }

                Debug.WriteLine($"[JobFetchService] 浏览器是否初始化: {instance.IsInitialized}");
                Debug.WriteLine($"[JobFetchService] 当前URL: {instance.CurrentUrl}");
                
                if (!instance.IsInitialized)
                {
                    Debug.WriteLine($"[JobFetchService] 错误: 浏览器实例未初始化");
                    return JobFetchResult.Fail(
                        "账号浏览器实例未初始化", 
                        JobFetchErrorType.AccountNotStarted);
                }

                // 【关键检查】验证浏览器是否已被释放
                if (instance.Browser == null)
                {
                    Debug.WriteLine($"[JobFetchService] 错误: 浏览器控件为空（可能已被释放）");
                    return JobFetchResult.Fail(
                        "浏览器实例已失效，请重新登录账号", 
                        JobFetchErrorType.AccountNotStarted);
                }

                // 2. 检测平台
                var platform = DetectPlatform(instance.CurrentUrl);
                result.Platform = platform;
                Debug.WriteLine($"[JobFetchService] 检测到平台: {platform}");

                if (!_readers.TryGetValue(platform, out var reader))
                {
                    return JobFetchResult.Fail($"暂不支持平台: {platform}");
                }

                // 3. 检查登录状态
                ReportProgress(accountId, "正在检查登录状态...", 10);
                var loginStatus = await reader.CheckLoginStatusAsync(instance, ct);
                Debug.WriteLine($"[JobFetchService] 登录状态: {loginStatus}");

                if (loginStatus == AccountLoginStatus.NotLoggedIn)
                {
                    return JobFetchResult.Fail(
                        "账号未登录，请先在浏览器中完成登录",
                        JobFetchErrorType.LoginExpired);
                }

                if (loginStatus == AccountLoginStatus.Expired)
                {
                    return JobFetchResult.Fail(
                        "账号登录已过期，请重新登录",
                        JobFetchErrorType.LoginExpired);
                }

                if (loginStatus == AccountLoginStatus.Unknown)
                {
                    // 尝试继续，可能页面还在加载
                    ReportProgress(accountId, "登录状态未知，尝试继续...", 15);
                }

                // 4. 导航到岗位管理页面
                ReportProgress(accountId, "正在进入职位管理页面...", 20);
                
                // AI模式下会模拟人工操作
                if (UseAIMode)
                {
                    ReportProgress(accountId, "AI模式：模拟人工操作进入页面...", 22);
                }
                
                var navigated = await reader.NavigateToJobListAsync(instance, ct);

                if (!navigated)
                {
                    return JobFetchResult.Fail(
                        "无法进入职位管理页面，请检查网络连接",
                        JobFetchErrorType.PageLoadFailed);
                }

                // 5. 读取岗位列表（仅开放中状态）
                ReportProgress(accountId, UseAIMode ? "AI视觉模式：截图+识别+模拟人工操作..." : "正在读取开放中的岗位...", 40);
                
                List<JobPosition> jobs;
                
                // 使用视觉模式时，获取详细状态
                if (UseAIMode && reader is BossJobPageReader bossReader)
                {
                    var browseResult = await bossReader.BrowseJobsWithStatusAsync(instance, accountId, ct);
                    jobs = browseResult.Jobs;
                    
                    // 根据视觉采集状态返回详细信息
                    if (browseResult.Status == BrowseStatus.Blocked)
                    {
                        result.Success = false;
                        result.ErrorType = JobFetchErrorType.PageLoadFailed;
                        result.ErrorMessage = browseResult.Reason ?? "页面可见但程序读取被反爬";
                        result.Jobs = new List<JobPosition>();
                        result.ElapsedMs = sw.ElapsedMilliseconds;
                        
                        // 返回详细状态供UI显示
                        ReportProgress(accountId, $"⚠️ {browseResult.Reason} | 建议: {browseResult.Suggest}", 100);
                        return result;
                    }
                    
                    if (browseResult.Status == BrowseStatus.NeedLogin)
                    {
                        return JobFetchResult.Fail(
                            "账号未登录，请先在浏览器中完成登录",
                            JobFetchErrorType.LoginExpired);
                    }
                    
                    ReportProgress(accountId, $"视觉识别完成: 发现{browseResult.TotalJobsFound}个岗位，{browseResult.OpenJobsCount}个开放中", 50);
                }
                else
                {
                    jobs = await reader.ReadOpenJobsAsync(instance, accountId, ct);
                }

                if (jobs.Count == 0)
                {
                    result.Success = true;
                    result.ErrorType = JobFetchErrorType.NoOpenJobs;
                    result.ErrorMessage = "暂无开放中的岗位";
                    result.Jobs = jobs;
                    result.ElapsedMs = sw.ElapsedMilliseconds;
                    return result;
                }

                ReportProgress(accountId, $"发现 {jobs.Count} 个开放中的岗位", 50);

                // 6. 读取岗位详情（可选）
                if (fetchDetails)
                {
                    for (int i = 0; i < jobs.Count; i++)
                    {
                        ct.ThrowIfCancellationRequested();

                        var progress = 50 + (int)(40.0 * (i + 1) / jobs.Count);
                        ReportProgress(accountId, $"正在读取岗位详情 ({i + 1}/{jobs.Count}): {jobs[i].Title}", progress);

                        var detailedJob = await reader.ReadJobDetailAsync(instance, jobs[i], ct);
                        if (detailedJob != null)
                        {
                            jobs[i] = detailedJob;
                        }

                        // 返回列表页继续下一个
                        if (i < jobs.Count - 1)
                        {
                            instance.Navigate(reader.JobListUrl);
                            await reader.WaitForPageReadyAsync(instance, 10000, ct);
                        }
                    }
                }

                ReportProgress(accountId, "岗位读取完成", 100);

                result.Success = true;
                result.Jobs = jobs;
                result.ElapsedMs = sw.ElapsedMilliseconds;
            }
            catch (OperationCanceledException)
            {
                result.Success = false;
                result.ErrorMessage = "操作已取消";
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.ErrorMessage = $"读取失败: {ex.Message}";
                result.ErrorType = JobFetchErrorType.Unknown;
            }

            sw.Stop();
            result.ElapsedMs = sw.ElapsedMilliseconds;

            OnCompleted?.Invoke(this, result);
            return result;
        }

        /// <summary>
        /// 从多个账号并行读取岗位
        /// 每个账号使用独立的浏览器实例和任务线程
        /// </summary>
        public async Task<List<JobFetchResult>> FetchJobsFromMultipleAccountsAsync(
            IEnumerable<string> accountIds,
            bool fetchDetails = true,
            CancellationToken ct = default)
        {
            var tasks = new List<Task<JobFetchResult>>();
            var results = new List<JobFetchResult>();

            foreach (var accountId in accountIds)
            {
                // 为每个账号创建独立的取消令牌
                var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                _runningTasks[accountId] = cts;

                // 启动独立任务
                var task = Task.Run(async () =>
                {
                    try
                    {
                        return await FetchJobsFromAccountAsync(accountId, fetchDetails, cts.Token);
                    }
                    finally
                    {
                        _runningTasks.TryRemove(accountId, out _);
                    }
                }, ct);

                tasks.Add(task);
            }

            // 等待所有任务完成
            var completedResults = await Task.WhenAll(tasks);
            results.AddRange(completedResults);

            return results;
        }

        /// <summary>
        /// 取消指定账号的岗位读取任务
        /// </summary>
        public void CancelFetch(string accountId)
        {
            if (_runningTasks.TryRemove(accountId, out var cts))
            {
                cts.Cancel();
                cts.Dispose();
            }
        }

        /// <summary>
        /// 取消所有岗位读取任务
        /// </summary>
        public void CancelAll()
        {
            foreach (var kvp in _runningTasks.ToArray())
            {
                kvp.Value.Cancel();
                kvp.Value.Dispose();
            }
            _runningTasks.Clear();
        }

        /// <summary>
        /// 检查指定账号是否正在读取
        /// </summary>
        public bool IsFetching(string accountId)
        {
            return _runningTasks.ContainsKey(accountId);
        }

        #region 私有方法

        private RecruitPlatform DetectPlatform(string url)
        {
            if (string.IsNullOrEmpty(url))
                return RecruitPlatform.Boss;

            url = url.ToLower();

            if (url.Contains("zhipin.com") || url.Contains("boss"))
                return RecruitPlatform.Boss;
            if (url.Contains("zhaopin.com"))
                return RecruitPlatform.Zhilian;
            if (url.Contains("51job.com"))
                return RecruitPlatform.Job51;
            if (url.Contains("liepin.com"))
                return RecruitPlatform.Liepin;

            return RecruitPlatform.Boss;
        }

        private async Task<AccountLoginStatus> CheckLoginStatusAsync(
            AccountBrowserInstance instance,
            RecruitPlatform platform)
        {
            // 先检查缓存（5分钟内有效）
            if (_loginStatusCache.TryGetValue(instance.AccountId, out var cached))
            {
                if ((DateTime.UtcNow - cached.LastChecked).TotalMinutes < 5 
                    && cached.LoginStatus == AccountLoginStatus.LoggedIn)
                {
                    return cached.LoginStatus;
                }
            }

            // 实时检测
            if (_readers.TryGetValue(platform, out var reader))
            {
                return await reader.CheckLoginStatusAsync(instance);
            }
            return AccountLoginStatus.Unknown;
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

        #endregion
    }

    /// <summary>
    /// 账号登录信息缓存
    /// </summary>
    internal class AccountLoginInfo
    {
        public string AccountId { get; set; } = string.Empty;
        public RecruitPlatform Platform { get; set; }
        public AccountLoginStatus LoginStatus { get; set; }
        public DateTime LastChecked { get; set; }
    }

    /// <summary>
    /// 岗位读取进度事件参数
    /// </summary>
    public class JobFetchProgressEventArgs : EventArgs
    {
        public string AccountId { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public int Percentage { get; set; }
    }
}
