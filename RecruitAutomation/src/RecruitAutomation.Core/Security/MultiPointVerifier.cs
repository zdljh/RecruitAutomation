using System;
using System.Runtime.CompilerServices;
using System.Threading;

namespace RecruitAutomation.Core.Security
{
    /// <summary>
    /// 多点校验（优化版）
    /// </summary>
    public sealed class MultiPointVerifier
    {
        private static MultiPointVerifier? _instance;
        private static readonly object _lock = new();

        private readonly Random _random;
        private Timer? _periodicTimer;
        private bool _isRunning;

        private MultiPointVerifier()
        {
            _random = new Random((int)(DateTime.UtcNow.Ticks ^ Environment.TickCount64));
        }

        public static MultiPointVerifier Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        _instance ??= new MultiPointVerifier();
                    }
                }
                return _instance;
            }
        }

        /// <summary>
        /// 启动多点校验
        /// 【防闪退改造】禁用后台 Timer，避免跨线程问题
        /// </summary>
        public void Start()
        {
            // 【防闪退】禁用后台定时校验，避免跨线程问题导致闪退
            // 校验改为在业务入口点按需执行
            _isRunning = true;
            
            // 原代码已注释，不再启动 Timer
            // _periodicTimer = new Timer(
            //     _ => PeriodicVerify(),
            //     null,
            //     TimeSpan.FromSeconds(60),
            //     TimeSpan.FromSeconds(60));
        }

        /// <summary>
        /// 停止校验
        /// </summary>
        public void Stop()
        {
            _isRunning = false;
            _periodicTimer?.Dispose();
            _periodicTimer = null;
        }

        /// <summary>
        /// 校验点：UI 初始化
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public bool VerifyOnUIInit()
        {
            return ExecuteVerification();
        }

        /// <summary>
        /// 校验点：业务入口
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public bool VerifyOnBusinessEntry()
        {
            return ExecuteVerification();
        }

        /// <summary>
        /// 快速校验
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool QuickVerify()
        {
            try
            {
                return SecureLicenseState.Instance.VerifyIntegrity();
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 获取操作令牌
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public long GetOperationToken()
        {
            try
            {
                if (!QuickVerify())
                    return 0;

                return SecureLicenseState.Instance.GetAuthToken();
            }
            catch
            {
                return 0;
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private bool ExecuteVerification()
        {
            try
            {
                // License 状态检查
                if (!SecureLicenseState.Instance.VerifyIntegrity())
                    return false;

                // 随机刷新动态令牌
                if (_random.Next(100) < 20)
                {
                    SecureLicenseState.Instance.RefreshDynamicToken();
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        private void PeriodicVerify()
        {
            if (!_isRunning)
                return;

            try
            {
                ExecuteVerification();
            }
            catch { }
        }
    }
}
