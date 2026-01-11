using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using RecruitAutomation.Core.Constants;
using RecruitAutomation.Core.Models;

namespace RecruitAutomation.Core.Services
{
    /// <summary>
    /// 岗位数据仓储
    /// 支持全局存储（所有账号的岗位）
    /// </summary>
    public class JobRepository
    {
        private readonly string _dataDir;
        private readonly string _jobsFile;
        private readonly string _logsFile;
        private readonly SemaphoreSlim _lock = new(1, 1);

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        /// <summary>
        /// 创建全局岗位仓储
        /// </summary>
        public JobRepository()
        {
            _dataDir = Path.Combine(AppConstants.DataRootPath, "jobs");
            _jobsFile = Path.Combine(_dataDir, "all_jobs.json");
            _logsFile = Path.Combine(_dataDir, "job_logs.json");

            if (!Directory.Exists(_dataDir))
                Directory.CreateDirectory(_dataDir);
        }

        /// <summary>
        /// 创建账号专属岗位仓储（兼容旧版）
        /// </summary>
        public JobRepository(string accountId)
        {
            _dataDir = Path.Combine(AppConstants.DataRootPath, "accounts", accountId);
            _jobsFile = Path.Combine(_dataDir, "jobs.json");
            _logsFile = Path.Combine(_dataDir, "job_logs.json");

            if (!Directory.Exists(_dataDir))
                Directory.CreateDirectory(_dataDir);
        }

        #region 岗位管理

        /// <summary>
        /// 获取所有岗位
        /// </summary>
        public async Task<List<JobPosition>> GetAllAsync()
        {
            await _lock.WaitAsync();
            try
            {
                return await LoadJobsUnsafeAsync();
            }
            finally
            {
                _lock.Release();
            }
        }

        /// <summary>
        /// 获取单个岗位
        /// </summary>
        public async Task<JobPosition?> GetByIdAsync(string jobId)
        {
            var all = await GetAllAsync();
            return all.FirstOrDefault(j => j.Id == jobId);
        }

        /// <summary>
        /// 保存岗位
        /// </summary>
        public async Task SaveAsync(JobPosition job)
        {
            await _lock.WaitAsync();
            try
            {
                var all = await LoadJobsUnsafeAsync();

                if (string.IsNullOrEmpty(job.Id))
                {
                    job.Id = $"{job.Platform}_{job.PlatformJobId}";
                }

                var index = all.FindIndex(j => j.Id == job.Id);
                if (index >= 0)
                {
                    job.UpdatedAt = DateTime.UtcNow;
                    all[index] = job;
                }
                else
                {
                    job.FetchedAt = DateTime.UtcNow;
                    job.UpdatedAt = DateTime.UtcNow;
                    all.Add(job);
                }

                await SaveJobsUnsafeAsync(all);
            }
            finally
            {
                _lock.Release();
            }
        }

        /// <summary>
        /// 删除岗位
        /// </summary>
        public async Task DeleteAsync(string jobId)
        {
            await _lock.WaitAsync();
            try
            {
                var all = await LoadJobsUnsafeAsync();
                all.RemoveAll(j => j.Id == jobId);
                await SaveJobsUnsafeAsync(all);
            }
            finally
            {
                _lock.Release();
            }
        }

        /// <summary>
        /// 获取开放中的岗位
        /// </summary>
        public async Task<List<JobPosition>> GetOpenJobsAsync()
        {
            var all = await GetAllAsync();
            return all.Where(j => j.Status == JobStatus.Open).ToList();
        }

        /// <summary>
        /// 获取需要刷新的岗位
        /// </summary>
        public async Task<List<JobPosition>> GetNeedRefreshAsync(int intervalHours = 4)
        {
            var all = await GetAllAsync();
            var threshold = DateTime.UtcNow.AddHours(-intervalHours);

            return all.Where(j =>
                j.Status == JobStatus.Open &&
                (j.FetchedAt < threshold))
                .OrderBy(j => j.FetchedAt)
                .ToList();
        }

        /// <summary>
        /// 按账号获取岗位
        /// </summary>
        public async Task<List<JobPosition>> GetByAccountAsync(string accountId)
        {
            var all = await GetAllAsync();
            return all.Where(j => j.AccountId == accountId).ToList();
        }

        /// <summary>
        /// 按平台获取岗位
        /// </summary>
        public async Task<List<JobPosition>> GetByPlatformAsync(RecruitPlatform platform)
        {
            var all = await GetAllAsync();
            return all.Where(j => j.Platform == platform).ToList();
        }

        #endregion

        #region 操作日志

        /// <summary>
        /// 添加操作日志
        /// </summary>
        public async Task AddLogAsync(JobOperationLog log)
        {
            await _lock.WaitAsync();
            try
            {
                var logs = await LoadLogsUnsafeAsync();
                logs.Add(log);

                // 只保留最近1000条
                if (logs.Count > 1000)
                {
                    logs = logs.Skip(logs.Count - 1000).ToList();
                }

                await SaveLogsUnsafeAsync(logs);
            }
            finally
            {
                _lock.Release();
            }
        }

        /// <summary>
        /// 获取岗位操作日志
        /// </summary>
        public async Task<List<JobOperationLog>> GetLogsAsync(string? jobId = null, int limit = 100)
        {
            await _lock.WaitAsync();
            try
            {
                var logs = await LoadLogsUnsafeAsync();

                if (!string.IsNullOrEmpty(jobId))
                {
                    logs = logs.Where(l => l.JobId == jobId).ToList();
                }

                return logs.OrderByDescending(l => l.Timestamp).Take(limit).ToList();
            }
            finally
            {
                _lock.Release();
            }
        }

        #endregion

        private async Task<List<JobPosition>> LoadJobsUnsafeAsync()
        {
            if (!File.Exists(_jobsFile))
                return new List<JobPosition>();

            var json = await File.ReadAllTextAsync(_jobsFile);
            return JsonSerializer.Deserialize<List<JobPosition>>(json, JsonOptions)
                   ?? new List<JobPosition>();
        }

        private async Task SaveJobsUnsafeAsync(List<JobPosition> jobs)
        {
            var json = JsonSerializer.Serialize(jobs, JsonOptions);
            await File.WriteAllTextAsync(_jobsFile, json);
        }

        private async Task<List<JobOperationLog>> LoadLogsUnsafeAsync()
        {
            if (!File.Exists(_logsFile))
                return new List<JobOperationLog>();

            var json = await File.ReadAllTextAsync(_logsFile);
            return JsonSerializer.Deserialize<List<JobOperationLog>>(json, JsonOptions)
                   ?? new List<JobOperationLog>();
        }

        private async Task SaveLogsUnsafeAsync(List<JobOperationLog> logs)
        {
            var json = JsonSerializer.Serialize(logs, JsonOptions);
            await File.WriteAllTextAsync(_logsFile, json);
        }
    }
}
