using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;

namespace MSBackupPipe.StdPlugins.Streams
{
    ///-------------------------------------------------------------------------------------------------
    /// <summary>	Unbuffered file stream. </summary>
    ///
    /// <remarks>	Wes Brown, 6/03/2014. </remarks>
    ///-------------------------------------------------------------------------------------------------

    public class UnBufferedFileStream : Stream
    {
        #region private custom variables

        //flag to open file handle without caching at all
        private const FileOptions FileFlagNoBuffering = (FileOptions)0x20000000;

        //our file stream to disk
        private Stream _outfile;

        //our file name we need to hold onto for later
        private readonly String _outputfilename;
        
        //our incoming buffer
        private readonly byte[] _readBuffer;

        //our buffer so we can flush in 512 byte blocks
        private readonly byte[] _writeBuffer;

        //our buffer so we can flush in 512 byte blocks
        private readonly byte[] _flipBuffer;

        //how full is the write buffer?
        private int _writeBufferOffset;

        //is our holding buffer full?
        private bool _flipBufferFull;

        //is our holding buffer full?
        private bool _readBufferFull;

        //how many bytes have we written to the file?
        private long _bytesWritten;

        //holding variable to help test buffer full state
        private long _writeLength;

        //locker object
        private readonly object _lock;

        //write thread
        private readonly Thread _flushingBuffer;

        private readonly FileAccess _writeMode;

        private readonly int _bufferSize ;
        #endregion


        public UnBufferedFileStream(String outputfile)
            : this(outputfile, FileAccess.Write, (1048576 * 32),(1024*1024))
        {
        }

        /// ---------------------------------------------------------------------------------------------------------------------------------------------------------
        ///  <summary>	Constructor. </summary>
        /// 
        /// <remarks> Wes Brown, 6/03/2014. </remarks>
        /// 
        ///  <exception cref="ArgumentNullException">	Thrown when one or more required arguments are null. </exception>
        ///  <exception cref="ArgumentException">		Thrown when one or more arguments have unsupported or illegal values. </exception>
        /// <param name="writeMode"></param>
        /// <param name="bufferSize"></param>
        /// <param name="outputFile"></param>
        /// <param name="estimatedTotalBytes"></param>
        /// ---------------------------------------------------------------------------------------------------------------------------------------------------------
        public UnBufferedFileStream(String outputFile, FileAccess writeMode, int bufferSize, long estimatedTotalBytes)
        {
            _writeBuffer = new byte[bufferSize];
            _readBuffer = new byte[bufferSize];
            _flipBuffer = new byte[bufferSize];
            _writeMode = writeMode;
            _bufferSize = bufferSize;
            _lock = new object();

            //save our file name for later when we need to flush the tail and set the size.
            _outputfilename = outputFile;

            //open our file without OS or disk buffering.
            switch (writeMode)
            {
                case FileAccess.Write:
                    //open the file with no buffering to write to it.
                    _outfile = new FileStream(outputFile, FileMode.Create, FileAccess.Write, FileShare.None, (1024*64),FileFlagNoBuffering);

                    //if the estimatedTotalBytes isn't evenly divisible by 4k, our assumed on disk sector size then make it so.
                    if ((estimatedTotalBytes%4096) > 1)
                    {
                        var blocks = estimatedTotalBytes/4096;
                        estimatedTotalBytes = 4096*blocks;
                    }

                    _outfile.SetLength(estimatedTotalBytes);
                    //start the write buffer flush thread
                    _flushingBuffer = new Thread(FlushWriteBuffer);
                    _flushingBuffer.Start();
                    break;
                case FileAccess.Read:
                    _outfile = new FileStream(outputFile, FileMode.Open, FileAccess.Read, FileShare.None, (1024*64),FileOptions.WriteThrough);
                    break;
            }
        }

        // Get the base _stream that underlies this one.
        ///-------------------------------------------------------------------------------------------------
        /// <summary>	Gets the base stream. </summary>
        ///
        /// <value>	The base stream. </value>
        ///-------------------------------------------------------------------------------------------------
        private Stream BaseStream { get; set; }
        ///-------------------------------------------------------------------------------------------------
        /// <summary>	Gets a value indicating whether we can read. </summary>
        ///
        /// <value>	true if we can read, false if not. </value>
        ///-------------------------------------------------------------------------------------------------
        public override bool CanRead
        {
            get { return true; }
        }

