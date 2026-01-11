using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using RecruitAutomation.Core.Models;

namespace RecruitAutomation.Browser.JobFetcher
{
    /// <summary>
    /// 岗位页面读取器接口
    /// 每个平台实现自己的读取逻辑
    /// </summary>
    public interface IJobPageReader
    {
        /// <summary>
        /// 支持的平台
        /// </summary>
        RecruitPlatform Platform { get; }

        /// <summary>
        /// 岗位管理页面URL
        /// </summary>
        string JobListUrl { get; }

        /// <summary>
        /// 检查登录状态
        /// </summary>
        Task<AccountLoginStatus> CheckLoginStatusAsync(
            AccountBrowserInstance browser,
            CancellationToken ct = default);

        /// <summary>
        /// 导航到岗位管理页面
        /// </summary>
        Task<bool> NavigateToJobListAsync(
            AccountBrowserInstance browser,
            CancellationToken ct = default);

        /// <summary>
        /// 等待页面加载完成（AI判断）
        /// </summary>
        Task<bool> WaitForPageReadyAsync(
            AccountBrowserInstance browser,
            int timeoutMs = 15000,
            CancellationToken ct = default);

        /// <summary>
        /// 读取岗位列表（仅开放中状态）
        /// </summary>
        Task<List<JobPosition>> ReadOpenJobsAsync(
            AccountBrowserInstance browser,
            string accountId,
            CancellationToken ct = default);

        /// <summary>
        /// 读取岗位详情
        /// </summary>
        Task<JobPosition?> ReadJobDetailAsync(
            AccountBrowserInstance browser,
            JobPosition job,
            CancellationToken ct = default);

        /// <summary>
        /// AI判断岗位是否为"开放中"状态
        /// </summary>
        Task<bool> IsJobOpenAsync(
            AccountBrowserInstance browser,
            string jobElementSelector,
            CancellationToken ct = default);
    }
}
