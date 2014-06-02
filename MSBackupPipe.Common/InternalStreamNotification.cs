/*************************************************************************************\
File Name  :  InternalStreamNotification.cs
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
using System.Linq;
using MSBackupPipe.StdPlugins;

namespace MSBackupPipe.Common
{
    internal class InternalStreamNotification : IStreamNotification
    {
        private readonly IUpdateNotification _mExternalNotification;
        private long _mEstimatedBytes = 1;
        private readonly IList<long> _mBytesProcessed = new List<long>();
        private int _mLastThreadIdUsed = -1;

        public InternalStreamNotification(IUpdateNotification notification)
        {
            _mExternalNotification = notification;
        }

        public long EstimatedBytes
        {
            get
            {
                lock (this)
                {
                    return _mEstimatedBytes;
                }
            }
            set
            {
                lock (this)
                {
                    _mEstimatedBytes = Math.Max(1, value);
                }
            }
        }

        public int GetThreadId()
        {
            lock (this)
            {
                _mLastThreadIdUsed++;
                _mBytesProcessed.Add(0);
                return _mLastThreadIdUsed;
            }
        }

        public TimeSpan UpdateBytesProcessed(long totalBytesProcessedByThread, int threadId)
        {
            if (totalBytesProcessedByThread < 0)
            {
                throw new ArgumentException(string.Format("totalBytesProcessedByThread must be non-negative. value={0}", totalBytesProcessedByThread));
            }
            
            float bytesProcessed;
            float size;
            long bytesProcessedSum;
            lock (this)
            {
                _mBytesProcessed[threadId] = totalBytesProcessedByThread;
                bytesProcessedSum = _mBytesProcessed.Sum();
                bytesProcessed = bytesProcessedSum;
                size = _mEstimatedBytes;
            }

            var suggestedWait = _mExternalNotification.OnStatusUpdate(Math.Max(0f, Math.Min(1f, bytesProcessed / size)),bytesProcessedSum);
            return suggestedWait;
            //return TimeSpan.FromMilliseconds(suggestedWait.TotalMilliseconds / 2);
        }
    }
}
