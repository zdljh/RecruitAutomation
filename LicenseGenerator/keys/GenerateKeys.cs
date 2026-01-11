// 临时脚本：生成 RSA 密钥对
// 运行: dotnet script GenerateKeys.cs 或在 C# Interactive 中执行

using System;
using System.IO;
using System.Security.Cryptography;

var rsa = RSA.Create(2048);
var publicKey = rsa.ToXmlString(false);
var privateKey = rsa.ToXmlString(true);

Console.WriteLine("=== PUBLIC KEY (复制到主程序 AppConstants.cs) ===");
Console.WriteLine(publicKey);
Console.WriteLine();
Console.WriteLine("=== PRIVATE KEY (保存到 private.key) ===");
Console.WriteLine(privateKey);

File.WriteAllText("public.key", publicKey);
File.WriteAllText("private.key", privateKey);
Console.WriteLine();
Console.WriteLine("密钥已保存到 public.key 和 private.key");
