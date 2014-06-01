/*************************************************************************************\
File Name  :  HeaderFileList.cs
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
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;

namespace MSBackupPipe.Common
{
    public static class HeaderFileList
    {
        public static void WriteHeaderFilelist(ConfigPair databaseConfig, ConfigPair storageConfig, IEnumerable<string> devices)
        {
            /*
            12 --2014 supported
            11 --2012 supported
            10 --2008/R2  supported
            9  --2005 supported
            8  --2000 NOT supported
            7  --7.0 NOT supported
            */
            string instanceName = null;

            if (databaseConfig.Parameters.ContainsKey("instancename"))
            {
                instanceName = databaseConfig.Parameters["instancename"][0];
            }

            if (instanceName != null)
            {
                instanceName = instanceName.Trim();
            }

            string clusterNetworkName = null;

            if (databaseConfig.Parameters.ContainsKey("ClusterNetworkName"))
            {
                clusterNetworkName = databaseConfig.Parameters["ClusterNetworkName"][0];
            }

            var serverConnectionName = clusterNetworkName ?? ".";
            var dataSource = String.IsNullOrEmpty(instanceName) ? serverConnectionName : String.Format(@"{0}\{1}", serverConnectionName, instanceName);
            string connectionString;
            if (databaseConfig.Parameters.ContainsKey("user"))
            {
                connectionString = databaseConfig.Parameters.ContainsKey("password") ? String.Format("Data Source={0};Initial Catalog=master;Integrated Security=False;User ID={1};Password={2};Asynchronous Processing=true;", dataSource, databaseConfig.Parameters["user"][0], databaseConfig.Parameters["password"][0]) : String.Format("Data Source={0};Initial Catalog=master;Integrated Security=False;User ID={1};Password={2};Asynchronous Processing=true;", dataSource, databaseConfig.Parameters["user"][0], null);
            }
            else
            {
                connectionString = String.Format("Data Source={0};Initial Catalog=master;Integrated Security=True;", dataSource);
            }

            int majorVersionNum;
            using (var cnn = new SqlConnection(connectionString))
            {
                var version = GetVersion(cnn);
                majorVersionNum = GetMajorVersionNumber(version);
            }

            var fileListHeaderOnlyQuery = new StringBuilder();


            const string queryCap = @")
			            )
            );";
            const string headeronly = @"SELECT [name]            AS BackupName, 
                   [description]               AS BackupDescription, 
                   CASE 
                     WHEN [type] = 'D' THEN 1 
                     WHEN [type] = 'I' THEN 5 
                     WHEN [type] = 'L' THEN 2 
                     WHEN [type] = 'F' THEN 4 
                     WHEN [type] = 'G' THEN 6 
                     WHEN [type] = 'P' THEN 7 
                     WHEN [type] = 'Q' THEN 8 
                     ELSE NULL 
                   END                         AS BackupType, 
                   expiration_date             AS ExpirationDate, 
                   0                           AS Compressed, 
                   position                    AS Position, 
                   2                           AS DeviceType, 
                   [user_name]                 AS UserName, 
                   [server_name]               AS ServerName, 
                   [database_name]             AS DatabaseName, 
                   database_version            AS DatabaseVersion, 
                   database_creation_date      AS DatabaseCreationDate, 
                   backup_size                 AS BackupSize, 
                   first_lsn                   AS FirstLSN, 
                   last_lsn                    AS LastLSN, 
                   checkpoint_lsn              AS CheckpointLSN, 
                   database_backup_lsn         AS DatabaseBackupLSN, 
                   backup_start_date           AS BackupStartDate, 
                   backup_finish_date          AS BackupFinishDate, 
                   sort_order                  AS SortOrder, 
                   code_page                   AS CodePage, 
                   unicode_locale              AS UnicodeLocaleid, 
                   unicode_compare_style       AS UnicodeComparisionStyle, 
                   compatibility_level         AS CompatibilityLevel, 
                   software_vendor_id          AS SoftwareVendorid, 
                   software_major_version      AS SoftwareVersionMajor, 
                   software_minor_version      AS SoftwareVersionMinor, 
                   software_build_version      AS SoftwareVersionBuild, 
                   machine_name                AS MachineName, 
                   flags                       AS Flags, 
                   drs.database_guid           AS BindingID, 
                   drs.recovery_fork_guid      AS RecoveryForkID, 
                   collation_name              AS Collation, 
                   bs.family_guid              AS FamilyGUID, 
                   has_bulk_logged_data        AS HasBulkLoggedData, 
                   is_snapshot                 AS IsSnapshot, 
                   is_readonly                 AS IsReadonly, 
                   is_single_user              AS IsSingleUser, 
                   has_backup_checksums        AS HasBackupChecksums, 
                   is_damaged                  AS IsDamaged, 
                   begins_log_chain            AS BeginsLogChain, 
                   has_incomplete_metadata     AS HasIncompleteMetaData, 
                   is_force_offline            AS IsForceOffline, 
                   is_copy_only                AS IsCopyOnly, 
                   bs.first_recovery_fork_guid AS FirstRecoveryForkID, 
                   bs.fork_point_lsn           AS ForPointLSN, 
                   recovery_model              AS RecoveryModel, 
                   differential_base_lsn       AS DifferentialBaseLSN, 
                   differential_base_guid      AS DifferentialBaseGUID, 
                   CASE 
                     WHEN [type] = 'D' THEN 'DATABASE' 
                     WHEN [type] = 'I' THEN 'DATABASE DIFFERENTIAL' 
                     WHEN [type] = 'L' THEN 'TRANSACTION LOG' 
                     WHEN [type] = 'F' THEN 'FILE OR FILEGROUP' 
                     WHEN [type] = 'G' THEN 'FILE DIFFERENTIAL PARTIAL' 
                     WHEN [type] = 'P' THEN 'PARTIAL DIFFERENTIAL' 
                     WHEN [type] = 'Q' THEN 'PARTIAL DIFFERENTIAL' 
                     ELSE NULL 
                   END                         AS BackupSetDescription,
                   backup_set_uuid             AS BackupSetGUID";

            const string headeronly2008 = @", 
                   compressed_backup_size      AS CompressedBackupSize";

            const string headeronly2012 = @", 
                   compressed_backup_size      AS CompressedBackupSize, 
                   0                           AS Containment ";

            const string headeronlytail = @"
            FROM   msdb.dbo.backupset bs 
            INNER JOIN msdb.sys.database_recovery_status drs 
                    ON bs.family_guid = drs.family_guid 
                    AND 
                    bs.database_guid = drs.database_guid
            where bs.backup_set_id
            in(
	            select 
			            backup_set_id 
		            from 
			            msdb.dbo.backupset 
		            where 
			            media_set_id in 
			            (
			            select 
				            distinct media_set_id 
			            from 
				            msdb.dbo.backupmediafamily 
			            where 
				            physical_device_name in(";


            const string fileListOnly = @"SELECT 
                            bf.logical_name           AS LogicalName,
                            bf.physical_name          AS PhysicalName, 
                            bf.file_type              AS [Type], 
                            bf.[filegroup_name]       AS FileGroupName, 
                            bf.[file_size]            AS Size, 
                            35184372080640            AS MaxSize, 
                            bf.file_number            AS FileID, 
                            bf.create_lsn             AS CreateLSN, 
                            bf.drop_lsn               AS DropLSN, 
                            bf.file_guid              AS UniqueID, 
                            bf.read_only_lsn          AS ReadOnlyLSN, 
                            bf.read_write_lsn         AS ReadWriteLSN, 
                            bf.backup_size            AS BackupSizeInBytes, 
                            bf.source_file_block_size AS SourceBlockSize, 
                            bfg.[filegroup_id]        AS FileGroupID, 
                            NULL                      AS logLogGroupGUID, 
                            bf.differential_base_lsn  AS DifferentialBaseLSN, 
                            bf.differential_base_guid AS DifferentialBaseGUID, 
                            bf.is_readonly            AS IsReadOnly, 
                            bf.is_present             AS IsPresent";

            const string filelistonly2008 = @", 
                            NULL                   AS TDEThumbprint ";

            const string filelistonlytail = @"
                        FROM   
                            msdb.dbo.backupfile bf
                        left outer join
                            msdb.dbo.backupfilegroup bfg
                        on
                            bf.backup_set_id = bfg.backup_set_id 
                        and 
                            bf.filegroup_guid = bf.filegroup_guid
                        where 
	                        bf.backup_set_id in 
	                        (
	                        select 
		                        backup_set_id 
	                        from 
		                        msdb.dbo.backupset 
	                        where 
		                        media_set_id in 
		                        (
		                        select 
			                        distinct media_set_id 
		                        from 
			                        msdb.dbo.backupmediafamily 
		                        where 
				                        physical_device_name in(";

            var metaDataPath = storageConfig.Parameters["path"][0] + ".hfl";

            var deviceList = new StringBuilder();

            foreach (var d in devices)
            {
                deviceList.Append("'");
                deviceList.Append(d);
                deviceList.Append("',");
            }
            deviceList.Remove(deviceList.Length - 1, 1);

            fileListHeaderOnlyQuery.Append(headeronly);
            switch (majorVersionNum)
            {
                case 12:
                    fileListHeaderOnlyQuery.Append(headeronly2012);
                    break;
                case 11:
                    fileListHeaderOnlyQuery.Append(headeronly2012);
                    break;
                case 10:
                    fileListHeaderOnlyQuery.Append(headeronly2008);
                    break;
            }
            fileListHeaderOnlyQuery.Append(headeronlytail);
            fileListHeaderOnlyQuery.Append(deviceList);
            fileListHeaderOnlyQuery.Append(queryCap);
            fileListHeaderOnlyQuery.Append(fileListOnly);
            switch (majorVersionNum)
            {
                case 12:
                    fileListHeaderOnlyQuery.Append(filelistonly2008);
                    break;
                case 11:
                    fileListHeaderOnlyQuery.Append(filelistonly2008);
                    break;
                case 10:
                    fileListHeaderOnlyQuery.Append(filelistonly2008);
                    break;
            }
            fileListHeaderOnlyQuery.Append(filelistonlytail);
            fileListHeaderOnlyQuery.Append(deviceList);
            fileListHeaderOnlyQuery.Append(queryCap);

            var headerFileList = new DataSet();

            /*
             * we only support sql server 2005 and above for meta-data right now
             */
            //TODO: add support for 2000 and 7.0
            if (majorVersionNum > 8)
            {
                using (var mCnn = new SqlConnection(connectionString))
                {
                    var adapter = new SqlDataAdapter
                    {
                        SelectCommand = new SqlCommand(fileListHeaderOnlyQuery.ToString(), mCnn)
                    };
                    mCnn.Open();
                    adapter.Fill(headerFileList);
                    adapter.Dispose();
                }

                using (var fs = new FileStream(metaDataPath, FileMode.Create))
                {
                    headerFileList.RemotingFormat = SerializationFormat.Binary;
                    var bFormat = new BinaryFormatter();
                    bFormat.Serialize(fs, headerFileList);
                }
            }
            headerFileList.Dispose();
        }

        private static string GetVersion(SqlConnection cnn)
        {
            using (var cmd = new SqlCommand("SELECT @@version;", cnn))
            {
                cnn.Open();
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
                throw new ArgumentException(String.Format("unexpected version string: {0}", version));
            }

            var dotPos = version.IndexOf('.', dashPos);

            if (dotPos < 0)
            {
                throw new ArgumentException(String.Format("unexpected version string: {0}", version));
            }

            int versionNum;
            if (!Int32.TryParse(version.Substring(dashPos + 1, dotPos - dashPos - 1), out versionNum))
            {
                throw new ArgumentException(String.Format("unexpected version string: {0}", version));
            }

            return versionNum;
        }
    }
}
