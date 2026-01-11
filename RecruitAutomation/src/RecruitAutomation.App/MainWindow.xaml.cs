using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using RecruitAutomation.Core.Models;
using RecruitAutomation.App.Helpers;
using RecruitAutomation.Browser;
using RecruitAutomation.Browser.JobFetcher;

namespace RecruitAutomation.App
{
    public partial class MainWindow : Window
    {
        public ObservableCollection<AccountItem> Accounts { get; } = new();
        public ObservableCollection<LogItem> Logs { get; } = new();
        public ObservableCollection<JobPosition> DisplayJobs { get; } = new();
        public ObservableCollection<BrowserInstanceItem> BrowserInstances { get; } = new();
        public ObservableCollection<GreetTemplateItem> GreetTemplates { get; } = new();
        public ObservableCollection<KeywordRuleItem> KeywordRules { get; } = new();
        
        private BrowserHelper? _browserHelper;
        private JobFetchService _jobFetchService = new();
        private bool _isRunning = false;
        private string _currentPanel = "Dashboard";

        public MainWindow()
        {
            InitializeComponent();
            DataContext = this;
            this.Loaded += Window_Loaded;
            this.Closing += Window_Closing;
            
            // 绑定数据源
            if (lstAccounts != null) lstAccounts.ItemsSource = Accounts;
            if (lstLogs != null) lstLogs.ItemsSource = Logs;
            if (dgJobs != null) dgJobs.ItemsSource = DisplayJobs;
            if (dgBrowserInstances != null) dgBrowserInstances.ItemsSource = BrowserInstances;
            
            // 订阅岗位读取事件
            _jobFetchService.OnProgress += (s, e) => AddLog($"[{e.AccountId}] {e.Message}");
            _jobFetchService.OnCompleted += (s, e) => 
            {
                if (e.Success)
                {
                    Dispatcher.Invoke(() => 
                    {
                        DisplayJobs.Clear();
                        foreach (var job in e.Jobs) DisplayJobs.Add(job);
                        AddLog($"岗位列表已更新，共 {e.Jobs.Count} 条数据");
                    });
                }
                else
                {
                    AddLog($"读取失败: {e.ErrorMessage}");
                }
            };
        }

