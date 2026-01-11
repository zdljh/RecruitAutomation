using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using RecruitAutomation.Core.Constants;
using RecruitAutomation.Core.Models;

namespace RecruitAutomation.Core.Services
{
    /// <summary>
    /// 账号活跃度服务
    /// 模拟人工操作保持账号活跃
    /// </summary>
    public class AccountActivityService
    {
        private readonly string _accountId;
        private readonly string _configFile;
        private readonly string _statsFile;
        private readonly Random _random = new();

        private AccountActivityConfig _config;
        private ActivityStatistics _todayStats;
        private CancellationTokenSource? _cts;
        private bool _isRunning;

        /// <summary>
        /// 刷新岗位事件
        /// </summary>
        public event Func<CancellationToken, Task>? OnRefreshJobs;

        /// <summary>
        /// 浏览简历事件
        /// </summary>
        public event Func<int, CancellationToken, Task>? OnBrowseResumes;

        /// <summary>
        /// 检查消息事件
        /// </summary>
        public event Func<CancellationToken, Task>? OnCheckMessages;

        /// <summary>
        /// 随机滚动事件
        /// </summary>
        public event Func<CancellationToken, Task>? OnRandomScroll;

        /// <summary>
        /// 随机点击事件
        /// </summary>
        public event Func<CancellationToken, Task>? OnRandomClick;

        /// <summary>
        /// 状态变更事件
        /// </summary>
        public event EventHandler<ActivityStatusEventArgs>? OnStatusChanged;

        public bool IsRunning => _isRunning;

        public AccountActivityService(string accountId)
        {
            _accountId = accountId;
            var dataDir = Path.Combine(AppConstants.DataRootPath, "accounts", accountId);
            _configFile = Path.Combine(dataDir, "activity_config.json");
            _statsFile = Path.Combine(dataDir, "activity_stats.json");

            if (!Directory.Exists(dataDir))
                Directory.CreateDirectory(dataDir);

            _config = new AccountActivityConfig();
            _todayStats = new ActivityStatistics();
        }

        /// <summary>
        /// 活跃状态事件参数
        /// </summary>
        public class ActivityStatusEventArgs : EventArgs
        {
            public string Status { get; set; } = string.Empty;
            public string Action { get; set; } = string.Empty;
            public DateTime Timestamp { get; set; } = DateTime.Now;
        }

        /// <summary>
        /// 加载配置
        /// </summary>
        public async Task LoadConfigAsync()
        {
            if (File.Exists(_configFile))
            {
                try
                {
                    var json = await File.ReadAllTextAsync(_configFile);
                    _config = JsonSerializer.Deserialize<AccountActivityConfig>(json, JsonOptions)
                              ?? new AccountActivityConfig();
                }
                catch
                {
                    _config = new AccountActivityConfig();
                }
            }

            await LoadTodayStatsAsync();
        }

        /// <summary>
        /// 保存配置
        /// </summary>
        public async Task SaveConfigAsync()
        {
            var json = JsonSerializer.Serialize(_config, JsonOptions);
            await File.WriteAllTextAsync(_configFile, json);
        }

        /// <summary>
        /// 启动活跃度维护
        /// </summary>
        public async Task StartAsync()
        {
            if (_isRunning)
                return;

            _isRunning = true;
            _cts = new CancellationTokenSource();

            NotifyStatus("已启动", "开始活跃度维护");

            await RunActivityLoopAsync(_cts.Token);
        }

        /// <summary>
        /// 停止活跃度维护
        /// </summary>
        public void Stop()
        {
            if (!_isRunning)
                return;

            _cts?.Cancel();
            _isRunning = false;

            NotifyStatus("已停止", "活跃度维护已停止");
        }

        /// <summary>
        /// 活跃度维护主循环
        /// </summary>
        private async Task RunActivityLoopAsync(CancellationToken ct)
        {
            var lastJobRefresh = DateTime.MinValue;
            var lastResumeBrowse = DateTime.MinValue;
            var lastMessageCheck = DateTime.MinValue;

            while (!ct.IsCancellationRequested)
            {
                try
                {
                    // 检查是否在活跃时间
                    if (!_config.ActiveHours.IsActiveNow())
                    {
                        NotifyStatus("休息中", "当前不在活跃时间段");
                        await Task.Delay(TimeSpan.FromMinutes(5), ct);
                        continue;
                    }

                    var now = DateTime.Now;

                    // 1. 岗位刷新
                    if (_config.JobRefresh.Enabled)
                    {
                        var refreshInterval = TimeSpan.FromHours(_config.JobRefresh.IntervalHours);
                        if (now - lastJobRefresh >= refreshInterval)
                        {
                            await ExecuteJobRefreshAsync(ct);
                            lastJobRefresh = now;
                        }
                    }

                    // 2. 简历浏览
                    if (_config.ResumeBrowse.Enabled)
                    {
                        var browseInterval = TimeSpan.FromMinutes(_config.ResumeBrowse.IntervalMinutes);
                        if (now - lastResumeBrowse >= browseInterval)
                        {
                            await ExecuteResumeBrowseAsync(ct);
                            lastResumeBrowse = now;
                        }
                    }

                    // 3. 消息检查
                    if (_config.MessageCheck.Enabled)
                    {
                        var checkInterval = TimeSpan.FromMinutes(_config.MessageCheck.IntervalMinutes);
                        if (now - lastMessageCheck >= checkInterval)
                        {
                            await ExecuteMessageCheckAsync(ct);
                            lastMessageCheck = now;
                        }
                    }

                    // 4. 随机行为
                    if (_config.RandomBehavior.Enabled)
                    {
                        await ExecuteRandomBehaviorAsync(ct);
                    }

                    // 更新统计
                    await SaveTodayStatsAsync();

                    // 等待下一轮
                    var waitTime = _random.Next(30, 90);
                    await Task.Delay(TimeSpan.FromSeconds(waitTime), ct);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    NotifyStatus("异常", $"活跃度维护异常: {ex.Message}");
                    await Task.Delay(TimeSpan.FromMinutes(1), ct);
                }
            }
        }

