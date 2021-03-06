/*************************************************************************************\
File Name  :  SqlThread.cs
Project    :  MSSQL Compressed Backup

Copyright 2009 Clay Lenhart <clay@lenharts.net>

This file is part of MSSQL Compressed Backup.

MSSQL Compressed Backup is free software: you can redistribute it and/or modify
it under the terms of the GNU General Public License as published by
the Free Software Foundation, either version 3 of the License, or
(at your option) any later version.

MSSQL Compressed Backup is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
GNU General Public License for more details.

You should have received a copy of the GNU General Public License
along with MSSQL Compressed Backup.  If not, see <http://www.gnu.org/licenses/>.

THIS CODE AND INFORMATION IS PROVIDED "AS IS" WITHOUT WARRANTY OF ANY KIND, 
EITHER EXPRESSED OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE IMPLIED 
WARRANTIES OF MERCHANTABILITY AND/OR FITNESS FOR A PARTICULAR PURPOSE.
\*************************************************************************************/

using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;

//Additional includes other than default dot net framework should go here.
using MSBackupPipe.StdPlugins;

namespace MSBackupPipe.Common
{
    class SqlThread : IDisposable
    {
        //private Exception mException;
        private SqlConnection _mCnn;
        private SqlCommand _mCmd;
        IAsyncResult _mAsyncResult;
        private bool _mDisposed;

        /// <summary>
        /// Returns the auto-generated device names
        /// </summary>
        public List<string> PreConnect(string clusterNetworkName, string instanceName, string deviceSetName, int numDevices, IBackupDatabase dbComponent, Dictionary<string, List<string>> dbConfig, int isBackup, IUpdateNotification notifier, out long estimatedTotalBytes)
        {
            var serverConnectionName = clusterNetworkName ?? ".";
            var dataSource = string.IsNullOrEmpty(instanceName) ? serverConnectionName : string.Format(@"{0}\{1}", serverConnectionName, instanceName);
            var connectionString = dbConfig.ContainsKey("user") ? string.Format("Data Source={0};Initial Catalog=master;Integrated Security=False;User ID={1};Password={2};Asynchronous Processing=true;;Connect Timeout=2147483647", dataSource, dbConfig["user"][0], dbConfig.ContainsKey("password") ? dbConfig["password"][0] : null) : string.Format("Data Source={0};Initial Catalog=master;Integrated Security=True;Asynchronous Processing=true;Connect Timeout=2147483647", dataSource);
            

            notifier.OnConnecting(string.Format("Connecting: {0}", connectionString));

            _mCnn = new SqlConnection(connectionString);
            _mCnn.Open();

            var deviceNames = new List<string>(numDevices) {deviceSetName};
            for (var i = 1; i < numDevices; i++)
            {
                deviceNames.Add(Guid.NewGuid().ToString());
            }

            _mCmd = new SqlCommand {Connection = _mCnn, CommandTimeout = 0};
            estimatedTotalBytes = 0;
            //Console.WriteLine(mCmd);
            switch (isBackup)
            {
                case 1:
                    dbComponent.ConfigureBackupCommand(dbConfig, deviceNames, _mCmd);
                    estimatedTotalBytes = CalculateEstimatedDatabaseSize(_mCnn, dbConfig);
                    break;
                case 2:
                    dbComponent.ConfigureRestoreCommand(dbConfig, deviceNames, _mCmd);
                    estimatedTotalBytes = 0;
                    break;
                case 3:
                    dbComponent.ConfigureVerifyCommand(dbConfig, deviceNames, _mCmd);
                    estimatedTotalBytes = 0;
                    break;

                default:
                    Console.WriteLine("Default case");
                    break;
            }

            return deviceNames;

        }

