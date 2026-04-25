using System.Security.Cryptography;
var rsa = RSA.Create(2048);
File.WriteAllText("prod_private.pem", rsa.ExportRSAPrivateKeyPem());
File.WriteAllText("prod_public.pem", rsa.ExportRSAPublicKeyPem());
Console.WriteLine("Keys generated!");
Console.WriteLine(rsa.ExportRSAPrivateKeyPem());