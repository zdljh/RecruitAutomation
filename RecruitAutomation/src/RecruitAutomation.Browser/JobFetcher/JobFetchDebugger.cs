using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using CefSharp;

namespace RecruitAutomation.Browser.JobFetcher
{
    /// <summary>
    /// 岗位读取调试器
    /// 提供完整的可视化调试功能
    /// </summary>
    public class JobFetchDebugger
    {
        private static readonly string DataRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RecruitAutomation");
        private static readonly string LogPath = Path.Combine(DataRoot, "logs", "debug.log");
        private static readonly string DiagnosticPath = Path.Combine(DataRoot, "diagnostics");

        private DebugBrowserWindow? _debugWindow;
        private readonly object _lock = new();

        /// <summary>
        /// 是否启用调试模式
        /// </summary>
        public bool IsDebugMode { get; set; } = false;

        /// <summary>
        /// 调试模式下是否暂停自动操作
        /// </summary>
        public bool PauseForManualObservation { get; set; } = false;

        /// <summary>
        /// 当前诊断结果
        /// </summary>
        public PageDiagnosticResult? LastDiagnostic { get; private set; }

        public JobFetchDebugger()
        {
            EnsureDirectories();
        }

        /// <summary>
        /// 开始调试会话
        /// </summary>
        public void StartDebugSession(AccountBrowserInstance browser, string accountId)
        {
            if (!IsDebugMode) return;

            Application.Current.Dispatcher.Invoke(() =>
            {
                lock (_lock)
                {
                    // 关闭旧窗口
                    _debugWindow?.Close();

                    // 创建新的调试窗口
                    _debugWindow = new DebugBrowserWindow(browser.Browser, accountId);
                    _debugWindow.Show();
                    _debugWindow.Activate();
                }
            });

            Log($"[DEBUG] 开始调试会话: {accountId}");
        }

