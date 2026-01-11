using System;
using System.IO;
using System.Security.Cryptography;

namespace LicenseGenerator.Core.Crypto
{
    /// <summary>
    /// RSA 密钥对生成器
    /// </summary>
    public static class RsaKeyPairGenerator
    {
        /// <summary>
        /// 生成 RSA 密钥对
        /// </summary>
        /// <param name="keySize">密钥长度（推荐 2048 或 4096）</param>
        /// <returns>公钥和私钥的 XML 字符串</returns>
        public static (string PublicKey, string PrivateKey) GenerateKeyPair(int keySize = 2048)
        {
            using var rsa = RSA.Create(keySize);
            var publicKey = rsa.ToXmlString(false);  // 仅公钥
            var privateKey = rsa.ToXmlString(true);  // 包含私钥
            return (publicKey, privateKey);
        }

        /// <summary>
        /// 生成密钥对并保存到文件
        /// </summary>
        public static void GenerateAndSaveKeyPair(string directory, int keySize = 2048)
        {
            var (publicKey, privateKey) = GenerateKeyPair(keySize);

            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(Path.Combine(directory, "public.key"), publicKey);
            File.WriteAllText(Path.Combine(directory, "private.key"), privateKey);
        }
    }
}
