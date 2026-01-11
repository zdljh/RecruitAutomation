using System;
using System.Threading;
using System.Threading.Tasks;
using CefSharp;

namespace RecruitAutomation.Browser.HumanBehavior
{
    /// <summary>
    /// 人工行为模拟器（统一入口）
    /// 整合所有模拟器，提供简单易用的 API
    /// </summary>
    public class HumanBehaviorSimulator
    {
        private readonly HumanBehaviorConfig _config;
        private readonly MouseSimulator _mouse;
        private readonly ScrollSimulator _scroll;
        private readonly TypingSimulator _typing;
        private readonly ActionScheduler _scheduler;

        public HumanBehaviorSimulator(HumanBehaviorConfig? config = null)
        {
            _config = config ?? HumanBehaviorConfig.Default;
            _mouse = new MouseSimulator(_config);
            _scroll = new ScrollSimulator(_config);
            _typing = new TypingSimulator(_config);
            _scheduler = new ActionScheduler(_config);
        }

        /// <summary>
        /// 配置
        /// </summary>
        public HumanBehaviorConfig Config => _config;

        /// <summary>
        /// 鼠标模拟器
        /// </summary>
        public MouseSimulator Mouse => _mouse;

        /// <summary>
        /// 滚动模拟器
        /// </summary>
        public ScrollSimulator Scroll => _scroll;

        /// <summary>
        /// 输入模拟器
        /// </summary>
        public TypingSimulator Typing => _typing;

        /// <summary>
        /// 操作调度器
        /// </summary>
        public ActionScheduler Scheduler => _scheduler;

        /// <summary>
        /// 模拟点击元素
        /// </summary>
        public async Task ClickAsync(IWebBrowser browser, string selector, CancellationToken ct = default)
        {
            await _scheduler.WaitForNextAction(ct);

            var script = GenerateClickScript(selector);
            await browser.EvaluateScriptAsync(script);
        }

        /// <summary>
        /// 模拟输入文本
        /// </summary>
        public async Task TypeAsync(IWebBrowser browser, string selector, string text, CancellationToken ct = default)
        {
            await _scheduler.WaitForNextAction(ct);

            var script = _typing.GenerateTypingScript(selector, text);
            await browser.EvaluateScriptAsync(script);
        }

        /// <summary>
        /// 模拟清空并输入
        /// </summary>
        public async Task ClearAndTypeAsync(IWebBrowser browser, string selector, string text, CancellationToken ct = default)
        {
            await _scheduler.WaitForNextAction(ct);

            var script = _typing.GenerateClearAndTypeScript(selector, text);
            await browser.EvaluateScriptAsync(script);
        }

        /// <summary>
        /// 模拟滚动
        /// </summary>
        public async Task ScrollAsync(IWebBrowser browser, int distance, CancellationToken ct = default)
        {
            await _scheduler.WaitShort(ct);

            var script = _scroll.GenerateSmoothScrollScript(distance);
            await browser.EvaluateScriptAsync(script);
        }

        /// <summary>
        /// 滚动到元素
        /// </summary>
        public async Task ScrollToElementAsync(IWebBrowser browser, string selector, CancellationToken ct = default)
        {
            await _scheduler.WaitShort(ct);

            var script = _scroll.GenerateScrollToElementScript(selector);
            await browser.EvaluateScriptAsync(script);
        }

        /// <summary>
        /// 模拟浏览页面
        /// </summary>
        public async Task BrowsePageAsync(IWebBrowser browser, CancellationToken ct = default)
        {
            // 获取页面高度
            var result = await browser.EvaluateScriptAsync(
                "JSON.stringify({pageHeight: document.body.scrollHeight, viewportHeight: window.innerHeight})");
            
            if (result.Success && result.Result != null)
            {
                var json = result.Result.ToString();
                // 简单解析
                var pageHeight = ExtractJsonInt(json, "pageHeight");
                var viewportHeight = ExtractJsonInt(json, "viewportHeight");

                if (pageHeight > viewportHeight)
                {
                    var script = _scroll.GenerateBrowseScrollScript(pageHeight, viewportHeight);
                    await browser.EvaluateScriptAsync(script);
                }
            }
        }

