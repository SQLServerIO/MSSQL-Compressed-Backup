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
#include "VirtualDeviceSetConfig.h"

namespace MSBackupPipe
{
	namespace VirtualBackupDevice
	{


		VirtualDeviceSetConfig::VirtualDeviceSetConfig(void)
		{
			DeviceCount = 0;
			Features = FeatureSet::PipeLike;
			PrefixZoneSize = 0;
			Alignment = 0;
			SoftFileMarkBlockSize = 0;
			EomWarningSize = 0;
			ServerTimeout = Nullable<TimeSpan>();
			
		}

		void VirtualDeviceSetConfig::CopyTo(VDConfig* config) 
		{
			config->deviceCount = this->DeviceCount;
			config->alignment = this->Alignment;
			config->blockSize = 0;
			config->bufferAreaSize = 0;
			config->EOMWarningSize = this->EomWarningSize;
			config->features = (UINT32)this->Features;
			config->maxIODepth = 0;
			config->maxTransferSize = 0;
			config->prefixZoneSize = this->PrefixZoneSize;
			if (this->ServerTimeout.HasValue) 
			{
				config->serverTimeOut = Convert::ToUInt32(this->ServerTimeout.Value.TotalMilliseconds);
			}
			else 
			{
				config->serverTimeOut = 0;
			}
			config->softFileMarkBlockSize = this->SoftFileMarkBlockSize;
		}


		void VirtualDeviceSetConfig::CopyFrom(const VDConfig* config)
		{
			this->DeviceCount = config->deviceCount;
			this->Alignment = config->alignment;
			this->EomWarningSize = config->EOMWarningSize;
			this->Features = (FeatureBits)(int)config->features;
			this->PrefixZoneSize = config->prefixZoneSize;
			if (config->serverTimeOut == 0) 
			{
				this->ServerTimeout = Nullable<TimeSpan>();
			}
			else 
			{
				this->ServerTimeout = TimeSpan::FromMilliseconds(Convert::ToDouble((UINT32)config->serverTimeOut));
			}
			this->SoftFileMarkBlockSize = config->softFileMarkBlockSize;
		}


	}
}