using System;
using System.IO;
using MSBackupPipe.Common.Annotations;

namespace MSBackupPipe.Common
{
    ///-------------------------------------------------------------------------------------------------
    /// <summary>	Unbuffered file stream. </summary>
    ///
    /// <remarks>	Wes Brown, 6/03/2014. </remarks>
    ///-------------------------------------------------------------------------------------------------

    public class UnBufferedFileStream : Stream
    {
        #region private custom variables

        //4MB is the biggest packet we can get from VDI so I'll start twice that
        public static int CopyBufferSize = 8 * 1024 * 1024;
        public static byte[] Buffer = new byte[CopyBufferSize];
        const FileOptions FileFlagNoBuffering = (FileOptions)0x20000000;
        public static long BytesWritten;
        public FileStream Outfile;
        public String Outputfilename;

        #endregion

        /// -------------------------------------------------------------------------------------------------
        ///  <summary>	Constructor. </summary>
        /// 
        ///  <remarks>	Wes Brown, 5/26/2009. </remarks>
        /// 
        ///  <exception cref="ArgumentNullException">	Thrown when one or more required arguments are null
        ///  											. </exception>
        ///  <exception cref="ArgumentException">		Thrown when one or more arguments have unsupported
        ///  											or illegal values. </exception>
        /// <param name="outputfile"></param>
        /// <param name="stream">		The stream. </param>
        /// -------------------------------------------------------------------------------------------------
        public UnBufferedFileStream(String outputfile,Stream stream)
        {
            if (stream == null)
                throw new ArgumentNullException("stream");

            if (!stream.CanRead)
            {
                throw new ArgumentException (("IO_NotReadable"), "stream");
            }
            BaseStream = stream;
            Outfile = new FileStream(outputfile, FileMode.Create, FileAccess.Write, FileShare.None, CopyBufferSize, FileOptions.WriteThrough | FileFlagNoBuffering);
            Outputfilename = outputfile;
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
            get { return false; }
        }

