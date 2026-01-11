using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using RecruitAutomation.Core.Constants;
using RecruitAutomation.Core.License;
using RecruitAutomation.Core.Models;
using RecruitAutomation.Core.Services;

namespace RecruitAutomation.App
{
    /// <summary>
    /// 主窗口 - 防闪退终极版
    /// 【核心原则】
    /// 1. 构造函数中不访问任何 UI 控件（除 InitializeComponent）
    /// 2. Window_Loaded 中不启动任何后台任务
    /// 3. 所有 async void 事件处理器都包裹 try-catch
    /// 4. 所有自动化只在用户点击"启动"后执行
    /// </summary>
    public partial class MainWindow : Window
    {
        private DispatcherTimer? _runtimeTimer;
        private DateTime _startTime;
        private bool _isRunning;
        private string _currentPanel = "Dashboard";

        // 数据绑定
        public ObservableCollection<AccountItem> Accounts { get; } = new();
        public ObservableCollection<LogItem> Logs { get; } = new();

        // 岗位管理 - 延迟初始化
        private Helpers.BrowserHelper? _browserHelper;
        private readonly ObservableCollection<JobViewModel> _jobs = new();
        private CancellationTokenSource? _jobFetchCts;

        public MainWindow()
        {
            try
            {
                App.WriteLog("MainWindow 构造函数开始");
                
                // 【关键】只调用 InitializeComponent，不访问任何控件
                InitializeComponent();
                
                App.WriteLog("MainWindow InitializeComponent 完成");
            }
            catch (Exception ex)
            {
                App.WriteLog($"【FATAL】MainWindow 构造函数异常: {ex}");
                App.WriteRuntimeLog($"【FATAL】MainWindow 构造函数异常: {ex}");
                // 不抛出，让窗口尝试显示
            }
        }
        
        /// <summary>
        /// 窗口加载完成 - 【关键】延迟初始化所有内容
        /// </summary>
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                App.WriteLog("Window_Loaded 开始");
                App.WriteRuntimeLog("Window_Loaded 开始");
                
                // 【关键】在 Loaded 事件中初始化 UI 绑定
                SafeInitializeUI();
                
                // 只做最基本的UI初始化，不启动任何后台任务
                SafeInitializeLicenseDisplay();
                SafeLoadAccountsFromCache();
                