        /// <summary>
        /// 结束调试会话
        /// </summary>
        public void EndDebugSession()
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                lock (_lock)
                {
                    _debugWindow?.Close();
                    _debugWindow = null;
                }
            });
        }

        /// <summary>
        /// 更新步骤显示
        /// </summary>
        public void UpdateStep(int current, int total, string description)
        {
            if (!IsDebugMode || _debugWindow == null) return;

            _debugWindow.UpdateStep(current, total, description);
            Log($"[STEP {current}/{total}] {description}");
        }

        /// <summary>
        /// 诊断当前页面类型
        /// </summary>
        public async Task<PageDiagnosticResult> DiagnosePageAsync(AccountBrowserInstance browser, CancellationToken ct = default)
        {
            var result = new PageDiagnosticResult
            {
                Timestamp = DateTime.Now,
                Url = browser.CurrentUrl ?? "unknown"
            };

            try
            {
                // 1. 检查URL
                var url = browser.CurrentUrl?.ToLower() ?? "";
                result.Url = browser.CurrentUrl ?? "";

                Log($"[DIAGNOSE] URL: {result.Url}");

                // URL判断
                if (url.Contains("/login") || url.Contains("/signin"))
                {
                    result.PageType = PageDiagnosticType.LoginPage;
                    result.Detail = "URL包含登录路径";
                }
                else if (url.Contains("/security") || url.Contains("/verify") || url.Contains("/captcha"))
                {
                    result.PageType = PageDiagnosticType.RiskControl;
                    result.Detail = "URL包含安全验证路径";
                }
                else if (url.Contains("/job/list") || url.Contains("/web/chat/job"))
                {
                    // 可能是正常页面，需要进一步检查内容
                    result.PageType = PageDiagnosticType.Unknown;
                }

                // 2. 检查页面内容
                var pageInfo = await GetPageInfoAsync(browser);
                result.PageText = pageInfo.Text;
                result.VisibleText = pageInfo.Text?.Length > 500 ? pageInfo.Text.Substring(0, 500) + "..." : pageInfo.Text;

                if (pageInfo.Text != null)
                {
                    var text = pageInfo.Text;

                    // 检测登录页
                    if (text.Contains("请登录") || text.Contains("登录账号") || text.Contains("手机号登录"))
                    {
                        result.PageType = PageDiagnosticType.LoginPage;
                        result.Detail = "页面包含登录提示";
                    }
                    // 检测风控页
                    else if (text.Contains("安全验证") || text.Contains("滑动验证") || text.Contains("请完成验证") ||
                             text.Contains("操作频繁") || text.Contains("访问受限") || text.Contains("异常"))
                    {
                        result.PageType = PageDiagnosticType.RiskControl;
                        result.Detail = "页面包含风控/验证提示";
                    }
                    // 检测加载中
                    else if (text.Length < 100 || text.Contains("加载中") || text.Contains("loading"))
                    {
                        result.PageType = PageDiagnosticType.Loading;
                        result.Detail = "页面内容过少或正在加载";
                    }
                    // 检测空状态
                    else if (text.Contains("暂无职位") || text.Contains("没有职位") || text.Contains("去发布"))
                    {
                        result.PageType = PageDiagnosticType.EmptyState;
                        result.Detail = "页面显示暂无职位";
                    }
                    // 检测正常职位页
                    else if (text.Contains("开放中") || text.Contains("职位管理") || text.Contains("招聘中"))
                    {
                        result.PageType = PageDiagnosticType.NormalJobList;
                        result.Detail = "检测到职位相关内容";

                        // 统计可见岗位数
                        result.VisibleJobCount = CountOccurrences(text, "开放中");
                    }
                }

                // 3. 截图保存
                result.ScreenshotPath = await SaveDiagnosticScreenshotAsync(browser);

                // 更新调试窗口
                _debugWindow?.UpdatePageType(result.PageType, result.Detail);

                // 保存诊断结果
                LastDiagnostic = result;
                SaveDiagnosticResult(result);

                Log($"[DIAGNOSE] 结果: {result.PageType} - {result.Detail}");
                Log($"[DIAGNOSE] 可见岗位数: {result.VisibleJobCount}");
            }
            catch (Exception ex)
            {
                result.PageType = PageDiagnosticType.Unknown;
                result.Detail = $"诊断异常: {ex.Message}";
                Log($"[DIAGNOSE] 异常: {ex.Message}");
            }

            return result;
        }

        /// <summary>
        /// 显示错误
        /// </summary>
        public void ShowError(string message)
        {
            _debugWindow?.ShowError(message);
            Log($"[ERROR] {message}");
        }

        /// <summary>
        /// 高亮区域
        /// </summary>
        public void HighlightArea(int x, int y, int width, int height, string label)
        {
            _debugWindow?.HighlightArea(x, y, width, height, label);
        }

        /// <summary>
        /// 清除高亮
        /// </summary>
        public void ClearHighlight()
        {
            _debugWindow?.ClearHighlight();
        }

        /// <summary>
        /// 等待用户确认（调试模式下暂停）
        /// </summary>
        public async Task WaitForUserConfirmationAsync(string message, CancellationToken ct)
        {
            if (!IsDebugMode || !PauseForManualObservation) return;

            var result = MessageBox.Show(
                message + "\n\nPlease observe the browser window. Click Yes to continue, No to cancel.",
                "Debug Mode - Manual Confirmation",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.No)
            {
                throw new OperationCanceledException("User cancelled operation");
            }

            await Task.Delay(500, ct);
        }

        #region 私有方法

        private async Task<(string? Text, string? Html)> GetPageInfoAsync(AccountBrowserInstance browser)
        {
            try
            {
                var result = await browser.Browser.EvaluateScriptAsync(
                    "(function(){return JSON.stringify({text:document.body.innerText||'',html:document.body.innerHTML?.substring(0,2000)||''});})();");

                if (result.Success && result.Result != null)
                {
                    var json = result.Result.ToString();
                    if (!string.IsNullOrEmpty(json))
                    {
                        var doc = JsonDocument.Parse(json);
                        return (
                            doc.RootElement.GetProperty("text").GetString(),
                            doc.RootElement.GetProperty("html").GetString()
                        );
                    }
                }
            }
            catch { }
            return (null, null);
        }

        private async Task<string?> SaveDiagnosticScreenshotAsync(AccountBrowserInstance browser)
        {
            try
            {
                var devTools = browser.Browser.GetDevToolsClient();
                if (devTools == null) return null;

                var result = await devTools.Page.CaptureScreenshotAsync();
                if (result?.Data == null) return null;

                var fileName = $"diag_{DateTime.Now:yyyyMMdd_HHmmss}.png";
                var filePath = Path.Combine(DiagnosticPath, fileName);
                
                File.WriteAllBytes(filePath, result.Data);
                
                Log($"[SCREENSHOT] 保存诊断截图: {filePath}");
                return filePath;
            }
            catch (Exception ex)
            {
                Log($"[SCREENSHOT] 截图失败: {ex.Message}");
                return null;
            }
        }

        private void SaveDiagnosticResult(PageDiagnosticResult result)
        {
            try
            {
                var fileName = $"diag_{DateTime.Now:yyyyMMdd_HHmmss}.json";
                var filePath = Path.Combine(DiagnosticPath, fileName);
                
                var json = JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(filePath, json);
            }
            catch { }
        }

        private int CountOccurrences(string text, string pattern)
        {
            int count = 0;
            int index = 0;
            while ((index = text.IndexOf(pattern, index, StringComparison.Ordinal)) != -1)
            {
                count++;
                index += pattern.Length;
            }
            return count;
        }

        private void Log(string message)
        {
            try
            {
                var logMessage = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}\n";
                File.AppendAllText(LogPath, logMessage);
                System.Diagnostics.Debug.WriteLine(logMessage);
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
                if (!Directory.Exists(DiagnosticPath))
                    Directory.CreateDirectory(DiagnosticPath);
            }
            catch { }
        }

        #endregion
    }

    /// <summary>
    /// 页面诊断结果
    /// </summary>
    public class PageDiagnosticResult
    {
        public DateTime Timestamp { get; set; }
        public string Url { get; set; } = "";
        public PageDiagnosticType PageType { get; set; } = PageDiagnosticType.Unknown;
        public string Detail { get; set; } = "";
        public string? PageText { get; set; }
        public string? VisibleText { get; set; }
        public int VisibleJobCount { get; set; }
        public string? ScreenshotPath { get; set; }

        /// <summary>
        /// 是否可以继续读取岗位
        /// </summary>
        public bool CanProceed => PageType == PageDiagnosticType.NormalJobList;

        /// <summary>
        /// 获取建议操作
        /// </summary>
        public string GetSuggestion()
        {
            return PageType switch
            {
                PageDiagnosticType.LoginPage => "请在浏览器中手动登录后重试",
                PageDiagnosticType.RiskControl => "账号触发风控，请手动完成验证或等待一段时间后重试",
                PageDiagnosticType.Loading => "页面正在加载，请等待几秒后重试",
                PageDiagnosticType.EmptyState => "该账号暂无发布的职位",
                PageDiagnosticType.NormalJobList => "页面正常，可以读取岗位",
                _ => "无法识别页面类型，请手动检查浏览器窗口"
            };
        }
    }
}