        ///-------------------------------------------------------------------------------------------------
        /// <summary>	Gets a value indicating whether we can write. </summary>
        ///
        /// <value>	true if we can write, false if not. </value>
        ///-------------------------------------------------------------------------------------------------
        public override bool CanWrite
        {
            get
            {
                return (BaseStream != null);
            }
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

        public int BufferSize { [UsedImplicitly] private get; set; }

        ///-------------------------------------------------------------------------------------------------
        /// <summary>	When overridden in a derived class, clears all buffers for this stream and causes
        /// 			any buffered data to be written to the underlying device. </summary>
        ///
        /// <remarks>	Wes Brown, 5/26/2009. </remarks>
        ///
        /// <exception cref="ObjectDisposedException">	Thrown when a supplied object has been disposed. </exception>
        ///
        /// ### <exception cref="T:System.IO.IOException">	An I/O error occurs. </exception>
        ///-------------------------------------------------------------------------------------------------
        public override void Flush()
        {
            if (BaseStream == null)
            {
                throw new ObjectDisposedException(("Exception_Disposed"));
            }
        }

        ///-------------------------------------------------------------------------------------------------
        /// <summary>	When overridden in a derived class, reads a sequence of bytes from the current
        /// 			stream and advances the position within the stream by the number of bytes read. </summary>
        ///
        /// <remarks>	
        /// Wes Brown, 5/26/2009. 
        /// buffer needs to be the block size + 400 _bytes i.e. 1MB + 400b
        /// </remarks>
        ///
        /// <param name="buffer">	An array of bytes. When this method returns, the buffer contains the specified byte array with the values between <paramref name="offset" /> and (<paramref name="offset" /> + <paramref name="count" /> - 1)
        /// 						replaced by the bytes read from the current source. </param> <param name="offset">	The zero-based byte offset in <paramref name="buffer" /> at which to
        /// 						begin storing the data read from the current stream. </param> <param name="count">	The maximum number of bytes to be read from the current stream. </param>
        ///
        /// <returns>	The total number of bytes read into the buffer. This can be less than the number
        /// 			of bytes requested if that many bytes are not currently available, or zero (0) if
        /// 			the end of the stream has been reached. </returns>
        ///
        /// ### <exception cref="T:System.ArgumentException">			The sum of <paramref name="offset"/> and <paramref name="count" /> is
        /// 															larger than the buffer length. </exception>### <exception cref="T:System.ArgumentNullException">		<paramref name="buffer" /> is null.
        /// 															</exception>
        /// ### <exception cref="T:System.ArgumentOutOfRangeException">	<paramref name="offset" /> or <paramref name="count" /> is negative. </exception>
        /// ### <exception cref="T:System.IO.IOException">				An I/O error occurs. </exception>
        /// ### <exception cref="T:System.NotSupportedException">		The stream does not support reading
        /// 															. </exception>
        /// ### <exception cref="T:System.ObjectDisposedException">		Methods were called after the strea
        /// 															m was closed. </exception>
        ///-------------------------------------------------------------------------------------------------
        public override int Read(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException(("IO_NotSupp_Read"));
        }

        ///-------------------------------------------------------------------------------------------------
        /// <summary>	When overridden in a derived class, writes a sequence of bytes to the current
        /// 			stream and advances the current position within this stream by the number of
        /// 			bytes written. </summary>
        ///
        /// <remarks>	Wes Brown, 5/26/2009. </remarks>
        ///
        /// <param name="buffer">	An array of bytes. This method copies <paramref name="count" /> bytes
        /// 						from <paramref name="buffer" /> to the current stream. </param>
        /// <param name="offset">	The zero-based byte offset in <paramref name="buffer" /> at which to
        /// 						begin copying bytes to the current stream. </param>
        /// <param name="count">	The number of bytes to be written to the current stream. </param>
        ///
        /// ### <exception cref="T:System.ArgumentException">	The sum of <paramref name="offset" /> and <
        /// 													paramref name="count" /> is greater than th
        /// 													e buffer length. </exception>
        ///
        /// ### <exception cref="T:System.ArgumentNullException">		<paramref name="buffer" /> is null.
        /// 															</exception>
        /// ### <exception cref="T:System.ArgumentOutOfRangeException">	<paramref name="offset" /> or <paramref name="count" /> is negative. </exception>
        /// ### <exception cref="T:System.IO.IOException">				An I/O error occurs. </exception>
        /// ### <exception cref="T:System.NotSupportedException">		The stream does not support writing
        /// 															. </exception>
        /// ### <exception cref="T:System.ObjectDisposedException">		Methods were called after the strea
        /// 															m was closed. </exception>
        ///-------------------------------------------------------------------------------------------------
        public override void Write(byte[] buffer, int offset, int count)
        {
            Outfile.Write(buffer, offset, buffer.Length);
            BytesWritten += count;
            Console.WriteLine(BytesWritten);
            //write to file here
        }

        ///-------------------------------------------------------------------------------------------------
        /// <summary>	Releases the unmanaged resources used by the <see cref="T:System.IO.Stream" />
        /// 			and optionally releases the managed resources. </summary>
        ///
        /// <remarks>	Wes Brown, 5/26/2009. </remarks>
        ///
        /// <param name="disposing">	true to release both managed and unmanaged resources; false to
        /// 							release only unmanaged resources. </param>
        ///-------------------------------------------------------------------------------------------------
        protected override void Dispose(bool disposing)
        {
            try
            {
                // Flush on the underlying _stream can throw (ex., low disk space)
                if (disposing && BaseStream != null)
                {
                    Flush();
                }
            }
            finally
            {
                try
                {
                    // Attempt to close the _stream even if there was an IO error from Flushing.
                    // be aware that Stream.Close() can potentially throw here may or may not be
                    // due to the same Flush error). In this case, we still need to ensure 
                    // cleaning up internal resources, hence the finally block.  
                    if (disposing && BaseStream != null)
                    {
                        Outfile.Close();
                        Outfile = new FileStream(Outputfilename, FileMode.Open, FileAccess.Write, FileShare.None, CopyBufferSize, FileOptions.WriteThrough);
                        Outfile.SetLength(BytesWritten);
                        Outfile.Flush();
                        Outfile.Close();

                        BaseStream.Close();
                    }
                }
                finally
                {
                    BaseStream = null;
                    base.Dispose(disposing);
                }
            }
        }

        //------------------------------------custom to our _stream---------------------------------------------
        //private helpters for read and write methods
        ///-------------------------------------------------------------------------------------------------
        /// <summary>	Get the length of the expected uncompressed stream. </summary>
        ///
        /// <value>	long Length. </value>
        ///-------------------------------------------------------------------------------------------------
        public override long Length
        {
            get { throw new NotSupportedException(("IO_NotSupp_Seek")); }
        }

        #region unsupported methods
        // Get or set the current seek position within this _stream.

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
        /// <remarks>	Wes Brown, 5/26/2009. </remarks>
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
        /// <remarks>	Wes Brown, 5/26/2009. </remarks>
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
        /// <remarks>	Wes Brown, 5/26/2009. </remarks>
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
