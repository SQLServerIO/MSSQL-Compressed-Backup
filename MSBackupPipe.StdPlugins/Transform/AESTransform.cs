/*************************************************************************************\
File Name  :  AESTransform.cs
Project    :  MSSQL Compressed Backup

Copyright 2009 Clay Lenhart <clay@lenharts.net>

This file is part of MSSQL Compressed Backup.

MSSQL Compressed Backup is free software: you can redistribute it and/or modify
it under the terms of the GNU General Public License as published by
the Free Software Foundation, either version 3 of the License, or
(at your option) any later version.

MSSQL Compressed Backup is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
GNU General Public License for more details.

You should have received a copy of the GNU General Public License
along with MSSQL Compressed Backup.  If not, see <http://www.gnu.org/licenses/>.

THIS CODE AND INFORMATION IS PROVIDED "AS IS" WITHOUT WARRANTY OF ANY KIND, 
EITHER EXPRESSED OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE IMPLIED 
WARRANTIES OF MERCHANTABILITY AND/OR FITNESS FOR A PARTICULAR PURPOSE.
\*************************************************************************************/

using System.Security.Cryptography;

namespace MSBackupPipe.StdPlugins.Transform
{
    using System;
    using System.Collections.Generic;
    using System.IO;

    namespace MSBackupPipe.StdPlugins
    {
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA1704:IdentifiersShouldBeSpelledCorrectly", MessageId = "AES")]
        public class AESTransform : IBackupTransformer
        {
            byte[] _aesKey = { };
            byte[] _iv = { };
            private static readonly Dictionary<string, ParameterInfo> MBackupParamSchema;
            private static readonly Dictionary<string, ParameterInfo> MRestoreParamSchema;
            static AESTransform()
            {
                MBackupParamSchema = new Dictionary<string, ParameterInfo>(StringComparer.InvariantCultureIgnoreCase)
                {
                    {"key", new ParameterInfo(false, true)}
                };

                MRestoreParamSchema = new Dictionary<string, ParameterInfo>(StringComparer.InvariantCultureIgnoreCase)
                {
                    {"key", new ParameterInfo(false, true)}
                };
            }

            #region IBackupTransformer Members

            public Stream GetBackupWriter(Dictionary<string, List<string>> config, Stream writeToStream)
            {
                ParameterInfo.ValidateParams(MBackupParamSchema, config);

                List<string> sKey;
                if (config.TryGetValue("key", out sKey))
                {
                    if (String.IsNullOrEmpty(sKey[0]))
                    {
                        throw new ArgumentException(string.Format("AES: Unable to read the key: {0}", sKey[0]));
                    }
                }
                else
                {
                    throw new ArgumentException(string.Format("AES: key is required to encrypt data."));
                }

                Console.WriteLine("Encrypter: AES");

                using (var pdb = new PasswordDeriveBytes(sKey[0], new byte[] {0x49, 0x76, 0x61, 0x6e, 0x20, 0x4d, 0x65, 0x64, 0x76, 0x65, 0x64, 0x65, 0x76}))
                {
#pragma warning disable 618
                    _aesKey = pdb.GetBytes(32);
                    _iv = pdb.GetBytes(16);
#pragma warning restore 618
                }

                using (var aes = Rijndael.Create())
                {
                    aes.BlockSize = 128;
                    aes.KeySize = 256;
                    aes.FeedbackSize = 128;
                    aes.Mode = CipherMode.CFB;
                    aes.Padding = PaddingMode.PKCS7;
                    aes.IV = _iv;
                    aes.Key = _aesKey;

                    return new CryptoStream(writeToStream, aes.CreateEncryptor(), CryptoStreamMode.Write);
                }
            }

            public string Name
            {
                get { return "AES"; }
            }

            public Stream GetRestoreReader(Dictionary<string, List<string>> config, Stream readFromStream)
            {
                ParameterInfo.ValidateParams(MRestoreParamSchema, config);

                Console.WriteLine("Decrypter: AES");

                List<string> sKey;
                if (config.TryGetValue("key", out sKey))
                {
                    if (String.IsNullOrEmpty(sKey[0]))
                    {
                        throw new ArgumentException(string.Format("AES: Unable to read the key: {0}", sKey[0]));
                    }
                }
                else
                {
                    throw new ArgumentException(string.Format("AES: key is required to decrypt data."));
                }

                using (var pdb = new PasswordDeriveBytes(sKey[0], new byte[] {0x49, 0x76, 0x61, 0x6e, 0x20, 0x4d, 0x65, 0x64, 0x76, 0x65, 0x64, 0x65, 0x76}))
                {
#pragma warning disable 618
                    _aesKey = pdb.GetBytes(32);
                    _iv = pdb.GetBytes(16);
#pragma warning restore 618
                }

                using (var aes = Rijndael.Create())
                {
                    aes.BlockSize = 128;
                    aes.KeySize = 256;
                    aes.FeedbackSize = 128;
                    aes.Mode = CipherMode.CFB;
                    aes.Padding = PaddingMode.PKCS7;//.None;
                    aes.IV = _iv;
                    aes.Key = _aesKey;

                    return new CryptoStream(readFromStream, aes.CreateDecryptor(), CryptoStreamMode.Read);
                }
            }

            public string CommandLineHelp
            {
                get
                {
                    return "AES Usage: \nAES will encrypt (or decrypt) the data.\nYou must pass in the ascii key to use for encryption and decryption \nExample: \nAES (key = adaabvadre)";
                }
            }

            #endregion
        }
    }
}