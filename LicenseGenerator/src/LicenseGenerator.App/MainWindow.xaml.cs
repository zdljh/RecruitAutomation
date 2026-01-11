using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using LicenseGenerator.Core.License;

namespace LicenseGenerator.App
{
    public partial class MainWindow : Window
    {
        private const string PrivateKey = @"<RSAKeyValue><Modulus>w7MjS1hZzJKUDZdb1xhYVJaQSwrMXvmY6IsPvp+qnYYm9IekPT+ekpQR3XcPdQw2n6ZXfZaLI3Y12FKkGdvuSowMjMvkQkj/FianTTuyYeWFxrq9qucnwvyIzu9eA7f4dTk+EOL/lWV+95uLW0UjkOoWNSO2ONopJLo7iN3eSxD0o2YcFXWNTFumqkVuhauy/KDANnyhJc3t3P34O+wW1ukQc5JtJ+N5pd2Bmdh2u8cfi4koHvas7X1WOpYCY8Ke87CMvHm3WE9k3XrNLUokYhZo8wnFvK3fsxQMmztu47lEWQRfmJKRaqzq+QqVBuG1yh/PqQTvnbxvb+V+EVSeWQ==</Modulus><Exponent>AQAB</Exponent><P>/yscUW7P4Fw9S+bhrF15B83F1Xrmf2FpnRLMcT/8Jd0DY35ri4pUWHB9izpU7Axl9ud75lHqbm6D/BRIhY8z2FAJQ9Tde9t+X7aQZ0kS0Er35qXOf82lKQ/ayGjDs2/riB8HdpWzxQBH2FnT5zF22XKT1mpIZCMev0U232vqKWM=</P><Q>xFZpeTJTe+rpj7Mvi3LRymDTJomw/j3oGzQV624g27hlJieZ4hv1QQ0DD+FlDe4s3P/zMk3KOMrBATLB02bGeboBfxf6bpG0hNJJL9w4jmPkkM1RdzCuClj4SJ4KClU65V0M8eW455chY7budTbtV3jB+wkqL/zUGPLbkbjaBBM=</Q><DP>6wj8po2ZcKHF3DoouKnIp8WEaqUv1zkVHReJtO8pBH9VdbmmufuKwYOsQChUvLCW4xxJ5daiR2IItJLCUjObn63pOs/ByypcdzEkRd7rM206dvtXACWd8fqmnV7SlF+M5e8e4r31vooJo2DqbNQFEzoUrrVrRMGMusW4S4eNQt0=</DP><DQ>PMRyCKzm4eenOm6/PG3hOL4XHEppmYcXm7PXRPLlAJxl0hVXr3/vvJ6GYBfm6xTYld4yK1OgT0uRyQkorIGW1H4ZkHifbjFyqdlcBZAngQqx549ks3tBoro+vlsLyH7wp6TRKN1tCWDhWLd5vpWth/E8OLJxeEDMdJxWERghgjk=</DQ><InverseQ>OXF0jXMTm4NUH3fxTw0G9rH5R2vg6qXugWWOgSqf3THR0IR66QXQTLzRnx6t96nWUa1UZgf92v+nY5x1kAXrFRB2yQZNIDGEnR802Rx4uNTka8i4KIdR7qHz0OKHP/SnRuenOMtHhDRa0WWLHO+9MsVasnuqQz+F59uMqS3/s8k=</InverseQ><D>V68H/VZUxehXFc/fgnyR9zSO6lCoSVWkQW0tXMfFdlcJVT8BQ8AhmKNnbcdO0a7rOpUZVlgBd54behVtGXkFR7mAVgV0/I4gXRhslZpNzrc8PVKmcNCpbCAiXDW79gaT+FHxkTdkNNgJD4BN7FMKIIAB+0VI/CgjjyUyT5y5YQApj3gmhuvcsux4n4YFjTcfArFQItr8E+HLULqF8//5EiQ4QhkO0k7TtMILNC8MzrRTv+89SlOWoq0sjdLh0yKP85Ao4DMq0Z0vkru4rdRlWGx53EoyK/AMiiibkGGlnoFEbDHIkvLLmC0XTVQuqjSrN/dhQzeUbsPIUWbqv4PoUQ==</D></RSAKeyValue>";

        private LicenseInfo? _lastGeneratedLicense;

        public MainWindow()
        {
            InitializeComponent();
        }

