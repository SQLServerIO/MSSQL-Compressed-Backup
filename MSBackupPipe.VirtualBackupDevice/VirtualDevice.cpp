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
#include "VirtualDevice.h"

namespace MSBackupPipe
{
	namespace VirtualBackupDevice
	{
		VirtualDevice::VirtualDevice(IClientVirtualDevice* dev)
		{
			mDevice = dev;
		}

		bool VirtualDevice::GetCommand(Nullable<TimeSpan> timeout, CommandBuffer^ buff)
		{

			DWORD dwTimeOut = INFINITE;
			if (timeout.HasValue) 
			{
				dwTimeOut = Convert::ToUInt32(timeout.Value.TotalMilliseconds);
			}



			VDC_Command* cmd;

			HRESULT hr = mDevice->GetCommand(dwTimeOut, &cmd);
			if (SUCCEEDED(hr))
			{
				buff->SetCommand(cmd);
				buff->mTimedOut = false;
				return true;
			}
			else if (hr == VD_E_TIMEOUT)
			{
				buff->mTimedOut = true;
				return true;
			}
			else
			{
				if (hr == VD_E_CLOSE)
				{
					buff->mTimedOut = false;
					return false; // EOF
				}

				throw gcnew InvalidProgramException(String::Format("Unable to get the next command: {0}.", hr));
			}
			


		}


		void VirtualDevice::CompleteCommand(CommandBuffer^ buff, CompletionCode completionCode, UINT32 bytesTransferred, UINT64 position)
		{
			HRESULT hr;
			if (!SUCCEEDED(hr = mDevice->CompleteCommand(buff->GetCommand(), (UINT32)completionCode, bytesTransferred, position)))
			{
				throw gcnew InvalidProgramException(String::Format("Unable to complete the command: {0}.", hr));
			}
		}


	}
}