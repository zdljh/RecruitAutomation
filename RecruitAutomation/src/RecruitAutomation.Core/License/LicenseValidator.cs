using System;
using System.IO;
using System.Text.Json;
using RecruitAutomation.Core.Constants;
using RecruitAutomation.Core.Crypto;

namespace RecruitAutomation.Core.License
{
    /// <summary>
    /// License 验证器
    /// 负责验证 License 文件的有效性
    /// </summary>
    public sealed class LicenseValidator : IDisposable
    {
        private readonly RsaCryptoService _cryptoService;
        private readonly string _currentMachineCode;
        private bool _disposed;

        public LicenseValidator()
        {
            _cryptoService = new RsaCryptoService(AppConstants.RsaPublicKey);
            _currentMachineCode = MachineCodeGenerator.Generate();
        }

        /// <summary>
        /// 获取当前机器码
        /// </summary>
        public string CurrentMachineCode => _currentMachineCode;

        /// <summary>
        /// 验证 License 文件
        /// </summary>
        public LicenseValidationResult Validate()
        {
            return Validate(AppConstants.LicenseFilePath);
        }

        /// <summary>
        /// 验证指定路径的 License 文件
        /// </summary>
        public LicenseValidationResult Validate(string licenseFilePath)
        {
            ThrowIfDisposed();

            // 1. 检查文件是否存在
            if (!File.Exists(licenseFilePath))
            {
                return LicenseValidationResult.FileNotFound();
            }

            // 2. 读取并解析 License 文件
            LicenseInfo? licenseInfo;
            try
            {
                var json = File.ReadAllText(licenseFilePath);
                licenseInfo = JsonSerializer.Deserialize<LicenseInfo>(json);
                
                if (licenseInfo == null)
                {
                    return LicenseValidationResult.InvalidFormat("反序列化结果为空");
                }
            }
            catch (JsonException ex)
            {
                return LicenseValidationResult.InvalidFormat(ex.Message);
            }
            catch (Exception ex)
            {
                return LicenseValidationResult.Error($"读取 License 文件失败: {ex.Message}");
            }

            // 3. 验证数字签名（最重要的安全检查）
            var signingData = licenseInfo.GetSigningData();
            if (!_cryptoService.VerifySignature(signingData, licenseInfo.Signature))
            {
                return LicenseValidationResult.InvalidSignature();
            }

            // 4. 验证机器码
            if (!string.Equals(licenseInfo.MachineCode, _currentMachineCode, StringComparison.OrdinalIgnoreCase))
            {
                return LicenseValidationResult.MachineCodeMismatch(licenseInfo.MachineCode, _currentMachineCode);
            }

            // 5. 验证时间有效性
            var now = DateTime.UtcNow;
            
            if (now < licenseInfo.IssuedAt)
            {
                return LicenseValidationResult.NotYetValid(licenseInfo.IssuedAt);
            }

            if (now > licenseInfo.ExpiresAt)
            {
                return LicenseValidationResult.Expired(licenseInfo.ExpiresAt);
            }

            // 全部验证通过
            return LicenseValidationResult.Success(licenseInfo);
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(LicenseValidator));
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _cryptoService.Dispose();
                _disposed = true;
            }
        }
    }
}
