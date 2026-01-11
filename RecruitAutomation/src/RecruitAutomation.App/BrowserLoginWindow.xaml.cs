using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using RecruitAutomation.Core.Constants;

namespace RecruitAutomation.App
{
    /// <summary>
    /// 浏览器登录窗口 - 用于添加新账号或重新登录
    /// 注意：不在顶部 using RecruitAutomation.Browser，避免类加载时触发 CefSharp 程序集加载
    /// </summary>
    public partial class BrowserLoginWindow : Window
    {
        private object? _browserInstance; // 实际类型: AccountBrowserInstance
        private string _accountId = string.Empty;
        private string _currentPlatform = "BOSS直聘";
        private bool _isLoggedIn;
        private readonly bool _isRelogin;
        private readonly string? _existingAccountId;
        private readonly string? _existingPlatform;

        /// <summary>
        /// 登录成功后的账号信息
        /// </summary>
        public AccountLoginResult? LoginResult { get; private set; }

        /// <summary>
        /// 登录成功后的浏览器实例（保持运行）
        /// 注意：返回 object 类型避免在属性访问时加载 Browser 程序集
        /// </summary>
        public object? BrowserInstance => _browserInstance;

        /// <summary>
        /// 默认构造函数 - 添加新账号
        /// </summary>
        public BrowserLoginWindow()
        {
            InitializeComponent();
            _isRelogin = false;
            Loaded += Window_Loaded;
            Closing += Window_Closing;
        }

