using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using CefSharp;
using RecruitAutomation.Browser.HumanBehavior;
using RecruitAutomation.Core.Models;

namespace RecruitAutomation.Browser.JobFetcher
{
    public class BossJobPageReader : AIJobPageReaderBase
    {
        private static readonly string DataRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RecruitAutomation");
        private static readonly string LogPath = Path.Combine(DataRoot, "logs", "boss_reader.log");

        private readonly VisualJobBrowser _visualBrowser;

        public bool UseVisualMode { get; set; } = true;

        public bool DebugMode
        {
            get => _visualBrowser.DebugMode;
            set => _visualBrowser.DebugMode = value;
        }

        public bool PauseForManualObservation
        {
            get => _visualBrowser.PauseForManualObservation;
            set => _visualBrowser.PauseForManualObservation = value;
        }

        public override RecruitPlatform Platform => RecruitPlatform.Boss;
        public override string JobListUrl => "https://www.zhipin.com/web/chat/job/list";

        public BossJobPageReader(HumanBehaviorSimulator? humanBehavior = null, AIJobPageAnalyzer? aiAnalyzer = null)
            : base(humanBehavior, aiAnalyzer)
        {
            _visualBrowser = new VisualJobBrowser(humanBehavior);
            EnsureDirectories();
        }

        public void UpdateVisualAIConfig(string apiKey, string baseUrl, string model)
            => _visualBrowser.UpdateAIConfig(apiKey, baseUrl, model);

        private void Log(string msg)
        {
            try { File.AppendAllText(LogPath, $"[{DateTime.Now:HH:mm:ss}] {msg}\n"); } catch { }
        }

        private void EnsureDirectories()
        {
            try
            {
                var logDir = Path.GetDirectoryName(LogPath);
                if (!string.IsNullOrEmpty(logDir) && !Directory.Exists(logDir))
                    Directory.CreateDirectory(logDir);
            }
            catch { }
        }

        public override async Task<List<JobPosition>> ReadOpenJobsAsync(
            AccountBrowserInstance browser, string accountId, CancellationToken ct = default)
        {
            if (browser?.Browser == null) return new List<JobPosition>();

            try
            {
                if (UseVisualMode)
                {
                    var result = await _visualBrowser.BrowseJobsAsync(browser, accountId, ct);
                    return result.Jobs;
                }
                await Task.Delay(_random.Next(2000, 3500), ct);
                return await ExtractJobsFromText(browser, accountId, ct);
            }
            catch { return new List<JobPosition>(); }
        }

        public async Task<VisualBrowseResult> BrowseJobsWithStatusAsync(
            AccountBrowserInstance browser, string accountId, CancellationToken ct = default)
        {
            if (browser?.Browser == null)
                return new VisualBrowseResult { AccountId = accountId, Status = BrowseStatus.Error };
            return await _visualBrowser.BrowseJobsAsync(browser, accountId, ct);
        }

        private async Task<List<JobPosition>> ExtractJobsFromText(
            AccountBrowserInstance browser, string accountId, CancellationToken ct)
        {
            var jobs = new List<JobPosition>();
            try
            {
                var result = await browser.Browser.EvaluateScriptAsync("document.body.innerText||''");
                if (!result.Success || result.Result == null) return jobs;

                var text = result.Result.ToString() ?? "";
                if (!text.Contains("开放中")) return jobs;

                foreach (var seg in text.Split(new[] { "开放中" }, StringSplitOptions.RemoveEmptyEntries))
                {
                    var job = ParseJob(seg.Length > 500 ? seg.Substring(seg.Length - 500) : seg, accountId);
                    if (job != null) jobs.Add(job);
                }
            }
            catch { }
            return jobs;
        }

        private JobPosition? ParseJob(string text, string accountId)
        {
            if (string.IsNullOrWhiteSpace(text)) return null;
            var job = new JobPosition { Platform = RecruitPlatform.Boss, AccountId = accountId, Status = JobStatus.Open, FetchedAt = DateTime.UtcNow };

            var titleMatch = Regex.Match(text, @"([A-Za-z\u4e00-\u9fa5]+(?:工程师|经理|主管|专员|开发|运营|产品|测试|[A-Za-z]+))");
            if (titleMatch.Success) job.Title = titleMatch.Groups[1].Value.Trim();

            var salaryMatch = Regex.Match(text, @"(\d+)-(\d+)[Kk]");
            if (salaryMatch.Success)
            {
                job.SalaryText = salaryMatch.Value;
                int.TryParse(salaryMatch.Groups[1].Value, out var min); job.SalaryMin = min;
                int.TryParse(salaryMatch.Groups[2].Value, out var max); job.SalaryMax = max;
            }

            if (string.IsNullOrEmpty(job.Title)) return null;
            job.PlatformJobId = $"boss_{job.Title.GetHashCode():X}_{DateTime.Now.Ticks}";
            job.Id = $"boss_{accountId}_{job.PlatformJobId}";
            return job;
        }

        protected override async Task<AccountLoginStatus> QuickCheckLoginAsync(AccountBrowserInstance browser, CancellationToken ct)
        {
            try
            {
                var url = browser.CurrentUrl?.ToLower() ?? "";
                if (url.Contains("/login")) return AccountLoginStatus.NotLoggedIn;

                var result = await browser.Browser.EvaluateScriptAsync(
                    "(function(){var t=document.body.innerText||'';if(t.indexOf('请登录')>=0)return'not';if(t.indexOf('开放中')>=0)return'ok';return'unk';})();");
                if (result.Success && result.Result != null)
                {
                    var s = result.Result.ToString();
                    return s == "ok" ? AccountLoginStatus.LoggedIn : s == "not" ? AccountLoginStatus.NotLoggedIn : AccountLoginStatus.Unknown;
                }
            }
            catch { }
            return AccountLoginStatus.Unknown;
        }

        protected override Task<bool> TryClickJobMenuAsync(AccountBrowserInstance browser, CancellationToken ct)
            => Task.FromResult(true);

        protected override async Task<bool> CheckBasicPageReadyAsync(AccountBrowserInstance browser)
        {
            try
            {
                var result = await browser.Browser.EvaluateScriptAsync(
                    "(function(){return document.readyState==='complete'&&(document.body.innerText||'').length>100;})();");
                return result.Success && result.Result is bool ready && ready;
            }
            catch { return false; }
        }

        protected override async Task<bool> WaitForDetailPageAsync(AccountBrowserInstance browser, int timeoutMs, CancellationToken ct)
        {
            var start = DateTime.Now;
            while ((DateTime.Now - start).TotalMilliseconds < timeoutMs)
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    var result = await browser.Browser.EvaluateScriptAsync(
                        "(function(){var t=document.body.innerText||'';return t.indexOf('岗位描述')>=0||t.indexOf('职位描述')>=0;})();");
                    if (result.Success && result.Result is bool ready && ready) return true;
                }
                catch { }
                await Task.Delay(500, ct);
            }
            return false;
        }

        protected override Task<List<JobPosition>> FallbackReadJobsAsync(AccountBrowserInstance browser, string accountId, CancellationToken ct)
            => ExtractJobsFromText(browser, accountId, ct);
    }
}
