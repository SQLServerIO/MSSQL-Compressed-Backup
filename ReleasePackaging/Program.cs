/*************************************************************************************\
File Name  :  Program.cs
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

using ICSharpCode.SharpZipLib.Zip;

namespace ReleasePackaging
{
    /// <summary>
    /// Copies files needed to be packaged with the application and add them into a new zip file.
    /// </summary>
    class Program
    {
        static int Main(string[] args)
        {
            try
            {
                const string BETA_STRING = ""; // "beta";
                const string VERSION_STRING = "2.0";

                DirectoryInfo solutionDir = new DirectoryInfo(args[0]);

                if (!solutionDir.Exists)
                {
                    throw new ArgumentException(string.Format("The solution directory, {0}, does not exist."));
                }

                string platformName = args[1];
                string configurationName = args[2];

                if (configurationName != "Release")
                {
                    throw new ArgumentException(string.Format("Only include this project in the release build.  Currently building {0}", configurationName));
                }


                DirectoryInfo dirToCompiledFiles = new DirectoryInfo(Path.Combine(solutionDir.FullName, @"MSBackupPipe.Cmd\bin\Release"));

                if (!dirToCompiledFiles.Exists)
                {
                    throw new ArgumentException(string.Format("The directory to package does not exist: {0}", dirToCompiledFiles));
                }



                DirectoryInfo dirToZip = new DirectoryInfo(solutionDir.FullName + @"obj\dirToZip");
                if (dirToZip.Exists)
                {
                    dirToZip.Delete(true);
                }

                dirToZip.Create();


                string dirName = string.Format("MSSQLCompressedBackup_{3}_{1:yyyyMMdd}{2}_{0}", platformName, DateTime.UtcNow, BETA_STRING, VERSION_STRING);
                string zipSubDirPath = dirToZip.CreateSubdirectory(dirName).FullName;



                foreach (FileInfo file in dirToCompiledFiles.GetFiles())
                {
                    if (!file.Name.Contains(".vshost."))
                    {
                        file.CopyTo(Path.Combine(zipSubDirPath, file.Name));
                    }
                }

                string redistArch = platformName == "x86" ? "x86" : "x64";
                string redistPath = Path.Combine(solutionDir.FullName, string.Format(@"Binaries\{0}\Microsoft.VC80.CRT", redistArch));
                DirectoryInfo redistDir = new DirectoryInfo(redistPath);
                string redistSubDirPath = new DirectoryInfo(zipSubDirPath).CreateSubdirectory("Microsoft.VC80.CRT").FullName;

                foreach (FileInfo file in redistDir.GetFiles())
                {
                    file.CopyTo(Path.Combine(redistSubDirPath, file.Name));
                }

                string destFilename = dirName + ".zip";
                FileInfo destinationZipFile = new FileInfo(solutionDir.FullName + @"\out\" + destFilename);

                if (!destinationZipFile.Directory.Exists)
                {
                    destinationZipFile.Directory.Create();
                }

                if (destinationZipFile.Exists)
                {
                    destinationZipFile.Delete();
                }

                FastZip fZip = new FastZip();
                fZip.CreateZip(destinationZipFile.FullName, dirToZip.FullName, true, null);

                Console.Write("Built: ");
                Console.WriteLine(destinationZipFile.FullName);

                return 0;
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                Console.WriteLine(e.StackTrace);

                return -1;
            }
        }
    }
}
