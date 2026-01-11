using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using RecruitAutomation.Browser;
using RecruitAutomation.Core.Models;

namespace RecruitAutomation.App.V2.Pages
{
    public partial class BrowserManagementPage : UserControl
    {
        private readonly ObservableCollection<BrowserAccountItem> _accounts;

        public BrowserManagementPage()
        {
            InitializeComponent();
            _accounts = new ObservableCollection<BrowserAccountItem>();
            lstAccounts.ItemsSource = _accounts;
            Loaded += OnLoaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            RefreshAccountList();
        }

        private void BtnAddAccount_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new AddAccountDialog();
            if (dialog.ShowDialog() == true)
            {
                var accountId = dialog.AccountId;
                var platform = dialog.Platform;
                
                try
                {
                    var startUrl = platform == "Boss" 
                        ? "https://www.zhipin.com/web/user/?ka=header-login"
                        : "https://www.zhaopin.com/";
                    
                    var instance = BrowserInstanceManager.Instance.GetOrCreate(accountId, startUrl);
                    
                    // 创建浏览器窗口
                    var browserWindow = new BrowserWindow(instance, accountId);
                    browserWindow.Show();
                    
                    RefreshAccountList();
                    MessageBox.Show($"账号 {accountId} 已添加！\n\n请在弹出的浏览器窗口中完成登录。", 
                        "成功", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"添加账号失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void BtnRefresh_Click(object sender, RoutedEventArgs e)
        {
            RefreshAccountList();
        }

        private void RefreshAccountList()
        {
            _accounts.Clear();
            var manager = BrowserInstanceManager.Instance;
            
            foreach (var accountId in manager.RunningAccountIds)
            {
                var instance = manager.Get(accountId);
                if (instance != null)
                {
                    _accounts.Add(new BrowserAccountItem
                    {
                        AccountId = accountId,
                        Platform = DetectPlatform(instance.CurrentUrl),
                        IsRunning = instance.IsInitialized,
                        CurrentUrl = instance.CurrentUrl
                    });
                }
            }
        }

        private string DetectPlatform(string url)
        {
            if (string.IsNullOrEmpty(url)) return "未知";
            url = url.ToLower();
            if (url.Contains("zhipin.com") || url.Contains("boss")) return "Boss直聘";
            if (url.Contains("zhaopin.com")) return "智联招聘";
            if (url.Contains("51job.com")) return "前程无忧";
            if (url.Contains("liepin.com")) return "猎聘";
            return "未知";
        }
    }

    public class BrowserAccountItem : INotifyPropertyChanged
    {
        public string AccountId { get; set; } = "";
        public string Platform { get; set; } = "";
        public bool IsRunning { get; set; }
        public string CurrentUrl { get; set; } = "";

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? n = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
    }

    /// <summary>
    /// 添加账号对话框
    /// </summary>
    public class AddAccountDialog : Window
    {
        public string AccountId { get; private set; } = "";
        public string Platform { get; private set; } = "Boss";

        private TextBox _txtAccountId;
        private ComboBox _cmbPlatform;

        public AddAccountDialog()
        {
            Title = "添加账号";
            Width = 350;
            Height = 200;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ResizeMode = ResizeMode.NoResize;

            var grid = new Grid { Margin = new Thickness(20) };
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // 账号ID
            var sp1 = new StackPanel { Margin = new Thickness(0, 0, 0, 10) };
            sp1.Children.Add(new TextBlock { Text = "账号ID（自定义名称）：", Margin = new Thickness(0, 0, 0, 5) });
            _txtAccountId = new TextBox { Height = 28 };
            sp1.Children.Add(_txtAccountId);
            Grid.SetRow(sp1, 0);
            grid.Children.Add(sp1);

            // 平台选择
            var sp2 = new StackPanel { Margin = new Thickness(0, 0, 0, 10) };
            sp2.Children.Add(new TextBlock { Text = "招聘平台：", Margin = new Thickness(0, 0, 0, 5) });
            _cmbPlatform = new ComboBox { Height = 28 };
            _cmbPlatform.Items.Add("Boss直聘");
            _cmbPlatform.Items.Add("智联招聘");
            _cmbPlatform.SelectedIndex = 0;
            sp2.Children.Add(_cmbPlatform);
            Grid.SetRow(sp2, 1);
            grid.Children.Add(sp2);

            // 按钮
            var btnPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            var btnOk = new Button { Content = "确定", Width = 80, Height = 28, Margin = new Thickness(0, 0, 10, 0) };
            btnOk.Click += (s, e) =>
            {
                if (string.IsNullOrWhiteSpace(_txtAccountId.Text))
                {
                    MessageBox.Show("请输入账号ID", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                AccountId = _txtAccountId.Text.Trim();
                Platform = _cmbPlatform.SelectedItem?.ToString()?.Contains("Boss") == true ? "Boss" : "Zhilian";
                DialogResult = true;
            };
            btnPanel.Children.Add(btnOk);

            var btnCancel = new Button { Content = "取消", Width = 80, Height = 28 };
            btnCancel.Click += (s, e) => DialogResult = false;
            btnPanel.Children.Add(btnCancel);

            Grid.SetRow(btnPanel, 3);
            grid.Children.Add(btnPanel);

            Content = grid;
        }
    }

    /// <summary>
    /// 浏览器窗口
    /// </summary>
    public class BrowserWindow : Window
    {
        private readonly AccountBrowserInstance _instance;

        public BrowserWindow(AccountBrowserInstance instance, string title)
        {
            _instance = instance;
            Title = $"浏览器 - {title}";
            Width = 1200;
            Height = 800;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;

            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            // 工具栏
            var toolbar = new StackPanel 
            { 
                Orientation = Orientation.Horizontal, 
                Background = System.Windows.Media.Brushes.WhiteSmoke,
                Height = 40
            };
            
            var btnBack = new Button { Content = "◀", Width = 40, Margin = new Thickness(5) };
            btnBack.Click += (s, e) => _instance.GoBack();
            toolbar.Children.Add(btnBack);

            var btnForward = new Button { Content = "▶", Width = 40, Margin = new Thickness(5) };
            btnForward.Click += (s, e) => _instance.GoForward();
            toolbar.Children.Add(btnForward);

            var btnRefresh = new Button { Content = "🔄", Width = 40, Margin = new Thickness(5) };
            btnRefresh.Click += (s, e) => _instance.Refresh();
            toolbar.Children.Add(btnRefresh);

            var txtUrl = new TextBox 
            { 
                Width = 600, 
                Height = 28, 
                Margin = new Thickness(10, 5, 10, 5),
                VerticalContentAlignment = VerticalAlignment.Center
            };
            txtUrl.Text = _instance.CurrentUrl;
            _instance.UrlChanged += (s, url) => Dispatcher.Invoke(() => txtUrl.Text = url);
            txtUrl.KeyDown += (s, e) =>
            {
                if (e.Key == System.Windows.Input.Key.Enter)
                    _instance.Navigate(txtUrl.Text);
            };
            toolbar.Children.Add(txtUrl);

            Grid.SetRow(toolbar, 0);
            grid.Children.Add(toolbar);

            // 浏览器容器
            var browserContainer = new Grid();
            Grid.SetRow(browserContainer, 1);
            grid.Children.Add(browserContainer);

            _instance.AttachTo(browserContainer);

            Content = grid;

            Closed += (s, e) => _instance.Detach();
        }
    }
}