        private static long CalculateEstimatedDatabaseSize(SqlConnection cnn, Dictionary<string, List<string>> dbConfig)
        {

            var version = GetVersion(cnn);

            var backupType = "full";
            if (dbConfig.ContainsKey("backuptype"))
            {
                backupType = dbConfig["backuptype"][0].ToLowerInvariant();
            }
            if (backupType != "full" && backupType != "differential" && backupType != "log")
            {
                throw new ArgumentException(string.Format("db: Unknown backuptype: {0}", backupType));
            }

            var databaseName = dbConfig["database"][0];
            if (string.IsNullOrEmpty(databaseName))
            {
                throw new ArgumentException(string.Format("db: database parameter required"));
            }

            if (databaseName.Contains("[") || databaseName.Contains("]"))
            {
                throw new ArgumentException(string.Format("The database name cannot contain [ or ] to avoid SQL injection attacks: {0}", databaseName));
            }

            // full backups can use sp_spaceused() to calculate the backup size: http://msdn.microsoft.com/en-us/library/ms188776.aspx
            if (backupType == "full")
            {
                using (var cmd = new SqlCommand(string.Format("use [{0}]; exec sp_spaceused", databaseName), cnn))
                {
                    cmd.CommandTimeout = 0;
                    using (var reader = cmd.ExecuteReader())
                    {
                        reader.NextResult();
                        reader.Read();
                        var sizeStr = reader.GetString(reader.GetOrdinal("reserved"));
                        if (sizeStr.Contains("KB"))
                        {
                            var pos = sizeStr.IndexOf("KB", StringComparison.Ordinal);
                            return long.Parse(sizeStr.Substring(0, pos)) * 1024L;
                        }
                        // I don't know if this will occur:
                        if (sizeStr.Contains("MB"))
                        {
                            var pos = sizeStr.IndexOf("MB", StringComparison.Ordinal);
                            return long.Parse(sizeStr.Substring(0, pos)) * 1024L * 1024L;
                        }
                        throw new InvalidCastException(string.Format("Unknown units (usually this is KB): {0}", sizeStr));
                    }
                }
            }

            // differiential? DIFF_MAP? http://social.msdn.microsoft.com/Forums/en-SG/sqldisasterrecovery/thread/7a5ea034-9c5a-4531-a0b3-40f67c9cef4a
            // Paul's got it. :)  Code for differential estimation heavily influenced by:
            // http://www.sqlskills.com/BLOGS/PAUL/post/New-script-How-much-of-the-database-has-changed-since-the-last-full-backup.aspx
            if (backupType == "differential")
            {
                const string commandFor2005Or2008 = @"
                    /* 2005 and 2008 */

                    DECLARE @dbName nvarchar(128);
                    SET @dbName = @dbNameParam;

                    DECLARE dbFiles CURSOR FOR
	                    SELECT file_id, size
	                    FROM master.sys.master_files with(NOLOCK)
	                    WHERE type_desc = 'ROWS'
		                    AND state_desc = 'ONLINE'
		                    AND database_id = DB_ID(@dbName)
                    	
                    DECLARE @result TABLE
                    (
	                    Field varchar(100)
                    )	
                    	
                    DECLARE @file_id int;
                    DECLARE @size int;
                    OPEN dbFiles;

                    FETCH NEXT FROM dbFiles INTO @file_id, @size;

                    WHILE @@FETCH_STATUS = 0
                    BEGIN
	                    DECLARE @extentID int;
	                    SET @extentID = 0;
                    	
	                    WHILE (@extentID < @size)
	                    BEGIN
		                    DECLARE @pageID int;
		                    SET @pageID = @extentID + 6;
                    		
		                    DECLARE @sql nvarchar(1000);
		                    SET @sql = 'DBCC PAGE (@sqlDbName,' 
			                    + CAST(@file_id as varchar(20)) + ','
			                    + CAST(@pageID as varchar(20)) + ','
			                    + '3) WITH TABLERESULTS, NO_INFOMSGS';
                    			
		                    DECLARE @tempDbccPage TABLE
		                    (
			                    ParentObject varchar(100),
			                    [Object] varchar(100),
			                    Field varchar(100),
			                    Value varchar(100)
		                    );	
                            DELETE FROM @tempDbccPage;
		                    INSERT INTO @tempDbccPage EXEC sp_executesql @sql, N'@sqlDbName nvarchar(128)', @sqlDbName = @dbName;
                    		
		                    INSERT INTO @result (Field) 
		                    SELECT Field
		                    FROM @tempDbccPage
		                    WHERE Value = '    CHANGED'
			                    AND ParentObject LIKE 'DIFF_MAP%';
                    		
		                    SET @extentID = @extentID + 511232;
	                    END
                    	
	                    FETCH NEXT FROM dbFiles INTO @file_id, @size;

                    END

                    CLOSE dbFiles;
                    DEALLOCATE dbFiles;

                    SELECT Field 
                    from @result;
                    ";

                var sql2000 = @"/* 2000 */

					USE [{0}];

                    DECLARE @dbName nvarchar(128);
                    SET @dbName = DB_NAME();

                    DECLARE dbFiles CURSOR FOR
	                    SELECT fileid, size
	                    FROM sysfiles WITH(NOLOCK)
	                    WHERE (status & 0x2) > 0 AND (status & 0x40) = 0
                    	
                    DECLARE @result TABLE
                    (
	                    Field varchar(100)
                    )	
                    
					CREATE TABLE #tempDbccPage 
					(
						ParentObject varchar(100),
						[Object] varchar(100),
						Field varchar(100),
						Value varchar(100)
					);	
                    	
                    DECLARE @file_id int;
                    DECLARE @size int;
                    OPEN dbFiles;

                    FETCH NEXT FROM dbFiles INTO @file_id, @size;

                    WHILE @@FETCH_STATUS = 0
                    BEGIN
	                    DECLARE @extentID int;
	                    SET @extentID = 0;
                    	
	                    WHILE (@extentID < @size)
	                    BEGIN
		                    DECLARE @pageID int;
		                    SET @pageID = @extentID + 6;
                    		
		                    DECLARE @sql nvarchar(1000);
		                    SET @sql = 'DBCC PAGE (@sqlDbName,' 
			                    + CAST(@file_id as varchar(20)) + ','
			                    + CAST(@pageID as varchar(20)) + ','
			                    + '3) WITH TABLERESULTS, NO_INFOMSGS';
		                   
                            DELETE FROM #tempDbccPage;
		                    INSERT INTO #tempDbccPage EXEC sp_executesql @sql, N'@sqlDbName nvarchar(128)', @sqlDbName = @dbName;
                    		
		                    INSERT INTO @result (Field) 
		                    SELECT Field
		                    FROM #tempDbccPage
		                    WHERE Value = '    CHANGED'
			                    AND ParentObject LIKE 'DIFF_MAP%';
                    		
		                    SET @extentID = @extentID + 511232;
	                    END
                    	
	                    FETCH NEXT FROM dbFiles INTO @file_id, @size;

                    END

                    CLOSE dbFiles;
                    DEALLOCATE dbFiles;

                    SELECT Field 
                    from @result;";

                var majorVersionNum = GetMajorVersionNumber(version);
                if (majorVersionNum < 8)
                {
                    throw new InvalidOperationException(string.Format("Differential backups only work on SQL Server 2000 or greater. Version found: {0}", version));
                }

                sql2000 = string.Format(sql2000, databaseName);

                var sql = majorVersionNum == 8 ? sql2000 : commandFor2005Or2008;

                using (var cmd = new SqlCommand(string.Format(sql, databaseName), cnn))
                {
                    cmd.CommandTimeout = 0;
                    var param = cmd.CreateParameter();
                    param.SqlDbType = SqlDbType.NVarChar;
                    param.Size = 128;
                    param.Direction = ParameterDirection.Input;
                    param.ParameterName = "dbNameParam";
                    param.Value = databaseName;
                    cmd.Parameters.Add(param);
                    cmd.CommandTimeout = (int)TimeSpan.FromMinutes(10).TotalSeconds;

                    //dbNameParam
                    using (var reader = cmd.ExecuteReader())
                    {
                        long size = 0;
                        while (reader.Read())
                        {
                            var extentDescription = reader.GetString(0);
                            size += ConvertExtentDescriptionToBytes(extentDescription);
                        }

                        return size;
                    }
                }
            }

            // transaction log suggestions here: http://www.eggheadcafe.com/software/aspnet/32622031/how-big-will-my-backup-be.aspx
            if (backupType == "log")
            {
                const string sql = @"
                        CREATE TABLE #t
                        (
	                        [Database Name] nvarchar(500),
	                        [Log Size (MB)] nvarchar(100),
	                        [Log Space Used (%)] nvarchar(100),
	                        Status nvarchar(20)
                        );

                        INSERT INTO #t
                        EXEC ('DBCC SQLPERF(logspace)')

                        select CAST(CAST([Log Size (MB)] as float) * CAST([Log Space Used (%)] AS float) / 100.0 * 1024.0 * 1024.0 AS bigint) AS LogUsed 
                        from #t
                        WHERE [Database Name] = @dbNameParam;
                        ";

                using (var cmd = new SqlCommand(string.Format(sql), cnn))
                {
                    cmd.CommandTimeout = 0;
                    var param = cmd.CreateParameter();
                    param.SqlDbType = SqlDbType.NVarChar;
                    param.Size = 128;
                    param.Direction = ParameterDirection.Input;
                    param.ParameterName = "dbNameParam";
                    param.Value = databaseName;
                    cmd.Parameters.Add(param);
                    using (var reader = cmd.ExecuteReader())
                    {
                        reader.Read();
                        return reader.GetInt64(0);
                    }
                }
            }

            throw new NotImplementedException();
        }

