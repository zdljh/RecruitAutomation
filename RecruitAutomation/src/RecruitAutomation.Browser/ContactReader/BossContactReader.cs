using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using CefSharp;
using RecruitAutomation.Browser.HumanBehavior;
using RecruitAutomation.Core.Models;

namespace RecruitAutomation.Browser.ContactReader
{
    /// <summary>
    /// BOSS直聘联系人读取器
    /// 从消息列表页面读取已建立聊天关系的联系人
    /// </summary>
    public class BossContactReader
    {
        private readonly AccountBrowserInstance _browser;
        private readonly HumanBehaviorSimulator _humanBehavior;
        private readonly Random _random = new();

        // BOSS消息列表URL
        private const string MessageListUrl = "https://www.zhipin.com/web/boss/message";

        public event EventHandler<string>? OnLog;

        public BossContactReader(AccountBrowserInstance browser)
        {
            _browser = browser;
            _humanBehavior = new HumanBehaviorSimulator();
        }

        /// <summary>
        /// 读取联系人列表
        /// </summary>
        public async Task<List<ContactInfo>> ReadContactsAsync(int maxCount = 100, CancellationToken ct = default)
        {
            var contacts = new List<ContactInfo>();

            try
            {
                Log("正在进入消息列表页面...");
                
                // 导航到消息列表
                _browser.Navigate(MessageListUrl);
                await WaitForPageLoadAsync(ct);
                
                // 等待页面加载
                await Task.Delay(_random.Next(2000, 3500), ct);

                // 检查是否需要登录
                if (await CheckNeedLoginAsync())
                {
                    Log("检测到需要登录，请先登录账号");
                    return contacts;
                }

                Log("正在读取联系人列表...");

                // 滚动加载更多联系人
                var loadedCount = 0;
                var maxScrollAttempts = 10;
                var scrollAttempts = 0;

                while (loadedCount < maxCount && scrollAttempts < maxScrollAttempts)
                {
                    ct.ThrowIfCancellationRequested();

                    // 读取当前可见的联系人
                    var currentContacts = await ReadVisibleContactsAsync();
                    
                    foreach (var contact in currentContacts)
                    {
                        if (!contacts.Exists(c => c.ContactId == contact.ContactId))
                        {
                            contacts.Add(contact);
                            loadedCount++;
                            
                            if (loadedCount >= maxCount)
                                break;
                        }
                    }

                    Log($"已读取 {contacts.Count} 个联系人");

                    if (loadedCount >= maxCount)
                        break;

                    // 模拟人工滚动加载更多
                    await _humanBehavior.ScrollAsync(_browser.Browser!, 300, ct);
                    await Task.Delay(_random.Next(1000, 2000), ct);
                    
                    scrollAttempts++;
                }

                Log($"联系人读取完成，共 {contacts.Count} 个");
            }
            catch (OperationCanceledException)
            {
                Log("读取已取消");
            }
            catch (Exception ex)
            {
                Log($"读取联系人失败: {ex.Message}");
            }

            return contacts;
        }

