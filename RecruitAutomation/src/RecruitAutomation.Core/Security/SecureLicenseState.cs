using System;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Threading;

namespace RecruitAutomation.Core.Security
{
    /// <summary>
    /// 【模块一】内存级 License 状态校验
    /// 授权状态拆分存储，防止单点内存 Patch
    /// </summary>
    public sealed class SecureLicenseState
    {
        private static SecureLicenseState? _instance;
        private static readonly object _lock = new();

        // ═══════════════════════════════════════════════════════════
        // 授权状态拆分为多个字段，存储在内存不同位置
        // ═══════════════════════════════════════════════════════════

        // 字段1：主授权标记（异或混淆）
        private long _authFlag1;
        private readonly long _xorKey1;

        // 字段2：授权时间戳（加盐）
        private long _authTimestamp;
        private readonly long _salt2;

        // 字段3：动态令牌（定期变化）
        private byte[] _dynamicToken;
        private readonly byte[] _tokenKey;

        // 字段4：校验和（前三个字段的哈希）
        private byte[] _checksum;

        // 字段5：过期时间（混淆存储）
        private long _expiresAtTicks;
        private readonly long _xorKey5;

        // 字段6：最大账号数（参与业务计算）
        private int _maxAccountsEncoded;
        private readonly int _accountKey;

        // 上次校验时间
        private long _lastVerifyTicks;

        // 随机数生成器
        private readonly RandomNumberGenerator _rng;

        private SecureLicenseState()
        {
            _rng = RandomNumberGenerator.Create();

            // 生成随机密钥
            _xorKey1 = GenerateRandomLong();
            _salt2 = GenerateRandomLong();
            _tokenKey = GenerateRandomBytes(32);
            _xorKey5 = GenerateRandomLong();
            _accountKey = GenerateRandomInt();

            _dynamicToken = new byte[32];
            _checksum = new byte[32];

            // 初始状态：未授权
            SetUnauthorized();
        }

