/*************************************************************************************\
File Name  :  BlowfishTransform.cs
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
using System.IO;

using FlowGroup.Crypto;

namespace MSBackupPipe.StdPlugins
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA1704:IdentifiersShouldBeSpelledCorrectly", MessageId = "Blowfish")]
    public class BlowfishTransform : IBackupTransformer
    {
        byte[] BLOWFISH_KEY = new byte[] { };
        private static Dictionary<string, ParameterInfo> mBackupParamSchema;
        private static Dictionary<string, ParameterInfo> mRestoreParamSchema;
        static BlowfishTransform()
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
                    throw new ArgumentException(string.Format("Blowfish: Unable to read the key: {0}", sKey[0]));
                }
            }
            else
            {
                throw new ArgumentException(string.Format("Blowfish: key is required to encrypt data."));
            }

            Console.WriteLine(string.Format("Encryptor: Blowfish"));

            BLOWFISH_KEY = Encoding.ASCII.GetBytes(sKey[0]);

            return new FlowGroup.Crypto.BlowfishStream(writeToStream, BLOWFISH_KEY, FlowGroup.Crypto.BlowfishStreamMode.Write);
        }

        public string Name
        {
            get { return "Blowfish"; }
        }

        public Stream GetRestoreReader(Dictionary<string, List<string>> config, Stream readFromStream)
        {
            ParameterInfo.ValidateParams(mRestoreParamSchema, config);

            Console.WriteLine(string.Format("Decrypter: Blowfish"));

            List<string> sKey;
            if (config.TryGetValue("key", out sKey))
            {
                if (String.IsNullOrEmpty(sKey[0]) == true)
                {
                    throw new ArgumentException(string.Format("Blowfish: Unable to read the key: {0}", sKey[0]));
                }
            }
            else
            {
                throw new ArgumentException(string.Format("Blowfish: key is required to decrypt data."));
            }

            Console.WriteLine(string.Format("Blowfish"));

            BLOWFISH_KEY = Encoding.ASCII.GetBytes(sKey[0]);

            return new FlowGroup.Crypto.BlowfishStream(readFromStream, BLOWFISH_KEY, FlowGroup.Crypto.BlowfishStreamMode.Read);
        }

        public string CommandLineHelp
        {
            get
            {
                return @"Blowfish Usage: \nBlowfish will compress (or uncompress) the data. \nYou must pass in the ascii key to use for encryption and decryption \nExample: \nBlowfish (key = adaabvadre)";
            }
        }

        #endregion
    }
}