        /// <summary>
        /// 重新登录构造函数 - 为现有账号重新登录
        /// </summary>
        /// <param name="accountId">现有账号ID</param>
        /// <param name="platform">平台名称</param>
        public BrowserLoginWindow(string accountId, string platform)
        {
            InitializeComponent();
            _isRelogin = true;
            _existingAccountId = accountId;
            _existingPlatform = platform;
            Loaded += Window_Loaded;
            Closing += Window_Closing;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                LogInfo("BrowserLoginWindow 开始加载...");
                
                // CefSharp 已在程序启动时初始化，直接检查状态
                if (!CefSharp.Cef.IsInitialized)
                {
                    LogError("CefSharp 未初始化！这不应该发生。");
                    MessageBox.Show(
                        "浏览器引擎未初始化，请重启程序。",
                        "错误",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                    Close();
                    return;
                }
                
                LogInfo("CefSharp 已初始化，准备创建浏览器实例...");
                
                // 如果是重新登录，设置平台选择并禁用
                if (_isRelogin && !string.IsNullOrEmpty(_existingPlatform))
                {
                    SelectPlatformByName(_existingPlatform);
                    cmbPlatform.IsEnabled = false;
                    txtAccountName.IsEnabled = false;
                    txtAccountName.Text = "重新登录";
                    txtTip.Text = "💡 请在浏览器中完成登录，登录成功后点击「保存账号」";
                }
                
                InitializeBrowser();
                LogInfo("浏览器初始化完成");
            }
            catch (Exception ex)
            {
                LogError($"窗口加载失败: {ex.Message}\n{ex.StackTrace}");
                MessageBox.Show($"浏览器初始化失败:\n{ex.Message}\n\n请查看日志文件获取详细信息。", "错误",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                Close();
            }
        }
        
        /// <summary>
        /// 根据平台名称选择下拉框项
        /// </summary>
        private void SelectPlatformByName(string platformName)
        {
            foreach (ComboBoxItem item in cmbPlatform.Items)
            {
                if (item.Content?.ToString() == platformName)
                {
                    cmbPlatform.SelectedItem = item;
                    break;
                }
            }
        }

        /// <summary>
        /// 初始化浏览器（独立方法，确保 JIT 编译时才加载程序集）
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private void InitializeBrowser()
        {
            try
            {
                LogInfo("InitializeBrowser 开始...");
                
                // 使用现有账号ID或生成新ID
                _accountId = _isRelogin && !string.IsNullOrEmpty(_existingAccountId) 
                    ? _existingAccountId 
                    : $"account_{DateTime.Now:yyyyMMddHHmmss}";
                
                LogInfo($"账号ID: {_accountId}");
                
                // 通过 BrowserInstanceManager 创建浏览器实例（确保注册到管理器）
                var startUrl = GetSelectedPlatformUrl();
                LogInfo($"起始URL: {startUrl}");
                
                LogInfo("调用 BrowserInstanceManager.GetOrCreate...");
                var instance = Browser.BrowserInstanceManager.Instance.GetOrCreate(_accountId, startUrl);
                _browserInstance = instance;
                LogInfo("BrowserInstanceManager.GetOrCreate 完成");

                // 绑定事件
                instance.UrlChanged += OnUrlChanged;
                instance.LoadingStateChanged += OnLoadingStateChanged;
                instance.TitleChanged += OnTitleChanged;
                instance.BrowserCrashed += OnBrowserCrashed;

                // 将浏览器添加到容器
                if (instance.Browser != null)
                {
                    LogInfo("将浏览器添加到容器...");
                    browserContainer.Child = instance.Browser;
                    LogInfo("浏览器已添加到容器");
                }
                else
                {
                    LogError("instance.Browser 为 null");
                }

                txtUrl.Text = startUrl;
                UpdateStatus("加载中...", true);
                
                LogInfo($"浏览器初始化成功: {_accountId}");
            }
            catch (Exception ex)
            {
                LogError($"InitializeBrowser 失败: {ex.Message}\n{ex.StackTrace}");
                throw;
            }
        }
        
        /// <summary>
        /// 获取浏览器实例（类型安全访问）
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private Browser.AccountBrowserInstance? GetBrowserInstance()
        {
            return _browserInstance as Browser.AccountBrowserInstance;
        }
        
        /// <summary>
        /// 浏览器崩溃处理
        /// </summary>
        private void OnBrowserCrashed(object? sender, string reason)
        {
            Dispatcher.Invoke(() =>
            {
                UpdateStatus($"浏览器异常: {reason}", false);
                txtTip.Text = $"⚠️ 浏览器发生异常 ({reason})，正在尝试恢复...";
                txtTip.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E74C3C"));
            });
        }
        
        private void LogInfo(string message)
        {
            try
            {
                var logPath = Path.Combine(AppConstants.DataRootPath, "logs", "login_window.log");
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
                var logPath = Path.Combine(AppConstants.DataRootPath, "logs", "login_window_error.log");
                var logDir = Path.GetDirectoryName(logPath);
                if (!string.IsNullOrEmpty(logDir) && !Directory.Exists(logDir))
                    Directory.CreateDirectory(logDir);
                File.AppendAllText(logPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [ERROR] {message}\n");
            }
            catch { }
        }

        private string GetSelectedPlatformUrl()
        {
            if (cmbPlatform.SelectedItem is ComboBoxItem item && item.Tag is string url)
            {
                _currentPlatform = item.Content?.ToString() ?? "未知平台";
                return url;
            }
            return "https://www.zhipin.com/web/user/?ka=header-login";
        }

        private void OnUrlChanged(object? sender, string url)
        {
            Dispatcher.Invoke(() =>
            {
                txtUrl.Text = url;
                CheckLoginStatus(url);
            });
        }

        private void OnLoadingStateChanged(object? sender, bool isLoading)
        {
            Dispatcher.Invoke(() =>
            {
                if (isLoading)
                {
                    UpdateStatus("加载中...", true);
                }
                else
                {
                    UpdateStatus("就绪", false);
                }
            });
        }

        private void OnTitleChanged(object? sender, string title)
        {
            Dispatcher.Invoke(() =>
            {
                if (!string.IsNullOrEmpty(title))
                {
                    Title = $"账号登录 - {title}";
                }
            });
        }

        /// <summary>
        /// 检查是否已登录（根据URL变化判断）
        /// </summary>
        private void CheckLoginStatus(string url)
        {
            // 简单判断：如果URL不再是登录页面，可能已登录
            var loginKeywords = new[] { "login", "passport", "signin", "auth" };
            var isLoginPage = false;
            
            foreach (var keyword in loginKeywords)
            {
                if (url.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                {
                    isLoginPage = true;
                    break;
                }
            }

            if (!isLoginPage && !_isLoggedIn)
            {
                _isLoggedIn = true;
                txtTip.Text = "✅ 检测到登录成功，请输入账号名称后点击「保存账号」";
                txtTip.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#27AE60"));
            }
        }

        private void UpdateStatus(string text, bool isLoading)
        {
            txtStatus.Text = text;
            statusIndicator.Fill = new SolidColorBrush(
                (Color)ColorConverter.ConvertFromString(isLoading ? "#F39C12" : "#27AE60"));
        }

        private void CmbPlatform_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var instance = GetBrowserInstance();
            if (instance?.Browser == null)
                return;

            var url = GetSelectedPlatformUrl();
            instance.Navigate(url);
            _isLoggedIn = false;
            txtTip.Text = "💡 请在浏览器中完成登录，登录成功后点击「保存账号」";
            txtTip.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#7F8C8D"));
        }

        private void BtnBack_Click(object sender, RoutedEventArgs e)
        {
            GetBrowserInstance()?.GoBack();
        }

        private void BtnForward_Click(object sender, RoutedEventArgs e)
        {
            GetBrowserInstance()?.GoForward();
        }

        private void BtnRefresh_Click(object sender, RoutedEventArgs e)
        {
            GetBrowserInstance()?.Refresh();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            var accountName = txtAccountName.Text.Trim();
            
            // 重新登录时不需要输入账号名称
            if (!_isRelogin && string.IsNullOrWhiteSpace(accountName))
            {
                MessageBox.Show("请输入账号名称", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                txtAccountName.Focus();
                return;
            }

            try
            {
                // 创建登录结果
                LoginResult = new AccountLoginResult
                {
                    AccountName = _isRelogin ? "重新登录" : accountName,
                    Platform = _currentPlatform,
                    AccountId = _accountId,
                    LoginTime = DateTime.Now,
                    IsLoggedIn = _isLoggedIn
                };

                // 从容器中移除浏览器（但保持实例运行）
                browserContainer.Child = null;

                // 解绑事件（但不销毁实例）
                UnbindBrowserEvents();

                LogInfo($"账号保存成功: {_accountId}");
                
                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                LogError($"保存账号时出错: {ex.Message}");
                MessageBox.Show($"保存账号失败:\n{ex.Message}", "错误",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        /// <summary>
        /// 解绑浏览器事件（独立方法）
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private void UnbindBrowserEvents()
        {
            var instance = GetBrowserInstance();
            if (instance != null)
            {
                instance.UrlChanged -= OnUrlChanged;
                instance.LoadingStateChanged -= OnLoadingStateChanged;
                instance.TitleChanged -= OnTitleChanged;
                instance.BrowserCrashed -= OnBrowserCrashed;
            }
        }

        private void Window_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            // 如果没有保存，关闭浏览器实例
            if (DialogResult != true && _browserInstance != null)
            {
                try
                {
                    UnbindBrowserEvents();
                    
                    // 从管理器中移除并销毁
                    CloseBrowserInstance(_accountId);
                }
                catch (Exception ex)
                {
                    LogError($"关闭浏览器实例时出错: {ex.Message}");
                }
            }
        }
        
        /// <summary>
        /// 关闭浏览器实例（独立方法）
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private void CloseBrowserInstance(string accountId)
        {
            Browser.BrowserInstanceManager.Instance.Close(accountId);
        }
    }

    /// <summary>
    /// 账号登录结果
    /// </summary>
    public class AccountLoginResult
    {
        public string AccountId { get; set; } = string.Empty;
        public string AccountName { get; set; } = string.Empty;
        public string Platform { get; set; } = string.Empty;
        public DateTime LoginTime { get; set; }
        public bool IsLoggedIn { get; set; }
    }
}
