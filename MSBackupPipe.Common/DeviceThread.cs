/*************************************************************************************\
File Name  :  DeviceThread.cs
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
using System.Threading;
using System.IO;
using VdiNet.VirtualBackupDevice;

namespace MSBackupPipe.Common
{
    class DeviceThread : IDisposable
    {
        private bool _mDisposed;
        private Stream _mTopOfPipeline;
        private IVirtualDevice _mDevice;
        private IVirtualDeviceSet _mDeviceSet;
        private bool _mIsBackup;

        private Thread _mThread;
        private Exception _mException;

        public void Initialize(bool isBackup, Stream topOfPipeline, IVirtualDevice device, IVirtualDeviceSet deviceSet)
        {
            _mIsBackup = isBackup;
            _mTopOfPipeline = topOfPipeline;
            _mDevice = device;
            _mDeviceSet = deviceSet;
        }

        public void BeginCopy()
        {

            var job = new ThreadStart(ThreadStart);
            _mThread = new Thread(job);
            _mThread.Start();
        }

        public Exception EndCopy()
        {
            _mThread.Join();
            return _mException;
        }

        private void ThreadStart()
        {
            try
            {
                var buff = _mDevice.CreateCommandBuffer();

                try
                {
                    ReadWriteData(_mDevice, buff, _mTopOfPipeline, _mIsBackup);
                }
                catch (Exception)
                {
                    _mDeviceSet.SignalAbort();
                    throw;
                }
            }
            catch (Exception e)
            {
                _mException = e;
            }
        }

        private static void ReadWriteData(IVirtualDevice device, ICommandBuffer buff, Stream stream, bool isBackup)
        {
            while (device.GetCommand(null, buff))
            {
                if (!buff.TimedOut)
                {
                    var completionCode = CompletionCode.DiskFull;
                    uint bytesTransferred = 0;

                    try
                    {
                        switch (buff.CommandType)
                        {
                            case DeviceCommandType.Write:

                                if (!isBackup)
                                {
                                    throw new InvalidOperationException("Cannot write in 'restore' mode");
                                }

                                bytesTransferred = (uint)buff.WriteToStream(stream);

                                completionCode = CompletionCode.Success;

                                break;
                            case DeviceCommandType.Read:

                                if (isBackup)
                                {
                                    throw new InvalidOperationException("Cannot read in 'backup' mode");
                                }

                                bytesTransferred = (uint)buff.ReadFromStream(stream);

                                completionCode = bytesTransferred > 0 ? CompletionCode.Success : CompletionCode.HandleEof;

                                break;
                            case DeviceCommandType.ClearError:
                                completionCode = CompletionCode.Success;
                                break;

                            case DeviceCommandType.Flush:
                                stream.Flush();
                                completionCode = CompletionCode.Success;
                                break;

                            default:
                                throw new ArgumentException(string.Format("Unknown command: {0}", buff.CommandType));
                        }
                    }
                    finally
                    {
                        device.CompleteCommand(buff, completionCode, bytesTransferred, 0);
                    }
                }
            }
        }

        #region IDisposable Members

        public void Dispose()
        {
            GC.SuppressFinalize(this);
            //Dispose(true);
        }

        ~DeviceThread()
        {
            Dispose(false);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_mDisposed)
            {
                if (disposing)
                {
                    // dispose of managed resources
                    _mDevice = null;
                    _mDeviceSet = null;

                    // the Program class will dispose of the streams

                    //if (mTopOfPipeline != null)
                    //{
                    //    mTopOfPipeline.Dispose();
                    //    mTopOfPipeline = null;
                    //}
                }

                // There are no unmanaged resources to release, but
                // if we add them, they need to be released here.
            }
            _mDisposed = true;

            // If it is available, make the call to the
            // base class's Dispose(Boolean) method
            //base.Dispose(disposing);
        }

        #endregion
    }
}
