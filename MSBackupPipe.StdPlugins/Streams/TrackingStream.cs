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
using System.IO;

namespace MSBackupPipe.StdPlugins.Streams
{
    public class TrackingStream : Stream
    {
        private readonly Stream _mSourceStream;
        private readonly IStreamNotification _mNotification;
        private long _mTotalBytesProcessed;
        private readonly int _mThreadId;
        private DateTime _mNextNotificationUtc = DateTime.UtcNow;

        public TrackingStream(Stream source, IStreamNotification notification)
        {
            _mSourceStream = source;
            _mNotification = notification;
            _mThreadId = notification.GetThreadId();
        }

        public override bool CanRead { get { return _mSourceStream.CanRead; } }
        public override bool CanSeek { get { return false; } }
        public override bool CanTimeout { get { return _mSourceStream.CanTimeout; } }
        public override bool CanWrite { get { return _mSourceStream.CanWrite; } }
        public override long Length { get { return _mSourceStream.Length; } }
        public override long Position { get { return _mSourceStream.Position; } set { throw new NotSupportedException(); } }
        public override int ReadTimeout { get { return _mSourceStream.ReadTimeout; } set { _mSourceStream.ReadTimeout = value; } }
        public override int WriteTimeout { get { return _mSourceStream.WriteTimeout; } set { _mSourceStream.WriteTimeout = value; } }

        public override IAsyncResult BeginRead(byte[] buffer, int offset, int count, AsyncCallback callback, object state)
        {
            return _mSourceStream.BeginRead(buffer, offset, count, callback, state);
        }

        public override IAsyncResult BeginWrite(byte[] buffer, int offset, int count, AsyncCallback callback, object state)
        {
            _mTotalBytesProcessed += count;
            if (DateTime.UtcNow <= _mNextNotificationUtc)
                return _mSourceStream.BeginWrite(buffer, offset, count, callback, state);
            var nextWait = _mNotification.UpdateBytesProcessed(_mTotalBytesProcessed, _mThreadId);
            _mNextNotificationUtc = DateTime.UtcNow.Add(nextWait);
            return _mSourceStream.BeginWrite(buffer, offset, count, callback, state);
        }

        public override void Close()
        {
            _mSourceStream.Close();
        }

        protected override void Dispose(bool disposing)
        {
            _mSourceStream.Dispose();
            GC.SuppressFinalize(this);
        }

        public override int EndRead(IAsyncResult asyncResult)
        {
            var bytesRead = _mSourceStream.EndRead(asyncResult);
            _mTotalBytesProcessed += bytesRead;
            if (DateTime.UtcNow <= _mNextNotificationUtc) return bytesRead;
            var nextWait = _mNotification.UpdateBytesProcessed(_mTotalBytesProcessed, _mThreadId);
            _mNextNotificationUtc = DateTime.UtcNow.Add(nextWait);
            return bytesRead;
        }

        public override void EndWrite(IAsyncResult asyncResult)
        {
            _mSourceStream.EndWrite(asyncResult);
        }

        public override void Flush()
        {
            _mSourceStream.Flush();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            var bytesRead = _mSourceStream.Read(buffer, offset, count);
            _mTotalBytesProcessed += bytesRead;

            if (DateTime.UtcNow <= _mNextNotificationUtc) return bytesRead > 0 ? bytesRead : 0;

            var nextWait = _mNotification.UpdateBytesProcessed(_mTotalBytesProcessed, _mThreadId);
            _mNextNotificationUtc = DateTime.UtcNow.Add(nextWait);
            //TODO: this check is here to keep from blowing up the backup process due to slow stream
            //there are edge cases where we can get an invalid number here I'm investgating it.
            return bytesRead > 0 ? bytesRead : 0;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException();
        }

        public override void SetLength(long value)
        {
            _mSourceStream.SetLength(value);
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            //write to file happens here!
            _mSourceStream.Write(buffer, offset, count);
            _mTotalBytesProcessed += count;
            if (DateTime.UtcNow <= _mNextNotificationUtc) return;
            var nextWait = _mNotification.UpdateBytesProcessed(_mTotalBytesProcessed, _mThreadId);
            _mNextNotificationUtc = DateTime.UtcNow.Add(nextWait);
        }
    }
}