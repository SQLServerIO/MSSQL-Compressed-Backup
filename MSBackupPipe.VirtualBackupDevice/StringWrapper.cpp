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
#include "StringWrapper.h"

using namespace System::Runtime::InteropServices;

namespace MSBackupPipe
{
	namespace VirtualBackupDevice
	{
		StringWrapper::StringWrapper(String^ s)
		{
			mIntPtr = IntPtr::Zero;
			if (!String::IsNullOrEmpty(s)) 
			{
				mIntPtr = Marshal::StringToHGlobalUni(s);
			}
		}


		StringWrapper::~StringWrapper(void)
		{
			Marshal::FreeHGlobal(mIntPtr);
		}

		LPCWSTR StringWrapper::ToPointer()
		{
			LPCWSTR instanceNamePtr = NULL;
			if (mIntPtr != IntPtr::Zero) 
			{
				instanceNamePtr = (LPCWSTR)mIntPtr.ToPointer();
			}

			return instanceNamePtr;
		}
	}
}