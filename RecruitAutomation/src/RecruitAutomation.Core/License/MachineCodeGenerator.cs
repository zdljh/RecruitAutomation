using System;
using System.Management;
using System.Security.Cryptography;
using System.Text;

namespace RecruitAutomation.Core.License
{
    /// <summary>
    /// 机器码生成器
    /// 基于 CPU ID + 主板序列号 + 硬盘序列号 生成唯一机器指纹
    /// </summary>
    public static class MachineCodeGenerator
    {
        /// <summary>
        /// 生成机器码（格式：XXXX-XXXX-XXXX-XXXX）
        /// </summary>
        public static string Generate()
        {
            var rawData = GetCpuId() + GetMotherboardId() + GetDiskId();
            var hash = ComputeSha256Hash(rawData);
            return FormatMachineCode(hash);
        }

        /// <summary>
        /// 获取 CPU ID
        /// </summary>
        private static string GetCpuId()
        {
            try
            {
                using var searcher = new ManagementObjectSearcher("SELECT ProcessorId FROM Win32_Processor");
                foreach (var obj in searcher.Get())
                {
                    var cpuId = obj["ProcessorId"]?.ToString();
                    if (!string.IsNullOrEmpty(cpuId))
                        return cpuId;
                }
            }
            catch
            {
                // 忽略异常，返回备用值
            }
            return "CPU_FALLBACK_ID";
        }

        /// <summary>
        /// 获取主板序列号
        /// </summary>
        private static string GetMotherboardId()
        {
            try
            {
                using var searcher = new ManagementObjectSearcher("SELECT SerialNumber FROM Win32_BaseBoard");
                foreach (var obj in searcher.Get())
                {
                    var serial = obj["SerialNumber"]?.ToString();
                    if (!string.IsNullOrEmpty(serial) && serial != "To be filled by O.E.M.")
                        return serial;
                }
            }
            catch
            {
                // 忽略异常
            }
            return "MB_FALLBACK_ID";
        }

        /// <summary>
        /// 获取系统盘序列号
        /// </summary>
        private static string GetDiskId()
        {
            try
            {
                using var searcher = new ManagementObjectSearcher(
                    "SELECT SerialNumber FROM Win32_DiskDrive WHERE Index=0");
                foreach (var obj in searcher.Get())
                {
                    var serial = obj["SerialNumber"]?.ToString()?.Trim();
                    if (!string.IsNullOrEmpty(serial))
                        return serial;
                }
            }
            catch
            {
                // 忽略异常
            }
            return "DISK_FALLBACK_ID";
        }

        /// <summary>
        /// 计算 SHA256 哈希
        /// </summary>
        private static string ComputeSha256Hash(string input)
        {
            using var sha256 = SHA256.Create();
            var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(input));
            var sb = new StringBuilder();
            foreach (var b in bytes)
            {
                sb.Append(b.ToString("X2"));
            }
            return sb.ToString();
        }

        /// <summary>
        /// 格式化机器码为 XXXX-XXXX-XXXX-XXXX 格式
        /// </summary>
        private static string FormatMachineCode(string hash)
        {
            // 取哈希的前16个字符，分成4组
            var shortHash = hash.Substring(0, 16).ToUpperInvariant();
            return $"{shortHash.Substring(0, 4)}-{shortHash.Substring(4, 4)}-" +
                   $"{shortHash.Substring(8, 4)}-{shortHash.Substring(12, 4)}";
        }
    }
}
