using System;
using System.Threading;
using System.Threading.Tasks;
using RecruitAutomation.Core.Automation.Commands;

namespace RecruitAutomation.Core.Automation.Execution
{
    /// <summary>
    /// 候选人指令执行器（占位实现）
    /// </summary>
    public sealed class CandidateCommandExecutor : CommandExecutorBase
    {
        public override string Name => "CandidateExecutor";

        public override CommandType[] SupportedTypes => new[]
        {
            CommandType.FilterCandidate,
            CommandType.CollectResume,
            CommandType.MarkCandidate
        };

        protected override async Task<CommandResult> ExecuteCoreAsync(AutomationCommand command, CancellationToken ct)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();

            try
            {
                switch (command)
                {
                    case FilterCandidateCommand filter:
                        return await ExecuteFilterCandidateAsync(filter, ct);

                    case CollectResumeCommand collect:
                        return await ExecuteCollectResumeAsync(collect, ct);

                    case MarkCandidateCommand mark:
                        return await ExecuteMarkCandidateAsync(mark, ct);

                    default:
                        sw.Stop();
                        return CommandResult.Failed(command, $"不支持的指令类型: {command.GetType().Name}", sw.ElapsedMilliseconds);
                }
            }
            catch (Exception ex)
            {
                sw.Stop();
                return CommandResult.Failed(command, ex, sw.ElapsedMilliseconds);
            }
        }

        private async Task<CommandResult> ExecuteFilterCandidateAsync(FilterCandidateCommand cmd, CancellationToken ct)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();

            // 占位实现：记录筛选结果
            // TODO: 接入真实数据库操作
            await Task.Delay(100, ct);

            sw.Stop();

            return CommandResult.Success(cmd, sw.ElapsedMilliseconds, new
            {
                CandidateId = cmd.CandidateId,
                Decision = cmd.Decision.ToString(),
                MatchScore = cmd.MatchScore,
                FilteredAt = DateTime.Now
            });
        }

        private async Task<CommandResult> ExecuteCollectResumeAsync(CollectResumeCommand cmd, CancellationToken ct)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();

            // 占位实现：模拟收集简历
            await Task.Delay(1000, ct);

            sw.Stop();

            return CommandResult.Success(cmd, sw.ElapsedMilliseconds, new
            {
                CandidateId = cmd.CandidateId,
                Source = cmd.Source,
                SavePath = cmd.SavePath ?? "default_path",
                CollectedAt = DateTime.Now
            });
        }

        private async Task<CommandResult> ExecuteMarkCandidateAsync(MarkCandidateCommand cmd, CancellationToken ct)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();

            // 占位实现：标记候选人
            await Task.Delay(50, ct);

            sw.Stop();

            return CommandResult.Success(cmd, sw.ElapsedMilliseconds, new
            {
                CandidateId = cmd.CandidateId,
                MarkType = cmd.MarkType,
                MarkValue = cmd.MarkValue,
                MarkedAt = DateTime.Now
            });
        }
    }
}
