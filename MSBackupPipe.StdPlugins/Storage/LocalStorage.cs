/*************************************************************************************\
File Name  :  LocalStorage.cs
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

namespace MSBackupPipe.StdPlugins.Storage
{
    public class LocalStorage : IBackupStorage
    {
        private List<bool> mDeleteOnAbort = new List<bool>();
        private List<FileInfo> mFileInfosToDeleteOnAbort = new List<FileInfo>();

        private static Dictionary<string, ParameterInfo> mBackupParamSchema;
        static LocalStorage()
        {
            mBackupParamSchema = new Dictionary<string, ParameterInfo>(StringComparer.InvariantCultureIgnoreCase);
            mBackupParamSchema.Add("path", new ParameterInfo(true, true));
        }

        #region IBackupStorage Members

        public string Name
        {
            get { return "local"; }
        }

        public int GetNumberOfDevices(Dictionary<string, List<string>> config)
        {
            ParameterInfo.ValidateParams(mBackupParamSchema, config);

            return config["path"].Count;
        }

        public Stream[] GetBackupWriter(Dictionary<string, List<string>> config)
        {
            mDeleteOnAbort.Clear();
            mFileInfosToDeleteOnAbort.Clear();

            ParameterInfo.ValidateParams(mBackupParamSchema, config);

            List<string> paths = config["path"];
            List<FileInfo> fileInfos = paths.ConvertAll<FileInfo>(delegate(string path)
            {
                return new FileInfo(path);
            });

            // initialize to false:
            mDeleteOnAbort = new List<bool>(new bool[fileInfos.Count]);
            mFileInfosToDeleteOnAbort = fileInfos;

            for (int i = 0; i < mDeleteOnAbort.Count; i++)
            {
                mDeleteOnAbort[i] = !fileInfos[i].Exists;
            }

            Console.WriteLine(string.Format("storage:"));
            foreach (FileInfo fi in fileInfos)
            {
                Console.WriteLine(string.Format("path={0}", fi.FullName));
            }

            List<Stream> results = new List<Stream>(fileInfos.Count);
            //TODO: preallocate file to speed up writes and cut down on fragmentations
            //TODO: add unbuffered IO to speed up writes and cut down on memory usage
            foreach (FileInfo fi in fileInfos)
            {
                results.Add(fi.Open(FileMode.Create));
            }

            return results.ToArray();
        }

        public Stream[] GetRestoreReader(Dictionary<string, List<string>> config, out long estimatedTotalBytes)
        {
            mDeleteOnAbort.Clear();
            mFileInfosToDeleteOnAbort.Clear();

            ParameterInfo.ValidateParams(mBackupParamSchema, config);

            List<string> paths = config["path"];
            List<FileInfo> fileInfos = paths.ConvertAll<FileInfo>(delegate(string path)
            {
                return new FileInfo(path);
            });

            long combinedSize = 0;

            Console.WriteLine(string.Format("storage:"));
            foreach (FileInfo fi in fileInfos)
            {
                Console.WriteLine(string.Format("path={0}", fi.FullName));
                combinedSize += fi.Length;
            }

            estimatedTotalBytes = combinedSize;

            List<Stream> results = new List<Stream>(fileInfos.Count);
            foreach (FileInfo fi in fileInfos)
            {
                results.Add(fi.Open(FileMode.Open));
            }
            return results.ToArray();
        }

        public string CommandLineHelp
        {
            get
            {
                return "local Usage: \nThis is a plugin to store or read a backup file. \nTo reference a file, enter: \nlocal(path=<file>) \nmsbp.exe has an alias for the local plugin.  If it begins with file://, it is \nconverted to the 'local' plugin equivalent.  file:///c:\\model.bak is converted \nto local(path=c:\\model.bak).";
            }
        }

        public void CleanupOnAbort()
        {
            for (int i = 0; i < mFileInfosToDeleteOnAbort.Count; i++)
            {
                FileInfo fi = mFileInfosToDeleteOnAbort[i];
                bool deleteOnAbort = mDeleteOnAbort[i];
                if (deleteOnAbort && fi != null)
                {
                    fi.Delete();
                }
            }
            mFileInfosToDeleteOnAbort = null;
        }
        #endregion
    }
}