        /// <summary>
        /// 读取当前可见的联系人
        /// </summary>
        private async Task<List<ContactInfo>> ReadVisibleContactsAsync()
        {
            var contacts = new List<ContactInfo>();

            var script = @"
(function() {
    var contacts = [];
    
    // BOSS直聘消息列表选择器
    var selectors = [
        '.chat-list .chat-item',
        '.message-list .message-item',
        '[class*=""chat""][class*=""item""]',
        '[class*=""conversation""][class*=""item""]'
    ];
    
    var items = [];
    for (var i = 0; i < selectors.length; i++) {
        items = document.querySelectorAll(selectors[i]);
        if (items.length > 0) break;
    }
    
    for (var j = 0; j < items.length; j++) {
        var item = items[j];
        
        // 提取联系人ID
        var contactId = item.getAttribute('data-uid') || 
                        item.getAttribute('data-id') ||
                        item.getAttribute('data-geek-id') ||
                        'contact_' + j;
        
        // 提取姓名
        var nameEl = item.querySelector('.name, .user-name, [class*=""name""]');
        var name = nameEl ? nameEl.textContent.trim() : '';
        
        // 提取头像
        var avatarEl = item.querySelector('img.avatar, img[class*=""avatar""], .avatar img');
        var avatar = avatarEl ? avatarEl.src : '';
        
        // 提取最后消息
        var msgEl = item.querySelector('.last-msg, .message-content, [class*=""msg""], [class*=""content""]');
        var lastMessage = msgEl ? msgEl.textContent.trim() : '';
        
        // 提取时间
        var timeEl = item.querySelector('.time, .msg-time, [class*=""time""]');
        var timeText = timeEl ? timeEl.textContent.trim() : '';
        
        // 检查是否有未读
        var unreadEl = item.querySelector('.unread, .badge, [class*=""unread""]');
        var hasUnread = unreadEl !== null;
        
        // 提取岗位信息
        var jobEl = item.querySelector('.job-name, .position, [class*=""job""]');
        var jobTitle = jobEl ? jobEl.textContent.trim() : '';
        
        if (name) {
            contacts.push({
                contactId: contactId,
                name: name,
                avatar: avatar,
                lastMessage: lastMessage,
                timeText: timeText,
                hasUnread: hasUnread,
                jobTitle: jobTitle
            });
        }
    }
    
    return JSON.stringify(contacts);
})();";

            try
            {
                var result = await _browser.ExecuteJavaScriptAsync(script);
                
                if (result.Success && result.Result != null)
                {
                    var json = result.Result.ToString();
                    var items = JsonSerializer.Deserialize<List<ContactJsonItem>>(json ?? "[]");
                    
                    if (items != null)
                    {
                        foreach (var item in items)
                        {
                            contacts.Add(new ContactInfo
                            {
                                ContactId = item.contactId ?? "",
                                Name = item.name ?? "",
                                Avatar = item.avatar ?? "",
                                LastMessage = item.lastMessage ?? "",
                                HasUnread = item.hasUnread,
                                JobTitle = item.jobTitle ?? ""
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log($"解析联系人数据失败: {ex.Message}");
            }

            return contacts;
        }

        /// <summary>
        /// 打开与联系人的会话
        /// </summary>
        public async Task<bool> OpenConversationAsync(string contactId, CancellationToken ct = default)
        {
            try
            {
                // 点击联系人打开会话
                var script = $@"
(function() {{
    var selectors = [
        '[data-uid=""{contactId}""]',
        '[data-id=""{contactId}""]',
        '[data-geek-id=""{contactId}""]'
    ];
    
    for (var i = 0; i < selectors.length; i++) {{
        var item = document.querySelector(selectors[i]);
        if (item) {{
            item.click();
            return true;
        }}
    }}
    return false;
}})();";

                var result = await _browser.ExecuteJavaScriptAsync(script);
                
                if (result.Success && result.Result is bool clicked && clicked)
                {
                    // 等待会话加载
                    await Task.Delay(_random.Next(1000, 2000), ct);
                    return true;
                }
            }
            catch (Exception ex)
            {
                Log($"打开会话失败: {ex.Message}");
            }

            return false;
        }

        /// <summary>
        /// 发送消息
        /// </summary>
        public async Task<bool> SendMessageAsync(string message, CancellationToken ct = default)
        {
            try
            {
                // 模拟人工延迟
                await Task.Delay(_random.Next(500, 1500), ct);

                // 查找输入框并输入
                var inputSelector = "textarea.chat-input, .input-area textarea, [class*='chat'][class*='input'], .message-input";
                
                await _humanBehavior.ClearAndTypeAsync(_browser.Browser!, inputSelector, message, ct);
                
                // 模拟思考延迟
                await Task.Delay(_random.Next(800, 1500), ct);

                // 点击发送按钮
                var sendScript = @"
(function() {
    var selectors = [
        'button.send-btn',
        '.send-button',
        '[class*=""send""][class*=""btn""]',
        'button[type=""submit""]'
    ];
    
    for (var i = 0; i < selectors.length; i++) {
        var btn = document.querySelector(selectors[i]);
        if (btn && !btn.disabled) {
            btn.click();
            return true;
        }
    }
    
    // 尝试按Enter发送
    var input = document.querySelector('textarea.chat-input, .input-area textarea');
    if (input) {
        var event = new KeyboardEvent('keydown', {
            key: 'Enter',
            code: 'Enter',
            keyCode: 13,
            which: 13,
            bubbles: true
        });
        input.dispatchEvent(event);
        return true;
    }
    
    return false;
})();";

                var result = await _browser.ExecuteJavaScriptAsync(sendScript);
                
                if (result.Success && result.Result is bool sent && sent)
                {
                    Log($"消息发送成功");
                    return true;
                }
            }
            catch (Exception ex)
            {
                Log($"发送消息失败: {ex.Message}");
            }

            return false;
        }

        /// <summary>
        /// 检查是否有新回复
        /// </summary>
        public async Task<bool> CheckHasNewReplyAsync(string contactId)
        {
            var script = $@"
(function() {{
    var item = document.querySelector('[data-uid=""{contactId}""], [data-id=""{contactId}""]');
    if (item) {{
        var unread = item.querySelector('.unread, .badge, [class*=""unread""]');
        return unread !== null;
    }}
    return false;
}})();";

            try
            {
                var result = await _browser.ExecuteJavaScriptAsync(script);
                return result.Success && result.Result is bool hasReply && hasReply;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 检查页面是否有异常提示（频繁操作等）
        /// </summary>
        public async Task<bool> CheckHasWarningAsync()
        {
            var script = @"
(function() {
    var warnings = document.querySelectorAll('.warning, .alert, .error-tip, [class*=""warning""], [class*=""频繁""]');
    for (var i = 0; i < warnings.length; i++) {
        var text = warnings[i].textContent;
        if (text.includes('频繁') || text.includes('操作过快') || text.includes('稍后') || text.includes('限制')) {
            return true;
        }
    }
    return false;
})();";

            try
            {
                var result = await _browser.ExecuteJavaScriptAsync(script);
                return result.Success && result.Result is bool hasWarning && hasWarning;
            }
            catch
            {
                return false;
            }
        }

        private async Task<bool> CheckNeedLoginAsync()
        {
            var script = @"
(function() {
    var url = window.location.href;
    if (url.includes('login') || url.includes('signin')) return true;
    
    var loginBtn = document.querySelector('.login-btn, .sign-in, [class*=""login""]');
    if (loginBtn && loginBtn.offsetParent !== null) return true;
    
    return false;
})();";

            try
            {
                var result = await _browser.ExecuteJavaScriptAsync(script);
                return result.Success && result.Result is bool needLogin && needLogin;
            }
            catch
            {
                return true;
            }
        }

        private async Task WaitForPageLoadAsync(CancellationToken ct)
        {
            var maxWait = 30;
            var waited = 0;
            
            while (_browser.IsLoading && waited < maxWait)
            {
                await Task.Delay(500, ct);
                waited++;
            }
        }

        private void Log(string message)
        {
            OnLog?.Invoke(this, message);
        }

        private class ContactJsonItem
        {
            public string? contactId { get; set; }
            public string? name { get; set; }
            public string? avatar { get; set; }
            public string? lastMessage { get; set; }
            public string? timeText { get; set; }
            public bool hasUnread { get; set; }
            public string? jobTitle { get; set; }
        }
    }
}
