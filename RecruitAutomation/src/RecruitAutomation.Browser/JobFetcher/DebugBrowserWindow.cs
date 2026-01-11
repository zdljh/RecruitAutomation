using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using CefSharp.Wpf;

namespace RecruitAutomation.Browser.JobFetcher
{
    /// <summary>
    /// 调试模式浏览器窗口
    /// 用于可视化调试岗位读取过程
    /// </summary>
    public class DebugBrowserWindow : Window
    {
        private readonly ChromiumWebBrowser _browser;
        private readonly TextBlock _stepIndicator;
        private readonly TextBlock _pageTypeIndicator;
        private readonly TextBlock _urlIndicator;
        private readonly Border _highlightOverlay;
        private readonly DispatcherTimer _refreshTimer;

        public DebugBrowserWindow(ChromiumWebBrowser browser, string accountId)
        {
            _browser = browser;
            
            Title = $"🔍 调试模式 - 账号: {accountId}";
            Width = 1400;
            Height = 900;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            Topmost = true; // 置顶显示
            
            var mainGrid = new Grid();
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // 顶部状态栏
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // 浏览器
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // 底部信息栏

            // 顶部状态栏
            var topBar = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(33, 33, 33)),
                Padding = new Thickness(15, 10, 15, 10)
            };
            var topStack = new StackPanel { Orientation = Orientation.Horizontal };
            
            _stepIndicator = new TextBlock
            {
                Text = "⏳ 准备中...",
                Foreground = Brushes.White,
                FontSize = 16,
                FontWeight = FontWeights.Bold,
                VerticalAlignment = VerticalAlignment.Center
            };
            topStack.Children.Add(_stepIndicator);
            
            topStack.Children.Add(new TextBlock { Text = "  |  ", Foreground = Brushes.Gray, VerticalAlignment = VerticalAlignment.Center });
            
            _pageTypeIndicator = new TextBlock
            {
                Text = "页面类型: 检测中...",
                Foreground = new SolidColorBrush(Color.FromRgb(255, 193, 7)),
                FontSize = 14,
                VerticalAlignment = VerticalAlignment.Center
            };
            topStack.Children.Add(_pageTypeIndicator);
            
            topBar.Child = topStack;
            Grid.SetRow(topBar, 0);
            mainGrid.Children.Add(topBar);

            // 浏览器容器（带高亮覆盖层）
            var browserContainer = new Grid();
            
            // 浏览器内容
            var browserBorder = new Border
            {
                BorderBrush = new SolidColorBrush(Color.FromRgb(76, 175, 80)),
                BorderThickness = new Thickness(3),
                Child = browser
            };
            browserContainer.Children.Add(browserBorder);
            
            // 高亮覆盖层（用于显示AI识别区域）
            _highlightOverlay = new Border
            {
                Background = Brushes.Transparent,
                IsHitTestVisible = false
            };
            browserContainer.Children.Add(_highlightOverlay);
            
            Grid.SetRow(browserContainer, 1);
            mainGrid.Children.Add(browserContainer);

