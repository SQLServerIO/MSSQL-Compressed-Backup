/*************************************************************************************\
File Name  :  Util.cs
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

namespace MSBackupPipe.Cmd
{
    internal static class Util
    {
        //basic error reporter
        public static void WriteError(Exception e)
        {
            Console.WriteLine(e.Message);
            Console.WriteLine();
            //cut down on verbosity in release builds
            //there will always be multiple nested exeptions
            //and it makes it almost impossible to figure out 
            //what the real error is
            Console.WriteLine(e.GetType().FullName);
            Console.WriteLine(e.StackTrace);
            if (e.InnerException != null)
            {
                WriteError(e.InnerException);
            }
        }
    }
}