        private static long ConvertExtentDescriptionToBytes(string extentDescription)
        {
            // run "DBCC PAGE (<database Name>, 1, 6, 3) WITH TABLERESULTS, NO_INFOMSGS" on your favourite database to see example of 
            // extent descriptions.
            // Extent descriptions come in two flavours:
            // "(1:0)        - (1:24)      "
            // or 
            // "(1:48)       -             "

            var twoParts = extentDescription.Split('-');

            if (twoParts.Length > 2 || twoParts.Length == 0)
            {
                throw new InvalidOperationException(string.Format("Unexpected extent description when estimating backup size: {0}", extentDescription));
            }

            if (twoParts.Length == 2 && !string.IsNullOrEmpty(twoParts[1].Trim()))
            {
                // We're working with the first example: "(1:0)        - (1:24)      "
                try
                {
                    var startPage = ConvertExtentPartToPageNumber(twoParts[0]);
                    var endPage = ConvertExtentPartToPageNumber(twoParts[1]);

                    return (endPage - startPage) * (8L * 1024L);
                }
                catch (Exception e)
                {
                    throw new InvalidOperationException(string.Format("Unexpected extent description when estimating backup size: {0}", extentDescription) + "\r\n" + e.Message);
                }

            }
            // We're working with the second example: "(1:48)       -             "
            // it's always 8 pages (of 8K). 8 pages == 1 extent:
            return 8L * (8L * 1024L);
        }

