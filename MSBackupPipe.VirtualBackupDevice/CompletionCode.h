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

		public enum class CompletionCode
		{
			Success = ERROR_SUCCESS,
			CA_SUPPRESS_MESSAGE("Microsoft.Naming", "CA1704:IdentifiersShouldBeSpelledCorrectly", MessageId="Eof")
			HandleEof = ERROR_HANDLE_EOF,
			DiskFull = ERROR_DISK_FULL,
			NotSupported  = ERROR_NOT_SUPPORTED,
			NoDataDetected = ERROR_NO_DATA_DETECTED,
			CA_SUPPRESS_MESSAGE("Microsoft.Naming", "CA1704:IdentifiersShouldBeSpelledCorrectly", MessageId="Filemark")
			FilemarkDetected = ERROR_FILEMARK_DETECTED,
			EomOverflow = ERROR_EOM_OVERFLOW,
			EndOfMedia = ERROR_END_OF_MEDIA,
			OperationAborted = ERROR_OPERATION_ABORTED

		};
	}
}