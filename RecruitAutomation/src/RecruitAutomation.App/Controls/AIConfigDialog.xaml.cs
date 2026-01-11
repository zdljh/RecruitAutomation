using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;

namespace RecruitAutomation.App.Controls
{
    /// <summary>
    /// AI配置对话框
    /// </summary>
    public partial class AIConfigDialog : Window
    {
        private bool _showPassword = false;
        private string _currentApiKey = string.Empty;

        /// <summary>
        /// 获取配置的API Key
        /// </summary>
        public string ApiKey => _currentApiKey;

        public AIConfigDialog(string currentApiKey, string providerName)
        {
            InitializeComponent();
            
            _currentApiKey = currentApiKey;
            
            if (!string.IsNullOrEmpty(currentApiKey))
            {
                txtApiKey.Password = currentApiKey;
                txtApiKeyHint.Text = "已配置API Key";
            }

            // 根据提供商显示不同提示
            txtProviderTip.Text = providerName switch
            {
                "智谱GLM" => "推荐使用智谱GLM-4-Flash，每月有免费额度（约100万tokens）",
                "通义千问" => "通义千问需要开通阿里云账号，有免费试用额度",
                "Kimi" => "Kimi需要注册月之暗面账号，有免费试用额度",
                _ => "推荐使用智谱GLM-4-Flash，每月有免费额度"
            };
        }

        private void BtnTogglePassword_Click(object sender, RoutedEventArgs e)
        {
            _showPassword = !_showPassword;
            
            if (_showPassword)
            {
                // 显示密码（简化处理，实际应该用TextBox替换）
                txtApiKeyHint.Text = txtApiKey.Password;
            }
            else
            {
                txtApiKeyHint.Text = string.IsNullOrEmpty(txtApiKey.Password) ? "" : "已配置API Key";
            }
        }

        private void Link_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Hyperlink link && link.Tag is string url)
            {
                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = url,
                        UseShellExecute = true
                    });
                }
                catch { }
            }
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            var apiKey = txtApiKey.Password.Trim();
            
            if (string.IsNullOrEmpty(apiKey))
            {
                MessageBox.Show("请输入API Key", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            _currentApiKey = apiKey;
            DialogResult = true;
            Close();
        }
    }
}
