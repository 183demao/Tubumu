﻿using System;
using System.Security.Cryptography;
using System.Text;

namespace Tubumu.Modules.Framework.Utilities.Cryptography
{
    /// <summary>
    /// SHA1加密算法
    /// </summary>
    public static class SHA1
    {
        /// <summary>
        /// Encrypt
        /// </summary>
        /// <param name="rawString"></param>
        /// <returns></returns>
        public static String Encrypt(String rawString)
        {
            if(rawString == null)
            {
                throw new ArgumentNullException(nameof(rawString));
            }

            return Convert.ToBase64String(EncryptToByteArray(rawString));
        }

        /// <summary>
        /// EncryptToByteArray
        /// </summary>
        /// <param name="rawString"></param>
        /// <returns></returns>
        public static Byte[] EncryptToByteArray(String rawString)
        {
            if(rawString == null)
            {
                throw new ArgumentNullException(nameof(rawString));
            }

            Byte[] salted = Encoding.UTF8.GetBytes(rawString);
            System.Security.Cryptography.SHA1 hasher = new SHA1Managed();
            Byte[] hashed = hasher.ComputeHash(salted);
            return hashed;
        }
    }
}
