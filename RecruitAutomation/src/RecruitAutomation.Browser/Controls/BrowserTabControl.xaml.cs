using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace RecruitAutomation.Browser.Controls
{
    /// <summary>
    /// 浏览器标签页控件
    /// 封装单个账号的浏览器实例和工具栏
    /// </summary>
    public partial class BrowserTabControl : UserControl, IDisposable
    {
        private AccountBrowserInstance? _browserInstance;
        private string _homeUrl = "about:blank";
        private bool _disposed;

        /// <summary>
        /// 账号 ID
        /// </summary>
        public string? AccountId => _browserInstance?.AccountId;

        /// <summary>
        /// 浏览器实例
        /// </summary>
        public AccountBrowserInstance? BrowserInstance => _browserInstance;

        /// <summary>
        /// 主页 URL
        /// </summary>
        public string HomeUrl
        {
            get => _homeUrl;
            set => _homeUrl = value ?? "about:blank";
        }

        public BrowserTabControl()
        {
            InitializeComponent();
        }

        /// <summary>
        /// 绑定账号浏览器实例
        /// </summary>
        public void BindInstance(AccountBrowserInstance instance)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(BrowserTabControl));

            // 解绑旧实例
            UnbindInstance();

            _browserInstance = instance;

            // 绑定事件
            _browserInstance.UrlChanged += OnUrlChanged;
            _browserInstance.TitleChanged += OnTitleChanged;
            _browserInstance.LoadingStateChanged += OnLoadingStateChanged;

            // 将浏览器控件添加到容器
            _browserInstance.AttachTo(browserContainer);

            // 更新 UI
            txtAccountId.Text = $"账号: {instance.AccountId}";
            txtUrl.Text = instance.CurrentUrl;
        }

        /// <summary>
        /// 解绑实例
        /// </summary>
        public void UnbindInstance()
        {
            if (_browserInstance == null)
                return;

            _browserInstance.UrlChanged -= OnUrlChanged;
            _browserInstance.TitleChanged -= OnTitleChanged;
            _browserInstance.LoadingStateChanged -= OnLoadingStateChanged;
            _browserInstance.Detach();
            _browserInstance = null;
        }

        private void OnUrlChanged(object? sender, string url)
        {
            Dispatcher.Invoke(() =>
            {
                txtUrl.Text = url;
            });
        }

        private void OnTitleChanged(object? sender, string title)
        {
            Dispatcher.Invoke(() =>
            {
                txtTitle.Text = title;
            });
        }

        private void OnLoadingStateChanged(object? sender, bool isLoading)
        {
            Dispatcher.Invoke(() =>
            {
                txtStatus.Text = isLoading ? "加载中..." : "就绪";
                btnRefresh.Content = isLoading ? "✕" : "↻";
                btnRefresh.ToolTip = isLoading ? "停止" : "刷新";
            });
        }

        private void BtnBack_Click(object sender, RoutedEventArgs e)
        {
            _browserInstance?.GoBack();
        }

        private void BtnForward_Click(object sender, RoutedEventArgs e)
        {
            _browserInstance?.GoForward();
        }

        private void BtnRefresh_Click(object sender, RoutedEventArgs e)
        {
            if (_browserInstance == null) return;

            if (_browserInstance.IsLoading)
            {
                _browserInstance.Browser?.GetBrowser()?.StopLoad();
            }
            else
            {
                _browserInstance.Refresh();
            }
        }

        private void BtnHome_Click(object sender, RoutedEventArgs e)
        {
            _browserInstance?.Navigate(_homeUrl);
        }

        private void TxtUrl_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && _browserInstance != null)
            {
                var url = txtUrl.Text.Trim();
                
                // 自动补全协议
                if (!url.StartsWith("http://") && !url.StartsWith("https://") && !url.StartsWith("about:"))
                {
                    url = "https://" + url;
                }

                _browserInstance.Navigate(url);
            }
        }

        /// <summary>
        /// 导航到指定 URL
        /// </summary>
        public void Navigate(string url)
        {
            _browserInstance?.Navigate(url);
            txtUrl.Text = url;
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            UnbindInstance();
        }
    }
}
