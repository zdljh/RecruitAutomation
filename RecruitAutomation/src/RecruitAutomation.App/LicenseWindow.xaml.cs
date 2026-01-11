using System;
using System.IO;
using System.Windows;
using System.Windows.Media;
using RecruitAutomation.Core.Constants;
using RecruitAutomation.Core.License;

namespace RecruitAutomation.App
{
    /// <summary>
    /// 授权窗口 - 粘贴授权码激活
    /// </summary>
    public partial class LicenseWindow : Window
    {
        public LicenseWindow()
        {
            InitializeComponent();
            InitializeUI();
        }

        private void InitializeUI()
        {
            // 显示机器码
            txtMachineCode.Text = LicenseGuard.Instance.MachineCode;

            // 检查是否已有授权
            var result = LicenseGuard.Instance.LastValidationResult;
            if (result.IsValid && result.LicenseInfo != null)
            {
                ShowLicenseInfo(result.LicenseInfo);
                SetStatus("✅ 已授权", $"有效期至: {result.LicenseInfo.ExpiresAt:yyyy-MM-dd}", "#D4EDDA", "#155724");
            }
            else
            {
                UpdateStatusDisplay(result);
            }
        }

        private void ShowLicenseInfo(LicenseInfo license)
        {
            txtLicenseInfo.Text = $"授权类型: {GetLicenseTypeName(license.LicenseType)} | " +
                                 $"最大账号: {license.MaxAccounts} | " +
                                 $"有效期至: {license.ExpiresAt:yyyy-MM-dd}";
            grpLicenseInfo.Visibility = Visibility.Visible;
        }

        private void UpdateStatusDisplay(LicenseValidationResult result)
        {
            switch (result.Status)
            {
                case LicenseStatus.FileNotFound:
                    SetStatus("⚠ 未授权", "请输入授权码激活软件", "#FFF3CD", "#856404");
                    break;
                case LicenseStatus.Expired:
                    SetStatus("⏰ 授权已过期", result.Message, "#F8D7DA", "#721C24");
                    break;
                case LicenseStatus.InvalidSignature:
                    SetStatus("❌ 授权码无效", "请检查授权码是否正确", "#F8D7DA", "#721C24");
                    break;
                case LicenseStatus.MachineCodeMismatch:
                    SetStatus("🖥 机器码不匹配", "此授权码不适用于本机", "#F8D7DA", "#721C24");
                    break;
                default:
                    SetStatus("⚠ 授权验证失败", result.Message, "#FFF3CD", "#856404");
                    break;
            }
        }

        private void SetStatus(string title, string message, string bgColor, string fgColor)
        {
            txtStatusTitle.Text = title;
            txtStatusMessage.Text = message;
            borderStatus.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(bgColor));
            txtStatusTitle.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(fgColor));
            txtStatusMessage.Foreground = txtStatusTitle.Foreground;
        }

        private void BtnCopyMachineCode_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Clipboard.SetText(txtMachineCode.Text);
                MessageBox.Show("机器码已复制到剪贴板\n\n请发送给管理员获取授权码", "提示",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"复制失败: {ex.Message}", "错误",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnActivate_Click(object sender, RoutedEventArgs e)
        {
            var licenseCode = txtLicenseCode.Text.Trim();
            if (string.IsNullOrWhiteSpace(licenseCode))
            {
                MessageBox.Show("请粘贴授权码", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                txtLicenseCode.Focus();
                return;
            }

            try
            {
                // 保存授权码到文件
                var dir = Path.GetDirectoryName(AppConstants.LicenseFilePath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }
                File.WriteAllText(AppConstants.LicenseFilePath, licenseCode);

                // 验证授权
                var result = LicenseGuard.Instance.Validate();

                if (result.IsValid)
                {
                    var license = result.LicenseInfo!;
                    ShowLicenseInfo(license);
                    SetStatus("✅ 授权成功", $"有效期至: {license.ExpiresAt:yyyy-MM-dd}", "#D4EDDA", "#155724");

                    MessageBox.Show(
                        $"✅ 授权激活成功！\n\n" +
                        $"授权类型: {GetLicenseTypeName(license.LicenseType)}\n" +
                        $"有效期至: {license.ExpiresAt:yyyy-MM-dd}\n" +
                        $"最大账号: {license.MaxAccounts}",
                        "授权成功",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);

                    // 打开主窗口
                    var mainWindow = new MainWindow();
                    mainWindow.Show();
                    Close();
                }
                else
                {
                    // 删除无效的授权文件
                    try { File.Delete(AppConstants.LicenseFilePath); } catch { }
                    
                    UpdateStatusDisplay(result);
                    MessageBox.Show($"授权验证失败\n\n{result.Message}", "验证失败",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"激活失败: {ex.Message}", "错误",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private static string GetLicenseTypeName(LicenseType type)
        {
            return type switch
            {
                LicenseType.Trial => "试用版",
                LicenseType.Professional => "专业版",
                LicenseType.Enterprise => "企业版",
                _ => type.ToString()
            };
        }
    }
}
