using System;
using System.Text.Json.Serialization;

namespace RecruitAutomation.Core.License
{
    /// <summary>
    /// License 授权类型
    /// </summary>
    public enum LicenseType
    {
        /// <summary>试用版</summary>
        Trial = 0,
        /// <summary>专业版</summary>
        Professional = 1,
        /// <summary>企业版</summary>
        Enterprise = 2
    }

    /// <summary>
    /// License 数据结构
    /// </summary>
    public sealed class LicenseInfo
    {
        /// <summary>
        /// License 格式版本
        /// </summary>
        [JsonPropertyName("version")]
        public string Version { get; set; } = "1.0";

        /// <summary>
        /// 绑定的机器码
        /// </summary>
        [JsonPropertyName("machineCode")]
        public string MachineCode { get; set; } = string.Empty;

        /// <summary>
        /// 授权类型
        /// </summary>
        [JsonPropertyName("licenseType")]
        public LicenseType LicenseType { get; set; } = LicenseType.Trial;

        /// <summary>
        /// 授权给（公司/个人名称）
        /// </summary>
        [JsonPropertyName("licenseTo")]
        public string LicenseTo { get; set; } = string.Empty;

        /// <summary>
        /// 颁发时间 (UTC)
        /// </summary>
        [JsonPropertyName("issuedAt")]
        public DateTime IssuedAt { get; set; }

        /// <summary>
        /// 过期时间 (UTC)
        /// </summary>
        [JsonPropertyName("expiresAt")]
        public DateTime ExpiresAt { get; set; }

        /// <summary>
        /// 允许的功能列表
        /// </summary>
        [JsonPropertyName("features")]
        public string[] Features { get; set; } = Array.Empty<string>();

        /// <summary>
        /// 最大账号数量限制
        /// </summary>
        [JsonPropertyName("maxAccounts")]
        public int MaxAccounts { get; set; } = 1;

        /// <summary>
        /// RSA 数字签名 (Base64)
        /// </summary>
        [JsonPropertyName("signature")]
        public string Signature { get; set; } = string.Empty;

        /// <summary>
        /// 获取用于签名的数据（不含签名字段本身）
        /// </summary>
        public string GetSigningData()
        {
            return $"{Version}|{MachineCode}|{(int)LicenseType}|{LicenseTo}|" +
                   $"{IssuedAt:O}|{ExpiresAt:O}|{string.Join(",", Features)}|{MaxAccounts}";
        }
    }
}
