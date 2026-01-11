using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using RecruitAutomation.Core.Models;
using RecruitAutomation.App.Helpers;
using RecruitAutomation.Browser.JobFetcher;

namespace RecruitAutomation.App
{
    public partial class MainWindow : Window
    {
        public ObservableCollection<AccountItem> Accounts { get; } = new();
        public ObservableCollection<LogItem> Logs { get; } = new();
        public ObservableCollection<JobPosition> DisplayJobs { get; } = new();
        
        private BrowserHelper? _browserHelper;
        private JobFetchService _jobFetchService = new();

        public MainWindow()
        {
            InitializeComponent();
            this.Loaded += OnWindowLoaded;
            
            // 绑定岗位列表
            if (dgJobs != null) dgJobs.ItemsSource = DisplayJobs;
            
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

        private async void OnWindowLoaded(object sender, RoutedEventArgs e)
        {
            AddLog("系统启动完成，AI 图像识别模块已就绪");
            // 初始化 AI 配置（实际应从设置读取）
            _jobFetchService.ConfigureAI("YOUR_API_KEY"); 
        }

        // “读取岗位”按钮点击事件
        private async void BtnFetchJobs_Click(object sender, RoutedEventArgs e)
        {
            var selectedAccount = lstAccounts?.SelectedItem as AccountItem;
            if (selectedAccount == null)
            {
                MessageBox.Show("请先在左侧选择一个已登录的账号", "提示");
                return;
            }

            AddLog($"开始为账号 {selectedAccount.Name} 读取岗位...");
            
            // 在后台线程执行识别任务，不阻塞 UI
            await Task.Run(async () => 
            {
                await _jobFetchService.FetchJobsFromAccountAsync(selectedAccount.Id, true);
            });
        }

        public void AddLog(string message)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                Logs.Insert(0, new LogItem { Message = message, Time = DateTime.Now.ToString("HH:mm:ss") });
                if (Logs.Count > 100) Logs.RemoveAt(100);
            }));
        }
    }
}
