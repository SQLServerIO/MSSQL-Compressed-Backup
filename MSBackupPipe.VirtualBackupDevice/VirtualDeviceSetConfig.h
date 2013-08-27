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



using namespace System;

namespace MSBackupPipe
{
	namespace VirtualBackupDevice
	{

		[Flags]
		public enum class FeatureBits : int
		{
			None = 0,
			Removable = VDF_Removable,
			FileMarks = VDF_FileMarks,
			RandomAccess = VDF_RandomAccess ,
			Rewind = VDF_Rewind,
			Position = VDF_Position,
			SkipBlocks = VDF_SkipBlocks,
			ReversePosition = VDF_ReversePosition,
			Discard = VDF_Discard,
			SnapshotPrepare = VDF_SnapshotPrepare,
			WriteMedia = VDF_WriteMedia,
			ReadMedia = VDF_ReadMedia
		};



		public ref class VirtualDeviceSetConfig
		{
		public:
			VirtualDeviceSetConfig(void);

			property UINT32 DeviceCount
			{
				UINT32 get() { return mDeviceCount; }
				void set(UINT32 val) { mDeviceCount = val; }
			}

			property FeatureBits Features
			{
				FeatureBits get() { return mFeatures; }
				void set(FeatureBits val) { mFeatures = val; }
			}

			property UINT32 PrefixZoneSize
			{
				UINT32 get() { return mPrefixZoneSize; }
				void set(UINT32 val) { mPrefixZoneSize = val; }
			}

			property UINT32 Alignment
			{
				UINT32 get() { return mAlignment; }
				void set(UINT32 val) { mAlignment = val; }
			}

			property UINT32 SoftFileMarkBlockSize
			{
				UINT32 get() { return mSoftFileMarkBlockSize; }
				void set(UINT32 val) { mSoftFileMarkBlockSize = val; }
			}

			property UINT32 EomWarningSize
			{
				UINT32 get() { return mEomWarningSize; }
				void set(UINT32 val) { mEomWarningSize = val; }
			}

			property Nullable<TimeSpan> ServerTimeout
			{
				Nullable<TimeSpan> get() { return mServerTimeout; }
				void set(Nullable<TimeSpan> val) { mServerTimeout = val; }
			}


			
		internal:
			void CopyTo(VDConfig* config);
			void CopyFrom(const VDConfig* config);

		private:
			UINT32 mDeviceCount;
			FeatureBits mFeatures;
			UINT32 mPrefixZoneSize;
			UINT32 mAlignment;
			UINT32 mSoftFileMarkBlockSize;
			UINT32 mEomWarningSize;
			Nullable<TimeSpan> mServerTimeout;

		};


		public ref class FeatureSet sealed
		{
		public:
			literal FeatureBits PipeLike = FeatureBits::None;
			literal FeatureBits TapeLike = FeatureBits::FileMarks | FeatureBits::Removable | FeatureBits::ReversePosition | FeatureBits::Rewind | FeatureBits::Position | FeatureBits::SkipBlocks;
			literal FeatureBits DiskLike = FeatureBits::RandomAccess;
		private:
			// Don't allow instances of this class
			FeatureSet() {};
		};
	}
}