        ///-------------------------------------------------------------------------------------------------
        /// <summary>	Gets a value indicating whether we can write. </summary>
        ///
        /// <value>	true if we can write, false if not. </value>
        ///-------------------------------------------------------------------------------------------------
        public override bool CanWrite
        {
            get { return true; }
        }

        ///-------------------------------------------------------------------------------------------------
        /// <summary>	Gets a value indicating whether we can seek. </summary>
        ///
        /// <value>	true if we can seek, false if not. </value>
        ///-------------------------------------------------------------------------------------------------
        public override bool CanSeek
        {
            get { return false; }
        }

        public int BufferSize { get; set; }

        ///-------------------------------------------------------------------------------------------------
        /// <summary>	When overridden in a derived class, clears all buffers for this stream and causes
        /// 			any buffered data to be written to the underlying device. </summary>
        ///
        /// <remarks> Wes Brown, 6/03/2014. </remarks>
        ///
        /// <exception cref="ObjectDisposedException">	Thrown when a supplied object has been disposed. </exception>
        ///
        /// ### <exception cref="T:System.IO.IOException">	An I/O error occurs. </exception>
        ///-------------------------------------------------------------------------------------------------
        public override void Flush()
        {
        }

        ///-------------------------------------------------------------------------------------------------
        /// <summary>	When overridden in a derived class, reads a sequence of bytes from the current
        /// 			stream and advances the position within the stream by the number of bytes read. </summary>
        ///
        /// <remarks>	
        /// Wes Brown, 5/26/2009. 
        /// </remarks>
        ///-------------------------------------------------------------------------------------------------
        public override int Read([In, Out] byte[] buffer, int offset, int count)
        {
            if (buffer == null)
                throw new ArgumentNullException("buffer");
            if (!CanRead)
                throw new NotSupportedException("Stream does not support reading");
            var len = buffer.Length;
            if (offset < 0)
                throw new ArgumentOutOfRangeException("offset", "< 0");
            if (count < 0)
                throw new ArgumentOutOfRangeException("count", "< 0");
            if (offset > len)
                throw new ArgumentException("destination offset is beyond array size");

            Console.WriteLine("buffer.size: {0}", len);
            Console.WriteLine("offset     : {0}", offset);
            Console.WriteLine("count      : {0}", count);
            return _outfile.Read(buffer, offset, count);
        }

        ///-------------------------------------------------------------------------------------------------
        /// <summary>	When overridden in a derived class, writes a sequence of bytes to the current
        /// 			stream and advances the current position within this stream by the number of
        /// 			bytes written. </summary>
        ///-------------------------------------------------------------------------------------------------
        public override void Write(byte[] buffer, int offset, int count)
        {
            while (true)
            {
                // we have 3 options here:
                // buffer can still be filled --> we fill
                // buffer is full --> we write to file
                // buffer+incomming will overflow the buffer --> we write to file and put the remainder in the buffer

                // 1. there is enough room, the buffer is not full
                _writeLength = _writeBufferOffset + count;
                if (_writeLength <= _writeBuffer.Length)
                {
                    Buffer.BlockCopy(buffer, offset, _writeBuffer, _writeBufferOffset, count);
                    _writeBufferOffset += count;

                    // 2. same size: write
                    if (_writeBufferOffset == _writeBuffer.Length)
                    {
                        lock (_lock)
                        {
                            if (!_readBufferFull)
                            {
                                Buffer.BlockCopy(_writeBuffer, 0, _readBuffer, 0, _writeBuffer.Length);
                                _bytesWritten += _writeBuffer.Length;
                                _writeBufferOffset = 0;
                                _readBufferFull = true;
                            }
                            else
                            {
                                Thread.SpinWait(10);
                            }
                        }
                    }
                }
                // 3. buffer overflow: write full buffer and put remainder in buffer
                else
                {
                    //fill the buffer up
                    Buffer.BlockCopy(buffer, offset, _writeBuffer, _writeBufferOffset, (_writeBuffer.Length - _writeBufferOffset));

                    lock (_lock)
                    {
                        if (!_readBufferFull)
                        {
                            Buffer.BlockCopy(_writeBuffer, 0, _readBuffer, 0, _writeBuffer.Length);
                            _bytesWritten += _writeBuffer.Length;
                            // set the new offset and count to put the remainder of the incoming buffer in our write buffer
                            offset = offset + (_writeBuffer.Length - _writeBufferOffset);
                            count = count - (_writeBuffer.Length - _writeBufferOffset);
                            _writeBufferOffset = 0;
                            _readBufferFull = true;
                        }
                        else
                        {
                            Thread.SpinWait(10);
                        }
                    }
                    continue;                
                }
                break;
            }
        }

