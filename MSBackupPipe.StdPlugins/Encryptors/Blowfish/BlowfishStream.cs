/****************************************************************************
          Copyright 2002-2003 GL Conseil.  All rights reserved.

    Permission is granted to anyone to use this software for any purpose on
    any computer system, and to alter it and redistribute it, subject
    to the following restrictions:

    1. The author is not responsible for the consequences of use of this
       software, no matter how awful, even if they arise from flaws in it.

    2. The origin of this software must not be misrepresented, either by
       explicit claim or by omission.  Since few users ever read sources,
       credits must appear in the documentation.

    3. Altered versions must be plainly marked as such, and must not be
       misrepresented as being the original software.  Since few users
       ever read sources, credits must appear in the documentation.

    4. This notice may not be removed or altered.

  ****************************************************************************/

using System;
using System.IO;
using Blowfish_NET;
 
namespace FlowGroup.Crypto
{
	/// <summary>
	/// Summary description for BlowfishStream.
	/// </summary>
	public enum BlowfishStreamMode
	{
		Read,
		Write
	}

	/// <summary>
	/// This class encrypt data on writing and decrypt them on reading. 
	/// The stream can be opened in reading or writing mode, but not both, and is not seekable.
	/// The stream uses an internal buffer of 256 bytes. The block is prefix by the length of the
	/// block, minus one (so it fits in a byte value).
	/// </summary>
	public class BlowfishStream : Stream
	{
		static private readonly byte[] signature = { 5, 11, 14, 22, 6, 17, 15, 192};
	
		private System.IO.Stream mStream;
		private Blowfish mBlowfish;
		private BlowfishStreamMode mMode;
		private bool mIsEncrypted = false;
		private byte[] mBuffer;
		private int mCount = 0;
		private byte mOffset = 0;

		/// <summary>
		/// 
		/// </summary>
		/// <param name="stream"></param>
		/// <param name="mode"></param>
		private void _BlowfishStream(Stream stream, BlowfishStreamMode mode)
		{
			mBuffer = new byte[256];
			mStream = stream;
			mMode = mode;
			switch(mode)
			{
				case BlowfishStreamMode.Read:
					mCount = (byte)mStream.Read(mBuffer, 0, 8);
					mOffset = 0;
					if(mCount == 8)
					{
						mIsEncrypted = true;
						while(mCount > 0)
						{
							mCount--;
							if(mBuffer[mCount] != signature[mCount])
							{
								mCount = 8;
								mIsEncrypted = false;
								break;
							} // if
						}
					}
					break;
				case BlowfishStreamMode.Write:
					mIsEncrypted = true;
					mStream.Write(signature, 0, 8);
					break;
			}
		}
		
		/// <summary>
		/// 
		/// </summary>
		/// <param name="stream"></param>
		/// <param name="mode"></param>
		public BlowfishStream(Stream stream, BlowfishStreamMode mode)
		{			
			_BlowfishStream(stream,mode);
		}
		
