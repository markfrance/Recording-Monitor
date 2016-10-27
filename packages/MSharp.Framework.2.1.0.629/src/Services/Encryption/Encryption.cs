using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace System.Security
{
    public class Encryption
    {
        /// <summary>
        /// Generates a public/private key for asymmetric encryption.
        /// </summary>
        public static KeyValuePair<string, string> GenerateAsymmetricKeys()
        {
            var rsa = CreateRSACryptoServiceProvider();

            return new KeyValuePair<string, string>(rsa.ToXmlString(includePrivateParameters: false), rsa.ToXmlString(includePrivateParameters: true));
        }

        static RSACryptoServiceProvider CreateRSACryptoServiceProvider()
        {
            const int PROVIDER_RSA_FULL = 1;
            string container = "SpiderContainer" + Guid.NewGuid();
            var cspParams = new CspParameters(PROVIDER_RSA_FULL)
            {
                KeyContainerName = container,
                Flags = CspProviderFlags.UseMachineKeyStore,
                ProviderName = "Microsoft Strong Cryptographic Provider"
            };
            var rsa = new RSACryptoServiceProvider(cspParams);

            return rsa;
        }

        /// <summary>
        /// Encrypts the specified text with the specified public key.
        /// </summary>
        public static string EncryptAsymmetric(string text, string publicKeyXml)
        {
            if (text.IsEmpty())
                throw new ArgumentNullException("text");

            if (publicKeyXml.IsEmpty())
                throw new ArgumentNullException("publicKeyXml");

            if (text.Length > 117)
            {
                return text.Split(117).Select(p => EncryptAsymmetric(p, publicKeyXml)).ToString("|");
            }
            else
            {
                var rsa = CreateRSACryptoServiceProvider();

                rsa.FromXmlString(publicKeyXml);

                //read plaintext, encrypt it to ciphertext

                var plainbytes = Encoding.UTF8.GetBytes(text);
                var cipherbytes = rsa.Encrypt(plainbytes, fOAEP: false);
                return Convert.ToBase64String(cipherbytes);
            }
        }

        /// <summary>
        /// Decrypts the specified text with the specified public/private key pair.
        /// </summary>
        public static string DecryptAsymmetric(string encodedText, string publicPrivateKeyXml)
        {
            if (encodedText.IsEmpty())
                throw new ArgumentNullException("encodedText");

            if (publicPrivateKeyXml.IsEmpty())
                throw new ArgumentNullException("publicPrivateKeyXml");

            if (encodedText.Contains("|"))
            {
                return encodedText.Split('|').Select(p => DecryptAsymmetric(p, publicPrivateKeyXml)).ToString(string.Empty);
            }
            else
            {
                var rsa = CreateRSACryptoServiceProvider();
                var encodedData = Convert.FromBase64String(encodedText);

                rsa.FromXmlString(publicPrivateKeyXml);

                //read ciphertext, decrypt it to plaintext
                var plain = rsa.Decrypt(encodedData, fOAEP: false);
                return plain.ToString(Encoding.UTF8);
            }
        }

        /// <summary>
        /// Encrypts the specified text with the specified password.
        /// </summary>
        public static string Encrypt(string text, string password)
        {
            if (text.IsEmpty())
                throw new ArgumentNullException("text");

            if (password.IsEmpty())
                throw new ArgumentNullException("password");
            
            var key = new PasswordDeriveBytes(password, Encoding.ASCII.GetBytes(password.Length.ToString()));

            var encryptor = new RijndaelManaged { Padding = PaddingMode.PKCS7 }.CreateEncryptor(key.GetBytes(32), key.GetBytes(16));

            var textData = Encoding.Unicode.GetBytes(text);
            using (var encrypted = new MemoryStream())
            {
                using (var cryptoStream = new CryptoStream(encrypted, encryptor, CryptoStreamMode.Write))
                {
                    cryptoStream.Write(textData, 0, textData.Length);
                    cryptoStream.FlushFinalBlock();

                    return Convert.ToBase64String(encrypted.ToArray());
                }
            }
        }

        /// <summary>
        /// Decrypts the specified encrypted text with the specified password.
        /// </summary>
        public static string Decrypt(string encryptedText, string password)
        {
            using (var key = new PasswordDeriveBytes(password, Encoding.ASCII.GetBytes(password.Length.ToString())))
            {
                var encryptedData = Convert.FromBase64String(encryptedText);

                using (var rijndael = new RijndaelManaged { Padding = PaddingMode.PKCS7 })
                {
                    using (var decryptor = rijndael.CreateDecryptor(key.GetBytes(32), key.GetBytes(16)))
                    {
                        using (var memoryStream = new MemoryStream(encryptedData))
                        {
                            using (var cryptoStream = new CryptoStream(memoryStream, decryptor, CryptoStreamMode.Read))
                            {
                                // The size of decrypted data is unknown, so we allocate a buffer big enough to store encrypted data.
                                // decrypted data is always the same or smaller than encrypted data.
                                var plainText = new byte[encryptedData.Length];
                                int decryptedSize = cryptoStream.Read(plainText, 0, plainText.Length);

                                return Encoding.Unicode.GetString(plainText, 0, decryptedSize);
                            }
                        }
                    }
                }
            }
        }
    }
}