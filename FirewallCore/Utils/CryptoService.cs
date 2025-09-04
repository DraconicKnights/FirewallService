using System.Security.Cryptography;
using System.Text;

namespace FirewallCore.Utils;

public class CryptoService
{
        private readonly byte[] _key;
        private readonly byte[] _iv;

        public CryptoService(string keyString, string ivString)
        {
            if (string.IsNullOrWhiteSpace(keyString) || string.IsNullOrWhiteSpace(ivString))
                throw new ArgumentException("Crypto key and IV must be provided.");

            try
            {
                _key = Convert.FromBase64String(keyString.Trim());
                _iv = Convert.FromBase64String(ivString.Trim());
            }
            catch (FormatException e)
            {
                FirewallServiceProvider.Instance.GetLogger.LogException(e);
            }
            

            if (!(_key!.Length == 16 || _key.Length == 24 || _key.Length == 32))
                throw new ArgumentException("Invalid key length; must be 16/24/32 bytes.");
            if (_iv!.Length != 16)
                throw new ArgumentException("Invalid IV length; must be 16 bytes.");
        }

        public string Encrypt(string plainText)
        {
            if (string.IsNullOrEmpty(plainText))
                return string.Empty;

            using var aes = Aes.Create();
            aes.KeySize = 256;
            aes.Key = _key;
            aes.IV  = _iv;

            using var ms = new MemoryStream();
            using var cs = new CryptoStream(ms, aes.CreateEncryptor(), CryptoStreamMode.Write);
            using var sw = new StreamWriter(cs, Encoding.UTF8);
            sw.Write(plainText);
            sw.Flush();
            cs.FlushFinalBlock();
            
            return Convert.ToBase64String(ms.ToArray());
        }

        public string Decrypt(string cipherText)
        {
            if (string.IsNullOrEmpty(cipherText))
                return string.Empty;

            var buffer = Convert.FromBase64String(cipherText);
            using var aes = Aes.Create();
            aes.KeySize = 256;
            aes.Key = _key;
            aes.IV  = _iv;

            using var ms = new MemoryStream(buffer);
            using var cs = new CryptoStream(ms, aes.CreateDecryptor(), CryptoStreamMode.Read);
            using var sr = new StreamReader(cs, Encoding.UTF8);
            return sr.ReadToEnd();
        }
}