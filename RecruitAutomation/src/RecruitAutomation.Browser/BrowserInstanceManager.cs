using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CefSharp;
using RecruitAutomation.Core.License;

namespace RecruitAutomation.Browser
{
    /// <summary>
    /// 浏览器实例管理器
    /// 管理所有账号的浏览器实例，支持多账号同时运行
    /// </summary>
    public sealed class BrowserInstanceManager : IDisposable
    {
        // 使用 LocalApplicationData 作为根目录（绝对路径）
        private static readonly string DataRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "RecruitAutomation");
        
        private static readonly Lazy<BrowserInstanceManager> _instance = 
            new(() => new BrowserInstanceManager());

        private readonly ConcurrentDictionary<string, AccountBrowserInstance> _instances = new();
        private bool _disposed;

        /// <summary>
        /// 获取单例实例
        /// </summary>
        public static BrowserInstanceManager Instance => _instance.Value;

        private BrowserInstanceManager()
        {
            // 不在构造函数中初始化 CefSharp
        }

        /// <summary>
        /// 当前运行的实例数量
        /// </summary>
        public int RunningCount => _instances.Count;

        /// <summary>
        /// 获取所有运行中的账号 ID
        /// </summary>
        public IEnumerable<string> RunningAccountIds => _instances.Keys.ToList();

        /// <summary>
        /// 获取最大允许的账号数量
        /// </summary>
        public int MaxAllowedAccounts
        {
            get
            {
                var license = LicenseGuard.Instance.CurrentLicense;
                return license?.MaxAccounts ?? 1;
            }
        }

        /// <summary>
        /// 创建或获取账号浏览器实例
        /// 【防闪退改造】不抛异常，返回 null 表示失败
        /// </summary>
        /// <param name="accountId">账号 ID</param>
        /// <param name="startUrl">初始 URL</param>
        /// <returns>浏览器实例，失败返回 null</returns>
        public AccountBrowserInstance? GetOrCreate(string accountId, string startUrl = "about:blank")
        {
            try
            {
                if (_disposed)
                {
                    LogError("BrowserInstanceManager 已释放");
                    return null;
                }
                
                // 【改造】使用不抛异常的授权检查
                if (!LicenseGuard.Instance.TryEnsureLicensed(out var licenseError))
                {
                    LogError($"授权检查失败: {licenseError}");
                    return null;
                }

                if (string.IsNullOrWhiteSpace(accountId))
                {
                    LogError("账号ID不能为空");
                    return null;
                }

                // 检查是否已存在
                if (_instances.TryGetValue(accountId, out var existing))
                {
                    if (!existing.IsInitialized)
                    {
                        try
                        {
                            existing.Initialize(startUrl);
                        }
                        catch (Exception ex)
                        {
                            LogError($"初始化现有实例失败: {ex.Message}");
                            _instances.TryRemove(accountId, out _);
                            try { existing.Dispose(); } catch { }
                            return null;
                        }
                    }
                    return existing;
                }

                // 检查账号数量限制
                if (_instances.Count >= MaxAllowedAccounts)
                {
                    LogError($"已达到最大账号数量限制 ({MaxAllowedAccounts})");
                    return null;
                }

                // 确保 CefSharp 已初始化
                if (!Cef.IsInitialized)
                {
                    LogError("CefSharp 未初始化");
                    return null;
                }

                // 创建新实例
                AccountBrowserInstance? instance = null;
                try
                {
                    instance = new AccountBrowserInstance(accountId);
                    instance.Initialize(startUrl);

                    if (!_instances.TryAdd(accountId, instance))
                    {
                        try { instance.Dispose(); } catch { }
                        _instances.TryGetValue(accountId, out var existingInstance);
                        return existingInstance;
                    }
                    
                    LogInfo($"创建浏览器实例成功: {accountId}");
                    return instance;
                }
                catch (Exception ex)
                {
                    LogError($"创建浏览器实例失败: {ex.Message}\n{ex.StackTrace}");
                    try { instance?.Dispose(); } catch { }
                    return null;
                }
            }
            catch (Exception ex)
            {
                LogError($"GetOrCreate 异常: {ex.Message}");
                return null;
            }
        }
        
        private void LogInfo(string message)
        {
            try
            {
                var logPath = Path.Combine(DataRoot, "logs", "browser_manager.log");
                var logDir = Path.GetDirectoryName(logPath);
                if (!string.IsNullOrEmpty(logDir) && !Directory.Exists(logDir))
                    Directory.CreateDirectory(logDir);
                File.AppendAllText(logPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [INFO] {message}\n");
            }
            catch { }
        }
        
        private void LogError(string message)
        {
            try
            {
                var logPath = Path.Combine(DataRoot, "logs", "browser_manager_error.log");
                var logDir = Path.GetDirectoryName(logPath);
                if (!string.IsNullOrEmpty(logDir) && !Directory.Exists(logDir))
                    Directory.CreateDirectory(logDir);
                File.AppendAllText(logPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [ERROR] {message}\n");
            }
            catch { }
        }

        /// <summary>
        /// 获取指定账号的浏览器实例
        /// </summary>
        public AccountBrowserInstance? Get(string accountId)
        {
            ThrowIfDisposed();
            _instances.TryGetValue(accountId, out var instance);
            return instance;
        }

        /// <summary>
        /// 检查账号是否正在运行
        /// </summary>
        public bool IsRunning(string accountId)
        {
            return _instances.ContainsKey(accountId);
        }

        /// <summary>
        /// 关闭指定账号的浏览器实例
        /// </summary>
        public bool Close(string accountId)
        {
            ThrowIfDisposed();

            if (_instances.TryRemove(accountId, out var instance))
            {
                try
                {
                    instance.Dispose();
                    LogInfo($"关闭浏览器实例: {accountId}");
                }
                catch (Exception ex)
                {
                    LogError($"关闭浏览器实例时出错: {ex.Message}");
                }
                return true;
            }
            return false;
        }

        /// <summary>
        /// 关闭所有浏览器实例
        /// </summary>
        public void CloseAll()
        {
            ThrowIfDisposed();

            foreach (var accountId in _instances.Keys.ToList())
            {
                Close(accountId);
            }
        }

        /// <summary>
        /// 获取所有运行中的实例
        /// </summary>
        public IEnumerable<AccountBrowserInstance> GetAllInstances()
        {
            return _instances.Values.ToList();
        }

        /// <summary>
        /// 批量启动账号
        /// </summary>
        /// <param name="accountIds">账号 ID 列表</param>
        /// <param name="startUrl">初始 URL</param>
        /// <returns>成功启动的实例列表</returns>
        public List<AccountBrowserInstance> StartMultiple(IEnumerable<string> accountIds, string startUrl = "about:blank")
        {
            ThrowIfDisposed();
            
            // 【改造】使用不抛异常的授权检查
            if (!LicenseGuard.Instance.TryEnsureLicensed(out var licenseError))
            {
                LogError($"批量启动授权检查失败: {licenseError}");
                return new List<AccountBrowserInstance>();
            }

            var results = new List<AccountBrowserInstance>();
            var remaining = MaxAllowedAccounts - _instances.Count;

            foreach (var accountId in accountIds)
            {
                if (remaining <= 0)
                    break;

                if (_instances.ContainsKey(accountId))
                {
                    results.Add(_instances[accountId]);
                    continue;
                }

                try
                {
                    var instance = GetOrCreate(accountId, startUrl);
                    results.Add(instance);
                    remaining--;
                }
                catch
                {
                    // 忽略单个账号启动失败
                }
            }

            return results;
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(BrowserInstanceManager));
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            CloseAll();
        }
    }
}
