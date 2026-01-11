using System;
using System.Security.Cryptography;
using System.Text;

namespace LicenseGenerator.Core.Crypto
{
    /// <summary>
    /// RSA 签名服务
    /// 使用私钥生成数字签名
    /// </summary>
    public sealed class RsaSigningService : IDisposable
    {
        private readonly RSA _rsa;
        private bool _disposed;

        /// <summary>
        /// 使用私钥初始化
        /// </summary>
        /// <param name="privateKeyXml">XML 格式的私钥</param>
        public RsaSigningService(string privateKeyXml)
        {
            _rsa = RSA.Create();
            _rsa.FromXmlString(privateKeyXml);
        }

        /// <summary>
        /// 对数据进行签名
        /// </summary>
        /// <param name="data">要签名的数据</param>
        /// <returns>Base64 编码的签名</returns>
        public string Sign(string data)
        {
            ThrowIfDisposed();

            var dataBytes = Encoding.UTF8.GetBytes(data);
            var signatureBytes = _rsa.SignData(
                dataBytes,
                HashAlgorithmName.SHA256,
                RSASignaturePadding.Pkcs1);

            return Convert.ToBase64String(signatureBytes);
        }

        /// <summary>
        /// 验证签名（用于测试）
        /// </summary>
        public bool Verify(string data, string signatureBase64)
        {
            ThrowIfDisposed();

            try
            {
                var dataBytes = Encoding.UTF8.GetBytes(data);
                var signatureBytes = Convert.FromBase64String(signatureBase64);

                return _rsa.VerifyData(
                    dataBytes,
                    signatureBytes,
                    HashAlgorithmName.SHA256,
                    RSASignaturePadding.Pkcs1);
            }
            catch
            {
                return false;
            }
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(RsaSigningService));
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _rsa.Dispose();
                _disposed = true;
            }
        }
    }
}