        private void BtnGenerate_Click(object sender, RoutedEventArgs e)
        {
            // 验证机器码
            var machineCode = txtMachineCode.Text.Trim().ToUpperInvariant();
            if (string.IsNullOrWhiteSpace(machineCode))
            {
                ShowError("请输入机器码");
                txtMachineCode.Focus();
                return;
            }

            // 标准化机器码格式（支持16位和32位两种格式）
            machineCode = machineCode.Replace(" ", "");
            
            // 如果是不带横线的格式，添加横线
            var cleanCode = machineCode.Replace("-", "");
            if (cleanCode.Length == 16)
            {
                // 16位短格式：XXXX-XXXX-XXXX-XXXX
                machineCode = $"{cleanCode.Substring(0, 4)}-{cleanCode.Substring(4, 4)}-{cleanCode.Substring(8, 4)}-{cleanCode.Substring(12, 4)}";
            }
            else if (cleanCode.Length == 32)
            {
                // 32位长格式：XXXXXXXX-XXXX-XXXX-XXXX-XXXXXXXXXXXX
                machineCode = $"{cleanCode.Substring(0, 8)}-{cleanCode.Substring(8, 4)}-{cleanCode.Substring(12, 4)}-{cleanCode.Substring(16, 4)}-{cleanCode.Substring(20)}";
            }
            else if (!machineCode.Contains("-"))
            {
                ShowError("机器码格式不正确，请输入16位或32位机器码");
                txtMachineCode.Focus();
                return;
            }

            // 验证最大账号数
            if (!int.TryParse(txtMaxAccounts.Text, out var maxAccounts) || maxAccounts < 1)
            {
                ShowError("最大账号数必须大于 0");
                txtMaxAccounts.Focus();
                return;
            }

            try
            {
                // 获取授权天数
                var durationItem = (ComboBoxItem)cmbDuration.SelectedItem;
                var days = int.Parse(durationItem.Tag.ToString()!);

                // 获取授权类型
                var typeItem = (ComboBoxItem)cmbLicenseType.SelectedItem;
                var licenseType = (LicenseType)int.Parse(typeItem.Tag.ToString()!);

                // 获取功能列表
                var features = new List<string>();
                if (chkBoss.IsChecked == true) features.Add("boss");
                if (chkZhilian.IsChecked == true) features.Add("zhilian");
                if (chk58.IsChecked == true) features.Add("58");
                if (chkGanji.IsChecked == true) features.Add("ganji");
                if (chkQiancheng.IsChecked == true) features.Add("qiancheng");
                if (chkLiepin.IsChecked == true) features.Add("liepin");
                if (chkYupaowang.IsChecked == true) features.Add("yupaowang");
                if (chkAI.IsChecked == true) features.Add("ai");

                // 生成 License
                using var builder = new LicenseBuilder(PrivateKey);
                _lastGeneratedLicense = builder
                    .SetMachineCode(machineCode)
                    .SetLicenseTo("授权用户")
                    .SetLicenseType(licenseType)
                    .SetValidDays(days)
                    .SetMaxAccounts(maxAccounts)
                    .SetFeatures(features.ToArray())
                    .Build();

                // 显示紧凑格式的授权码（直接 JSON）
                txtResult.Text = _lastGeneratedLicense.ToCompactJson();

                // 显示 License 信息
                var sb = new StringBuilder();
                sb.AppendLine($"机器码: {_lastGeneratedLicense.MachineCode}");
                sb.AppendLine($"授权类型: {GetLicenseTypeName(licenseType)}");
                sb.AppendLine($"最大账号: {maxAccounts}");
                sb.AppendLine($"生效时间: {_lastGeneratedLicense.IssuedAt:yyyy-MM-dd HH:mm}");
                sb.AppendLine($"过期时间: {_lastGeneratedLicense.ExpiresAt:yyyy-MM-dd HH:mm}");
                sb.AppendLine($"授权功能: {string.Join(", ", features)}");
                txtLicenseInfo.Text = sb.ToString();

                ShowSuccess("授权码生成成功！复制后发送给客户即可");
            }
            catch (Exception ex)
            {
                ShowError($"生成失败: {ex.Message}");
            }
        }

        private string GetLicenseTypeName(LicenseType type)
        {
            return type switch
            {
                LicenseType.Trial => "试用版",
                LicenseType.Professional => "标准版",
                LicenseType.Enterprise => "专业版",
                _ => type.ToString()
            };
        }

        private void BtnCopy_Click(object sender, RoutedEventArgs e)
        {
            if (_lastGeneratedLicense == null || string.IsNullOrWhiteSpace(txtResult.Text))
            {
                ShowError("请先生成授权码");
                return;
            }

            try
            {
                Clipboard.SetText(txtResult.Text);
                ShowSuccess("授权码已复制到剪贴板！\n\n客户粘贴到主程序即可激活。");
            }
            catch (Exception ex)
            {
                ShowError($"复制失败: {ex.Message}");
            }
        }

        private void ShowError(string message)
        {
            MessageBox.Show(message, "错误", MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        private void ShowSuccess(string message)
        {
            MessageBox.Show(message, "成功", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}
