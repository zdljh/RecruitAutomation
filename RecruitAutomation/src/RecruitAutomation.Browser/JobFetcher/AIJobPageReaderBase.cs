using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using CefSharp;
using RecruitAutomation.Browser.HumanBehavior;
using RecruitAutomation.Core.Models;

namespace RecruitAutomation.Browser.JobFetcher
{
    /// <summary>
    /// AI驱动的岗位页面读取器基类
    /// 使用AI分析页面内容，模拟人工操作，防止封号
    /// </summary>
    public abstract class AIJobPageReaderBase : IJobPageReader
    {
        protected readonly HumanBehaviorSimulator _humanBehavior;
        protected readonly AIJobPageAnalyzer _aiAnalyzer;
        protected readonly Random _random = new();

        public abstract RecruitPlatform Platform { get; }
        public abstract string JobListUrl { get; }

        protected AIJobPageReaderBase(
            HumanBehaviorSimulator? humanBehavior = null,
            AIJobPageAnalyzer? aiAnalyzer = null)
        {
            _humanBehavior = humanBehavior ?? new HumanBehaviorSimulator();
            _aiAnalyzer = aiAnalyzer ?? new AIJobPageAnalyzer();
        }

        /// <summary>
        /// 更新AI配置
        /// </summary>
        public void UpdateAIConfig(AIAnalyzerConfig config)
        {
            _aiAnalyzer.UpdateConfig(config);
        }

        /// <summary>
        /// 检查登录状态（AI辅助判断）
        /// </summary>
        public virtual async Task<AccountLoginStatus> CheckLoginStatusAsync(
            AccountBrowserInstance browser,
            CancellationToken ct = default)
        {
            if (browser?.Browser == null)
                return AccountLoginStatus.Unknown;

            try
            {
                // 先用传统方式快速检测
                var quickResult = await QuickCheckLoginAsync(browser, ct);
                if (quickResult != AccountLoginStatus.Unknown)
                    return quickResult;

                // 如果不确定，使用AI分析
                var pageHtml = await GetPageHtmlAsync(browser);
                var currentUrl = browser.CurrentUrl;

                var status = await _aiAnalyzer.AnalyzePageStatusAsync(pageHtml, currentUrl, ct);

                if (status.NeedLogin)
                    return AccountLoginStatus.NotLoggedIn;
                if (status.IsJobList || status.HasJobs)
                    return AccountLoginStatus.LoggedIn;

                return AccountLoginStatus.Unknown;
            }
            catch
            {
                return AccountLoginStatus.Unknown;
            }
        }