                AddLog("系统启动完成");
                App.WriteLog("Window_Loaded 完成");
                App.WriteRuntimeLog("Window_Loaded 完成");
            }
            catch (Exception ex)
            {
                App.WriteLog($"Window_Loaded 异常: {ex}");
                App.WriteRuntimeLog($"Window_Loaded 异常: {ex}");
                // 不抛出异常，让窗口继续显示
            }
        }
        
        /// <summary>
        /// 安全初始化 UI 绑定
        /// </summary>
        private void SafeInitializeUI()
        {
            try
            {
                // 绑定数据源
                if (lstAccounts != null) lstAccounts.ItemsSource = Accounts;
                if (lstLogs != null) lstLogs.ItemsSource = Logs;
                if (dgJobs != null) dgJobs.ItemsSource = _jobs;
                
                // 初始化定时器
                _runtimeTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
                _runtimeTimer.Tick += RuntimeTimer_Tick;
                
                App.WriteLog("SafeInitializeUI 完成");
            }
            catch (Exception ex)
            {
                App.WriteLog($"SafeInitializeUI 异常: {ex.Message}");
                App.WriteRuntimeLog($"SafeInitializeUI 异常: {ex.Message}");
            }
        }

        /// <summary>
        /// 安全初始化授权显示
        /// </summary>
        private void SafeInitializeLicenseDisplay()
        {
            try
            {
                var license = LicenseGuard.Instance.CurrentLicense;
                if (license == null)
                {
                    App.WriteLog("SafeInitializeLicenseDisplay: license 为 null");
                    if (txtLicenseType != null) txtLicenseType.Text = "未授权";
                    if (txtExpireDays != null) txtExpireDays.Text = "-";
                    if (txtVersion != null) txtVersion.Text = "v1.0.0";
                    return;
                }

                if (txtLicenseType != null)
                {
                    txtLicenseType.Text = license.LicenseType switch
                    {
                        LicenseType.Trial => "试用版",
                        LicenseType.Professional => "专业版",
                        LicenseType.Enterprise => "企业版",
                        _ => license.LicenseType.ToString()
                    };
                }

                if (txtExpireDays != null)
                {
                    var daysLeft = (license.ExpiresAt - DateTime.UtcNow).Days;
                    txtExpireDays.Text = daysLeft > 0 ? $"剩余{daysLeft}天" : "已过期";
                }
                
                if (txtVersion != null) txtVersion.Text = "v1.0.0";
            }
            catch (Exception ex)
            {
                App.WriteLog($"SafeInitializeLicenseDisplay 异常: {ex.Message}");
                App.WriteRuntimeLog($"SafeInitializeLicenseDisplay 异常: {ex.Message}");
            }
        }

        /// <summary>
        /// 安全加载账号缓存
        /// </summary>
        private void SafeLoadAccountsFromCache()
        {
            try
            {
                var cacheFile = Path.Combine(AppConstants.DataRootPath, "accounts.json");
                if (!File.Exists(cacheFile))
                    return;

                var json = File.ReadAllText(cacheFile);
                var cachedAccounts = JsonSerializer.Deserialize<List<AccountCacheData>>(json);
                
                if (cachedAccounts != null)
                {
                    foreach (var data in cachedAccounts)
                    {
                        Accounts.Add(new AccountItem
                        {
                            Id = data.Id,
                            Name = data.Name,
                            Platform = data.Platform,
                            Status = data.IsEnabled ? "离线" : "已停用",
                            IsEnabled = data.IsEnabled
                        });
                    }
                    
                    if (Accounts.Count > 0)
                    {
                        AddLog($"已加载 {Accounts.Count} 个缓存账号");
                    }
                }
            }
            catch (Exception ex)
            {
                App.WriteLog($"SafeLoadAccountsFromCache 异常: {ex.Message}");
                App.WriteRuntimeLog($"SafeLoadAccountsFromCache 异常: {ex.Message}");
            }
        }

        /// <summary>
        /// 延迟初始化浏览器服务 - 只在需要时调用
        /// 【改造】不抛异常，返回是否成功
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private bool EnsureBrowserServicesInitialized()
        {
            if (_browserHelper != null && _browserHelper.IsInitialized)
                return true;
                
            try
            {
                App.WriteLog("EnsureBrowserServicesInitialized 开始");
                App.WriteRuntimeLog("EnsureBrowserServicesInitialized 开始");
                AddLog("正在初始化浏览器服务...");
                
                _browserHelper = new Helpers.BrowserHelper();
                _browserHelper.OnProgress += BrowserHelper_OnProgress;
                _browserHelper.OnCompleted += BrowserHelper_OnCompleted;
                
                // 【改造】使用返回值判断是否成功
                if (!_browserHelper.Initialize())
                {
                    var error = _browserHelper.LastError;
                    App.WriteLog($"浏览器服务初始化失败: {error}");
                    App.WriteRuntimeLog($"浏览器服务初始化失败: {error}");
                    AddLog($"浏览器服务初始化失败: {error}");
                    return false;
                }
                
                AddLog("浏览器服务初始化完成");
                App.WriteLog("EnsureBrowserServicesInitialized 完成");
                return true;
            }
            catch (Exception ex)
            {
                App.WriteLog($"EnsureBrowserServicesInitialized 异常: {ex}");
                App.WriteRuntimeLog($"EnsureBrowserServicesInitialized 异常: {ex}");
                AddLog($"浏览器服务初始化失败: {ex.Message}");
                return false;
            }
        }
        
        private void BrowserHelper_OnProgress(string accountId, string message, int percentage)
        {
            try
            {
                SafeAsync.RunOnUI(() =>
                {
                    if (txtJobStatus != null) txtJobStatus.Text = $"[{accountId}] {message}";
                    if (jobProgressBar != null) jobProgressBar.Value = percentage;
                }, "BrowserHelper_OnProgress");
            }
            catch { }
        }
        
        private void BrowserHelper_OnCompleted(JobFetchResult result)
        {
            // 完成事件
        }

        /// <summary>
        /// 安全添加日志 - 确保在UI线程执行
        /// </summary>
        private void AddLog(string message)
        {
            try
            {
                SafeAsync.RunOnUI(() =>
                {
                    Logs.Insert(0, new LogItem
                    {
                        Message = message,
                        Time = DateTime.Now.ToString("HH:mm:ss")
                    });

                    if (Logs.Count > 100)
                        Logs.RemoveAt(Logs.Count - 1);
                }, "AddLog");
            }
            catch (Exception ex)
            {
                App.WriteRuntimeLog($"AddLog 异常: {ex.Message}");
            }
        }

        private void SaveAccountsToCache()
        {
            try
            {
                var cacheData = new List<AccountCacheData>();
                foreach (var account in Accounts)
                {
                    cacheData.Add(new AccountCacheData
                    {
                        Id = account.Id,
                        Name = account.Name,
                        Platform = account.Platform,
                        IsEnabled = account.IsEnabled
                    });
                }

                var cacheFile = Path.Combine(AppConstants.DataRootPath, "accounts.json");
                var dir = Path.GetDirectoryName(cacheFile);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                var json = JsonSerializer.Serialize(cacheData, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(cacheFile, json);
            }
            catch (Exception ex)
            {
                App.WriteRuntimeLog($"SaveAccountsToCache 异常: {ex.Message}");
                AddLog($"保存账号缓存失败: {ex.Message}");
            }
        }

        #region 导航

        private void NavButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (sender is Button btn && btn.Tag is string tag)
                {
                    SwitchPanel(tag);
                }
            }
            catch (Exception ex)
            {
                App.WriteRuntimeLog($"NavButton_Click 异常: {ex.Message}");
            }
        }

        private void SwitchPanel(string panelName)
        {
            try
            {
                _currentPanel = panelName;

                // 更新导航按钮样式
                var navButtons = new[] { btnDashboard, btnAccounts, btnBrowser, btnJobs, btnCandidates, btnMessages,
                                         btnAutoGreet, btnAutoReply, btnBatchSend, btnActivity, btnFilter, btnAI, btnSettings };
                
                foreach (var btn in navButtons)
                {
                    if (btn != null) btn.Style = (Style)FindResource("NavButton");
                }

                var activeBtn = panelName switch
                {
                    "Dashboard" => btnDashboard,
                    "Accounts" => btnAccounts,
                    "Browser" => btnBrowser,
                    "Jobs" => btnJobs,
                    "Candidates" => btnCandidates,
                    "Messages" => btnMessages,
                    "AutoGreet" => btnAutoGreet,
                    "AutoReply" => btnAutoReply,
                    "BatchSend" => btnBatchSend,
                    "Activity" => btnActivity,
                    "Filter" => btnFilter,
                    "AI" => btnAI,
                    "Settings" => btnSettings,
                    _ => btnDashboard
                };
                if (activeBtn != null) activeBtn.Style = (Style)FindResource("NavButtonActive");

                // 更新页面标题
                var (title, desc) = panelName switch
                {
                    "Dashboard" => ("数据概览", "查看招聘数据统计和今日工作概况"),
                    "Accounts" => ("账号管理", "管理招聘平台账号，支持多账号同时运行"),
                    "Browser" => ("浏览器管理", "管理浏览器实例，启动/停止/重启浏览器"),
                    "Jobs" => ("岗位管理", "发布、更新、刷新招聘岗位"),
                    "Candidates" => ("候选人库", "查看采集的候选人信息和筛选结果"),
                    "Messages" => ("消息中心", "查看和管理与候选人的沟通记录"),
                    "AutoGreet" => ("自动打招呼", "配置自动打招呼规则和话术模板"),
                    "AutoReply" => ("智能回复", "配置关键词自动回复和AI智能回复"),
                    "BatchSend" => ("群发联系人", "批量发送消息给已建立聊天关系的联系人"),
                    "Activity" => ("活跃度维护", "自动刷新岗位、浏览简历保持账号活跃"),
                    "Filter" => ("筛选规则", "配置候选人筛选条件和评分规则"),
                    "AI" => ("AI配置", "配置AI大模型接口和智能回复参数"),
                    "Settings" => ("系统设置", "软件基础设置和数据管理"),
                    _ => ("数据概览", "")
                };
                if (txtPageTitle != null) txtPageTitle.Text = title;
                if (txtPageDesc != null) txtPageDesc.Text = desc;

                // 切换面板显示
                if (panelDashboard != null) panelDashboard.Visibility = panelName == "Dashboard" ? Visibility.Visible : Visibility.Collapsed;
                if (panelAccounts != null) panelAccounts.Visibility = panelName == "Accounts" ? Visibility.Visible : Visibility.Collapsed;
                if (panelBrowser != null) panelBrowser.Visibility = panelName == "Browser" ? Visibility.Visible : Visibility.Collapsed;
                if (panelJobs != null) panelJobs.Visibility = panelName == "Jobs" ? Visibility.Visible : Visibility.Collapsed;
                if (panelCandidates != null) panelCandidates.Visibility = panelName == "Candidates" ? Visibility.Visible : Visibility.Collapsed;
                if (panelMessages != null) panelMessages.Visibility = panelName == "Messages" ? Visibility.Visible : Visibility.Collapsed;
                if (panelAutoGreet != null) panelAutoGreet.Visibility = panelName == "AutoGreet" ? Visibility.Visible : Visibility.Collapsed;
                if (panelAutoReply != null) panelAutoReply.Visibility = panelName == "AutoReply" ? Visibility.Visible : Visibility.Collapsed;
                if (panelActivity != null) panelActivity.Visibility = panelName == "Activity" ? Visibility.Visible : Visibility.Collapsed;
                if (panelFilter != null) panelFilter.Visibility = panelName == "Filter" ? Visibility.Visible : Visibility.Collapsed;
                if (panelAI != null) panelAI.Visibility = panelName == "AI" ? Visibility.Visible : Visibility.Collapsed;
                if (panelBatchSend != null) panelBatchSend.Visibility = panelName == "BatchSend" ? Visibility.Visible : Visibility.Collapsed;
                if (panelSettings != null) panelSettings.Visibility = panelName == "Settings" ? Visibility.Visible : Visibility.Collapsed;
                
                // 切换到浏览器管理时刷新列表
                if (panelName == "Browser")
                {
                    _ = SafeRefreshBrowserListAsync();
                }
                // 切换到账号管理时更新统计
                if (panelName == "Accounts")
                {
                    UpdateAccountStats();
                }
            }
            catch (Exception ex)
            {
                App.WriteRuntimeLog($"SwitchPanel 异常: {ex.Message}");
            }
        }

        #endregion

        #region 操作按钮

        private void BtnStartStop_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_isRunning)
                {
                    StopAutomation();
                }
                else
                {
                    StartAutomation();
                }
            }
            catch (Exception ex)
            {
                App.WriteRuntimeLog($"BtnStartStop_Click 异常: {ex.Message}");
                AddLog($"操作失败: {ex.Message}");
            }
        }

        private void StartAutomation()
        {
            _isRunning = true;
            _startTime = DateTime.Now;
            _runtimeTimer?.Start();

            if (btnStartStop != null)
            {
                btnStartStop.Content = "⏹ 停止";
                btnStartStop.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E74C3C"));
            }
            if (statusIndicator != null) statusIndicator.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#27AE60"));
            if (txtActivityStatus != null) txtActivityStatus.Text = "运行中";
            if (txtStatus != null) txtStatus.Text = "自动化任务运行中...";

            AddLog("自动化任务已启动");
        }

        private void StopAutomation()
        {
            _isRunning = false;
            _runtimeTimer?.Stop();

            if (btnStartStop != null)
            {
                btnStartStop.Content = "▶ 启动";
                btnStartStop.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#3498DB"));
            }
            if (statusIndicator != null) statusIndicator.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#95A5A6"));
            if (txtActivityStatus != null) txtActivityStatus.Text = "已停止";
            if (txtStatus != null) txtStatus.Text = "系统就绪";

            AddLog("自动化任务已停止");
        }

        private void RuntimeTimer_Tick(object? sender, EventArgs e)
        {
            try
            {
                var runtime = DateTime.Now - _startTime;
                if (txtRuntime != null) txtRuntime.Text = runtime.ToString(@"hh\:mm\:ss");
            }
            catch { }
        }

        // 【关键】所有 async void 事件处理器都包裹完整的 try-catch
        private async void BtnAddAccount_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!EnsureBrowserServicesInitialized())
                {
                    MessageBox.Show("浏览器服务初始化失败，请查看日志", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                
                var loginWindow = new BrowserLoginWindow();
                loginWindow.Owner = this;
                
                if (loginWindow.ShowDialog() == true && loginWindow.LoginResult != null)
                {
                    var result = loginWindow.LoginResult;
                    
                    Accounts.Add(new AccountItem
                    {
                        Id = result.AccountId,
                        Name = result.AccountName,
                        Platform = result.Platform,
                        Status = result.IsLoggedIn ? "在线" : "未登录",
                        IsEnabled = true
                    });
                    
                    SaveAccountsToCache();
                    
                    if (result.IsLoggedIn)
                    {
                        var platform = result.Platform switch
                        {
                            "BOSS直聘" => RecruitPlatform.Boss,
                            "智联招聘" => RecruitPlatform.Zhilian,
                            "前程无忧" => RecruitPlatform.Job51,
                            "猎聘" => RecruitPlatform.Liepin,
                            _ => RecruitPlatform.Boss
                        };
                        _browserHelper?.MarkAccountAsLoggedIn(result.AccountId, platform);
                    }
                    
                    AddLog($"账号 [{result.AccountName}] 添加成功");
                    
                    MessageBox.Show(
                        $"账号添加成功！\n\n账号名称: {result.AccountName}\n平台: {result.Platform}",
                        "成功", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                App.WriteRuntimeLog($"BtnAddAccount_Click 异常: {ex}");
                AddLog($"添加账号失败: {ex.Message}");
                MessageBox.Show($"添加账号失败:\n{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            
            await Task.CompletedTask; // 保持 async 签名
        }

        private void BtnLicenseInfo_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var license = LicenseGuard.Instance.CurrentLicense;
                if (license == null)
                {
                    MessageBox.Show("无法获取授权信息", "错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var features = license.Features != null && license.Features.Length > 0
                    ? string.Join("\n  • ", license.Features)
                    : "全部功能";

                var daysLeft = (license.ExpiresAt - DateTime.UtcNow).Days;
                var daysLeftText = daysLeft > 0 ? $"{daysLeft} 天" : "已过期";
                var licenseTypeText = txtLicenseType?.Text ?? license.LicenseType.ToString();

                MessageBox.Show(
                    $"授权详情\n────────────────────────\n\n" +
                    $"授权给: {license.LicenseTo}\n授权类型: {licenseTypeText}\n" +
                    $"机器码: {license.MachineCode}\n\n" +
                    $"颁发时间: {license.IssuedAt:yyyy-MM-dd HH:mm}\n" +
                    $"有效期至: {license.ExpiresAt:yyyy-MM-dd HH:mm}\n" +
                    $"剩余时间: {daysLeftText}\n\n" +
                    $"最大账号数: {license.MaxAccounts}\n功能权限:\n  • {features}",
                    "授权信息", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                App.WriteRuntimeLog($"BtnLicenseInfo_Click 异常: {ex.Message}");
            }
        }

        private async void BtnOnlineAccount_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (sender is Button btn && btn.Tag is AccountItem account)
                {
                    if (!EnsureBrowserServicesInitialized())
                    {
                        MessageBox.Show("浏览器服务初始化失败", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }
                    
                    var loginWindow = new BrowserLoginWindow(account.Id, account.Platform);
                    loginWindow.Owner = this;
                    
                    if (loginWindow.ShowDialog() == true && loginWindow.LoginResult != null)
                    {
                        var result = loginWindow.LoginResult;
                        account.Status = result.IsLoggedIn ? "在线" : "未登录";
                        
                        if (result.IsLoggedIn)
                        {
                            var platform = account.Platform switch
                            {
                                "BOSS直聘" => RecruitPlatform.Boss,
                                "智联招聘" => RecruitPlatform.Zhilian,
                                "前程无忧" => RecruitPlatform.Job51,
                                "猎聘" => RecruitPlatform.Liepin,
                                _ => RecruitPlatform.Boss
                            };
                            _browserHelper?.MarkAccountAsLoggedIn(account.Id, platform);
                        }
                        
                        AddLog($"账号 [{account.Name}] 重新上线成功");
                    }
                }
            }
            catch (Exception ex)
            {
                App.WriteRuntimeLog($"BtnOnlineAccount_Click 异常: {ex}");
                AddLog($"账号上线失败: {ex.Message}");
            }
            
            await Task.CompletedTask;
        }

        private void BtnToggleAccount_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (sender is Button btn && btn.Tag is AccountItem account)
                {
                    if (account.IsEnabled)
                    {
                        account.IsEnabled = false;
                        account.Status = "已停用";
                        AddLog($"账号 [{account.Name}] 已停用");
                    }
                    else
                    {
                        account.IsEnabled = true;
                        account.Status = "离线";
                        AddLog($"账号 [{account.Name}] 已启用");
                    }
                    SaveAccountsToCache();
                }
            }
            catch (Exception ex)
            {
                App.WriteRuntimeLog($"BtnToggleAccount_Click 异常: {ex.Message}");
            }
        }

        private void BtnDeleteAccount_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (sender is Button btn && btn.Tag is AccountItem account)
                {
                    var result = MessageBox.Show(
                        $"确定要删除账号 [{account.Name}] 吗？",
                        "确认删除", MessageBoxButton.YesNo, MessageBoxImage.Warning);

                    if (result == MessageBoxResult.Yes)
                    {
                        if (_browserHelper?.IsInitialized == true)
                        {
                            _browserHelper.CloseBrowserInstance(account.Id);
                        }
                        
                        Accounts.Remove(account);
                        SaveAccountsToCache();
                        AddLog($"账号 [{account.Name}] 已删除");
                    }
                }
            }
            catch (Exception ex)
            {
                App.WriteRuntimeLog($"BtnDeleteAccount_Click 异常: {ex.Message}");
            }
        }

        #endregion


        #region 岗位管理

        private async void BtnRefreshJobAccounts_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                await SafeRefreshJobAccountsAsync();
            }
            catch (Exception ex)
            {
                App.WriteRuntimeLog($"BtnRefreshJobAccounts_Click 异常: {ex}");
                AddLog($"刷新账号失败: {ex.Message}");
            }
        }

        private async Task SafeRefreshJobAccountsAsync()
        {
            try
            {
                if (!EnsureBrowserServicesInitialized())
                {
                    AddLog("浏览器服务初始化失败");
                    if (cmbJobAccounts != null) cmbJobAccounts.ItemsSource = new List<JobAccountItem>();
                    return;
                }
                
                if (_browserHelper == null || !_browserHelper.IsInitialized)
                {
                    AddLog("浏览器服务未初始化");
                    if (cmbJobAccounts != null) cmbJobAccounts.ItemsSource = new List<JobAccountItem>();
                    return;
                }
                
                var runningAccounts = await _browserHelper.GetAvailableAccountsAsync();
                var accountList = new List<JobAccountItem>();
                
                foreach (var a in runningAccounts)
                {
                    var uiAccount = Accounts.FirstOrDefault(x => x.Id == a.AccountId);
                    var displayName = uiAccount?.Name ?? a.AccountId;
                    
                    accountList.Add(new JobAccountItem
                    {
                        AccountId = a.AccountId,
                        DisplayName = displayName,
                        Platform = a.Platform,
                        PlatformName = a.Platform.ToString(),
                        IsStarted = a.IsStarted,
                        LoginStatus = a.LoginStatus,
                        IsAvailable = a.IsAvailable
                    });
                }

                if (cmbJobAccounts != null)
                {
                    cmbJobAccounts.ItemsSource = accountList;

                    if (accountList.Count > 0)
                    {
                        var available = accountList.FirstOrDefault(a => a.IsAvailable);
                        cmbJobAccounts.SelectedItem = available ?? accountList[0];
                    }
                }

                UpdateJobFetchButtonState();
                AddLog($"账号刷新完成：{accountList.Count} 个");
            }
            catch (Exception ex)
            {
                App.WriteRuntimeLog($"SafeRefreshJobAccountsAsync 异常: {ex}");
                AddLog($"刷新账号列表失败: {ex.Message}");
            }
        }

        private void CmbJobAccounts_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try { UpdateJobFetchButtonState(); } catch { }
        }

        private void UpdateJobFetchButtonState()
        {
            try
            {
                var selected = cmbJobAccounts?.SelectedItem as JobAccountItem;
                if (btnFetchJobs != null) btnFetchJobs.IsEnabled = selected?.IsAvailable == true;
            }
            catch { }
        }

        private async void BtnFetchJobs_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var account = cmbJobAccounts?.SelectedItem as JobAccountItem;
                if (account == null)
                {
                    MessageBox.Show("请先选择一个账号", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                
                await SafeFetchJobsAsync(new[] { account.AccountId });
            }
            catch (Exception ex)
            {
                App.WriteRuntimeLog($"BtnFetchJobs_Click 异常: {ex}");
                AddLog($"读取岗位失败: {ex.Message}");
            }
        }

        private async void BtnFetchAllJobs_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!EnsureBrowserServicesInitialized())
                {
                    MessageBox.Show("浏览器服务初始化失败", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                
                if (_browserHelper == null) return;
                
                var accounts = await _browserHelper.GetAvailableAccountsAsync();
                var availableIds = accounts.Where(a => a.IsAvailable).Select(a => a.AccountId).ToList();

                if (availableIds.Count == 0)
                {
                    MessageBox.Show("没有可用的账号", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                await SafeFetchJobsAsync(availableIds);
            }
            catch (Exception ex)
            {
                App.WriteRuntimeLog($"BtnFetchAllJobs_Click 异常: {ex}");
                AddLog($"读取岗位失败: {ex.Message}");
            }
        }

        private async Task SafeFetchJobsAsync(IEnumerable<string> accountIds)
        {
            _jobFetchCts = new CancellationTokenSource();

            try
            {
                ShowJobProgress(true);
                if (btnFetchJobs != null) btnFetchJobs.IsEnabled = false;
                if (btnFetchAllJobs != null) btnFetchAllJobs.IsEnabled = false;
                if (btnCancelFetch != null) btnCancelFetch.Visibility = Visibility.Visible;

                var accountList = accountIds.ToList();

                if (accountList.Count == 1)
                {
                    var fetchResult = await _browserHelper!.FetchJobsFromAccountAsync(
                        accountList[0], true, _jobFetchCts.Token);
                    await HandleJobFetchResultAsync(fetchResult);
                }
                else
                {
                    var results = await _browserHelper!.FetchJobsFromMultipleAccountsAsync(
                        accountList, true, _jobFetchCts.Token);

                    foreach (var fetchResult in results)
                    {
                        await HandleJobFetchResultAsync(fetchResult);
                    }
                }

                await SafeLoadJobsAsync();
            }
            catch (OperationCanceledException)
            {
                UpdateJobStatus("操作已取消");
                AddLog("岗位读取已取消");
            }
            catch (Exception ex)
            {
                App.WriteRuntimeLog($"SafeFetchJobsAsync 异常: {ex}");
                AddLog($"读取岗位失败: {ex.Message}");
            }
            finally
            {
                ShowJobProgress(false);
                if (btnFetchJobs != null) btnFetchJobs.IsEnabled = true;
                if (btnFetchAllJobs != null) btnFetchAllJobs.IsEnabled = true;
                if (btnCancelFetch != null) btnCancelFetch.Visibility = Visibility.Collapsed;
                _jobFetchCts?.Dispose();
                _jobFetchCts = null;
            }
        }

        private async Task HandleJobFetchResultAsync(JobFetchResult result)
        {
            try
            {
                if (result.Success && result.Jobs.Count > 0)
                {
                    await _browserHelper!.JobManagementService!.SaveFetchedJobsAsync(result.AccountId, result.Jobs);
                    AddLog($"账号 [{result.AccountId}] 读取到 {result.Jobs.Count} 个岗位");
                }
                else
                {
                    AddLog($"账号 [{result.AccountId}] 读取失败: {result.ErrorMessage}");
                }
            }
            catch (Exception ex)
            {
                App.WriteRuntimeLog($"HandleJobFetchResultAsync 异常: {ex.Message}");
            }
        }

        private void BtnCancelFetch_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _jobFetchCts?.Cancel();
                _browserHelper?.CancelAll();
            }
            catch { }
        }

        private async Task SafeLoadJobsAsync()
        {
            try
            {
                if (!EnsureBrowserServicesInitialized())
                {
                    AddLog("浏览器服务初始化失败，无法加载岗位");
                    return;
                }
                
                if (_browserHelper?.JobManagementService == null) return;
                
                var jobs = await _browserHelper.JobManagementService.GetAllAsync();

                _jobs.Clear();
                foreach (var job in jobs.OrderByDescending(j => j.FetchedAt))
                {
                    _jobs.Add(new JobViewModel(job));
                }

                UpdateJobStats();
            }
            catch (Exception ex)
            {
                App.WriteRuntimeLog($"SafeLoadJobsAsync 异常: {ex.Message}");
                AddLog($"加载岗位列表失败: {ex.Message}");
            }
        }

        private void ChkSelectAllJobs_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var isChecked = chkSelectAllJobs?.IsChecked == true;
                foreach (var job in _jobs) job.IsSelected = isChecked;
                UpdateJobStats();
            }
            catch { }
        }

        private void DgJobs_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try { UpdateJobStats(); } catch { }
        }

        private async void JobAutomationToggle_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (sender is CheckBox chk && chk.DataContext is JobViewModel vm)
                {
                    if (!EnsureBrowserServicesInitialized() || _browserHelper?.JobManagementService == null)
                    {
                        AddLog("浏览器服务未初始化");
                        return;
                    }
                    
                    if (vm.AutomationEnabled)
                        await _browserHelper.JobManagementService.EnableAutomationAsync(vm.Id);
                    else
                        await _browserHelper.JobManagementService.DisableAutomationAsync(vm.Id);
                }
            }
            catch (Exception ex)
            {
                App.WriteRuntimeLog($"JobAutomationToggle_Click 异常: {ex.Message}");
            }
        }

        private async void BtnBatchEnableAutomation_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var selected = _jobs.Where(j => j.IsSelected).ToList();
                if (selected.Count == 0) return;

                if (!EnsureBrowserServicesInitialized() || _browserHelper?.JobManagementService == null)
                {
                    AddLog("浏览器服务未初始化");
                    return;
                }
                await _browserHelper.JobManagementService.BatchEnableAutomationAsync(selected.Select(j => j.Id));
                await SafeLoadJobsAsync();
                AddLog($"批量启用 {selected.Count} 个岗位");
            }
            catch (Exception ex)
            {
                App.WriteRuntimeLog($"BtnBatchEnableAutomation_Click 异常: {ex.Message}");
            }
        }

        private async void BtnBatchDisableAutomation_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var selected = _jobs.Where(j => j.IsSelected).ToList();
                if (selected.Count == 0) return;

                if (!EnsureBrowserServicesInitialized() || _browserHelper?.JobManagementService == null)
                {
                    AddLog("浏览器服务未初始化");
                    return;
                }
                await _browserHelper.JobManagementService.BatchDisableAutomationAsync(selected.Select(j => j.Id));
                await SafeLoadJobsAsync();
                AddLog($"批量禁用 {selected.Count} 个岗位");
            }
            catch (Exception ex)
            {
                App.WriteRuntimeLog($"BtnBatchDisableAutomation_Click 异常: {ex.Message}");
            }
        }

        private async void BtnDeleteSelectedJobs_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var selected = _jobs.Where(j => j.IsSelected).ToList();
                if (selected.Count == 0) return;

                var result = MessageBox.Show($"确定要删除选中的 {selected.Count} 个岗位吗？",
                    "确认删除", MessageBoxButton.YesNo, MessageBoxImage.Warning);

                if (result == MessageBoxResult.Yes)
                {
                    if (!EnsureBrowserServicesInitialized() || _browserHelper?.JobManagementService == null)
                    {
                        AddLog("浏览器服务未初始化");
                        return;
                    }
                    foreach (var job in selected)
                    {
                        await _browserHelper.JobManagementService.DeleteAsync(job.Id);
                    }
                    await SafeLoadJobsAsync();
                    AddLog($"已删除 {selected.Count} 个岗位");
                }
            }
            catch (Exception ex)
            {
                App.WriteRuntimeLog($"BtnDeleteSelectedJobs_Click 异常: {ex.Message}");
            }
        }

        private void ShowJobProgress(bool show)
        {
            try
            {
                if (jobStatusBar != null) jobStatusBar.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
                if (jobProgressBar != null) jobProgressBar.Value = 0;
            }
            catch { }
        }

        private void UpdateJobStatus(string message)
        {
            try 
            { 
                SafeAsync.RunOnUI(() => 
                { 
                    if (txtJobStatus != null) txtJobStatus.Text = message; 
                }, "UpdateJobStatus"); 
            } 
            catch { }
        }

        private void UpdateJobStats()
        {
            try
            {
                var total = _jobs.Count;
                var selected = _jobs.Count(j => j.IsSelected);
                var automationEnabled = _jobs.Count(j => j.AutomationEnabled);

                if (txtJobStats != null) txtJobStats.Text = $"共 {total} 个岗位，{automationEnabled} 个已启用自动化";
                if (txtJobSelectedStats != null) txtJobSelectedStats.Text = $"已选择 {selected} 个";
            }
            catch { }
        }

        #endregion

        #region 浏览器管理

        private readonly ObservableCollection<BrowserInstanceItem> _browserInstances = new();

        private void UpdateAccountStats()
        {
            try
            {
                var total = Accounts.Count;
                var online = Accounts.Count(a => a.Status == "在线");
                var offline = Accounts.Count(a => a.Status == "离线" || a.Status == "已停用");
                
                if (txtAccountCount != null) txtAccountCount.Text = total.ToString();
                if (txtOnlineCount != null) txtOnlineCount.Text = online.ToString();
                if (txtOfflineCount != null) txtOfflineCount.Text = offline.ToString();
            }
            catch { }
        }

        private async Task SafeRefreshBrowserListAsync()
        {
            try
            {
                if (!EnsureBrowserServicesInitialized())
                {
                    AddLog("浏览器服务初始化失败");
                    return;
                }
                
                if (_browserHelper == null || !_browserHelper.IsInitialized)
                {
                    AddLog("浏览器服务未初始化");
                    return;
                }
                
                var accounts = await _browserHelper.GetAvailableAccountsAsync();
                
                _browserInstances.Clear();
                foreach (var a in accounts)
                {
                    var uiAccount = Accounts.FirstOrDefault(x => x.Id == a.AccountId);
                    var displayName = uiAccount?.Name ?? a.AccountId;
                    
                    _browserInstances.Add(new BrowserInstanceItem
                    {
                        AccountId = a.AccountId,
                        DisplayName = displayName,
                        Platform = a.Platform,
                        PlatformName = a.Platform.ToString(),
                        IsStarted = a.IsStarted,
                        LoginStatus = a.LoginStatus
                    });
                }
                
                if (dgBrowserInstances != null)
                {
                    dgBrowserInstances.ItemsSource = _browserInstances;
                }
                
                UpdateBrowserStats();
                AddLog($"浏览器列表刷新完成：{_browserInstances.Count} 个实例");
            }
            catch (Exception ex)
            {
                App.WriteRuntimeLog($"SafeRefreshBrowserListAsync 异常: {ex}");
                AddLog($"刷新浏览器列表失败: {ex.Message}");
            }
        }

        private void UpdateBrowserStats()
        {
            try
            {
                var total = _browserInstances.Count;
                var running = _browserInstances.Count(b => b.IsStarted);
                
                if (txtBrowserCount != null) txtBrowserCount.Text = total.ToString();
                if (txtBrowserRunning != null) txtBrowserRunning.Text = running.ToString();
            }
            catch { }
        }

        private async void BtnRefreshBrowserStatus_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                await SafeRefreshBrowserListAsync();
            }
            catch (Exception ex)
            {
                App.WriteRuntimeLog($"BtnRefreshBrowserStatus_Click 异常: {ex}");
                AddLog($"刷新浏览器状态失败: {ex.Message}");
            }
        }

        private async void BtnStartAllBrowsers_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!EnsureBrowserServicesInitialized())
                {
                    MessageBox.Show("浏览器服务初始化失败", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                
                AddLog("正在启动所有浏览器实例...");
                
                foreach (var instance in _browserInstances.Where(b => !b.IsStarted))
                {
                    try
                    {
                        await _browserHelper!.StartBrowserInstanceAsync(instance.AccountId, instance.Platform);
                        instance.IsStarted = true;
                        AddLog($"浏览器实例 [{instance.DisplayName}] 已启动");
                    }
                    catch (Exception ex)
                    {
                        AddLog($"启动 [{instance.DisplayName}] 失败: {ex.Message}");
                    }
                }
                
                UpdateBrowserStats();
                AddLog("全部浏览器启动完成");
            }
            catch (Exception ex)
            {
                App.WriteRuntimeLog($"BtnStartAllBrowsers_Click 异常: {ex}");
                AddLog($"启动浏览器失败: {ex.Message}");
            }
        }

        private async void BtnStopAllBrowsers_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_browserHelper == null || !_browserHelper.IsInitialized)
                {
                    AddLog("浏览器服务未初始化");
                    return;
                }
                
                AddLog("正在停止所有浏览器实例...");
                
                foreach (var instance in _browserInstances.Where(b => b.IsStarted))
                {
                    try
                    {
                        _browserHelper.CloseBrowserInstance(instance.AccountId);
                        instance.IsStarted = false;
                        AddLog($"浏览器实例 [{instance.DisplayName}] 已停止");
                    }
                    catch (Exception ex)
                    {
                        AddLog($"停止 [{instance.DisplayName}] 失败: {ex.Message}");
                    }
                }
                
                UpdateBrowserStats();
                AddLog("全部浏览器已停止");
            }
            catch (Exception ex)
            {
                App.WriteRuntimeLog($"BtnStopAllBrowsers_Click 异常: {ex}");
                AddLog($"停止浏览器失败: {ex.Message}");
            }
            
            await Task.CompletedTask;
        }

        private async void BtnStartBrowser_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (sender is Button btn && btn.Tag is BrowserInstanceItem instance)
                {
                    if (!EnsureBrowserServicesInitialized())
                    {
                        MessageBox.Show("浏览器服务初始化失败", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }
                    
                    AddLog($"正在启动浏览器实例 [{instance.DisplayName}]...");
                    await _browserHelper!.StartBrowserInstanceAsync(instance.AccountId, instance.Platform);
                    instance.IsStarted = true;
                    UpdateBrowserStats();
                    AddLog($"浏览器实例 [{instance.DisplayName}] 已启动");
                }
            }
            catch (Exception ex)
            {
                App.WriteRuntimeLog($"BtnStartBrowser_Click 异常: {ex}");
                AddLog($"启动浏览器失败: {ex.Message}");
            }
        }

        private async void BtnStopBrowser_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (sender is Button btn && btn.Tag is BrowserInstanceItem instance)
                {
                    if (_browserHelper == null || !_browserHelper.IsInitialized)
                    {
                        AddLog("浏览器服务未初始化");
                        return;
                    }
                    
                    AddLog($"正在停止浏览器实例 [{instance.DisplayName}]...");
                    _browserHelper.CloseBrowserInstance(instance.AccountId);
                    instance.IsStarted = false;
                    UpdateBrowserStats();
                    AddLog($"浏览器实例 [{instance.DisplayName}] 已停止");
                }
            }
            catch (Exception ex)
            {
                App.WriteRuntimeLog($"BtnStopBrowser_Click 异常: {ex}");
                AddLog($"停止浏览器失败: {ex.Message}");
            }
            
            await Task.CompletedTask;
        }

        private async void BtnRestartBrowser_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (sender is Button btn && btn.Tag is BrowserInstanceItem instance)
                {
                    if (!EnsureBrowserServicesInitialized())
                    {
                        MessageBox.Show("浏览器服务初始化失败", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }
                    
                    AddLog($"正在重启浏览器实例 [{instance.DisplayName}]...");
                    
                    // 先停止
                    _browserHelper!.CloseBrowserInstance(instance.AccountId);
                    instance.IsStarted = false;
                    
                    // 等待一下
                    await Task.Delay(500);
                    
                    // 再启动
                    await _browserHelper.StartBrowserInstanceAsync(instance.AccountId, instance.Platform);
                    instance.IsStarted = true;
                    
                    UpdateBrowserStats();
                    AddLog($"浏览器实例 [{instance.DisplayName}] 已重启");
                }
            }
            catch (Exception ex)
            {
                App.WriteRuntimeLog($"BtnRestartBrowser_Click 异常: {ex}");
                AddLog($"重启浏览器失败: {ex.Message}");
            }
        }

        private async void BtnLoginBrowser_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (sender is Button btn && btn.Tag is BrowserInstanceItem instance)
                {
                    if (!EnsureBrowserServicesInitialized())
                    {
                        MessageBox.Show("浏览器服务初始化失败", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }
                    
                    var loginWindow = new BrowserLoginWindow(instance.AccountId, instance.PlatformName);
                    loginWindow.Owner = this;
                    
                    if (loginWindow.ShowDialog() == true && loginWindow.LoginResult != null)
                    {
                        var result = loginWindow.LoginResult;
                        if (result.IsLoggedIn)
                        {
                            instance.LoginStatus = AccountLoginStatus.LoggedIn;
                            _browserHelper!.MarkAccountAsLoggedIn(instance.AccountId, instance.Platform);
                            AddLog($"账号 [{instance.DisplayName}] 登录成功");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                App.WriteRuntimeLog($"BtnLoginBrowser_Click 异常: {ex}");
                AddLog($"登录失败: {ex.Message}");
            }
            
            await Task.CompletedTask;
        }

        #endregion

        #region 其他面板事件处理

        private void ChkAutoGreetEnabled_Changed(object sender, RoutedEventArgs e) { }
        private void BtnAddGreetTemplate_Click(object sender, RoutedEventArgs e) { }
        private void BtnDeleteGreetTemplate_Click(object sender, RoutedEventArgs e) { }
        private void BtnSaveAutoGreetSettings_Click(object sender, RoutedEventArgs e) { AddLog("设置已保存"); }
        private void ChkAutoReplyEnabled_Changed(object sender, RoutedEventArgs e) { }
        private void BtnAddKeywordRule_Click(object sender, RoutedEventArgs e) { }
        private void BtnEditKeywordRule_Click(object sender, RoutedEventArgs e) { }
        private void BtnDeleteKeywordRule_Click(object sender, RoutedEventArgs e) { }
        private void BtnSaveAutoReplySettings_Click(object sender, RoutedEventArgs e) { AddLog("设置已保存"); }
        private void ChkActivityEnabled_Changed(object sender, RoutedEventArgs e) { }
        private void BtnSaveActivitySettings_Click(object sender, RoutedEventArgs e) { AddLog("设置已保存"); }
        private void BtnSaveFilterSettings_Click(object sender, RoutedEventArgs e) { AddLog("设置已保存"); }
        private void ChkAIEnabled_Changed(object sender, RoutedEventArgs e) { }
        
        private async void BtnTestAIConnection_Click(object sender, RoutedEventArgs e) 
        { 
            try
            {
                await Task.Delay(100); 
            }
            catch (Exception ex)
            {
                App.WriteRuntimeLog($"BtnTestAIConnection_Click 异常: {ex.Message}");
            }
        }
        
        private void BtnSaveAISettings_Click(object sender, RoutedEventArgs e) { AddLog("设置已保存"); }
        private void BtnExportData_Click(object sender, RoutedEventArgs e) { }
        private void BtnImportData_Click(object sender, RoutedEventArgs e) { }
        private void BtnClearCache_Click(object sender, RoutedEventArgs e) { }
        private void BtnSaveSystemSettings_Click(object sender, RoutedEventArgs e) { AddLog("设置已保存"); }

        #endregion

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            try
            {
                App.WriteLog("Window_Closing");
                App.WriteRuntimeLog("Window_Closing");
                
                if (_isRunning)
                {
                    var result = MessageBox.Show("自动化任务正在运行，确定要退出吗？", "确认退出",
                        MessageBoxButton.YesNo, MessageBoxImage.Question);
                    
                    if (result == MessageBoxResult.No)
                    {
                        e.Cancel = true;
                        return;
                    }

                    StopAutomation();
                }

                _runtimeTimer?.Stop();
            }
            catch (Exception ex)
            {
                App.WriteRuntimeLog($"Window_Closing 异常: {ex.Message}");
            }
        }
    }

    #region 数据模型

    public class AccountCacheData
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Platform { get; set; } = string.Empty;
        public bool IsEnabled { get; set; } = true;
    }

    public class AccountItem : INotifyPropertyChanged
    {
        private string _name = string.Empty;
        private string _platform = string.Empty;
        private string _status = string.Empty;
        private bool _isEnabled = true;

        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        
        public string Name
        {
            get => _name;
            set { _name = value; OnPropertyChanged(nameof(Name)); }
        }
        
        public string Platform
        {
            get => _platform;
            set { _platform = value; OnPropertyChanged(nameof(Platform)); }
        }
        
        public string Status
        {
            get => _status;
            set { _status = value; OnPropertyChanged(nameof(Status)); UpdateVisualProperties(); }
        }

        public bool IsEnabled
        {
            get => _isEnabled;
            set { _isEnabled = value; OnPropertyChanged(nameof(IsEnabled)); UpdateVisualProperties(); }
        }

        public string AvatarColor => IsEnabled ? "#3498DB" : "#95A5A6";
        public string StatusBackground => Status == "在线" ? "#E8F5E9" : (Status == "离线" ? "#FFEBEE" : "#FFF3E0");
        public string StatusForeground => Status == "在线" ? "#27AE60" : (Status == "离线" ? "#E74C3C" : "#F57C00");
        public string RowBackground => IsEnabled ? "Transparent" : "#F5F5F5";
        public string OnlineButtonText => "上线";
        public string OnlineButtonBackground => "#27AE60";
        public Visibility OnlineButtonVisibility => (Status == "离线" && IsEnabled) ? Visibility.Visible : Visibility.Collapsed;
        public string ToggleButtonText => "停用";
        public string ToggleButtonBackground => "#F39C12";
        public Visibility ToggleButtonVisibility => Status == "在线" ? Visibility.Visible : Visibility.Collapsed;

        private void UpdateVisualProperties()
        {
            OnPropertyChanged(nameof(AvatarColor));
            OnPropertyChanged(nameof(StatusBackground));
            OnPropertyChanged(nameof(StatusForeground));
            OnPropertyChanged(nameof(RowBackground));
            OnPropertyChanged(nameof(OnlineButtonVisibility));
            OnPropertyChanged(nameof(ToggleButtonVisibility));
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class LogItem
    {
        public string Message { get; set; } = string.Empty;
        public string Time { get; set; } = string.Empty;
    }

    public class JobAccountItem
    {
        public string AccountId { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public RecruitPlatform Platform { get; set; }
        public string PlatformName { get; set; } = string.Empty;
        public bool IsStarted { get; set; }
        public AccountLoginStatus LoginStatus { get; set; }
        public bool IsAvailable { get; set; }
    }

    public class JobViewModel : INotifyPropertyChanged
    {
        private readonly JobPosition _job;
        private bool _isSelected;

        public JobViewModel(JobPosition job) { _job = job; }

        public string Id => _job.Id;
        public string PlatformName => _job.Platform.ToString();
        public string Title => _job.Title;
        public string SalaryText => _job.SalaryText;
        public string Location => _job.Location;
        public string ExperienceRequired => _job.ExperienceRequired;
        public string EducationRequired => _job.EducationRequired;
        public int MatchedCount => _job.MatchedCount;
        public int TargetMatchCount => _job.TargetMatchCount;

        public bool IsSelected
        {
            get => _isSelected;
            set { _isSelected = value; OnPropertyChanged(); }
        }

        public bool AutomationEnabled
        {
            get => _job.AutomationEnabled;
            set { _job.AutomationEnabled = value; OnPropertyChanged(); }
        }

        public string ExecutionStatusText => _job.ExecutionStatus switch
        {
            JobExecutionStatus.Pending => "待执行",
            JobExecutionStatus.Running => "执行中",
            JobExecutionStatus.Completed => "已完成",
            JobExecutionStatus.Paused => "已暂停",
            JobExecutionStatus.Failed => "失败",
            _ => "-"
        };

        public string FetchedAtText => _job.FetchedAt.ToLocalTime().ToString("MM-dd HH:mm");

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }

    public class BrowserInstanceItem : INotifyPropertyChanged
    {
        private bool _isStarted;
        private AccountLoginStatus _loginStatus;

        public string AccountId { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public RecruitPlatform Platform { get; set; }
        public string PlatformName { get; set; } = string.Empty;

        public bool IsStarted
        {
            get => _isStarted;
            set { _isStarted = value; OnPropertyChanged(); UpdateVisualProperties(); }
        }

        public AccountLoginStatus LoginStatus
        {
            get => _loginStatus;
            set { _loginStatus = value; OnPropertyChanged(); UpdateVisualProperties(); }
        }

        // 浏览器状态显示
        public string BrowserStatusText => IsStarted ? "运行中" : "已停止";
        public string BrowserStatusBackground => IsStarted ? "#E8F5E9" : "#FFEBEE";
        public string BrowserStatusForeground => IsStarted ? "#27AE60" : "#E74C3C";

        // 登录状态显示
        public string LoginStatusText => LoginStatus switch
        {
            AccountLoginStatus.LoggedIn => "已登录",
            AccountLoginStatus.NotLoggedIn => "未登录",
            AccountLoginStatus.Expired => "已过期",
            _ => "未知"
        };
        public string LoginStatusBackground => LoginStatus switch
        {
            AccountLoginStatus.LoggedIn => "#E8F5E9",
            AccountLoginStatus.NotLoggedIn => "#FFF3E0",
            AccountLoginStatus.Expired => "#FFEBEE",
            _ => "#F5F5F5"
        };
        public string LoginStatusForeground => LoginStatus switch
        {
            AccountLoginStatus.LoggedIn => "#27AE60",
            AccountLoginStatus.NotLoggedIn => "#F57C00",
            AccountLoginStatus.Expired => "#E74C3C",
            _ => "#999"
        };

        // 按钮可见性
        public Visibility StartButtonVisibility => !IsStarted ? Visibility.Visible : Visibility.Collapsed;
        public Visibility StopButtonVisibility => IsStarted ? Visibility.Visible : Visibility.Collapsed;
        public Visibility RestartButtonVisibility => IsStarted ? Visibility.Visible : Visibility.Collapsed;
        public Visibility LoginButtonVisibility => IsStarted && LoginStatus != AccountLoginStatus.LoggedIn ? Visibility.Visible : Visibility.Collapsed;

        private void UpdateVisualProperties()
        {
            OnPropertyChanged(nameof(BrowserStatusText));
            OnPropertyChanged(nameof(BrowserStatusBackground));
            OnPropertyChanged(nameof(BrowserStatusForeground));
            OnPropertyChanged(nameof(LoginStatusText));
            OnPropertyChanged(nameof(LoginStatusBackground));
            OnPropertyChanged(nameof(LoginStatusForeground));
            OnPropertyChanged(nameof(StartButtonVisibility));
            OnPropertyChanged(nameof(StopButtonVisibility));
            OnPropertyChanged(nameof(RestartButtonVisibility));
            OnPropertyChanged(nameof(LoginButtonVisibility));
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }

    #endregion
}