        private static long ConvertExtentPartToPageNumber(string extentPartDescrption)
        {
            // extentPartDescrption is in the format "(1:48)      ".  In this example, this method returns 48.
            extentPartDescrption = extentPartDescrption.Trim();
            if (extentPartDescrption[0] != '('
                || extentPartDescrption[extentPartDescrption.Length - 1] != ')')
            {
                throw new InvalidOperationException(string.Format("Unexpected extent part description: {0}", extentPartDescrption));
            }
            extentPartDescrption = extentPartDescrption.Substring(1, extentPartDescrption.Length - 2);
            try
            {
                return long.Parse(extentPartDescrption.Substring(extentPartDescrption.IndexOf(':') + 1));
            }
            catch (FormatException)
            {
                throw new InvalidOperationException(string.Format("Unexpected extent part description: {0}", extentPartDescrption));
            }
        }

        private static string GetVersion(SqlConnection cnn)
        {
            using (var cmd = new SqlCommand("SELECT @@version;", cnn))
            {
                cmd.CommandTimeout = 0;
                return cmd.ExecuteScalar().ToString();
            }
        }

        private static int GetMajorVersionNumber(string version)
        {
            //TODO: simplify checker
            //could get edition and version number from SELECT SERVERPROPERTY('Edition') and SELECT SERVERPROPERTY('ProductVersion')
            // example string:
            // Microsoft SQL Server 2008 (SP1) - 10.0.2531.0 (Intel X86)   Mar 29 2009 10:27:29   Copyright (c) 1988-2008 Microsoft Corporation  Developer Edition on Windows NT 5.1 <X86> (Build 2600: Service Pack 3) 

            var dashPos = version.IndexOf('-');
            if (dashPos < 0)
            {
                throw new ArgumentException(string.Format("unexpected version string: {0}", version));
            }

            var dotPos = version.IndexOf('.', dashPos);

            if (dotPos < 0)
            {
                throw new ArgumentException(string.Format("unexpected version string: {0}", version));
            }

            int versionNum;
            if (!int.TryParse(version.Substring(dashPos + 1, dotPos - dashPos - 1), out versionNum))
            {
                throw new ArgumentException(string.Format("unexpected version string: {0}", version));
            }

            return versionNum;
        }

        public void BeginExecute()
        {
            _mAsyncResult = _mCmd.BeginExecuteNonQuery();
            //get return message from query giving us the standard backup/restore finish message
            _mCnn.InfoMessage += (sender, e) => Console.WriteLine(e.Message);
        }

        public Exception EndExecute()
        {
            try
            {
                _mCmd.EndExecuteNonQuery(_mAsyncResult);
                return null;
            }
            catch (Exception e)
            {
                return e;
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        ~SqlThread()
        {
            Dispose(false);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_mDisposed)
            {
                if (disposing)
                {
                    if (_mCmd != null)
                    {
                        _mCmd.Dispose();
                    }
                    _mCmd = null;

                    if (_mCnn != null)
                    {
                        _mCnn.Dispose();
                    }
                    _mCnn = null;
                }

                // There are no unmanaged resources to release, but
                // if we add them, they need to be released here.
            }
            _mDisposed = true;
            // If it is available, make the call to the
            // base class's Dispose(Boolean) method
            //base.Dispose(disposing);
        }
    }
}