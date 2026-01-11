using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using RecruitAutomation.Core.Models;
using RecruitAutomation.Core.Runtime;
using RecruitAutomation.Core.Services;

namespace RecruitAutomation.App.Runtime
{
    /// <summary>
    /// 浏览器模块控制器（白图 AI 4.0 风格）
    /// 
    /// 职责：
    /// 1. 管理 CefSharp 生命周期
    /// 2. 管理浏览器实例
    /// 3. 提供岗位读取服务
    /// 
    /// 设计原则：
    /// - 所有操作都有异常保护
    /// - 延迟加载 Browser 程序集
    /// - 状态变化通过事件通知 UI
    /// </summary>
    public sealed class BrowserModuleController : ModuleControllerBase
    {
        private static readonly Lazy<BrowserModuleController> _instance =
            new(() => new BrowserModuleController(), LazyThreadSafetyMode.ExecutionAndPublication);

        public static BrowserModuleController Instance => _instance.Value;

        private object? _jobFetchService;
        private JobManagementService? _jobManagementService;
        private JobRepository? _jobRepository;
        private bool _cefInitialized;

        public override string ModuleId => ModuleNames.Browser;
        public override string DisplayName => "浏览器模块";

        /// <summary>
        /// 岗位管理服务
        /// </summary>
        public JobManagementService? JobManagementService => _jobManagementService;

        /// <summary>
        /// CefSharp 是否已初始化
        /// </summary>
        public bool IsCefInitialized => _cefInitialized;

        // 事件
        public event Action<string, string, int>? OnProgress;
        public event Action<JobFetchResult>? OnCompleted;

        private BrowserModuleController() { }

        #region 生命周期

        [MethodImpl(MethodImplOptions.NoInlining)]
        protected override async Task<bool> DoInitializeAsync(CancellationToken ct)
        {
            await Task.Yield(); // 确保异步执行

            try
            {
                // 1. 确保 CefSharp 已初始化
                WriteLog("检查 CefSharp 状态...");
                App.EnsureCefInitialized();

                if (!CefSharp.Cef.IsInitialized)
                {
                    LastError = "CefSharp 未初始化";
                    return false;
                }
                _cefInitialized = true;
                WriteLog("CefSharp 已初始化");

                // 2. 初始化数据服务
                WriteLog("初始化数据服务...");
                _jobRepository = new JobRepository();
                _jobManagementService = new JobManagementService(_jobRepository);

                // 3. 初始化岗位读取服务（延迟加载）
                WriteLog("初始化岗位读取服务...");
                InitializeJobFetchService();

                return true;
            }
            catch (Exception ex)
            {
                LastError = ex.Message;
                return false;
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private void InitializeJobFetchService()
        {
            var service = new Browser.JobFetcher.JobFetchService();
            service.OnProgress += (s, e) =>
            {
                try { OnProgress?.Invoke(e.AccountId, e.Message, e.Percentage); } catch { }
            };
            service.OnCompleted += (s, e) =>
            {
                try { OnCompleted?.Invoke(e); } catch { }
            };
            _jobFetchService = service;
        }

        protected override Task DoStopAsync()
        {
            // 停止时不关闭浏览器实例，只是暂停自动化
            return Task.CompletedTask;
        }

        protected override void DoDispose()
        {
            try
            {
                // 关闭所有浏览器实例
                CloseBrowserManager();
            }
            catch { }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private void CloseBrowserManager()
        {
            Browser.BrowserInstanceManager.Instance.Dispose();
        }

        #endregion

        #region 浏览器操作

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
                WriteError("MarkAccountAsLoggedIn", ex);
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
                WriteError("GetAvailableAccountsAsync", ex);
            }
            return new List<AvailableAccount>();
        }

        /// <summary>
        /// 从单个账号读取岗位
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public async Task<JobFetchResult> FetchJobsFromAccountAsync(string accountId, bool useAI, CancellationToken ct)
        {
            try
            {
                if (_jobFetchService is Browser.JobFetcher.JobFetchService service)
                {
                    return await service.FetchJobsFromAccountAsync(accountId, useAI, ct);
                }
            }
            catch (Exception ex)
            {
                WriteError("FetchJobsFromAccountAsync", ex);
                return new JobFetchResult { Success = false, ErrorMessage = ex.Message };
            }
            return new JobFetchResult { Success = false, ErrorMessage = "服务未初始化" };
        }

        /// <summary>
        /// 从多个账号读取岗位
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public async Task<List<JobFetchResult>> FetchJobsFromMultipleAccountsAsync(
            IEnumerable<string> accountIds, bool useAI, CancellationToken ct)
        {
            try
            {
                if (_jobFetchService is Browser.JobFetcher.JobFetchService service)
                {
                    return await service.FetchJobsFromMultipleAccountsAsync(accountIds, useAI, ct);
                }
            }
            catch (Exception ex)
            {
                WriteError("FetchJobsFromMultipleAccountsAsync", ex);
            }
            return new List<JobFetchResult>();
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
                WriteError("CancelAll", ex);
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
                WriteError("CloseBrowserInstance", ex);
            }
        }

        #endregion
    }
}
