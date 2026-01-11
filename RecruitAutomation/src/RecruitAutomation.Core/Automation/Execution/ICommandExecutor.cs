using System;
using System.Threading;
using System.Threading.Tasks;
using RecruitAutomation.Core.Automation.Commands;

namespace RecruitAutomation.Core.Automation.Execution
{
    /// <summary>
    /// 指令执行器接口（白图 AI 4.0 风格）
    /// 
    /// 设计原则：
    /// - 执行器只负责执行指令，不做决策
    /// - 所有异常都被捕获并转换为 CommandResult
    /// - 执行器不直接操作 UI
    /// </summary>
    public interface ICommandExecutor
    {
        /// <summary>
        /// 执行器名称
        /// </summary>
        string Name { get; }

        /// <summary>
        /// 支持的指令类型
        /// </summary>
        CommandType[] SupportedTypes { get; }

        /// <summary>
        /// 是否可以执行指定指令
        /// </summary>
        bool CanExecute(AutomationCommand command);

        /// <summary>
        /// 执行指令
        /// </summary>
        Task<CommandResult> ExecuteAsync(AutomationCommand command, CancellationToken ct = default);
    }

    /// <summary>
    /// 指令执行器基类
    /// </summary>
    public abstract class CommandExecutorBase : ICommandExecutor
    {
        public abstract string Name { get; }
        public abstract CommandType[] SupportedTypes { get; }

        public virtual bool CanExecute(AutomationCommand command)
        {
            foreach (var type in SupportedTypes)
            {
                if (type == command.Type)
                    return true;
            }
            return false;
        }

        public async Task<CommandResult> ExecuteAsync(AutomationCommand command, CancellationToken ct = default)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            int retryCount = 0;

            while (retryCount <= command.MaxRetries)
            {
                try
                {
                    ct.ThrowIfCancellationRequested();

                    using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                    cts.CancelAfter(command.TimeoutMs);

                    var result = await ExecuteCoreAsync(command, cts.Token);
                    sw.Stop();

                    if (result.IsSuccess || retryCount >= command.MaxRetries)
                    {
                        return result;
                    }

                    retryCount++;
                    await Task.Delay(1000 * retryCount, ct); // 递增延迟重试
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    sw.Stop();
                    return CommandResult.Cancelled(command, sw.ElapsedMilliseconds);
                }
                catch (OperationCanceledException)
                {
                    sw.Stop();
                    return CommandResult.Timeout(command, sw.ElapsedMilliseconds);
                }
                catch (Exception ex)
                {
                    if (retryCount >= command.MaxRetries)
                    {
                        sw.Stop();
                        return CommandResult.Failed(command, ex, sw.ElapsedMilliseconds, retryCount);
                    }

                    retryCount++;
                    await Task.Delay(1000 * retryCount, ct);
                }
            }

            sw.Stop();
            return CommandResult.Failed(command, "超过最大重试次数", sw.ElapsedMilliseconds);
        }

        /// <summary>
        /// 执行核心逻辑（子类实现）
        /// </summary>
        protected abstract Task<CommandResult> ExecuteCoreAsync(AutomationCommand command, CancellationToken ct);
    }
}
