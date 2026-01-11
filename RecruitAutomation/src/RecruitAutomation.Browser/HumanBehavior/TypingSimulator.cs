using System;
using System.Collections.Generic;
using System.Text;

namespace RecruitAutomation.Browser.HumanBehavior
{
    /// <summary>
    /// 键盘输入模拟器
    /// 生成类人的打字行为
    /// </summary>
    public class TypingSimulator
    {
        private readonly HumanBehaviorConfig _config;
        
        // 相邻按键映射（用于模拟打字错误）
        private static readonly Dictionary<char, char[]> AdjacentKeys = new()
        {
            {'a', new[] {'q', 'w', 's', 'z'}},
            {'b', new[] {'v', 'g', 'h', 'n'}},
            {'c', new[] {'x', 'd', 'f', 'v'}},
            {'d', new[] {'s', 'e', 'r', 'f', 'c', 'x'}},
            {'e', new[] {'w', 's', 'd', 'r'}},
            {'f', new[] {'d', 'r', 't', 'g', 'v', 'c'}},
            {'g', new[] {'f', 't', 'y', 'h', 'b', 'v'}},
            {'h', new[] {'g', 'y', 'u', 'j', 'n', 'b'}},
            {'i', new[] {'u', 'j', 'k', 'o'}},
            {'j', new[] {'h', 'u', 'i', 'k', 'm', 'n'}},
            {'k', new[] {'j', 'i', 'o', 'l', 'm'}},
            {'l', new[] {'k', 'o', 'p'}},
            {'m', new[] {'n', 'j', 'k'}},
            {'n', new[] {'b', 'h', 'j', 'm'}},
            {'o', new[] {'i', 'k', 'l', 'p'}},
            {'p', new[] {'o', 'l'}},
            {'q', new[] {'w', 'a'}},
            {'r', new[] {'e', 'd', 'f', 't'}},
            {'s', new[] {'a', 'w', 'e', 'd', 'x', 'z'}},
            {'t', new[] {'r', 'f', 'g', 'y'}},
            {'u', new[] {'y', 'h', 'j', 'i'}},
            {'v', new[] {'c', 'f', 'g', 'b'}},
            {'w', new[] {'q', 'a', 's', 'e'}},
            {'x', new[] {'z', 's', 'd', 'c'}},
            {'y', new[] {'t', 'g', 'h', 'u'}},
            {'z', new[] {'a', 's', 'x'}},
        };

        public TypingSimulator(HumanBehaviorConfig? config = null)
        {
            _config = config ?? HumanBehaviorConfig.Default;
        }

        /// <summary>
        /// 生成输入序列（包含延迟和可能的错误）
        /// </summary>
        public List<(char Char, int Delay, bool IsBackspace)> GenerateTypingSequence(string text)
        {
            var sequence = new List<(char Char, int Delay, bool IsBackspace)>();

            foreach (char c in text)
            {
                // 基础延迟（高斯分布）
                int delay = RandomGenerator.NextGaussianInt(_config.TypeDelayMin, _config.TypeDelayMax);

                // 特殊字符打字更慢
                if (!char.IsLetterOrDigit(c))
                {
                    delay = (int)(delay * 1.3);
                }

                // 偶尔暂停（模拟思考）
                if (RandomGenerator.Chance(_config.TypePauseRate))
                {
                    delay += _config.TypePauseDuration;
                }

                // 模拟打字错误
                if (RandomGenerator.Chance(_config.TypoRate) && char.IsLetter(c))
                {
                    char typo = GetAdjacentKey(char.ToLower(c));
                    if (typo != c)
                    {
                        // 输入错误字符
                        sequence.Add((char.IsUpper(c) ? char.ToUpper(typo) : typo, delay, false));
                        
                        // 短暂停顿后发现错误
                        int pauseDelay = RandomGenerator.Next(100, 300);
                        
                        // 删除错误字符
                        sequence.Add(('\b', pauseDelay, true));
                        
                        // 重新输入正确字符
                        delay = RandomGenerator.NextGaussianInt(_config.TypeDelayMin, _config.TypeDelayMax);
                    }
                }

                sequence.Add((c, delay, false));
            }

            return sequence;
        }

        /// <summary>
        /// 获取相邻按键（用于模拟打字错误）
        /// </summary>
        private char GetAdjacentKey(char c)
        {
            if (AdjacentKeys.TryGetValue(c, out var adjacent) && adjacent.Length > 0)
            {
                return adjacent[RandomGenerator.Next(0, adjacent.Length - 1)];
            }
            return c;
        }

        /// <summary>
        /// 生成输入文本的 JavaScript 代码
        /// </summary>
        public string GenerateTypingScript(string selector, string text)
        {
            var sequence = GenerateTypingSequence(text);
            var script = new StringBuilder();

            script.AppendLine("(async function() {");
            script.AppendLine("  const sleep = ms => new Promise(r => setTimeout(r, ms));");
            script.AppendLine($"  const input = document.querySelector('{EscapeSelector(selector)}');");
            script.AppendLine("  if (!input) return;");
            script.AppendLine("  input.focus();");
            script.AppendLine($"  await sleep({RandomGenerator.Next(100, 300)});");

            foreach (var (c, delay, isBackspace) in sequence)
            {
                if (isBackspace)
                {
                    script.AppendLine($"  await sleep({delay});");
                    script.AppendLine("  input.value = input.value.slice(0, -1);");
                    script.AppendLine("  input.dispatchEvent(new Event('input', {bubbles: true}));");
                }
                else
                {
                    script.AppendLine($"  await sleep({delay});");
                    script.AppendLine($"  input.value += '{EscapeJsChar(c)}';");
                    script.AppendLine("  input.dispatchEvent(new Event('input', {bubbles: true}));");
                }
            }

            // 触发 change 事件
            script.AppendLine("  input.dispatchEvent(new Event('change', {bubbles: true}));");
            script.AppendLine("})();");

            return script.ToString();
        }

        /// <summary>
        /// 生成清空并输入的 JavaScript 代码
        /// </summary>
        public string GenerateClearAndTypeScript(string selector, string text)
        {
            var script = new StringBuilder();
            script.AppendLine("(async function() {");
            script.AppendLine("  const sleep = ms => new Promise(r => setTimeout(r, ms));");
            script.AppendLine($"  const input = document.querySelector('{EscapeSelector(selector)}');");
            script.AppendLine("  if (!input) return;");
            script.AppendLine("  input.focus();");
            script.AppendLine($"  await sleep({RandomGenerator.Next(100, 200)});");
            script.AppendLine("  input.select();");
            script.AppendLine($"  await sleep({RandomGenerator.Next(50, 150)});");
            script.AppendLine("  input.value = '';");
            script.AppendLine("  input.dispatchEvent(new Event('input', {bubbles: true}));");
            script.AppendLine($"  await sleep({RandomGenerator.Next(100, 300)});");
            script.AppendLine("})();");

            // 追加输入脚本
            return script.ToString() + GenerateTypingScript(selector, text);
        }

        private static string EscapeSelector(string selector)
        {
            return selector.Replace("'", "\\'");
        }

        private static string EscapeJsChar(char c)
        {
            return c switch
            {
                '\'' => "\\'",
                '\\' => "\\\\",
                '\n' => "\\n",
                '\r' => "\\r",
                '\t' => "\\t",
                _ => c.ToString()
            };
        }
    }
}
