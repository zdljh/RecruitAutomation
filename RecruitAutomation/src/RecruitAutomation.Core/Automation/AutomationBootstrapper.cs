using System;
using RecruitAutomation.Core.Automation.AI;
using RecruitAutomation.Core.Automation.Execution;
using RecruitAutomation.Core.Automation.UI;

namespace RecruitAutomation.Core.Automation
{
    /// <summary>
    /// 自动化系统引导器（白图 AI 4.0 风格）
    /// 
    /// 核心职责：
    /// 1. 初始化自动化系统的所有组件
    /// 2. 注册执行器和决策器
    /// 3. 启动/停止自动化系统
    /// 
    /// 使用方式：
    /// - 在应用启动时调用 Initialize()
    /// - 用户点击"启动"按钮时调用 Start()
    /// - 用户点击"停止"按钮时调用 Stop()
    /// </summary>
    public static class AutomationBootstrapper
    {
        private static volatile bool _initialized;
        private static readonly object _lock = new();

        /// <summary>
        /// 是否已初始化
        /// </summary>
        public static bool IsInitialized => _initialized;

        /// <summary>
        /// 初始化自动化系统
        /// </summary>
        public static void Initialize()
        {
            if (_initialized) return;

            lock (_lock)
            {
                if (_initialized) return;

                try
                {
                    // 注册执行器
                    RegisterExecutors();

                    // 注册决策器
                    RegisterDecisionMakers();

                    // 初始化状态通知器（触发单例创建）
                    _ = AutomationStateNotifier.Instance;

                    _initialized = true;
                }
                catch (Exception ex)
                {
                    // 初始化失败不抛异常，只记录
                    System.Diagnostics.Debug.WriteLine($"[AutomationBootstrapper] 初始化失败: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// 启动自动化系统
        /// </summary>
        public static void Start()
        {
            if (!_initialized)
            {
                Initialize();
            }

            try
            {
                CommandDispatcher.Instance.Start();
                AutomationStateNotifier.Instance.UpdateSystemState(AutomationSystemState.Running);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AutomationBootstrapper] 启动失败: {ex.Message}");
                AutomationStateNotifier.Instance.UpdateSystemState(AutomationSystemState.Error);
            }
        }

        /// <summary>
        /// 停止自动化系统
        /// </summary>
        public static void Stop()
        {
            try
            {
                AutomationStateNotifier.Instance.UpdateSystemState(AutomationSystemState.Stopping);
                CommandDispatcher.Instance.Stop();
                AutomationStateNotifier.Instance.UpdateSystemState(AutomationSystemState.Idle);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AutomationBootstrapper] 停止失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 暂停自动化系统
        /// </summary>
        public static void Pause()
        {
            try
            {
                CommandDispatcher.Instance.Stop();
                AutomationStateNotifier.Instance.UpdateSystemState(AutomationSystemState.Paused);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AutomationBootstrapper] 暂停失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 恢复自动化系统
        /// </summary>
        public static void Resume()
        {
            try
            {
                CommandDispatcher.Instance.Start();
                AutomationStateNotifier.Instance.UpdateSystemState(AutomationSystemState.Running);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AutomationBootstrapper] 恢复失败: {ex.Message}");
            }
        }

        private static void RegisterExecutors()
        {
            var dispatcher = CommandDispatcher.Instance;

            // 注册消息执行器
            dispatcher.RegisterExecutor(new MessageCommandExecutor());

            // 注册候选人执行器
            dispatcher.RegisterExecutor(new CandidateCommandExecutor());

            // TODO: 注册浏览器执行器（在 Browser 项目中实现）
        }

        private static void RegisterDecisionMakers()
        {
            var service = AIDecisionService.Instance;

            // 默认决策器已在 AIDecisionService 构造函数中注册
            // 这里可以注册额外的决策器
        }
    }
}
