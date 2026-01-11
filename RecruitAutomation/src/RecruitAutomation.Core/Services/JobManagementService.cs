using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using RecruitAutomation.Core.Models;

namespace RecruitAutomation.Core.Services
{
    /// <summary>
    /// 岗位管理服务
    /// 核心职责：管理从浏览器实例读取的岗位数据，与自动化任务联动
    /// </summary>
    public class JobManagementService
    {
        private readonly JobRepository _repository;

        /// <summary>
        /// 操作完成事件
        /// </summary>
        public event EventHandler<JobOperationLog>? OnOperationCompleted;

        /// <summary>
        /// 岗位数据变更事件
        /// </summary>
        public event EventHandler<JobDataChangedEventArgs>? OnJobDataChanged;

        public JobManagementService(JobRepository repository)
        {
            _repository = repository;
        }

        #region 岗位数据管理（来自浏览器读取）

        /// <summary>
        /// 保存从浏览器读取的岗位列表
        /// </summary>
        public async Task<int> SaveFetchedJobsAsync(
            string accountId,
            List<JobPosition> jobs)
        {
            var savedCount = 0;

            foreach (var job in jobs)
            {
                // 确保绑定账号ID
                job.AccountId = accountId;

                // 检查是否已存在
                var existing = await _repository.GetByIdAsync(job.Id);
                if (existing != null)
                {
                    // 保留自动化配置
                    job.AutomationEnabled = existing.AutomationEnabled;
                    job.ExecutionStatus = existing.ExecutionStatus;
                    job.MatchedCount = existing.MatchedCount;
                    job.TargetMatchCount = existing.TargetMatchCount;
                    job.GreetedCount = existing.GreetedCount;
                    job.ResumeCollectedCount = existing.ResumeCollectedCount;
                    job.LastExecutedAt = existing.LastExecutedAt;
                }

                await _repository.SaveAsync(job);
                savedCount++;
            }

            await LogOperationAsync("", accountId, JobOperation.Fetch, true,
                $"读取并保存 {savedCount} 个岗位");

            OnJobDataChanged?.Invoke(this, new JobDataChangedEventArgs
            {
                AccountId = accountId,
                ChangeType = JobDataChangeType.Fetched,
                AffectedCount = savedCount
            });

            return savedCount;
        }

        /// <summary>
        /// 获取账号下的所有岗位
        /// </summary>
        public async Task<List<JobPosition>> GetJobsByAccountAsync(string accountId)
        {
            var all = await _repository.GetAllAsync();
            return all.Where(j => j.AccountId == accountId).ToList();
        }

        /// <summary>
        /// 获取所有开放中的岗位
        /// </summary>
        public async Task<List<JobPosition>> GetOpenJobsAsync()
        {
            var all = await _repository.GetAllAsync();
            return all.Where(j => j.Status == JobStatus.Open).ToList();
        }

        /// <summary>
        /// 获取已启用自动化的岗位
        /// </summary>
        public async Task<List<JobPosition>> GetAutomationEnabledJobsAsync()
        {
            var all = await _repository.GetAllAsync();
            return all.Where(j => j.AutomationEnabled && j.Status == JobStatus.Open).ToList();
        }

        /// <summary>
        /// 获取待执行的岗位
        /// </summary>
        public async Task<List<JobPosition>> GetPendingJobsAsync()
        {
            var all = await _repository.GetAllAsync();
            return all.Where(j =>
                j.AutomationEnabled &&
                j.Status == JobStatus.Open &&
                j.ExecutionStatus == JobExecutionStatus.Pending)
                .ToList();
        }

        #endregion

        #region 自动化控制

