/*************************************************************************************\
File Name  :  IUpdateNotification.cs
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

namespace MSBackupPipe.Common
{
    public interface IUpdateNotification
    {
        /// <summary>
        /// Called several times when connecting to the various components
        /// </summary>
        void OnConnecting(string message);

        /// <summary>
        /// Called when everything is ready to backup or restore, and 
        /// nothing much can go wrong after this point.
        /// </summary>
        void OnStart();

        /// <summary>
        /// Updates the percent complete.
        /// </summary>
        /// <param name="percentComplete">A number from 0.0 to 1.0</param>
        /// <param name="bytes"></param>
        /// <returns>The *suggested* duration to be notified again</returns>
        TimeSpan OnStatusUpdate(float percentComplete, long bytes);
    }
}
