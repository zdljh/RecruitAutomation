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
    /// 候选人数据仓储
    /// 使用 JSON 文件存储，按账号隔离
    /// </summary>
    public class CandidateRepository
    {
        private readonly string _accountId;
        private readonly string _dataDir;
        private readonly string _candidatesFile;
        private readonly SemaphoreSlim _lock = new(1, 1);

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        public CandidateRepository(string accountId)
        {
            _accountId = accountId;
            _dataDir = Path.Combine(AppConstants.DataRootPath, "accounts", accountId);
            _candidatesFile = Path.Combine(_dataDir, "candidates.json");
            
            EnsureDirectoryExists();
        }

        private void EnsureDirectoryExists()
        {
            if (!Directory.Exists(_dataDir))
                Directory.CreateDirectory(_dataDir);
        }

        /// <summary>
        /// 获取所有候选人
        /// </summary>
        public async Task<List<CandidateInfo>> GetAllAsync()
        {
            await _lock.WaitAsync();
            try
            {
                if (!File.Exists(_candidatesFile))
                    return new List<CandidateInfo>();

                var json = await File.ReadAllTextAsync(_candidatesFile);
                return JsonSerializer.Deserialize<List<CandidateInfo>>(json, JsonOptions) 
                       ?? new List<CandidateInfo>();
            }
            finally
            {
                _lock.Release();
            }
        }

        /// <summary>
        /// 根据ID获取候选人
        /// </summary>
        public async Task<CandidateInfo?> GetByIdAsync(string id)
        {
            var all = await GetAllAsync();
            return all.FirstOrDefault(c => c.Id == id);
        }

        /// <summary>
        /// 根据平台用户ID获取候选人
        /// </summary>
        public async Task<CandidateInfo?> GetByPlatformUserIdAsync(string platform, string platformUserId)
        {
            var all = await GetAllAsync();
            return all.FirstOrDefault(c => 
                c.Platform == platform && c.PlatformUserId == platformUserId);
        }

        /// <summary>
        /// 保存候选人（新增或更新）
        /// </summary>
        public async Task SaveAsync(CandidateInfo candidate)
        {
            await _lock.WaitAsync();
            try
            {
                var all = await LoadUnsafeAsync();
                
                // 生成ID
                if (string.IsNullOrEmpty(candidate.Id))
                {
                    candidate.Id = $"{candidate.Platform}_{candidate.PlatformUserId}";
                }

                // 查找是否存在
                var index = all.FindIndex(c => c.Id == candidate.Id);
                if (index >= 0)
                {
                    candidate.UpdatedAt = DateTime.UtcNow;
                    all[index] = candidate;
                }
                else
                {
                    candidate.CollectedAt = DateTime.UtcNow;
                    candidate.UpdatedAt = DateTime.UtcNow;
                    all.Add(candidate);
                }

                await SaveUnsafeAsync(all);
            }
            finally
            {
                _lock.Release();
            }
        }

        /// <summary>
        /// 批量保存候选人
        /// </summary>
        public async Task SaveManyAsync(IEnumerable<CandidateInfo> candidates)
        {
            await _lock.WaitAsync();
            try
            {
                var all = await LoadUnsafeAsync();
                var dict = all.ToDictionary(c => c.Id);

                foreach (var candidate in candidates)
                {
                    if (string.IsNullOrEmpty(candidate.Id))
                    {
                        candidate.Id = $"{candidate.Platform}_{candidate.PlatformUserId}";
                    }

                    if (dict.ContainsKey(candidate.Id))
                    {
                        candidate.UpdatedAt = DateTime.UtcNow;
                        dict[candidate.Id] = candidate;
                    }
                    else
                    {
                        candidate.CollectedAt = DateTime.UtcNow;
                        candidate.UpdatedAt = DateTime.UtcNow;
                        dict[candidate.Id] = candidate;
                    }
                }

                await SaveUnsafeAsync(dict.Values.ToList());
            }
            finally
            {
                _lock.Release();
            }
        }

        /// <summary>
        /// 更新候选人状态
        /// </summary>
        public async Task UpdateStatusAsync(string id, CandidateStatus status)
        {
            var candidate = await GetByIdAsync(id);
            if (candidate != null)
            {
                candidate.Status = status;
                candidate.UpdatedAt = DateTime.UtcNow;
                await SaveAsync(candidate);
            }
        }

        /// <summary>
        /// 标记已打招呼
        /// </summary>
        public async Task MarkGreetedAsync(string id)
        {
            var candidate = await GetByIdAsync(id);
            if (candidate != null)
            {
                candidate.HasGreeted = true;
                candidate.GreetedAt = DateTime.UtcNow;
                candidate.Status = CandidateStatus.Greeted;
                candidate.UpdatedAt = DateTime.UtcNow;
                await SaveAsync(candidate);
            }
        }

        /// <summary>
        /// 删除候选人
        /// </summary>
        public async Task DeleteAsync(string id)
        {
            await _lock.WaitAsync();
            try
            {
                var all = await LoadUnsafeAsync();
                all.RemoveAll(c => c.Id == id);
                await SaveUnsafeAsync(all);
            }
            finally
            {
                _lock.Release();
            }
        }

        /// <summary>
        /// 查询候选人
        /// </summary>
        public async Task<List<CandidateInfo>> QueryAsync(
            CandidateStatus? status = null,
            bool? hasGreeted = null,
            double? minScore = null,
            int skip = 0,
            int take = 100)
        {
            var all = await GetAllAsync();
            
            var query = all.AsEnumerable();

            if (status.HasValue)
                query = query.Where(c => c.Status == status.Value);

            if (hasGreeted.HasValue)
                query = query.Where(c => c.HasGreeted == hasGreeted.Value);

            if (minScore.HasValue)
                query = query.Where(c => c.FilterScore >= minScore.Value);

            return query
                .OrderByDescending(c => c.FilterScore)
                .ThenByDescending(c => c.CollectedAt)
                .Skip(skip)
                .Take(take)
                .ToList();
        }

        /// <summary>
        /// 获取待打招呼的候选人
        /// </summary>
        public async Task<List<CandidateInfo>> GetPendingGreetAsync(double minScore, int limit = 50)
        {
            var all = await GetAllAsync();
            return all
                .Where(c => !c.HasGreeted && c.Status == CandidateStatus.Passed && c.FilterScore >= minScore)
                .OrderByDescending(c => c.FilterScore)
                .Take(limit)
                .ToList();
        }

        private async Task<List<CandidateInfo>> LoadUnsafeAsync()
        {
            if (!File.Exists(_candidatesFile))
                return new List<CandidateInfo>();

            var json = await File.ReadAllTextAsync(_candidatesFile);
            return JsonSerializer.Deserialize<List<CandidateInfo>>(json, JsonOptions) 
                   ?? new List<CandidateInfo>();
        }

        private async Task SaveUnsafeAsync(List<CandidateInfo> candidates)
        {
            var json = JsonSerializer.Serialize(candidates, JsonOptions);
            await File.WriteAllTextAsync(_candidatesFile, json);
        }
    }
}
