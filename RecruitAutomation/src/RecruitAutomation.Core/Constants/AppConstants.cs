using System;
using System.IO;

namespace RecruitAutomation.Core.Constants
{
    /// <summary>
    /// 应用程序常量
    /// </summary>
    public static class AppConstants
    {
        /// <summary>
        /// 应用程序名称
        /// </summary>
        public const string AppName = "RecruitAutomation";

        /// <summary>
        /// License 文件名
        /// </summary>
        public const string LicenseFileName = "license.lic";

        /// <summary>
        /// 数据存储根目录
        /// </summary>
        public static string DataRootPath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            AppName);

        /// <summary>
        /// License 文件完整路径
        /// </summary>
        public static string LicenseFilePath => Path.Combine(DataRootPath, LicenseFileName);

        /// <summary>
        /// RSA 公钥（XML 格式）
        /// 注意：此公钥仅用于验证签名，无法生成有效签名
        /// 私钥仅存在于 License 生成器中
        /// </summary>
        public const string RsaPublicKey = @"<RSAKeyValue><Modulus>w7MjS1hZzJKUDZdb1xhYVJaQSwrMXvmY6IsPvp+qnYYm9IekPT+ekpQR3XcPdQw2n6ZXfZaLI3Y12FKkGdvuSowMjMvkQkj/FianTTuyYeWFxrq9qucnwvyIzu9eA7f4dTk+EOL/lWV+95uLW0UjkOoWNSO2ONopJLo7iN3eSxD0o2YcFXWNTFumqkVuhauy/KDANnyhJc3t3P34O+wW1ukQc5JtJ+N5pd2Bmdh2u8cfi4koHvas7X1WOpYCY8Ke87CMvHm3WE9k3XrNLUokYhZo8wnFvK3fsxQMmztu47lEWQRfmJKRaqzq+QqVBuG1yh/PqQTvnbxvb+V+EVSeWQ==</Modulus><Exponent>AQAB</Exponent></RSAKeyValue>";
    }
}
