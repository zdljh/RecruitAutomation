using System.Windows;
using System.Windows.Controls;
using RecruitAutomation.App.V2.Pages;

namespace RecruitAutomation.App.V2
{
    public partial class MainWindow : Window
    {
        private JobManagementPage? _jobPage;
        private BrowserManagementPage? _browserPage;

        public MainWindow()
        {
            InitializeComponent();
            Loaded += MainWindow_Loaded;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // 默认显示岗位管理页面
            ShowJobManagementPage();
        }

        private void BtnNavBrowser_Click(object sender, RoutedEventArgs e)
        {
            ResetNavButtons();
            btnNavBrowser.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(76, 175, 80));
            ShowBrowserManagementPage();
        }

        private void BtnNavJobs_Click(object sender, RoutedEventArgs e)
        {
            ResetNavButtons();
            btnNavJobs.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(76, 175, 80));
            ShowJobManagementPage();
        }

        private void BtnNavCandidates_Click(object sender, RoutedEventArgs e)
        {
            ResetNavButtons();
            btnNavCandidates.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(76, 175, 80));
            MessageBox.Show("候选人库功能开发中...", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void ResetNavButtons()
        {
            btnNavBrowser.Background = System.Windows.Media.Brushes.Transparent;
            btnNavJobs.Background = System.Windows.Media.Brushes.Transparent;
            btnNavCandidates.Background = System.Windows.Media.Brushes.Transparent;
        }

        private void ShowJobManagementPage()
        {
            if (_jobPage == null)
                _jobPage = new JobManagementPage();
            
            contentArea.Children.Clear();
            contentArea.Children.Add(_jobPage);
        }

        private void ShowBrowserManagementPage()
        {
            if (_browserPage == null)
                _browserPage = new BrowserManagementPage();
            
            contentArea.Children.Clear();
            contentArea.Children.Add(_browserPage);
        }
    }
}
