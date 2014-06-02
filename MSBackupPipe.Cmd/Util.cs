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

namespace MSBackupPipe.Cmd
{
    internal static class Util
    {
        //basic error reporter
        public static void WriteError(Exception e)
        {
            VDIErrorHelp(e);
            Console.WriteLine();
            //TOOO: cut down on verbosity in release builds
            //there will always be multiple nested exeptions
            //and it makes it almost impossible to figure out 
            //what the real error is
        }
        public static void VDIErrorHelp(Exception e)
        {
            Console.WriteLine(e.Message);
#if DEBUG
//            if (e.Message.IndexOf("-2139684860", StringComparison.OrdinalIgnoreCase) <= 0)
//            {
            Console.WriteLine(e.GetType().FullName);
            Console.WriteLine();
            Console.WriteLine(e.StackTrace);
            Console.WriteLine();
            if (e.InnerException != null)
            {
                WriteError(e.InnerException);
            }
//            }
#endif
            //track error messages thrown from VDI and give some kind of guideance
            //this only tracks the hex value and not the decimal value that can come back as well.
            if (e.Message.IndexOf("x80070002", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                Console.WriteLine("Please validate sqlvdi.dll is registered properly.");
            }
            if (e.Message.IndexOf("x80770003", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                Console.WriteLine("waiting for SQL Server to respond to backup/restore request, but did not receive any response.");
            }
            if (e.Message.IndexOf("x80070004", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                Console.WriteLine("An abort request is preventing anything except an termination action.");
            }
            if (e.Message.IndexOf("x80070005", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                Console.WriteLine("Error with security or permissions.\nPlease run with administrator permissions at the OS level.");
            }
            if (e.Message.IndexOf("x80070006", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                Console.WriteLine("An invalid parameter was supplied.\nPossible mismatch with SQL Server version and VDI Version please validate SQLVDI.dll.");
            }
            if (e.Message.IndexOf("x80070007", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                Console.WriteLine("Failed to recognize the SQL Server instance name.\nPossible mismatch with SQL Server version and VDI Version please validate SQLVDI.dll.");
            }
            if (e.Message.IndexOf("x80070009", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                Console.WriteLine("The requested configuration is invalid.\nPossible mismatch with SQL Server version and VDI Version please validate SQLVDI.dll.");
            }
            if (e.Message.IndexOf("x8007000a", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                Console.WriteLine("Out of memory.\nPlease lower the number of buffers or MAXTRANSFERSIZE");
            }
            if (e.Message.IndexOf("x8007000b", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                Console.WriteLine("Unexpected internal error.");
            }
            if (e.Message.IndexOf("x8007000c", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                Console.WriteLine("Protocol error.\nThis is an unexpected error.");
            }
            if (e.Message.IndexOf("x8007000d", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                Console.WriteLine("All devices are open.\nThis is an unexpected error.");
            }
            if (e.Message.IndexOf("x8007000e", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                Console.WriteLine("The object is now closed.\nThis is an unexpected error.");
            }
            if (e.Message.IndexOf("x8007000f", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                Console.WriteLine("The resource is busy.\nThis is an unexpected error.");
            }
        }
    }
}