        /// <summary>
        /// 启用岗位自动化
        /// </summary>
        public async Task<bool> EnableAutomationAsync(
            string jobId,
            int targetMatchCount = 100)
        {
            var job = await _repository.GetByIdAsync(jobId);
            if (job == null)
                return false;

            job.AutomationEnabled = true;
            job.TargetMatchCount = targetMatchCount;
            job.ExecutionStatus = JobExecutionStatus.Pending;
            job.UpdatedAt = DateTime.UtcNow;

            await _repository.SaveAsync(job);
            await LogOperationAsync(jobId, job.AccountId, JobOperation.EnableAutomation, true,
                $"启用自动化，目标匹配数: {targetMatchCount}");

            return true;
        }

        /// <summary>
        /// 禁用岗位自动化
        /// </summary>
        public async Task<bool> DisableAutomationAsync(string jobId)
        {
            var job = await _repository.GetByIdAsync(jobId);
            if (job == null)
                return false;

            job.AutomationEnabled = false;
            job.ExecutionStatus = JobExecutionStatus.Paused;
            job.UpdatedAt = DateTime.UtcNow;

            await _repository.SaveAsync(job);
            await LogOperationAsync(jobId, job.AccountId, JobOperation.DisableAutomation, true, "禁用自动化");

            return true;
        }

        /// <summary>
        /// 更新岗位执行状态
        /// </summary>
        public async Task UpdateExecutionStatusAsync(
            string jobId,
            JobExecutionStatus status)
        {
            var job = await _repository.GetByIdAsync(jobId);
            if (job == null)
                return;

            var oldStatus = job.ExecutionStatus;
            job.ExecutionStatus = status;

            if (status == JobExecutionStatus.Running)
            {
                job.LastExecutedAt = DateTime.UtcNow;
            }

            job.UpdatedAt = DateTime.UtcNow;
            await _repository.SaveAsync(job);

            var operation = status switch
            {
                JobExecutionStatus.Running => JobOperation.StartExecution,
                JobExecutionStatus.Paused => JobOperation.PauseExecution,
                JobExecutionStatus.Completed => JobOperation.CompleteExecution,
                _ => JobOperation.Fetch
            };

            await LogOperationAsync(jobId, job.AccountId, operation, true,
                $"状态变更: {oldStatus} -> {status}");
        }

        /// <summary>
        /// 更新岗位执行计数
        /// </summary>
        public async Task UpdateExecutionCountsAsync(
            string jobId,
            int? matchedCount = null,
            int? greetedCount = null,
            int? resumeCollectedCount = null)
        {
            var job = await _repository.GetByIdAsync(jobId);
            if (job == null)
                return;

            if (matchedCount.HasValue)
                job.MatchedCount = matchedCount.Value;
            if (greetedCount.HasValue)
                job.GreetedCount = greetedCount.Value;
            if (resumeCollectedCount.HasValue)
                job.ResumeCollectedCount = resumeCollectedCount.Value;

            // 检查是否达到目标
            if (job.MatchedCount >= job.TargetMatchCount)
            {
                job.ExecutionStatus = JobExecutionStatus.Completed;
            }

            job.UpdatedAt = DateTime.UtcNow;
            await _repository.SaveAsync(job);
        }

        /// <summary>
        /// 批量启用自动化
        /// </summary>
        public async Task<int> BatchEnableAutomationAsync(
            IEnumerable<string> jobIds,
            int targetMatchCount = 100)
        {
            var count = 0;
            foreach (var jobId in jobIds)
            {
                if (await EnableAutomationAsync(jobId, targetMatchCount))
                    count++;
            }
            return count;
        }

        /// <summary>
        /// 批量禁用自动化
        /// </summary>
        public async Task<int> BatchDisableAutomationAsync(IEnumerable<string> jobIds)
        {
            var count = 0;
            foreach (var jobId in jobIds)
            {
                if (await DisableAutomationAsync(jobId))
                    count++;
            }
            return count;
        }

        #endregion

        #region 基础操作

        /// <summary>
        /// 获取所有岗位
        /// </summary>
        public Task<List<JobPosition>> GetAllAsync() => _repository.GetAllAsync();

