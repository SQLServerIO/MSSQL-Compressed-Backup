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

//Additional includes other than default dot net framework should go here.
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
                            PrintHelp.PrintUsage();
                        }
                        else
                        {
                            switch (args[1].ToLowerInvariant())
                            {
                                case "backup":
                                    PrintHelp.PrintBackupUsage();
                                    break;
                                case "restore":
                                    PrintHelp.PrintRestoreUsage();
                                    break;
                                case "restoreverifyonly":
                                    PrintHelp.PrintVerifyOnlyUsage();
                                    break;
                                case "restoreheaderonly":
                                    PrintHelp.PrintRestoreHeaderOnlyUsage();
                                    break;
                                case "restorefilelistonly":
                                    PrintHelp.PrintRestoreFilelistOnlyUsage();
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
                                    PrintHelp.PrintUsage();
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
                            HeaderFileList.WriteHeaderFilelist(databaseConfig, storageConfig, devicenames);

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
                        PrintHelp.PrintPlugins(pipelineComponents, databaseComponents, storageComponents);
                        return 0;
                    case "helpplugin":
                        if (args.Length < 2)
                        {
                            Console.WriteLine("Please give a plugin name, like msbp.exe helpplugin <plugin>");
                            return -1;
                        }
                        return PrintHelp.PrintPluginHelp(args[1], pipelineComponents, databaseComponents, storageComponents);

                    case "version":
                        var version = Assembly.GetEntryAssembly().GetName().Version;
                        //ProcessorArchitecture arch = typeof(VirtualDeviceSet).Assembly.GetName().ProcessorArchitecture;
                        Console.WriteLine("v{0} 32+64 ({1:yyyy MMM dd})", version, (new DateTime(2000, 1, 1)).AddDays(version.Build));
                        return 0;
                    default:
                        Console.WriteLine("Unknown command: {0}", args[0]);

                        PrintHelp.PrintUsage();
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
                PrintHelp.PrintUsage();
                return -1;
            }
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

            PrintHelp.PrintUsage();
#if DEBUG
            Console.WriteLine();
            Console.WriteLine("Hit Any Key To Continue:");
            Console.ReadKey();
#endif

        }

        private static List<string> CopySubArgs(IList<string> args)
        {
            var result = new List<string>(args.Count - 2);
            for (var i = 1; i < args.Count; i++)
            {
                result.Add(args[i]);
            }
            return result;
        }

        private static List<ConfigPair> ParseBackupOrRestoreArgs(IList<string> args, int commandType, Dictionary<string, Type> pipelineComponents, Dictionary<string, Type> databaseComponents, Dictionary<string, Type> storageComponents, out ConfigPair databaseConfig, out ConfigPair storageConfig)
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

        private static List<ConfigPair> BuildPipelineFromString(IList<string> pipelineArgs, Dictionary<string, Type> pipelineComponents)
        {
            for (var i = 0; i < pipelineArgs.Count; i++)
            {
                pipelineArgs[i] = pipelineArgs[i].Trim();
            }

            var results = new List<ConfigPair>(pipelineArgs.Count);
            results.AddRange(pipelineArgs.Select(componentString => ConfigUtil.ParseComponentConfig(pipelineComponents, componentString)));

            return results;
        }
    }
}
