/*
	Copyright 2009 Clay Lenhart <clay@lenharts.net>


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


using System;
using System.Collections.Generic;
using System.Text;

namespace MSBackupPipe.Common
{
    class DisposableList<T> : List<T>, IDisposable where T : IDisposable
    {
        private bool mDisposed = false;

        public DisposableList()
            : base()
        {
        }

        public DisposableList(IEnumerable<T> collection)
            : base(collection)
        {
        }

        public DisposableList(int capacity)
            : base(capacity)
        {
        }

        #region IDisposable Members


        public void Dispose()
        {
            Dispose(true);
        }

        ~DisposableList()
        {
            Dispose(false);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!mDisposed)
            {
                if (disposing)
                {
                    foreach (T t in this)
                    {
                        t.Dispose();
                    }
                }

                // There are no unmanaged resources to release, but
                // if we add them, they need to be released here.
            }
            mDisposed = true;

            // If it is available, make the call to the
            // base class's Dispose(Boolean) method
            //base.Dispose(disposing);

        }



        #endregion
    }
}
