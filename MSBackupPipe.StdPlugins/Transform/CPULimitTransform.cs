/*************************************************************************************\
File Name  :  CPUTransform.cs
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
using System.IO;
using System.Management;
using System.Diagnostics;
using System.Threading;

namespace MSBackupPipe.StdPlugins.Transform
{
    public class CPUTransform : IBackupTransformer
    {
        #region IBackupTransformer Members

        private static readonly Dictionary<string, ParameterInfo> MBackupParamSchema;
        private static ManagementObject processor;
        protected static PerformanceCounter cpuCounter; 

        static CPUTransform()
        {
            MBackupParamSchema = new Dictionary<string, ParameterInfo>(StringComparer.InvariantCultureIgnoreCase)
            {
                {"limitcpu", new ParameterInfo(false, true)}
            };
        }

        public Stream GetBackupWriter(Dictionary<string, List<string>> config, Stream writeToStream)
        {
            processor = new ManagementObject("Win32_PerfFormattedData_PerfOS_Processor.Name='_Total'");
            cpuCounter = new PerformanceCounter();
            cpuCounter.CategoryName = "Processor";
            cpuCounter.CounterName = "% Processor Time";
            cpuCounter.InstanceName = "_Total"; 

            ParameterInfo.ValidateParams(MBackupParamSchema, config);

            int limitCPU;

            //if (!parsedConfig.ContainsKey("limitcpu"))
            //{
            //    throw new ArgumentException("limitcpu: The limitcpu parameter is missing. Use the limitcpu option like cpu(limitcpu=10)");
            //}

            if (!int.TryParse(config["limitcpu"][0], out limitCPU))
            {
                throw new ArgumentException(string.Format("rate: Unable to parse the number: {0}", config["limitcpu"][0]));
            }

            Console.WriteLine("Limiter: limitcpu = {0}", limitCPU);

            return new CPULimitStream(writeToStream, limitCPU);
        }

        public Stream GetRestoreReader(Dictionary<string, List<string>> config, Stream readFromStream)
        {
            return GetBackupWriter(config, readFromStream);
        }

        #endregion

        #region IBackupPlugin Members

        public string Name
        {
            get { return "cpu"; }
        }

        public string CommandLineHelp
        {
            get
            {
                return "cpu Usage: \nYou can limit the cpu used to ensure the server is not overloaded.\n Example: \ncpu(limitcpu=80)";
            }
        }

        #endregion

        private class CPULimitStream : Stream
        {
            //locker object
            private readonly object _lock;

            //write thread
            private readonly Thread _cpuTimer;

            private readonly int _mlimitCPU;

            private int _mcurrentCPU;

            private readonly Stream _mStream;

            private bool _mDisposed;


            public CPULimitStream(Stream s, int limitCPU)
            {
                _mStream = s;
                _mlimitCPU = limitCPU;
                _lock = new object();
                _cpuTimer = new Thread(getCPUCounter);
                _cpuTimer.IsBackground = true;
                _cpuTimer.Start();
            }

            public override bool CanRead
            {
                get { return _mStream.CanRead; }
            }

            public override bool CanSeek
            {
                get { return _mStream.CanSeek; }
            }

            public override bool CanWrite
            {
                get { return _mStream.CanWrite; }
            }

            public override void Flush()
            {
                _mStream.Flush();
            }

            public override long Length
            {
                get { return _mStream.Length; }
            }

            public override long Position
            {
                get
                {
                    return _mStream.Position;
                }
                set
                {
                    _mStream.Position = value;
                }
            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                Wait();
                return _mStream.Read(buffer, offset, count);
            }

            public override long Seek(long offset, SeekOrigin origin)
            {
                return _mStream.Seek(offset, origin);
            }

            public override void SetLength(long value)
            {
                _mStream.SetLength(value);
            }

            public override void Write(byte[] buffer, int offset, int count)
            {
                Wait();
                _mStream.Write(buffer, offset, count);
            }

            protected override void Dispose(bool disposing)
            {
                _cpuTimer.Abort();
                if (!_mDisposed)
                {
                    if (disposing)
                    {
                        _mStream.Dispose();
                        // dispose of managed resources
                    }

                    // There are no unmanaged resources to release, but
                    // if we add them, they need to be released here.
                }
                _mDisposed = true;

                // If it is available, make the call to the
                // base class's Dispose(Boolean) method
                base.Dispose(disposing);
            }

            public void getCPUCounter()
            {

                PerformanceCounter cpuCounter = new PerformanceCounter();
                cpuCounter.CategoryName = "Processor";
                cpuCounter.CounterName = "% Processor Time";
                cpuCounter.InstanceName = "_Total";
                int _holdCPUValue;

                    while(true)
                    {
                        _holdCPUValue = Convert.ToInt16(cpuCounter.NextValue());
                        System.Threading.Thread.Sleep(1000);
                        lock (_lock)
                        {
                            _mcurrentCPU = Convert.ToInt16(cpuCounter.NextValue());
                        }
                    }
                }

            public void Wait()
            {
                lock (_lock)
                {
                    if (_mlimitCPU < _mcurrentCPU)
                    {
                        Console.WriteLine("Hit CPU Limit: {0} Current CPU: {1}", _mlimitCPU, _mcurrentCPU);
                        System.Threading.Thread.Sleep(1000);
                    }
                }
            }
        }
    }
}

