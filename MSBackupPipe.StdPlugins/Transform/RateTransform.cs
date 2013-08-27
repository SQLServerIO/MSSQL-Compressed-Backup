/*
	Copyright 2009 Clay Lenhart <clay@lenharts.net>


	This file is part of MSSQL Compressed Backup.

    MSSQL Compressed Backup is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

    Foobar is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with Foobar.  If not, see <http://www.gnu.org/licenses/>.
*/

using System;
using System.Collections.Generic;
using System.Text;
using System.IO;

namespace MSBackupPipe.StdPlugins.Transform
{
    public class RateTransform : IBackupTransformer
    {

        #region IBackupTransformer Members

        private static Dictionary<string, ParameterInfo> mBackupParamSchema;
        static RateTransform()
        {
            mBackupParamSchema = new Dictionary<string, ParameterInfo>(StringComparer.InvariantCultureIgnoreCase);
            mBackupParamSchema.Add("ratemb", new ParameterInfo(false, true));
        }

        public Stream GetBackupWriter(Dictionary<string, List<string>> config, Stream writeToStream)
        {
            ParameterInfo.ValidateParams(mBackupParamSchema, config);




            double rateMb;

            //if (!parsedConfig.ContainsKey("ratemb"))
            //{
            //    throw new ArgumentException("rate: The ratemb parameter is missing.  Use the rate option like rate(ratemb=10)");
            //}

            if (!double.TryParse(config["ratemb"][0], out rateMb))
            {
                throw new ArgumentException(string.Format("rate: Unable to parse the number: {0}", config["ratemb"][0]));
            }



            Console.WriteLine(string.Format("rate: ratemb = {0}", rateMb));

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
                return @"rate Usage:
You can slow down the pipeline to ensure the server is not overloaded.  Enter a rate in MB like:
    rate(rateMB=10.0)";
            }
        }

        #endregion


        private class RateLimitStream : Stream
        {
            private Stream mStream;
            private double mRateMB;
            private DateTime mNextStartTimeUtc;
            private bool mDisposed;

            public RateLimitStream(Stream s, double rateMB)
            {
                mStream = s;
                mRateMB = rateMB;
                mNextStartTimeUtc = DateTime.UtcNow;
            }

            public override bool CanRead
            {
                get { return mStream.CanRead; }
            }

            public override bool CanSeek
            {
                get { return mStream.CanSeek; }
            }

            public override bool CanWrite
            {
                get { return mStream.CanWrite; }
            }

            public override void Flush()
            {
                mStream.Flush();
            }

            public override long Length
            {
                get { return mStream.Length; }
            }

            public override long Position
            {
                get
                {
                    return mStream.Position;
                }
                set
                {
                    mStream.Position = value;
                }
            }

            private void Wait(int count)
            {
                DateTime utcNow = DateTime.UtcNow;
                while (mNextStartTimeUtc > utcNow)
                {
                    System.Threading.Thread.Sleep(mNextStartTimeUtc - utcNow);

                    utcNow = DateTime.UtcNow;
                }

                mNextStartTimeUtc = mNextStartTimeUtc.AddSeconds(((double)count) / mRateMB / (1024 * 1024));

                // only allow a 0.5 second burst
                DateTime minStartTime = DateTime.UtcNow.AddMilliseconds(-500);
                if (mNextStartTimeUtc < minStartTime)
                {
                    mNextStartTimeUtc = minStartTime;
                }
            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                Wait(count);

                return mStream.Read(buffer, offset, count);

            }

            public override long Seek(long offset, SeekOrigin origin)
            {
                return mStream.Seek(offset, origin);
            }

            public override void SetLength(long value)
            {
                mStream.SetLength(value);
            }

            public override void Write(byte[] buffer, int offset, int count)
            {
                Wait(count);
                mStream.Write(buffer, offset, count);
            }

            protected override void Dispose(bool disposing)
            {
                if (!mDisposed)
                {
                    if (disposing)
                    {
                        mStream.Dispose();
                        // dispose of managed resources

                    }

                    // There are no unmanaged resources to release, but
                    // if we add them, they need to be released here.
                }
                mDisposed = true;

                // If it is available, make the call to the
                // base class's Dispose(Boolean) method
                base.Dispose(disposing);

            }
        }
    }
}
