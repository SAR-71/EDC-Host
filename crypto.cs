using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Collections.Generic;

namespace EDC_Host
{
    public class crypto
    {
        #region Settings

            private int      _iterations = 2;
            private int      _keySize = 256;
            private byte[]   _stopByte = { 254, 255 };

            private string   _hash   =   "SHA1";
            private string   _salt; 
            private string   _vector;

            private string   _aesPass;
            private byte     _xorPass;

        #endregion

        #region Interna

        public byte[] Encrypt(byte[] value, string password)
        {
            return Encrypt<AesManaged>(value, password);
        }
        public byte[] Encrypt<T>(byte[] valueBytes, string password)
                where T : SymmetricAlgorithm, new()
        {
            byte[] vectorBytes = Encoding.ASCII.GetBytes(_vector);
            byte[] saltBytes = Encoding.ASCII.GetBytes(_salt);

            byte[] encrypted;
            using (T cipher = new T())
            {
                PasswordDeriveBytes _passwordBytes =
                    new PasswordDeriveBytes(password, saltBytes, _hash, _iterations);
                byte[] keyBytes = _passwordBytes.GetBytes(_keySize / 8);

                cipher.Mode = CipherMode.CBC;

                using (ICryptoTransform encryptor = cipher.CreateEncryptor(keyBytes, vectorBytes))
                {
                    using (MemoryStream to = new MemoryStream())
                    {
                        using (CryptoStream writer = new CryptoStream(to, encryptor, CryptoStreamMode.Write))
                        {
                            writer.Write(valueBytes, 0, valueBytes.Length);
                            writer.FlushFinalBlock();
                            encrypted = to.ToArray();
                        }
                    }
                }
                cipher.Clear();
            }
            return encrypted;
        }

        public byte[] Decrypt(byte[] value, string password)
        {
            return Decrypt<AesManaged>(value, password);
        }
        public byte[] Decrypt<T>(byte[] value, string password) where T : SymmetricAlgorithm, new()
        {
            byte[] vectorBytes = Encoding.ASCII.GetBytes(_vector);
            byte[] saltBytes = Encoding.ASCII.GetBytes(_salt);
            byte[] valueBytes = value;
            byte[] decrypted;
            int decryptedByteCount = 0;

            using (T cipher = new T())
            {
                PasswordDeriveBytes _passwordBytes = new PasswordDeriveBytes(password, saltBytes, _hash, _iterations);
                byte[] keyBytes = _passwordBytes.GetBytes(_keySize / 8);

                cipher.Mode = CipherMode.CBC;



                try
                {
                    using (ICryptoTransform decryptor = cipher.CreateDecryptor(keyBytes, vectorBytes))
                    {
                        using (MemoryStream from = new MemoryStream(valueBytes))
                        {
                            using (CryptoStream reader = new CryptoStream(from, decryptor, CryptoStreamMode.Read))
                            {
                                decrypted = new byte[valueBytes.Length];
                                decryptedByteCount = reader.Read(decrypted, 0, decrypted.Length);


                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    throw ex;
                }

                cipher.Clear();
            }

            List<byte> output = new List<Byte>();
            bool flag = false;
            //Säubern von Decrypted:
            for (int i = 0; i < decrypted.Length; i++)
            {
                if ((decrypted[i] == 255) && flag)
                {
                    output.RemoveAt(i - 1);
                    break;
                }
                else
                    flag = false;

                if (decrypted[i] == 254)
                    flag = true;


                output.Add(decrypted[i]);

            }


            return output.ToArray();
        }


        private byte[] addStopBytes(byte[] input)
        {
            List<byte> output = new List<byte>();
            output.AddRange(input);
            output.AddRange(_stopByte);

            return output.ToArray();
        }
        private byte[] XOR(byte[] text)
        {
            byte encryptedByte;
            byte plainByte;
            List<byte> output = new List<byte>();

            for (int i = 0; i < text.Length; i++)
            {
                plainByte = Convert.ToByte(text[i]);
                encryptedByte = (byte)(plainByte ^ _xorPass);
                output.Add(encryptedByte);
            }

            return output.ToArray();
        }

        #endregion

        //Constructor
        public crypto(string salt, string vector, string aesPass, byte xorPass)
        {
            _salt = salt;
            _vector = vector;
            _aesPass = aesPass;
            _xorPass = xorPass;
        }

        #region Interface
        public byte[] encyrptMessage(byte[] text)
        {
            byte[] encrypted = addStopBytes(text);
            encrypted = XOR(encrypted);
            encrypted = Encrypt<RijndaelManaged>(encrypted, _aesPass);
            encrypted = XOR(encrypted);

            return encrypted;
        }
        public byte[] encyrptMessage(string text)
        {
            byte[] encrypted = Encoding.ASCII.GetBytes(text);
            encrypted = XOR(encrypted);
            encrypted = Encrypt<RijndaelManaged>(addStopBytes(encrypted), _aesPass);
            encrypted = XOR(encrypted);

            return encrypted;
        }
        public string decryptMessage(string text)
        {
            byte[] decrypted = Encoding.ASCII.GetBytes(text);
            try
            {
                decrypted = XOR(decrypted);
                decrypted = Decrypt<RijndaelManaged>(decrypted, _aesPass);
                decrypted = XOR(decrypted);
            }
            catch { return String.Empty; };

            return System.Text.Encoding.ASCII.GetString(decrypted);
        }
        public string decryptMessage(byte[] text)
        {
            byte[] decrypted = text;
            try
            {
                decrypted = XOR(decrypted);
                decrypted = Decrypt<RijndaelManaged>(decrypted, _aesPass);
                decrypted = XOR(decrypted);
            }
            catch { return String.Empty; };

            return System.Text.Encoding.ASCII.GetString(decrypted);
        }

        #endregion

    }
}