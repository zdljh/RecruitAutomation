using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CefSharp;

namespace RecruitAutomation.Browser
{
    /// <summary>
    /// Cookie 访问器
    /// 用于异步获取所有 Cookie
    /// </summary>
    internal sealed class CookieVisitor : ICookieVisitor
    {
        private readonly List<Cookie> _cookies = new();
        private readonly TaskCompletionSource<List<Cookie>> _tcs = new();

        public bool Visit(Cookie cookie, int count, int total, ref bool deleteCookie)
        {
            _cookies.Add(cookie);
            
            // 最后一个 Cookie
            if (count == total - 1)
            {
                _tcs.TrySetResult(_cookies);
            }
            
            deleteCookie = false;
            return true;
        }

        public void Dispose()
        {
            // 如果没有 Cookie，也要完成任务
            _tcs.TrySetResult(_cookies);
        }

        public Task<List<Cookie>> GetCookiesAsync()
        {
            return _tcs.Task;
        }
    }
}