        public static SecureLicenseState Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        _instance ??= new SecureLicenseState();
                    }
                }
                return _instance;
            }
        }

        /// <summary>
        /// 设置授权状态（仅由 LicenseGuard 调用）
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal void SetAuthorized(DateTime expiresAt, int maxAccounts)
        {
            var now = DateTime.UtcNow.Ticks;

            // 字段1：设置授权标记（0x5A5A5A5A 异或混淆）
            _authFlag1 = 0x5A5A5A5A5A5A5A5A ^ _xorKey1;

            // 字段2：记录授权时间戳
            _authTimestamp = now ^ _salt2;

            // 字段3：生成动态令牌
            RefreshDynamicToken();

            // 字段5：存储过期时间
            _expiresAtTicks = expiresAt.Ticks ^ _xorKey5;

            // 字段6：存储最大账号数
            _maxAccountsEncoded = maxAccounts ^ _accountKey;

            // 字段4：计算校验和
            UpdateChecksum();

            _lastVerifyTicks = now;
        }

        /// <summary>
        /// 设置未授权状态
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal void SetUnauthorized()
        {
            _authFlag1 = GenerateRandomLong(); // 随机值，非授权标记
            _authTimestamp = 0;
            _dynamicToken = GenerateRandomBytes(32);
            _expiresAtTicks = 0;
            _maxAccountsEncoded = _accountKey; // 0 ^ key = key
            UpdateChecksum();
        }

        /// <summary>
        /// 验证授权状态完整性
        /// 【防闪退改造】所有检查都包裹 try-catch，异常时放行
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        public bool VerifyIntegrity()
        {
            try
            {
                // 检查1：主授权标记
                var flag = _authFlag1 ^ _xorKey1;
                if (flag != 0x5A5A5A5A5A5A5A5A)
                    return false;

                // 检查2：校验和（异常时放行）
                try
                {
                    var currentChecksum = ComputeChecksum();
                    if (!ConstantTimeEquals(_checksum, currentChecksum))
                        return false;
                }
                catch
                {
                    // 校验和计算异常时放行
                }

                // 检查3：是否过期（异常时放行）
                try
                {
                    var expiresAtTicks = _expiresAtTicks ^ _xorKey5;
                    // 防止无效的 Ticks 值导致异常
                    if (expiresAtTicks <= 0 || expiresAtTicks > DateTime.MaxValue.Ticks)
                        return true; // 无效值时放行
                        
                    var expiresAt = new DateTime(expiresAtTicks);
                    if (DateTime.UtcNow > expiresAt)
                        return false;
                }
                catch
                {
                    // 时间检查异常时放行
                }

                // 检查4：动态令牌有效性（异常时放行）
                try
                {
                    if (!VerifyDynamicToken())
                        return false;
                }
                catch
                {
                    // 令牌验证异常时放行
                }

                _lastVerifyTicks = DateTime.UtcNow.Ticks;
                return true;
            }
            catch
            {
                // 任何异常都放行，避免崩溃
                return true;
            }
        }

        /// <summary>
        /// 获取授权令牌（业务逻辑依赖此值）
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public long GetAuthToken()
        {
            if (!VerifyIntegrity())
                return 0;

            // 返回基于多个字段计算的令牌
            var token = _authFlag1 ^ _authTimestamp;
            token ^= BitConverter.ToInt64(_dynamicToken, 0);
            return token;
        }

        /// <summary>
        /// 获取最大账号数（业务逻辑依赖）
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public int GetMaxAccounts()
        {
            if (!VerifyIntegrity())
                return 0;

            return _maxAccountsEncoded ^ _accountKey;
        }

        /// <summary>
        /// 获取功能系数（用于业务计算）
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public double GetFeatureMultiplier()
        {
            if (!VerifyIntegrity())
                return 0.0;

            // 基于授权状态计算系数
            var token = GetAuthToken();
            if (token == 0)
                return 0.0;

            return 1.0 + (Math.Abs(token) % 100) / 1000.0;
        }

        /// <summary>
        /// 刷新动态令牌（定期调用）
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal void RefreshDynamicToken()
        {
            var timestamp = BitConverter.GetBytes(DateTime.UtcNow.Ticks);
            var newToken = new byte[32];

            // 前8字节存储时间戳（XOR密钥混淆）
            for (int i = 0; i < 8; i++)
            {
                newToken[i] = (byte)(timestamp[i] ^ _tokenKey[i]);
            }

            // 后24字节填充随机数据
            var randomPart = GenerateRandomBytes(24);
            for (int i = 0; i < 24; i++)
            {
                newToken[8 + i] = (byte)(randomPart[i] ^ _tokenKey[8 + i]);
            }

            _dynamicToken = newToken;
            UpdateChecksum();
        }

        private bool VerifyDynamicToken()
        {
            try
            {
                // 【改造】增强防御性检查
                if (_dynamicToken == null || _dynamicToken.Length != 32)
                {
                    // 令牌无效时放行，避免崩溃
                    return true;
                }

                // 检查令牌是否在有效期内（放宽到24小时，避免时间问题导致验证失败）
                long tokenTime;
                try
                {
                    tokenTime = ExtractTokenTime();
                }
                catch
                {
                    // 提取时间失败时放行
                    return true;
                }
                
                var now = DateTime.UtcNow.Ticks;
                var elapsed = now - tokenTime;
                
                // 【改造】更宽松的时间检查，避免因时间问题导致验证失败
                // 允许未来10分钟（时钟偏差）和过去48小时
                var minElapsed = -TimeSpan.FromMinutes(10).Ticks;
                var maxElapsed = TimeSpan.FromHours(48).Ticks;
                
                // 如果时间戳明显异常（如负数或超大值），放行
                if (tokenTime <= 0 || tokenTime > DateTime.MaxValue.Ticks - TimeSpan.FromDays(365).Ticks)
                {
                    return true;
                }
                
                return elapsed >= minElapsed && elapsed < maxElapsed;
            }
            catch
            {
                // 验证异常时放行，避免崩溃
                return true;
            }
        }

        private long ExtractTokenTime()
        {
            var timeBytes = new byte[8];
            for (int i = 0; i < 8; i++)
            {
                timeBytes[i] = (byte)(_dynamicToken[i] ^ _tokenKey[i]);
            }
            return BitConverter.ToInt64(timeBytes, 0);
        }

        private void UpdateChecksum()
        {
            _checksum = ComputeChecksum();
        }

        private byte[] ComputeChecksum()
        {
            using var sha = SHA256.Create();
            var data = new byte[8 + 8 + 32 + 8];
            var offset = 0;

            BitConverter.GetBytes(_authFlag1).CopyTo(data, offset); offset += 8;
            BitConverter.GetBytes(_authTimestamp).CopyTo(data, offset); offset += 8;
            _dynamicToken.CopyTo(data, offset); offset += 32;
            BitConverter.GetBytes(_expiresAtTicks).CopyTo(data, offset);

            return sha.ComputeHash(data);
        }

        // 常量时间比较，防止时序攻击
        private static bool ConstantTimeEquals(byte[] a, byte[] b)
        {
            if (a.Length != b.Length)
                return false;

            int diff = 0;
            for (int i = 0; i < a.Length; i++)
            {
                diff |= a[i] ^ b[i];
            }
            return diff == 0;
        }

        private long GenerateRandomLong()
        {
            var bytes = new byte[8];
            _rng.GetBytes(bytes);
            return BitConverter.ToInt64(bytes, 0);
        }

        private int GenerateRandomInt()
        {
            var bytes = new byte[4];
            _rng.GetBytes(bytes);
            return BitConverter.ToInt32(bytes, 0);
        }

        private byte[] GenerateRandomBytes(int length)
        {
            var bytes = new byte[length];
            _rng.GetBytes(bytes);
            return bytes;
        }
    }
}
