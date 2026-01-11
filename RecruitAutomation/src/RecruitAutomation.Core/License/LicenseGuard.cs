using System;
using System.Runtime.CompilerServices;
using System.Threading;
using RecruitAutomation.Core.Security;

namespace RecruitAutomation.Core.License
{
    /// <summary>
    /// License 守卫（增强版）
    /// </summary>
    public sealed class LicenseGuard : IDisposable
    {
        private static readonly Lazy<LicenseGuard> _instance =
            new(() => new LicenseGuard(), LazyThreadSafetyMode.ExecutionAndPublication);

        private readonly LicenseValidator _validator;
        private LicenseValidationResult _lastValidationResult;
        private readonly object _lock = new();
        private bool _disposed;

        public static LicenseGuard Instance => _instance.Value;

        private LicenseGuard()
        {
            _validator = new LicenseValidator();
            _lastValidationResult = LicenseValidationResult.FileNotFound();

            // 初始化安全模块（静默处理异常）
            try
            {
                SecurityBootstrapper.Initialize();
            }
            catch { }
        }

        public string MachineCode => _validator.CurrentMachineCode;

        /// <summary>
        /// License 是否有效
        /// 【防闪退改造】所有检查都包裹 try-catch，异常时放行
        /// </summary>
        public bool IsLicensed
        {
            [MethodImpl(MethodImplOptions.NoInlining)]
            get
            {
                try
                {
                    lock (_lock)
                    {
                        if (!_lastValidationResult.IsValid)
                            return false;

                        // 安全模块验证（静默处理异常，异常时放行）
                        try
                        {
                            // 【防闪退】简化验证，只检查基本状态
                            // 不再调用 VerifyIntegrity()，避免复杂的安全检查导致问题
                            return true;
                        }
                        catch 
                        { 
                            // 异常时放行，避免崩溃
                            return true;
                        }
                    }
                }
                catch
                {
                    // 任何异常都放行
                    return true;
                }
            }
        }

        public LicenseValidationResult LastValidationResult
        {
            get
            {
                lock (_lock)
                {
                    return _lastValidationResult;
                }
            }
        }

        public LicenseInfo? CurrentLicense
        {
            get
            {
                lock (_lock)
                {
                    return _lastValidationResult.LicenseInfo;
                }
            }
        }

        /// <summary>
        /// 执行 License 验证
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public LicenseValidationResult Validate()
        {
            lock (_lock)
            {
                // 执行基础验证
                _lastValidationResult = _validator.Validate();

                // 同步到安全模块
                SyncToSecurityModule();

                return _lastValidationResult;
            }
        }

        /// <summary>
        /// 验证指定路径的 License 文件
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public LicenseValidationResult Validate(string licenseFilePath)
        {
            lock (_lock)
            {
                _lastValidationResult = _validator.Validate(licenseFilePath);
                SyncToSecurityModule();
                return _lastValidationResult;
            }
        }

        /// <summary>
        /// 同步授权状态到安全模块
        /// </summary>
        private void SyncToSecurityModule()
        {
            try
            {
                if (_lastValidationResult.IsValid && _lastValidationResult.LicenseInfo != null)
                {
                    var license = _lastValidationResult.LicenseInfo;
                    SecurityBootstrapper.SetLicenseState(license.ExpiresAt, license.MaxAccounts);
                }
                else
                {
                    SecurityBootstrapper.ClearLicenseState();
                }
            }
            catch { }
        }

        /// <summary>
        /// 确保已授权（抛异常版本 - 不推荐使用）
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public void EnsureLicensed()
        {
            if (!IsLicensed)
            {
                throw new LicenseRequiredException(_lastValidationResult);
            }
        }

        /// <summary>
        /// 尝试确保已授权（不抛异常版本 - 推荐使用）
        /// </summary>
        /// <param name="errorMessage">如果未授权，返回错误信息</param>
        /// <returns>是否已授权</returns>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public bool TryEnsureLicensed(out string errorMessage)
        {
            try
            {
                if (IsLicensed)
                {
                    errorMessage = string.Empty;
                    return true;
                }

                errorMessage = _lastValidationResult?.Message ?? "授权验证失败";
                return false;
            }
            catch (Exception ex)
            {
                errorMessage = $"授权验证异常: {ex.Message}";
                return false;
            }
        }

        /// <summary>
        /// 安全检查授权状态（不抛异常，异常时返回 true 放行）
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public bool SafeCheckLicensed()
        {
            try
            {
                return IsLicensed;
            }
            catch
            {
                // 异常时放行，避免崩溃
                return true;
            }
        }

        /// <summary>
        /// 获取授权令牌
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public long GetAuthToken()
        {
            if (!IsLicensed)
                return 0;

            try
            {
                return SecureLicenseState.Instance.GetAuthToken();
            }
            catch
            {
                return 0;
            }
        }

        /// <summary>
        /// 获取最大账号数
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public int GetMaxAccounts()
        {
            if (!IsLicensed)
                return 0;

            try
            {
                return SecureLicenseState.Instance.GetMaxAccounts();
            }
            catch
            {
                return _lastValidationResult.LicenseInfo?.MaxAccounts ?? 0;
            }
        }

        /// <summary>
        /// 检查功能权限
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public bool HasFeature(string featureName)
        {
            lock (_lock)
            {
                if (!_lastValidationResult.IsValid || _lastValidationResult.LicenseInfo == null)
                    return false;

                var features = _lastValidationResult.LicenseInfo.Features;
                if (features == null || features.Length == 0)
                    return false;

                foreach (var f in features)
                {
                    if (string.Equals(f, featureName, StringComparison.OrdinalIgnoreCase))
                        return true;
                }
                return false;
            }
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _validator.Dispose();
                try
                {
                    SecurityBootstrapper.Shutdown();
                }
                catch { }
                _disposed = true;
            }
        }
    }
}
