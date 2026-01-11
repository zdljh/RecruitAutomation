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
        private static readonly string LogPath = Path.Combine(DataRoot, "logs", "visual_browser.log");
        private static readonly string ScreenshotPath = Path.Combine(DataRoot, "screenshots");

        private readonly HumanBehaviorSimulator _humanBehavior;
        private readonly AIVisionAnalyzer _visionAnalyzer;
        private readonly JobFetchDebugger _debugger;
        private readonly Random _random = new();

        public bool DebugMode
        {
            get => _debugger.IsDebugMode;
            set => _debugger.IsDebugMode = value;
        }

        public bool PauseForManualObservation
        {
            get => _debugger.PauseForManualObservation;
            set => _debugger.PauseForManualObservation = value;
        }

        public VisualJobBrowser(HumanBehaviorSimulator? humanBehavior = null, AIVisionAnalyzer? visionAnalyzer = null)
        {
            _humanBehavior = humanBehavior ?? new HumanBehaviorSimulator();
            _visionAnalyzer = visionAnalyzer ?? new AIVisionAnalyzer();
            _debugger = new JobFetchDebugger();
            EnsureDirectories();
        }

        public void UpdateAIConfig(string apiKey, string baseUrl, string model)
            => _visionAnalyzer.UpdateConfig(apiKey, baseUrl, model);

        public async Task<VisualBrowseResult> BrowseJobsAsync(
            AccountBrowserInstance browser, string accountId, CancellationToken ct = default)
        {
            var result = new VisualBrowseResult { AccountId = accountId };

            if (browser?.Browser == null)
            {
                result.Status = BrowseStatus.Error;
                result.Reason = "Browser instance is null";
                result.Stage = "init";
                return result;
            }

            try
            {
                _debugger.StartDebugSession(browser, accountId);

                Log($"Starting job browse for: {accountId}");

                _debugger.UpdateStep(1, 6, "Waiting for page to stabilize...");
                await Task.Delay(_random.Next(2000, 3500), ct);

                _debugger.UpdateStep(2, 6, "Diagnosing page type...");
                var diagnostic = await _debugger.DiagnosePageAsync(browser, ct);
                
                result.Stage = "diagnose";
                result.PageType = diagnostic.PageType.ToString();
                result.CurrentUrl = diagnostic.Url;

                Log($"Page diagnosis: {diagnostic.PageType} - {diagnostic.Detail}");

                if (diagnostic.PageType == PageDiagnosticType.LoginPage)
                {
                    result.Status = BrowseStatus.NeedLogin;
                    result.Reason = "Login page detected";
                    result.Suggest = diagnostic.GetSuggestion();
                    return result;
                }

                if (diagnostic.PageType == PageDiagnosticType.RiskControl)
                {
                    result.Status = BrowseStatus.Blocked;
                    result.Reason = "Risk control page detected";
                    result.Suggest = diagnostic.GetSuggestion();
                    return result;
                }

                if (diagnostic.PageType == PageDiagnosticType.Loading)
                {
                    _debugger.UpdateStep(2, 6, "Page loading, waiting...");
                    await Task.Delay(3000, ct);
                    diagnostic = await _debugger.DiagnosePageAsync(browser, ct);
                }

                if (diagnostic.PageType == PageDiagnosticType.EmptyState)
                {
                    result.Status = BrowseStatus.Success;
                    result.Reason = "No jobs published for this account";
                    result.TotalJobsFound = 0;
                    return result;
                }

                await _debugger.WaitForUserConfirmationAsync(
                    $"Page diagnosis complete\n\nType: {diagnostic.PageType}\nDetail: {diagnostic.Detail}\nVisible jobs: {diagnostic.VisibleJobCount}\n\nContinue?", ct);

                _debugger.UpdateStep(3, 6, "Capturing screenshot...");
                var screenshot = await CaptureScreenshotAsync(browser, "job_list");
                
                if (screenshot == null)
                {
                    result.Status = BrowseStatus.Error;
                    result.Reason = "Screenshot capture failed";
                    result.Stage = "screenshot";
                    return result;
                }

                _debugger.UpdateStep(4, 6, "AI vision analysis...");
                var analysis = await _visionAnalyzer.AnalyzeJobListScreenAsync(screenshot, ct);
                
                result.Stage = "ai_analysis";

                if (!analysis.Success)
                {
                    result.Status = BrowseStatus.Error;
                    result.Reason = $"AI analysis failed: {analysis.ErrorMessage}";
                    return result;
                }

                if (analysis.NeedLogin)
                {
                    result.Status = BrowseStatus.NeedLogin;
                    result.Reason = "AI detected login required";
                    return result;
                }

                if (analysis.JobCards.Count == 0)
                {
                    _debugger.UpdateStep(5, 6, "No jobs found, scrolling...");
                    await SimulateHumanScroll(browser, ct);
                    await Task.Delay(_random.Next(1500, 2500), ct);
                    
                    screenshot = await CaptureScreenshotAsync(browser, "scroll");
                    if (screenshot != null)
                    {
                        analysis = await _visionAnalyzer.AnalyzeJobListScreenAsync(screenshot, ct);
                    }
                }

                _debugger.UpdateStep(6, 6, "Processing results...");

                if (analysis.JobCards.Count == 0)
                {
                    result.Status = BrowseStatus.Blocked;
                    result.Reason = "AI could not detect any job cards";
                    result.Suggest = "Check: 1. Page displays jobs correctly 2. Not blocked by anti-crawler 3. Screenshot saved correctly";
                    result.Stage = "no_jobs_detected";
                    
                    result.DiagnosticInfo = new DiagnosticInfo
                    {
                        PageDiagnosis = diagnostic.PageType.ToString(),
                        VisibleJobCount = diagnostic.VisibleJobCount,
                        AIDetectedCount = 0,
                        ScreenshotPath = diagnostic.ScreenshotPath,
                        PageTextSample = diagnostic.VisibleText
                    };
                    
                    return result;
                }

                result.TotalJobsFound = analysis.JobCards.Count;
                
                foreach (var card in analysis.JobCards)
                {
                    if (DebugMode)
                    {
                        _debugger.HighlightArea(card.CenterX - 100, card.CenterY - 30, 200, 60, card.Title);
                        await Task.Delay(500, ct);
                    }

                    if (IsJobOpen(card.StatusText))
                    {
                        result.OpenJobsCount++;
                        result.Jobs.Add(CreateJobFromCard(card, accountId));
                        Log($"Found open job: {card.Title} - {card.StatusText}");
                    }
                }

                _debugger.ClearHighlight();

                result.Status = result.Jobs.Count > 0 ? BrowseStatus.Success : BrowseStatus.PartialSuccess;
                result.Stage = "completed";

                Log($"Browse complete: Total={result.TotalJobsFound}, Open={result.OpenJobsCount}");
            }
            catch (OperationCanceledException)
            {
                result.Status = BrowseStatus.Cancelled;
                result.Reason = "Operation cancelled";
                result.Stage = "cancelled";
            }
            catch (Exception ex)
            {
                result.Status = BrowseStatus.Error;
                result.Reason = ex.Message;
                result.Stage = "exception";
                Log($"Exception: {ex}");
            }
            finally
            {
                if (!DebugMode)
                {
                    _debugger.EndDebugSession();
                }
            }

            return result;
        }

        private async Task SimulateHumanScroll(AccountBrowserInstance browser, CancellationToken ct)
        {
            for (int i = 0; i < 3; i++)
            {
                ct.ThrowIfCancellationRequested();
                var scrollY = _random.Next(200, 400);
                await browser.Browser.EvaluateScriptAsync($"window.scrollBy(0,{scrollY})");
                await Task.Delay(_random.Next(400, 800), ct);
            }
            await browser.Browser.EvaluateScriptAsync("window.scrollTo(0,0)");
            await Task.Delay(_random.Next(500, 1000), ct);
        }

        private async Task<byte[]?> CaptureScreenshotAsync(AccountBrowserInstance browser, string name)
        {
            try
            {
                var devTools = browser.Browser.GetDevToolsClient();
                if (devTools == null) return await FallbackCaptureAsync(browser, name);

                var result = await devTools.Page.CaptureScreenshotAsync();
                if (result?.Data == null) return await FallbackCaptureAsync(browser, name);

                byte[] bytes = result.Data;
                SaveScreenshot(bytes, name);
                return bytes;
            }
            catch
            {
                return await FallbackCaptureAsync(browser, name);
            }
        }

        private async Task<byte[]?> FallbackCaptureAsync(AccountBrowserInstance browser, string name)
        {
            try
            {
                var result = await browser.Browser.EvaluateScriptAsync(
                    "(function(){return JSON.stringify({text:document.body.innerText||'',url:location.href});})();");
                if (result.Success && result.Result != null)
                {
                    var text = result.Result.ToString() ?? "";
                    return System.Text.Encoding.UTF8.GetBytes(text);
                }
            }
            catch { }
            return null;
        }

        private void SaveScreenshot(byte[] data, string name)
        {
            try
            {
                var path = Path.Combine(ScreenshotPath, $"{DateTime.Now:yyyyMMdd_HHmmss}_{name}.png");
                File.WriteAllBytes(path, data);
            }
            catch { }
        }

        private bool IsJobOpen(string? status)
        {
            if (string.IsNullOrEmpty(status)) return false;
            return status.Contains("开放") || status.Contains("招聘中") || status.Contains("在招");
        }

        private JobPosition CreateJobFromCard(VisualJobCard card, string accountId) => new()
        {
            Platform = RecruitPlatform.Boss,
            AccountId = accountId,
            PlatformJobId = $"boss_{card.Title.GetHashCode():X}_{DateTime.Now.Ticks}",
            Id = $"boss_{accountId}_{card.Title.GetHashCode():X}",
            Title = card.Title,
            SalaryText = card.SalaryText,
            Location = card.Location ?? "",
            ExperienceRequired = card.Experience ?? "",
            EducationRequired = card.Education ?? "",
            Status = JobStatus.Open,
            FetchedAt = DateTime.UtcNow
        };

        private void Log(string msg)
        {
            try
            {
                File.AppendAllText(LogPath, $"[{DateTime.Now:HH:mm:ss}] {msg}\n");
                System.Diagnostics.Debug.WriteLine($"[VisualJobBrowser] {msg}");
            }
            catch { }
        }

        private void EnsureDirectories()
        {
            try
            {
                var logDir = Path.GetDirectoryName(LogPath);
                if (!string.IsNullOrEmpty(logDir) && !Directory.Exists(logDir))
                    Directory.CreateDirectory(logDir);
                if (!Directory.Exists(ScreenshotPath))
                    Directory.CreateDirectory(ScreenshotPath);
            }
            catch { }
        }
    }
}