        ///-------------------------------------------------------------------------------------------------
        /// <summary>	Releases the unmanaged resources used by the <see cref="T:System.IO.Stream" />
        /// 			and optionally releases the managed resources. </summary>
        ///
        /// <remarks> Wes Brown, 6/03/2014. </remarks>
        ///
        /// <param name="disposing">	true to release both managed and unmanaged resources; false to
        /// 							release only unmanaged resources. </param>
        ///-------------------------------------------------------------------------------------------------
        protected override void Dispose(bool disposing)
        {
            switch (_writeMode)
            {
                case FileAccess.Write:
                    //if we are flushing the write buffer then just wait for a bit.
                    while (_flipBufferFull || _readBufferFull)
                    {
                        Thread.SpinWait(10);
                    }
                    try
                    {
                        //if we have a partial buffer flush the whole thing to disk.
                        if (_writeBufferOffset > 0)
                        {
                            var blocks = 0;
                            //don't write a whole buffer to disk only the part that is used plus enough to round it to the next 4k sector size.
                            if ((_writeBufferOffset%4096) > 1)
                            {
                                blocks = _writeBufferOffset/4096;
                                blocks = 4096*blocks;
                            }

                            _outfile.Write(_writeBuffer, 0, blocks);
                            _bytesWritten += (_writeBufferOffset);
                            _writeBufferOffset = 0;
                        }

#if DEBUG
                        Console.WriteLine("BytesWritten :{0}", _bytesWritten);
#endif
                        //close the file and reset the end of it to the correct byte count.
                        _outfile.Close();
                        _outfile = new FileStream(_outputfilename, FileMode.Open);
                        _outfile.SetLength(_bytesWritten);
                        _outfile.Close();

                        if (BaseStream != null) BaseStream.Close();
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine(e.Message);
                    }
                    finally
                    {
                        //kill the thread that is doing the flushing work.
                        _flushingBuffer.Abort();
                        BaseStream = null;
                        base.Dispose(disposing);
                    }
                    break;
                case FileAccess.Read:
                {
                    _outfile.Close();
                }
                    break;
            }
        }

        
        //------------------------------------custom to our _stream---------------------------------------------
        //private helpters for read and write methods

        /// -------------------------------------------------------------------------------------------------
        /// write thread function
        /// -------------------------------------------------------------------------------------------------
        private void FlushWriteBuffer()
        {
            int flushsize = 1048576*4;
            while (true)
            {
                //if the read buffer is full AND our holding buffer isn't full 
                //THEN flush the read buffer into the holding buffer and write it out to disk.
                if (_readBufferFull && !_flipBufferFull)
                {
                    lock (_lock)
                    {
                        Buffer.BlockCopy(_readBuffer, 0, _flipBuffer, 0, _readBuffer.Length);
                        _flipBufferFull = true;
                        _readBufferFull = false;
                    }
                    //write out whole buffer in chunks to disk
                    //TODO: tie this to maxtransfersize or add a maxtransfersizeout parameter
                    var i = 0;
                        while (i < _flipBuffer.Length)
                        {
                            _outfile.Write(_flipBuffer, i, flushsize);
                            i += flushsize;
                        }
                    lock (_lock)
                    {
                        _flipBufferFull = false;
                    }
                }
                else
                {
                    Thread.SpinWait(10);
                }
            }
        }

        //------------------------------------custom to our _stream---------------------------------------------

        #region unsupported methods

        ///-------------------------------------------------------------------------------------------------
        ///-------------------------------------------------------------------------------------------------
        public override long Length
        {
            get { throw new NotSupportedException(("IO_NotSupp_Seek")); }
        }

        ///-------------------------------------------------------------------------------------------------
        /// <summary>	Gets or sets the position. </summary>
        ///
        /// <value>	The position. </value>
        ///-------------------------------------------------------------------------------------------------
        public override long Position
        {
            get { throw new NotSupportedException(("IO_NotSupp_Seek")); }
            set { throw new NotSupportedException(("IO_NotSupp_Seek")); }
        }

