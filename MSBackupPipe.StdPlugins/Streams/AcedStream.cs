using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Diagnostics;
using Aced.Compression;
using PlanetaryDBLib.Collections;

// ReSharper disable RedundantUsingDirective
//using Aced.Compression;
// ReSharper restore RedundantUsingDirective

namespace PlanetaryDBLib.IO.Compression.Streams
{
    public class AcedStream : Stream
    {
        private long _timeread = 0;
        private long _timewrite = 0;
        private long _timecomressing = 0;
        private long _timedequeue = 0;
        private long _timequeue = 0;
        private Stopwatch st = new Stopwatch();

        #region private custom variables

        //4MB is the biggest packet we can get from VDI so I'll start twice that
        private static int _fifosize = 8192 * 1024;

        //the actual queue constructor
        private BlockQueue _bibreadBuffer = new BlockQueue(_fifosize, false);

        //header from compressed file
        //we do this instead of storing additional info in each compressed packet

        //size of the buffer array map that will be written to the header
        private int _buffermapsize;

        //size of the packet map array map that will be written to the header
        private int _packetmapsize;

        //holds the sise of the buffer for the packet
        //the packetmap version of this is public right now so look for PacketMap[]
        private int[] _buffermap;

        //packet number
        private int _buffermappointer;

        //packet number 
        private int _packetmappointer;

        //decompressed size of every packet in the archive
        private long _decompressedSize;

        //for dequeueing, decompressing and streaming back to requestor
        private byte[] _compressbuff = new byte[_fifosize];

        //holds the packet read from the archive
        private byte[] _compressed = new byte[1];

        //holds the decompressed packet from the archive
        private byte[] _decompressed = new byte[_fifosize];

        //which library is this file compressed with
        private byte _compressionlibrary;
        //which algorithm from the compression library is this file using
        private byte _compressionalgorithm;
        //if the library or algorithm supports multiple compression levels what is it?
        private byte _compressionlevel;

        //holds any remander from a buffer read
        private int _enqueuewriteremander;

        //record count holds number of records compressed or decompressed 
        private int _reccount;

        //holds the buffer size for the uncompressed buffer just compressed to be written to the header
        private readonly List<int> _bufferlen = new List<int>();
        //holds the buffer size for the compressed packet just compressed to be written to the header
        private readonly List<uint> _packetlen = new List<uint>();

        //number of bytes read from the basestream on decompress
        private int _bytes;

        //used to hold the bibbuffer read or write offset
        private int _decompressedoffset;
        //number of bytes compressed or decompressed by the compressor return variable
        private int _dstlen;

        //used to hold number of records currently in queue may want to use 
        //the bibbuffer.Used var instead
        private int _recordsbuffered;

        //number of total records read or written 
        private int _recordsread;

        //buffer sizes at diffrent compression levels only level 1 is implmented in code at the moment.
        //lvl1_32bit 36880 lvl2_32bit 34832 lvl3_32bit 266256 lvl1_64bit 69648 lvl2_64bit 67600  lvl3_32bit 528400
        /*
                private byte[] _scratch = new byte[36880];
        */

        //used by BriefLZ to do the compression work
        private byte[] _scratchCompress = new byte[36880];

        #endregion

        // Internal state.
        private readonly bool _leaveOpen;
        private readonly CompressionMode _mode;
        // Constructors.

        ///-------------------------------------------------------------------------------------------------
        /// <summary>	Constructor. </summary>
        ///
        /// <remarks>	Wes Brown, 5/26/2009. </remarks>
        ///
        /// <param name="stream">	The stream. </param>
        /// <param name="mode">		The mode. </param>
        ///-------------------------------------------------------------------------------------------------
        public AcedStream(Stream stream, CompressionMode mode)
            : this(stream, mode, false)
        {
        }

