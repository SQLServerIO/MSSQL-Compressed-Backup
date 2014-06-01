/*************************************************************************************\
File Name  :  RateTransform.cs
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

namespace MSBackupPipe.StdPlugins.Transform
{
    public class RateTransform : IBackupTransformer
    {
        #region IBackupTransformer Members

        private static readonly Dictionary<string, ParameterInfo> MBackupParamSchema;
        static RateTransform()
        {
            MBackupParamSchema = new Dictionary<string, ParameterInfo>(StringComparer.InvariantCultureIgnoreCase)
            {
                {"ratemb", new ParameterInfo(false, true)}
            };
        }

        public Stream GetBackupWriter(Dictionary<string, List<string>> config, Stream writeToStream)
        {
            ParameterInfo.ValidateParams(MBackupParamSchema, config);

            double rateMb;

            //if (!parsedConfig.ContainsKey("ratemb"))
            //{
            //    throw new ArgumentException("rate: The ratemb parameter is missing.  Use the rate option like rate(ratemb=10)");
            //}

            if (!double.TryParse(config["ratemb"][0], out rateMb))
            {
                throw new ArgumentException(string.Format("rate: Unable to parse the number: {0}", config["ratemb"][0]));
            }

            Console.WriteLine("Limiter: rate - ratemb = {0}", rateMb);

            return new RateLimitStream(writeToStream, rateMb);
        }

        public Stream GetRestoreReader(Dictionary<string, List<string>> config, Stream readFromStream)
        {
            return GetBackupWriter(config, readFromStream);
        }

        #endregion

        #region IBackupPlugin Members

        public string Name
        {
            get { return "rate"; }
        }

        public string CommandLineHelp
        {
            get
            {
                return "rate Usage: \nYou can slow down the pipeline to ensure the server is not overloaded.\n Example: \nrate(rateMB=10.0)";
            }
        }

        #endregion

        private class RateLimitStream : Stream
        {
            private readonly Stream _mStream;
            private readonly double _mRateMB;
            private DateTime _mNextStartTimeUtc;
            private bool _mDisposed;

            public RateLimitStream(Stream s, double rateMB)
            {
                _mStream = s;
                _mRateMB = rateMB;
                _mNextStartTimeUtc = DateTime.UtcNow;
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

            private void Wait(int count)
            {
                var utcNow = DateTime.UtcNow;
                while (_mNextStartTimeUtc > utcNow)
                {
                    System.Threading.Thread.Sleep(_mNextStartTimeUtc - utcNow);

                    utcNow = DateTime.UtcNow;
                }

                _mNextStartTimeUtc = _mNextStartTimeUtc.AddSeconds(count / _mRateMB / (1024 * 1024));

                // only allow a 0.5 second burst
                var minStartTime = DateTime.UtcNow.AddMilliseconds(-500);
                if (_mNextStartTimeUtc < minStartTime)
                {
                    _mNextStartTimeUtc = minStartTime;
                }
            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                Wait(count);

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
                Wait(count);
                _mStream.Write(buffer, offset, count);
            }

            protected override void Dispose(bool disposing)
            {
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
        }
    }
}
