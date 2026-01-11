using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using CefSharp;
using RecruitAutomation.Browser;
using RecruitAutomation.Browser.JobFetcher;
using RecruitAutomation.Core.Models;
using RecruitAutomation.Core.Services;

// 使用别名避免类型冲突
using AvailableAccountModel = RecruitAutomation.Core.Models.AvailableAccount;

namespace RecruitAutomation.App.Controls
{
    public partial class JobManagementPanel : UserControl
    {
        [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr hWnd);
        [DllImport("user32.dll")] private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
        private const int SW_RESTORE = 9;

        private readonly JobFetchService _fetchService;
        private readonly JobManagementService _jobService;
        private readonly JobRepository _jobRepository;
        private readonly ObservableCollection<JobViewModel> _jobs;
        private CancellationTokenSource? _cts;
        private int _logLineCount = 0;

        private static readonly string LogDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "RecruitAutomation", "logs");
        private static readonly string ScreenshotDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "RecruitAutomation", "screenshots");

        public JobManagementPanel()
        {
            InitializeComponent();
            _jobRepository = new JobRepository();
            _jobService = new JobManagementService(_jobRepository);
            _fetchService = new JobFetchService();
            _jobs = new ObservableCollection<JobViewModel>();
            dgJobs.ItemsSource = _jobs;
            _fetchService.OnProgress += FetchService_OnProgress;
            Loaded += JobManagementPanel_Loaded;
            EnsureDirectories();
            AppendDebugLog("调试模式已启用 - Build: DEBUG-JOB-READER-v2");
        }

        private void EnsureDirectories()
        {
            try { if (!Directory.Exists(LogDir)) Directory.CreateDirectory(LogDir); } catch { }
            try { if (!Directory.Exists(ScreenshotDir)) Directory.CreateDirectory(ScreenshotDir); } catch { }
        }

        private void AppendDebugLog(string message)
        {
            Dispatcher.Invoke(() =>
            {
                _logLineCount++;
                var ts = DateTime.Now.ToString("HH:mm:ss");
                var line = $"[{ts}] {message}";
                txtDebugLog.Text = _logLineCount == 1 ? line : txtDebugLog.Text + "\n" + line;
                var lines = txtDebugLog.Text.Split('\n');
                if (lines.Length > 50) txtDebugLog.Text = string.Join("\n", lines.Skip(lines.Length - 50));
                debugLogScroller.ScrollToEnd();
            });
            try { File.AppendAllText(Path.Combine(LogDir, $"debug_{DateTime.Now:yyyyMMdd}.log"), $"[{DateTime.Now:HH:mm:ss}] {message}\n"); } catch { }
        }

        private void UpdateProgress(string msg, int pct)
        {
            Dispatcher.Invoke(() => { txtStatus.Text = msg; progressBar.Value = pct; txtDebugInfo.Text = $"调试信息：{msg}"; });
            AppendDebugLog(msg);
        }

        private async void JobManagementPanel_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                await RefreshAccountsAsync();
                await LoadJobsAsync();
                AppendDebugLog("界面加载完成");
            }
            catch (Exception ex)
            {
                AppendDebugLog($"界面加载异常: {ex.Message}");
                // 不抛出，让界面继续显示
            }
        }

        #region Debug Buttons

        private void BtnDebugOpenBrowser_Click(object sender, RoutedEventArgs e)
        {
            var account = cmbAccounts.SelectedItem as AvailableAccountModel;
            if (account == null) { MessageBox.Show("请先选择账号！", "提示"); return; }
            var inst = BrowserInstanceManager.Instance.Get(account.AccountId);
            if (inst?.Browser == null) { MessageBox.Show("浏览器实例不存在！", "错误"); return; }
            BringBrowserToFront(inst);
            AppendDebugLog($"已打开浏览器，URL: {inst.CurrentUrl}");
            MessageBox.Show($"浏览器已弹出！\nURL: {inst.CurrentUrl}", "成功");
        }

        private async void BtnDebugGoToJobPage_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var account = cmbAccounts.SelectedItem as AvailableAccountModel;
                if (account == null) { MessageBox.Show("请先选择账号！", "提示"); return; }
                var inst = BrowserInstanceManager.Instance.Get(account.AccountId);
                if (inst?.Browser == null) { MessageBox.Show("浏览器实例不存在！", "错误"); return; }
                BringBrowserToFront(inst);
                AppendDebugLog("导航到职位管理页...");
                inst.Navigate("https://www.zhipin.com/web/chat/job/list");
                await Task.Delay(2500);
                AppendDebugLog($"导航完成，URL: {inst.CurrentUrl}");
                MessageBox.Show($"已导航到职位管理页！\nURL: {inst.CurrentUrl}", "成功");
            }
            catch (Exception ex)
            {
                AppendDebugLog($"导航异常: {ex.Message}");
            }
        }

        private async void BtnDebugReadJobs_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var account = cmbAccounts.SelectedItem as AvailableAccountModel;
                if (account == null) { MessageBox.Show("请先选择账号！", "提示"); return; }
                var inst = BrowserInstanceManager.Instance.Get(account.AccountId);
                if (inst?.Browser == null) { MessageBox.Show("浏览器实例不存在！", "错误"); return; }

                statusBar.Visibility = Visibility.Visible;
                btnFetchJobs.IsEnabled = false;
                btnCancel.Visibility = Visibility.Visible;

                AppendDebugLog("========== 开始读取职位 ==========");
                BringBrowserToFront(inst);

                try
                {
                    UpdateProgress("步骤1/5: 检查页面...", 10);
                    AppendDebugLog($"当前URL: {inst.CurrentUrl}");

                    if (!inst.CurrentUrl.Contains("zhipin.com/web/chat/job"))
                    {
                        UpdateProgress("步骤2/5: 导航到职位页...", 20);
                        inst.Navigate("https://www.zhipin.com/web/chat/job/list");
                        await Task.Delay(3000);
                    }

                    UpdateProgress("步骤3/5: 等待加载...", 40);
                    await Task.Delay(2000);

                    UpdateProgress("步骤4/5: 分析页面...", 55);
                    var jobs = await ExtractJobsAsync(inst, account.AccountId);

                    UpdateProgress($"步骤5/5: 找到 {jobs.Count} 个职位", 90);

                    if (jobs.Count > 0)
                    {
                        await _jobService.SaveFetchedJobsAsync(account.AccountId, jobs);
                        await LoadJobsAsync();
                        UpdateProgress($"完成! 读取 {jobs.Count} 个职位", 100);
                        MessageBox.Show($"成功读取 {jobs.Count} 个职位！", "成功");
                    }
                    else
                    {
                        UpdateProgress("未读取到职位", 100);
                        MessageBox.Show("未读取到职位！\n\n请检查：\n1. 是否已登录\n2. 是否有开放中的职位", "提示");
                    }
                }
                catch (Exception ex)
                {
                    AppendDebugLog($"异常: {ex.Message}");
                    MessageBox.Show($"读取失败: {ex.Message}", "错误");
                }
                finally
                {
                    btnFetchJobs.IsEnabled = true;
                    btnCancel.Visibility = Visibility.Collapsed;
                    AppendDebugLog("========== 读取结束 ==========");
                }
            }
            catch (Exception ex)
            {
                AppendDebugLog($"BtnDebugReadJobs_Click 异常: {ex.Message}");
            }
        }

        private async void BtnDebugScreenshot_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var account = cmbAccounts.SelectedItem as AvailableAccountModel;
                if (account == null) { MessageBox.Show("请先选择账号！", "提示"); return; }
                var inst = BrowserInstanceManager.Instance.Get(account.AccountId);
                if (inst?.Browser == null) { MessageBox.Show("浏览器实例不存在！", "错误"); return; }

                try
                {
                    var devTools = inst.Browser.GetDevToolsClient();
                    var result = await devTools.Page.CaptureScreenshotAsync();
                    if (result?.Data != null)
                    {
                        var fn = $"debug_{DateTime.Now:yyyyMMdd_HHmmss}.png";
                        var path = Path.Combine(ScreenshotDir, fn);
                        File.WriteAllBytes(path, result.Data);
                        AppendDebugLog($"截图已保存: {path}");
                        Process.Start("explorer.exe", $"/select,\"{path}\"");
                        MessageBox.Show($"截图已保存！\n{path}", "成功");
                    }
                }
                catch (Exception ex)
                {
                    AppendDebugLog($"截图失败: {ex.Message}");
                    MessageBox.Show($"截图失败: {ex.Message}", "错误");
                }
            }
            catch (Exception ex)
            {
                AppendDebugLog($"BtnDebugScreenshot_Click 异常: {ex.Message}");
            }
        }

        private void BringBrowserToFront(AccountBrowserInstance inst)
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
                    }
                }
                catch { }
            });
        }

        #endregion

        #region Job Extraction

        private async Task<List<JobPosition>> ExtractJobsAsync(AccountBrowserInstance inst, string accountId)
        {
            var jobs = new List<JobPosition>();
            var browser = inst.Browser;

            AppendDebugLog("开始提取职位数据...");

            // 多种选择器尝试
            var extractScript = @"
(function() {
    var jobs = [];
    
    // 方法1: Boss直聘职位列表选择器
    var jobItems = document.querySelectorAll('.job-list-item, .job-item, [class*=""job-card""], [class*=""position-item""]');
    if (jobItems.length === 0) {
        jobItems = document.querySelectorAll('li[data-job], div[data-job], .job-box, .position-box');
    }
    if (jobItems.length === 0) {
        // 尝试表格行
        jobItems = document.querySelectorAll('table tbody tr, .list-item, .item-row');
    }
    
    for (var i = 0; i < jobItems.length; i++) {
        var item = jobItems[i];
        var job = {};
        
        // 提取职位ID
        job.id = item.getAttribute('data-job-id') || item.getAttribute('data-id') || 
                 item.getAttribute('data-encryptjobid') || ('job_' + i);
        
        // 提取职位名称
        var titleEl = item.querySelector('.job-title, .position-name, .job-name, h3, h4, .title, [class*=""title""]');
        job.title = titleEl ? titleEl.innerText.trim() : '';
        
        // 提取薪资
        var salaryEl = item.querySelector('.salary, .job-salary, .money, [class*=""salary""]');
        job.salary = salaryEl ? salaryEl.innerText.trim() : '';
        
        // 提取地点
        var locationEl = item.querySelector('.location, .job-area, .city, [class*=""location""], [class*=""area""]');
        job.location = locationEl ? locationEl.innerText.trim() : '';
        
        // 提取经验要求
        var expEl = item.querySelector('.experience, .job-exp, [class*=""exp""]');
        job.experience = expEl ? expEl.innerText.trim() : '';
        
        // 提取学历要求
        var eduEl = item.querySelector('.education, .job-edu, .degree, [class*=""edu""]');
        job.education = eduEl ? eduEl.innerText.trim() : '';
        
        // 提取状态
        var statusEl = item.querySelector('.status, .job-status, [class*=""status""]');
        job.status = statusEl ? statusEl.innerText.trim() : '开放中';
        
        // 提取链接
        var linkEl = item.querySelector('a[href*=""job""], a[href*=""position""]');
        job.url = linkEl ? linkEl.href : '';
        
        if (job.title) {
            jobs.push(job);
        }
    }
    
    // 如果没找到，尝试从页面文本提取
    if (jobs.length === 0) {
        var allText = document.body.innerText;
        return { jobs: [], pageText: allText.substring(0, 2000), html: document.body.innerHTML.substring(0, 3000) };
    }
    
    return { jobs: jobs, count: jobs.length };
})();
";

            try
            {
                var result = await browser.EvaluateScriptAsync(extractScript);
                AppendDebugLog($"脚本执行结果: Success={result.Success}");

                if (result.Success && result.Result != null)
                {
                    var dict = result.Result as IDictionary<string, object>;
                    if (dict != null)
                    {
                        if (dict.ContainsKey("jobs") && dict["jobs"] is IList<object> jobList)
                        {
                            AppendDebugLog($"找到 {jobList.Count} 个职位元素");

                            foreach (var jobObj in jobList)
                            {
                                if (jobObj is IDictionary<string, object> jobDict)
                                {
                                    var title = jobDict.ContainsKey("title") ? jobDict["title"]?.ToString() ?? "" : "";
                                    if (string.IsNullOrWhiteSpace(title)) continue;

                                    var platformJobId = jobDict.ContainsKey("id") ? jobDict["id"]?.ToString() ?? Guid.NewGuid().ToString("N") : Guid.NewGuid().ToString("N");
                                    var job = new JobPosition
                                    {
                                        Id = $"Boss_{accountId}_{platformJobId}",
                                        Platform = RecruitPlatform.Boss,
                                        AccountId = accountId,
                                        PlatformJobId = platformJobId,
                                        Title = title,
                                        SalaryText = jobDict.ContainsKey("salary") ? jobDict["salary"]?.ToString() ?? "" : "",
                                        Location = jobDict.ContainsKey("location") ? jobDict["location"]?.ToString() ?? "" : "",
                                        ExperienceRequired = jobDict.ContainsKey("experience") ? jobDict["experience"]?.ToString() ?? "" : "",
                                        EducationRequired = jobDict.ContainsKey("education") ? jobDict["education"]?.ToString() ?? "" : "",
                                        PageUrl = jobDict.ContainsKey("url") ? jobDict["url"]?.ToString() ?? "" : "",
                                        Status = JobStatus.Open,
                                        FetchedAt = DateTime.UtcNow
                                    };

                                    // 解析薪资
                                    ParseSalary(job);
                                    jobs.Add(job);
                                    AppendDebugLog($"  职位: {job.Title} | {job.SalaryText} | {job.Location}");
                                }
                            }
                        }

                        // 如果没有找到职位，记录页面信息用于调试
                        if (jobs.Count == 0)
                        {
                            if (dict.ContainsKey("pageText"))
                            {
                                var pageText = dict["pageText"]?.ToString() ?? "";
                                AppendDebugLog($"页面文本(前500字): {pageText.Substring(0, Math.Min(500, pageText.Length))}");
                            }
                            if (dict.ContainsKey("html"))
                            {
                                var html = dict["html"]?.ToString() ?? "";
                                AppendDebugLog($"HTML片段(前500字): {html.Substring(0, Math.Min(500, html.Length))}");
                            }
                        }
                    }
                }
                else
                {
                    AppendDebugLog($"脚本执行失败: {result.Message}");
                }
            }
            catch (Exception ex)
            {
                AppendDebugLog($"提取异常: {ex.Message}");
            }

            AppendDebugLog($"提取完成，共 {jobs.Count} 个职位");
            return jobs;
        }

        private void ParseSalary(JobPosition job)
        {
            if (string.IsNullOrWhiteSpace(job.SalaryText)) return;

            try
            {
                var match = Regex.Match(job.SalaryText, @"(\d+)-(\d+)");
                if (match.Success)
                {
                    job.SalaryMin = int.Parse(match.Groups[1].Value);
                    job.SalaryMax = int.Parse(match.Groups[2].Value);
                }

                var monthMatch = Regex.Match(job.SalaryText, @"(\d+)薪");
                if (monthMatch.Success)
                {
                    job.SalaryMonths = int.Parse(monthMatch.Groups[1].Value);
                }
            }
            catch { }
        }

        #endregion

        #region Account Management

        private async Task RefreshAccountsAsync()
        {
            try
            {
                var accounts = new List<AvailableAccountModel>();
                foreach (var accountId in BrowserInstanceManager.Instance.RunningAccountIds)
                {
                    var inst = BrowserInstanceManager.Instance.Get(accountId);
                    if (inst != null)
                    {
                        accounts.Add(new AvailableAccountModel
                        {
                            AccountId = accountId,
                            Platform = RecruitPlatform.Boss,
                            IsStarted = true,
                            LoginStatus = AccountLoginStatus.LoggedIn,
                            DisplayName = accountId
                        });
                    }
                }

                cmbAccounts.ItemsSource = accounts;
                if (accounts.Count > 0 && cmbAccounts.SelectedIndex < 0)
                {
                    cmbAccounts.SelectedIndex = 0;
                }

                UpdateFetchButtonState();
                AppendDebugLog($"刷新账号列表，共 {accounts.Count} 个账号");
            }
            catch (Exception ex)
            {
                AppendDebugLog($"刷新账号失败: {ex.Message}");
            }
        }

        private void UpdateFetchButtonState()
        {
            var account = cmbAccounts.SelectedItem as AvailableAccountModel;
            btnFetchJobs.IsEnabled = account?.IsAvailable == true;
        }

        private async void BtnRefreshAccounts_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                await RefreshAccountsAsync();
            }
            catch (Exception ex)
            {
                AppendDebugLog($"刷新账号异常: {ex.Message}");
            }
        }

        private async void CmbAccounts_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                UpdateFetchButtonState();
                await LoadJobsAsync();
            }
            catch (Exception ex)
            {
                AppendDebugLog($"账号切换异常: {ex.Message}");
            }
        }

        #endregion

        #region Job List Management

        private async Task LoadJobsAsync()
        {
            try
            {
                _jobs.Clear();
                var account = cmbAccounts.SelectedItem as AvailableAccountModel;

                List<JobPosition> jobList;
                if (account != null)
                {
                    jobList = await _jobService.GetJobsByAccountAsync(account.AccountId);
                }
                else
                {
                    jobList = await _jobService.GetAllAsync();
                }

                foreach (var job in jobList)
                {
                    _jobs.Add(new JobViewModel(job));
                }

                UpdateStats();
                AppendDebugLog($"加载 {_jobs.Count} 个职位");
            }
            catch (Exception ex)
            {
                AppendDebugLog($"加载职位失败: {ex.Message}");
            }
        }

        private void UpdateStats()
        {
            var total = _jobs.Count;
            var enabled = _jobs.Count(j => j.AutomationEnabled);
            var selected = _jobs.Count(j => j.IsSelected);

            txtStats.Text = $"共 {total} 个岗位，{enabled} 个已启用自动化";
            txtSelectedStats.Text = $"已选择 {selected} 个";
        }

        private void ChkSelectAll_Click(object sender, RoutedEventArgs e)
        {
            var isChecked = chkSelectAll.IsChecked == true;
            foreach (var job in _jobs)
            {
                job.IsSelected = isChecked;
            }
            UpdateStats();
        }

        private async void AutomationToggle_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (sender is ToggleButton toggle && toggle.DataContext is JobViewModel vm)
                {
                    if (vm.AutomationEnabled)
                    {
                        await _jobService.EnableAutomationAsync(vm.Id);
                    }
                    else
                    {
                        await _jobService.DisableAutomationAsync(vm.Id);
                    }
                    UpdateStats();
                }
            }
            catch (Exception ex)
            {
                AppendDebugLog($"切换自动化状态异常: {ex.Message}");
            }
        }

        private async void BtnBatchEnable_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var selected = _jobs.Where(j => j.IsSelected).ToList();
                if (selected.Count == 0)
                {
                    MessageBox.Show("请先选择要启用的岗位！", "提示");
                    return;
                }

                await _jobService.BatchEnableAutomationAsync(selected.Select(j => j.Id));
                foreach (var job in selected)
                {
                    job.AutomationEnabled = true;
                }
                UpdateStats();
                MessageBox.Show($"已启用 {selected.Count} 个岗位的自动化！", "成功");
            }
            catch (Exception ex)
            {
                AppendDebugLog($"批量启用异常: {ex.Message}");
            }
        }

        private async void BtnBatchDisable_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var selected = _jobs.Where(j => j.IsSelected).ToList();
                if (selected.Count == 0)
                {
                    MessageBox.Show("请先选择要禁用的岗位！", "提示");
                    return;
                }

                await _jobService.BatchDisableAutomationAsync(selected.Select(j => j.Id));
                foreach (var job in selected)
                {
                    job.AutomationEnabled = false;
                }
                UpdateStats();
                MessageBox.Show($"已禁用 {selected.Count} 个岗位的自动化！", "成功");
            }
            catch (Exception ex)
            {
                AppendDebugLog($"批量禁用异常: {ex.Message}");
            }
        }

        private async void BtnDeleteSelected_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var selected = _jobs.Where(j => j.IsSelected).ToList();
                if (selected.Count == 0)
                {
                    MessageBox.Show("请先选择要删除的岗位！", "提示");
                    return;
                }

                var result = MessageBox.Show($"确定要删除选中的 {selected.Count} 个岗位吗？", "确认删除",
                    MessageBoxButton.YesNo, MessageBoxImage.Warning);

                if (result == MessageBoxResult.Yes)
                {
                    foreach (var job in selected)
                    {
                        await _jobService.DeleteAsync(job.Id);
                        _jobs.Remove(job);
                    }
                    UpdateStats();
                    MessageBox.Show($"已删除 {selected.Count} 个岗位！", "成功");
                }
            }
            catch (Exception ex)
            {
                AppendDebugLog($"删除岗位异常: {ex.Message}");
            }
        }

        #endregion

        #region Fetch Jobs (Main Button)

        private async void BtnFetchJobs_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var account = cmbAccounts.SelectedItem as AvailableAccountModel;
                if (account == null)
                {
                    MessageBox.Show("请先选择账号！", "提示");
                    return;
                }

                // 使用调试读取方法
                BtnDebugReadJobs_Click(sender, e);
            }
            catch (Exception ex)
            {
                AppendDebugLog($"读取岗位异常: {ex.Message}");
            }
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            _cts?.Cancel();
            btnCancel.Visibility = Visibility.Collapsed;
            btnFetchJobs.IsEnabled = true;
            UpdateProgress("已取消", 0);
        }

        private void FetchService_OnProgress(object? sender, JobFetchProgressEventArgs e)
        {
            AppendDebugLog(e.Message);
        }

        #endregion
    }

    #region JobViewModel

    public class JobViewModel : INotifyPropertyChanged
    {
        private bool _isSelected;
        private bool _automationEnabled;

        public string Id { get; set; }
        public string Platform { get; set; }
        public string Title { get; set; }
        public string SalaryText { get; set; }
        public string Location { get; set; }
        public string ExperienceRequired { get; set; }
        public string EducationRequired { get; set; }
        public string FetchedAtText { get; set; }

        public bool IsSelected
        {
            get => _isSelected;
            set { _isSelected = value; OnPropertyChanged(); }
        }

        public bool AutomationEnabled
        {
            get => _automationEnabled;
            set { _automationEnabled = value; OnPropertyChanged(); }
        }

        public JobViewModel(JobPosition job)
        {
            Id = job.Id;
            Platform = job.Platform.ToString();
            Title = job.Title;
            SalaryText = job.SalaryText;
            Location = job.Location;
            ExperienceRequired = job.ExperienceRequired;
            EducationRequired = job.EducationRequired;
            FetchedAtText = job.FetchedAt.ToLocalTime().ToString("MM-dd HH:mm");
            AutomationEnabled = job.AutomationEnabled;
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }

    #endregion
}