        /// <summary>
        /// 获取岗位
        /// </summary>
        public Task<JobPosition?> GetByIdAsync(string jobId) => _repository.GetByIdAsync(jobId);

        /// <summary>
        /// 删除岗位
        /// </summary>
        public async Task DeleteAsync(string jobId)
        {
            var job = await _repository.GetByIdAsync(jobId);
            var accountId = job?.AccountId ?? "";

            await _repository.DeleteAsync(jobId);
            await LogOperationAsync(jobId, accountId, JobOperation.Delete, true, "删除岗位");
        }

        /// <summary>
        /// 删除账号下的所有岗位
        /// </summary>
        public async Task DeleteByAccountAsync(string accountId)
        {
            var jobs = await GetJobsByAccountAsync(accountId);
            foreach (var job in jobs)
            {
                await _repository.DeleteAsync(job.Id);
            }

            await LogOperationAsync("", accountId, JobOperation.Delete, true,
                $"删除账号下 {jobs.Count} 个岗位");
        }

        /// <summary>
        /// 获取操作日志
        /// </summary>
        public Task<List<JobOperationLog>> GetLogsAsync(string? jobId = null, int limit = 100)
            => _repository.GetLogsAsync(jobId, limit);

        /// <summary>
        /// 获取账号的操作日志
        /// </summary>
        public async Task<List<JobOperationLog>> GetLogsByAccountAsync(string accountId, int limit = 100)
        {
            var logs = await _repository.GetLogsAsync(null, limit * 2);
            return logs.Where(l => l.AccountId == accountId).Take(limit).ToList();
        }

        #endregion

        #region 统计

        /// <summary>
        /// 获取账号岗位统计
        /// </summary>
        public async Task<JobStatistics> GetStatisticsAsync(string? accountId = null)
        {
            var all = await _repository.GetAllAsync();

            if (!string.IsNullOrEmpty(accountId))
            {
                all = all.Where(j => j.AccountId == accountId).ToList();
            }

            return new JobStatistics
            {
                TotalCount = all.Count,
                OpenCount = all.Count(j => j.Status == JobStatus.Open),
                AutomationEnabledCount = all.Count(j => j.AutomationEnabled),
                RunningCount = all.Count(j => j.ExecutionStatus == JobExecutionStatus.Running),
                CompletedCount = all.Count(j => j.ExecutionStatus == JobExecutionStatus.Completed),
                TotalMatchedCount = all.Sum(j => j.MatchedCount),
                TotalGreetedCount = all.Sum(j => j.GreetedCount)
            };
        }

        #endregion

        private async Task LogOperationAsync(
            string jobId,
            string accountId,
            JobOperation operation,
            bool success,
            string message)
        {
            var log = new JobOperationLog
            {
                JobId = jobId,
                AccountId = accountId,
                Operation = operation,
                Success = success,
                Message = message
            };

            await _repository.AddLogAsync(log);
            OnOperationCompleted?.Invoke(this, log);
        }
    }

    /// <summary>
    /// 岗位数据变更事件参数
    /// </summary>
    public class JobDataChangedEventArgs : EventArgs
    {
        public string AccountId { get; set; } = string.Empty;
        public JobDataChangeType ChangeType { get; set; }
        public int AffectedCount { get; set; }
    }

    /// <summary>
    /// 岗位数据变更类型
    /// </summary>
    public enum JobDataChangeType
    {
        Fetched,
        Updated,
        Deleted
    }

    /// <summary>
    /// 岗位统计
    /// </summary>
    public class JobStatistics
    {
        public int TotalCount { get; set; }
        public int OpenCount { get; set; }
        public int AutomationEnabledCount { get; set; }
        public int RunningCount { get; set; }
        public int CompletedCount { get; set; }
        public int TotalMatchedCount { get; set; }
        public int TotalGreetedCount { get; set; }
    }
}
