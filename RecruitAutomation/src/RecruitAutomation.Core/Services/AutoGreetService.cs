using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using RecruitAutomation.Core.Models;

namespace RecruitAutomation.Core.Services
{
    /// <summary>
    /// 自动打招呼服务
    /// </summary>
    public class AutoGreetService
    {
        private readonly CandidateRepository _repository;
        private readonly CandidateFilterService _filterService;
        private readonly FilterConfig _filterConfig;

        /// <summary>
        /// 打招呼事件（由平台模块订阅实现）
        /// </summary>
        public event Func<CandidateInfo, string, CancellationToken, Task<bool>>? OnGreetCandidate;

        /// <summary>
        /// 打招呼完成事件
        /// </summary>
        public event EventHandler<GreetResult>? OnGreetCompleted;

        public AutoGreetService(
            CandidateRepository repository,
            CandidateFilterService filterService,
            FilterConfig filterConfig)
        {
            _repository = repository;
            _filterService = filterService;
            _filterConfig = filterConfig;
        }

        /// <summary>
        /// 打招呼结果
        /// </summary>
        public class GreetResult
        {
            public CandidateInfo Candidate { get; set; } = null!;
            public bool Success { get; set; }
            public string Message { get; set; } = string.Empty;
            public DateTime Timestamp { get; set; } = DateTime.Now;
        }

        /// <summary>
        /// 处理新采集的候选人
        /// </summary>
        public async Task<GreetResult?> ProcessCandidateAsync(
            CandidateInfo candidate, 
            string greetMessage,
            CancellationToken ct = default)
        {
            // 1. 筛选
            var filterResult = _filterService.Filter(candidate);
            candidate.FilterScore = filterResult.Score;

            // 2. 更新状态
            if (filterResult.Passed)
            {
                candidate.Status = CandidateStatus.Passed;
            }
            else
            {
                candidate.Status = CandidateStatus.Rejected;
            }

            // 3. 保存
            await _repository.SaveAsync(candidate);

            // 4. 判断是否自动打招呼
            if (filterResult.ShouldAutoGreet && !candidate.HasGreeted)
            {
                return await GreetCandidateAsync(candidate, greetMessage, ct);
            }

            return null;
        }

        /// <summary>
        /// 对单个候选人打招呼
        /// </summary>
        public async Task<GreetResult> GreetCandidateAsync(
            CandidateInfo candidate,
            string greetMessage,
            CancellationToken ct = default)
        {
            var result = new GreetResult { Candidate = candidate };

            try
            {
                if (candidate.HasGreeted)
                {
                    result.Success = false;
                    result.Message = "已经打过招呼";
                    return result;
                }

                // 触发打招呼事件（由平台模块实现）
                if (OnGreetCandidate != null)
                {
                    result.Success = await OnGreetCandidate(candidate, greetMessage, ct);
                    result.Message = result.Success ? "打招呼成功" : "打招呼失败";
                }
                else
                {
                    result.Success = false;
                    result.Message = "未配置打招呼处理器";
                }

                // 更新状态
                if (result.Success)
                {
                    await _repository.MarkGreetedAsync(candidate.Id);
                }

                OnGreetCompleted?.Invoke(this, result);
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Message = $"打招呼异常: {ex.Message}";
            }

            return result;
        }

        /// <summary>
        /// 批量自动打招呼
        /// </summary>
        public async Task<List<GreetResult>> BatchGreetAsync(
            string greetMessage,
            int maxCount = 50,
            int delayBetweenMs = 3000,
            CancellationToken ct = default)
        {
            var results = new List<GreetResult>();

            // 获取待打招呼的候选人
            var candidates = await _repository.GetPendingGreetAsync(
                _filterConfig.AutoGreetScore, 
                maxCount);

            foreach (var candidate in candidates)
            {
                if (ct.IsCancellationRequested)
                    break;

                var result = await GreetCandidateAsync(candidate, greetMessage, ct);
                results.Add(result);

                // 延迟，避免操作过快
                if (delayBetweenMs > 0)
                {
                    await Task.Delay(delayBetweenMs, ct);
                }
            }

            return results;
        }

        /// <summary>
        /// 重新筛选所有候选人
        /// </summary>
        public async Task RefilterAllAsync()
        {
            var all = await _repository.GetAllAsync();

            foreach (var candidate in all)
            {
                var filterResult = _filterService.Filter(candidate);
                candidate.FilterScore = filterResult.Score;

                if (filterResult.Passed)
                {
                    if (candidate.Status == CandidateStatus.New || 
                        candidate.Status == CandidateStatus.Rejected)
                    {
                        candidate.Status = CandidateStatus.Passed;
                    }
                }
                else
                {
                    if (candidate.Status == CandidateStatus.New || 
                        candidate.Status == CandidateStatus.Passed)
                    {
                        candidate.Status = CandidateStatus.Rejected;
                    }
                }
            }

            await _repository.SaveManyAsync(all);
        }
    }
}
