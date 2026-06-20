using System;

namespace KicsitLibrary.Core.Entities
{
    public class SystemSettings : EntityBase
    {
        private string _value = string.Empty;
        private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("KicsitLibraryEntropyRule");

        public string Key { get; set; } = string.Empty;
        
        public string Value
        {
            get
            {
                if (IsSensitiveKey(Key))
                {
                    return Decrypt(_value);
                }
                return _value;
            }
            set
            {
                if (IsSensitiveKey(Key))
                {
                    _value = Encrypt(value);
                }
                else
                {
                    _value = value;
                }
            }
        }

        public string? Description { get; set; }
        public string Group { get; set; } = "General";
        
        public int? UpdatedByUserId { get; set; }
        public virtual User? UpdatedByUser { get; set; }

        private static bool IsSensitiveKey(string key)
        {
            if (string.IsNullOrEmpty(key)) return false;
            return key.Equals("SmtpPassword", StringComparison.OrdinalIgnoreCase) ||
                   key.Equals("SmtpUser", StringComparison.OrdinalIgnoreCase) ||
                   key.Contains("ApiKey", StringComparison.OrdinalIgnoreCase) ||
                   key.Contains("Token", StringComparison.OrdinalIgnoreCase) ||
                   key.Contains("Secret", StringComparison.OrdinalIgnoreCase);
        }

        private static string Encrypt(string plainText)
        {
            if (string.IsNullOrEmpty(plainText)) return plainText;
            try
            {
                if (IsBase64String(plainText))
                {
                    try
                    {
                        var testBytes = Convert.FromBase64String(plainText);
                        ProtectedData.Unprotect(testBytes, Entropy, DataProtectionScope.LocalMachine);
                        return plainText;
                    }
                    catch { }
                }
                var plainBytes = Encoding.UTF8.GetBytes(plainText);
                var encryptedBytes = ProtectedData.Protect(plainBytes, Entropy, DataProtectionScope.LocalMachine);
                return Convert.ToBase64String(encryptedBytes);
            }
            catch
            {
                return plainText;
            }
        }

        private static string Decrypt(string cipherText)
        {
            if (string.IsNullOrEmpty(cipherText)) return cipherText;
            try
            {
                var cipherBytes = Convert.FromBase64String(cipherText);
                var decryptedBytes = ProtectedData.Unprotect(cipherBytes, Entropy, DataProtectionScope.LocalMachine);
                return Encoding.UTF8.GetString(decryptedBytes);
            }
            catch
            {
                return cipherText;
            }
        }

        private static bool IsBase64String(string base64String)
        {
            if (string.IsNullOrEmpty(base64String) || base64String.Length % 4 != 0
               || base64String.Contains(" ") || base64String.Contains("\t") || base64String.Contains("\r") || base64String.Contains("\n"))
                return false;

            try
            {
                Convert.FromBase64String(base64String);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
