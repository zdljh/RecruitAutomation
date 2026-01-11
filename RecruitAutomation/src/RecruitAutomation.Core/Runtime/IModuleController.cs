using System;
using System.Threading;
using System.Threading.Tasks;

namespace RecruitAutomation.Core.Runtime
{
    /// <summary>
    /// 模块控制器接口（白图 AI 4.0 风格）
    /// 
    /// 所有独立模块都实现此接口，统一生命周期管理
    /// </summary>
    public interface IModuleController : IDisposable
    {
        /// <summary>
        /// 模块 ID
        /// </summary>
        string ModuleId { get; }

        /// <summary>
        /// 模块显示名称
        /// </summary>
        string DisplayName { get; }

        /// <summary>
        /// 当前状态
        /// </summary>
        ModuleStatus Status { get; }

        /// <summary>
        /// 最后错误信息
        /// </summary>
        string? LastError { get; }

        /// <summary>
        /// 初始化模块（异步）
        /// </summary>
        Task<bool> InitializeAsync(CancellationToken ct = default);

        /// <summary>
        /// 启动模块
        /// </summary>
        Task<bool> StartAsync(CancellationToken ct = default);

        /// <summary>
        /// 停止模块
        /// </summary>
        Task StopAsync();

        /// <summary>
        /// 重置模块（从错误状态恢复）
        /// </summary>
        Task<bool> ResetAsync();

        /// <summary>
        /// 状态变化事件
        /// </summary>
        event EventHandler<ModuleStatusEventArgs>? StatusChanged;
    }

    /// <summary>
    /// 模块状态变化事件参数
    /// </summary>
    public class ModuleStatusEventArgs : EventArgs
    {
        public ModuleStatus OldStatus { get; }
        public ModuleStatus NewStatus { get; }
        public string? Message { get; }

        public ModuleStatusEventArgs(ModuleStatus oldStatus, ModuleStatus newStatus, string? message = null)
        {
            OldStatus = oldStatus;
            NewStatus = newStatus;
            Message = message;
        }
    }
}
