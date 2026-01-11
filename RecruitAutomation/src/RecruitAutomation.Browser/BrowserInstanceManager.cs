using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CefSharp;
using RecruitAutomation.Core.License;

namespace RecruitAutomation.Browser
{
    /// <summary>
    /// 浏览器实例管理器 - 白图 AI 4.0 规范重构版
    /// 1. 严格账号隔离 (独立 CachePath)
    /// 2. 异步生命周期管理
    /// 3. 状态持久化支持
    /// </summary>
    public sealed class BrowserInstanceManager : IDisposable
    {
        private static readonly string DataRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "RecruitAutomation");
        
        private static readonly Lazy<BrowserInstanceManager> _instance = 
            new(() => new BrowserInstanceManager());

        private readonly ConcurrentDictionary<string, AccountBrowserInstance> _instances = new();
        private bool _disposed;

        public static BrowserInstanceManager Instance => _instance.Value;

        private BrowserInstanceManager() { }

        public AccountBrowserInstance? GetOrCreate(string accountId, string startUrl = "about:blank")
        {
            if (_disposed) return null;

            // 1. 授权检查
            if (!LicenseGuard.Instance.IsLicensed) return null;

            // 2. 实例复用
            if (_instances.TryGetValue(accountId, out var existing))
            {
                return existing;
            }

            // 3. 数量限制
            var max = LicenseGuard.Instance.CurrentLicense?.MaxAccounts ?? 1;
            if (_instances.Count >= max) return null;

            try
            {
                var instance = new AccountBrowserInstance(accountId);
                // 核心：每个账号独立的存储路径，实现彻底隔离
                var profilePath = Path.Combine(DataRoot, "Profiles", accountId);
                if (!Directory.Exists(profilePath)) Directory.CreateDirectory(profilePath);

                instance.Initialize(startUrl);
                
                if (_instances.TryAdd(accountId, instance))
                {
                    return instance;
                }
                else
                {
                    instance.Dispose();
                    return _instances[accountId];
                }
            }
            catch (Exception ex)
            {
                LogException(ex);
                return null;
            }
        }

        public async Task<bool> CloseAsync(string accountId)
        {
            if (_instances.TryRemove(accountId, out var instance))
            {
                return await Task.Run(() =>
                {
                    try
                    {
                        instance.Dispose();
                        return true;
                    }
                    catch
                    {
                        return false;
                    }
                });
            }
            return false;
        }

        public async Task RestartAsync(string accountId)
        {
            await CloseAsync(accountId);
            GetOrCreate(accountId);
        }

        private void LogException(Exception ex)
        {
            try
            {
                var logPath = Path.Combine(DataRoot, "logs", "browser_error.log");
                File.AppendAllText(logPath, $"[{DateTime.Now}] {ex.Message}\n{ex.StackTrace}\n");
            }
            catch { }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            foreach (var instance in _instances.Values)
            {
                try { instance.Dispose(); } catch { }
            }
            _instances.Clear();
        }
    }
}
