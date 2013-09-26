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
using System.Collections.Generic;
using System.Text;

using MSBackupPipe.Common;

namespace MSBackupPipe.Cmd
{
    //base console notifier reports on status of current opperation
    class CommandLineNotifier : IUpdateNotification
    {
        private bool mIsBackup;
        private DateTime mNextNotificationTimeUtc = DateTime.Today.AddDays(1);
        private DateTime mStartTime = DateTime.UtcNow;

        private readonly TimeSpan mMinTimeForUpdate = TimeSpan.FromSeconds(0.5);//1.2

        public CommandLineNotifier(bool isBackup)
        {
            mIsBackup = isBackup;
        }

        public void OnConnecting(string message)
        {
            // do nothing
        }

        public void OnStart()
        {
            lock (this)
            {
                Console.WriteLine(string.Format("{0} has started", mIsBackup ? "Backup" : "Restore"));
                mNextNotificationTimeUtc = DateTime.UtcNow.Add(TimeSpan.FromMilliseconds(10));
                mStartTime = DateTime.UtcNow;
            }
        }

        public TimeSpan OnStatusUpdate(float percentComplete, long bytes)
        {
            DateTime utcNow = DateTime.UtcNow;

            lock (this)
            {

                if (mNextNotificationTimeUtc < utcNow && utcNow - mStartTime > mMinTimeForUpdate)
                {
                    string percent = string.Format("{0:0.00}%", percentComplete * 100.0);
                    percent = new string(' ', 7 - percent.Length) + percent;
                    Console.Write(percent + " Complete. ");
                    ReturnByteScale(bytes);
                    DateTime estEndTime = mStartTime;
                    if (percentComplete > 0)
                    {
                        estEndTime = mStartTime.AddMilliseconds((utcNow - mStartTime).TotalMilliseconds / percentComplete);
                    }
                    //TODO: currently shows time remaining need to add a switch to show ether time remaining or estimated datetime to finish.
                    if (1 == 1)
                    {
                        Console.WriteLine(" Time Remaining: {0}", string.Format("{0:dd\\:hh\\:mm\\:ss}", estEndTime.Subtract(utcNow).Duration()));
                    }
                    else
                    {
                        Console.WriteLine(string.Format(" Estimated End: {0} ", estEndTime.ToLocalTime()));
                    }
                    TimeSpan nextWait = CalculateNextNotification(utcNow - mStartTime);
                    mNextNotificationTimeUtc = utcNow.Add(nextWait);
                    return nextWait;
                }
                if (((utcNow - mStartTime) - mMinTimeForUpdate) > utcNow - mNextNotificationTimeUtc)
                {
                    return mMinTimeForUpdate;
                }
                else
                {
                    return (utcNow - mNextNotificationTimeUtc);
                }
            }
        }

        /// <summary>
        /// converts bytes to largest next measure bytes, kilobytes, megabytes, gigabytes and terabytes
        /// </summary>
        /// <param name="bytesRead">the total amount of bytes read</param>
        private static void ReturnByteScale(long bytesRead)
        {
            string bytesCompleted = "";
            string bytesMeasure = "";

            if ((bytesRead / 1024.0) > 1)
            {
                if ((bytesRead / 1024.0 / 1024) > 1)
                {
                    if ((bytesRead / 1024.0 / 1024 / 1024) > 1)
                    {
                        if ((bytesRead / 1024.0 / 1024 / 1024 / 1024) > 1)
                        {
                            bytesMeasure = " TB Completed.";
                            bytesCompleted = string.Format("{0:0.00}", (bytesRead / 1024.0 / 1024 / 1024 / 1024));
                        }
                        else
                        {
                            bytesMeasure = " GB Completed.";
                            bytesCompleted = string.Format("{0:0.00}", (bytesRead / 1024.0 / 1024 / 1024));
                        }
                    }
                    else
                    {
                        bytesMeasure = " MB Completed.";
                        bytesCompleted = string.Format("{0:0.00}", (bytesRead / 1024.0 / 1024));
                    }
                }
                else
                {
                    bytesMeasure = " MB Completed.";
                    bytesCompleted = string.Format("{0:0.00}", (bytesRead / 1024.0));
                }
            }
            else
            {
                bytesMeasure = " Completed.";
                bytesCompleted = string.Format("{0:0.00}", (bytesRead));
            }
            bytesCompleted = new string(' ', 6 - bytesCompleted.Length) + bytesCompleted;
            Console.Write(bytesCompleted + bytesMeasure);
        }


        /// <summary>
        /// More updates at the beginning -- fewer updates as time goes on.
        /// </summary>
        /// <param name="elapsedTime">The total time elapsed from when the processing started</param>
        /// <returns>A timespan to wait until displaying the next update</returns>
        private static TimeSpan CalculateNextNotification(TimeSpan elapsedTime)
        {
            if (elapsedTime.TotalSeconds < 3000)
            {
                return TimeSpan.FromSeconds(1.2);//1.2
            }
            else
            {
                double secondsDelay = elapsedTime.TotalSeconds * 0.4;
                return TimeSpan.FromSeconds(secondsDelay);
            }
        }
    }
}
