using System;
using System.Collections.Concurrent;
using System.IO;
using System.Windows;
using CefSharp;
using CefSharp.Wpf;

namespace RecruitAutomation.App.Helpers
{
    /// <summary>
    /// CefSharp 管理器（单例）
    /// 【关键】所有 Browser 实例必须通过此类创建和管理
    /// 防止 GC 回收导致的闪退
    /// </summary>
    public sealed class CefManager
    {
        private static readonly Lazy<CefManager> _instance = new(() => new CefManager());
        public static CefManager Instance => _instance.Value;
        
        private static readonly string LogDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "RecruitAutomation", "logs");
        
        // 【关键】静态引用所有 Browser 实例，防止 GC 回收
        private readonly ConcurrentDictionary<string, ChromiumWebBrowser> _browsers = new();
        
        private CefManager() { }
        
        /// <summary>
        /// CefSharp 是否已初始化
        /// </summary>
        public bool IsInitialized => Cef.IsInitialized;
        
        /// <summary>
        /// 创建浏览器实例（必须在 UI 线程调用）
        /// </summary>
        /// <param name="id">唯一标识符</param>
        /// <param name="initialUrl">初始 URL</param>
        /// <returns>浏览器实例</returns>
        public ChromiumWebBrowser? CreateBrowser(string id, string initialUrl = "about:blank")
        {
            try
            {
                if (!Cef.IsInitialized)
                {
                    WriteLog($"[CefManager] CreateBrowser 失败: CefSharp 未初始化");
                    return null;
                }
                
                // 检查是否在 UI 线程
                if (Application.Current?.Dispatcher != null && !Application.Current.Dispatcher.CheckAccess())
                {
                    WriteLog($"[CefManager] CreateBrowser 警告: 不在 UI 线程");
                    return null;
                }
                
                // 如果已存在，返回现有实例
                if (_browsers.TryGetValue(id, out var existing))
                {
                    WriteLog($"[CefManager] 返回已存在的 Browser: {id}");
                    return existing;
                }
                
                WriteLog($"[CefManager] 创建新 Browser: {id}, URL: {initialUrl}");
                
                var browser = new ChromiumWebBrowser(initialUrl);
                
                // 注册事件处理
                browser.LoadError += (s, e) =>
                {
                    if (e.ErrorCode != CefErrorCode.Aborted)
                    {
                        WriteLog($"[CefManager] Browser {id} 加载错误: {e.ErrorCode} - {e.ErrorText}");
                    }
                };
                
                browser.IsBrowserInitializedChanged += (s, e) =>
                {
                    if (browser.IsBrowserInitialized)
                    {
                        WriteLog($"[CefManager] Browser {id} 初始化完成");
                    }
                };
                
                // 【关键】保存到静态字典，防止 GC 回收
                _browsers[id] = browser;
                
                return browser;
            }
            catch (Exception ex)
            {
                WriteLog($"[CefManager] CreateBrowser 异常: {ex}");
                return null;
            }
        }
        
        /// <summary>
        /// 获取浏览器实例
        /// </summary>
        public ChromiumWebBrowser? GetBrowser(string id)
        {
            _browsers.TryGetValue(id, out var browser);
            return browser;
        }
        
        /// <summary>
        /// 安全关闭浏览器实例
        /// </summary>
        public void CloseBrowser(string id)
        {
            try
            {
                if (_browsers.TryRemove(id, out var browser))
                {
                    WriteLog($"[CefManager] 关闭 Browser: {id}");
                    
                    // 在 UI 线程执行关闭
                    if (Application.Current?.Dispatcher != null)
                    {
                        Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                        {
                            try
                            {
                                if (!browser.IsDisposed)
                                {
                                    browser.Dispose();
                                }
                            }
                            catch (Exception ex)
                            {
                                WriteLog($"[CefManager] Dispose Browser {id} 异常: {ex.Message}");
                            }
                        }));
                    }
                }
            }
            catch (Exception ex)
            {
                WriteLog($"[CefManager] CloseBrowser 异常: {ex.Message}");
            }
        }
        
        /// <summary>
        /// 关闭所有浏览器实例
        /// </summary>
        public void CloseAllBrowsers()
        {
            try
            {
                WriteLog($"[CefManager] 关闭所有 Browser，数量: {_browsers.Count}");
                
                foreach (var id in _browsers.Keys)
                {
                    CloseBrowser(id);
                }
            }
            catch (Exception ex)
            {
                WriteLog($"[CefManager] CloseAllBrowsers 异常: {ex.Message}");
            }
        }
        
        /// <summary>
        /// 获取当前管理的浏览器数量
        /// </summary>
        public int BrowserCount => _browsers.Count;
        
        private static void WriteLog(string message)
        {
            try
            {
                if (!Directory.Exists(LogDir))
                    Directory.CreateDirectory(LogDir);
                var logFile = Path.Combine(LogDir, "cef_manager.log");
                File.AppendAllText(logFile, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {message}\n");
            }
            catch { }
        }
    }
}
