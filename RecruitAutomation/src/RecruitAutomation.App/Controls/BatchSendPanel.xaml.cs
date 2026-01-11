using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using RecruitAutomation.Browser;
using RecruitAutomation.Browser.ContactReader;
using RecruitAutomation.Core.Models;
using RecruitAutomation.Core.Services;

namespace RecruitAutomation.App.Controls
{
    public partial class BatchSendPanel : UserControl
    {
        private readonly ObservableCollection<LogItem> _logs = new();
        private readonly Dictionary<string, BatchSendService> _services = new();
        private readonly Dictionary<string, BossContactReader> _readers = new();
        
        private CancellationTokenSource? _cts;
        private string? _currentAccountId;
        private bool _isPaused;

        public BatchSendPanel()
        {
            InitializeComponent();
            lstLogs.ItemsSource = _logs;
            
            cmbTemplates.SelectionChanged += CmbTemplates_SelectionChanged;
        }

        /// <summary>
        /// 初始化面板
        /// </summary>
        public async Task InitializeAsync()
        {
            await RefreshAccountsAsync();
        }

        #region 账号管理

        private async void BtnRefreshAccounts_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                await RefreshAccountsAsync();
            }
            catch (Exception ex)
            {
                AddLog($"刷新账号异常: {ex.Message}");
            }
        }

        private async Task RefreshAccountsAsync()
        {
            await Task.Yield(); // 确保异步执行
            try
            {
                var accounts = new List<BatchSendAccountItem>();
                var instances = BrowserInstanceManager.Instance.GetAllInstances();

                foreach (var instance in instances)
                {
                    if (instance.IsInitialized)
                    {
                        accounts.Add(new BatchSendAccountItem
                        {
                            AccountId = instance.AccountId,
                            DisplayName = instance.AccountId
                        });
                    }
                }

                cmbAccounts.ItemsSource = accounts;
                
                if (accounts.Count > 0)
                {
                    cmbAccounts.SelectedIndex = 0;
                }
                else
                {
                    AddLog("没有可用的账号，请先添加账号并登录");
                }
            }
            catch (Exception ex)
            {
                AddLog($"刷新账号失败: {ex.Message}");
            }
        }

        private async void CmbAccounts_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                if (cmbAccounts.SelectedItem is BatchSendAccountItem account)
                {
                    _currentAccountId = account.AccountId;
                    await LoadAccountDataAsync(account.AccountId);
                }
            }
            catch (Exception ex)
            {
                AddLog($"账号切换异常: {ex.Message}");
            }
        }

        private async Task LoadAccountDataAsync(string accountId)
        {
            try
            {
                // 获取或创建服务
                if (!_services.TryGetValue(accountId, out var service))
                {
                    service = new BatchSendService(accountId);
                    service.OnLog += (s, msg) => Dispatcher.Invoke(() => AddLog(msg));
                    service.OnStatusChanged += (s, status) => Dispatcher.Invoke(() => UpdateStatus(status));
                    service.OnItemSent += (s, result) => Dispatcher.Invoke(() => OnItemSent(result));
                    _services[accountId] = service;
                }

                // 加载统计
                var stats = await service.GetStatsAsync();
                UpdateStats(stats);

                // 加载模板
                var templates = await service.GetTemplatesAsync();
                cmbTemplates.ItemsSource = templates;
                if (templates.Count > 0)
                {
                    cmbTemplates.SelectedIndex = 0;
                }

                btnStartBatchSend.IsEnabled = stats.EligibleCount > 0;
            }
            catch (Exception ex)
            {
                AddLog($"加载账号数据失败: {ex.Message}");
            }
        }

        #endregion

        #region 读取联系人

        private async void BtnReadContacts_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (string.IsNullOrEmpty(_currentAccountId))
                {
                    MessageBox.Show("请先选择账号", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var instance = BrowserInstanceManager.Instance.Get(_currentAccountId);
                if (instance == null || !instance.IsInitialized)
                {
                    MessageBox.Show("账号浏览器未启动，请先登录账号", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                btnReadContacts.IsEnabled = false;
                _cts = new CancellationTokenSource();

                try
                {
                    AddLog("开始读取联系人...");

                    // 获取或创建读取器
                    if (!_readers.TryGetValue(_currentAccountId, out var reader))
                    {
                        reader = new BossContactReader(instance);
                        reader.OnLog += (s, msg) => Dispatcher.Invoke(() => AddLog(msg));
                        _readers[_currentAccountId] = reader;
                    }

                    // 读取联系人
                    var contacts = await reader.ReadContactsAsync(100, _cts.Token);

                    if (contacts.Count > 0)
                    {
                        // 同步到本地
                        var service = _services[_currentAccountId];
                        await service.SyncContactsAsync(contacts);

                        // 刷新统计
                        var stats = await service.GetStatsAsync();
                        UpdateStats(stats);

                        btnStartBatchSend.IsEnabled = stats.EligibleCount > 0;
                        AddLog($"联系人同步完成，共 {contacts.Count} 个，可群发 {stats.EligibleCount} 个");
                    }
                    else
                    {
                        AddLog("未读取到联系人，请确保已登录并有聊天记录");
                    }
                }
                catch (OperationCanceledException)
                {
                    AddLog("读取已取消");
                }
                catch (Exception ex)
                {
                    AddLog($"读取联系人失败: {ex.Message}");
                }
                finally
                {
                    btnReadContacts.IsEnabled = true;
                    _cts?.Dispose();
                    _cts = null;
                }
            }
            catch (Exception ex)
            {
                AddLog($"BtnReadContacts_Click 异常: {ex.Message}");
            }
        }

        #endregion

        #region 群发操作

        private async void BtnStartBatchSend_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (string.IsNullOrEmpty(_currentAccountId))
                {
                    MessageBox.Show("请先选择账号", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var instance = BrowserInstanceManager.Instance.Get(_currentAccountId);
                if (instance == null || !instance.IsInitialized)
                {
                    MessageBox.Show("账号浏览器未启动", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // 获取配置
                var targetCount = GetSelectedSendCount();
                var interval = int.TryParse(txtInterval.Text, out var i) ? i : 3000;

                var config = new BatchSendConfig
                {
                    BatchSize = targetCount,
                    IntervalMs = interval,
                    SkipIfSentMoreThan = chkSkipSentLimit.IsChecked == true ? 5 : int.MaxValue,
                    SkipIfReplied = chkSkipReplied.IsChecked == true
                };

                // 确认
                var result = MessageBox.Show(
                    $"即将向 {targetCount} 个联系人发送消息，是否继续？\n\n" +
                    $"• 已发送≥5条的联系人将被跳过\n" +
                    $"• 已回复的联系人将被跳过\n" +
                    $"• 发送间隔约 {interval}ms（随机波动）",
                    "确认群发",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result != MessageBoxResult.Yes)
                    return;

                // 开始群发
                _cts = new CancellationTokenSource();
                _isPaused = false;

                // 更新UI
                btnStartBatchSend.IsEnabled = false;
                btnReadContacts.IsEnabled = false;
                btnPauseBatchSend.Visibility = Visibility.Visible;
                btnStopBatchSend.Visibility = Visibility.Visible;
                statusPanel.Visibility = Visibility.Visible;

                txtStatTarget.Text = targetCount.ToString();

                try
                {
                    var service = _services[_currentAccountId];
                    service.LoadConfig(config);

                    // 设置变量
                    service.Variables["岗位名称"] = "技术岗位";
                    service.Variables["公司名"] = "我们公司";

                    // 绑定浏览器操作
                    var reader = _readers.GetValueOrDefault(_currentAccountId) ?? new BossContactReader(instance);
                    
                    service.OpenConversationHandler = async (contactId, ct) => 
                        await reader.OpenConversationAsync(contactId, ct);
                    
                    service.SendMessageHandler = async (contactId, message, ct) => 
                        await reader.SendMessageAsync(message, ct);
                    
                    service.CheckHasReplyHandler = async (contactId) => 
                        await reader.CheckHasNewReplyAsync(contactId);
                    
                    service.CheckHasWarningHandler = async () => 
                        await reader.CheckHasWarningAsync();

                    // 执行群发
                    var summary = await service.ExecuteBatchSendAsync(targetCount, _cts.Token);

                    // 显示结果
                    MessageBox.Show(
                        $"群发完成！\n\n" +
                        $"成功: {summary.SuccessCount}\n" +
                        $"失败: {summary.FailedCount}\n" +
                        $"跳过: {summary.SkippedCount}\n" +
                        $"耗时: {summary.Duration.TotalMinutes:F1} 分钟",
                        "群发完成",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
                catch (OperationCanceledException)
                {
                    AddLog("群发已停止");
                }
                catch (Exception ex)
                {
                    AddLog($"群发出错: {ex.Message}");
                    MessageBox.Show($"群发出错: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                finally
                {
                    // 恢复UI
                    btnStartBatchSend.IsEnabled = true;
                    btnReadContacts.IsEnabled = true;
                    btnPauseBatchSend.Visibility = Visibility.Collapsed;
                    btnStopBatchSend.Visibility = Visibility.Collapsed;
                    
                    _cts?.Dispose();
                    _cts = null;

                    // 刷新统计
                    if (_services.TryGetValue(_currentAccountId, out var svc))
                    {
                        var stats = await svc.GetStatsAsync();
                        UpdateStats(stats);
                    }
                }
            }
            catch (Exception ex)
            {
                AddLog($"BtnStartBatchSend_Click 异常: {ex.Message}");
            }
        }

        private void BtnPauseBatchSend_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_currentAccountId) || !_services.TryGetValue(_currentAccountId, out var service))
                return;

            _isPaused = !_isPaused;

            if (_isPaused)
            {
                service.Pause();
                btnPauseBatchSend.Content = "▶ 继续";
            }
            else
            {
                service.Resume();
                btnPauseBatchSend.Content = "⏸ 暂停";
            }
        }

        private void BtnStopBatchSend_Click(object sender, RoutedEventArgs e)
        {
            _cts?.Cancel();
        }

        #endregion

        #region UI更新

        private void UpdateStatus(BatchSendTaskStatus status)
        {
            txtCurrentContact.Text = string.IsNullOrEmpty(status.CurrentContact) 
                ? "当前：-" 
                : $"当前：{status.CurrentContact}";
            
            txtDelayStatus.Text = status.DelayStatus;
            txtProgress.Text = $"{status.SentCount}/{status.TargetCount}";
            
            if (status.TargetCount > 0)
            {
                progressBar.Value = (double)status.SentCount / status.TargetCount * 100;
            }

            txtStatSent.Text = status.SentCount.ToString();
            txtStatSkipped.Text = status.SkippedCount.ToString();

            txtSkipSentLimit.Text = status.SkipStats.SentLimitReached.ToString();
            txtSkipReplied.Text = status.SkipStats.AlreadyReplied.ToString();
            txtSkipSystem.Text = status.SkipStats.SystemMarked.ToString();

            warningPanel.Visibility = status.NeedSlowDown ? Visibility.Visible : Visibility.Collapsed;
        }

        private void UpdateStats(BatchSendStats stats)
        {
            txtStatEligible.Text = stats.EligibleCount.ToString();
            txtSkipSentLimit.Text = stats.SkippedBySentLimit.ToString();
            txtSkipReplied.Text = stats.SkippedByReplied.ToString();
            txtSkipSystem.Text = stats.SkippedBySystem.ToString();
        }

        private void OnItemSent(BatchSendService.BatchSendItemResult result)
        {
            var status = result.Success ? "✓" : "✗";
            AddLog($"[{result.Index}/{result.Total}] {status} {result.CandidateName}");
        }

        private void CmbTemplates_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cmbTemplates.SelectedItem is BatchSendTemplate template)
            {
                txtTemplatePreview.Text = template.Content;
            }
        }

        private int GetSelectedSendCount()
        {
            if (cmbSendCount.SelectedItem is ComboBoxItem item && item.Tag is string tag)
            {
                return int.TryParse(tag, out var count) ? count : 20;
            }
            return 20;
        }

        #endregion

        #region 日志

        private void AddLog(string message)
        {
            _logs.Insert(0, new LogItem
            {
                Message = message,
                Time = DateTime.Now.ToString("HH:mm:ss")
            });

            if (_logs.Count > 200)
            {
                _logs.RemoveAt(_logs.Count - 1);
            }

            txtLogCount.Text = $"共 {_logs.Count} 条日志";
        }

        private void BtnClearLogs_Click(object sender, RoutedEventArgs e)
        {
            _logs.Clear();
            txtLogCount.Text = "共 0 条日志";
        }

        #endregion

        public class LogItem
        {
            public string Message { get; set; } = string.Empty;
            public string Time { get; set; } = string.Empty;
        }

        public class BatchSendAccountItem
        {
            public string AccountId { get; set; } = string.Empty;
            public string DisplayName { get; set; } = string.Empty;
        }
    }
}
