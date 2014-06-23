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
using System.IO;
using System.Linq;
using MSBackupPipe.StdPlugins.Streams;

namespace MSBackupPipe.StdPlugins.Storage
{
    public class LocalStorage : IBackupStorage
    {
        private List<bool> _mDeleteOnAbort = new List<bool>();
        private List<FileInfo> _mFileInfosToDeleteOnAbort = new List<FileInfo>();

        private static readonly Dictionary<string, ParameterInfo> MBackupParamSchema;
        static LocalStorage()
        {
            MBackupParamSchema = new Dictionary<string, ParameterInfo>(StringComparer.InvariantCultureIgnoreCase)
            {
                {"path", new ParameterInfo(true, true)}
            };
        }

        #region IBackupStorage Members

        public string Name
        {
            get { return "local"; }
        }

        public int GetNumberOfDevices(Dictionary<string, List<string>> config)
        {
            ParameterInfo.ValidateParams(MBackupParamSchema, config);

            return config["path"].Count;
        }

        public Stream[] GetBackupWriter(Dictionary<string, List<string>> config)
        {
            _mDeleteOnAbort.Clear();
            _mFileInfosToDeleteOnAbort.Clear();

            ParameterInfo.ValidateParams(MBackupParamSchema, config);

            var paths = config["path"];
            var fileInfos = paths.ConvertAll(path => new FileInfo(path));

            // initialize to false:
            _mDeleteOnAbort = new List<bool>(new bool[fileInfos.Count]);
            _mFileInfosToDeleteOnAbort = fileInfos;

            for (var i = 0; i < _mDeleteOnAbort.Count; i++)
            {
                _mDeleteOnAbort[i] = !fileInfos[i].Exists;
            }

            Console.WriteLine(string.Format("storage:"));
            foreach (var fi in fileInfos)
            {
                Console.WriteLine("path={0}", fi.FullName);
            }

            var results = new List<Stream>(fileInfos.Count);
            //my unbuffered filestream.
            results.AddRange(fileInfos.Select(fi => new UnBufferedFileStream(fi.FullName, FileAccess.Write, (1048576 * 32))));

            //NULL stream this is just for testing speed of data flow without writing to disk.
            //results.AddRange(fileInfos.Select(fi => new NullStream()));
            
            //native .net FileStream uses OS buffer
            //results.AddRange(fileInfos.Select(fi => fi.Open(FileMode.Create)));

            //TODO: preallocate file to speed up writes and cut down on fragmentations
            //TODO: add unbuffered IO to speed up writes and cut down on memory usage

            return results.ToArray();
        }

        public Stream[] GetRestoreReader(Dictionary<string, List<string>> config, out long estimatedTotalBytes)
        {
            _mDeleteOnAbort.Clear();
            _mFileInfosToDeleteOnAbort.Clear();

            ParameterInfo.ValidateParams(MBackupParamSchema, config);

            var paths = config["path"];
            var fileInfos = paths.ConvertAll(path => new FileInfo(path));

            long combinedSize = 0;

            Console.WriteLine(string.Format("storage:"));
            foreach (var fi in fileInfos)
            {
                Console.WriteLine("path={0}", fi.FullName);
                combinedSize += fi.Length;
            }

            estimatedTotalBytes = combinedSize;

            var results = new List<Stream>(fileInfos.Count);
            results.AddRange(fileInfos.Select(fi => fi.Open(FileMode.Open)));
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
            for (var i = 0; i < _mFileInfosToDeleteOnAbort.Count; i++)
            {
                var fi = _mFileInfosToDeleteOnAbort[i];
                var deleteOnAbort = _mDeleteOnAbort[i];
                if (deleteOnAbort && fi != null)
                {
                    fi.Delete();
                }
            }
            _mFileInfosToDeleteOnAbort = null;
        }
        #endregion
    }
}
