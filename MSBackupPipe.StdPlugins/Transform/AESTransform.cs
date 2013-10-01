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

using System;
using System.Collections.Generic;
using System.Text;
using System.Security.Cryptography;

namespace MSBackupPipe.StdPlugins.Transform
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using System.IO;

    namespace MSBackupPipe.StdPlugins
    {
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA1704:IdentifiersShouldBeSpelledCorrectly", MessageId = "AES")]
        public class AESTransform : IBackupTransformer
        {
            byte[] AES_KEY = new byte[] { };
            byte[] IV = new byte[] { };
            private static Dictionary<string, ParameterInfo> mBackupParamSchema;
            private static Dictionary<string, ParameterInfo> mRestoreParamSchema;
            static AESTransform()
            {
                mBackupParamSchema = new Dictionary<string, ParameterInfo>(StringComparer.InvariantCultureIgnoreCase);
                mBackupParamSchema.Add("key", new ParameterInfo(false, true));

                mRestoreParamSchema = new Dictionary<string, ParameterInfo>(StringComparer.InvariantCultureIgnoreCase);
                mRestoreParamSchema.Add("key", new ParameterInfo(false, true));
            }

            #region IBackupTransformer Members

            public Stream GetBackupWriter(Dictionary<string, List<string>> config, Stream writeToStream)
            {
                ParameterInfo.ValidateParams(mBackupParamSchema, config);

                List<string> sKey;
                if (config.TryGetValue("key", out sKey))
                {
                    if (String.IsNullOrEmpty(sKey[0]) == true)
                    {
                        throw new ArgumentException(string.Format("AES: Unable to read the key: {0}", sKey[0]));
                    }
                }
                else
                {
                    throw new ArgumentException(string.Format("AES: key is required to encrypt data."));
                }

                Console.WriteLine("Encrypter: AES");

                PasswordDeriveBytes pdb = new PasswordDeriveBytes(sKey[0], new byte[] { 0x49, 0x76, 0x61, 0x6e, 0x20, 0x4d, 0x65, 0x64, 0x76, 0x65, 0x64, 0x65, 0x76 });

                AES_KEY = pdb.GetBytes(32);
                IV = pdb.GetBytes(16);

                Rijndael AES = Rijndael.Create();
                AES.BlockSize = 128;
                AES.KeySize = 256;
                AES.FeedbackSize = 128;
                AES.Mode = CipherMode.CFB;
                AES.Padding = PaddingMode.PKCS7;
                AES.IV = IV;
                AES.Key = AES_KEY;

                return new CryptoStream(writeToStream, AES.CreateEncryptor(), CryptoStreamMode.Write);
            }

            public string Name
            {
                get { return "AES"; }
            }

            public Stream GetRestoreReader(Dictionary<string, List<string>> config, Stream readFromStream)
            {
                ParameterInfo.ValidateParams(mRestoreParamSchema, config);

                Console.WriteLine("Decrypter: AES");

                List<string> sKey;
                if (config.TryGetValue("key", out sKey))
                {
                    if (String.IsNullOrEmpty(sKey[0]) == true)
                    {
                        throw new ArgumentException(string.Format("AES: Unable to read the key: {0}", sKey[0]));
                    }
                }
                else
                {
                    throw new ArgumentException(string.Format("AES: key is required to decrypt data."));
                }

                PasswordDeriveBytes pdb = new PasswordDeriveBytes(sKey[0], new byte[] { 0x49, 0x76, 0x61, 0x6e, 0x20, 0x4d, 0x65, 0x64, 0x76, 0x65, 0x64, 0x65, 0x76 });

                AES_KEY = pdb.GetBytes(32);
                IV = pdb.GetBytes(16);

                Rijndael AES = Rijndael.Create();

                AES.BlockSize = 128;
                AES.KeySize = 256;
                AES.FeedbackSize = 128;
                AES.Mode = CipherMode.CFB;
                AES.Padding = PaddingMode.PKCS7;//.None;
                AES.IV = IV;
                AES.Key = AES_KEY;

                return new CryptoStream(readFromStream, AES.CreateDecryptor(), CryptoStreamMode.Read);
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