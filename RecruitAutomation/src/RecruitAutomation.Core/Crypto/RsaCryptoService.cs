using System;
using System.Security.Cryptography;
using System.Text;

namespace RecruitAutomation.Core.Crypto
{
    /// <summary>
    /// RSA 加密服务
    /// 主程序仅使用公钥验证签名
    /// </summary>
    public sealed class RsaCryptoService : IDisposable
    {
        private readonly RSA _rsa;
        private bool _disposed;

        /// <summary>
        /// 使用公钥初始化（用于验证签名）
        /// </summary>
        /// <param name="publicKeyXml">XML 格式的公钥</param>
        public RsaCryptoService(string publicKeyXml)
        {
            _rsa = RSA.Create();
            _rsa.FromXmlString(publicKeyXml);
        }

        /// <summary>
        /// 验证数字签名
        /// </summary>
        /// <param name="data">原始数据</param>
        /// <param name="signatureBase64">Base64 编码的签名</param>
        /// <returns>签名是否有效</returns>
        public bool VerifySignature(string data, string signatureBase64)
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
                throw new ObjectDisposedException(nameof(RsaCryptoService));
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
