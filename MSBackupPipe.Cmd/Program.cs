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
using System.IO;
using System.Reflection;
using System.Diagnostics;

using MSBackupPipe.StdPlugins;
using MSBackupPipe.Common;

namespace MSBackupPipe.Cmd
{
    class Program
    {
        static int Main(string[] args)
        {

#if DEBUG
            Debugger.Launch();
#endif

            try
            {


                Dictionary<string, Type> pipelineComponents = BackupPipeSystem.LoadTransformComponents();
                Dictionary<string, Type> databaseComponents = BackupPipeSystem.LoadDatabaseComponents();
                Dictionary<string, Type> storageComponents = BackupPipeSystem.LoadStorageComponents();


                if (args.Length == 0)
                {
                    Console.WriteLine("For help, type 'msbp.exe help'");
                    Console.ReadKey();
                    return 0;
                }
                else
                {
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
                                        Console.WriteLine(string.Format("Command doesn't exist: {0}", args[1]));
                                        PrintUsage();
                                        Console.ReadKey();
                                        return -1;
                                }

                            }
                            Console.ReadKey();
                            return 0;

                        case "backup":
                            {
                                try
                                {

                                    ConfigPair storageConfig;
                                    ConfigPair databaseConfig;
                                    bool isBackup = true;

                                    List<ConfigPair> pipelineConfig = ParseBackupOrRestoreArgs(CopySubArgs(args), isBackup, pipelineComponents, databaseComponents, storageComponents, out databaseConfig, out storageConfig);

                                    CommandLineNotifier notifier = new CommandLineNotifier(true);

                                    DateTime startTime = DateTime.UtcNow;
                                    BackupPipeSystem.Backup(databaseConfig, pipelineConfig, storageConfig, notifier);
                                    Console.WriteLine(string.Format("Completed Successfully. {0}", DateTime.UtcNow - startTime));
                                    Console.ReadKey();
                                    return 0;
                                }
                                catch (ParallelExecutionException ee)
                                {
                                    HandleExecutionExceptions(ee, true);
                                    Console.ReadKey();
                                    return -1;
                                }
                                catch (Exception e)
                                {
                                    HandleException(e, true);
                                    Console.ReadKey();
                                    return -1;
                                }
                            }

                        case "restore":
                            {
                                try
                                {
                                    ConfigPair storageConfig;
                                    ConfigPair databaseConfig;

                                    bool isBackup = false;

                                    List<ConfigPair> pipelineConfig = ParseBackupOrRestoreArgs(CopySubArgs(args), isBackup, pipelineComponents, databaseComponents, storageComponents, out databaseConfig, out storageConfig);

                                    CommandLineNotifier notifier = new CommandLineNotifier(false);

                                    DateTime startTime = DateTime.UtcNow;
                                    BackupPipeSystem.Restore(storageConfig, pipelineConfig, databaseConfig, notifier);
                                    Console.WriteLine(string.Format("Completed Successfully. {0}", DateTime.UtcNow - startTime));
                                    Console.ReadKey();
                                    return 0;
                                }
                                catch (ParallelExecutionException ee)
                                {
                                    HandleExecutionExceptions(ee, false);
                                    Console.ReadKey();
                                    return -1;
                                }
                                catch (Exception e)
                                {
                                    HandleException(e, false);
                                    Console.ReadKey();
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
                                Console.ReadKey();
                                return -1;
                            }
                            else
                            {
                                return PrintPluginHelp(args[1], pipelineComponents, databaseComponents, storageComponents);
                            }

                        case "version":
                            Version version = Assembly.GetEntryAssembly().GetName().Version;
                            //ProcessorArchitecture arch = typeof(VirtualDeviceSet).Assembly.GetName().ProcessorArchitecture;
                            Console.WriteLine(string.Format("v{0} 32+64 ({1:yyyy MMM dd})", version, (new DateTime(2000, 1, 1)).AddDays(version.Build)));
                            Console.ReadKey();
                            return 0;
                        default:
                            Console.WriteLine(string.Format("Unknown command: {0}", args[0]));

                            Console.ReadKey();
                            PrintUsage();
                            return -1;
                    }
                }
            }

