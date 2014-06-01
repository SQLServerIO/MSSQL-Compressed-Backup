/*************************************************************************************\
File Name  :  Program.cs
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
using System.Linq;
using System.Text;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization.Formatters.Binary;
using System.Data;
using System.Data.SqlClient;

//Additional includes other than default dot net framework should go here.
using MSBackupPipe.StdPlugins;
using MSBackupPipe.Common;

//main program entry point
namespace MSBackupPipe.Cmd
{
    class Program
    {
        static int Main(string[] args)
        {
            //TODO: add pause on complete switch to command line
            //TODO: restore on top of an existing database? no error message
            //TODO: package version number does not match assembly's version number.
            //TODO: cntl-c kills the app, but we can handle the event: http://www.codeneverwritten.com/2006/10/ctrl-c-and-net-console-application.html
            //TODO: Do escaped cmd line args work?
            //TODO: error on overwrite? 
            //TODO: Verify SQL Server version compatibility preferred 2000 throught 2014+
            //TODO: better device timeout, longer execution timeout (db might be locked)
            //TODO: check file extensions
            try
            {
                //everything here is modular and is passed from pipeline to pipeline these are the
                //three main pipelines, pipeline componnets are for compression, encryption and filters
                //anything that would be considered a filter
                //database components are the VDI commands and data stream from the backup or restore operation
                //storage components are the last stage and consists of just disk targets at the moment.
                var pipelineComponents = BackupPipeSystem.LoadTransformComponents();
                var databaseComponents = BackupPipeSystem.LoadDatabaseComponents();
                var storageComponents = BackupPipeSystem.LoadStorageComponents();

                if (args.Length == 0)
                {
                    Console.WriteLine("For help, type \'msbp.exe help\'");
                    return 0;
                }
                switch (args[0].ToLowerInvariant())
                {
                    case "help":
                        if (args.Length == 1)
                        {
                            PrintUsage();
                        }
                        else
                        {
                            switch (args[1].ToLowerInvariant())
                            {
                                case "backup":
                                    PrintBackupUsage();
                                    break;
                                case "restore":
                                    PrintRestoreUsage();
                                    break;
                                case "restoreverifyonly":
                                    PrintVerifyOnlyUsage();
                                    break;
                                case "restoreheaderonly":
                                    PrintRestoreHeaderOnlyUsage();
                                    break;
                                case "restorefilelistonly":
                                    PrintRestoreFilelistOnlyUsage();
                                    break;
                                case "listplugins":
                                    Console.WriteLine("Lists the plugins available.  Go on, try it.");
                                    break;
                                case "helpplugin":
                                    Console.WriteLine("Displays a plugin's help text. For example:");
                                    Console.WriteLine("\tmsbp.exe helpplugin gzip");
                                    break;
                                case "version":
                                    Console.WriteLine("Displays the version number.");
                                    break;
                                default:
                                    Console.WriteLine("Command doesn't exist: {0}", args[1]);
                                    PrintUsage();
                                    return -1;
                            }
                        }
                        return 0;
                        //start of backup command
                    case "backup":
                    {
                        try
                        {
                            ConfigPair storageConfig;
                            ConfigPair databaseConfig;
                            const int commandType = 1;

                            var pipelineConfig = ParseBackupOrRestoreArgs(CopySubArgs(args), commandType, pipelineComponents, databaseComponents, storageComponents, out databaseConfig, out storageConfig);
                            //async notifier for percent complete report
                            var notifier = new CommandLineNotifier(true);

                            var startTime = DateTime.UtcNow;
                            //configure and start backup command
                            List<string> devicenames;
                            BackupPipeSystem.Backup(databaseConfig, pipelineConfig, storageConfig, notifier, out devicenames);
                            Console.WriteLine("Completed Successfully. {0}", string.Format("{0:dd\\:hh\\:mm\\:ss\\.ff}", DateTime.UtcNow - startTime));

                            //this is so we can do a restore filelistonly and restore headeronly
                            WriteHeaderFilelist(databaseConfig, storageConfig, devicenames);

                            return 0;
                        }
                        catch (ParallelExecutionException ee)
                        {
                            HandleExecutionExceptions(ee, true);
                            return -1;
                        }
                        catch (Exception e)
                        {
                            HandleException(e, true);
                            return -1;
                        }
                    }
                        //start of restore command
                    case "restore":
                    {
                        try
                        {
                            ConfigPair storageConfig;
                            ConfigPair databaseConfig;

                            const int commandType = 2;

                            var pipelineConfig = ParseBackupOrRestoreArgs(CopySubArgs(args), commandType, pipelineComponents, databaseComponents, storageComponents, out databaseConfig, out storageConfig);
                            //async notifier for percent complete report
                            var notifier = new CommandLineNotifier(false);

                            var startTime = DateTime.UtcNow;
                            //configure and start restore command
                            BackupPipeSystem.Restore(storageConfig, pipelineConfig, databaseConfig, notifier);
                            Console.WriteLine("Completed Successfully. {0}", string.Format("{0:dd\\:hh\\:mm\\:ss\\.ff}", DateTime.UtcNow - startTime));
                            return 0;
                        }
                        catch (ParallelExecutionException ee)
                        {
                            HandleExecutionExceptions(ee, false);
                            return -1;
                        }
                        catch (Exception e)
                        {
                            HandleException(e, false);
                            return -1;
                        }
                    }
                    case "restorefilelistonly":
                    {
                        var startTime = DateTime.UtcNow;
                        var bFormat = new BinaryFormatter();
                        //var hargs = CopySubArgs(args);
                        var storageArg = args[1];
                        ConfigPair storageConfig;
                        try
                        {

                            if (storageArg.StartsWith("file://"))
                            {
                                var uri = new Uri(storageArg);
                                storageArg = string.Format("local(path={0})", uri.LocalPath.Replace(";", ";;"));
                            }
                        }
                        catch (Exception e)
                        {
                            HandleException(e, false);
                            return -1;
                        }
                        try
                        {
                            storageConfig = ConfigUtil.ParseComponentConfig(storageComponents, storageArg);
                        }
                        catch (Exception e)
                        {
                            HandleException(e, false);
                            return -1;
                        }
                        var metaDataPath = storageConfig.Parameters["path"][0];
                        if (!File.Exists(metaDataPath))
                        {
                            metaDataPath = storageConfig.Parameters["path"][0] + ".hfl";
                        }

                        try
                        {
                            using (var fs = new FileStream(metaDataPath, FileMode.Open))
                            {
                                using (var headerOnlyData = (DataSet) bFormat.Deserialize(fs))
                                {
                                    using (var table = headerOnlyData.Tables[1])
                                    {
                                        var sb = new StringBuilder();
                                        foreach (DataColumn column in table.Columns)
                                        {
                                            sb.Append(column);
                                            sb.Append(",");
                                        }
                                        sb.Length -= 1;
                                        sb.AppendLine();
                                        foreach (DataRow row in table.Rows) // Loop over the rows.
                                        {
                                            foreach (var value in row.ItemArray)
                                            {
                                                sb.Append(value);
                                                sb.Append(",");
                                            }
                                            sb.Length -= 1;
                                            sb.AppendLine();
                                        }
                                        Console.WriteLine(sb.ToString());
                                    }
                                }
                                Console.WriteLine("Completed Successfully. {0}", string.Format("{0:dd\\:hh\\:mm\\:ss\\.ff}", DateTime.UtcNow - startTime));
                            }
                            return 0;
                        }
                        catch (Exception e)
                        {
                            HandleException(e, false);
                            return -1;
                        }
                    }
                    case "restoreheaderonly":
                    {
                        var startTime = DateTime.UtcNow;
                        var bFormat = new BinaryFormatter();
                        //var hargs = CopySubArgs(args);
                        var storageArg = args[1];
                        ConfigPair storageConfig;
                        try
                        {
                            if (storageArg.StartsWith("file://"))
                            {
                                var uri = new Uri(storageArg);
                                storageArg = string.Format("local(path={0})", uri.LocalPath.Replace(";", ";;"));
                            }
                        }
                        catch (Exception e)
                        {
                            HandleException(e, false);
                            return -1;
                        }
                        try
                        {
                            storageConfig = ConfigUtil.ParseComponentConfig(storageComponents, storageArg);
                        }
                        catch (Exception e)
                        {
                            HandleException(e, false);
                            return -1;
                        }

                        var metaDataPath = storageConfig.Parameters["path"][0];
                        if (!File.Exists(metaDataPath))
                        {
                            metaDataPath = storageConfig.Parameters["path"][0] + ".hfl";
                        }

                        try
                        {
                            using (var fs = new FileStream(metaDataPath, FileMode.Open))
                            {
                                using (var headerOnlyData = (DataSet) bFormat.Deserialize(fs))
                                {
                                    using (var table = headerOnlyData.Tables[0])
                                    {
                                        var sb = new StringBuilder();
                                        foreach (DataColumn column in table.Columns)
                                        {
                                            sb.Append(column);
                                            sb.Append(",");
                                        }
                                        sb.Length -= 1;
                                        sb.AppendLine();
                                        foreach (DataRow row in table.Rows) // Loop over the rows.
                                        {
                                            foreach (var value in row.ItemArray)
                                            {
                                                sb.Append(value);
                                                sb.Append(",");
                                            }
                                            sb.Length -= 1;
                                            sb.AppendLine();
                                        }
                                        Console.WriteLine(sb.ToString());
                                    }
                                    Console.WriteLine("Completed Successfully. {0}", string.Format("{0:dd\\:hh\\:mm\\:ss\\.ff}", DateTime.UtcNow - startTime));
                                }
                            }
                            return 0;
                        }
                        catch (Exception e)
                        {
                            HandleException(e, false);
                            return -1;
                        }
                    }

                    case "restoreverifyonly":
                    {
                        try
                        {
                            ConfigPair storageConfig;
                            ConfigPair databaseConfig;

                            const int commandType = 3;

                            var pipelineConfig = ParseBackupOrRestoreArgs(CopySubArgs(args), commandType, pipelineComponents, databaseComponents, storageComponents, out databaseConfig, out storageConfig);
                            //async notifier for percent complete report
                            var notifier = new CommandLineNotifier(false);

                            var startTime = DateTime.UtcNow;
                            //configure and start restore command
                            BackupPipeSystem.Verify(storageConfig, pipelineConfig, databaseConfig, notifier);
                            Console.WriteLine("Completed Successfully. {0}", string.Format("{0:dd\\:hh\\:mm\\:ss\\.ff}", DateTime.UtcNow - startTime));
                            return 0;
                        }
                        catch (ParallelExecutionException ee)
                        {
                            HandleExecutionExceptions(ee, false);
                            return -1;
                        }
                        catch (Exception e)
                        {
                            HandleException(e, false);
                            return -1;
                        }
                    }

                    case "listplugins":
                        PrintPlugins(pipelineComponents, databaseComponents, storageComponents);
                        return 0;
                    case "helpplugin":
                        if (args.Length < 2)
                        {
                            Console.WriteLine("Please give a plugin name, like msbp.exe helpplugin <plugin>");
                            return -1;
                        }
                        return PrintPluginHelp(args[1], pipelineComponents, databaseComponents, storageComponents);

                    case "version":
                        var version = Assembly.GetEntryAssembly().GetName().Version;
                        //ProcessorArchitecture arch = typeof(VirtualDeviceSet).Assembly.GetName().ProcessorArchitecture;
                        Console.WriteLine("v{0} 32+64 ({1:yyyy MMM dd})", version, (new DateTime(2000, 1, 1)).AddDays(version.Build));
                        return 0;
                    default:
                        Console.WriteLine("Unknown command: {0}", args[0]);

                        PrintUsage();
                        return -1;
                }
            }

            catch (Exception e)
            {
                Util.WriteError(e);
                //TODO: cut down on verbosity of error messages in release mode

                var ie = e;
                while (ie.InnerException != null)
                {
                    ie = ie.InnerException;
                }
                if (!ie.Equals(e))
                {
                    Console.WriteLine(ie.Message);
                }
                PrintUsage();
                return -1;
            }
        }

        private static void WriteHeaderFilelist(ConfigPair databaseConfig, ConfigPair storageConfig, IEnumerable<string> devices)
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
            var dataSource = string.IsNullOrEmpty(instanceName) ? serverConnectionName : string.Format(@"{0}\{1}", serverConnectionName, instanceName);
            string connectionString;
            if (databaseConfig.Parameters.ContainsKey("user"))
            {
                connectionString = databaseConfig.Parameters.ContainsKey("password") ? string.Format("Data Source={0};Initial Catalog=master;Integrated Security=False;User ID={1};Password={2};Asynchronous Processing=true;", dataSource, databaseConfig.Parameters["user"][0], databaseConfig.Parameters["password"][0]) : string.Format("Data Source={0};Initial Catalog=master;Integrated Security=False;User ID={1};Password={2};Asynchronous Processing=true;", dataSource, databaseConfig.Parameters["user"][0], null);
            }
            else
            {
                connectionString = string.Format("Data Source={0};Initial Catalog=master;Integrated Security=True;", dataSource);
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

        private static void HandleExecutionExceptions(ParallelExecutionException ee, bool isBackup)
        {
            var i = 1;

            foreach (var e in ee.Exceptions)
            {
                Console.WriteLine("------------------------");
                Console.WriteLine("Exception #{0}", i);
                Util.WriteError(e);
                Console.WriteLine();
                i++;
            }

            Console.WriteLine();
            Console.WriteLine("The {0} failed.", isBackup ? "backup" : "restore");
#if DEBUG
            Console.WriteLine();
            Console.WriteLine("Hit Any Key To Continue:");
            Console.ReadKey();
#endif
        }

        private static void HandleException(Exception e, bool isBackup)
        {
            Util.WriteError(e);

            var ie = e;
            while (ie.InnerException != null)
            {
                ie = ie.InnerException;
            }
            if (!ie.Equals(e))
            {
                Console.WriteLine(ie.Message);
            }

            Console.WriteLine();
            Console.WriteLine("The {0} failed.", isBackup ? "backup" : "restore");

            PrintUsage();
#if DEBUG
            Console.WriteLine();
            Console.WriteLine("Hit Any Key To Continue:");
            Console.ReadKey();
#endif

        }

        private static List<string> CopySubArgs(string[] args)
        {
            var result = new List<string>(args.Length - 2);
            for (var i = 1; i < args.Length; i++)
            {
                result.Add(args[i]);
            }
            return result;
        }

        private static List<ConfigPair> ParseBackupOrRestoreArgs(List<string> args, int commandType, Dictionary<string, Type> pipelineComponents, Dictionary<string, Type> databaseComponents, Dictionary<string, Type> storageComponents, out ConfigPair databaseConfig, out ConfigPair storageConfig)
        {
            if (args.Count < 2)
            {
                throw new ArgumentException("Please provide both the database and storage plugins after the backup subcommand.");
            }

            string databaseArg;
            string storageArg;

            if (commandType == 1)
            {
                databaseArg = args[0];
                storageArg = args[args.Count - 1];
            }
            else
            {
                databaseArg = args[args.Count - 1];
                storageArg = args[0];
            }

            if (databaseArg.Contains("://"))
            {
                throw new ArgumentException("The first sub argument must be the name of the database.");
            }

            if (databaseArg[0] == '[' && databaseArg[databaseArg.Length - 1] == ']')
            {
                databaseArg = string.Format("db(database={0})", databaseArg.Substring(1, databaseArg.Length - 2).Replace(";", ";;"));
            }

            databaseConfig = ConfigUtil.ParseComponentConfig(databaseComponents, databaseArg);

            if (storageArg[0] == '[' && storageArg[databaseArg.Length - 1] == ']')
            {
                throw new ArgumentException("The last sub argument must be a storage plugin.");
            }

            if (storageArg.StartsWith("file://"))
            {
                var uri = new Uri(storageArg);
                storageArg = string.Format("local(path={0})", uri.LocalPath.Replace(";", ";;"));
            }

            storageConfig = ConfigUtil.ParseComponentConfig(storageComponents, storageArg);

            var pipelineArgs = new List<string>();
            for (var i = 1; i < args.Count - 1; i++)
            {
                pipelineArgs.Add(args[i]);
            }

            var pipeline = BuildPipelineFromString(pipelineArgs, pipelineComponents);

            return pipeline;
        }

        private static List<ConfigPair> BuildPipelineFromString(List<string> pipelineArgs, Dictionary<string, Type> pipelineComponents)
        {
            for (var i = 0; i < pipelineArgs.Count; i++)
            {
                pipelineArgs[i] = pipelineArgs[i].Trim();
            }

            var results = new List<ConfigPair>(pipelineArgs.Count);
            results.AddRange(pipelineArgs.Select(componentString => ConfigUtil.ParseComponentConfig(pipelineComponents, componentString)));

            return results;
        }

        private static void PrintUsage()
        {
            Console.WriteLine("Below are the commands for msbp.exe:");
            Console.WriteLine("\tmsbp.exe help");
            Console.WriteLine("\tmsbp.exe backup");
            Console.WriteLine("\tmsbp.exe restore");
            Console.WriteLine("\tmsbp.exe restoreverifyonly");
            Console.WriteLine("\tmsbp.exe restoreheaderonly");
            Console.WriteLine("\tmsbp.exe restorefilelistonly");
            Console.WriteLine("\tmsbp.exe listplugins");
            Console.WriteLine("\tmsbp.exe helpplugin");
            Console.WriteLine("\tmsbp.exe version");
            Console.WriteLine("");
            Console.WriteLine("For more information, type msbp.exe help <command>");
        }

        private static void PrintBackupUsage()
        {
            Console.WriteLine("To backup a database, the first parameter must be the database in brackets, and the last parameter must be the file.  The middle parameters can modify the data, for example compressing it.");
            Console.WriteLine("To backup to a standard *.bak file:");
            Console.WriteLine("\tmsbp.exe backup [model] file:///c:\\model.bak");
            Console.WriteLine("To compress the backup file using gzip:");
            Console.WriteLine("\tmsbp.exe backup [model] gzip file:///c:\\model.bak.gz");
            Console.WriteLine("");
            Console.WriteLine("For more information on the different pipline options, type msbp.exe listplugins");
        }

        private static void PrintRestoreUsage()
        {
            Console.WriteLine("To restore a database, the first parameter must be the file, and the last parameter must be the database in brackets.  The middle parameters can modify the data, for example uncompressing it.");
            Console.WriteLine("To restore to a standard *.bak file:");
            Console.WriteLine("\tmsbp.exe restore file:///c:\\model.bak [model]");
            Console.WriteLine("To compress the backup file using gzip:");
            Console.WriteLine("\tmsbp.exe restore file:///c:\\model.bak.gz gzip [model]");
            Console.WriteLine("");
            Console.WriteLine("For more information on the different pipline options, type msbp.exe listplugins");
        }

        private static void PrintVerifyOnlyUsage()
        {
            Console.WriteLine("To verify a backup set, the first parameter must be the file, and the last parameter must be the database in brackets.  The middle parameters can modify the data, for example uncompressing it.");
            Console.WriteLine("To verify a standard *.bak file:");
            Console.WriteLine("\tmsbp.exe restoreverifyonly file:///c:\\model.bak [model]");
            Console.WriteLine("To decompress  the backup file using gzip:");
            Console.WriteLine("\tmsbp.exe restoreverifyonly file:///c:\\model.bak.gz gzip [model]");
            Console.WriteLine("");
            Console.WriteLine("For more information on the different pipline options, type msbp.exe listplugins");
        }

        private static void PrintRestoreHeaderOnlyUsage()
        {
            Console.WriteLine("To restore header only, the first parameter must be the metadata (hfl) file");
            Console.WriteLine("\tmsbp.exe restoreheaderonly file:///c:\\model.bak.gz.hfl");
        }

        private static void PrintRestoreFilelistOnlyUsage()
        {
            Console.WriteLine("To restore filelist only, the first parameter must be the metadata (hfl) file");
            Console.WriteLine("\tmsbp.exe restorefilelist file:///c:\\model.bak.gz.hfl");
        }

        private static void PrintPlugins(Dictionary<string, Type> pipelineComponents, Dictionary<string, Type> databaseComponents, Dictionary<string, Type> storageComponents)
        {
            Console.WriteLine("Database plugins:");
            PrintComponents(databaseComponents);
            Console.WriteLine("Pipeline plugins:");
            PrintComponents(pipelineComponents);
            Console.WriteLine("Storage plugins:");
            PrintComponents(storageComponents);

            Console.WriteLine("");
            Console.WriteLine("To find more information about a plugin, type msbp.exe helpplugin <plugin>");
        }

        private static void PrintComponents(Dictionary<string, Type> components)
        {
            foreach (var db in (from key in components.Keys select components[key].GetConstructor(new Type[0]) into constructorInfo where constructorInfo != null select constructorInfo.Invoke(new object[0])).OfType<IBackupPlugin>())
            {
                Console.WriteLine("\t" + db.Name);
            }
        }

        private static int PrintPluginHelp(string pluginName, Dictionary<string, Type> pipelineComponents, Dictionary<string, Type> databaseComponents, Dictionary<string, Type> storageComponents)
        {
            PrintPluginHelp(pluginName, databaseComponents);
            PrintPluginHelp(pluginName, pipelineComponents);
            PrintPluginHelp(pluginName, storageComponents);
            return 0;
        }

        private static void PrintPluginHelp(string pluginName, Dictionary<string, Type> components)
        {
            if (!components.ContainsKey(pluginName)) return;
            var constructorInfo = components[pluginName].GetConstructor(new Type[0]);
            if (constructorInfo == null) return;
            var db = constructorInfo.Invoke(new object[0]) as IBackupPlugin;
            if (db != null) Console.WriteLine(db.CommandLineHelp);
        }
        private static string GetVersion(SqlConnection cnn)
        {
            using (var cmd = new SqlCommand("SELECT @@version;", cnn))
            {
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
    }
}
