using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;

namespace RecruitAutomation.Core.Security
{
    /// <summary>
    /// 反调试检测（优化版）
    /// </summary>
    public sealed class AntiDebugger
    {
        private static AntiDebugger? _instance;
        private static readonly object _lock = new();

        private Timer? _checkTimer;
        private bool _isRunning;

        // 常见调试器进程名
        private static readonly string[] DebuggerProcesses = new[]
        {
            "dnspy", "x64dbg", "x32dbg", "ollydbg", "ida", "ida64",
            "windbg", "cheatengine", "megadumper"
        };

        private AntiDebugger() { }

        public static AntiDebugger Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        _instance ??= new AntiDebugger();
                    }
                }
                return _instance;
            }
        }

        #region P/Invoke

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool IsDebuggerPresent();

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CheckRemoteDebuggerPresent(IntPtr hProcess, out bool isDebuggerPresent);

        [DllImport("kernel32.dll")]
        private static extern IntPtr GetCurrentProcess();

        #endregion

        /// <summary>
        /// 启动反调试监控
        /// 【防闪退改造】禁用后台 Timer，避免误判导致授权被清除
        /// </summary>
        public void Start()
        {
            // 【防闪退】禁用反调试监控，避免误判导致授权被清除引发闪退
            // 在开发/调试环境下，反调试会导致程序无法正常运行
            _isRunning = false;
            
            // 原代码已注释
            // _checkTimer = new Timer(
            //     _ => PeriodicCheck(),
            //     null,
            //     TimeSpan.FromSeconds(10),
            //     TimeSpan.FromSeconds(30));
        }

        /// <summary>
        /// 停止监控
        /// </summary>
        public void Stop()
        {
            _isRunning = false;
            _checkTimer?.Dispose();
            _checkTimer = null;
        }

        /// <summary>
        /// 检测是否存在调试器
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public bool IsDebuggerDetected()
        {
            try
            {
                // 检测1：托管调试器
                if (Debugger.IsAttached)
                    return true;

                // 检测2：本机调试器
                if (CheckNativeDebugger())
                    return true;

                // 检测3：远程调试器
                if (CheckRemoteDebugger())
                    return true;

                // 检测4：调试器进程（可选，性能开销较大）
                // if (CheckDebuggerProcesses())
                //     return true;

                return false;
            }
            catch
            {
                // 检测异常时不判定为调试
                return false;
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private bool CheckNativeDebugger()
        {
            try
            {
                return IsDebuggerPresent();
            }
            catch
            {
                return false;
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private bool CheckRemoteDebugger()
        {
            try
            {
                if (CheckRemoteDebuggerPresent(GetCurrentProcess(), out bool isDebuggerPresent))
                {
                    return isDebuggerPresent;
                }
            }
            catch { }

            return false;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private bool CheckDebuggerProcesses()
        {
            try
            {
                var processes = Process.GetProcesses();
                foreach (var proc in processes)
                {
                    try
                    {
                        var name = proc.ProcessName.ToLowerInvariant();
                        if (DebuggerProcesses.Any(d => name.Contains(d)))
                        {
                            return true;
                        }
                    }
                    catch { }
                }
            }
            catch { }

            return false;
        }

        private void PeriodicCheck()
        {
            if (!_isRunning)
                return;

            try
            {
                if (IsDebuggerDetected())
                {
                    // 检测到调试器，清除授权状态
                    SecureLicenseState.Instance.SetUnauthorized();
                }
            }
            catch { }
        }
    }
}
