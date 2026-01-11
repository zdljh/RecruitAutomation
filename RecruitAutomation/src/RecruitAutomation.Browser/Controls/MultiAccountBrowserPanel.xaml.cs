using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using RecruitAutomation.Core.License;

namespace RecruitAutomation.Browser.Controls
{
    /// <summary>
    /// 多账号浏览器面板
    /// 支持同时运行多个账号，每个账号独立隔离
    /// </summary>
    public partial class MultiAccountBrowserPanel : UserControl, IDisposable
    {
        private readonly Dictionary<string, BrowserTabControl> _tabs = new();
        private readonly Dictionary<string, Button> _tabButtons = new();
        private string? _activeAccountId;
        private bool _disposed;

        /// <summary>
        /// 默认主页 URL
        /// </summary>
        public string DefaultHomeUrl { get; set; } = "https://www.zhipin.com";

        /// <summary>
        /// 当前活动账号 ID
        /// </summary>
        public string? ActiveAccountId => _activeAccountId;

        /// <summary>
        /// 账号切换事件
        /// </summary>
        public event EventHandler<string>? AccountSwitched;

        /// <summary>
        /// 账号关闭事件
        /// </summary>
        public event EventHandler<string>? AccountClosed;

        public MultiAccountBrowserPanel()
        {
            InitializeComponent();
            UpdateAccountCount();
        }

        /// <summary>
        /// 添加并启动账号
        /// </summary>
        public bool AddAccount(string accountId, string? startUrl = null)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(MultiAccountBrowserPanel));

            if (string.IsNullOrWhiteSpace(accountId))
                return false;

            // 已存在则切换
            if (_tabs.ContainsKey(accountId))
            {
                SwitchTo(accountId);
                return true;
            }

            try
            {
                // 创建浏览器实例
                var instance = BrowserInstanceManager.Instance.GetOrCreate(
                    accountId, 
                    startUrl ?? DefaultHomeUrl);

                // 创建标签页控件
                var tabControl = new BrowserTabControl
                {
                    HomeUrl = DefaultHomeUrl
                };
                tabControl.BindInstance(instance);

                _tabs[accountId] = tabControl;

                // 创建标签按钮
                CreateTabButton(accountId);

                // 切换到新账号
                SwitchTo(accountId);

                UpdateAccountCount();
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"启动账号失败: {ex.Message}", "错误",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

        /// <summary>
        /// 创建标签按钮
        /// </summary>
        private void CreateTabButton(string accountId)
        {
            var button = new Button
            {
                Tag = accountId,
                Padding = new Thickness(10, 5, 5, 5),
                Margin = new Thickness(0, 0, 2, 0),
                Background = Brushes.White,
                BorderThickness = new Thickness(0)
            };

            // 按钮内容：账号名 + 关闭按钮
            var panel = new StackPanel { Orientation = Orientation.Horizontal };
            panel.Children.Add(new TextBlock 
            { 
                Text = accountId, 
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 8, 0)
            });

            var closeBtn = new Button
            {
                Content = "×",
                Width = 18,
                Height = 18,
                Padding = new Thickness(0),
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Foreground = Brushes.Gray,
                FontSize = 14,
                VerticalContentAlignment = VerticalAlignment.Center,
                Tag = accountId
            };
            closeBtn.Click += CloseTabButton_Click;
            panel.Children.Add(closeBtn);

            button.Content = panel;
            button.Click += TabButton_Click;

            _tabButtons[accountId] = button;
            tabsPanel.Children.Add(button);
        }

        /// <summary>
        /// 切换到指定账号
        /// </summary>
        public void SwitchTo(string accountId)
        {
            if (!_tabs.TryGetValue(accountId, out var tabControl))
                return;

            // 隐藏当前
            if (_activeAccountId != null && _tabs.TryGetValue(_activeAccountId, out var currentTab))
            {
                contentArea.Children.Remove(currentTab);
            }

            // 显示新的
            _activeAccountId = accountId;
            emptyHint.Visibility = Visibility.Collapsed;
            
            if (!contentArea.Children.Contains(tabControl))
            {
                contentArea.Children.Add(tabControl);
            }

            // 更新标签按钮样式
            UpdateTabButtonStyles();

            AccountSwitched?.Invoke(this, accountId);
        }

        /// <summary>
        /// 关闭账号
        /// </summary>
        public void CloseAccount(string accountId)
        {
            if (!_tabs.TryGetValue(accountId, out var tabControl))
                return;

            // 从 UI 移除
            contentArea.Children.Remove(tabControl);
            tabControl.Dispose();
            _tabs.Remove(accountId);

            // 移除标签按钮
            if (_tabButtons.TryGetValue(accountId, out var button))
            {
                tabsPanel.Children.Remove(button);
                _tabButtons.Remove(accountId);
            }

            // 关闭浏览器实例
            BrowserInstanceManager.Instance.Close(accountId);

            // 切换到其他账号
            if (_activeAccountId == accountId)
            {
                _activeAccountId = null;
                
                if (_tabs.Count > 0)
                {
                    var nextAccount = _tabs.Keys.GetEnumerator();
                    if (nextAccount.MoveNext())
                        SwitchTo(nextAccount.Current);
                }
                else
                {
                    emptyHint.Visibility = Visibility.Visible;
                }
            }

            UpdateAccountCount();
            AccountClosed?.Invoke(this, accountId);
        }

        private void TabButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string accountId)
            {
                SwitchTo(accountId);
            }
        }

        private void CloseTabButton_Click(object sender, RoutedEventArgs e)
        {
            e.Handled = true; // 阻止冒泡
            
            if (sender is Button btn && btn.Tag is string accountId)
            {
                var result = MessageBox.Show(
                    $"确定要关闭账号 [{accountId}] 吗？",
                    "确认关闭",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    CloseAccount(accountId);
                }
            }
        }

        private void BtnAddAccount_Click(object sender, RoutedEventArgs e)
        {
            // 检查数量限制
            var max = BrowserInstanceManager.Instance.MaxAllowedAccounts;
            if (_tabs.Count >= max)
            {
                MessageBox.Show(
                    $"已达到最大账号数量限制 ({max})，请先关闭其他账号或升级授权",
                    "提示",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            // 生成账号 ID（实际使用时应该从账号管理模块获取）
            var accountId = $"account_{DateTime.Now:HHmmss}";
            AddAccount(accountId);
        }

        private void UpdateTabButtonStyles()
        {
            foreach (var kvp in _tabButtons)
            {
                var isActive = kvp.Key == _activeAccountId;
                kvp.Value.Background = isActive 
                    ? new SolidColorBrush(Color.FromRgb(0, 122, 204)) 
                    : Brushes.White;
                
                if (kvp.Value.Content is StackPanel panel && panel.Children[0] is TextBlock txt)
                {
                    txt.Foreground = isActive ? Brushes.White : Brushes.Black;
                }
            }
        }

        private void UpdateAccountCount()
        {
            var max = LicenseGuard.Instance.CurrentLicense?.MaxAccounts ?? 1;
            txtAccountCount.Text = $"{_tabs.Count}/{max}";
        }

        /// <summary>
        /// 关闭所有账号
        /// </summary>
        public void CloseAll()
        {
            foreach (var accountId in new List<string>(_tabs.Keys))
            {
                CloseAccount(accountId);
            }
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
