/*************************************************************************************\
File Name  :  Bzip2Transform.cs
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

using ICSharpCode.SharpZipLib.BZip2;


namespace MSBackupPipe.StdPlugins
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA1704:IdentifiersShouldBeSpelledCorrectly", MessageId = "Bzip")]
    public class Bzip2Transform : IBackupTransformer
    {
        private static Dictionary<string, ParameterInfo> mBackupParamSchema;
        private static Dictionary<string, ParameterInfo> mRestoreParamSchema;
        static Bzip2Transform()
        {
            mBackupParamSchema = new Dictionary<string, ParameterInfo>(StringComparer.InvariantCultureIgnoreCase);
            mBackupParamSchema.Add("level", new ParameterInfo(false, false));

            mRestoreParamSchema = new Dictionary<string, ParameterInfo>(StringComparer.InvariantCultureIgnoreCase);
        }

        #region IBackupTransformer Members

        public Stream GetBackupWriter(Dictionary<string, List<string>> config, Stream writeToStream)
        {
            ParameterInfo.ValidateParams(mBackupParamSchema, config);

            int level = 1;
            List<string> sLevel;
            if (config.TryGetValue("level", out sLevel))
            {
                if (!int.TryParse(sLevel[0], out level))
                {
                    throw new ArgumentException(string.Format("bzip2: Unable to parse the integer: {0}", sLevel));
                }
            }

            if (level < 1 || level > 9)
            {
                throw new ArgumentException(string.Format("bzip2: Level must be between 1 and 9: {0}", level));
            }

            Console.WriteLine(string.Format("Compressor: bzip2 - level = {0}", level));

            return new BZip2OutputStream(writeToStream, level);
        }

        public string Name
        {
            get { return "bzip2"; }
        }

        public Stream GetRestoreReader(Dictionary<string, List<string>> config, Stream readFromStream)
        {
            ParameterInfo.ValidateParams(mRestoreParamSchema, config);

            Console.WriteLine(string.Format("Decompressor: bzip2"));

            return new BZip2InputStream(readFromStream);
        }

        public string CommandLineHelp
        {
            get
            {
                return @"bzip2 Usage: \nbzip2 will compress (or uncompress) the data. \nBy default bzip2 compresses with level=1.  You use a level from 1 to 9 \n Example: \nbzip(level=5) \nLevel is ignored when restoring a database since the data is being uncompressed.";
            }
        }

        #endregion
    }
}