            catch (Exception e)
            {

                Util.WriteError(e);

                Exception ie = e;
                while (ie.InnerException != null)
                {
                    ie = ie.InnerException;
                }
                if (!ie.Equals(e))
                {
                    Console.WriteLine(ie.Message);
                }
                Console.ReadKey();
                PrintUsage();

                return -1;
            }


        }


        private static void HandleExecutionExceptions(ParallelExecutionException ee, bool isBackup)
        {

            int i = 1;
            foreach (Exception e in ee.Exceptions)
            {
                Console.WriteLine("------------------------");
                Console.WriteLine(string.Format("Exception #{0}", i));
                Util.WriteError(e);
                Console.WriteLine();
                i++;
            }

            Console.WriteLine();
            Console.WriteLine(string.Format("The {0} failed.", isBackup ? "backup" : "restore"));


            PrintUsage();
        }


        private static void HandleException(Exception e, bool isBackup)
        {
            Util.WriteError(e);

            Exception ie = e;
            while (ie.InnerException != null)
            {
                ie = ie.InnerException;
            }
            if (!ie.Equals(e))
            {
                Console.WriteLine(ie.Message);
            }

            Console.WriteLine();
            Console.WriteLine(string.Format("The {0} failed.", isBackup ? "backup" : "restore"));

            PrintUsage();
        }

        private static List<string> CopySubArgs(string[] args)
        {
            List<string> result = new List<string>(args.Length - 2);
            for (int i = 1; i < args.Length; i++)
            {
                result.Add(args[i]);
            }
            return result;
        }



        private static List<ConfigPair> ParseBackupOrRestoreArgs(List<string> args, bool isBackup, Dictionary<string, Type> pipelineComponents, Dictionary<string, Type> databaseComponents, Dictionary<string, Type> storageComponents, out ConfigPair databaseConfig, out ConfigPair storageConfig)
        {
            if (args.Count < 2)
            {
                throw new ArgumentException("Please provide both the database and storage plugins after the backup subcommand.");
            }


            string databaseArg;
            string storageArg;

            if (isBackup)
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
                Uri uri = new Uri(storageArg);
                storageArg = string.Format("local(path={0})", uri.LocalPath.Replace(";", ";;"));
            }


            storageConfig = ConfigUtil.ParseComponentConfig(storageComponents, storageArg);



            List<string> pipelineArgs = new List<string>();
            for (int i = 1; i < args.Count - 1; i++)
            {
                pipelineArgs.Add(args[i]);
            }


            List<ConfigPair> pipeline = BuildPipelineFromString(pipelineArgs, pipelineComponents);


            return pipeline;
        }





        private static List<ConfigPair> BuildPipelineFromString(List<string> pipelineArgs, Dictionary<string, Type> pipelineComponents)
        {

            for (int i = 0; i < pipelineArgs.Count; i++)
            {
                pipelineArgs[i] = pipelineArgs[i].Trim();
            }

            List<ConfigPair> results = new List<ConfigPair>(pipelineArgs.Count);

            foreach (string componentString in pipelineArgs)
            {
                ConfigPair config = ConfigUtil.ParseComponentConfig(pipelineComponents, componentString);

                results.Add(config);
            }


            return results;
        }




        private static void PrintUsage()
        {
            Console.WriteLine("Below are the commands for msbp.exe:");
            Console.WriteLine("\tmsbp.exe help");
            Console.WriteLine("\tmsbp.exe backup");
            Console.WriteLine("\tmsbp.exe restore");
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
            foreach (string key in components.Keys)
            {
                IBackupPlugin db = components[key].GetConstructor(new Type[0]).Invoke(new object[0]) as IBackupPlugin;
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
            if (components.ContainsKey(pluginName))
            {
                IBackupPlugin db = components[pluginName].GetConstructor(new Type[0]).Invoke(new object[0]) as IBackupPlugin;
                Console.WriteLine(db.CommandLineHelp);
            }
        }

    }
}