        ///-------------------------------------------------------------------------------------------------
        /// <summary>	Constructor. </summary>
        ///
        /// <remarks>	Wes Brown, 5/26/2009. </remarks>
        ///
        /// <exception cref="ArgumentNullException">	Thrown when one or more required arguments are null
        /// 											. </exception>
        /// <exception cref="ArgumentException">		Thrown when one or more arguments have unsupported
        /// 											or illegal values. </exception>
        ///
        /// <param name="stream">		The stream. </param>
        /// <param name="mode">			The mode. </param>
        /// <param name="leaveOpen">	true to leave open. </param>
        ///-------------------------------------------------------------------------------------------------
        public AcedStream(Stream stream, CompressionMode mode, bool leaveOpen)
        {
            _recordsread = 0;
            if (stream == null)
                throw new ArgumentNullException("stream");
            switch (mode)
            {
                case CompressionMode.Decompress:
                    if (!stream.CanRead)
                    {
                        throw new ArgumentException
                            (("IO_NotReadable"), "stream");
                    }
                    break;
                case CompressionMode.Compress:
                    _compressionlibrary = 2;
                    _compressionalgorithm = 1;
                    _compressionlevel = 1;
#if DEBUG
                    Console.WriteLine(BufferSize);
#endif
                    if (!stream.CanWrite)
                    {
                        throw new ArgumentException
                            (("IO_NotWritable"), "stream");
                    }
                    break;
                default:
                    throw new ArgumentException
                        (("IO_CompressionMode"), "mode");
            }

            //            _scratch = new byte[QlzScratchCompress];
            //            _scratchCompress = new byte[qlz_get_setting(1)];


            //            _scratchDecompress = QlzStreamingBuffer == 0 ? _scratchCompress : new byte[qlz_get_setting(2)];

            BaseStream = stream;
            _mode = mode;
            _leaveOpen = leaveOpen;
        }

        // Get the base _stream that underlies this one.

        ///-------------------------------------------------------------------------------------------------
        /// <summary>	Gets the base stream. </summary>
        ///
        /// <value>	The base stream. </value>
        ///-------------------------------------------------------------------------------------------------
        private Stream BaseStream { get; set; }

        // Determine if the _stream supports reading, writing, or seeking.

