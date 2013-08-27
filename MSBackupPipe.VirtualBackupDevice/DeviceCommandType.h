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

namespace MSBackupPipe
{
	namespace VirtualBackupDevice
	{
		CA_SUPPRESS_MESSAGE("Microsoft.Design", "CA1008:EnumsShouldHaveZeroValue")
		public enum class DeviceCommandType
		{
			Read = VDC_Read,
			Write = VDC_Write,
			ClearError = VDC_ClearError,
			Rewind = VDC_Rewind,
			WriteMark = VDC_WriteMark,
			SkipMarks = VDC_SkipMarks,
			SkipBlocks = VDC_SkipBlocks,
			Load = VDC_Load,
			GetPosition = VDC_GetPosition,
			SetPosition = VDC_SetPosition,
			Discard = VDC_Discard,
			Flush = VDC_Flush,
			Snapshot = VDC_Snapshot,
			PrepareToFreeze = VDC_PrepareToFreeze,
			MountSnapshot = VDC_MountSnapshot
		};
	}
}