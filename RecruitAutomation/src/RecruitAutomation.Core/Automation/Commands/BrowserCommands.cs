using System;

namespace RecruitAutomation.Core.Automation.Commands
{
    /// <summary>
    /// 导航指令
    /// </summary>
    public sealed class NavigateCommand : AutomationCommand
    {
        public override CommandType Type => CommandType.NavigateTo;

        /// <summary>
        /// 目标 URL
        /// </summary>
        public string Url { get; set; } = string.Empty;

        /// <summary>
        /// 等待页面加载完成
        /// </summary>
        public bool WaitForLoad { get; set; } = true;
    }

    /// <summary>
    /// 点击元素指令
    /// </summary>
    public sealed class ClickElementCommand : AutomationCommand
    {
        public override CommandType Type => CommandType.ClickElement;

        /// <summary>
        /// 元素选择器（CSS / XPath）
        /// </summary>
        public string Selector { get; set; } = string.Empty;

        /// <summary>
        /// 选择器类型
        /// </summary>
        public SelectorType SelectorType { get; set; } = SelectorType.Css;

        /// <summary>
        /// 是否模拟人类行为
        /// </summary>
        public bool SimulateHuman { get; set; } = true;
    }

    /// <summary>
    /// 输入文本指令
    /// </summary>
    public sealed class InputTextCommand : AutomationCommand
    {
        public override CommandType Type => CommandType.InputText;

        /// <summary>
        /// 元素选择器
        /// </summary>
        public string Selector { get; set; } = string.Empty;

        /// <summary>
        /// 选择器类型
        /// </summary>
        public SelectorType SelectorType { get; set; } = SelectorType.Css;

        /// <summary>
        /// 输入内容
        /// </summary>
        public string Text { get; set; } = string.Empty;

        /// <summary>
        /// 是否清空原有内容
        /// </summary>
        public bool ClearFirst { get; set; } = true;

        /// <summary>
        /// 是否模拟人类打字
        /// </summary>
        public bool SimulateTyping { get; set; } = true;
    }

    /// <summary>
    /// 滚动页面指令
    /// </summary>
    public sealed class ScrollPageCommand : AutomationCommand
    {
        public override CommandType Type => CommandType.ScrollPage;

        /// <summary>
        /// 滚动方向
        /// </summary>
        public ScrollDirection Direction { get; set; } = ScrollDirection.Down;

        /// <summary>
        /// 滚动距离（像素）
        /// </summary>
        public int Distance { get; set; } = 300;

        /// <summary>
        /// 是否滚动到元素
        /// </summary>
        public string? ScrollToSelector { get; set; }
    }

    /// <summary>
    /// 等待元素指令
    /// </summary>
    public sealed class WaitElementCommand : AutomationCommand
    {
        public override CommandType Type => CommandType.WaitElement;

        /// <summary>
        /// 元素选择器
        /// </summary>
        public string Selector { get; set; } = string.Empty;

        /// <summary>
        /// 选择器类型
        /// </summary>
        public SelectorType SelectorType { get; set; } = SelectorType.Css;

        /// <summary>
        /// 等待条件
        /// </summary>
        public WaitCondition Condition { get; set; } = WaitCondition.Visible;
    }

    /// <summary>
    /// 选择器类型
    /// </summary>
    public enum SelectorType
    {
        Css,
        XPath,
        Id,
        Name
    }

    /// <summary>
    /// 滚动方向
    /// </summary>
    public enum ScrollDirection
    {
        Up,
        Down,
        Left,
        Right
    }

    /// <summary>
    /// 等待条件
    /// </summary>
    public enum WaitCondition
    {
        Exists,
        Visible,
        Clickable,
        Hidden
    }
}
