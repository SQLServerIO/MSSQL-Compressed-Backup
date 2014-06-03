/*************************************************************************************\
File Name  :  CommandLineNotifier.cs
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
using MSBackupPipe.Common;

namespace MSBackupPipe.Cmd
{
    //base console notifier reports on status of current opperation
    class CommandLineNotifier : IUpdateNotification
    {
        private readonly bool _mIsBackup;
        private DateTime _mNextNotificationTimeUtc = DateTime.Today.AddDays(1);
        private DateTime _mStartTime = DateTime.UtcNow;

        private readonly TimeSpan _mMinTimeForUpdate = TimeSpan.FromSeconds(1.2);

        public CommandLineNotifier(bool isBackup)
        {
            _mIsBackup = isBackup;
        }

        public void OnConnecting(string message)
        {
            // do nothing
        }

        public void OnStart()
        {
            lock (this)
            {
                Console.WriteLine("{0} has started", _mIsBackup ? "Backup" : "Restore");
                _mNextNotificationTimeUtc = DateTime.UtcNow.Add(TimeSpan.FromMilliseconds(10));
                _mStartTime = DateTime.UtcNow;
            }
        }

        public TimeSpan OnStatusUpdate(float percentComplete, long bytes)
        {
            var utcNow = DateTime.UtcNow;

            lock (this)
            {
                if (_mNextNotificationTimeUtc < utcNow && utcNow - _mStartTime > _mMinTimeForUpdate)
                {
                    var percent = string.Format("{0:0.00}%", percentComplete * 100.0);
                    percent = new string(' ', 7 - percent.Length) + percent;
                    Console.Write(percent + " Complete. ");
                    ReturnByteScale(bytes);
                    var estEndTime = _mStartTime;
                    if (percentComplete > 0)
                    {
                        estEndTime = _mStartTime.AddMilliseconds((utcNow - _mStartTime).TotalMilliseconds / percentComplete);
                    }
                    //TODO: currently shows time remaining need to add a switch to show ether time remaining or estimated datetime to finish.
                    if (true)
                    {
                        Console.WriteLine(" Time Remaining: {0}", string.Format("{0:dd\\:hh\\:mm\\:ss}", estEndTime.Subtract(utcNow).Duration()));
                    }
                    else
                    {
                        Console.WriteLine(string.Format(" Estimated End: {0} ", estEndTime.ToLocalTime()));
                    }
                    var nextWait = CalculateNextNotification(utcNow - _mStartTime);
                    _mNextNotificationTimeUtc = utcNow.Add(nextWait);
                    return nextWait;
                }
                if (((utcNow - _mStartTime) - _mMinTimeForUpdate) > utcNow - _mNextNotificationTimeUtc)
                {
                    return _mMinTimeForUpdate;
                }
                return (utcNow - _mNextNotificationTimeUtc);
            }
        }

        /// <summary>
        /// converts bytes to largest next measure bytes, kilobytes, megabytes, gigabytes and terabytes
        /// </summary>
        /// <param name="bytesRead">the total amount of bytes read</param>
        private static void ReturnByteScale(long bytesRead)
        {
            if (bytesRead <= 0)
            {
                Console.Write("1.00 bt Completed.");
            }
            else
            {
                var bytesMeasure = " bt Completed.";
                double bytesByUnit;
                if (bytesRead >= 1099511627776)
                {
                    bytesByUnit = bytesRead/1099511627776.00;
                    bytesMeasure = bytesRead > 1099511627776 ? " TB Completed." : " GB Completed.";
                }
                else if (bytesRead >= 1073741824)
                {
                    bytesByUnit = bytesRead/1073741824.00;
                    bytesMeasure = bytesRead > 1099511627776 ? " TB Completed." : " GB Completed.";
                }
                else if (bytesRead >= 1048576)
                {
                    bytesByUnit = bytesRead / 1048576.00;
                    bytesMeasure = bytesRead > 1048576 ? " MB Completed." : " KB Completed.";
                }
                else if (bytesRead >= 1024)
                {
                    bytesByUnit = bytesRead / 1024.00;
                }
                else
                {
                    bytesByUnit = bytesRead;
                }

                var bytesCompleted = "";
                //TODO: BUG apparently there is a possibility that we will get a zero length string back
                if (string.Format("{0:0.00}", bytesByUnit).Length >= 3)
                {
                    bytesCompleted = new string(' ', 6 - string.Format("{0:0.00}", bytesByUnit).Length) + string.Format("{0:0.00}", bytesByUnit);
                }
                else if (string.Format("{0:0.00}", bytesByUnit).Length >= 1)
                {
                    bytesCompleted = new string(' ', 3) + string.Format("{0:0.00}", bytesByUnit);
                }
                Console.Write(bytesCompleted + bytesMeasure);
            }
        }


        /// <summary>
        /// More updates at the beginning -- fewer updates as time goes on.
        /// </summary>
        /// <param name="elapsedTime">The total time elapsed from when the processing started</param>
        /// <returns>A timespan to wait until displaying the next update</returns>
		//TODO: BUG This can cause a failure with slow compressors if the elapsedTime.TotalSeconds is set too low
		//TODO: tie this to STATS instead of a time frame.
        private static TimeSpan CalculateNextNotification(TimeSpan elapsedTime)
        {
            //DO NOT set this number too high it is what can lead to the race condition and kill the stream.
            if (elapsedTime.TotalSeconds < 3)
            {
                return TimeSpan.FromSeconds(1.2);
            }
            if ((elapsedTime.TotalSeconds * 0.4) > 60)
            {
                return TimeSpan.FromSeconds(60);
            }
            var secondsDelay = elapsedTime.TotalSeconds * 0.4;
            return TimeSpan.FromSeconds(secondsDelay);
        }
    }
}
