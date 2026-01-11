using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using CefSharp;
using RecruitAutomation.Browser;
using RecruitAutomation.Browser.JobFetcher;
using RecruitAutomation.Core.Models;

namespace RecruitAutomation.App.V2.Pages
{
    public partial class JobManagementPage : UserControl
    {
        [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr hWnd);
        [DllImport("user32.dll")] private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
        private const int SW_RESTORE = 9;

        private readonly JobFetchService _fetchService;
        private readonly ObservableCollection<JobItem> _jobs;
        private int _logLines = 0;

        private static readonly string LogDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "RecruitAutomation", "logs");
        private static readonly string ScreenshotDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "RecruitAutomation", "screenshots");

        public JobManagementPage()
        {
            InitializeComponent();
            _fetchService = new JobFetchService();
            _jobs = new ObservableCollection<JobItem>();
            dgJobs.ItemsSource = _jobs;
            Loaded += OnLoaded;
            EnsureDirs();
            Log("V2 岗位管理页面初始化完成");
        }

        private void EnsureDirs()
        {
            try { if (!Directory.Exists(LogDir)) Directory.CreateDirectory(LogDir); } catch { }
            try { if (!Directory.Exists(ScreenshotDir)) Directory.CreateDirectory(ScreenshotDir); } catch { }
        }

        private void Log(string msg)
        {
            Dispatcher.Invoke(() =>
            {
                _logLines++;
                var ts = DateTime.Now.ToString("HH:mm:ss");
                var line = $"[{ts}] {msg}";
                txtConsole.Text = _logLines == 1 ? line : txtConsole.Text + "\n" + line;
                
                var lines = txtConsole.Text.Split('\n');
                if (lines.Length > 100)
                    txtConsole.Text = string.Join("\n", lines.Skip(lines.Length - 100));
                
                consoleScroller.ScrollToEnd();
            });
            
            try
            {
                File.AppendAllText(Path.Combine(LogDir, $"v2_{DateTime.Now:yyyyMMdd}.log"), 
                    $"[{DateTime.Now:HH:mm:ss}] {msg}\n");
            }
            catch { }
        }

        private void SetStatus(string status)
        {
            Dispatcher.Invoke(() => txtDebugStatus.Text = $"状态：{status}");
            Log(status);
        }

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            await RefreshAccounts();
            SetStatus("页面加载完成，等待操作");
        }

        private void BtnRefresh_Click(object sender, RoutedEventArgs e) => _ = RefreshAccounts();

        private async Task RefreshAccounts()
        {
            try
            {
                Log("刷新账号列表...");
                var accounts = await _fetchService.GetAvailableAccountsAsync();
                cmbAccounts.ItemsSource = accounts;
                if (accounts.Count > 0)
                {
                    var avail = accounts.FirstOrDefault(a => a.IsAvailable);
                    cmbAccounts.SelectedItem = avail ?? accounts[0];
                }
                Log($"找到 {accounts.Count} 个账号");
            }
            catch (Exception ex)
            {
                Log($"刷新账号失败: {ex.Message}");
            }
        }

        private AccountBrowserInstance? GetSelectedBrowser()
        {
            var account = cmbAccounts.SelectedItem as AvailableAccount;
            if (account == null)
            {
                MessageBox.Show("请先选择一个账号！", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return null;
            }
            
            var instance = BrowserInstanceManager.Instance.Get(account.AccountId);
            if (instance?.Browser == null)
            {
                MessageBox.Show($"账号 {account.AccountId} 的浏览器未启动！\n\n请先在【浏览器管理】中启动。", 
                    "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                Log($"错误: 浏览器实例不存在 - {account.AccountId}");
                return null;
            }
            
            return instance;
        }

        private void BringToFront(AccountBrowserInstance inst)
        {
            Dispatcher.Invoke(() =>
            {
                try
                {
                    var win = Window.GetWindow(inst.Browser);
                    if (win != null)
                    {
                        win.WindowState = WindowState.Normal;
                        win.Activate();
                        win.Topmost = true;
                        win.Topmost = false;
                        var h = new System.Windows.Interop.WindowInteropHelper(win).Handle;
                        ShowWindow(h, SW_RESTORE);
                        SetForegroundWindow(h);
                        Log("浏览器窗口已置顶");
                    }
                }
                catch (Exception ex)
                {
                    Log($"置顶失败: {ex.Message}");
                }
            });
        }

        private void BtnOpenBrowser_Click(object sender, RoutedEventArgs e)
        {
            var inst = GetSelectedBrowser();
            if (inst == null) return;

            Log($"打开浏览器窗口，当前URL: {inst.CurrentUrl}");
            BringToFront(inst);
            SetStatus($"浏览器已弹出 - URL: {inst.CurrentUrl}");
            MessageBox.Show($"浏览器窗口已弹出！\n\n当前URL:\n{inst.CurrentUrl}", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private async void BtnGoToJobPage_Click(object sender, RoutedEventArgs e)
        {
            var inst = GetSelectedBrowser();
            if (inst == null) return;

            Log("步骤: 导航到职位管理页面...");
            SetStatus("正在导航到职位管理页...");
            BringToFront(inst);

            const string url = "https://www.zhipin.com/web/chat/job/list";
            Log($"目标URL: {url}");
            inst.Navigate(url);
            
            await Task.Delay(2500);
            
            Log($"导航完成，当前URL: {inst.CurrentUrl}");
            SetStatus($"已进入职位管理页");
            MessageBox.Show($"已导航到职位管理页！\n\nURL: {inst.CurrentUrl}", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private async void BtnReadJobs_Click(object sender, RoutedEventArgs e)
        {
            var inst = GetSelectedBrowser();
            if (inst == null) return;

            var account = cmbAccounts.SelectedItem as AvailableAccount;
            
            Log("========== 开始读取岗位 (V2) ==========");
            SetStatus("开始读取岗位...");
            BringToFront(inst);

            // Step 1
            Log($"[Step 1/4] 当前URL: {inst.CurrentUrl}");
            
            // Step 2: 检查页面
            Log("[Step 2/4] 分析页面DOM...");
            try
            {
                var script = @"(function(){
                    return JSON.stringify({
                        url: location.href,
                        title: document.title,
                        hasOpen: document.body.innerText.includes('开放中'),
                        hasJobMgmt: document.body.innerText.includes('职位管理'),
                        bodyLen: (document.body.innerText||'').length
                    });
                })();";
                
                var r = await inst.Browser.EvaluateScriptAsync(script);
                if (r.Success && r.Result != null)
                {
                    Log($"DOM分析: {r.Result}");
                }
            }
            catch (Exception ex)
            {
                Log($"DOM分析异常: {ex.Message}");
            }

            // Step 3: 提取职位
            Log("[Step 3/4] 提取职位信息...");
            var jobs = await ExtractJobs(inst, account?.AccountId ?? "unknown");
            
            // Step 4: 显示结果
            Log($"[Step 4/4] 提取完成，找到 {jobs.Count} 个职位");
            SetStatus($"读取完成: {jobs.Count} 个职位");

            _jobs.Clear();
            foreach (var j in jobs)
            {
                _jobs.Add(j);
                Log($"  - {j.Title}");
            }
            
            txtStats.Text = $"共 {_jobs.Count} 个岗位";
            Log("========== 读取结束 ==========");

            if (jobs.Count > 0)
                MessageBox.Show($"成功读取 {jobs.Count} 个职位！", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
            else
                MessageBox.Show("未读取到职位！\n\n请检查：\n1. 是否已登录\n2. 是否有开放中的职位\n3. 查看调试控制台", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        private async Task<ObservableCollection<JobItem>> ExtractJobs(AccountBrowserInstance inst, string accountId)
        {
            var result = new ObservableCollection<JobItem>();
            
            var script = @"(function(){
                var jobs = [];
                var sels = ['.job-list .job-card','.job-item','[class*=""job""][class*=""card""]','.position-list .position-item'];
                for(var s=0; s<sels.length; s++){
                    var cards = document.querySelectorAll(sels[s]);
                    for(var i=0; i<cards.length; i++){
                        var c = cards[i];
                        var t = c.querySelector('.job-title, .position-name, h3, [class*=""title""]');
                        var sal = c.querySelector('.salary, [class*=""salary""]');
                        var loc = c.querySelector('.job-area, .location, [class*=""area""]');
                        if(t && t.innerText.trim()){
                            jobs.push({
                                title: t.innerText.trim(),
                                salary: sal ? sal.innerText.trim() : '',
                                location: loc ? loc.innerText.trim() : ''
                            });
                            c.style.border = '3px solid red';
                        }
                    }
                    if(jobs.length > 0) break;
                }
                return JSON.stringify({count: jobs.length, jobs: jobs});
            })();";

            try
            {
                var r = await inst.Browser.EvaluateScriptAsync(script);
                if (r.Success && r.Result != null)
                {
                    var json = r.Result.ToString() ?? "";
                    Log($"提取结果JSON: {json.Substring(0, Math.Min(150, json.Length))}...");
                    
                    var titles = Regex.Matches(json, @"""title"":""([^""]+)""");
                    var salaries = Regex.Matches(json, @"""salary"":""([^""]*)""");
                    var locations = Regex.Matches(json, @"""location"":""([^""]*)""");

                    for (int i = 0; i < titles.Count; i++)
                    {
                        result.Add(new JobItem
                        {
                            Platform = "Boss",
                            Title = titles[i].Groups[1].Value,
                            Salary = i < salaries.Count ? salaries[i].Groups[1].Value : "",
                            Location = i < locations.Count ? locations[i].Groups[1].Value : "",
                            Status = "开放中",
                            FetchTime = DateTime.Now.ToString("MM-dd HH:mm")
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                Log($"提取异常: {ex.Message}");
            }

            return result;
        }

        private async void BtnScreenshot_Click(object sender, RoutedEventArgs e)
        {
            var inst = GetSelectedBrowser();
            if (inst == null) return;

            Log("截图当前页面...");
            SetStatus("正在截图...");

            try
            {
                var devTools = inst.Browser.GetDevToolsClient();
                var r = await devTools.Page.CaptureScreenshotAsync();
                if (r?.Data != null)
                {
                    var fn = $"v2_{DateTime.Now:yyyyMMdd_HHmmss}.png";
                    var path = Path.Combine(ScreenshotDir, fn);
                    byte[] bytes = r.Data;
                    File.WriteAllBytes(path, bytes);
                    
                    Log($"截图已保存: {path}");
                    SetStatus($"截图已保存: {fn}");
                    Process.Start("explorer.exe", $"/select,\"{path}\"");
                    MessageBox.Show($"截图已保存！\n\n{path}", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                Log($"截图失败: {ex.Message}");
                MessageBox.Show($"截图失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    public class JobItem : INotifyPropertyChanged
    {
        private bool _isSelected;
        public bool IsSelected { get => _isSelected; set { _isSelected = value; OnPropertyChanged(); } }
        public string Platform { get; set; } = "";
        public string Title { get; set; } = "";
        public string Salary { get; set; } = "";
        public string Location { get; set; } = "";
        public string Status { get; set; } = "";
        public string FetchTime { get; set; } = "";

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? n = null) 
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
    }
}
