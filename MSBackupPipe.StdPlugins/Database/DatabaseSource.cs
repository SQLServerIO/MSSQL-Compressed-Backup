/*************************************************************************************\
File Name  :  DatabaseSource.cs
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
using System.Linq;

namespace MSBackupPipe.StdPlugins.Database
{
    public class DatabaseSource : IBackupDatabase
    {
        private static readonly Dictionary<string, ParameterInfo> MBackupParamSchema;
        private static readonly Dictionary<string, ParameterInfo> MRestoreParamSchema;
        private static readonly Dictionary<string, ParameterInfo> MVerifyParamSchema;

        //TODO: make case insensitive for key words to cut down on false syntax errors.
        static DatabaseSource()
        {
            MBackupParamSchema = new Dictionary<string, ParameterInfo>(StringComparer.InvariantCultureIgnoreCase)
            {
                {"database", new ParameterInfo(false, true)},
                {"file", new ParameterInfo(true, false)},
                {"filegroup", new ParameterInfo(true, false)},
                {"instancename", new ParameterInfo(false, false)},
                {"clusternetworkname", new ParameterInfo(false, false)},
                {"backuptype", new ParameterInfo(false, false)},
                {"user", new ParameterInfo(false, false)},
                {"password", new ParameterInfo(false, false)},
                {"READ_WRITE_FILEGROUPS", new ParameterInfo(false, false)},
                {"COPY_ONLY", new ParameterInfo(false, false)},
                {"CHECKSUM", new ParameterInfo(false, false)},
                {"NO_CHECKSUM", new ParameterInfo(false, false)},
                {"STOP_ON_ERROR", new ParameterInfo(false, false)},
                {"CONTINUE_AFTER_ERROR", new ParameterInfo(false, false)},
                {"BUFFERCOUNT", new ParameterInfo(false, false)},
                {"MAXTRANSFERSIZE", new ParameterInfo(false, false)},
                {"BLOCKSIZE", new ParameterInfo(false, false)},
                {"STATS", new ParameterInfo(false, false)}
            };
            //TODO: enable switch to turn stats reporting on or off
            //mBackupParamSchema.Add("STATS", new ParameterInfo(false, false));

            MRestoreParamSchema = new Dictionary<string, ParameterInfo>(StringComparer.InvariantCultureIgnoreCase)
            {
                {"database", new ParameterInfo(false, true)},
                {"instancename", new ParameterInfo(false, false)},
                {"ClusterNetworkName", new ParameterInfo(false, false)},
                {"restoretype", new ParameterInfo(false, false)},
                {"user", new ParameterInfo(false, false)},
                {"password", new ParameterInfo(false, false)},
                {"CHECKSUM", new ParameterInfo(false, false)},
                {"NO_CHECKSUM", new ParameterInfo(false, false)},
                {"STOP_ON_ERROR", new ParameterInfo(false, false)},
                {"CONTINUE_AFTER_ERROR", new ParameterInfo(false, false)},
                {"KEEP_REPLICATION", new ParameterInfo(false, false)},
                {"ENABLE_BROKER", new ParameterInfo(false, false)},
                {"ERROR_BROKER_CONVERSATIONS", new ParameterInfo(false, false)},
                {"NEW_BROKER", new ParameterInfo(false, false)},
                {"RECOVERY", new ParameterInfo(false, false)},
                {"NORECOVERY", new ParameterInfo(false, false)},
                {"STANDBY", new ParameterInfo(false, false)},
                {"REPLACE", new ParameterInfo(false, false)},
                {"RESTART", new ParameterInfo(false, false)},
                {"RESTRICTED_USER", new ParameterInfo(false, false)},
                {"STOPAT", new ParameterInfo(false, false)},
                {"STOPATMARK", new ParameterInfo(false, false)},
                {"STOPBEFOREMARK", new ParameterInfo(false, false)},
                {"PARTIAL", new ParameterInfo(false, false)},
                {"READ_WRITE_FILEGROUPS", new ParameterInfo(false, false)},
                {"FILE", new ParameterInfo(true, false)},
                {"FILEGROUP", new ParameterInfo(true, false)},
                {"LOADHISTORY", new ParameterInfo(false, false)},
                {"MOVE", new ParameterInfo(true, false)},
                {"BUFFERCOUNT", new ParameterInfo(false, false)},
                {"MAXTRANSFERSIZE", new ParameterInfo(false, false)}
            };

            MVerifyParamSchema = new Dictionary<string, ParameterInfo>(StringComparer.InvariantCultureIgnoreCase)
            {
                {"database", new ParameterInfo(false, true)},
                {"instancename", new ParameterInfo(false, false)},
                {"ClusterNetworkName", new ParameterInfo(false, false)},
                {"restoretype", new ParameterInfo(false, false)},
                {"user", new ParameterInfo(false, false)},
                {"password", new ParameterInfo(false, false)},
                {"CHECKSUM", new ParameterInfo(false, false)},
                {"NO_CHECKSUM", new ParameterInfo(false, false)},
                {"STOP_ON_ERROR", new ParameterInfo(false, false)},
                {"CONTINUE_AFTER_ERROR", new ParameterInfo(false, false)},
                {"LOADHISTORY", new ParameterInfo(false, false)}
            };
        }

        #region IBackupDatabase Members

        public string Name
        {
            get { return "db"; }
        }

        public void ConfigureBackupCommand(Dictionary<string, List<string>> config, IEnumerable<string> deviceNames, SqlCommand cmd)
        {
            ParameterInfo.ValidateParams(MBackupParamSchema, config);

            var param = new SqlParameter("@databasename", SqlDbType.NVarChar, 255) {Value = config["database"][0]};
            cmd.Parameters.Add(param);

            // default values:
            var backupType = BackupType.Full;

            if (config.ContainsKey("backuptype"))
            {
                switch (config["backuptype"][0])
                {
                    case "full":
                        backupType = BackupType.Full;
                        break;
                    case "differential":
                        backupType = BackupType.Differential;
                        break;
                    case "log":
                        backupType = BackupType.Log;
                        break;
                    default:
                        throw new ArgumentException(string.Format("db: Unknown backuptype: {0}", config["backuptype"][0]));
                }
            }

            var withOptions = new List<string>();
            var filegroupOptions = new List<string>();

            if (config.ContainsKey("READ_WRITE_FILEGROUPS"))
            {
                filegroupOptions.Add("READ_WRITE_FILEGROUPS");
            }

            if (config.ContainsKey("FILE"))
            {
                var i = 0;
                foreach (var file in config["FILE"])
                {
                    filegroupOptions.Add(string.Format("FILE=@file{0}", i));
                    param = new SqlParameter(string.Format("@file{0}", i), SqlDbType.NVarChar, 2000) {Value = file};
                    cmd.Parameters.Add(param);

                    i++;
                }
            }

            if (config.ContainsKey("FILEGROUP"))
            {
                var i = 0;
                foreach (var filegroup in config["FILEGROUP"])
                {
                    filegroupOptions.Add(string.Format("FILEGROUP=@filegroup{0}", i));
                    param = new SqlParameter(string.Format("@filegroup{0}", i), SqlDbType.NVarChar, 2000)
                    {
                        Value = filegroup
                    };
                    cmd.Parameters.Add(param);

                    i++;
                }
            }

            if (config.ContainsKey("COPY_ONLY"))
            {
                withOptions.Add("COPY_ONLY");
            }

            if (config.ContainsKey("CHECKSUM"))
            {
                withOptions.Add("CHECKSUM");
            }

            if (config.ContainsKey("NO_CHECKSUM"))
            {
                withOptions.Add("NO_CHECKSUM");
            }

            if (config.ContainsKey("STOP_ON_ERROR"))
            {
                withOptions.Add("STOP_ON_ERROR");
            }

            if (config.ContainsKey("CONTINUE_AFTER_ERROR"))
            {
                withOptions.Add("CONTINUE_AFTER_ERROR");
            }

            if (config.ContainsKey("BUFFERCOUNT"))
            {
                var valList = config["BUFFERCOUNT"];
                if (valList.Count != 1)
                {
                    throw new ArgumentException("BUFFERCOUNT parameter must have a value.");
                }
                var val = valList[0];
                int valInt;
                if (!int.TryParse(val, out valInt))
                {
                    throw new ArgumentException(string.Format("BUFFERCOUNT parameter was not a number: {0}", val));
                }
                withOptions.Add(string.Format("BUFFERCOUNT={0}", valInt));
            }
            
            if (config.ContainsKey("MAXTRANSFERSIZE"))
            {
                var valList = config["MAXTRANSFERSIZE"];
                if (valList.Count != 1)
                {
                    throw new ArgumentException("MAXTRANSFERSIZE parameter must have a value between 65536 and 4194304.");
                }
                var val = valList[0];
                int valInt;
                if (!int.TryParse(val, out valInt))
                {
                    throw new ArgumentException(string.Format("MAXTRANSFERSIZE parameter was not a number: {0}", val));
                }
                else if ((valInt % 65536) > 1)
                {
                    throw new ArgumentException(string.Format("MAXTRANSFERSIZE parameter must be a multiple of 65536: {0}", val));
                }
                else if ((valInt > 4194304) || (valInt < 65536))
                {
                    throw new ArgumentException(string.Format("MAXTRANSFERSIZE parameter cannot be larger than 4194304 or smaller than 65536: {0}", val));
                }

                withOptions.Add(string.Format("MAXTRANSFERSIZE={0}", valInt));
            }

            if (config.ContainsKey("BLOCKSIZE"))
            {
                var valList = config["BLOCKSIZE"];
                if (valList.Count != 1)
                {
                    throw new ArgumentException("BLOCKSIZE parameter must have a value.");
                }
                var val = valList[0];
                int valInt;
                if (!int.TryParse(val, out valInt))
                {
                    throw new ArgumentException(string.Format("BLOCKSIZE parameter was not a number: {0}", val));
                }
                else if ((valInt % 512) > 1)
                {
                    throw new ArgumentException(string.Format("BLOCKSIZE parameter must be a multiple of 512: {0}", val));
                }

                else if (valInt == 512)
                {
                    withOptions.Add(string.Format("BlockSize={0}", valInt));
                }
                else if (valInt == 1024)
                {
                    withOptions.Add(string.Format("BlockSize={0}", valInt));
                }
                else if (valInt == 2048)
                {
                    withOptions.Add(string.Format("BlockSize={0}", valInt));
                }
                else if (valInt == 4096)
                {
                    withOptions.Add(string.Format("BlockSize={0}", valInt));
                }
                else if (valInt == 8192)
                {
                    withOptions.Add(string.Format("BlockSize={0}", valInt));
                }
                else if (valInt == 16384)
                {
                    withOptions.Add(string.Format("BlockSize={0}", valInt));
                }
                else if (valInt == 32768)
                {
                    withOptions.Add(string.Format("BlockSize={0}", valInt));
                }
                else if (valInt == 65536)
                {
                    withOptions.Add(string.Format("BlockSize={0}", valInt));
                }
                else
                {
                    throw new ArgumentException(string.Format("BLOCKSIZE must be 512, 1024, 2048, 4096, 8192, 16384, 32768, or 65536: {0}", val));
                }
            }

            //TODO: add validator for STATS other than is number
            if (config.ContainsKey("STATS"))
            {
                var valList = config["STATS"];
                if (valList.Count != 1)
                {
                    throw new ArgumentException("STATS parameter must have a value.");
                }
                var val = valList[0];
                int valInt;
                if (!int.TryParse(val, out valInt))
                {
                    throw new ArgumentException(string.Format("STATS parameter was not a number: {0}", val));
                }
                withOptions.Add(string.Format("Stats={0}", valInt));
            }

            if (backupType == BackupType.Differential)
            {
                withOptions.Insert(0, "DIFFERENTIAL");
            }

            string filegroupClause = null;
            if (filegroupOptions.Count > 0)
            {
                for (var i = 0; i < filegroupOptions.Count; i++)
                {
                    if (i > 0)
                    {
                        filegroupClause += ",";
                    }
                    filegroupClause += filegroupOptions[i];
                }
                filegroupClause += " ";
            }

            string withClause = null;
            if (withOptions.Count > 0)
            {
                withClause = " WITH ";
                for (var i = 0; i < withOptions.Count; i++)
                {
                    if (i > 0)
                    {
                        withClause += ",";
                    }
                    withClause += withOptions[i];
                }
            }

            var databaseOrLog = backupType == BackupType.Log ? "LOG" : "DATABASE";

            cmd.CommandType = CommandType.Text;

            var tempDevs = new List<string>(deviceNames);
            var devSql = tempDevs.ConvertAll(devName => string.Format("VIRTUAL_DEVICE='{0}'", devName));

            cmd.CommandText = string.Format("BACKUP {0} @databasename {1}TO {2}{3};", databaseOrLog, filegroupClause, string.Join(",", devSql.ToArray()), withClause);
        }

        public void ConfigureRestoreCommand(Dictionary<string, List<string>> config, IEnumerable<string> deviceNames, SqlCommand cmd)
        {
            ParameterInfo.ValidateParams(MRestoreParamSchema, config);

            var param = new SqlParameter("@databasename", SqlDbType.NVarChar, 255) {Value = config["database"][0]};
            cmd.Parameters.Add(param);

            // default values:
            var restoreType = RestoreType.Database;

            if (config.ContainsKey("restoretype"))
            {
                switch (config["restoretype"][0])
                {
                    case "database":
                        restoreType = RestoreType.Database;
                        break;
                    case "log":
                        restoreType = RestoreType.Log;
                        break;
                    default:
                        throw new ArgumentException(string.Format("db: Unknown restoreType: {0}", config["restoretype"][0]));
                }
            }

            var withOptions = new List<string>();
            var filegroupOptions = new List<string>();

            if (config.ContainsKey("CHECKSUM"))
            {
                withOptions.Add("CHECKSUM");
            }

            if (config.ContainsKey("NO_CHECKSUM"))
            {
                withOptions.Add("NO_CHECKSUM");
            }

            if (config.ContainsKey("STOP_ON_ERROR"))
            {
                withOptions.Add("STOP_ON_ERROR");
            }

            if (config.ContainsKey("CONTINUE_AFTER_ERROR"))
            {
                withOptions.Add("CONTINUE_AFTER_ERROR");
            }

            if (config.ContainsKey("KEEP_REPLICATION"))
            {
                withOptions.Add("KEEP_REPLICATION");
            }

            if (config.ContainsKey("ENABLE_BROKER"))
            {
                withOptions.Add("ENABLE_BROKER");
            }

            if (config.ContainsKey("ERROR_BROKER_CONVERSATIONS"))
            {
                withOptions.Add("ERROR_BROKER_CONVERSATIONS");
            }

            if (config.ContainsKey("NEW_BROKER"))
            {
                withOptions.Add("NEW_BROKER");
            }

            if (config.ContainsKey("RECOVERY"))
            {
                withOptions.Add("RECOVERY");
            }

            if (config.ContainsKey("NORECOVERY"))
            {
                withOptions.Add("NORECOVERY");
            }

            if (config.ContainsKey("STANDBY"))
            {
                var standbyFile = config["STANDBY"][0].Trim();
                if (standbyFile.StartsWith("'"))
                {
                    standbyFile = standbyFile.Substring(1);
                }
                if (standbyFile.EndsWith("'"))
                {
                    standbyFile = standbyFile.Substring(0, standbyFile.Length - 1);
                }
                if (standbyFile.Contains("'"))
                {
                    throw new ArgumentException("db: The standby filename cannot have a singe quote (') in the path.");
                }
                withOptions.Add(string.Format("STANDBY='{0}'", standbyFile));
            }

            if (config.ContainsKey("REPLACE"))
            {
                withOptions.Add("REPLACE");
            }

            if (config.ContainsKey("RESTART"))
            {
                withOptions.Add("RESTART");
            }

            if (config.ContainsKey("RESTRICTED_USER"))
            {
                withOptions.Add("RESTRICTED_USER");
            }

            if (config.ContainsKey("STOPAT"))
            {
                DateTime stopAtDateTime;
                if (!DateTime.TryParse(config["STOPAT"][0], out stopAtDateTime))
                {
                    throw new ArgumentException(string.Format("db: .Net was unable determine the date and time of the stopat parameter: {0}", config["STOPAT"][0]));
                }
                withOptions.Add("STOPAT=@stopat");
                param = new SqlParameter("@stopat", SqlDbType.DateTime) {Value = stopAtDateTime};
                cmd.Parameters.Add(param);
            }

            if (config.ContainsKey("STOPATMARK"))
            {
                withOptions.Add("STOPATMARK=@stopatmark");
                param = new SqlParameter("@stopatmark", SqlDbType.VarChar) {Value = config["STOPATMARK"][0]};
                cmd.Parameters.Add(param);
            }

            if (config.ContainsKey("STOPBEFOREMARK"))
            {
                withOptions.Add("STOPBEFOREMARK=@stopbeforemark");
                param = new SqlParameter("@stopbeforemark", SqlDbType.VarChar) {Value = config["STOPBEFOREMARK"][0]};
                cmd.Parameters.Add(param);
            }

            if (config.ContainsKey("PARTIAL"))
            {
                withOptions.Add("PARTIAL");
            }

            if (config.ContainsKey("PARTIAL"))
            {
                withOptions.Add("PARTIAL");
            }

            if (config.ContainsKey("READ_WRITE_FILEGROUPS"))
            {
                filegroupOptions.Add("READ_WRITE_FILEGROUPS");
            }

            if (config.ContainsKey("FILE"))
            {
                var i = 0;
                foreach (var file in config["FILE"])
                {
                    filegroupOptions.Add(string.Format("FILE=@file{0}", i));
                    param = new SqlParameter(string.Format("@file{0}", i), SqlDbType.NVarChar, 2000) {Value = file};
                    cmd.Parameters.Add(param);

                    i++;
                }
            }

            if (config.ContainsKey("FILEGROUP"))
            {
                var i = 0;
                foreach (var filegroup in config["FILEGROUP"])
                {
                    filegroupOptions.Add(string.Format("FILEGROUP=@filegroup{0}", i));
                    param = new SqlParameter(string.Format("@filegroup{0}", i), SqlDbType.NVarChar, 2000)
                    {
                        Value = filegroup
                    };
                    cmd.Parameters.Add(param);

                    i++;
                }
            }

            if (config.ContainsKey("LOADHISTORY"))
            {
                withOptions.Add("LOADHISTORY");
            }

            if (config.ContainsKey("MOVE"))
            {
                var moveClause = " ";
                var i = 0;
                foreach (var moveInfo in config["MOVE"])
                {
                    if (i > 0)
                    {
                        moveClause += ", ";
                    }

                    var quoteCount = moveInfo.Count(c => c == '\'');
                    if (quoteCount != 4)
                    {
                        throw new ArgumentException(string.Format("db: Invalid MOVE clause: {0}.  Please write it in the form MOVE='from'TO'to'", moveInfo));
                    }

                    var moveSplit = moveInfo.Split('\'');
                    var moveFrom = moveSplit[1];
                    var moveToKeyword = moveSplit[2].Trim();
                    var moveTo = moveSplit[3];

                    if (moveToKeyword != "TO")
                    {
                        throw new ArgumentException(string.Format("db: Invalid MOVE clause: {0}.  Please write it in the form MOVE='from'TO'to'", moveInfo));
                    }

                    moveClause += string.Format("MOVE '{0}' TO '{1}'", moveFrom, moveTo);

                    i++;
                }
                withOptions.Add(moveClause);
            }

            if (config.ContainsKey("BUFFERCOUNT"))
            {
                var valList = config["BUFFERCOUNT"];
                if (valList.Count != 1)
                {
                    throw new ArgumentException("BUFFERCOUNT parameter must have a value.");
                }
                var val = valList[0];
                int valInt;
                if (!int.TryParse(val, out valInt))
                {
                    throw new ArgumentException(string.Format("BUFFERCOUNT parameter was not a number: {0}", val));
                }
                withOptions.Add(string.Format("BUFFERCOUNT={0}", valInt));
            }

            if (config.ContainsKey("MAXTRANSFERSIZE"))
            {
                var valList = config["MAXTRANSFERSIZE"];
                if (valList.Count != 1)
                {
                    throw new ArgumentException("MAXTRANSFERSIZE parameter must have a value.");
                }
                var val = valList[0];
                int valInt;
                if (!int.TryParse(val, out valInt))
                {
                    throw new ArgumentException(string.Format("MAXTRANSFERSIZE parameter was not a number: {0}", val));
                }
                withOptions.Add(string.Format("MAXTRANSFERSIZE={0}", valInt));
            }

            if (config.ContainsKey("STATS"))
            {
                var valList = config["STATS"];
                if (valList.Count != 1)
                {
                    throw new ArgumentException("STATS parameter must have a value.");
                }
                var val = valList[0];
                int valInt;
                if (!int.TryParse(val, out valInt))
                {
                    throw new ArgumentException(string.Format("STATS parameter was not a number: {0}", val));
                }
                withOptions.Add(string.Format("STATS={0}", valInt));
            }

            string filegroupClause = null;
            if (filegroupOptions.Count > 0)
            {
                for (var i = 0; i < filegroupOptions.Count; i++)
                {
                    if (i > 0)
                    {
                        filegroupClause += ",";
                    }
                    filegroupClause += filegroupOptions[i];
                }
                filegroupClause += " ";
            }

            string withClause = null;
            if (withOptions.Count > 0)
            {
                withClause = " WITH ";
                for (var i = 0; i < withOptions.Count; i++)
                {
                    if (i > 0)
                    {
                        withClause += ",";
                    }
                    withClause += withOptions[i];
                }
            }

            var databaseOrLog = restoreType == RestoreType.Log ? "LOG" : "DATABASE";
            cmd.CommandType = CommandType.Text;
            var tempDevs = new List<string>(deviceNames);
            var devSql = tempDevs.ConvertAll(devName => string.Format("VIRTUAL_DEVICE='{0}'", devName));

            cmd.CommandText = string.Format("RESTORE {0} @databasename {1}FROM {2}{3};", databaseOrLog, filegroupClause, string.Join(",", devSql.ToArray()), withClause);
        }

        public void ConfigureVerifyCommand(Dictionary<string, List<string>> config, IEnumerable<string> deviceNames, SqlCommand cmd)
        {
            ParameterInfo.ValidateParams(MVerifyParamSchema, config);

            var param = new SqlParameter("@databasename", SqlDbType.NVarChar, 255) {Value = config["database"][0]};
            cmd.Parameters.Add(param);

            var withOptions = new List<string>();
            //var filegroupOptions = new List<string>();

            if (config.ContainsKey("CHECKSUM"))
            {
                withOptions.Add("CHECKSUM");
            }

            if (config.ContainsKey("NO_CHECKSUM"))
            {
                withOptions.Add("NO_CHECKSUM");
            }

            if (config.ContainsKey("STOP_ON_ERROR"))
            {
                withOptions.Add("STOP_ON_ERROR");
            }

            if (config.ContainsKey("CONTINUE_AFTER_ERROR"))
            {
                withOptions.Add("CONTINUE_AFTER_ERROR");
            }

            if (config.ContainsKey("LOADHISTORY"))
            {
                withOptions.Add("LOADHISTORY");
            }
            string withClause = null;
            if (withOptions.Count > 0)
            {
                withClause = " WITH ";
                for (var i = 0; i < withOptions.Count; i++)
                {
                    if (i > 0)
                    {
                        withClause += ",";
                    }
                    withClause += withOptions[i];
                }
            }

            const string databaseOrLog = "VERIFYONLY"; // restoreType == RestoreType.Log ? "LOG" : "DATABASE";
            cmd.CommandType = CommandType.Text;
            var tempDevs = new List<string>(deviceNames);
            var devSql = tempDevs.ConvertAll(devName => string.Format("VIRTUAL_DEVICE='{0}'", devName));
            cmd.CommandText = string.Format("RESTORE {0} FROM {1}{2};", databaseOrLog, string.Join(",", devSql.ToArray()), withClause);
        }

        public string GetInstanceName(Dictionary<string, List<string>> config)
        {
            string instanceName = null;

            if (config.ContainsKey("instancename"))
            {
                instanceName = config["instancename"][0];
            }

            if (instanceName != null)
            {
                instanceName = instanceName.Trim();
            }

            return instanceName;
        }

        public string GetClusterNetworkName(Dictionary<string, List<string>> config)
        {
            string clusterNetworkName = null;

            if (config.ContainsKey("ClusterNetworkName"))
            {
                clusterNetworkName = config["ClusterNetworkName"][0];
            }

            return clusterNetworkName;
        }

        public string CommandLineHelp
        {
            get
            {
                return @"db Usage: \ndb(database=<dbname>;instance=<instancename>) \n<dbname> should be the database name without any brackets.  \n<instancename> should only be the name of the instance after the slash.  If you \nwant to connect to localhost\sqlexpress, then enter instance=sqlexpress above. \nIf no instancename parameter is given, then it will connect to the default \ninstance. \nIf you have a cluster, please see the online documentation about the ClusterNetworkName option. \nThis plugin can only connect to SQL Server locally. \nmsbp.exe has an alias for the db plugin.  A database name in brackets, like [model] is converted to db(database=model).";
            }
        }

        #endregion

        private enum BackupType
        {
            Full,
            Differential,
            Log
        }

        private enum RestoreType
        {
            Database,
            Log
        }
    }
}