/*************************************************************************************\
File Name  :  BackupPipeSystem.cs
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
using System.Text;
using System.Reflection;
using System.IO;

//Additional includes other than default dot net framework should go here.
using MSBackupPipe.StdPlugins;
using VdiNet.VirtualBackupDevice;

namespace MSBackupPipe.Common
{
    //pipe system is the foundation of the entire process
    public static class BackupPipeSystem
    {
        public static Dictionary<string, Type> LoadTransformComponents()
        {
            return LoadComponents("IBackupTransformer");
        }

        public static Dictionary<string, Type> LoadDatabaseComponents()
        {
            return LoadComponents("IBackupDatabase");
        }

        public static Dictionary<string, Type> LoadStorageComponents()
        {
            return LoadComponents("IBackupStorage");
        }

        public static void Backup(ConfigPair databaseConfig, List<ConfigPair> pipelineConfig, ConfigPair storageConfig, IUpdateNotification updateNotifier, out List<string> devices)
        {
            List<string> returndevices;
            BackupOrRestore(1, storageConfig, databaseConfig, pipelineConfig, updateNotifier,out returndevices);
            devices = returndevices;
         }

        public static void Restore(ConfigPair storageConfig, List<ConfigPair> pipelineConfig, ConfigPair databaseConfig, IUpdateNotification updateNotifier)
        {
            List<string> returndevices;
            BackupOrRestore(2, storageConfig, databaseConfig, pipelineConfig, updateNotifier, out returndevices);
        }

        public static void Verify(ConfigPair storageConfig, List<ConfigPair> pipelineConfig, ConfigPair databaseConfig, IUpdateNotification updateNotifier)
        {
            List<string> returndevices;
            BackupOrRestore(3, storageConfig, databaseConfig, pipelineConfig, updateNotifier, out returndevices);
        }

        public static void HeaderOnly(ConfigPair storageConfig, List<ConfigPair> pipelineConfig, ConfigPair databaseConfig, IUpdateNotification updateNotifier)
        {
            List<string> returndevices;
            BackupOrRestore(4, storageConfig, databaseConfig, pipelineConfig, updateNotifier, out returndevices);
        }

        public static void FileList(ConfigPair storageConfig, List<ConfigPair> pipelineConfig, ConfigPair databaseConfig, IUpdateNotification updateNotifier)
        {
            List<string> returndevices;
            BackupOrRestore(5, storageConfig, databaseConfig, pipelineConfig, updateNotifier, out returndevices);
        }

        private static void BackupOrRestore(int commandType, ConfigPair storageConfig, ConfigPair databaseConfig, List<ConfigPair> pipelineConfig, IUpdateNotification updateNotifier, out List<string> returndevices)
        {
            bool isBackup;
            if (commandType == 1)
                isBackup = true;
            else
                isBackup = false;

            string deviceSetName = Guid.NewGuid().ToString();

            IBackupStorage storage = storageConfig.TransformationType.GetConstructor(new Type[0]).Invoke(new object[0]) as IBackupStorage;
            IBackupDatabase databaseComp = databaseConfig.TransformationType.GetConstructor(new Type[0]).Invoke(new object[0]) as IBackupDatabase;

            try
            {
                int numDevices = storage.GetNumberOfDevices(storageConfig.Parameters);
                //get instance name e.g. myserver\myinstance 
                string instanceName = databaseComp.GetInstanceName(databaseConfig.Parameters);
                //get the cluster networking name usually diffrent than the currently running node name
                string clusterNetworkName = databaseComp.GetClusterNetworkName(databaseConfig.Parameters);
                SqlPlatform sqlPlatform = new SqlPlatform();
                //get bit version of our platform x86,x64 or IA64
                if (databaseConfig.Parameters.ContainsKey("user"))
                {
                    if (databaseConfig.Parameters.ContainsKey("password"))
                    {
                        sqlPlatform = VirtualBackupDeviceFactory.DiscoverSqlPlatform(instanceName, clusterNetworkName, databaseConfig.Parameters["user"][0].ToString(), databaseConfig.Parameters["password"][0].ToString());
                    }
                    else
                    {
                        sqlPlatform = VirtualBackupDeviceFactory.DiscoverSqlPlatform(instanceName, clusterNetworkName, databaseConfig.Parameters["user"][0].ToString(), null);
                    }
                }
                else
                {
                    sqlPlatform = VirtualBackupDeviceFactory.DiscoverSqlPlatform(instanceName, clusterNetworkName, null, null);
                }

                //NotifyWhenReady notifyWhenReady = new NotifyWhenReady(deviceName, isBackup);))
                using (IVirtualDeviceSet deviceSet = VirtualBackupDeviceFactory.NewVirtualDeviceSet(sqlPlatform))
                {
                    //Since it is possible to have multiple backup/restore targets we encapsulate each call to a device in its own thread
                    using (SqlThread sql = new SqlThread())
                    {
                        bool sqlStarted = false;
                        bool sqlFinished = false;
                        returndevices = new List<string>();
                        ParallelExecutionException exceptions = new ParallelExecutionException();

                        try
                        {
                            IStreamNotification streamNotification = new InternalStreamNotification(updateNotifier);
                            long estimatedTotalBytes;
                            List<string> deviceNames = sql.PreConnect(clusterNetworkName, instanceName, deviceSetName, numDevices, databaseComp, databaseConfig.Parameters, commandType, updateNotifier, out estimatedTotalBytes);

                            using (DisposableList<Stream> fileStreams = new DisposableList<Stream>(isBackup ? storage.GetBackupWriter(storageConfig.Parameters) : storage.GetRestoreReader(storageConfig.Parameters, out estimatedTotalBytes)))
                            using (DisposableList<Stream> topOfPilelines = new DisposableList<Stream>(CreatePipeline(pipelineConfig, fileStreams, isBackup, streamNotification, estimatedTotalBytes)))
                            {
                                ReturnByteScale(estimatedTotalBytes);

                                VirtualDeviceSetConfig config = new VirtualDeviceSetConfig();
                                config.Features = FeatureSet.PipeLike;
                                config.DeviceCount = (uint)topOfPilelines.Count;
                                deviceSet.CreateEx(instanceName, deviceSetName, config);
                                sql.BeginExecute();
                                sqlStarted = true;
                                deviceSet.GetConfiguration(TimeSpan.FromMinutes(1));
                                List<IVirtualDevice> devices = new List<IVirtualDevice>();

                                foreach (string devName in deviceNames)
                                {
                                    devices.Add(deviceSet.OpenDevice(devName));
                                }

                                returndevices = deviceNames;

                                using (DisposableList<DeviceThread> threads = new DisposableList<DeviceThread>(devices.Count))
                                {
                                    for (int i = 0; i < devices.Count; i++)
                                    {
                                        DeviceThread dThread = new DeviceThread();
                                        threads.Add(dThread);
                                        dThread.Initialize(isBackup, topOfPilelines[i], devices[i], deviceSet);
                                    }
                                    foreach (DeviceThread dThread in threads)
                                    {
                                        dThread.BeginCopy();
                                    }

                                    updateNotifier.OnStart();
                                    Console.WriteLine(string.Format("{0} started", isBackup ? "Backup" : "Restore"));

                                    Exception sqlE = sql.EndExecute();
                                    sqlFinished = true;

                                    if (sqlE != null)
                                    {
                                        exceptions.Exceptions.Add(sqlE);
                                    }

                                    foreach (DeviceThread dThread in threads)
                                    {
                                        Exception devE = dThread.EndCopy();
                                        if (devE != null)
                                        {
                                            exceptions.Exceptions.Add(devE);
                                        }
                                    }
                                }
                            }
                        }
                        catch (Exception e)
                        {
                            exceptions.Exceptions.Add(e);
                        }
                        finally
                        {
                            if (sqlStarted && !sqlFinished)
                            {
                                Exception sqlE = sql.EndExecute();
                                sqlFinished = true;
                                if (sqlE != null)
                                {
                                    exceptions.Exceptions.Add(sqlE);
                                }
                            }
                        }

                        if (exceptions.HasExceptions)
                        {
                            throw exceptions;
                        }
                    }
                }
            }
            catch
            {
                storage.CleanupOnAbort();
                throw;
            }

        }

        private static void ReturnByteScale(long estimatedTotalBytes)
        {
            if ((estimatedTotalBytes / 1024) > 1)
            {
                if ((estimatedTotalBytes / 1024 / 1024) > 1)
                {
                    if ((estimatedTotalBytes / 1024 / 1024 / 1024) > 1)
                    {
                        if ((estimatedTotalBytes / 1024 / 1024 / 1024 / 1024) > 1)
                        {
                            Console.WriteLine("Estimated Number Of Terabytes To Backup: " + string.Format("{0:0.00}", (estimatedTotalBytes / 1024.0 / 1024 / 1024 / 1024)));
                        }
                        else
                        {
                            Console.WriteLine("Estimated Number Of Gigabytes To Backup: " + string.Format("{0:0.00}", (estimatedTotalBytes / 1024.0 / 1024 / 1024)));
                        }
                    }
                    else
                    {
                        Console.WriteLine("Estimated Number Of Megabytes To Backup: " + string.Format("{0:0.00}", (estimatedTotalBytes / 1024.0 / 1024)));
                    }
                }
                else
                {
                    Console.WriteLine("Estimated Number Of Kilobytes To Backup: " + string.Format("{0:0.00}", (estimatedTotalBytes / 1024.0)));
                }
            }
            else
            {
                Console.WriteLine("Estimated Number Of bytes To Backup: " + estimatedTotalBytes.ToString());
            }
        }

        private static Stream[] CreatePipeline(List<ConfigPair> pipelineConfig, IList<Stream> fileStreams, bool isBackup, IStreamNotification streamNotification, long estimatedTotalBytes)
        {
            streamNotification.EstimatedBytes = estimatedTotalBytes;

            List<Stream> result = new List<Stream>(fileStreams.Count);

            foreach (Stream fileStream in fileStreams)
            {
                Stream topStream = fileStream;
                if (!isBackup)
                {
                    topStream = new TrackingStream(topStream, streamNotification);
                }

                for (int i = pipelineConfig.Count - 1; i >= 0; i--)
                {
                    ConfigPair config = pipelineConfig[i];

                    IBackupTransformer tran = config.TransformationType.GetConstructor(new Type[0]).Invoke(new object[0]) as IBackupTransformer;
                    if (tran == null)
                    {
                        throw new ArgumentException(string.Format("Unable to create pipe component: {0}", config.TransformationType.Name));
                    }
                    topStream = isBackup ? tran.GetBackupWriter(config.Parameters, topStream) : tran.GetRestoreReader(config.Parameters, topStream);
                }

                if (isBackup)
                {
                    topStream = new TrackingStream(topStream, streamNotification);
                }
                result.Add(topStream);
            }
            return result.ToArray();
        }

        private static Dictionary<string, Type> LoadComponents(string interfaceName)
        {
            Dictionary<string, Type> result = new Dictionary<string, Type>(StringComparer.InvariantCultureIgnoreCase);

            DirectoryInfo binDir = new FileInfo(Assembly.GetEntryAssembly().Location).Directory;

            foreach (FileInfo file in binDir.GetFiles("*.dll"))
            {
                Assembly dll = null;
                try
                {
                    dll = Assembly.LoadFrom(file.FullName);
                }
                catch (Exception)
                { }
                if (dll != null)
                {
                    FindPlugins(dll, result, interfaceName);
                }
            }
            return result;
        }

        private static void FindPlugins(Assembly dll, Dictionary<string, Type> result, string interfaceName)
        {
            foreach (Type t in dll.GetTypes())
            {
                try
                {
                    if (t.IsPublic)
                    {
                        if (!((t.Attributes & TypeAttributes.Abstract) == TypeAttributes.Abstract))
                        {
                            if (t.GetInterface(interfaceName) != null)
                            {
                                object o = t.GetConstructor(new Type[0]).Invoke(new object[0]);

                                IBackupPlugin test = o as IBackupPlugin;

                                if (test != null)
                                {
                                    string name = test.Name.ToLowerInvariant();

                                    if (name.Contains("|") || name.Contains("("))
                                    {
                                        throw new ArgumentException(string.Format("The name of the plugin, {0}, cannot contain these characters: |, (", name));
                                    }

                                    if (result.ContainsKey(name))
                                    {
                                        throw new ArgumentException(string.Format("plugin found twice: {0}", name));
                                    }
                                    result.Add(name, t);
                                }
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    throw new Exception(string.Format("Warning: Plugin not loaded due to error: {0}", e.Message), e);
                }
            }
        }
    }
}