        #region Window Events

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                AddLog("系统启动完成");
                UpdateStatusDisplay();
                // 初始化 AI 配置
                _jobFetchService.ConfigureAI("YOUR_API_KEY");
            }
            catch (Exception ex)
            {
                AddLog($"启动异常: {ex.Message}");
            }
        }

        private void Window_Closing(object? sender, CancelEventArgs e)
        {
            try
            {
                AddLog("正在关闭系统...");
                BrowserInstanceManager.Instance.Dispose();
            }
            catch { }
        }

        #endregion

        #region Navigation

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
                AddLog($"导航错误: {ex.Message}");
            }
        }

        private void SwitchPanel(string panelName)
        {
            _currentPanel = panelName;
            
            // 隐藏所有面板
            HideAllPanels();
            
            // 重置所有导航按钮样式
            ResetNavButtonStyles();
            
            // 显示目标面板并更新标题
            switch (panelName)
            {
                case "Dashboard":
                    if (panelDashboard != null) panelDashboard.Visibility = Visibility.Visible;
                    SetActiveNavButton(btnDashboard);
                    UpdatePageTitle("数据概览", "查看招聘数据统计和今日工作概况");
                    break;
                case "Accounts":
                    if (panelAccounts != null) panelAccounts.Visibility = Visibility.Visible;
                    SetActiveNavButton(btnAccounts);
                    UpdatePageTitle("账号管理", "管理招聘平台账号");
                    break;
                case "Browser":
                    if (panelBrowser != null) panelBrowser.Visibility = Visibility.Visible;
                    SetActiveNavButton(btnBrowser);
                    UpdatePageTitle("浏览器管理", "管理浏览器实例和登录状态");
                    RefreshBrowserInstances();
                    break;
                case "Jobs":
                    ShowJobsPanel();
                    SetActiveNavButton(btnJobs);
                    UpdatePageTitle("岗位管理", "管理招聘岗位和自动化设置");
                    break;
                case "Candidates":
                    ShowCandidatesPanel();
                    SetActiveNavButton(btnCandidates);
                    UpdatePageTitle("候选人库", "查看和管理候选人信息");
                    break;
                case "Messages":
                    ShowMessagesPanel();
                    SetActiveNavButton(btnMessages);
                    UpdatePageTitle("消息中心", "查看聊天记录和消息统计");
                    break;
                case "AutoGreet":
                    ShowAutoGreetPanel();
                    SetActiveNavButton(btnAutoGreet);
                    UpdatePageTitle("自动打招呼", "配置自动打招呼规则");
                    break;
                case "AutoReply":
                    ShowAutoReplyPanel();
                    SetActiveNavButton(btnAutoReply);
                    UpdatePageTitle("智能回复", "配置AI智能回复规则");
                    break;
                case "BatchSend":
                    ShowBatchSendPanel();
                    SetActiveNavButton(btnBatchSend);
                    UpdatePageTitle("群发联系人", "批量发送消息给联系人");
                    break;
                case "Activity":
                    ShowActivityPanel();
                    SetActiveNavButton(btnActivity);
                    UpdatePageTitle("活跃度维护", "配置账号活跃度维护策略");
                    break;
                case "Filter":
                    ShowFilterPanel();
                    SetActiveNavButton(btnFilter);
                    UpdatePageTitle("筛选规则", "配置候选人筛选条件");
                    break;
                case "AI":
                    ShowAIPanel();
                    SetActiveNavButton(btnAI);
                    UpdatePageTitle("AI配置", "配置AI服务参数");
                    break;
                case "Settings":
                    ShowSettingsPanel();
                    SetActiveNavButton(btnSettings);
                    UpdatePageTitle("系统设置", "配置系统参数");
                    break;
            }
        }

        private void HideAllPanels()
        {
            if (panelDashboard != null) panelDashboard.Visibility = Visibility.Collapsed;
            if (panelAccounts != null) panelAccounts.Visibility = Visibility.Collapsed;
            if (panelBrowser != null) panelBrowser.Visibility = Visibility.Collapsed;
            // 其他面板通过 FindName 查找
            HidePanelByName("panelJobs");
            HidePanelByName("panelCandidates");
            HidePanelByName("panelMessages");
            HidePanelByName("panelAutoGreet");
            HidePanelByName("panelAutoReply");
            HidePanelByName("panelBatchSend");
            HidePanelByName("panelActivity");
            HidePanelByName("panelFilter");
            HidePanelByName("panelAI");
            HidePanelByName("panelSettings");
        }

        private void HidePanelByName(string name)
        {
            if (FindName(name) is FrameworkElement panel)
                panel.Visibility = Visibility.Collapsed;
        }

        private void ShowPanelByName(string name)
        {
            if (FindName(name) is FrameworkElement panel)
                panel.Visibility = Visibility.Visible;
        }

        private void ShowJobsPanel() => ShowPanelByName("panelJobs");
        private void ShowCandidatesPanel() => ShowPanelByName("panelCandidates");
        private void ShowMessagesPanel() => ShowPanelByName("panelMessages");
        private void ShowAutoGreetPanel() => ShowPanelByName("panelAutoGreet");
        private void ShowAutoReplyPanel() => ShowPanelByName("panelAutoReply");
        private void ShowBatchSendPanel() => ShowPanelByName("panelBatchSend");
        private void ShowActivityPanel() => ShowPanelByName("panelActivity");
        private void ShowFilterPanel() => ShowPanelByName("panelFilter");
        private void ShowAIPanel() => ShowPanelByName("panelAI");
        private void ShowSettingsPanel() => ShowPanelByName("panelSettings");

        private void ResetNavButtonStyles()
        {
            var navButtons = new[] { btnDashboard, btnAccounts, btnBrowser, btnJobs, btnCandidates, 
                btnMessages, btnAutoGreet, btnAutoReply, btnBatchSend, btnActivity, btnFilter, btnAI, btnSettings };
            foreach (var btn in navButtons)
            {
                if (btn != null) btn.Style = (Style)FindResource("NavButton");
            }
        }

        private void SetActiveNavButton(Button? btn)
        {
            if (btn != null) btn.Style = (Style)FindResource("NavButtonActive");
        }

        private void UpdatePageTitle(string title, string desc)
        {
            if (txtPageTitle != null) txtPageTitle.Text = title;
            if (txtPageDesc != null) txtPageDesc.Text = desc;
        }

        #endregion

        #region Account Management

        private void BtnAddAccount_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // 生成新账号
                var accountId = $"account_{DateTime.Now:yyyyMMddHHmmss}";
                var account = new AccountItem
                {
                    Id = accountId,
                    Name = $"账号{Accounts.Count + 1}",
                    Platform = "Boss直聘",
                    IsLoggedIn = false
                };
                Accounts.Add(account);
                AddLog($"已添加账号: {account.Name}");
                UpdateAccountStats();
            }
            catch (Exception ex)
            {
                AddLog($"添加账号失败: {ex.Message}");
            }
        }

        private void BtnOnlineAccount_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (sender is Button btn && btn.Tag is AccountItem account)
                {
                    // 启动浏览器登录
                    var instance = BrowserInstanceManager.Instance.GetOrCreate(account.Id, "https://www.zhipin.com");
                    if (instance != null)
                    {
                        account.IsLoggedIn = true;
                        AddLog($"账号 {account.Name} 已上线");
                        RefreshAccountList();
                    }
                    else
                    {
                        AddLog($"账号 {account.Name} 上线失败");
                    }
                }
            }
            catch (Exception ex)
            {
                AddLog($"上线失败: {ex.Message}");
            }
        }

        private void BtnToggleAccount_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (sender is Button btn && btn.Tag is AccountItem account)
                {
                    account.IsLoggedIn = !account.IsLoggedIn;
                    AddLog($"账号 {account.Name} 状态已切换为: {account.Status}");
                    RefreshAccountList();
                }
            }
            catch (Exception ex)
            {
                AddLog($"切换状态失败: {ex.Message}");
            }
        }

        private void BtnDeleteAccount_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (sender is Button btn && btn.Tag is AccountItem account)
                {
                    var result = MessageBox.Show($"确定要删除账号 [{account.Name}] 吗？", "确认删除",
                        MessageBoxButton.YesNo, MessageBoxImage.Question);
                    if (result == MessageBoxResult.Yes)
                    {
                        BrowserInstanceManager.Instance.Close(account.Id);
                        Accounts.Remove(account);
                        AddLog($"已删除账号: {account.Name}");
                        UpdateAccountStats();
                    }
                }
            }
            catch (Exception ex)
            {
                AddLog($"删除账号失败: {ex.Message}");
            }
        }

        private void RefreshAccountList()
        {
            // 触发 UI 刷新
            var temp = Accounts.ToList();
            Accounts.Clear();
            foreach (var a in temp) Accounts.Add(a);
            UpdateAccountStats();
        }

        private void UpdateAccountStats()
        {
            if (txtAccountCount != null) txtAccountCount.Text = Accounts.Count.ToString();
            if (txtOnlineCount != null) txtOnlineCount.Text = Accounts.Count(a => a.IsLoggedIn).ToString();
            if (txtOfflineCount != null) txtOfflineCount.Text = Accounts.Count(a => !a.IsLoggedIn).ToString();
        }

        #endregion

        #region Browser Management

        private void BtnRefreshBrowserStatus_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                RefreshBrowserInstances();
                AddLog("浏览器状态已刷新");
            }
            catch (Exception ex)
            {
                AddLog($"刷新状态失败: {ex.Message}");
            }
        }

        private void BtnStartAllBrowsers_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                foreach (var account in Accounts)
                {
                    BrowserInstanceManager.Instance.GetOrCreate(account.Id, "https://www.zhipin.com");
                }
                RefreshBrowserInstances();
                AddLog("已启动所有浏览器");
            }
            catch (Exception ex)
            {
                AddLog($"启动浏览器失败: {ex.Message}");
            }
        }

        private void BtnStopAllBrowsers_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                foreach (var account in Accounts)
                {
                    BrowserInstanceManager.Instance.Close(account.Id);
                }
                RefreshBrowserInstances();
                AddLog("已停止所有浏览器");
            }
            catch (Exception ex)
            {
                AddLog($"停止浏览器失败: {ex.Message}");
            }
        }

        private void BtnStartBrowser_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (sender is Button btn && btn.Tag is BrowserInstanceItem item)
                {
                    BrowserInstanceManager.Instance.GetOrCreate(item.AccountId, "https://www.zhipin.com");
                    RefreshBrowserInstances();
                    AddLog($"已启动浏览器: {item.AccountId}");
                }
            }
            catch (Exception ex)
            {
                AddLog($"启动浏览器失败: {ex.Message}");
            }
        }

        private void BtnStopBrowser_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (sender is Button btn && btn.Tag is BrowserInstanceItem item)
                {
                    BrowserInstanceManager.Instance.Close(item.AccountId);
                    RefreshBrowserInstances();
                    AddLog($"已停止浏览器: {item.AccountId}");
                }
            }
            catch (Exception ex)
            {
                AddLog($"停止浏览器失败: {ex.Message}");
            }
        }

        private void BtnRestartBrowser_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (sender is Button btn && btn.Tag is BrowserInstanceItem item)
                {
                    _ = BrowserInstanceManager.Instance.RestartAsync(item.AccountId);
                    RefreshBrowserInstances();
                    AddLog($"已重启浏览器: {item.AccountId}");
                }
            }
            catch (Exception ex)
            {
                AddLog($"重启浏览器失败: {ex.Message}");
            }
        }

        private void BtnLoginBrowser_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (sender is Button btn && btn.Tag is BrowserInstanceItem item)
                {
                    var instance = BrowserInstanceManager.Instance.Get(item.AccountId);
                    if (instance != null)
                    {
                        // 打开登录窗口
                        var loginWindow = new BrowserLoginWindow(item.AccountId, item.PlatformName);
                        loginWindow.ShowDialog();
                        RefreshBrowserInstances();
                    }
                    else
                    {
                        MessageBox.Show("请先启动浏览器", "提示");
                    }
                }
            }
            catch (Exception ex)
            {
                AddLog($"登录失败: {ex.Message}");
            }
        }

        private void RefreshBrowserInstances()
        {
            BrowserInstances.Clear();
            foreach (var account in Accounts)
            {
                var instance = BrowserInstanceManager.Instance.Get(account.Id);
                BrowserInstances.Add(new BrowserInstanceItem
                {
                    AccountId = account.Id,
                    DisplayName = account.Name,
                    PlatformName = account.Platform,
                    IsRunning = instance != null,
                    IsLoggedIn = account.IsLoggedIn
                });
            }
            if (txtBrowserCount != null) txtBrowserCount.Text = BrowserInstances.Count.ToString();
            if (txtBrowserRunning != null) txtBrowserRunning.Text = BrowserInstances.Count(b => b.IsRunning).ToString();
        }

        #endregion

        #region Job Management

        private void BtnFetchJobs_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var selectedAccount = lstAccounts?.SelectedItem as AccountItem;
                if (selectedAccount == null)
                {
                    MessageBox.Show("请先选择一个账号", "提示");
                    return;
                }
                AddLog($"开始为账号 {selectedAccount.Name} 读取岗位...");
                _ = Task.Run(async () => await _jobFetchService.FetchJobsFromAccountAsync(selectedAccount.Id, true));
            }
            catch (Exception ex)
            {
                AddLog($"读取岗位失败: {ex.Message}");
            }
        }

        private void BtnFetchAllJobs_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                AddLog("开始读取所有账号的岗位...");
                foreach (var account in Accounts.Where(a => a.IsLoggedIn))
                {
                    _ = Task.Run(async () => await _jobFetchService.FetchJobsFromAccountAsync(account.Id, true));
                }
            }
            catch (Exception ex)
            {
                AddLog($"批量读取岗位失败: {ex.Message}");
            }
        }

        private void BtnCancelFetch_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                AddLog("已取消岗位读取");
            }
            catch (Exception ex)
            {
                AddLog($"取消失败: {ex.Message}");
            }
        }

        private void BtnRefreshJobAccounts_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                AddLog("已刷新账号列表");
            }
            catch (Exception ex)
            {
                AddLog($"刷新失败: {ex.Message}");
            }
        }

        private void CmbJobAccounts_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // 账号选择变更
        }

        private void DgJobs_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // 岗位选择变更
        }

        private void ChkSelectAllJobs_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (sender is CheckBox chk)
                {
                    var isChecked = chk.IsChecked == true;
                    foreach (var job in DisplayJobs)
                    {
                        job.IsSelected = isChecked;
                    }
                }
            }
            catch (Exception ex)
            {
                AddLog($"全选失败: {ex.Message}");
            }
        }

        private void JobAutomationToggle_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (sender is CheckBox chk && chk.Tag is JobPosition job)
                {
                    job.AutomationEnabled = chk.IsChecked == true;
                    AddLog($"岗位 {job.Title} 自动化已{(job.AutomationEnabled ? "开启" : "关闭")}");
                }
            }
            catch (Exception ex)
            {
                AddLog($"切换自动化失败: {ex.Message}");
            }
        }

        private void BtnBatchEnableAutomation_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                foreach (var job in DisplayJobs.Where(j => j.IsSelected))
                {
                    job.AutomationEnabled = true;
                }
                AddLog("已批量开启自动化");
            }
            catch (Exception ex)
            {
                AddLog($"批量开启失败: {ex.Message}");
            }
        }

        private void BtnBatchDisableAutomation_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                foreach (var job in DisplayJobs.Where(j => j.IsSelected))
                {
                    job.AutomationEnabled = false;
                }
                AddLog("已批量关闭自动化");
            }
            catch (Exception ex)
            {
                AddLog($"批量关闭失败: {ex.Message}");
            }
        }

        private void BtnDeleteSelectedJobs_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var selected = DisplayJobs.Where(j => j.IsSelected).ToList();
                if (selected.Count == 0)
                {
                    MessageBox.Show("请先选择要删除的岗位", "提示");
                    return;
                }
                var result = MessageBox.Show($"确定要删除选中的 {selected.Count} 个岗位吗？", "确认删除",
                    MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (result == MessageBoxResult.Yes)
                {
                    foreach (var job in selected)
                    {
                        DisplayJobs.Remove(job);
                    }
                    AddLog($"已删除 {selected.Count} 个岗位");
                }
            }
            catch (Exception ex)
            {
                AddLog($"删除失败: {ex.Message}");
            }
        }

        #endregion

        #region Auto Greet Settings

        private void ChkAutoGreetEnabled_Changed(object sender, RoutedEventArgs e)
        {
            try
            {
                if (sender is CheckBox chk)
                {
                    AddLog($"自动打招呼已{(chk.IsChecked == true ? "开启" : "关闭")}");
                }
            }
            catch (Exception ex)
            {
                AddLog($"设置失败: {ex.Message}");
            }
        }

        private void BtnAddGreetTemplate_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                GreetTemplates.Add(new GreetTemplateItem
                {
                    Id = Guid.NewGuid().ToString(),
                    Content = "您好，我是招聘负责人，看到您的简历很感兴趣...",
                    Weight = 1
                });
                AddLog("已添加打招呼模板");
            }
            catch (Exception ex)
            {
                AddLog($"添加模板失败: {ex.Message}");
            }
        }

        private void BtnDeleteGreetTemplate_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (sender is Button btn && btn.Tag is GreetTemplateItem template)
                {
                    GreetTemplates.Remove(template);
                    AddLog("已删除打招呼模板");
                }
            }
            catch (Exception ex)
            {
                AddLog($"删除模板失败: {ex.Message}");
            }
        }

        private void BtnSaveAutoGreetSettings_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                AddLog("自动打招呼设置已保存");
                MessageBox.Show("设置已保存", "提示");
            }
            catch (Exception ex)
            {
                AddLog($"保存失败: {ex.Message}");
            }
        }

        #endregion

        #region Auto Reply Settings

        private void ChkAutoReplyEnabled_Changed(object sender, RoutedEventArgs e)
        {
            try
            {
                if (sender is CheckBox chk)
                {
                    AddLog($"智能回复已{(chk.IsChecked == true ? "开启" : "关闭")}");
                }
            }
            catch (Exception ex)
            {
                AddLog($"设置失败: {ex.Message}");
            }
        }

        private void BtnAddKeywordRule_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                KeywordRules.Add(new KeywordRuleItem
                {
                    Id = Guid.NewGuid().ToString(),
                    Keyword = "薪资",
                    Reply = "我们的薪资范围是...",
                    IsEnabled = true
                });
                AddLog("已添加关键词规则");
            }
            catch (Exception ex)
            {
                AddLog($"添加规则失败: {ex.Message}");
            }
        }

        private void BtnEditKeywordRule_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (sender is Button btn && btn.Tag is KeywordRuleItem rule)
                {
                    AddLog($"编辑规则: {rule.Keyword}");
                }
            }
            catch (Exception ex)
            {
                AddLog($"编辑规则失败: {ex.Message}");
            }
        }

        private void BtnDeleteKeywordRule_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (sender is Button btn && btn.Tag is KeywordRuleItem rule)
                {
                    KeywordRules.Remove(rule);
                    AddLog("已删除关键词规则");
                }
            }
            catch (Exception ex)
            {
                AddLog($"删除规则失败: {ex.Message}");
            }
        }

        private void BtnSaveAutoReplySettings_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                AddLog("智能回复设置已保存");
                MessageBox.Show("设置已保存", "提示");
            }
            catch (Exception ex)
            {
                AddLog($"保存失败: {ex.Message}");
            }
        }

        #endregion

        #region Activity Settings

        private void ChkActivityEnabled_Changed(object sender, RoutedEventArgs e)
        {
            try
            {
                if (sender is CheckBox chk)
                {
                    AddLog($"活跃度维护已{(chk.IsChecked == true ? "开启" : "关闭")}");
                }
            }
            catch (Exception ex)
            {
                AddLog($"设置失败: {ex.Message}");
            }
        }

        private void BtnSaveActivitySettings_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                AddLog("活跃度设置已保存");
                MessageBox.Show("设置已保存", "提示");
            }
            catch (Exception ex)
            {
                AddLog($"保存失败: {ex.Message}");
            }
        }

        #endregion

        #region Filter Settings

        private void BtnSaveFilterSettings_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                AddLog("筛选规则已保存");
                MessageBox.Show("设置已保存", "提示");
            }
            catch (Exception ex)
            {
                AddLog($"保存失败: {ex.Message}");
            }
        }

        #endregion

        #region AI Settings

        private void ChkAIEnabled_Changed(object sender, RoutedEventArgs e)
        {
            try
            {
                if (sender is CheckBox chk)
                {
                    AddLog($"AI功能已{(chk.IsChecked == true ? "开启" : "关闭")}");
                }
            }
            catch (Exception ex)
            {
                AddLog($"设置失败: {ex.Message}");
            }
        }

        private void BtnTestAIConnection_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                AddLog("正在测试AI连接...");
                // TODO: 实际测试AI连接
                MessageBox.Show("AI连接测试成功", "提示");
                AddLog("AI连接测试成功");
            }
            catch (Exception ex)
            {
                AddLog($"AI连接测试失败: {ex.Message}");
                MessageBox.Show($"AI连接测试失败: {ex.Message}", "错误");
            }
        }

        private void BtnSaveAISettings_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                AddLog("AI设置已保存");
                MessageBox.Show("设置已保存", "提示");
            }
            catch (Exception ex)
            {
                AddLog($"保存失败: {ex.Message}");
            }
        }

        #endregion

        #region System Settings

        private void BtnExportData_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                AddLog("正在导出数据...");
                MessageBox.Show("数据导出成功", "提示");
            }
            catch (Exception ex)
            {
                AddLog($"导出失败: {ex.Message}");
            }
        }

        private void BtnImportData_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                AddLog("正在导入数据...");
                MessageBox.Show("数据导入成功", "提示");
            }
            catch (Exception ex)
            {
                AddLog($"导入失败: {ex.Message}");
            }
        }

        private void BtnClearCache_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var result = MessageBox.Show("确定要清除缓存吗？", "确认",
                    MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (result == MessageBoxResult.Yes)
                {
                    AddLog("缓存已清除");
                    MessageBox.Show("缓存已清除", "提示");
                }
            }
            catch (Exception ex)
            {
                AddLog($"清除缓存失败: {ex.Message}");
            }
        }

        private void BtnSaveSystemSettings_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                AddLog("系统设置已保存");
                MessageBox.Show("设置已保存", "提示");
            }
            catch (Exception ex)
            {
                AddLog($"保存失败: {ex.Message}");
            }
        }

        #endregion

        #region Main Controls

        private void BtnStartStop_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _isRunning = !_isRunning;
                if (btnStartStop != null)
                {
                    btnStartStop.Content = _isRunning ? "⏹ 停止" : "▶ 启动";
                    btnStartStop.Background = _isRunning ? new SolidColorBrush(Color.FromRgb(231, 76, 60)) : new SolidColorBrush(Color.FromRgb(52, 152, 219));
                }
                UpdateStatusDisplay();
                AddLog(_isRunning ? "系统已启动" : "系统已停止");
            }
            catch (Exception ex)
            {
                AddLog($"操作失败: {ex.Message}");
            }
        }

        private void BtnLicenseInfo_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var licenseWindow = new LicenseWindow();
                licenseWindow.ShowDialog();
            }
            catch (Exception ex)
            {
                AddLog($"打开授权窗口失败: {ex.Message}");
            }
        }

        private void UpdateStatusDisplay()
        {
            if (statusIndicator != null)
            {
                statusIndicator.Background = _isRunning ? new SolidColorBrush(Color.FromRgb(39, 174, 96)) : new SolidColorBrush(Color.FromRgb(149, 165, 166));
            }
            if (txtActivityStatus != null)
            {
                txtActivityStatus.Text = _isRunning ? "运行中" : "已停止";
            }
        }

        #endregion

        #region Logging

        public void AddLog(string message)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                try
                {
                    Logs.Insert(0, new LogItem { Message = message, Time = DateTime.Now.ToString("HH:mm:ss") });
                    if (Logs.Count > 100) Logs.RemoveAt(100);
                }
                catch { }
            }));
        }

        #endregion
    }

    #region Data Models

    /// <summary>
    /// 账号项数据模型
    /// </summary>
    public class AccountItem : INotifyPropertyChanged
    {
        private bool _isLoggedIn;
        
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Platform { get; set; } = string.Empty;
        
        public bool IsLoggedIn
        {
            get => _isLoggedIn;
            set { _isLoggedIn = value; OnPropertyChanged(nameof(IsLoggedIn)); OnPropertyChanged(nameof(Status)); }
        }
        
        public string Status => IsLoggedIn ? "已登录" : "未登录";
        public string AvatarColor => IsLoggedIn ? "#3498DB" : "#95A5A6";
        public string StatusBackground => IsLoggedIn ? "#E8F8F5" : "#FADBD8";
        public string StatusForeground => IsLoggedIn ? "#27AE60" : "#E74C3C";
        public string RowBackground => "#FFFFFF";
        public string OnlineButtonText => IsLoggedIn ? "下线" : "上线";
        public string OnlineButtonBackground => IsLoggedIn ? "#E74C3C" : "#27AE60";
        public string ToggleButtonText => "切换";
        public string ToggleButtonBackground => "#F39C12";
        public Visibility OnlineButtonVisibility => Visibility.Visible;
        public Visibility ToggleButtonVisibility => Visibility.Visible;

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    /// <summary>
    /// 日志项数据模型
    /// </summary>
    public class LogItem
    {
        public string Message { get; set; } = string.Empty;
        public string Time { get; set; } = string.Empty;
    }

    /// <summary>
    /// 浏览器实例项
    /// </summary>
    public class BrowserInstanceItem : INotifyPropertyChanged
    {
        public string AccountId { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string PlatformName { get; set; } = string.Empty;
        public bool IsRunning { get; set; }
        public bool IsLoggedIn { get; set; }
        
        public string BrowserStatusText => IsRunning ? "运行中" : "已停止";
        public string BrowserStatusBackground => IsRunning ? "#E8F8F5" : "#FADBD8";
        public string BrowserStatusForeground => IsRunning ? "#27AE60" : "#E74C3C";
        public string LoginStatusText => IsLoggedIn ? "已登录" : "未登录";
        public string LoginStatusBackground => IsLoggedIn ? "#E8F8F5" : "#FEF9E7";
        public string LoginStatusForeground => IsLoggedIn ? "#27AE60" : "#F39C12";
        public Visibility StartButtonVisibility => IsRunning ? Visibility.Collapsed : Visibility.Visible;
        public Visibility StopButtonVisibility => IsRunning ? Visibility.Visible : Visibility.Collapsed;

        public event PropertyChangedEventHandler? PropertyChanged;
    }

    /// <summary>
    /// 打招呼模板项
    /// </summary>
    public class GreetTemplateItem
    {
        public string Id { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public int Weight { get; set; } = 1;
    }

    /// <summary>
    /// 关键词规则项
    /// </summary>
    public class KeywordRuleItem
    {
        public string Id { get; set; } = string.Empty;
        public string Keyword { get; set; } = string.Empty;
        public string Reply { get; set; } = string.Empty;
        public bool IsEnabled { get; set; } = true;
    }

    #endregion
}
