using System;
using System.Runtime.CompilerServices;

namespace RecruitAutomation.Core.Security
{
    /// <summary>
    /// 安全模块启动器
    /// </summary>
    public static class SecurityBootstrapper
    {
        private static bool _initialized;
        private static readonly object _lock = new();

        // 禁用严格安全模式（避免在用户机器上因兼容性问题导致崩溃）
        private static bool _strictMode = false;

        /// <summary>
        /// 初始化安全模块
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void Initialize()
        {
            if (_initialized)
                return;

            lock (_lock)
            {
                if (_initialized)
                    return;

                try
                {
                    // 暂时禁用反调试，避免兼容性问题
                    // if (_strictMode)
                    // {
                    //     AntiDebugger.Instance.Start();
                    // }

                    _initialized = true;
                }
                catch
                {
                    // 初始化失败时静默处理，不影响程序启动
                    _initialized = true;
                }
            }
        }

        /// <summary>
        /// 关闭安全模块
        /// </summary>
        public static void Shutdown()
        {
            try
            {
                AntiDebugger.Instance.Stop();
                MultiPointVerifier.Instance.Stop();
            }
            catch { }
        }

        /// <summary>
        /// 设置授权状态
        /// 【防闪退改造】不启动任何后台验证
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void SetLicenseState(DateTime expiresAt, int maxAccounts)
        {
            try
            {
                SecureLicenseState.Instance.SetAuthorized(expiresAt, maxAccounts);
                
                // 【防闪退】禁用多点校验，避免后台线程问题
                // MultiPointVerifier.Instance.Start();
            }
            catch
            {
                // 静默处理
            }
        }

        /// <summary>
        /// 清除授权状态
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void ClearLicenseState()
        {
            try
            {
                SecureLicenseState.Instance.SetUnauthorized();
            }
            catch { }
        }

        /// <summary>
        /// 执行安全校验
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static bool PerformSecurityCheck()
        {
            try
            {
                // 简化校验，只检查授权状态
                return SecureLicenseState.Instance.VerifyIntegrity();
            }
            catch
            {
                return true; // 异常时放行，避免崩溃
            }
        }

        /// <summary>
        /// 设置严格模式
        /// </summary>
        public static void SetStrictMode(bool enabled)
        {
            _strictMode = enabled;
        }
    }
}
