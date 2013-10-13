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
using System.Text;
using System.Data;
using System.Data.SqlClient;

//Additional includes other than default dot net framework should go here.

namespace MSBackupPipe.StdPlugins.Database
{
    public class DatabaseSource : IBackupDatabase
    {
        private static Dictionary<string, ParameterInfo> mBackupParamSchema;
        private static Dictionary<string, ParameterInfo> mRestoreParamSchema;
        private static Dictionary<string, ParameterInfo> mVerifyParamSchema;

        static DatabaseSource()
        {
            mBackupParamSchema = new Dictionary<string, ParameterInfo>(StringComparer.InvariantCultureIgnoreCase);
            mBackupParamSchema.Add("database", new ParameterInfo(false, true));
            mBackupParamSchema.Add("file", new ParameterInfo(true, false));
            mBackupParamSchema.Add("filegroup", new ParameterInfo(true, false));
            mBackupParamSchema.Add("instancename", new ParameterInfo(false, false));
            mBackupParamSchema.Add("clusternetworkname", new ParameterInfo(false, false));
            mBackupParamSchema.Add("backuptype", new ParameterInfo(false, false));
            mBackupParamSchema.Add("user", new ParameterInfo(false, false));
            mBackupParamSchema.Add("password", new ParameterInfo(false, false));
            mBackupParamSchema.Add("READ_WRITE_FILEGROUPS", new ParameterInfo(false, false));
            mBackupParamSchema.Add("COPY_ONLY", new ParameterInfo(false, false));
            mBackupParamSchema.Add("CHECKSUM", new ParameterInfo(false, false));
            mBackupParamSchema.Add("NO_CHECKSUM", new ParameterInfo(false, false));
            mBackupParamSchema.Add("STOP_ON_ERROR", new ParameterInfo(false, false));
            mBackupParamSchema.Add("CONTINUE_AFTER_ERROR", new ParameterInfo(false, false));
            mBackupParamSchema.Add("BUFFERCOUNT", new ParameterInfo(false, false));
            mBackupParamSchema.Add("MAXTRANSFERSIZE", new ParameterInfo(false, false));
            mBackupParamSchema.Add("BLOCKSIZE", new ParameterInfo(false, false));
            //TODO: enable switch to turn stats reporting on or off
            //mBackupParamSchema.Add("STATS", new ParameterInfo(false, false));

            mRestoreParamSchema = new Dictionary<string, ParameterInfo>(StringComparer.InvariantCultureIgnoreCase);
            mRestoreParamSchema.Add("database", new ParameterInfo(false, true));
            mRestoreParamSchema.Add("instancename", new ParameterInfo(false, false));
            mRestoreParamSchema.Add("ClusterNetworkName", new ParameterInfo(false, false));
            mRestoreParamSchema.Add("restoretype", new ParameterInfo(false, false));
            mRestoreParamSchema.Add("user", new ParameterInfo(false, false));
            mRestoreParamSchema.Add("password", new ParameterInfo(false, false));
            mRestoreParamSchema.Add("CHECKSUM", new ParameterInfo(false, false));
            mRestoreParamSchema.Add("NO_CHECKSUM", new ParameterInfo(false, false));
            mRestoreParamSchema.Add("STOP_ON_ERROR", new ParameterInfo(false, false));
            mRestoreParamSchema.Add("CONTINUE_AFTER_ERROR", new ParameterInfo(false, false));
            mRestoreParamSchema.Add("KEEP_REPLICATION", new ParameterInfo(false, false));
            mRestoreParamSchema.Add("ENABLE_BROKER", new ParameterInfo(false, false));
            mRestoreParamSchema.Add("ERROR_BROKER_CONVERSATIONS", new ParameterInfo(false, false));
            mRestoreParamSchema.Add("NEW_BROKER", new ParameterInfo(false, false));
            mRestoreParamSchema.Add("RECOVERY", new ParameterInfo(false, false));
            mRestoreParamSchema.Add("NORECOVERY", new ParameterInfo(false, false));
            mRestoreParamSchema.Add("STANDBY", new ParameterInfo(false, false));
            mRestoreParamSchema.Add("REPLACE", new ParameterInfo(false, false));
            mRestoreParamSchema.Add("RESTART", new ParameterInfo(false, false));
            mRestoreParamSchema.Add("RESTRICTED_USER", new ParameterInfo(false, false));
            mRestoreParamSchema.Add("STOPAT", new ParameterInfo(false, false));
            mRestoreParamSchema.Add("STOPATMARK", new ParameterInfo(false, false));
            mRestoreParamSchema.Add("STOPBEFOREMARK", new ParameterInfo(false, false));
            mRestoreParamSchema.Add("PARTIAL", new ParameterInfo(false, false));
            mRestoreParamSchema.Add("READ_WRITE_FILEGROUPS", new ParameterInfo(false, false));
            mRestoreParamSchema.Add("FILE", new ParameterInfo(true, false));
            mRestoreParamSchema.Add("FILEGROUP", new ParameterInfo(true, false));
            mRestoreParamSchema.Add("LOADHISTORY", new ParameterInfo(false, false));
            mRestoreParamSchema.Add("MOVE", new ParameterInfo(true, false));
            mRestoreParamSchema.Add("BUFFERCOUNT", new ParameterInfo(false, false));
            mRestoreParamSchema.Add("MAXTRANSFERSIZE", new ParameterInfo(false, false));

            mVerifyParamSchema = new Dictionary<string, ParameterInfo>(StringComparer.InvariantCultureIgnoreCase);
            mVerifyParamSchema.Add("database", new ParameterInfo(false, true));
            mVerifyParamSchema.Add("instancename", new ParameterInfo(false, false));
            mVerifyParamSchema.Add("ClusterNetworkName", new ParameterInfo(false, false));
            mVerifyParamSchema.Add("restoretype", new ParameterInfo(false, false));
            mVerifyParamSchema.Add("user", new ParameterInfo(false, false));
            mVerifyParamSchema.Add("password", new ParameterInfo(false, false));
            mVerifyParamSchema.Add("CHECKSUM", new ParameterInfo(false, false));
            mVerifyParamSchema.Add("NO_CHECKSUM", new ParameterInfo(false, false));
            mVerifyParamSchema.Add("STOP_ON_ERROR", new ParameterInfo(false, false));
            mVerifyParamSchema.Add("CONTINUE_AFTER_ERROR", new ParameterInfo(false, false));
            mVerifyParamSchema.Add("LOADHISTORY", new ParameterInfo(false, false));
        }

