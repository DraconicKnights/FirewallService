using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using FirewallCore.Core.Config;

namespace FirewallCore.Utils;

public static class EncryptionUtilityManager
{
    public static bool EnsureSecureLoggingPaths(this FirewallConfig cfg, string baseDirectory) 
    {
        bool dirty = false;
        var logCfg = cfg.Logging;

        if (logCfg.SecureExportPath == "__CREATE_SECURE_LOG_PATH__")
        {
            var secureDir = Path.Combine(baseDirectory, "SecureLogs");
            if (!Directory.Exists(secureDir))
                Directory.CreateDirectory(secureDir);

            logCfg.SecureExportPath = secureDir;
            dirty = true;
        }
        else
        {
            if (!Directory.Exists(logCfg.SecureExportPath))
            {
                Directory.CreateDirectory(logCfg.SecureExportPath);
                dirty = true;
            }
        }

        return dirty;
    }
    
    public static bool EnsureCryptoAndCertificate(this FirewallConfig cfg, string baseDirectory)
    {
        bool dirty = false;

        // 1) Generate AES key/IV if placeholder
        var crypto = cfg.FirewallSettings.Crypto;
        if (crypto.Key == "__GENERATE__" || crypto.Iv == "__GENERATE__")
        {
            using var aes = Aes.Create();
            aes.KeySize = 256;
            crypto.Key = Convert.ToBase64String(aes.Key);
            crypto.Iv  = Convert.ToBase64String(aes.IV);
            dirty = true;
        }

        // 2) Ensure certificate file exists
        var certCfg = cfg.FirewallSettings.Certificate;
        string certPath = Path.Combine(baseDirectory, certCfg.Path);

        if (!File.Exists(certPath) || certCfg.Password == "__GENERATE__")
        {
            // generate a strong random password
            certCfg.Password = Convert.ToBase64String(
                RandomNumberGenerator.GetBytes(32));

            // create the self-signed PFX
            byte[] pfx = CreateSelfSignedCertificatePfx(
                subjectName: "FirewallService",
                validFrom: DateTimeOffset.UtcNow,
                validTo:   DateTimeOffset.UtcNow.AddYears(5),
                password:  certCfg.Password);

            Directory.CreateDirectory(Path.GetDirectoryName(certPath)!);
            File.WriteAllBytes(certPath, pfx);
            dirty = true;
        }

        return dirty;
    }

    private static byte[] CreateSelfSignedCertificatePfx(string subjectName, DateTimeOffset validFrom, DateTimeOffset validTo, string password)
    {
        using RSA rsa = RSA.Create(2048);
        var req = new CertificateRequest(
            new X500DistinguishedName($"CN={subjectName}"),
            rsa,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);

        var cert = req.CreateSelfSigned(validFrom, validTo);
        return cert.Export(X509ContentType.Pfx, password);
    }
}