            // 底部信息栏
            var bottomBar = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(45, 45, 45)),
                Padding = new Thickness(15, 8, 15, 8)
            };
            var bottomStack = new StackPanel();
            
            _urlIndicator = new TextBlock
            {
                Text = "URL: 加载中...",
                Foreground = new SolidColorBrush(Color.FromRgb(144, 202, 249)),
                FontSize = 12,
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            bottomStack.Children.Add(_urlIndicator);
            
            var helpText = new TextBlock
            {
                Text = "💡 调试模式：观察浏览器窗口，确认页面状态是否正常。如果看到风控页/登录页，说明账号状态异常。",
                Foreground = new SolidColorBrush(Color.FromRgb(189, 189, 189)),
                FontSize = 11,
                Margin = new Thickness(0, 5, 0, 0)
            };
            bottomStack.Children.Add(helpText);
            
            bottomBar.Child = bottomStack;
            Grid.SetRow(bottomBar, 2);
            mainGrid.Children.Add(bottomBar);

            Content = mainGrid;

            // 定时刷新URL显示
            _refreshTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
            _refreshTimer.Tick += (s, e) => RefreshUrlDisplay();
            _refreshTimer.Start();

            Closed += (s, e) => _refreshTimer.Stop();
        }

        private void RefreshUrlDisplay()
        {
            try
            {
                var url = _browser?.Address ?? "未知";
                _urlIndicator.Text = $"URL: {url}";
            }
            catch { }
        }

        /// <summary>
        /// 更新当前步骤显示
        /// </summary>
        public void UpdateStep(int current, int total, string description)
        {
            Dispatcher.Invoke(() =>
            {
                _stepIndicator.Text = $"[步骤 {current}/{total}] {description}";
                _stepIndicator.Foreground = new SolidColorBrush(Color.FromRgb(76, 175, 80));
            });
        }

        /// <summary>
        /// 更新页面类型显示
        /// </summary>
        public void UpdatePageType(PageDiagnosticType type, string detail = "")
        {
            Dispatcher.Invoke(() =>
            {
                var (text, color) = type switch
                {
                    PageDiagnosticType.NormalJobList => ("✅ 正常职位管理页", Color.FromRgb(76, 175, 80)),
                    PageDiagnosticType.LoginPage => ("⚠️ 登录页面", Color.FromRgb(255, 152, 0)),
                    PageDiagnosticType.RiskControl => ("❌ 风控/校验页", Color.FromRgb(244, 67, 54)),
                    PageDiagnosticType.Loading => ("⏳ 加载中...", Color.FromRgb(33, 150, 243)),
                    PageDiagnosticType.EmptyState => ("📭 空状态页面", Color.FromRgb(156, 39, 176)),
                    PageDiagnosticType.Unknown => ("❓ 未知页面", Color.FromRgb(158, 158, 158)),
                    _ => ("检测中...", Color.FromRgb(255, 193, 7))
                };

                _pageTypeIndicator.Text = string.IsNullOrEmpty(detail) ? $"页面类型: {text}" : $"页面类型: {text} - {detail}";
                _pageTypeIndicator.Foreground = new SolidColorBrush(color);
            });
        }

        /// <summary>
        /// 显示错误状态
        /// </summary>
        public void ShowError(string message)
        {
            Dispatcher.Invoke(() =>
            {
                _stepIndicator.Text = $"❌ 错误: {message}";
                _stepIndicator.Foreground = new SolidColorBrush(Color.FromRgb(244, 67, 54));
            });
        }

        /// <summary>
        /// 高亮显示识别到的区域
        /// </summary>
        public void HighlightArea(int x, int y, int width, int height, string label)
        {
            Dispatcher.Invoke(() =>
            {
                var canvas = new Canvas();
                
                // 红色边框
                var rect = new System.Windows.Shapes.Rectangle
                {
                    Width = width,
                    Height = height,
                    Stroke = new SolidColorBrush(Color.FromRgb(244, 67, 54)),
                    StrokeThickness = 2,
                    Fill = new SolidColorBrush(Color.FromArgb(30, 244, 67, 54))
                };
                Canvas.SetLeft(rect, x);
                Canvas.SetTop(rect, y);
                canvas.Children.Add(rect);

                // 标签
                var labelBorder = new Border
                {
                    Background = new SolidColorBrush(Color.FromRgb(244, 67, 54)),
                    Padding = new Thickness(5, 2, 5, 2),
                    CornerRadius = new CornerRadius(3),
                    Child = new TextBlock
                    {
                        Text = label,
                        Foreground = Brushes.White,
                        FontSize = 11
                    }
                };
                Canvas.SetLeft(labelBorder, x);
                Canvas.SetTop(labelBorder, y - 25);
                canvas.Children.Add(labelBorder);

                _highlightOverlay.Child = canvas;
            });
        }

        /// <summary>
        /// 清除高亮
        /// </summary>
        public void ClearHighlight()
        {
            Dispatcher.Invoke(() => _highlightOverlay.Child = null);
        }
    }

    /// <summary>
    /// 页面诊断类型
    /// </summary>
    public enum PageDiagnosticType
    {
        Unknown,
        NormalJobList,
        LoginPage,
        RiskControl,
        Loading,
        EmptyState
    }
}