        /// <summary>
        /// 执行岗位刷新
        /// </summary>
        private async Task ExecuteJobRefreshAsync(CancellationToken ct)
        {
            NotifyStatus("执行中", "正在刷新岗位...");

            if (OnRefreshJobs != null)
            {
                await OnRefreshJobs(ct);
            }

            _todayStats.JobRefreshCount++;
            _todayStats.LastActiveAt = DateTime.Now;
        }

        /// <summary>
        /// 执行简历浏览
        /// </summary>
        private async Task ExecuteResumeBrowseAsync(CancellationToken ct)
        {
            NotifyStatus("执行中", "正在浏览简历...");

            if (OnBrowseResumes != null)
            {
                await OnBrowseResumes(_config.ResumeBrowse.CountPerSession, ct);
            }

            _todayStats.ResumeBrowseCount += _config.ResumeBrowse.CountPerSession;
            _todayStats.LastActiveAt = DateTime.Now;
        }

        /// <summary>
        /// 执行消息检查
        /// </summary>
        private async Task ExecuteMessageCheckAsync(CancellationToken ct)
        {
            NotifyStatus("执行中", "正在检查消息...");

            if (OnCheckMessages != null)
            {
                await OnCheckMessages(ct);
            }

            _todayStats.MessageCheckCount++;
            _todayStats.LastActiveAt = DateTime.Now;
        }

        /// <summary>
        /// 执行随机行为
        /// </summary>
        private async Task ExecuteRandomBehaviorAsync(CancellationToken ct)
        {
            var roll = _random.Next(100);

            // 随机暂停
            if (roll < _config.RandomBehavior.PauseProbability)
            {
                var pauseSeconds = _random.Next(
                    _config.RandomBehavior.PauseDurationMin,
                    _config.RandomBehavior.PauseDurationMax);

                NotifyStatus("暂停中", $"随机暂停 {pauseSeconds} 秒");
                await Task.Delay(TimeSpan.FromSeconds(pauseSeconds), ct);
                return;
            }

            roll = _random.Next(100);

            // 随机滚动
            if (roll < _config.RandomBehavior.ScrollProbability && OnRandomScroll != null)
            {
                NotifyStatus("执行中", "随机滚动页面");
                await OnRandomScroll(ct);
                return;
            }

            // 随机点击
            if (roll < _config.RandomBehavior.ScrollProbability + _config.RandomBehavior.ClickProbability 
                && OnRandomClick != null)
            {
                NotifyStatus("执行中", "随机点击");
                await OnRandomClick(ct);
            }
        }

        private void NotifyStatus(string status, string action)
        {
            OnStatusChanged?.Invoke(this, new ActivityStatusEventArgs
            {
                Status = status,
                Action = action
            });
        }

        private async Task LoadTodayStatsAsync()
        {
            if (File.Exists(_statsFile))
            {
                try
                {
                    var json = await File.ReadAllTextAsync(_statsFile);
                    _todayStats = JsonSerializer.Deserialize<ActivityStatistics>(json, JsonOptions)
                                  ?? new ActivityStatistics();

                    // 如果不是今天的统计，重置
                    if (_todayStats.Date.Date != DateTime.Today)
                    {
                        _todayStats = new ActivityStatistics();
                    }
                }
                catch
                {
                    _todayStats = new ActivityStatistics();
                }
            }
        }

        private async Task SaveTodayStatsAsync()
        {
            _todayStats.Date = DateTime.Today;
            var json = JsonSerializer.Serialize(_todayStats, JsonOptions);
            await File.WriteAllTextAsync(_statsFile, json);
        }

        /// <summary>
        /// 获取今日统计
        /// </summary>
        public ActivityStatistics GetTodayStats() => _todayStats;

        /// <summary>
        /// 获取配置
        /// </summary>
        public AccountActivityConfig GetConfig() => _config;

        /// <summary>
        /// 更新配置
        /// </summary>
        public async Task UpdateConfigAsync(AccountActivityConfig config)
        {
            _config = config;
            await SaveConfigAsync();
        }

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
    }
}