        /// <summary>
        /// 导航到岗位管理页面（模拟人工操作）
        /// </summary>
        public virtual async Task<bool> NavigateToJobListAsync(
            AccountBrowserInstance browser,
            CancellationToken ct = default)
        {
            if (browser?.Browser == null)
                return false;

            try
            {
                // 模拟人工操作前的随机等待
                await _humanBehavior.Scheduler.WaitForNextAction(ct);

                // 直接导航到职位管理页面（更可靠）
                System.Diagnostics.Debug.WriteLine($"导航到职位管理页面: {JobListUrl}");
                browser.Navigate(JobListUrl);

                // 等待页面开始加载
                await Task.Delay(1000, ct);

                // 等待页面加载完成，增加超时时间到30秒
                var ready = await WaitForPageReadyAsync(browser, 30000, ct);
                
                if (!ready)
                {
                    System.Diagnostics.Debug.WriteLine("页面加载超时，尝试继续读取...");
                    // 即使超时也尝试继续，可能页面已经部分加载
                    await Task.Delay(2000, ct);
                    return true; // 返回true让后续逻辑尝试读取
                }

                return ready;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"导航失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 等待页面加载完成（AI判断）
        /// </summary>
        public virtual async Task<bool> WaitForPageReadyAsync(
            AccountBrowserInstance browser,
            int timeoutMs = 15000,
            CancellationToken ct = default)
        {
            if (browser?.Browser == null)
                return false;

            var startTime = DateTime.Now;
            var checkCount = 0;

            System.Diagnostics.Debug.WriteLine($"开始等待页面加载，超时时间: {timeoutMs}ms");

            while ((DateTime.Now - startTime).TotalMilliseconds < timeoutMs)
            {
                ct.ThrowIfCancellationRequested();

                // 先用传统方式检测
                var basicReady = await CheckBasicPageReadyAsync(browser);
                if (basicReady)
                {
                    System.Diagnostics.Debug.WriteLine("页面加载完成（传统检测）");
                    // 模拟人工确认页面加载
                    await Task.Delay(_random.Next(200, 500), ct);
                    return true;
                }

                checkCount++;
                System.Diagnostics.Debug.WriteLine($"页面检测第 {checkCount} 次，当前URL: {browser.CurrentUrl}");

                // 每5次检测输出一次页面状态
                if (checkCount % 5 == 0)
                {
                    try
                    {
                        var debugScript = @"
(function() {
    return JSON.stringify({
        readyState: document.readyState,
        url: window.location.href,
        bodyLength: document.body ? document.body.innerText.length : 0,
        hasJobList: !!document.querySelector('.job-list, [class*=""job-list""], table'),
        hasJobCards: document.querySelectorAll('[class*=""job""]').length
    });
})();";
                        var debugResult = await browser.Browser.EvaluateScriptAsync(debugScript);
                        if (debugResult.Success)
                        {
                            System.Diagnostics.Debug.WriteLine($"页面状态: {debugResult.Result}");
                        }
                    }
                    catch { }
                }

                await Task.Delay(500, ct);
            }

            System.Diagnostics.Debug.WriteLine("页面加载超时");
            return false;
        }

        /// <summary>
        /// 读取开放中的岗位列表（AI分析）
        /// </summary>
        public virtual async Task<List<JobPosition>> ReadOpenJobsAsync(
            AccountBrowserInstance browser,
            string accountId,
            CancellationToken ct = default)
        {
            var jobs = new List<JobPosition>();

            if (browser?.Browser == null)
                return jobs;

            try
            {
                System.Diagnostics.Debug.WriteLine("开始读取岗位列表...");
                
                // 【关键】模拟人工浏览页面 - 必须完整执行滚动+等待才能触发数据渲染
                System.Diagnostics.Debug.WriteLine("执行 AI 模拟人工操作（滚动+等待+触发渲染）...");
                await SimulateHumanBrowsingForJobList(browser, ct);

                // 先尝试传统方式读取（更可靠）
                System.Diagnostics.Debug.WriteLine("使用传统方式读取岗位...");
                jobs = await FallbackReadJobsAsync(browser, accountId, ct);
                
                if (jobs.Count > 0)
                {
                    System.Diagnostics.Debug.WriteLine($"传统方式读取到 {jobs.Count} 个岗位");
                    return jobs;
                }

                // 如果传统方式没读到，尝试AI分析
                System.Diagnostics.Debug.WriteLine("传统方式未读取到岗位，尝试AI分析...");
                
                // 获取页面HTML
                var pageHtml = await GetPageHtmlAsync(browser);
                System.Diagnostics.Debug.WriteLine($"页面HTML长度: {pageHtml?.Length ?? 0}");

                // 使用AI分析提取岗位
                var extractedJobs = await _aiAnalyzer.AnalyzeJobListPageAsync(
                    pageHtml, Platform, ct);

                System.Diagnostics.Debug.WriteLine($"AI分析提取到 {extractedJobs.Count} 个岗位");

                // 转换为JobPosition
                foreach (var extracted in extractedJobs)
                {
                    var job = ConvertToJobPosition(extracted, accountId);
                    jobs.Add(job);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"AI读取岗位失败: {ex.Message}");
                // 降级到传统方式
                jobs = await FallbackReadJobsAsync(browser, accountId, ct);
            }

            return jobs;
        }

        /// <summary>
        /// 读取岗位详情（AI分析）
        /// </summary>
        public virtual async Task<JobPosition?> ReadJobDetailAsync(
            AccountBrowserInstance browser,
            JobPosition job,
            CancellationToken ct = default)
        {
            if (browser?.Browser == null || string.IsNullOrEmpty(job.PageUrl))
                return job;

            try
            {
                // 模拟人工点击进入详情页
                await _humanBehavior.Scheduler.WaitForNextAction(ct);
                browser.Navigate(job.PageUrl);

                // 模拟人工等待
                await SimulateWaitingForPage(ct);

                // 等待详情页加载
                var loaded = await WaitForDetailPageAsync(browser, 10000, ct);
                if (!loaded)
                    return job;

                // 模拟人工阅读页面
                await SimulateHumanReading(browser, ct);

                // 获取页面HTML
                var pageHtml = await GetPageHtmlAsync(browser);

                // AI分析详情
                var detail = await _aiAnalyzer.AnalyzeJobDetailPageAsync(pageHtml, Platform, ct);

                if (detail != null)
                {
                    ApplyJobDetail(job, detail);
                }

                job.UpdatedAt = DateTime.UtcNow;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"AI读取岗位详情失败: {ex.Message}");
            }

            return job;
        }

        /// <summary>
        /// AI判断岗位是否为开放状态
        /// </summary>
        public virtual async Task<bool> IsJobOpenAsync(
            AccountBrowserInstance browser,
            string jobElementSelector,
            CancellationToken ct = default)
        {
            if (browser?.Browser == null)
                return false;

            try
            {
                // 获取元素HTML
                var script = $@"
(function() {{
    var el = document.querySelector('{EscapeSelector(jobElementSelector)}');
    return el ? el.outerHTML : '';
}})();";

                var result = await browser.Browser.EvaluateScriptAsync(script);
                if (result.Success && result.Result != null)
                {
                    var elementHtml = result.Result.ToString();
                    if (!string.IsNullOrEmpty(elementHtml))
                    {
                        return await _aiAnalyzer.IsJobOpenAsync(elementHtml, ct);
                    }
                }
            }
            catch { }

            return false;
        }

        #region 抽象方法（子类实现）

        /// <summary>
        /// 快速检测登录状态（传统方式）
        /// </summary>
        protected abstract Task<AccountLoginStatus> QuickCheckLoginAsync(
            AccountBrowserInstance browser, CancellationToken ct);

        /// <summary>
        /// 尝试点击岗位管理菜单
        /// </summary>
        protected abstract Task<bool> TryClickJobMenuAsync(
            AccountBrowserInstance browser, CancellationToken ct);

        /// <summary>
        /// 基础页面加载检测
        /// </summary>
        protected abstract Task<bool> CheckBasicPageReadyAsync(AccountBrowserInstance browser);

        /// <summary>
        /// 等待详情页加载
        /// </summary>
        protected abstract Task<bool> WaitForDetailPageAsync(
            AccountBrowserInstance browser, int timeoutMs, CancellationToken ct);

        /// <summary>
        /// 降级读取岗位（传统方式）
        /// </summary>
        protected abstract Task<List<JobPosition>> FallbackReadJobsAsync(
            AccountBrowserInstance browser, string accountId, CancellationToken ct);

        #endregion

        #region 辅助方法

        /// <summary>
        /// 获取页面HTML
        /// </summary>
        protected async Task<string> GetPageHtmlAsync(AccountBrowserInstance browser)
        {
            var script = "document.documentElement.outerHTML";
            var result = await browser.Browser.EvaluateScriptAsync(script);
            return result.Success ? result.Result?.ToString() ?? "" : "";
        }

        /// <summary>
        /// 模拟人工浏览页面
        /// </summary>
        protected async Task SimulateHumanBrowsing(AccountBrowserInstance browser, CancellationToken ct)
        {
            // 随机滚动
            var scrollDistance = _random.Next(100, 400);
            await _humanBehavior.ScrollAsync(browser.Browser, scrollDistance, ct);

            // 随机停顿
            await Task.Delay(_random.Next(300, 800), ct);

            // 可能再滚动一次
            if (_random.NextDouble() > 0.5)
            {
                scrollDistance = _random.Next(50, 200);
                await _humanBehavior.ScrollAsync(browser.Browser, scrollDistance, ct);
            }
        }

        /// <summary>
        /// 【关键】模拟人工浏览岗位列表页面 - 完整的滚动+等待+触发渲染
        /// Boss直聘等平台必须有人类行为才能触发真实数据渲染
        /// </summary>
        protected async Task SimulateHumanBrowsingForJobList(AccountBrowserInstance browser, CancellationToken ct)
        {
            if (browser?.Browser == null)
                return;

            try
            {
                System.Diagnostics.Debug.WriteLine("开始模拟人工浏览岗位列表...");

                // 1. 等待页面初始化（人工进入页面后的自然等待）
                var initialWait = _random.Next(1500, 2500);
                System.Diagnostics.Debug.WriteLine($"等待页面初始化: {initialWait}ms");
                await Task.Delay(initialWait, ct);

                // 2. 执行 JS 模拟人工滚动行为（关键！触发数据渲染）
                var scrollScript = @"
(async function() {
    function sleep(ms) {
        return new Promise(r => setTimeout(r, ms));
    }
    
    console.log('开始模拟人工滚动...');
    
    // 等待页面稳定
    await sleep(800);
    
    // 模拟人工滚动 - 多次小幅度滚动，模拟真实用户行为
    for (let i = 0; i < 6; i++) {
        // 随机滚动距离（300-700像素）
        var scrollY = 300 + Math.floor(Math.random() * 400);
        window.scrollBy(0, scrollY);
        console.log('滚动 ' + scrollY + ' 像素');
        
        // 随机等待（600-1200ms），模拟人工阅读
        var waitTime = 600 + Math.floor(Math.random() * 600);
        await sleep(waitTime);
    }
    
    // 滚回顶部（人类行为 - 查看完后回到顶部）
    console.log('滚回顶部...');
    window.scrollTo({ top: 0, behavior: 'smooth' });
    await sleep(800);
    
    // 再次小幅滚动，确保触发视口渲染
    window.scrollBy(0, 200);
    await sleep(500);
    
    console.log('人工滚动完成');
    return true;
})();";

                System.Diagnostics.Debug.WriteLine("执行滚动脚本...");
                var result = await browser.Browser.EvaluateScriptAsync(scrollScript);
                
                if (!result.Success)
                {
                    System.Diagnostics.Debug.WriteLine($"滚动脚本执行失败: {result.Message}");
                }

                // 3. 等待滚动脚本执行完成（脚本内部有 await，但 EvaluateScriptAsync 不会等待）
                // 估算脚本执行时间：6次滚动 * (滚动 + 等待约900ms) + 回顶部等待 ≈ 7秒
                System.Diagnostics.Debug.WriteLine("等待滚动脚本执行完成...");
                await Task.Delay(7500, ct);

                // 4. 额外等待数据渲染
                var renderWait = _random.Next(1000, 1500);
                System.Diagnostics.Debug.WriteLine($"等待数据渲染: {renderWait}ms");
                await Task.Delay(renderWait, ct);

                // 5. 检查是否有数据加载
                var checkScript = @"
(function() {
    var jobElements = document.querySelectorAll(
        '.job-card, .position-item, [class*=""job-card""], [class*=""position-item""], ' +
        '.job-item, [data-job-id], tr[class*=""job""], .job-list-item, ' +
        'a[href*=""/job/""], a[href*=""/web/job/""]'
    );
    console.log('检测到岗位元素数量: ' + jobElements.length);
    return jobElements.length;
})();";

                var checkResult = await browser.Browser.EvaluateScriptAsync(checkScript);
                if (checkResult.Success)
                {
                    System.Diagnostics.Debug.WriteLine($"检测到岗位元素数量: {checkResult.Result}");
                    
                    // 如果没有检测到岗位，尝试再次滚动
                    if (checkResult.Result is int count && count == 0)
                    {
                        System.Diagnostics.Debug.WriteLine("未检测到岗位，尝试再次滚动...");
                        await RetryScrollForJobs(browser, ct);
                    }
                }

                System.Diagnostics.Debug.WriteLine("人工浏览模拟完成");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"模拟人工浏览失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 重试滚动以触发岗位加载
        /// </summary>
        private async Task RetryScrollForJobs(AccountBrowserInstance browser, CancellationToken ct)
        {
            try
            {
                // 刷新页面
                var reloadScript = @"
(async function() {
    function sleep(ms) {
        return new Promise(r => setTimeout(r, ms));
    }
    
    console.log('重新加载页面...');
    location.reload();
    return true;
})();";

                await browser.Browser.EvaluateScriptAsync(reloadScript);
                
                // 等待页面重新加载
                await Task.Delay(3000, ct);

                // 再次执行滚动
                var scrollScript = @"
(async function() {
    function sleep(ms) {
        return new Promise(r => setTimeout(r, ms));
    }
    
    await sleep(2000);
    
    for (let i = 0; i < 4; i++) {
        window.scrollBy(0, 500);
        await sleep(800);
    }
    
    window.scrollTo(0, 0);
    await sleep(500);
    
    return true;
})();";

                await browser.Browser.EvaluateScriptAsync(scrollScript);
                await Task.Delay(5000, ct);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"重试滚动失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 模拟人工阅读
        /// </summary>
        protected async Task SimulateHumanReading(AccountBrowserInstance browser, CancellationToken ct)
        {
            // 模拟阅读时间（1-3秒）
            await Task.Delay(_random.Next(1000, 3000), ct);

            // 随机滚动查看内容
            await _humanBehavior.ScrollAsync(browser.Browser, _random.Next(200, 500), ct);
            await Task.Delay(_random.Next(500, 1000), ct);
        }

        /// <summary>
        /// 模拟等待页面加载
        /// </summary>
        protected async Task SimulateWaitingForPage(CancellationToken ct)
        {
            // 人工等待页面加载的随机时间
            await Task.Delay(_random.Next(500, 1500), ct);
        }

        /// <summary>
        /// 转换AI提取结果为JobPosition
        /// </summary>
        protected JobPosition ConvertToJobPosition(AIExtractedJob extracted, string accountId)
        {
            var job = new JobPosition
            {
                Platform = Platform,
                AccountId = accountId,
                PlatformJobId = extracted.PlatformJobId,
                Title = extracted.Title,
                SalaryText = extracted.SalaryText,
                Location = extracted.Location,
                ExperienceRequired = extracted.ExperienceRequired,
                EducationRequired = extracted.EducationRequired,
                PageUrl = extracted.PageUrl,
                Status = extracted.IsOpen ? JobStatus.Open : JobStatus.Closed,
                FetchedAt = DateTime.UtcNow
            };

            job.Id = $"{Platform.ToString().ToLower()}_{accountId}_{job.PlatformJobId}";
            ParseSalary(job);

            return job;
        }

        /// <summary>
        /// 应用岗位详情
        /// </summary>
        protected void ApplyJobDetail(JobPosition job, AIJobDetail detail)
        {
            if (!string.IsNullOrEmpty(detail.Description))
                job.Description = detail.Description;
            if (!string.IsNullOrEmpty(detail.Requirements))
                job.Requirements = detail.Requirements;
            if (!string.IsNullOrEmpty(detail.Address))
                job.Address = detail.Address;
            if (detail.Tags?.Count > 0)
                job.Tags = job.Tags.Union(detail.Tags).Distinct().ToList();

            job.Keywords = ExtractKeywords(job.Description, job.Requirements, job.Tags);
        }

        /// <summary>
        /// 解析薪资
        /// </summary>
        protected void ParseSalary(JobPosition job)
        {
            if (string.IsNullOrEmpty(job.SalaryText))
                return;

            var match = Regex.Match(job.SalaryText, @"(\d+)-(\d+)", RegexOptions.IgnoreCase);
            if (match.Success)
            {
                if (int.TryParse(match.Groups[1].Value, out var min))
                {
                    job.SalaryMin = job.SalaryText.Contains("万") || min < 100 ? min : min / 1000;
                }
                if (int.TryParse(match.Groups[2].Value, out var max))
                {
                    job.SalaryMax = job.SalaryText.Contains("万") || max < 100 ? max : max / 1000;
                }
            }

            var monthMatch = Regex.Match(job.SalaryText, @"(\d+)薪");
            if (monthMatch.Success && int.TryParse(monthMatch.Groups[1].Value, out var months))
                job.SalaryMonths = months;
        }

        /// <summary>
        /// 提取关键词
        /// </summary>
        protected List<string> ExtractKeywords(string description, string requirements, List<string> tags)
        {
            var keywords = new HashSet<string>(tags, StringComparer.OrdinalIgnoreCase);

            var techKeywords = new[]
            {
                "Java", "Python", "C#", "JavaScript", "TypeScript", "Go", "Rust",
                "React", "Vue", "Angular", "Node.js", ".NET", "Spring",
                "MySQL", "PostgreSQL", "MongoDB", "Redis", "Elasticsearch",
                "Docker", "Kubernetes", "AWS", "Azure", "Linux"
            };

            var fullText = $"{description} {requirements}";

            foreach (var keyword in techKeywords)
            {
                if (fullText.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                    keywords.Add(keyword);
            }

            return keywords.Take(20).ToList();
        }

        protected static string EscapeSelector(string selector)
        {
            return selector.Replace("'", "\\'").Replace("\\", "\\\\");
        }

        #endregion
    }
}
