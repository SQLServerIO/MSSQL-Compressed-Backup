/*
	Copyright 2009 Clay Lenhart <clay@lenharts.net>


	This file is part of MSSQL Compressed Backup.

    MSSQL Compressed Backup is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

    Foobar is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with Foobar.  If not, see <http://www.gnu.org/licenses/>.
*/

using System;
using System.Collections.Generic;
using System.Text;
using System.IO;

using LZ4;


namespace MSBackupPipe.StdPlugins
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA1704:IdentifiersShouldBeSpelledCorrectly", MessageId = "LZ4")]
    public class LZ4Transform : IBackupTransformer
    {

        private static Dictionary<string, ParameterInfo> mBackupParamSchema;
        private static Dictionary<string, ParameterInfo> mRestoreParamSchema;
        static LZ4Transform()
        {
            mBackupParamSchema = new Dictionary<string, ParameterInfo>(StringComparer.InvariantCultureIgnoreCase);
            mBackupParamSchema.Add("level", new ParameterInfo(false, false));


            mRestoreParamSchema = new Dictionary<string, ParameterInfo>(StringComparer.InvariantCultureIgnoreCase);
        }

        #region IBackupTransformer Members

        public Stream GetBackupWriter(Dictionary<string, List<string>> config, Stream writeToStream)
        {
            ParameterInfo.ValidateParams(mBackupParamSchema, config);

            Console.WriteLine(string.Format("LZ4"));

            return new LZ4.LZ4Stream(writeToStream, System.IO.Compression.CompressionMode.Compress, false, 1048576);
        }

        public string Name
        {
            get { return "LZ4"; }
        }

        public Stream GetRestoreReader(Dictionary<string, List<string>> config, Stream readFromStream)
        {
            ParameterInfo.ValidateParams(mRestoreParamSchema, config);


            Console.WriteLine(string.Format("LZ4"));

            return new LZ4.LZ4Stream(readFromStream, System.IO.Compression.CompressionMode.Decompress, false, 1048576);
        }

        public string CommandLineHelp
        {
            get
            {
                return @"LZ4 Usage: LZ4 will compress (or uncompress) the data.";
            }
        }

        #endregion
    }
}
