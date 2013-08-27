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

#include "Stdafx.h"
#include "VirtualDeviceSet.h"



#include "vdiguid.h"


namespace MSBackupPipe
{
	namespace VirtualBackupDevice
	{
		VirtualDeviceSet::VirtualDeviceSet(void)
		{
			mVds = NULL;
			mDeviceSetState = VirtualDeviceSetState::Unconfigured;

			HRESULT hr = CoInitializeEx(NULL, COINIT_MULTITHREADED);
			if (!SUCCEEDED(hr)) 
			{
				throw gcnew System::InvalidProgramException(String::Format("Failed to Coinit: x{0:X}", hr));
			}


			IClientVirtualDeviceSet2* vds;
			hr = CoCreateInstance(CLSID_MSSQL_ClientVirtualDeviceSet, NULL, CLSCTX_INPROC_SERVER, IID_IClientVirtualDeviceSet2, (void**)&vds);
			mVds = vds;
			if (!SUCCEEDED(hr)) 
			{
				throw gcnew System::InvalidProgramException(String::Format("Could not create an instance: CLSID_MSSQL_ClientVirtualDeviceSet, x{0:X}", hr));
			}
		}

		VirtualDeviceSet::~VirtualDeviceSet(void) 
		{
			if (mVds != NULL) 
			{
				mVds->Close();
				mVds->Release();
			}
			mVds = NULL;
			mDeviceSetState = VirtualDeviceSetState::Unconfigured;

		}

		CA_SUPPRESS_MESSAGE("Microsoft.Naming", "CA1711:IdentifiersShouldNotHaveIncorrectSuffix")
		void VirtualDeviceSet::CreateEx(String^ instanceName, String^ deviceSetName, VirtualDeviceSetConfig^ config)
		{
			if (mVds == NULL) 
			{
				throw gcnew System::InvalidProgramException(String::Format("The virtual device does not have an internal device set object."));
			}

			if (mDeviceSetState != VirtualDeviceSetState::Unconfigured) 
			{
				throw gcnew System::InvalidProgramException(String::Format("The virtual device set must be unconfigured."));
			}



			StringWrapper sInstanceName(instanceName);
			StringWrapper sDeviceSetName(deviceSetName);

			
			VDConfig vDConfig;
			config->CopyTo(&vDConfig);



			HRESULT hr = mVds->CreateEx(sInstanceName.ToPointer(), sDeviceSetName.ToPointer(), &vDConfig);
			if (!SUCCEEDED(hr)) 
			{
				throw gcnew System::InvalidProgramException(String::Format("VDS::Create failed: x{0:X}", hr));
			}

			mDeviceSetState = VirtualDeviceSetState::Configurable;

			//mDeviceCount = vDConfig.deviceCount;
			//mDeviceSetName = deviceSetName;

		}


		VirtualDeviceSetConfig^ VirtualDeviceSet::GetConfiguration(Nullable<TimeSpan> timeout)
		{
			if (mVds == NULL) 
			{
				throw gcnew System::InvalidProgramException(String::Format("The virtual device does not have an internal device set object."));
			}

			if (mDeviceSetState != VirtualDeviceSetState::Configurable) 
			{
				throw gcnew System::InvalidProgramException(String::Format("GetConfiguration() must be called after CreateEx()."));
			}

			DWORD dwTimeout = INFINITE;
			if (timeout.HasValue) 
			{
				dwTimeout = Convert::ToUInt32(timeout.Value.TotalMilliseconds);
			}

			VDConfig vDConfig;
			memset(&vDConfig, 0, sizeof(vDConfig));  

			HRESULT hr = mVds->GetConfiguration(dwTimeout, &vDConfig);
			if (!SUCCEEDED(hr)) 
			{
				throw gcnew System::InvalidProgramException(String::Format("VDS::GetConfiguration failed: x{0:X}", hr));
			}

			VirtualDeviceSetConfig^ config = gcnew VirtualDeviceSetConfig();
			config->CopyFrom(&vDConfig);


			mDeviceSetState = VirtualDeviceSetState::Initializing;

			return config;
		}
		
		VirtualDevice^ VirtualDeviceSet::OpenDevice(String^ deviceName)
		{
			if (mVds == NULL) 
			{
				throw gcnew System::InvalidProgramException(String::Format("The virtual device does not have an internal device set object."));
			}

			if (mDeviceSetState != VirtualDeviceSetState::Initializing && mDeviceSetState != VirtualDeviceSetState::Active) 
			{
				throw gcnew System::InvalidProgramException(String::Format("OpenDevice() must be called after GetConfiguration()."));
			}


			StringWrapper sDeviceName(deviceName);

			IClientVirtualDevice* ppVirtualDevice;

			HRESULT hr = mVds->OpenDevice(sDeviceName.ToPointer(), &ppVirtualDevice);
			if (!SUCCEEDED(hr)) 
			{
				throw gcnew System::InvalidProgramException(String::Format("VDS::SignalAbort failed: x{0:X}", hr));
			}

			VirtualDevice^ result = gcnew VirtualDevice(ppVirtualDevice);

			mDeviceSetState = VirtualDeviceSetState::Active;

			return result;

		}
		
		void VirtualDeviceSet::SignalAbort()
		{
			if (mVds == NULL) 
			{
				throw gcnew System::InvalidProgramException(String::Format("The virtual device does not have an internal device set object."));
			}

			HRESULT hr = mVds->SignalAbort();
			if (!SUCCEEDED(hr)) 
			{
				throw gcnew System::InvalidProgramException(String::Format("VDS::SignalAbort failed: x{0:X}", hr));
			}

			mDeviceSetState = VirtualDeviceSetState::AbnormallyTerminated;

		}

		
		void VirtualDeviceSet::Close()
		{
			if (mVds == NULL) 
			{
				throw gcnew System::InvalidProgramException(String::Format("The virtual device does not have an internal device set object."));
			}

			HRESULT hr = mVds->Close();
			if (!SUCCEEDED(hr)) 
			{
				throw gcnew System::InvalidProgramException(String::Format("VDS::Close failed: x{0:X}", hr));
			}

			if (mDeviceSetState == VirtualDeviceSetState::AbnormallyTerminated || mDeviceSetState == VirtualDeviceSetState::NormallyTerminated)
			{
				mDeviceSetState = VirtualDeviceSetState::Unconfigured;
			}
			else 
			{
				mDeviceSetState = VirtualDeviceSetState::AbnormallyTerminated;
			}
		}
	}
}