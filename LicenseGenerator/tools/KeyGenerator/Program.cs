using System;
using System.IO;
using System.Security.Cryptography;

var rsa = RSA.Create(2048);
var publicKey = rsa.ToXmlString(false);
var privateKey = rsa.ToXmlString(true);

File.WriteAllText("public.key", publicKey);
File.WriteAllText("private.key", privateKey);

Console.WriteLine("=== RSA 密钥对已生成 ===");
Console.WriteLine();
Console.WriteLine("公钥 (public.key):");
Console.WriteLine(publicKey);
Console.WriteLine();
Console.WriteLine("私钥已保存到 private.key");
