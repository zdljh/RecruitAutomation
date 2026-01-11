using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using CefSharp;
using RecruitAutomation.Browser.HumanBehavior;
using RecruitAutomation.Core.Models;

namespace RecruitAutomation.Browser.JobFetcher
{
    /// <summary>
    /// 智联招聘岗位页面读取器
    /// 支持AI模式和传统模式，AI模式模拟人工操作防止封号
    /// </summary>
    public class ZhilianJobPageReader : AIJobPageReaderBase
    {
        /// <summary>
        /// 是否启用AI模式
        /// </summary>
        public bool UseAIMode { get; set; } = true;

        public override RecruitPlatform Platform => RecruitPlatform.Zhilian;

        /// <summary>
        /// 智联招聘职位管理页面
        /// </summary>
        public override string JobListUrl => "https://ihr.zhaopin.com/job/joblist.html";

        public ZhilianJobPageReader(
            HumanBehaviorSimulator? humanBehavior = null,
            AIJobPageAnalyzer? aiAnalyzer = null)
            : base(humanBehavior, aiAnalyzer)
        {
        }

        #region 抽象方法实现

        protected override async Task<AccountLoginStatus> QuickCheckLoginAsync(
            AccountBrowserInstance browser,
            CancellationToken ct = default)
        {
            try
            {
                var script = @"
(function() {
    // 检查是否有登录按钮
    var loginBtn = document.querySelector('.login-btn, .btn-login, [class*=""login""]');
    if (loginBtn && loginBtn.offsetParent !== null) return 'not_logged_in';
    
    // 检查是否有用户信息
    var userInfo = document.querySelector('.user-info, .user-name, [class*=""user-avatar""]');
    if (userInfo) return 'logged_in';
    
    // 检查URL
    if (window.location.href.includes('/login') || 
        window.location.href.includes('/passport')) {
        return 'not_logged_in';
    }
    
    // 检查是否有职位列表
    var jobList = document.querySelector('.job-list, .position-list, [class*=""job-table""]');
    if (jobList) return 'logged_in';
    
    return 'unknown';
})();";

                var result = await browser.Browser.EvaluateScriptAsync(script);
                if (result.Success && result.Result != null)
                {
                    var status = result.Result.ToString();
                    return status switch
                    {
                        "logged_in" => AccountLoginStatus.LoggedIn,
                        "not_logged_in" => AccountLoginStatus.NotLoggedIn,
                        _ => AccountLoginStatus.Unknown
                    };
                }
            }
            catch { }

            return AccountLoginStatus.Unknown;
        }

        protected override async Task<bool> TryClickJobMenuAsync(
            AccountBrowserInstance browser,
            CancellationToken ct = default)
        {
            try
            {
                // 尝试点击"职位管理"菜单
                var script = @"
(function() {
    var menuItems = document.querySelectorAll('a, .menu-item, .nav-item');
    for (var i = 0; i < menuItems.length; i++) {
        var text = menuItems[i].innerText || '';
        if (text.includes('职位') || text.includes('岗位')) {
            menuItems[i].click();
            return true;
        }
    }
    return false;
})();";

                var result = await browser.Browser.EvaluateScriptAsync(script);
                return result.Success && result.Result is bool clicked && clicked;
            }
            catch
            {
                return false;
            }
        }

        protected override async Task<bool> CheckBasicPageReadyAsync(AccountBrowserInstance browser)
        {
            try
            {
                var script = @"
(function() {
    if (document.readyState !== 'complete') return false;
    
    // 检查加载状态
    var loading = document.querySelector('.loading, .spinner');
    if (loading && loading.offsetParent !== null) return false;
    
    // 检查职位列表
    var jobList = document.querySelector('.job-list, .position-list, table');
    if (jobList) return true;
    
    // 检查空状态
    var empty = document.body.innerText.includes('暂无') || 
                document.body.innerText.includes('没有职位');
    return empty;
})();";

                var result = await browser.Browser.EvaluateScriptAsync(script);
                return result.Success && result.Result is bool ready && ready;
            }
            catch
            {
                return false;
            }
        }

        protected override async Task<bool> WaitForDetailPageAsync(
            AccountBrowserInstance browser,
            int timeoutMs,
            CancellationToken ct)
        {
            var startTime = DateTime.Now;

            while ((DateTime.Now - startTime).TotalMilliseconds < timeoutMs)
            {
                ct.ThrowIfCancellationRequested();

                var script = @"
(function() {
    if (document.readyState !== 'complete') return false;
    var desc = document.querySelector('.job-detail, .job-desc, [class*=""description""]');
    return desc !== null;
})();";

                var result = await browser.Browser.EvaluateScriptAsync(script);
                if (result.Success && result.Result is bool ready && ready)
                {
                    return true;
                }

                await Task.Delay(300, ct);
            }

            return false;
        }