        ///-------------------------------------------------------------------------------------------------
        /// <summary>	Gets a value indicating whether we can read. </summary>
        ///
        /// <value>	true if we can read, false if not. </value>
        ///-------------------------------------------------------------------------------------------------
        public override bool CanRead
        {
            get
            {
                return (BaseStream != null &&
                        _mode == CompressionMode.Decompress);
            }
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
                return (BaseStream != null &&
                        _mode == CompressionMode.Compress);
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

        private int[] PacketMap { get; set; }

        public int BufferSize { private get; set; }

        // Flush this _stream.

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
                throw new ObjectDisposedException
                    (("Exception_Disposed"));
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
        /// <param name="buffer">	An array of bytes. When this method returns, the buffer contains the
        /// 						specified byte array with the values between <paramref name="offset" /
        /// 						> and (<paramref name="offset" /> + <paramref name="count" /> - 1)
        /// 						replaced by the bytes read from the current source. </param>
        /// <param name="offset">	The zero-based byte offset in <paramref name="buffer" /> at which to
        /// 						begin storing the data read from the current stream. </param>
        /// <param name="count">	The maximum number of bytes to be read from the current stream. </param>
        ///
        /// <returns>	The total number of bytes read into the buffer. This can be less than the number
        /// 			of bytes requested if that many bytes are not currently available, or zero (0) if
        /// 			the end of the stream has been reached. </returns>
        ///
        /// ### <exception cref="T:System.ArgumentException">			The sum of <paramref name="offset"
        /// 															/> and <paramref name="count" /> is
        /// 															larger than the buffer length. </exception>
        /// ### <exception cref="T:System.ArgumentNullException">		<paramref name="buffer" /> is null.
        /// 															</exception>
        /// ### <exception cref="T:System.ArgumentOutOfRangeException">	<paramref name="offset" /> or <para
        /// 															mref name="count" /> is negative. </exception>
        /// ### <exception cref="T:System.IO.IOException">				An I/O error occurs. </exception>
        /// ### <exception cref="T:System.NotSupportedException">		The stream does not support reading
        /// 															. </exception>
        /// ### <exception cref="T:System.ObjectDisposedException">		Methods were called after the strea
        /// 															m was closed. </exception>
        ///-------------------------------------------------------------------------------------------------
        public override int Read(byte[] buffer, int offset, int count)
        {

            var st = new Stopwatch();
            st.Start();

            var returnrecordcount = 0;

            //is my return buffer bigger or equal to my queue free space?
            //AND
            //do I still have packets to process?
            //are they asking for more data than I can give them?
            //if so reset the count to the available buffer and hope they can 
            //handle it.
            if (count > _fifosize)
                count = _fifosize;
            //throw new Exception("Trying to read more data than exists");

            if ((_packetmappointer < PacketMap.Length && _buffermap[_buffermappointer] <= _bibreadBuffer.Free) &&
                _bibreadBuffer.Free >= count && _recordsread < _decompressedSize)
                EqueueReadData();
            st.Stop();
            Console.WriteLine("EqueueReadData:   " + st.ElapsedTicks);
            st.Reset();

            //does my queue have data in it
            //AND
            //have less than the number of records requested to return?
            st.Start();
            while (_bibreadBuffer.Used > 0 && returnrecordcount < count)
                returnrecordcount = DequeueReadData(ref buffer, ref offset, ref count);
            st.Stop();
            Console.WriteLine("EqueueReadData:   " + st.ElapsedTicks);
            st.Reset();


            //don' t know if I need this second buffer fill since the top gets called every loop
            //is my queue free space equal to or greater than the return buffer request
            //AND
            //do I have any packets left to process?
            //if (_bibreadBuffer.Free >= count && _packetmappointer < PacketMap.Length)
            //  EqueueReadData();

            _recordsread += returnrecordcount;

            return returnrecordcount;
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
        /// ### <exception cref="T:System.ArgumentOutOfRangeException">	<paramref name="offset" /> or <para
        /// 															mref name="count" /> is negative. </exception>
        /// ### <exception cref="T:System.IO.IOException">				An I/O error occurs. </exception>
        /// ### <exception cref="T:System.NotSupportedException">		The stream does not support writing
        /// 															. </exception>
        /// ### <exception cref="T:System.ObjectDisposedException">		Methods were called after the strea
        /// 															m was closed. </exception>
        ///-------------------------------------------------------------------------------------------------
        public override void Write(byte[] buffer, int offset, int count)
        {
            //read from incomming decompressed _stream
            //if we have free queue space
            //we do a final compression of any data that doesn't fit in queue
            //after this
            //if (_bibreadBuffer.Free > count)//.Used < count)

            EnqueueWriteData(ref buffer, ref offset, ref count);

            //write to outgoing compressed _stream _stream
            //we flush this again on despose so don't sweat if
            //our buffer is bigger than all the data that was sent to us.

            if (_bibreadBuffer.Free == 0)// || _bibreadBuffer.Free < count)
                DequeueWriteData();


            //queue up any leftover blocks
            //and write them to the queue to be flushed on despose

            while (_enqueuewriteremander > 0)
            {

                int newoffset = count - _enqueuewriteremander;
#if DEBUG
                Console.WriteLine(_enqueuewriteremander);
                Console.WriteLine(newoffset);
#endif
                EnqueueWriteData(ref buffer, ref newoffset, ref _enqueuewriteremander);
                if (_bibreadBuffer.Used > 0)
                    DequeueWriteData();
            }

            //if (_bibreadBuffer.Used > 0)
            //DequeueWriteData();
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
            Console.WriteLine("timequeue:       " + _timequeue);
            Console.WriteLine("timedequeue:     " + _timedequeue);
            Console.WriteLine("timecomressing:  " + _timecomressing);
            Console.WriteLine("timeread:        " + _timeread);
            Console.WriteLine("timewrite:       " + _timewrite);
            try
            {
                // Flush on the underlying _stream can throw (ex., low disk space)
                if (disposing && BaseStream != null)
                {
                    Flush();

                    #region write header

                    if (_mode == CompressionMode.Compress)
                    {
                        //call one more flush just to be on the safe side
                        if (_bibreadBuffer.Free < _fifosize)
                        {
                            //int flushtail = _fifosize - _bibreadBuffer.Free;
                            DequeueWriteData();
                        }

                        var header = new byte[(_packetlen.Count * 4) + (_bufferlen.Count * 4) + 11];
#if DEBUG
                        Console.WriteLine("----------- Write Header -----------");
                        Console.WriteLine("packetcount:                " + _packetlen.Count);
                        Console.WriteLine("packetcount Size:           " + _packetlen.Count * 4);
                        Console.WriteLine("buffercount:                " + _bufferlen.Count);
                        Console.WriteLine("buffercount Size:           " + _bufferlen.Count * 4);
                        Console.WriteLine("Header Size:                " + header.Length);
#endif
                        _packetmapsize = _packetlen.Count * 4;

                        _buffermapsize = _bufferlen.Count * 4;

                        byte[] pklen;

                        int boffset = 0;
                        //Console.WriteLine("Offset:           "+boffset);

                        foreach (int i in _packetlen)
                        {
                            pklen = BitConverter.GetBytes(i);
                            Buffer.BlockCopy(pklen, 0, header, boffset, 4);
#if DEBUG
                            Console.WriteLine("PacketLen " + (boffset + 4) / 4 +
                                              ":".PadRight(18 - ((boffset + 4) / 4).ToString().Length) + i);
#endif
                            boffset = boffset + 4;
                            //Console.WriteLine("Offset:           " + boffset);
                        }

                        foreach (int i in _bufferlen)
                        {
                            pklen = BitConverter.GetBytes(i);
                            Buffer.BlockCopy(pklen, 0, header, boffset, 4);
#if DEBUG
                            Console.WriteLine("BufferLen " + (boffset / 4 + 1 - _packetmapsize / 4) +
                                              ":".PadRight(18 - (boffset / 4 + 1 - _packetmapsize / 4).ToString().Length) +
                                              i);
#endif
                            boffset = boffset + 4;
                            //Console.WriteLine("Offset:           " + boffset);
                        }

                        Buffer.BlockCopy(BitConverter.GetBytes(_compressionlibrary), 0, header, boffset, 1);
#if DEBUG
                        Console.WriteLine("CompressionLib :            " + _compressionlibrary);
#endif
                        boffset = boffset + 1;
                        Buffer.BlockCopy(BitConverter.GetBytes(_compressionalgorithm), 0, header, boffset, 1);
#if DEBUG
                        Console.WriteLine("CompressionAlg :            " + _compressionalgorithm);
#endif
                        boffset = boffset + 1;
                        Buffer.BlockCopy(BitConverter.GetBytes(_compressionlevel), 0, header, boffset, 1);
#if DEBUG
                        Console.WriteLine("Compressionlvl :            " + _compressionlevel);
#endif
                        boffset = boffset + 1;
                        Buffer.BlockCopy(BitConverter.GetBytes(_packetmapsize), 0, header, boffset, 4);
#if DEBUG
                        Console.WriteLine("Packet Map Size :           " + _packetmapsize);
#endif
                        BaseStream.Seek(0, SeekOrigin.End);
                        boffset = boffset + 4;
                        Buffer.BlockCopy(BitConverter.GetBytes(_buffermapsize), 0, header, boffset, 4);
#if DEBUG
                        Console.WriteLine("Buffer Map Size :           " + _buffermapsize);
#endif
                        BaseStream.Seek(0, SeekOrigin.End);
                        BaseStream.Write(header, 0, header.Length);
                        BaseStream.Flush();
#if DEBUG
                        Console.WriteLine("------------------------------------");
#endif
                    }

                    #endregion

                    #region closestream

                    // Need to do close the output _stream in compression _mode
                    //if (_mode == CompressionMode.Compress && _stream != null)
                    //{
                    //    int bytesCompressed;
                    //    // compress any _bytes left.
                    //    while (!deflater.NeedsInput())
                    //    {
                    //        bytesCompressed = deflater.GetDeflateOutput(buffer);
                    //        if (bytesCompressed != 0)
                    //        {
                    //            _stream.Write(buffer, 0, bytesCompressed);
                    //        }
                    //    }

                    //    // Write the end of compressed _stream data.
                    //    // We can safely do this since the buffer is large enough.
                    //    bytesCompressed = deflater.Finish(buffer);

                    //    if (bytesCompressed > 0)
                    //        _stream.Write(buffer, 0, bytesCompressed);
                    //}

                    #endregion
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

                    //Aced.Compression.AcedInflator.Release();
                    if (disposing && !_leaveOpen && BaseStream != null)
                    {
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
        ///-------------------------------------------------------------------------------------------------
        /// <summary>	Loads a header. </summary>
        ///
        /// <remarks>	Wes Brown, 5/26/2009. </remarks>
        ///
        /// <exception cref="ArgumentNullException">	Thrown when one or more required arguments are null
        /// 											. </exception>
        ///
        /// <param name="compressedFileStream">	The compressed file stream. </param>
        ///-------------------------------------------------------------------------------------------------
        public void LoadHeader(FileStream compressedFileStream)
        {
            if (compressedFileStream == null) throw new ArgumentNullException("compressedFileStream");
#if DEBUG
            Console.WriteLine("----------- Read Header ------------");

            Console.WriteLine("Stream Length:              " + compressedFileStream.Length);
            Console.WriteLine("Stream LHPos:               " + compressedFileStream.Position);
#endif
            compressedFileStream.Seek(-11, SeekOrigin.End);
#if DEBUG
            Console.WriteLine("Stream LHPos: `             " + compressedFileStream.Position);
#endif

            var buffbyte = new byte[1];
            var buffint = new byte[4];

            compressedFileStream.Read(buffbyte, 0, 1);
            _compressionlibrary = buffbyte[0];
#if DEBUG
            Console.WriteLine("_compressionlib :           " + buffbyte[0]);
#endif

            compressedFileStream.Read(buffbyte, 0, 1);
            _compressionalgorithm = buffbyte[0];
#if DEBUG
            Console.WriteLine("_compressionAlg :           " + buffbyte[0]);
#endif
            compressedFileStream.Read(buffbyte, 0, 1);
            _compressionlevel = buffbyte[0];
#if DEBUG
            Console.WriteLine("_compressionLvl :           " + buffbyte[0]);
#endif
            compressedFileStream.Read(buffint, 0, 4);
            _packetmapsize = BitConverter.ToInt32(buffint, 0);
#if DEBUG
            Console.WriteLine("_packetmapsize :            " + BitConverter.ToInt32(buffint, 0));
#endif
            compressedFileStream.Read(buffint, 0, 4);
            _buffermapsize = BitConverter.ToInt32(buffint, 0);
#if DEBUG
            Console.WriteLine("_buffermapsize :            " + BitConverter.ToInt32(buffint, 0));
#endif

            PacketMap = new int[_packetmapsize / 4];
            _buffermap = new int[_buffermapsize / 4];
            _packetmappointer = 0;
            _buffermappointer = 0;
            BufferSize = 0;

            compressedFileStream.Seek(-((_buffermapsize) + 11), SeekOrigin.End);
            int bi = 0;
            for (int i = 0; i < (_buffermapsize); i += 4)
            {
                compressedFileStream.Read(buffint, 0, 4);
                _buffermap[bi] = BitConverter.ToInt32(buffint, 0);
#if DEBUG
                Console.WriteLine("_buffersize :               " + _buffermap[bi]);
#endif
                if (_buffermap[bi] > _fifosize)
                    BufferSize = _buffermap[bi];
                _decompressedSize += _buffermap[bi];
                bi++;
            }

            compressedFileStream.Seek(-((_buffermapsize + _packetmapsize) + 11), SeekOrigin.End);
            int pi = 0;
            for (int i = 0; i < (_packetmapsize); i += 4)
            {
                compressedFileStream.Read(buffint, 0, 4);
                PacketMap[pi] = BitConverter.ToInt32(buffint, 0);
#if DEBUG
                Console.WriteLine("_packetsize :               " + BitConverter.ToInt32(buffint, 0));
#endif
                //if (PacketMap[pi] > _fifosize)
                //    BufferSize = PacketMap[pi];
                pi++;
            }

            if (BufferSize - 400 > _fifosize)
            {
                _fifosize = BufferSize;
                _bibreadBuffer = new BlockQueue(_fifosize, false);
            }


            compressedFileStream.Seek(0, SeekOrigin.Begin);
            compressedFileStream.Flush();
#if DEBUG
            Console.WriteLine("------------------------------------");
#endif
        }

        //private helpters for read and write methods

        ///-------------------------------------------------------------------------------------------------
        /// <summary>	Equeue read data. </summary>
        ///
        /// <remarks>	Wes Brown, 5/26/2009. </remarks>
        ///
        /// <exception cref="Exception">	Thrown when . </exception>
        ///-------------------------------------------------------------------------------------------------
        private unsafe void EqueueReadData()
        {
            //do we have data to decompress
            //do we have enough space in the queue
            //do fill the queue
            //return the number of items written to the queue
            //return any offset that didn't fit into the queue

            //holding variables for the buffer and read counts
            _bytes = 0;
            _dstlen = 0;
            _reccount = 0;

            //get size of our free space in the buffer
            var fb = _bibreadBuffer.Free;

            //while our returned number of records is less than the number we can return AND we have packets left to process.
            //while the number of records decompressed is less than available free space AND we have packets to process
            //sticks in loop where recordsbuffoered is less than fb but there isn't enough space to buffer more data?
            //do we have enough free space to decompress the block?
            while (_recordsbuffered <= fb && (_packetmappointer < PacketMap.Length) &&
                   (fb >= _buffermap[_buffermappointer]))
            {
#if DEBUG
                Console.WriteLine("Free Buffer Space:       " + fb);
                Console.WriteLine("Total Number of Packets: " + PacketMap.Length);
                Console.WriteLine("Current Packet:          " + (_packetmappointer + 1));
#endif
                //try to allocate the local buffer
                try
                {
                    _compressed = new byte[PacketMap[_packetmappointer]];
#if DEBUG
                    Console.WriteLine("packet size:               " + PacketMap[_packetmappointer]); //debug
                    Console.WriteLine("buffer size:               " + _buffermap[_buffermappointer]); //debug
#endif
                }
                catch (Exception ex)
                {
                    //trap the error and throw it back.
                    Console.WriteLine(ex.Message);
                }

                //external compressor handles everything via pointers so lets set that up
                _dstlen = AcedInflator.Instance.Decompress(_compressed, 0, _decompressed, 0);

                //enqueue our decompressed block
                _bibreadBuffer.Put(ref _decompressed, ref _decompressedoffset, ref _dstlen, out _reccount);
#if DEBUG
    //how much space have we consumed?
                    Console.WriteLine(_bibreadBuffer.Used); //debug
#endif
                //incrament the global counter of records buffered.
                _recordsbuffered += _reccount;

                //move our map forward
                _packetmappointer++;
                _buffermappointer++;

#if DEBUG
                    Console.WriteLine("------------------------------------"); //debug
#endif
                fb = _bibreadBuffer.Free;
            }
        } //we are done queueing up data so lets write it out to the calling _stream

        ///-------------------------------------------------------------------------------------------------
        /// <summary>	Dequeue read data. </summary>
        ///
        /// <remarks>	Wes Brown, 5/26/2009. </remarks>
        ///
        /// <param name="buffer">	[in,out] the buffer. </param>
        /// <param name="offset">	[in,out] the offset. </param>
        /// <param name="count">	[in,out] number of. </param>
        ///
        /// <returns>	. </returns>
        ///-------------------------------------------------------------------------------------------------
        private int DequeueReadData(ref byte[] buffer, ref int offset, ref int count)
        {
            int dequeueCount = 0;
            try
            {
                _bibreadBuffer.Get(ref buffer, ref offset, ref count, out dequeueCount);//_reccount
            }
            catch (BlockEmptyQueueException be)
            {
                Console.WriteLine(be);
            }

            //update number of records left in the buffer.
            _recordsbuffered -= dequeueCount;
            //if buffer is exausted then reset it to empty
            if (_recordsbuffered == 0)
            {
                _bibreadBuffer.Clear();
            }
            return dequeueCount;
        }

        ///-------------------------------------------------------------------------------------------------
        /// <summary>	Enqueue write data. </summary>
        ///
        /// <remarks>	Wes Brown, 5/26/2009. </remarks>
        ///
        /// <param name="buffer">	[in,out] the buffer. </param>
        /// <param name="offset">	[in,out] the offset. </param>
        /// <param name="count">	[in,out] number of. </param>
        ///-------------------------------------------------------------------------------------------------
        private void EnqueueWriteData(ref byte[] buffer, ref int offset, ref int count)
        {

            //_timeread = 0;
            //_timewrite = 0;
            //_timecomressing = 0;
            //_timedequeue = 0;
            //_timequeue = 0;

            int equeuedcount;
            int returncount = 0;
            int fb = _bibreadBuffer.Free;
#if DEBUG
            Console.WriteLine("buffer: " + buffer.Length);
            Console.WriteLine("offset: " + offset);
            Console.WriteLine("count: " + count);
            Console.WriteLine("Free: " + fb);
#endif

            //if our free block is greater or equal to our incomming count 
            //and our number of records returned is less than the count
            //keep Enqueueing data from the incomming _stream
            while (fb >= count && returncount < count)
            {
                _bibreadBuffer.Put(ref buffer, ref offset, ref count, out equeuedcount);
                returncount += equeuedcount;
                fb += equeuedcount;
            }

            //if our free block is less than our incomming count but there is still space in the free block
            //then enqueue as much of the incomming _stream to fill the queue completely.
            if (fb <= count && fb > 0)
            {
                _enqueuewriteremander = count - fb;
                _bibreadBuffer.Put(ref buffer, ref offset, ref fb, out equeuedcount);
                returncount += equeuedcount;
            }
            else
            {
                _enqueuewriteremander = 0;
            }

            //if there is anything left in the incomming _stream set the remander to be read after the 
            //dequeue
            if (returncount - count == 0)
                _enqueuewriteremander = 0;
        }

        ///-------------------------------------------------------------------------------------------------
        /// <summary>	Dequeue write data. </summary>
        ///
        /// <remarks>	Wes Brown, 5/26/2009. </remarks>
        ///-------------------------------------------------------------------------------------------------
        private unsafe void DequeueWriteData()
        {
            Console.WriteLine("------------------------------------------------");
            st.Start();

            //_timeread = 0;
            //_timewrite = 0;
            //_timecomressing = 0;
            //_timedequeue = 0;
            //_timequeue = 0;

            //check to see if the queue has data
            int reccnt;
            //holds number of _bytes compressed
            _dstlen = 0;

            //gets the size of the buffer slack it 400 _bytes so we don't overflow the decompress
            int buffsize = _bibreadBuffer.Used; //_fifosize - _bibreadBuffer.Free - 400;
            if (buffsize == 0)
                buffsize = _fifosize;
            //we are starting at the top of the buffer
            int offset = 0;

            //set the compressed space to the size of the buffer + 400 _bytes as per the QuickLZ docs.
            //Aced.Compression.AcedDeflator.Instance.Compress()

//            _scratchCompress = new byte[blz_workmem_size((uint)buffsize)];
            _compressed = new byte[_fifosize];
            //buffsize = _compressed.Length;
            //dump the buffer into the local holding buffer.
            //dunno if this is needed maybe return the pointer to the internal queue buffer??
            _bibreadBuffer.Get(ref _compressbuff, ref offset, ref buffsize, out reccnt);

            st.Stop();
            _timedequeue += st.ElapsedTicks;
            Console.WriteLine("_bibreadBuffer.Get:      " + st.ElapsedTicks);
            st.Reset();

            //compress the compressed buffer into the local holding buffer.
                //call the QuickLZ compressor
                st.Start();
            _dstlen = AcedDeflator.Instance.Compress(_compressbuff, 0, _fifosize, AcedCompressionLevel.Fastest, _compressed,0);
                //_dstlen = (int)blz_pack(source, destination, buffsize, scratch);
                st.Stop();
                _timecomressing += st.ElapsedTicks;
                Console.WriteLine("AcedDeflator:            " + st.ElapsedTicks);
                st.Reset();
                st.Start();
                //add the compressed packet size to the header
                _packetlen.Add((uint)_dstlen);
                //add the decompressed packet size to the header
                _bufferlen.Add(buffsize);
                st.Stop();
                Console.WriteLine("_bufferlen.Add:          " + st.ElapsedTicks);
                st.Reset();
                st.Start();
                //write the compressed data to the target _stream
                BaseStream.Write(_compressed, 0, _dstlen);
                st.Stop();
                _timewrite += st.ElapsedTicks;
                Console.WriteLine("BaseStream.Write:        " + st.ElapsedTicks);
                st.Reset();

#if DEBUG
                Console.WriteLine("buffsize:                   " + buffsize);
                Console.WriteLine("Compressed Size:            " + _dstlen);
                Console.WriteLine("Last Element Added:         " + _packetlen.Count);
#endif
            

            if ((_bibreadBuffer.Used > 0)) return;
            st.Start();
            _recordsbuffered = 0;
            //don't need to call the clear since if head == tail it resets to 0 
            //internally in the BlockQueue
            _bibreadBuffer.Clear();
            st.Stop();
            Console.WriteLine("_bibreadBuffer.Clear():  " + st.ElapsedTicks);
            st.Reset();
            //            Console.ReadKey();

        }

        ///-------------------------------------------------------------------------------------------------
        /// <summary>	Get the length of the expected uncompressed stream. </summary>
        ///
        /// <value>	long Length. </value>
        ///-------------------------------------------------------------------------------------------------
        public override long Length
        {
            get { return _decompressedSize; }
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
            //if (_stream == null)
            //{
            //    throw new ObjectDisposedException
            //        (("Exception_Disposed"));
            //}
            //if (_mode != CompressionMode.Decompress)
            //{
            //    throw new NotSupportedException(("IO_NotSupp_Read"));
            //}
            //return base.BeginRead(buffer, offset, count, callback, state);
        }

        // Wait for an asynchronous read operation to end.

        #endregion
    } ;
}