        /// <summary>
        /// 等待并点击（等待元素出现后点击）
        /// </summary>
        public async Task WaitAndClickAsync(IWebBrowser browser, string selector, 
            int timeoutMs = 10000, CancellationToken ct = default)
        {
            var waitScript = $@"
(async function() {{
    const timeout = {timeoutMs};
    const start = Date.now();
    while (Date.now() - start < timeout) {{
        const el = document.querySelector('{EscapeSelector(selector)}');
        if (el && el.offsetParent !== null) return true;
        await new Promise(r => setTimeout(r, 100));
    }}
    return false;
}})();";

            var result = await browser.EvaluateScriptAsync(waitScript);
            if (result.Success && result.Result is bool found && found)
            {
                await ClickAsync(browser, selector, ct);
            }
        }

        /// <summary>
        /// 生成点击脚本
        /// </summary>
        private string GenerateClickScript(string selector)
        {
            var clickDelay = _mouse.GenerateClickDelay();
            var holdDuration = _mouse.GenerateClickHoldDuration();
            var (offsetX, offsetY) = _mouse.GenerateClickOffset();

            return $@"
(async function() {{
    const sleep = ms => new Promise(r => setTimeout(r, ms));
    const el = document.querySelector('{EscapeSelector(selector)}');
    if (!el) return false;
    
    const rect = el.getBoundingClientRect();
    const x = rect.left + rect.width / 2 + {offsetX};
    const y = rect.top + rect.height / 2 + {offsetY};
    
    // 移动鼠标到元素
    el.dispatchEvent(new MouseEvent('mouseenter', {{clientX: x, clientY: y, bubbles: true}}));
    el.dispatchEvent(new MouseEvent('mouseover', {{clientX: x, clientY: y, bubbles: true}}));
    await sleep({clickDelay});
    
    // 鼠标按下
    el.dispatchEvent(new MouseEvent('mousedown', {{clientX: x, clientY: y, bubbles: true, button: 0}}));
    await sleep({holdDuration});
    
    // 鼠标释放
    el.dispatchEvent(new MouseEvent('mouseup', {{clientX: x, clientY: y, bubbles: true, button: 0}}));
    el.dispatchEvent(new MouseEvent('click', {{clientX: x, clientY: y, bubbles: true, button: 0}}));
    
    // 如果是链接或按钮，触发原生点击
    if (el.tagName === 'A' || el.tagName === 'BUTTON' || el.type === 'submit') {{
        el.click();
    }}
    
    return true;
}})();";
        }

        /// <summary>
        /// 模拟悬停
        /// </summary>
        public async Task HoverAsync(IWebBrowser browser, string selector, CancellationToken ct = default)
        {
            await _scheduler.WaitShort(ct);

            var script = $@"
(async function() {{
    const el = document.querySelector('{EscapeSelector(selector)}');
    if (!el) return;
    
    const rect = el.getBoundingClientRect();
    const x = rect.left + rect.width / 2;
    const y = rect.top + rect.height / 2;
    
    el.dispatchEvent(new MouseEvent('mouseenter', {{clientX: x, clientY: y, bubbles: true}}));
    el.dispatchEvent(new MouseEvent('mouseover', {{clientX: x, clientY: y, bubbles: true}}));
    el.dispatchEvent(new MouseEvent('mousemove', {{clientX: x, clientY: y, bubbles: true}}));
}})();";

            await browser.EvaluateScriptAsync(script);
        }

        private static string EscapeSelector(string selector)
        {
            return selector.Replace("'", "\\'").Replace("\\", "\\\\");
        }

        private static int ExtractJsonInt(string json, string key)
        {
            var pattern = $"\"{key}\":";
            var idx = json.IndexOf(pattern);
            if (idx < 0) return 0;
            
            idx += pattern.Length;
            var endIdx = json.IndexOfAny(new[] { ',', '}' }, idx);
            if (endIdx < 0) return 0;
            
            var value = json.Substring(idx, endIdx - idx).Trim();
            return int.TryParse(value, out var result) ? result : 0;
        }
    }
}
