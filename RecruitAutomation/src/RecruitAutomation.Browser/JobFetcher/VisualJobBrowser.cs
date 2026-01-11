using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using CefSharp;
using RecruitAutomation.Browser.HumanBehavior;
using RecruitAutomation.Core.Models;

namespace RecruitAutomation.Browser.JobFetcher
{
    public class VisualJobBrowser
    {
        private static readonly string DataRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RecruitAutomation");
        private static readonly string DebugDir = Path.Combine(DataRoot, "debug");
        private static readonly string LogPath = Path.Combine(DataRoot, "logs", "visual_browser.log");

        private readonly AIVisionAnalyzer _visionAnalyzer;
        private readonly Random _random = new();

        public bool DebugMode { get; set; } = true;

        public VisualJobBrowser(HumanBehaviorSimulator? humanBehavior = null, AIVisionAnalyzer? visionAnalyzer = null)
        {
            _visionAnalyzer = visionAnalyzer ?? new AIVisionAnalyzer();
            EnsureDirectories();
        }

        public void UpdateAIConfig(string apiKey, string baseUrl, string model)
            => _visionAnalyzer.UpdateConfig(apiKey, baseUrl, model);

        public async Task<VisualBrowseResult> BrowseJobsAsync(
            AccountBrowserInstance browser, string accountId, CancellationToken ct = default)
        {
            var result = new VisualBrowseResult { AccountId = accountId, Jobs = new List<JobPosition>() };

            try
            {
                Log(">>> 开始 AI 图像识别岗位流程 <<<");
                
                // 1. 等待页面稳定
                await Task.Delay(_random.Next(2000, 3000), ct);

                // 2. 截图采集
                Log("正在采集浏览器视口截图...");
                var screenshot = await CaptureViewportScreenshotAsync(browser);
                if (screenshot == null)
                {
                    result.Status = BrowseStatus.Error;
                    result.Reason = "截图采集失败";
                    return result;
                }

                // 3. 保存调试截图
                var debugPath = SaveDebugScreenshot(screenshot);
                Log($"调试截图已保存: {debugPath}");

                // 4. AI 视觉分析
                Log("正在调用 AI 视觉模型进行结构化识别...");
                var analysis = await _visionAnalyzer.AnalyzeJobListScreenAsync(screenshot, ct);
                
                if (!analysis.Success)
                {
                    result.Status = BrowseStatus.Error;
                    result.Reason = $"AI 识别失败: {analysis.ErrorMessage}";
                    Log(result.Reason);
                    return result;
                }

                // 5. 结果处理
                Log($"AI 识别完成。识别到岗位数量: {analysis.JobCards.Count}");
                result.TotalJobsFound = analysis.JobCards.Count;

                foreach (var card in analysis.JobCards)
                {
                    var job = new JobPosition
                    {
                        Platform = RecruitPlatform.Boss,
                        AccountId = accountId,
                        Title = card.Title,
                        SalaryText = card.SalaryText,
                        Location = card.Location ?? "未知",
                        ExperienceRequired = card.Experience ?? "不限",
                        EducationRequired = card.Education ?? "不限",
                        Status = JobStatus.Open,
                        FetchedAt = DateTime.UtcNow,
                        Id = $"boss_{accountId}_{Guid.NewGuid():N}"
                    };
                    result.Jobs.Add(job);
                }

                if (result.Jobs.Count > 0)
                {
                    var first = result.Jobs[0];
                    Log($"首条识别结果: {first.Title} | {first.SalaryText} | {first.Location}");
                }

                result.Status = BrowseStatus.Success;
                Log(">>> AI 图像识别流程结束 <<<");
            }
            catch (Exception ex)
            {
                result.Status = BrowseStatus.Error;
                result.Reason = $"识别过程发生异常: {ex.Message}";
                Log(result.Reason);
            }

            return result;
        }

        private async Task<byte[]?> CaptureViewportScreenshotAsync(AccountBrowserInstance browser)
        {
            try
            {
                var devTools = browser.Browser.GetDevToolsClient();
                var response = await devTools.Page.CaptureScreenshotAsync(
                    format: CefSharp.DevTools.Page.CaptureScreenshotFormat.Png);
                return response.Data;
            }
            catch (Exception ex)
            {
                Log($"截图异常: {ex.Message}");
                return null;
            }
        }

        private string SaveDebugScreenshot(byte[] data)
        {
            try
            {
                var fileName = $"debug_{DateTime.Now:yyyyMMdd_HHmmss}.png";
                var path = Path.Combine(DebugDir, fileName);
                File.WriteAllBytes(path, data);
                return path;
            }
            catch { return "save_failed"; }
        }

        private void Log(string msg)
        {
            try
            {
                var logMsg = $"[{DateTime.Now:HH:mm:ss}] {msg}\n";
                File.AppendAllText(LogPath, logMsg);
                System.Diagnostics.Debug.WriteLine($"[VisualJobBrowser] {msg}");
            }
            catch { }
        }

        private void EnsureDirectories()
        {
            try
            {
                if (!Directory.Exists(DebugDir)) Directory.CreateDirectory(DebugDir);
                var logDir = Path.GetDirectoryName(LogPath);
                if (!Directory.Exists(logDir)) Directory.CreateDirectory(logDir);
            }
            catch { }
        }

        public bool PauseForManualObservation { get; set; } = false;
    }

    /// <summary>
    /// 视觉浏览结果
    /// </summary>
    public class VisualBrowseResult
    {
        public string AccountId { get; set; } = string.Empty;
        public BrowseStatus Status { get; set; } = BrowseStatus.Unknown;
        public string? Reason { get; set; }
        public List<JobPosition> Jobs { get; set; } = new();
        public int TotalJobsFound { get; set; }
    }

    /// <summary>
    /// 浏览状态
    /// </summary>
    public enum BrowseStatus
    {
        Unknown,
        Success,
        Error,
        NeedLogin,
        Timeout
    }
}
