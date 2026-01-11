using System;
using System.Threading;
using System.Threading.Tasks;
using RecruitAutomation.Core.Automation.Commands;

namespace RecruitAutomation.Core.Automation.Execution
{
    /// <summary>
    /// 消息指令执行器（占位实现）
    /// 
    /// 后续接入真实浏览器操作时，只需修改 ExecuteCoreAsync 的实现
    /// </summary>
    public sealed class MessageCommandExecutor : CommandExecutorBase
    {
        public override string Name => "MessageExecutor";

        public override CommandType[] SupportedTypes => new[]
        {
            CommandType.SendMessage,
            CommandType.SendGreeting,
            CommandType.ReplyMessage
        };

        protected override async Task<CommandResult> ExecuteCoreAsync(AutomationCommand command, CancellationToken ct)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();

            try
            {
                switch (command)
                {
                    case SendMessageCommand sendMsg:
                        return await ExecuteSendMessageAsync(sendMsg, ct);

                    case SendGreetingCommand sendGreet:
                        return await ExecuteSendGreetingAsync(sendGreet, ct);

                    case ReplyMessageCommand reply:
                        return await ExecuteReplyMessageAsync(reply, ct);

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

        private async Task<CommandResult> ExecuteSendMessageAsync(SendMessageCommand cmd, CancellationToken ct)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();

            // 占位实现：模拟发送消息
            // TODO: 接入真实浏览器操作
            await Task.Delay(500, ct); // 模拟网络延迟

            sw.Stop();

            // 返回成功结果
            return CommandResult.Success(cmd, sw.ElapsedMilliseconds, new
            {
                CandidateId = cmd.CandidateId,
                Content = cmd.Content,
                SentAt = DateTime.Now
            });
        }

        private async Task<CommandResult> ExecuteSendGreetingAsync(SendGreetingCommand cmd, CancellationToken ct)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();

            // 占位实现：模拟发送招呼
            await Task.Delay(300, ct);

            sw.Stop();

            return CommandResult.Success(cmd, sw.ElapsedMilliseconds, new
            {
                CandidateId = cmd.CandidateId,
                TemplateId = cmd.TemplateId,
                SentAt = DateTime.Now
            });
        }

        private async Task<CommandResult> ExecuteReplyMessageAsync(ReplyMessageCommand cmd, CancellationToken ct)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();

            // 占位实现：模拟回复消息
            await Task.Delay(400, ct);

            sw.Stop();

            return CommandResult.Success(cmd, sw.ElapsedMilliseconds, new
            {
                ConversationId = cmd.ConversationId,
                Content = cmd.Content,
                RepliedAt = DateTime.Now
            });
        }
    }
}