        #region IBackupDatabase Members

        public string Name
        {
            get { return "db"; }
        }

        public void ConfigureBackupCommand(Dictionary<string, List<string>> config, IEnumerable<string> deviceNames, SqlCommand cmd)
        {
            ParameterInfo.ValidateParams(mBackupParamSchema, config);

            SqlParameter param;
            param = new SqlParameter("@databasename", SqlDbType.NVarChar, 255);
            param.Value = config["database"][0];
            cmd.Parameters.Add(param);

            // default values:
            BackupType backupType = BackupType.Full;

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

            List<string> withOptions = new List<string>();
            List<string> filegroupOptions = new List<string>();

            if (config.ContainsKey("READ_WRITE_FILEGROUPS"))
            {
                filegroupOptions.Add("READ_WRITE_FILEGROUPS");
            }

            if (config.ContainsKey("FILE"))
            {
                int i = 0;
                foreach (string file in config["FILE"])
                {
                    filegroupOptions.Add(string.Format("FILE=@file{0}", i));
                    param = new SqlParameter(string.Format("@file{0}", i), SqlDbType.NVarChar, 2000);
                    param.Value = file;
                    cmd.Parameters.Add(param);

                    i++;
                }
            }

            if (config.ContainsKey("FILEGROUP"))
            {
                int i = 0;
                foreach (string filegroup in config["FILEGROUP"])
                {
                    filegroupOptions.Add(string.Format("FILEGROUP=@filegroup{0}", i));
                    param = new SqlParameter(string.Format("@filegroup{0}", i), SqlDbType.NVarChar, 2000);
                    param.Value = filegroup;
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
                List<string> valList = config["BUFFERCOUNT"];
                if (valList.Count != 1)
                {
                    throw new ArgumentException("BUFFERCOUNT parameter must have a value.");
                }
                string val = valList[0];
                int valInt;
                if (!int.TryParse(val, out valInt))
                {
                    throw new ArgumentException(string.Format("BUFFERCOUNT parameter was not a number: {0}", val));
                }
                withOptions.Add(string.Format("BUFFERCOUNT={0}", valInt));
            }
            //TODO: add validator for maxtransfersize other than is number
            if (config.ContainsKey("MAXTRANSFERSIZE"))
            {
                List<string> valList = config["MAXTRANSFERSIZE"];
                if (valList.Count != 1)
                {
                    throw new ArgumentException("MAXTRANSFERSIZE parameter must have a value.");
                }
                string val = valList[0];
                int valInt;
                if (!int.TryParse(val, out valInt))
                {
                    throw new ArgumentException(string.Format("MAXTRANSFERSIZE parameter was not a number: {0}", val));
                }
                withOptions.Add(string.Format("MAXTRANSFERSIZE={0}", valInt));
            }
            //TODO: add validator for blocksize other than is number
            if (config.ContainsKey("BLOCKSIZE"))
            {
                List<string> valList = config["BLOCKSIZE"];
                if (valList.Count != 1)
                {
                    throw new ArgumentException("BlockSize parameter must have a value.");
                }
                string val = valList[0];
                int valInt;
                if (!int.TryParse(val, out valInt))
                {
                    throw new ArgumentException(string.Format("BlockSize parameter was not a number: {0}", val));
                }
                withOptions.Add(string.Format("BlockSize={0}", valInt));
            }

            //TODO: add validator for STATS other than is number
            if (config.ContainsKey("STATS"))
            {
                List<string> valList = config["STATS"];
                if (valList.Count != 1)
                {
                    throw new ArgumentException("STATS parameter must have a value.");
                }
                string val = valList[0];
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
                for (int i = 0; i < filegroupOptions.Count; i++)
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
                for (int i = 0; i < withOptions.Count; i++)
                {
                    if (i > 0)
                    {
                        withClause += ",";
                    }
                    withClause += withOptions[i];
                }
            }

            string databaseOrLog = backupType == BackupType.Log ? "LOG" : "DATABASE";

            cmd.CommandType = CommandType.Text;

            List<string> tempDevs = new List<string>(deviceNames);
            List<string> devSql = tempDevs.ConvertAll<string>(delegate(string devName)
            {
                return string.Format("VIRTUAL_DEVICE='{0}'", devName);
            });

            cmd.CommandText = string.Format("BACKUP {0} @databasename {1}TO {2}{3};", databaseOrLog, filegroupClause, string.Join(",", devSql.ToArray()), withClause);
        }

        public void ConfigureRestoreCommand(Dictionary<string, List<string>> config, IEnumerable<string> deviceNames, SqlCommand cmd)
        {
            ParameterInfo.ValidateParams(mRestoreParamSchema, config);

            SqlParameter param;
            param = new SqlParameter("@databasename", SqlDbType.NVarChar, 255);
            param.Value = config["database"][0];
            cmd.Parameters.Add(param);

            // default values:
            RestoreType restoreType = RestoreType.Database;

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

            List<string> withOptions = new List<string>();
            List<string> filegroupOptions = new List<string>();

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
                string standbyFile = config["STANDBY"][0].Trim();
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
                param = new SqlParameter("@stopat", SqlDbType.DateTime);
                param.Value = stopAtDateTime;
                cmd.Parameters.Add(param);
            }

            if (config.ContainsKey("STOPATMARK"))
            {
                withOptions.Add("STOPATMARK=@stopatmark");
                param = new SqlParameter("@stopatmark", SqlDbType.VarChar);
                param.Value = config["STOPATMARK"][0];
                cmd.Parameters.Add(param);
            }

            if (config.ContainsKey("STOPBEFOREMARK"))
            {
                withOptions.Add("STOPBEFOREMARK=@stopbeforemark");
                param = new SqlParameter("@stopbeforemark", SqlDbType.VarChar);
                param.Value = config["STOPBEFOREMARK"][0];
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
                int i = 0;
                foreach (string file in config["FILE"])
                {
                    filegroupOptions.Add(string.Format("FILE=@file{0}", i));
                    param = new SqlParameter(string.Format("@file{0}", i), SqlDbType.NVarChar, 2000);
                    param.Value = file;
                    cmd.Parameters.Add(param);

                    i++;
                }
            }

            if (config.ContainsKey("FILEGROUP"))
            {
                int i = 0;
                foreach (string filegroup in config["FILEGROUP"])
                {
                    filegroupOptions.Add(string.Format("FILEGROUP=@filegroup{0}", i));
                    param = new SqlParameter(string.Format("@filegroup{0}", i), SqlDbType.NVarChar, 2000);
                    param.Value = filegroup;
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
                string moveClause = " ";
                int i = 0;
                foreach (string moveInfo in config["MOVE"])
                {
                    if (i > 0)
                    {
                        moveClause += ", ";
                    }

                    int quoteCount = 0;
                    foreach (char c in moveInfo)
                    {
                        if (c == '\'')
                        {
                            quoteCount++;
                        }
                    }
                    if (quoteCount != 4)
                    {
                        throw new ArgumentException(string.Format("db: Invalid MOVE clause: {0}.  Please write it in the form MOVE='from'TO'to'", moveInfo));
                    }

                    string[] moveSplit = moveInfo.Split('\'');
                    string moveFrom = moveSplit[1];
                    string moveToKeyword = moveSplit[2].Trim(); ;
                    string moveTo = moveSplit[3];

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
                List<string> valList = config["BUFFERCOUNT"];
                if (valList.Count != 1)
                {
                    throw new ArgumentException("BUFFERCOUNT parameter must have a value.");
                }
                string val = valList[0];
                int valInt;
                if (!int.TryParse(val, out valInt))
                {
                    throw new ArgumentException(string.Format("BUFFERCOUNT parameter was not a number: {0}", val));
                }
                withOptions.Add(string.Format("BUFFERCOUNT={0}", valInt));
            }

            if (config.ContainsKey("MAXTRANSFERSIZE"))
            {
                List<string> valList = config["MAXTRANSFERSIZE"];
                if (valList.Count != 1)
                {
                    throw new ArgumentException("MAXTRANSFERSIZE parameter must have a value.");
                }
                string val = valList[0];
                int valInt;
                if (!int.TryParse(val, out valInt))
                {
                    throw new ArgumentException(string.Format("MAXTRANSFERSIZE parameter was not a number: {0}", val));
                }
                withOptions.Add(string.Format("MAXTRANSFERSIZE={0}", valInt));
            }

            if (config.ContainsKey("STATS"))
            {
                List<string> valList = config["STATS"];
                if (valList.Count != 1)
                {
                    throw new ArgumentException("STATS parameter must have a value.");
                }
                string val = valList[0];
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
                for (int i = 0; i < filegroupOptions.Count; i++)
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
                for (int i = 0; i < withOptions.Count; i++)
                {
                    if (i > 0)
                    {
                        withClause += ",";
                    }
                    withClause += withOptions[i];
                }
            }

            string databaseOrLog = restoreType == RestoreType.Log ? "LOG" : "DATABASE";
            cmd.CommandType = CommandType.Text;
            List<string> tempDevs = new List<string>(deviceNames);
            List<string> devSql = tempDevs.ConvertAll<string>(delegate(string devName)
            {
                return string.Format("VIRTUAL_DEVICE='{0}'", devName);
            });

            cmd.CommandText = string.Format("RESTORE {0} @databasename {1}FROM {2}{3};", databaseOrLog, filegroupClause, string.Join(",", devSql.ToArray()), withClause);
        }

        public void ConfigureVerifyCommand(Dictionary<string, List<string>> config, IEnumerable<string> deviceNames, SqlCommand cmd)
        {
            ParameterInfo.ValidateParams(mVerifyParamSchema, config);

            SqlParameter param;
            param = new SqlParameter("@databasename", SqlDbType.NVarChar, 255);
            param.Value = config["database"][0];
            cmd.Parameters.Add(param);

            List<string> withOptions = new List<string>();
            List<string> filegroupOptions = new List<string>();

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
                for (int i = 0; i < withOptions.Count; i++)
                {
                    if (i > 0)
                    {
                        withClause += ",";
                    }
                    withClause += withOptions[i];
                }
            }

            string databaseOrLog = "VERIFYONLY";// restoreType == RestoreType.Log ? "LOG" : "DATABASE";
            cmd.CommandType = CommandType.Text;
            List<string> tempDevs = new List<string>(deviceNames);
            List<string> devSql = tempDevs.ConvertAll<string>(delegate(string devName)
            {
                return string.Format("VIRTUAL_DEVICE='{0}'", devName);
            });
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