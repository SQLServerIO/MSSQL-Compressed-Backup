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



#include "VirtualDeviceSetConfig.h"
#include "VirtualDevice.h"
#include "StringWrapper.h"


using namespace System;
using namespace System::Collections::Generic;


namespace MSBackupPipe
{
	namespace VirtualBackupDevice
	{
	


		public ref class VirtualDeviceSet
		{
		public:
			VirtualDeviceSet(void);
			~VirtualDeviceSet(void);

			void CreateEx(String^ instanceName, String^ deviceSetName, VirtualDeviceSetConfig^ config);

			VirtualDeviceSetConfig^ GetConfiguration(Nullable<TimeSpan> timeout);

			VirtualDevice^ OpenDevice(String^ deviceName);

			void SignalAbort();

			void Close();

			//void OpenInSecondaryEx(String^ instanceName, String^ setName);


		private:
			enum class VirtualDeviceSetState
			{
				Unconfigured,
				Configurable,
				Initializing,
				Active,
				NormallyTerminated,
				AbnormallyTerminated
			};

			//String^ mDeviceSetName;
			IClientVirtualDeviceSet2* mVds;
			VirtualDeviceSetState mDeviceSetState;
			//UINT32 mDeviceCount;
		};
	}
}