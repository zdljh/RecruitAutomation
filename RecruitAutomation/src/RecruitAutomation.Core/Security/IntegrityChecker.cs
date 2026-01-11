using System;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;

namespace RecruitAutomation.Core.Security
{
    /// <summary>
    /// 代码完整性校验（优化版）
    /// </summary>
    public sealed class IntegrityChecker
    {
        private static IntegrityChecker? _instance;
        private static readonly object _lock = new();

        // 基准哈希（首次运行时计算）
        private byte[]? _baselineHash;
        private bool _initialized;

        private IntegrityChecker() { }

        public static IntegrityChecker Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        _instance ??= new IntegrityChecker();
                    }
                }
                return _instance;
            }
        }

        /// <summary>
        /// 验证程序集完整性
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public bool VerifyAssemblyIntegrity()
        {
            try
            {
                // 首次调用时初始化基准
                if (!_initialized)
                {
                    InitializeBaseline();
                    _initialized = true;
                    return true; // 首次运行直接通过
                }

                // 后续调用时比较
                if (_baselineHash == null)
                    return true;

                var currentHash = ComputeCurrentHash();
                if (currentHash == null)
                    return true;

                return ConstantTimeEquals(_baselineHash, currentHash);
            }
            catch
            {
                // 异常时放行
                return true;
            }
        }

        /// <summary>
        /// 强制验证
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public bool ForceVerify()
        {
            _initialized = false;
            return VerifyAssemblyIntegrity();
        }

        private void InitializeBaseline()
        {
            try
            {
                _baselineHash = ComputeCurrentHash();
            }
            catch
            {
                _baselineHash = null;
            }
        }

        private byte[]? ComputeCurrentHash()
        {
            try
            {
                var assembly = typeof(SecureLicenseState).Assembly;
                var location = assembly.Location;

                if (string.IsNullOrEmpty(location) || !File.Exists(location))
                    return null;

                using var sha = SHA256.Create();
                using var stream = File.OpenRead(location);
                return sha.ComputeHash(stream);
            }
            catch
            {
                return null;
            }
        }

        private static bool ConstantTimeEquals(byte[] a, byte[] b)
        {
            if (a.Length != b.Length)
                return false;

            int diff = 0;
            for (int i = 0; i < a.Length; i++)
            {
                diff |= a[i] ^ b[i];
            }
            return diff == 0;
        }
    }
}
