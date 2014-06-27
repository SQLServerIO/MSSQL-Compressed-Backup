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
using System.Linq;
using System.Reflection;
using System.IO;

//Additional includes other than default dot net framework should go here.
using MSBackupPipe.StdPlugins;
using MSBackupPipe.StdPlugins.Streams;
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

        private static void BackupOrRestore(int commandType, ConfigPair storageConfig, ConfigPair databaseConfig,List<ConfigPair> pipelineConfig, IUpdateNotification updateNotifier, out List<string> returndevices)
        {
            var isBackup = commandType == 1;

            var deviceSetName = Guid.NewGuid().ToString();

            var constructorInfo = storageConfig.TransformationType.GetConstructor(new Type[0]);
            if (constructorInfo == null)
                throw new ArgumentException("Unable to transformation type");

            var storage = constructorInfo.Invoke(new object[0]) as IBackupStorage;
            var constructor = databaseConfig.TransformationType.GetConstructor(new Type[0]);
            if (constructor == null)
                throw new ArgumentException("Unable to transformation type");

            var databaseComp = constructor.Invoke(new object[0]) as IBackupDatabase;
            try
            {
                if (storage == null)
                {
                    returndevices = null;
                    return;
                }
                var numDevices = storage.GetNumberOfDevices(storageConfig.Parameters);
                //get instance name e.g. myserver\myinstance 
                if (databaseComp == null)
                {
                    returndevices = null;
                    return;
                }
                var instanceName = databaseComp.GetInstanceName(databaseConfig.Parameters);
                //get the cluster networking name usually diffrent than the currently running node name
                var clusterNetworkName = databaseComp.GetClusterNetworkName(databaseConfig.Parameters);
                //get bit version of our platform x86,x64 or IA64
                var sqlPlatform = databaseConfig.Parameters.ContainsKey("user")
                    ? VirtualBackupDeviceFactory.DiscoverSqlPlatform(instanceName, clusterNetworkName,
                        databaseConfig.Parameters["user"][0],
                        databaseConfig.Parameters.ContainsKey("password")
                            ? databaseConfig.Parameters["password"][0]
                            : null)
                    : VirtualBackupDeviceFactory.DiscoverSqlPlatform(instanceName, clusterNetworkName, null,
                        null);

                //NotifyWhenReady notifyWhenReady = new NotifyWhenReady(deviceName, isBackup);))
                using (var deviceSet = VirtualBackupDeviceFactory.NewVirtualDeviceSet(sqlPlatform))
                {
                    //Since it is possible to have multiple backup/restore targets we encapsulate each call to a device in its own thread
                    using (var sql = new SqlThread())
                    {
                        var sqlStarted = false;
                        var sqlFinished = false;
                        returndevices = new List<string>();
                        var exceptions = new ParallelExecutionException();

                        try
                        {
                            IStreamNotification streamNotification =
                                new InternalStreamNotification(updateNotifier);
                            long estimatedTotalBytes;
                            var deviceNames = sql.PreConnect(clusterNetworkName, instanceName, deviceSetName,
                                numDevices, databaseComp, databaseConfig.Parameters, commandType, updateNotifier,
                                out estimatedTotalBytes);

                            using (
                                var fileStreams =
                                    new DisposableList<Stream>(isBackup
                                        ? storage.GetBackupWriter(storageConfig.Parameters, estimatedTotalBytes)
                                        : storage.GetRestoreReader(storageConfig.Parameters,
                                            out estimatedTotalBytes)))
                            using (
                                var topOfPilelines =
                                    new DisposableList<Stream>(CreatePipeline(pipelineConfig, fileStreams,
                                        isBackup, streamNotification, estimatedTotalBytes)))
                            {
                                ReturnByteScale(estimatedTotalBytes, isBackup);

                                var config = new VirtualDeviceSetConfig
                                {
                                    Features = FeatureSet.PipeLike,
                                    DeviceCount = (uint) topOfPilelines.Count
                                };
                                deviceSet.CreateEx(instanceName, deviceSetName, config);
                                sql.BeginExecute();
                                sqlStarted = true;
                                deviceSet.GetConfiguration(TimeSpan.FromMinutes(360));
                                var devices = deviceNames.Select(deviceSet.OpenDevice).ToList();

                                returndevices = deviceNames;

                                using (var threads = new DisposableList<DeviceThread>(devices.Count))
                                {
                                    for (var i = 0; i < devices.Count; i++)
                                    {
                                        var dThread = new DeviceThread();
                                        threads.Add(dThread);
                                        dThread.Initialize(isBackup, topOfPilelines[i], devices[i], deviceSet);
                                    }
                                    foreach (var dThread in threads)
                                    {
                                        dThread.BeginCopy();
                                    }

                                    updateNotifier.OnStart();

                                    var sqlE = sql.EndExecute();
                                    sqlFinished = true;

                                    if (sqlE != null)
                                    {
                                        exceptions.Exceptions.Add(sqlE);
                                    }

                                    foreach (var devE in threads.Select(dThread => dThread.EndCopy()).Where(devE => devE != null))
                                    {
                                        exceptions.Exceptions.Add(devE);
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
                            if (exceptions.HasExceptions)
                            {
                                throw exceptions;
                            }

                            if (sqlStarted && !sqlFinished)
                            {
                                var sqlE = sql.EndExecute();
                                if (sqlE != null)
                                {
                                    exceptions.Exceptions.Add(sqlE);
                                }
                            }
                        }
                    }
                }
            }
            catch
            {
                if (storage != null) storage.CleanupOnAbort();
                throw;
            }
        }

        private static void ReturnByteScale(long estimatedTotalBytes, bool isBackup)
        {
            if ((estimatedTotalBytes / 1024) > 1)
            {
                if ((estimatedTotalBytes / 1024 / 1024) > 1)
                {
                    if ((estimatedTotalBytes / 1024 / 1024 / 1024) > 1)
                    {
                        if ((estimatedTotalBytes / 1024 / 1024 / 1024 / 1024) > 1)
                        {
                            Console.WriteLine(string.Format("Estimated Number Of Terabytes To {0}:", isBackup ? "Backup" : "Restore") + string.Format("{0:0.00}", (estimatedTotalBytes / 1024.0 / 1024 / 1024 / 1024)));
                        }
                        else
                        {
                            Console.WriteLine(string.Format("Estimated Number Of Gigabytes To {0}:", isBackup ? "Backup" : "Restore") + string.Format("{0:0.00}", (estimatedTotalBytes / 1024.0 / 1024 / 1024)));
                        }
                    }
                    else
                    {
                        Console.WriteLine(string.Format("Estimated Number Of Megabytes To {0}:", isBackup ? "Backup" : "Restore") + string.Format("{0:0.00}", (estimatedTotalBytes / 1024.0 / 1024)));
                    }
                }
                else
                {
                    Console.WriteLine(string.Format("Estimated Number Of Kilobytes To {0}:", isBackup ? "Backup" : "Restore") + string.Format("{0:0.00}", (estimatedTotalBytes / 1024.0)));
                }
            }
            else
            {
                Console.WriteLine(string.Format("Estimated Number Of bytes To {0}:", isBackup ? "Backup" : "Restore") + estimatedTotalBytes);
            }
        }

        private static IEnumerable<Stream> CreatePipeline(IList<ConfigPair> pipelineConfig, ICollection<Stream> fileStreams, bool isBackup, IStreamNotification streamNotification, long estimatedTotalBytes)
        {
            streamNotification.EstimatedBytes = estimatedTotalBytes;

            var result = new List<Stream>(fileStreams.Count);

            foreach (var fileStream in fileStreams)
            {
                var topStream = fileStream;
                if (!isBackup)
                {
                    topStream = new TrackingStream(topStream, streamNotification);
                }

                for (var i = pipelineConfig.Count - 1; i >= 0; i--)
                {
                    var config = pipelineConfig[i];

                    var constructorInfo = config.TransformationType.GetConstructor(new Type[0]);
                    if (constructorInfo == null) continue;
                    var tran = constructorInfo.Invoke(new object[0]) as IBackupTransformer;
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
            var result = new Dictionary<string, Type>(StringComparer.InvariantCultureIgnoreCase);

            var binDir = new FileInfo(Assembly.GetEntryAssembly().Location).Directory;

            if (binDir == null) return result;
            foreach (var file in binDir.GetFiles("*.dll"))
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

        private static void FindPlugins(Assembly dll, IDictionary<string, Type> result, string interfaceName)
        {
            foreach (var t in dll.GetTypes())
            {
                try
                {
                    if (!t.IsPublic) continue;
                    if ((t.Attributes & TypeAttributes.Abstract) == TypeAttributes.Abstract) continue;
                    if (t.GetInterface(interfaceName) == null) continue;
                    var constructorInfo = t.GetConstructor(new Type[0]);
                    if (constructorInfo == null) continue;
                    var o = constructorInfo.Invoke(new object[0]);

                    var test = o as IBackupPlugin;

                    if (test == null) continue;
                    var name = test.Name.ToLowerInvariant();

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
                catch (Exception e)
                {
                    throw new Exception(string.Format("Warning: Plugin not loaded due to error: {0}", e.Message), e);
                }
            }
        }
    }
}