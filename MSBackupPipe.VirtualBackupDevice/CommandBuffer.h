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


#pragma once

#include "Stdafx.h"



#include "DeviceCommandType.h"


using namespace System::IO;




namespace MSBackupPipe
{
	namespace VirtualBackupDevice
	{


		public ref class CommandBuffer
		{
		public:
			CommandBuffer();
			int WriteToStream(Stream^ destination);
			int ReadFromStream(Stream^ source);


			property DeviceCommandType CommandType
			{
				DeviceCommandType get() { return (DeviceCommandType)mCmd->commandCode; }
			}

			property UINT64 Position
			{
				UINT64 get() { return mCmd->position; }
			}

			property UINT32 Size
			{
				UINT32 get() { return mCmd->size; }
			}

			property bool TimedOut
			{
				bool get() { return mTimedOut; }
			}

		internal:
			void SetCommand(VDC_Command* cmd);
			VDC_Command* GetCommand();

			bool mTimedOut;

		private:
			VDC_Command* mCmd;
			array<unsigned char>^ mCachedArray;

			void IncreaseCachedArraySize(int minSize);

			// maybe these should be public one day:
			void WriteToArray(array<unsigned char>^ ary, int count);
			void ReadFromArray(array<unsigned char>^ ary, int count);

		};
	}
}