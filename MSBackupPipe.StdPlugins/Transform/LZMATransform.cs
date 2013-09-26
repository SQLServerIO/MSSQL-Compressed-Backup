/*
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
*/

using System;
using System.Collections.Generic;
using System.Text;
using System.IO;


using SharpCompress;


namespace MSBackupPipe.StdPlugins
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA1704:IdentifiersShouldBeSpelledCorrectly", MessageId = "LZMA")]
    public class LZMATransform : IBackupTransformer
    {

        private static Dictionary<string, ParameterInfo> mBackupParamSchema;
        private static Dictionary<string, ParameterInfo> mRestoreParamSchema;
        static LZMATransform()
        {
            mBackupParamSchema = new Dictionary<string, ParameterInfo>(StringComparer.InvariantCultureIgnoreCase);
            mBackupParamSchema.Add("level", new ParameterInfo(false, false));


            mRestoreParamSchema = new Dictionary<string, ParameterInfo>(StringComparer.InvariantCultureIgnoreCase);
        }

        #region IBackupTransformer Members

        public Stream GetBackupWriter(Dictionary<string, List<string>> config, Stream writeToStream)
        {
            ParameterInfo.ValidateParams(mBackupParamSchema, config);

            int level = 9;
            List<string> sLevel;
            if (config.TryGetValue("level", out sLevel))
            {
                if (!int.TryParse(sLevel[0], out level))
                {
                    throw new ArgumentException(string.Format("LZMA: Unable to parse the integer: {0}", sLevel));
                }
            }

            if (level < 1 || level > 9)
            {
                throw new ArgumentException(string.Format("LZMA: Level must be between 1 and 9: {0}", level));
            }



            Console.WriteLine(string.Format("LZMA: level = {0}", level));

            var lzmaep = new SharpCompress.Compressor.PPMd.PpmdProperties();
            return new SharpCompress.Compressor.BZip2.BZip2Stream(writeToStream,SharpCompress.Compressor.CompressionMode.Compress,true,false);
        }

        public string Name
        {
            get { return "LZMA"; }
        }

        public Stream GetRestoreReader(Dictionary<string, List<string>> config, Stream readFromStream)
        {
            ParameterInfo.ValidateParams(mRestoreParamSchema, config);


            Console.WriteLine(string.Format("LZMA"));

            var lzmaep = new SharpCompress.Compressor.LZMA.LzmaEncoderProperties(false);
            return new SharpCompress.Compressor.LZMA.LzmaStream(lzmaep, true, readFromStream);
            
        }

        public string CommandLineHelp
        {
            get
            {
                return @"LZMA Usage:
LZMA will compress (or uncompress) the data.
By default LZMA compresses with level=9.  You use a level from 1 to 9, for 
example:
    LZMA(level=5)
Level is ignored when restoring a database since the data is being uncompressed.";
            }
        }

        #endregion
    }
}
