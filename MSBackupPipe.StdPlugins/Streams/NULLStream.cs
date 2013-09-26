﻿namespace PlanetaryDBLib.IO.Compression
{
    using System;
    using System.IO;

    public class NullStream : Stream
    {
        long _position = 0;
        long _length = 0;

        public NullStream()
        {
        }

        public override bool CanRead { get { return false; } }
        public override bool CanWrite { get { return true; } }
        public override bool CanSeek { get { return true; } }

        public override void Flush() { }

        public override long Length { get { return _length; } }
        public override long Position
        {
            get { return _position; }
            set
            {
                _position = value;
                if (_position > _length)
                    _length = _position;
            }
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            long newPosition = Position;

            switch (origin)
            {
                default:
                case SeekOrigin.Begin:
                    newPosition = offset;
                    break;
                case SeekOrigin.Current:
                    newPosition = Position + offset;
                    break;
                case SeekOrigin.End:
                    newPosition = Length + offset;
                    break;
            }
            if (newPosition < 0)
                throw new ArgumentException("Attempt to seek before start of stream.");
            Position = newPosition;
            return newPosition;
        }

        public override IAsyncResult BeginRead(byte[] buffer, int offset, int count, AsyncCallback callback, object state)
        {
            throw new NotImplementedException("This stream doesn't support reading.");
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            throw new NotImplementedException("This stream doesn't support reading.");
        }

        public override void SetLength(long value)
        {
            _length = value;
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            Seek((long)count, SeekOrigin.Current);
        }
    }
}