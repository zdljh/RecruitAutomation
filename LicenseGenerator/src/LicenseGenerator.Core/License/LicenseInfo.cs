using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace LicenseGenerator.Core.License
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
    /// License 数据结构（与主程序共享定义）
    /// </summary>
    public sealed class LicenseInfo
    {
        [JsonPropertyName("version")]
        public string Version { get; set; } = "1.0";

        [JsonPropertyName("machineCode")]
        public string MachineCode { get; set; } = string.Empty;

        [JsonPropertyName("licenseType")]
        public LicenseType LicenseType { get; set; } = LicenseType.Trial;

        [JsonPropertyName("licenseTo")]
        public string LicenseTo { get; set; } = string.Empty;

        [JsonPropertyName("issuedAt")]
        public DateTime IssuedAt { get; set; }

        [JsonPropertyName("expiresAt")]
        public DateTime ExpiresAt { get; set; }

        [JsonPropertyName("features")]
        public string[] Features { get; set; } = Array.Empty<string>();

        [JsonPropertyName("maxAccounts")]
        public int MaxAccounts { get; set; } = 1;

        [JsonPropertyName("signature")]
        public string Signature { get; set; } = string.Empty;

        /// <summary>
        /// 获取用于签名的数据
        /// </summary>
        public string GetSigningData()
        {
            return $"{Version}|{MachineCode}|{(int)LicenseType}|{LicenseTo}|" +
                   $"{IssuedAt:O}|{ExpiresAt:O}|{string.Join(",", Features)}|{MaxAccounts}";
        }

        /// <summary>
        /// 序列化为 JSON 字符串
        /// </summary>
        public string ToJson()
        {
            return JsonSerializer.Serialize(this, new JsonSerializerOptions
            {
                WriteIndented = true
            });
        }

        /// <summary>
        /// 序列化为紧凑 JSON（无缩进）
        /// </summary>
        public string ToCompactJson()
        {
            return JsonSerializer.Serialize(this);
        }
    }
}
