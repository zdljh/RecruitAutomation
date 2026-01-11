using System;

namespace RecruitAutomation.Core.License
{
    /// <summary>
    /// License 校验结果状态
    /// </summary>
    public enum LicenseStatus
    {
        /// <summary>有效</summary>
        Valid,
        /// <summary>文件不存在</summary>
        FileNotFound,
        /// <summary>文件格式错误</summary>
        InvalidFormat,
        /// <summary>签名无效</summary>
        InvalidSignature,
        /// <summary>机器码不匹配</summary>
        MachineCodeMismatch,
        /// <summary>已过期</summary>
        Expired,
        /// <summary>尚未生效</summary>
        NotYetValid,
        /// <summary>未知错误</summary>
        UnknownError
    }

    /// <summary>
    /// License 校验结果
    /// </summary>
    public sealed class LicenseValidationResult
    {
        /// <summary>
        /// 是否有效
        /// </summary>
        public bool IsValid => Status == LicenseStatus.Valid;

        /// <summary>
        /// 校验状态
        /// </summary>
        public LicenseStatus Status { get; }

        /// <summary>
        /// 错误消息
        /// </summary>
        public string Message { get; }

        /// <summary>
        /// License 信息（仅当有效时有值）
        /// </summary>
        public LicenseInfo? LicenseInfo { get; }

        private LicenseValidationResult(LicenseStatus status, string message, LicenseInfo? info = null)
        {
            Status = status;
            Message = message;
            LicenseInfo = info;
        }

        public static LicenseValidationResult Success(LicenseInfo info)
            => new(LicenseStatus.Valid, "License 验证成功", info);

        public static LicenseValidationResult FileNotFound()
            => new(LicenseStatus.FileNotFound, "License 文件不存在");

        public static LicenseValidationResult InvalidFormat(string detail = "")
            => new(LicenseStatus.InvalidFormat, $"License 文件格式错误{(string.IsNullOrEmpty(detail) ? "" : ": " + detail)}");

        public static LicenseValidationResult InvalidSignature()
            => new(LicenseStatus.InvalidSignature, "License 签名无效，可能已被篡改");

        public static LicenseValidationResult MachineCodeMismatch(string expected, string actual)
            => new(LicenseStatus.MachineCodeMismatch, $"机器码不匹配，此 License 不适用于当前设备");

        public static LicenseValidationResult Expired(DateTime expiresAt)
            => new(LicenseStatus.Expired, $"License 已于 {expiresAt:yyyy-MM-dd HH:mm} 过期");

        public static LicenseValidationResult NotYetValid(DateTime issuedAt)
            => new(LicenseStatus.NotYetValid, $"License 尚未生效，生效时间: {issuedAt:yyyy-MM-dd HH:mm}");

        public static LicenseValidationResult Error(string message)
            => new(LicenseStatus.UnknownError, message);
    }
}
