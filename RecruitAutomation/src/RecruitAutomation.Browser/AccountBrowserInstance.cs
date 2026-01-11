using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using CefSharp;
using CefSharp.Wpf;

namespace RecruitAutomation.Browser
{
    public sealed class AccountBrowserInstance : IDisposable
    {
        private static readonly string DataRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "RecruitAutomation");
        
        private readonly string _accountId;
        private readonly string _userDataDir;
        private readonly string _cachePath;
        private ChromiumWebBrowser? _browser;
        private IRequestContext? _requestContext;
        private bool _disposed;

        public string AccountId => _accountId;
        public string UserDataDir => _userDataDir;
        public ChromiumWebBrowser? Browser => _browser;
        public bool IsInitialized => _browser != null;
        public bool IsLoading => _browser?.IsLoading ?? false;
        public string CurrentUrl => _browser?.Address ?? string.Empty;

        public event EventHandler<string>? UrlChanged;
        public event EventHandler<string>? TitleChanged;
        public event EventHandler<bool>? LoadingStateChanged;
        public event EventHandler<string>? ConsoleMessage;
        public event EventHandler<string>? BrowserCrashed;

        public AccountBrowserInstance(string accountId)
        {
            if (string.IsNullOrWhiteSpace(accountId))
                throw new ArgumentNullException(nameof(accountId));

            _accountId = accountId;
            _userDataDir = Path.Combine(DataRoot, "accounts", accountId, "browser_data");
            _cachePath = Path.Combine(_userDataDir, "cache");
            EnsureDirectoriesExist();
        }

        private void EnsureDirectoriesExist()
        {
            try
            {
                if (!Directory.Exists(_userDataDir)) Directory.CreateDirectory(_userDataDir);
                if (!Directory.Exists(_cachePath)) Directory.CreateDirectory(_cachePath);
            }
            catch { }
        }

        public void Initialize(string startUrl = "about:blank")
        {
            if (_disposed) throw new ObjectDisposedException(nameof(AccountBrowserInstance));
            if (_browser != null) return;

            if (!Cef.IsInitialized)
                throw new InvalidOperationException("CefSharp not initialized");

            var settings = new RequestContextSettings
            {
                CachePath = _cachePath,
                PersistSessionCookies = true,
                PersistUserPreferences = true
            };

            _requestContext = new RequestContext(settings);
            _browser = new ChromiumWebBrowser(startUrl) { RequestContext = _requestContext };
            _browser.AddressChanged += (s, e) => UrlChanged?.Invoke(this, e.NewValue?.ToString() ?? "");
            _browser.TitleChanged += (s, e) => TitleChanged?.Invoke(this, e.NewValue?.ToString() ?? "");
            _browser.LoadingStateChanged += (s, e) => LoadingStateChanged?.Invoke(this, e.IsLoading);
            _browser.ConsoleMessage += (s, e) => ConsoleMessage?.Invoke(this, e.Message);
        }

        public void Navigate(string url) { ThrowIfNotInitialized(); _browser!.LoadUrl(url); }
        public void Refresh() { ThrowIfNotInitialized(); _browser!.Reload(); }
        public void GoBack() { ThrowIfNotInitialized(); if (_browser!.CanGoBack) _browser.Back(); }
        public void GoForward() { ThrowIfNotInitialized(); if (_browser!.CanGoForward) _browser.Forward(); }
        public async Task<JavascriptResponse> ExecuteJavaScriptAsync(string script) { ThrowIfNotInitialized(); return await _browser!.EvaluateScriptAsync(script); }
        public async Task<string> GetPageSourceAsync() { ThrowIfNotInitialized(); return await _browser!.GetSourceAsync(); }

        public async Task<System.Collections.Generic.List<Cookie>> GetCookiesAsync(string url = "")
        {
            ThrowIfNotInitialized();
            var mgr = _requestContext!.GetCookieManager(null);
            var visitor = new CookieVisitor();
            if (string.IsNullOrEmpty(url)) mgr.VisitAllCookies(visitor);
            else mgr.VisitUrlCookies(url, true, visitor);
            return await visitor.GetCookiesAsync();
        }

        public async Task<bool> SetCookieAsync(string url, string name, string value, string domain = "", string path = "/", DateTime? expires = null)
        {
            ThrowIfNotInitialized();
            var mgr = _requestContext!.GetCookieManager(null);
            return await mgr.SetCookieAsync(url, new Cookie { Name = name, Value = value, Domain = domain, Path = path, Expires = expires });
        }

        public async Task<bool> ClearCookiesAsync()
        {
            ThrowIfNotInitialized();
            return await _requestContext!.GetCookieManager(null).DeleteCookiesAsync("", "") > 0;
        }

        public void ClearCache()
        {
            ThrowIfNotInitialized();
            if (Directory.Exists(_cachePath))
            {
                try { Directory.Delete(_cachePath, true); Directory.CreateDirectory(_cachePath); } catch { }
            }
        }

        public void AttachTo(Panel container)
        {
            ThrowIfNotInitialized();
            if (!container.Children.Contains(_browser)) { container.Children.Clear(); container.Children.Add(_browser); }
        }

        public void Detach() { if (_browser?.Parent is Panel p) p.Children.Remove(_browser); }

        private void ThrowIfNotInitialized()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(AccountBrowserInstance));
            if (_browser == null) throw new InvalidOperationException("Browser not initialized");
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            if (_browser != null) { Detach(); _browser.Dispose(); _browser = null; }
            _requestContext?.Dispose(); _requestContext = null;
        }
    }
}