        ///-------------------------------------------------------------------------------------------------
        /// <summary>	When overridden in a derived class, sets the position within the current stream. </summary>
        ///
        /// <remarks> Wes Brown, 6/03/2014. </remarks>
        ///
        /// <exception cref="NotSupportedException">	Thrown when the requested operation is not supporte
        /// 											d. </exception>
        ///
        /// <param name="offset">	A byte offset relative to the <paramref name="origin" /> parameter. </param>
        /// <param name="origin">	A value of type <see cref="T:System.IO.SeekOrigin" /> indicating the
        /// 						reference point used to obtain the new position. </param>
        ///
        /// <returns>	The new position within the current stream. </returns>
        ///
        /// ### <exception cref="T:System.IO.IOException">			An I/O error occurs. </exception>
        /// ### <exception cref="T:System.NotSupportedException">	The stream does not support seeking, su
        /// 														ch as if the stream is constructed from
        /// 														a pipe or console output. </exception>
        /// ### <exception cref="T:System.ObjectDisposedException">	Methods were called after the stream wa
        /// 														s closed. </exception>
        ///-------------------------------------------------------------------------------------------------
        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException(("IO_NotSupp_Seek"));
        }

        ///-------------------------------------------------------------------------------------------------
        /// <summary>	When overridden in a derived class, sets the length of the current stream. </summary>
        ///
        /// <remarks> Wes Brown, 6/03/2014. </remarks>
        ///
        /// <exception cref="NotSupportedException">	Thrown when the requested operation is not supporte
        /// 											d. </exception>
        ///
        /// <param name="value">	The desired length of the current stream in bytes. </param>
        ///
        /// ### <exception cref="T:System.IO.IOException">	An I/O error occurs. </exception>
        ///
        /// ### <exception cref="T:System.NotSupportedException">	The stream does not support both writin
        /// 														g and seeking, such as if the stream is
        /// 														constructed from a pipe or console outp
        /// 														ut. </exception>
        /// ### <exception cref="T:System.ObjectDisposedException">	Methods were called after the stream wa
        /// 														s closed. </exception>
        ///-------------------------------------------------------------------------------------------------
        public override void SetLength(long value)
        {
            throw new NotSupportedException(("IO_NotSupp_SetLength"));
        }

        // Begin an asynchronous read operation.

        ///-------------------------------------------------------------------------------------------------
        /// <summary>	Begins an asynchronous read operation. </summary>
        ///
        /// <remarks> Wes Brown, 6/03/2014. </remarks>
        ///
        /// <exception cref="NotSupportedException">	Thrown when the requested operation is not supporte
        /// 											d. </exception>
        ///
        /// <param name="buffer">	The buffer to read the data into. </param>
        /// <param name="offset">	The byte offset in <paramref name="buffer" /> at which to begin
        /// 						writing data read from the stream. </param>
        /// <param name="count">	The maximum number of bytes to read. </param>
        /// <param name="callback">	An optional asynchronous callback, to be called when the read is
        /// 						complete. </param>
        /// <param name="state">	A user-provided object that distinguishes this particular
        /// 						asynchronous read request from other requests. </param>
        ///
        /// <returns>	An <see cref="T:System.IAsyncResult" /> that represents the asynchronous read,
        /// 			which could still be pending. </returns>
        ///
        /// ### <exception cref="T:System.IO.IOException">			Attempted an asynchronous read past the
        /// 														end of the stream, or a disk error occu
        /// 														rs. </exception>
        /// ### <exception cref="T:System.ArgumentException">		One or more of the arguments is invalid
        /// 														. </exception>
        /// ### <exception cref="T:System.ObjectDisposedException">	Methods were called after the stream wa
        /// 														s closed. </exception>
        /// ### <exception cref="T:System.NotSupportedException">	The current Stream implementation does
        /// 														not support the read operation. </exception>
        ///-------------------------------------------------------------------------------------------------
        public override IAsyncResult BeginRead
            (byte[] buffer, int offset, int count,
             AsyncCallback callback, Object state)
        {
            throw new NotSupportedException(("IO_NotSupp_Read"));
        }
        #endregion
    } ;
}