/*************************************************************************************\
File Name  :  TrackingStream.cs
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
//TODO: Switch to turn off tracking stream
using System;
using System.Collections.Generic;
using System.Text;
using System.IO;

namespace MSBackupPipe.StdPlugins
{
    public class TrackingStream : Stream
    {
        private Stream mSourceStream;
        private IStreamNotification mNotification;
        private long mTotalBytesProcessed = 0;
        private int mThreadId;
        private DateTime mNextNotificationUtc = DateTime.UtcNow;

        public TrackingStream(Stream source, IStreamNotification notification)
        {
            mSourceStream = source;
            mNotification = notification;
            mThreadId = notification.GetThreadId();
        }

        public override bool CanRead { get { return mSourceStream.CanRead; } }
        public override bool CanSeek { get { return false; } }
        public override bool CanTimeout { get { return mSourceStream.CanTimeout; } }
        public override bool CanWrite { get { return mSourceStream.CanWrite; } }
        public override long Length { get { return mSourceStream.Length; } }
        public override long Position { get { return mSourceStream.Position; } set { throw new NotSupportedException(); } }
        public override int ReadTimeout { get { return mSourceStream.ReadTimeout; } set { mSourceStream.ReadTimeout = value; } }
        public override int WriteTimeout { get { return mSourceStream.WriteTimeout; } set { mSourceStream.WriteTimeout = value; } }

        public override IAsyncResult BeginRead(byte[] buffer, int offset, int count, AsyncCallback callback, object state)
        {
            return mSourceStream.BeginRead(buffer, offset, count, callback, state);
        }

        public override IAsyncResult BeginWrite(byte[] buffer, int offset, int count, AsyncCallback callback, object state)
        {
            mTotalBytesProcessed += count;
            if (DateTime.UtcNow > mNextNotificationUtc)
            {
                TimeSpan nextWait = mNotification.UpdateBytesProcessed(mTotalBytesProcessed, mThreadId);
                mNextNotificationUtc = DateTime.UtcNow.Add(nextWait);
            }
            return mSourceStream.BeginWrite(buffer, offset, count, callback, state);
        }

        public override void Close()
        {
            mSourceStream.Close();
        }

        protected override void Dispose(bool disposing)
        {
            mSourceStream.Dispose();
        }

        public override int EndRead(IAsyncResult asyncResult)
        {
            int bytesRead = mSourceStream.EndRead(asyncResult);
            mTotalBytesProcessed += bytesRead;
            if (DateTime.UtcNow > mNextNotificationUtc)
            {
                TimeSpan nextWait = mNotification.UpdateBytesProcessed(mTotalBytesProcessed, mThreadId);
                mNextNotificationUtc = DateTime.UtcNow.Add(nextWait);
            }
            return bytesRead;
        }

        public override void EndWrite(IAsyncResult asyncResult)
        {
            mSourceStream.EndWrite(asyncResult);
        }

        public override void Flush()
        {
            mSourceStream.Flush();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            int bytesRead = mSourceStream.Read(buffer, offset, count);

            mTotalBytesProcessed += bytesRead;
            if (DateTime.UtcNow > mNextNotificationUtc)
            {
                TimeSpan nextWait = mNotification.UpdateBytesProcessed(mTotalBytesProcessed, mThreadId);
                mNextNotificationUtc = DateTime.UtcNow.Add(nextWait);
            }
            //TODO: this check is here to keep from blowing up the backup process due to slow stream
            //there are edge cases where we can get an invalid number here I'm investgating it.
            if (bytesRead > 0)
            {
                return bytesRead;
            }
            else
            {
                return 0;
            }
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException();
        }

        public override void SetLength(long value)
        {
            mSourceStream.SetLength(value);
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            mSourceStream.Write(buffer, offset, count);
            mTotalBytesProcessed += count;
            if (DateTime.UtcNow > mNextNotificationUtc)
            {
                TimeSpan nextWait = mNotification.UpdateBytesProcessed(mTotalBytesProcessed, mThreadId);
                mNextNotificationUtc = DateTime.UtcNow.Add(nextWait);
            }
        }
    }
}