        protected override async Task<List<JobPosition>> FallbackReadJobsAsync(
            AccountBrowserInstance browser,
            string accountId,
            CancellationToken ct = default)
        {
            var jobs = new List<JobPosition>();

            if (browser?.Browser == null)
                return jobs;

            try
            {
                await _humanBehavior.ScrollAsync(browser.Browser, 200, ct);
                await Task.Delay(300, ct);

                var script = @"
(function() {
    var jobs = [];
    
    // 查找职位行（智联通常是表格形式）
    var rows = document.querySelectorAll('tr[class*=""job""], .job-item, .position-row');
    
    rows.forEach(function(row, index) {
        try {
            var job = {};
            
            // 读取职位ID
            var link = row.querySelector('a[href*=""job""]');
            if (link) {
                var href = link.getAttribute('href');
                var match = href.match(/(\d+)/);
                if (match) job.platformJobId = match[1];
                job.pageUrl = link.href;
            }
            
            // 读取职位名称
            var titleEl = row.querySelector('.job-name, .position-name, td:first-child a');
            if (titleEl) job.title = titleEl.innerText.trim();
            
            // 读取薪资
            var salaryEl = row.querySelector('.salary, [class*=""salary""]');
            if (salaryEl) job.salaryText = salaryEl.innerText.trim();
            
            // 读取地点
            var locationEl = row.querySelector('.city, .location, [class*=""city""]');
            if (locationEl) job.location = locationEl.innerText.trim();
            
            // 读取状态
            var statusEl = row.querySelector('.status, [class*=""status""]');
            var statusText = statusEl ? statusEl.innerText.trim() : '';
            
            // AI语义判断是否开放
            var isOpen = false;
            if (statusText.includes('发布中') || statusText.includes('招聘中') || 
                statusText.includes('开放') || statusText.includes('在线')) {
                isOpen = true;
            }
            
            // 排除非开放状态
            var rowText = row.innerText;
            if (rowText.includes('已关闭') || rowText.includes('已下线') || 
                rowText.includes('审核') || rowText.includes('暂停')) {
                isOpen = false;
            }
            
            job.isOpen = isOpen;
            job.statusText = statusText;
            job.index = index;
            
            if (job.title) jobs.push(job);
        } catch(e) {}
    });
    
    return JSON.stringify(jobs);
})();";

                var result = await browser.Browser.EvaluateScriptAsync(script);
                if (result.Success && result.Result != null)
                {
                    var json = result.Result.ToString();
                    var rawJobs = JsonSerializer.Deserialize<List<RawJobData>>(json ?? "[]");

                    if (rawJobs != null)
                    {
                        foreach (var raw in rawJobs)
                        {
                            if (!raw.IsOpen)
                                continue;

                            var job = new JobPosition
                            {
                                Platform = RecruitPlatform.Zhilian,
                                AccountId = accountId,
                                PlatformJobId = raw.PlatformJobId ?? "",
                                Title = raw.Title ?? "",
                                SalaryText = raw.SalaryText ?? "",
                                Location = raw.Location ?? "",
                                PageUrl = raw.PageUrl ?? "",
                                Status = JobStatus.Open,
                                FetchedAt = DateTime.UtcNow
                            };

                            job.Id = $"zhilian_{accountId}_{job.PlatformJobId}";
                            ParseSalary(job);
                            jobs.Add(job);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"读取智联岗位列表失败: {ex.Message}");
            }

            return jobs;
        }

        #endregion

        #region 传统方式读取岗位详情（保留兼容）

        /// <summary>
        /// 传统方式读取岗位详情
        /// </summary>
        public async Task<JobPosition?> ReadJobDetailLegacyAsync(
            AccountBrowserInstance browser,
            JobPosition job,
            CancellationToken ct = default)
        {
            if (browser?.Browser == null || string.IsNullOrEmpty(job.PageUrl))
                return job;

            try
            {
                await _humanBehavior.Scheduler.WaitForNextAction(ct);
                browser.Navigate(job.PageUrl);
                await Task.Delay(1000, ct);

                // 等待详情页加载
                var loaded = await WaitForDetailPageAsync(browser, 10000, ct);
                if (!loaded)
                    return job;

                await _humanBehavior.ScrollAsync(browser.Browser, 300, ct);
                await Task.Delay(500, ct);

                var script = @"
(function() {
    var detail = {};
    
    // 读取岗位描述
    var descEl = document.querySelector('.job-detail, .job-desc, [class*=""description""]');
    if (descEl) detail.description = descEl.innerText.trim();
    
    // 读取岗位要求
    var reqEl = document.querySelector('.job-require, [class*=""require""]');
    if (reqEl) detail.requirements = reqEl.innerText.trim();
    
    // 读取详细地址
    var addrEl = document.querySelector('.job-address, [class*=""address""]');
    if (addrEl) detail.address = addrEl.innerText.trim();
    
    // 读取经验要求
    var expEl = document.querySelector('[class*=""experience""]');
    if (expEl) detail.experienceRequired = expEl.innerText.trim();
    
    // 读取学历要求
    var eduEl = document.querySelector('[class*=""education""]');
    if (eduEl) detail.educationRequired = eduEl.innerText.trim();
    
    return JSON.stringify(detail);
})();";

                var result = await browser.Browser.EvaluateScriptAsync(script);
                if (result.Success && result.Result != null)
                {
                    var json = result.Result.ToString();
                    var detail = JsonSerializer.Deserialize<JobDetailData>(json ?? "{}");

                    if (detail != null)
                    {
                        if (!string.IsNullOrEmpty(detail.Description))
                            job.Description = detail.Description;
                        if (!string.IsNullOrEmpty(detail.Requirements))
                            job.Requirements = detail.Requirements;
                        if (!string.IsNullOrEmpty(detail.Address))
                            job.Address = detail.Address;
                        if (!string.IsNullOrEmpty(detail.ExperienceRequired))
                            job.ExperienceRequired = detail.ExperienceRequired;
                        if (!string.IsNullOrEmpty(detail.EducationRequired))
                            job.EducationRequired = detail.EducationRequired;

                        job.Keywords = ExtractKeywords(job.Description, job.Requirements, job.Tags);
                    }
                }

                job.UpdatedAt = DateTime.UtcNow;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"读取智联岗位详情失败: {ex.Message}");
            }

            return job;
        }

        #endregion

        #region AI判断岗位状态（保留兼容）

        /// <summary>
        /// 传统方式判断岗位是否开放
        /// </summary>
        public async Task<bool> IsJobOpenLegacyAsync(
            AccountBrowserInstance browser,
            string jobElementSelector,
            CancellationToken ct = default)
        {
            if (browser?.Browser == null)
                return false;

            try
            {
                var script = $@"
(function() {{
    var el = document.querySelector('{EscapeSelector(jobElementSelector)}');
    if (!el) return false;
    
    var text = el.innerText;
    
    if (text.includes('已关闭') || text.includes('已下线') || 
        text.includes('审核') || text.includes('暂停')) {{
        return false;
    }}
    
    if (text.includes('发布中') || text.includes('招聘中') || 
        text.includes('开放') || text.includes('在线')) {{
        return true;
    }}
    
    return false;
}})();";

                var result = await browser.Browser.EvaluateScriptAsync(script);
                return result.Success && result.Result is bool isOpen && isOpen;
            }
            catch
            {
                return false;
            }
        }

        #endregion

        #region 私有方法

        private async Task<bool> WaitForDetailPageLegacyAsync(
            AccountBrowserInstance browser,
            int timeoutMs,
            CancellationToken ct)
        {
            var startTime = DateTime.Now;

            while ((DateTime.Now - startTime).TotalMilliseconds < timeoutMs)
            {
                ct.ThrowIfCancellationRequested();

                var script = @"
(function() {
    if (document.readyState !== 'complete') return false;
    var desc = document.querySelector('.job-detail, .job-desc, [class*=""description""]');
    return desc !== null;
})();";

                var result = await browser.Browser.EvaluateScriptAsync(script);
                if (result.Success && result.Result is bool ready && ready)
                {
                    return true;
                }

                await Task.Delay(300, ct);
            }

            return false;
        }

        private new void ParseSalary(JobPosition job)
        {
            base.ParseSalary(job);
        }

        private new List<string> ExtractKeywords(string description, string requirements, List<string> tags)
        {
            return base.ExtractKeywords(description, requirements, tags);
        }

        private static new string EscapeSelector(string selector)
        {
            return selector.Replace("'", "\\'").Replace("\\", "\\\\");
        }

        #endregion

        #region 内部数据类

        private class RawJobData
        {
            public string? PlatformJobId { get; set; }
            public string? Title { get; set; }
            public string? SalaryText { get; set; }
            public string? Location { get; set; }
            public string? PageUrl { get; set; }
            public bool IsOpen { get; set; }
            public string? StatusText { get; set; }
        }

        private class JobDetailData
        {
            public string? Description { get; set; }
            public string? Requirements { get; set; }
            public string? Address { get; set; }
            public string? ExperienceRequired { get; set; }
            public string? EducationRequired { get; set; }
        }

        #endregion
    }
}