		/// <summary>
		/// 
		/// </summary>
		/// <param name="stream"></param>
		/// <param name="key"></param>
		/// <param name="mode"></param>
		public BlowfishStream(Stream stream, byte[] key, BlowfishStreamMode mode)
		{
			_BlowfishStream(stream,mode);
			SetKey(key);
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="key"></param>
		public void SetKey(byte[] key)
		{
			mBlowfish = new Blowfish(key);
		}

		/// <summary>
		/// 
		/// </summary>
		public override void Close()
		{
			try
			{
				Flush();
			}
			catch(Exception)
			{
				// does not care, should work anyway!
				//e;
			}
			finally
			{
				mStream.Close();
			}
		}

		/// <summary>
		/// 
		/// </summary>
		public override void Flush()
		{
			if(!mIsEncrypted)
			{
			}
			else if(mMode == BlowfishStreamMode.Write)
			{
				if(mCount > 0)
				{
					int bound = ((mCount % 8) == 0) ? mCount : (((mCount / 8) + 1) * 8);
					mBlowfish.Encrypt(mBuffer, mBuffer, 0, 0, bound);
					mStream.WriteByte((byte)(mCount - 1));
					mStream.Write(mBuffer, 0, bound);
					mCount = 0;
					mOffset = 0;
				}
			}
			mStream.Flush();
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="buffer"></param>
		/// <param name="offset"></param>
		/// <param name="count"></param>
		/// <returns></returns>
		public override int Read(byte[] buffer, int offset, int count)
		{
			if(mIsEncrypted)
			{
				int totalRead = 0;
				int read = 0;
				int bound;

				// while data are required, fill the internal buffer
				while(count > 0)
				{
					if(mCount == 0)
					{
						int nRead = 0;
						read = mStream.ReadByte();
						if(read == -1)
							break; // the stream is empty
						mCount = read + 1;
						mOffset = 0;
						bound = ((mCount % 8) == 0) ? mCount : (((mCount / 8) + 1) * 8);
						read = mStream.Read(mBuffer, mOffset, bound);
						nRead += read;
						if(bound != read)
						{
							int nMaxCount = 256;
							while(nRead < bound)
							{
								read = mStream.Read(mBuffer, mOffset+nRead, bound-nRead);
								nRead += read;

								nMaxCount--;
								if (nMaxCount <= 0)
									throw new System.IO.IOException();
							}
						}
						mBlowfish.Decrypt(mBuffer, mBuffer, mOffset, mOffset, bound);				
					}

					buffer[offset++] = mBuffer[mOffset++];
					count--;
					mCount--;
					totalRead++;
				}
				return totalRead;
			}
			else
			{
				int cbRead = 0;
				if(mCount > 0)
				{
					while((mOffset < 8) && (count > 0))
					{
						buffer[offset++] = mBuffer[mOffset++];
						count--;
						cbRead++;
					}
					if(mOffset == 8)
					{ // the "wrong" signature has been transfered back to the reader,
					  // so empty the buffer
						mCount = 0;
						mOffset = 0;
					}
				}
				cbRead += mStream.Read(buffer, offset, count);
				return cbRead;
			}
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="buffer"></param>
		/// <param name="offset"></param>
		/// <param name="count"></param>
		public override void Write(byte[] buffer, int offset, int count)
		{
			if(mIsEncrypted)
			{
				// while data are required, fill the internal buffer
				while(count > 0)
				{
					if(mCount == 256)
					{
						mStream.WriteByte(255);
						mBlowfish.Encrypt(mBuffer, mBuffer, 0, 0, 256);
						mStream.Write(mBuffer, 0, 256);
						mOffset = 0;
						mCount = 0;
					}

					mBuffer[mOffset++] = buffer[offset++];
					count--;
					mCount++;
				}
			}
			else
				mStream.Write(buffer, offset, count);
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="offset"></param>
		/// <param name="origin"></param>
		/// <returns></returns>
		public override long Seek(long offset, System.IO.SeekOrigin origin)
		{
			if(mIsEncrypted)
				throw new System.NotSupportedException();
			return mStream.Seek(offset, origin);
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="value"></param>
		public override void SetLength(long value)
		{
			if(mIsEncrypted)
				throw new NotSupportedException();
			mStream.SetLength(value);
		}

		/// <summary>
		/// 
		/// </summary>
		public override bool CanRead
		{
			get
			{
				return mIsEncrypted ? (mMode == BlowfishStreamMode.Read) :  mStream.CanRead;
			}
		}

		/// <summary>
		/// 
		/// </summary>
		public override bool CanSeek
		{
			get
			{
				return mIsEncrypted ? false : mStream.CanSeek;
			}
		}

		/// <summary>
		/// 
		/// </summary>
		public override bool CanWrite
		{
			get
			{
				return mIsEncrypted ? (mMode == BlowfishStreamMode.Write) :  mStream.CanWrite;
			}
		}

		/// <summary>
		/// 
		/// </summary>
		public override long Length
		{
			get
			{
				if(mIsEncrypted)
					throw new System.NotSupportedException();
				return mStream.Length;
			}
		}

		/// <summary>
		/// 
		/// </summary>
		public override long Position
		{
			get
			{
				if(mIsEncrypted)
					throw new System.NotSupportedException();
				return mStream.Position;
			}
			set
			{
				if(mIsEncrypted)
					throw new System.NotSupportedException();
				mStream.Position = value;
			}
		}
	}
}
