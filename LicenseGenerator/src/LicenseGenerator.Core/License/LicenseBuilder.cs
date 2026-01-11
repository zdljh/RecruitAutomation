using System;
using System.IO;
using System.Text.Json;
using LicenseGenerator.Core.Crypto;

namespace LicenseGenerator.Core.License
{
    /// <summary>
    /// License 构建器
    /// 用于创建和签名 License 文件
    /// </summary>
    public sealed class LicenseBuilder : IDisposable
    {
        private readonly RsaSigningService _signingService;
        private bool _disposed;

        private string _machineCode = string.Empty;
        private LicenseType _licenseType = LicenseType.Trial;
        private string _licenseTo = string.Empty;
        private DateTime _issuedAt = DateTime.UtcNow;
        private DateTime _expiresAt = DateTime.UtcNow.AddDays(30);
        private string[] _features = Array.Empty<string>();
        private int _maxAccounts = 1;

        /// <summary>
        /// 使用私钥初始化
        /// </summary>
        /// <param name="privateKeyXml">XML 格式的私钥</param>
        public LicenseBuilder(string privateKeyXml)
        {
            _signingService = new RsaSigningService(privateKeyXml);
        }

        public LicenseBuilder SetMachineCode(string machineCode)
        {
            _machineCode = machineCode ?? throw new ArgumentNullException(nameof(machineCode));
            return this;
        }

        public LicenseBuilder SetLicenseType(LicenseType type)
        {
            _licenseType = type;
            return this;
        }

        public LicenseBuilder SetLicenseTo(string licenseTo)
        {
            _licenseTo = licenseTo ?? string.Empty;
            return this;
        }

        public LicenseBuilder SetIssuedAt(DateTime issuedAt)
        {
            _issuedAt = issuedAt.ToUniversalTime();
            return this;
        }

        public LicenseBuilder SetExpiresAt(DateTime expiresAt)
        {
            _expiresAt = expiresAt.ToUniversalTime();
            return this;
        }

        public LicenseBuilder SetValidDays(int days)
        {
            _expiresAt = DateTime.UtcNow.AddDays(days);
            return this;
        }

        public LicenseBuilder SetFeatures(params string[] features)
        {
            _features = features ?? Array.Empty<string>();
            return this;
        }

        public LicenseBuilder SetMaxAccounts(int maxAccounts)
        {
            _maxAccounts = Math.Max(1, maxAccounts);
            return this;
        }

        /// <summary>
        /// 构建 License 对象
        /// </summary>
        public LicenseInfo Build()
        {
            ThrowIfDisposed();

            if (string.IsNullOrWhiteSpace(_machineCode))
                throw new InvalidOperationException("机器码不能为空");

            var license = new LicenseInfo
            {
                Version = "1.0",
                MachineCode = _machineCode,
                LicenseType = _licenseType,
                LicenseTo = _licenseTo,
                IssuedAt = _issuedAt,
                ExpiresAt = _expiresAt,
                Features = _features,
                MaxAccounts = _maxAccounts
            };

            // 生成签名
            var signingData = license.GetSigningData();
            license.Signature = _signingService.Sign(signingData);

            return license;
        }

        /// <summary>
        /// 构建并保存到文件
        /// </summary>
        public LicenseInfo BuildAndSave(string filePath)
        {
            var license = Build();
            var json = JsonSerializer.Serialize(license, new JsonSerializerOptions
            {
                WriteIndented = true
            });

            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(filePath, json);
            return license;
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(LicenseBuilder));
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _signingService.Dispose();
                _disposed = true;
            }
        }
    }
}
