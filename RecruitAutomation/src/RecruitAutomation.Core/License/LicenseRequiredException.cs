using System;

namespace RecruitAutomation.Core.License
{
    /// <summary>
    /// License 授权异常
    /// 当未授权用户尝试访问受保护功能时抛出
    /// </summary>
    public sealed class LicenseRequiredException : Exception
    {
        /// <summary>
        /// 验证结果
        /// </summary>
        public LicenseValidationResult ValidationResult { get; }

        public LicenseRequiredException(LicenseValidationResult result)
            : base(result.Message)
        {
            ValidationResult = result;
        }

        public LicenseRequiredException(LicenseValidationResult result, string message)
            : base(message)
        {
            ValidationResult = result;
        }
    }
}
