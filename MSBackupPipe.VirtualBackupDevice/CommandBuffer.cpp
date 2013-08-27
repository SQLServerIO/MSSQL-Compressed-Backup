/*
	Copyright 2008 Clay Lenhart <clay@lenharts.net>


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


#include "StdAfx.h"
#include "CommandBuffer.h"


using namespace System;
using namespace System::Runtime::InteropServices;

namespace MSBackupPipe
{
	namespace VirtualBackupDevice
	{

		CommandBuffer::CommandBuffer(void)
		{
			mCmd = NULL;
			mCachedArray = gcnew array<unsigned char>(0);
		}



		int CommandBuffer::WriteToStream(Stream^ destination)
		{
			int count = (int)mCmd->size;

			IncreaseCachedArraySize(count);

			WriteToArray(mCachedArray, count);

			destination->Write(mCachedArray, 0, count);

			return count;
		}

		void CommandBuffer::WriteToArray(array<unsigned char>^ ary, int count)
		{
			if (count < 0) 
			{
				throw gcnew System::ArgumentException("count must be non-negative.");
			}

			if (ary->Length < count) 
			{
				throw gcnew System::ArgumentException("Cannot write past the end of parameter 'ary'.");
			}

			
			if (mCmd->size != (UINT32)count) 
			{
				throw gcnew System::ArgumentException("The count parameter must be equal to the Size property of CommandBuffer.");
			}


			if (count > 0) 
			{
				IntPtr buffIp(mCmd->buffer);

				Marshal::Copy(buffIp, ary, 0, count);
			}

		}

		void CommandBuffer::IncreaseCachedArraySize(int minSize)
		{
			if (mCachedArray->Length < minSize)
			{
				mCachedArray = gcnew array<unsigned char>(minSize);
			}
		}

		int CommandBuffer::ReadFromStream(Stream^ source)
		{
			int cmdCount = (int)mCmd->size;

			IncreaseCachedArraySize(cmdCount);

			int count = source->Read(mCachedArray, 0, cmdCount);

			ReadFromArray(mCachedArray, count);

			return count;
		}


		void CommandBuffer::ReadFromArray(array<unsigned char>^ ary, int count)
		{
			if (count < 0) 
			{
				throw gcnew System::ArgumentException("count must be non-negative.");
			}

			if (ary->Length < count) 
			{
				throw gcnew System::ArgumentException("Cannot read past the end of parameter 'ary'.");
			}

			
			if (mCmd->size < (UINT32)count) 
			{
				throw gcnew System::ArgumentException("The count parameter must less than or equal to the Size property of CommandBuffer.");
			}



			IntPtr buffIp(mCmd->buffer);

			Marshal::Copy(ary,0, buffIp, count);

		}


		void CommandBuffer::SetCommand(VDC_Command* cmd)
		{
			mCmd = cmd;
		}

		VDC_Command* CommandBuffer::GetCommand()
		{
			return mCmd;
		}
	}
}

