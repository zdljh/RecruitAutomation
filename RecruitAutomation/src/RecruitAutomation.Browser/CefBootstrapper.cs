using System;
using System.IO;
using CefSharp;

namespace RecruitAutomation.Browser
{
    /// <summary>
    /// CefSharp 引导器（简化版）
    /// 注意：CefSharp 的主要初始化已移至 App.xaml.cs 的 Main 函数中
    /// 这个类现在主要用于状态检查和关闭操作
    /// </summary>
    public static class CefBootstrapper
    {
        private static readonly string DataRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "RecruitAutomation");

        /// <summary>
        /// 是否已初始化
        /// </summary>
        public static bool IsInitialized => Cef.IsInitialized;

        /// <summary>
        /// 关闭 CefSharp
        /// </summary>
        public static void Shutdown()
        {
            if (!Cef.IsInitialized)
                return;

            try
            {
                LogInfo("正在关闭 CefSharp...");
                Cef.Shutdown();
                LogInfo("CefSharp 已关闭");
            }
            catch (Exception ex)
            {
                LogError("CefSharp 关闭时出错", ex);
            }
        }
        
        private static void LogInfo(string message)
        {
            try
            {
                var logPath = Path.Combine(DataRoot, "logs", "cef.log");
                var logDir = Path.GetDirectoryName(logPath);
                if (!string.IsNullOrEmpty(logDir) && !Directory.Exists(logDir))
                    Directory.CreateDirectory(logDir);
                File.AppendAllText(logPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [INFO] {message}\n");
            }
            catch { }
        }
        
        private static void LogError(string message, Exception? ex = null)
        {
            try
            {
                var logPath = Path.Combine(DataRoot, "logs", "cef_error.log");
                var logDir = Path.GetDirectoryName(logPath);
                if (!string.IsNullOrEmpty(logDir) && !Directory.Exists(logDir))
                    Directory.CreateDirectory(logDir);
                File.AppendAllText(logPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [ERROR] {message}\n{ex?.Message}\n{ex?.StackTrace}\n\n");
            }
            catch { }
        }
    }
}
