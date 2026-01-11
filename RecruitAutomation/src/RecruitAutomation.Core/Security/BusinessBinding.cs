using System;
using System.Runtime.CompilerServices;

namespace RecruitAutomation.Core.Security
{
    /// <summary>
    /// 【模块五】授权状态与业务深度绑定
    /// 授权状态参与业务计算，而非简单的 if 判断
    /// </summary>
    public static class BusinessBinding
    {
        // ═══════════════════════════════════════════════════════════
        // 示例1：自动回复次数限制
        // 次数 = 基础值 × 授权系数
        // ═══════════════════════════════════════════════════════════

        /// <summary>
        /// 计算允许的自动回复次数
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static int CalculateAutoReplyLimit(int baseLimit)
        {
            // 获取授权令牌
            var token = SecureLicenseState.Instance.GetAuthToken();

            // 未授权时令牌为 0，计算结果也为 0
            if (token == 0)
                return 0;

            // 授权系数参与计算
            var multiplier = SecureLicenseState.Instance.GetFeatureMultiplier();

            // 基于令牌计算附加值
            var bonus = (int)(Math.Abs(token) % 50);

            // 最终值 = 基础值 × 系数 + 附加值
            return (int)(baseLimit * multiplier) + bonus;
        }

        /// <summary>
        /// 验证自动回复操作
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static bool ValidateAutoReplyOperation(int currentCount, int baseLimit)
        {
            var limit = CalculateAutoReplyLimit(baseLimit);

            // 未授权时 limit = 0，任何操作都不允许
            return currentCount < limit;
        }

        // ═══════════════════════════════════════════════════════════
        // 示例2：浏览器实例数量限制
        // 数量 = 授权的最大账号数
        // ═══════════════════════════════════════════════════════════

        /// <summary>
        /// 获取允许的浏览器实例数量
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static int GetAllowedBrowserInstances()
        {
            // 直接依赖授权状态中的最大账号数
            var maxAccounts = SecureLicenseState.Instance.GetMaxAccounts();

            // 未授权时返回 0
            if (maxAccounts <= 0)
                return 0;

            // 验证完整性
            if (!SecureLicenseState.Instance.VerifyIntegrity())
                return 0;

            return maxAccounts;
        }

        /// <summary>
        /// 验证是否可以创建新浏览器实例
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static bool CanCreateBrowserInstance(int currentCount)
        {
            var allowed = GetAllowedBrowserInstances();
            return currentCount < allowed;
        }

        // ═══════════════════════════════════════════════════════════
        // 示例3：随机延迟参数
        // 延迟 = 基础延迟 × 授权系数
        // ═══════════════════════════════════════════════════════════

        /// <summary>
        /// 计算操作延迟（毫秒）
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static int CalculateOperationDelay(int baseDelayMs)
        {
            var token = SecureLicenseState.Instance.GetAuthToken();

            // 未授权时返回极大延迟，使功能不可用
            if (token == 0)
                return int.MaxValue;

            var multiplier = SecureLicenseState.Instance.GetFeatureMultiplier();

            // 基于令牌生成随机因子
            var randomFactor = 0.8 + (Math.Abs(token) % 40) / 100.0;

            return (int)(baseDelayMs * multiplier * randomFactor);
        }

        /// <summary>
        /// 获取带授权验证的随机延迟
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static int GetSecureRandomDelay(int minMs, int maxMs)
        {
            var token = SecureLicenseState.Instance.GetAuthToken();

            if (token == 0)
                return int.MaxValue;

            // 使用令牌作为随机种子的一部分
            var seed = (int)(token ^ DateTime.UtcNow.Ticks);
            var random = new Random(seed);

            var baseDelay = random.Next(minMs, maxMs);
            return CalculateOperationDelay(baseDelay);
        }

        // ═══════════════════════════════════════════════════════════
        // 示例4：数据加密密钥派生
        // 密钥 = Hash(授权令牌 + 固定盐)
        // ═══════════════════════════════════════════════════════════

        /// <summary>
        /// 派生数据加密密钥
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static byte[] DeriveEncryptionKey()
        {
            var token = SecureLicenseState.Instance.GetAuthToken();

            // 未授权时返回空密钥
            if (token == 0)
                return Array.Empty<byte>();

            using var sha = System.Security.Cryptography.SHA256.Create();

            // 组合令牌和固定盐
            var tokenBytes = BitConverter.GetBytes(token);
            var salt = new byte[] { 0x52, 0x41, 0x5F, 0x4B, 0x45, 0x59, 0x5F, 0x56 }; // "RA_KEY_V"

            var combined = new byte[tokenBytes.Length + salt.Length];
            tokenBytes.CopyTo(combined, 0);
            salt.CopyTo(combined, tokenBytes.Length);

            return sha.ComputeHash(combined);
        }

        /// <summary>
        /// 验证数据操作权限
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static bool ValidateDataOperation()
        {
            var key = DeriveEncryptionKey();

            // 未授权时密钥为空
            return key.Length > 0;
        }

        // ═══════════════════════════════════════════════════════════
        // 示例5：功能解锁计算
        // 功能码 = 授权令牌 XOR 功能标识
        // ═══════════════════════════════════════════════════════════

        /// <summary>
        /// 计算功能解锁码
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static long CalculateFeatureUnlockCode(string featureId)
        {
            var token = SecureLicenseState.Instance.GetAuthToken();

            if (token == 0)
                return 0;

            // 计算功能标识的哈希
            var featureHash = 0L;
            foreach (var c in featureId)
            {
                featureHash = featureHash * 31 + c;
            }

            // 解锁码 = 令牌 XOR 功能哈希
            return token ^ featureHash;
        }

        /// <summary>
        /// 验证功能是否解锁
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static bool IsFeatureUnlocked(string featureId)
        {
            var unlockCode = CalculateFeatureUnlockCode(featureId);

            // 未授权时解锁码为 0
            if (unlockCode == 0)
                return false;

            // 验证解锁码有效性（非零且通过完整性检查）
            return SecureLicenseState.Instance.VerifyIntegrity();
        }
    }
}
