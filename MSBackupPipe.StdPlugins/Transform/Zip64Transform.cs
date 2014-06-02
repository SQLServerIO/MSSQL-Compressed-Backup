/*************************************************************************************\
File Name  :  Zip64DESTransform.cs
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
using System.IO;
using ICSharpCode.SharpZipLib.Zip;

namespace MSBackupPipe.StdPlugins.Transform
{
    public class Zip64Transform : IBackupTransformer
    {
        private static readonly Dictionary<string, ParameterInfo> MBackupParamSchema;
        private static readonly Dictionary<string, ParameterInfo> MRestoreParamSchema;
        static Zip64Transform()
        {
            MBackupParamSchema = new Dictionary<string, ParameterInfo>(StringComparer.InvariantCultureIgnoreCase)
            {
                {"level", new ParameterInfo(false, false)},
                {"filename", new ParameterInfo(false, false)}
            };

            MRestoreParamSchema = new Dictionary<string, ParameterInfo>(StringComparer.InvariantCultureIgnoreCase)
            {
                {"filename", new ParameterInfo(false, false)}
            };
        }

        #region IBackupTransformer Members

        public Stream GetBackupWriter(Dictionary<string, List<string>> config, Stream writeToStream)
        {
            ParameterInfo.ValidateParams(MBackupParamSchema, config);

            var filename = "database.bak";
            var level = 7;

            List<string> sLevel;
            if (config.TryGetValue("level", out sLevel))
            {
                if (!int.TryParse(sLevel[0], out level))
                {
                    throw new ArgumentException(string.Format("zip64: Unable to parse the integer: {0}", sLevel));
                }
            }


            if (level < 1 || level > 9)
            {
                throw new ArgumentException(string.Format("zip64: Level must be between 1 and 9: {0}", level));
            }

            if (config.ContainsKey("filename"))
            {
                filename = config["filename"][0];
            }
            Console.WriteLine("Compressor: zip64 - level = {0}, filename={1}", level, filename);

            return new OneFileZipOutputStream(filename, level, writeToStream);
        }

        public string Name
        {
            get { return "zip64"; }
        }

        public Stream GetRestoreReader(Dictionary<string, List<string>> config, Stream readFromStream)
        {
            ParameterInfo.ValidateParams(MRestoreParamSchema, config);

            string filename = null;
            if (config.ContainsKey("filename"))
            {
                filename = config["filename"][0];
            }

            Console.WriteLine(string.Format("Decompressor: zip64"));

            if (filename == null)
            {
                return new FirstFileZipInputStream(readFromStream);
            }
            return new FindFileZipInputStream(readFromStream, filename);
        }

        public string CommandLineHelp
        {
            get
            {
                return "zip64 Usage: \nzip64 will compress (or uncompress) the data. \nBy default zip64 compresses with level=7, and the internal filename is \ndatabase.bak.  You use a level from 1 to 9 \nExample: \nzip64(level=5) \nand an internal filename like: \nzip64(level=5;filename=model.bak) \nLevel is ignored when restoring a database since the data is being uncompressed. \nzip64 creates a zip file in the new zip64 format to overcome 4 GB uncompressed \nfile limitation.";
            }
        }

        #endregion

        private class OneFileZipOutputStream : ZipOutputStream
        {
            private bool _mDisposed;
            public OneFileZipOutputStream(string internalFilename, int compressionLevel, Stream writeToStream)
                : base(writeToStream)
            {
                var entry = new ZipEntry(internalFilename);

                IsStreamOwner = true;
                PutNextEntry(entry);
                SetLevel(compressionLevel);
            }

            protected override void Dispose(bool disposing)
            {
                if (!_mDisposed)
                {
                    if (disposing)
                    {
                        base.Finish();
                        base.Close();
                    }

                    // There are no unmanaged resources to release, but
                    // if we add them, they need to be released here.
                }
                _mDisposed = true;

                // If it is available, make the call to the
                // base class's Dispose(Boolean) method
                base.Dispose(disposing);
            }
        }

        private class FirstFileZipInputStream : ZipInputStream
        {
            private bool _mDisposed;
            public FirstFileZipInputStream(Stream readFromStream)
                : base(readFromStream)
            {
                IsStreamOwner = true;
                var entry = GetNextEntry();

                if (entry == null)
                {
                    throw new FileNotFoundException("The zip file is empty.");
                }
            }

            protected override void Dispose(bool disposing)
            {
                if (!_mDisposed)
                {
                    if (disposing)
                    {
                        base.Close();
                        // dispose of managed resources
                    }

                    // There are no unmanaged resources to release, but
                    // if we add them, they need to be released here.
                }
                _mDisposed = true;

                // If it is available, make the call to the
                // base class's Dispose(Boolean) method
                base.Dispose(disposing);
            }
        }

        private class FindFileZipInputStream : ZipInputStream
        {
            private bool _mDisposed;
            public FindFileZipInputStream(Stream readFromStream, string filename)
                : base(readFromStream)
            {
                IsStreamOwner = true;

                var entry = GetNextEntry();
                while (!entry.IsFile || entry.Name != filename)
                {
                    entry = GetNextEntry();
                }

                if (entry == null)
                {
                    throw new FileNotFoundException("The zip file not found.");
                }
            }

            protected override void Dispose(bool disposing)
            {
                if (!_mDisposed)
                {
                    if (disposing)
                    {
                        base.Close();
                        // dispose of managed resources
                    }

                    // There are no unmanaged resources to release, but
                    // if we add them, they need to be released here.
                }
                _mDisposed = true;

                // If it is available, make the call to the
                // base class's Dispose(Boolean) method
                base.Dispose(disposing);
            }
        }
    }
}
