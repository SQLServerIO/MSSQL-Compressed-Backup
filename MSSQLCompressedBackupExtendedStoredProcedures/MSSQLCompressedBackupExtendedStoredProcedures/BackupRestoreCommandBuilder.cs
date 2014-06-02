using System;
using System.Data;
using System.Data.SqlClient;
using System.Data.SqlTypes;
using Microsoft.SqlServer.Server;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Text;
using System.Collections.Generic;
using System.Security.Permissions;
using System.Runtime.Serialization.Formatters.Binary;
using System.IO;

namespace BackupRestoreCommandBuilder
{
    public class BackupRestoreParser
    {//help me!SS
        public Int32 _blocksize;
        public Int32 _buffercount;
        public Int32 _maxtransfersize;
        public Int16 _checksum;
        public Int16 _no_checksum;
        public Int16 _continue_after_error;
        public Int16 _stop_on_error;
        public Int16 _stats;
        public Int16 _read_write_filegroups;
        public string _description;
        public string _name;
        public string _mediadescription;
        public string _medianame;
        public string _databasename;
        public string _filegroupcmd;
        public string _username;
        public string _password;
        public string _clusternetworkname;
        public string _instancename;
        public string _encryption_key;

        //backup specific options
        public Int16 _backuptype;
        public string[] _backupdisks;
        public Int16 _compression;
        public Int16 _encryption;
        public Int16 _copy_only;

        //restore specific variables
        public Int16 _restoretype;
        public Int16 _recoverytype;
        public Int16 _partial;
        public string _page;
        public Int16 _replace;
        public Int16 _restart;
        public Int16 _restricted_user;
        public Int16 _keep_replicaton;
        public Int16 _keep_cdc;
        public Int16 _enable_broker;
        public Int16 _error_broker_conversations;
        public Int16 _new_broker;
        public string _standby_file;
        public string _stopat;
        public string _stopatmark;
        public string _stopbeforemark;
        public string _filestream;

        public List<string> _backupfiles = new List<string>();
        public List<string> _moveoptions = new List<string>();
        public List<string> _withoptions = new List<string>();
        public List<string> _filesoption = new List<string>();
        public List<string> _filegroupoption = new List<string>();

        public void ParseSQLBackupCommand(string sqlCommand)
        {
            _backuptype = 1;
            string text = Regex.Replace(sqlCommand, @"\s+", " ");

            string pattern = " DATABASE | LOG ";
            string[] words = Regex.Split(text, pattern, RegexOptions.IgnoreCase);

            pattern = " ";
            string[] words2 = Regex.Split(text, pattern, RegexOptions.IgnoreCase);

            _databasename = words2[2].Trim();

            string _dbcmd = text.Substring(text.IndexOf("BACKUP DATABASE", StringComparison.InvariantCultureIgnoreCase) + 15, text.IndexOf("TO DISK", StringComparison.InvariantCultureIgnoreCase) - text.IndexOf("BACKUP DATABASE", StringComparison.InvariantCultureIgnoreCase) - 15).Trim();
            _dbcmd = _dbcmd.Trim();
            if (!string.IsNullOrEmpty(_dbcmd))
            {
                if (_dbcmd.IndexOf(" ") > 0)
                {
                    _filegroupcmd = _dbcmd.Substring(_dbcmd.IndexOf(" ")).Trim();
                    string[] _filegroupcmds = _filegroupcmd.Split(',');

                    foreach (string s in _filegroupcmds)
                    {
                        string parse = " " + s.Trim() + " ";
                        if (parse.IndexOf("READ_WRITE_FILEGROUPS", StringComparison.InvariantCultureIgnoreCase) >= 0)
                        {
                            _read_write_filegroups = 1;
                        }
                        else if ((parse.IndexOf("FILE", StringComparison.InvariantCultureIgnoreCase) >= 0) && (parse.IndexOf("FILEGROUP", StringComparison.InvariantCultureIgnoreCase) == -1))
                        {
                            string[] name = parse.Split('=');
                            _filesoption.Add(name[1].Replace("=", "").Replace("'", "").Trim());
                        }
                        else if (parse.IndexOf("FILEGROUP", StringComparison.InvariantCultureIgnoreCase) >= 0)
                        {
                            string[] name = parse.Split('=');
                            _filegroupoption.Add(name[1].Replace("=", "").Replace("'", "").Trim());
                        }
                    }
                }
            }
            if (text.IndexOf("BACKUP LOG", StringComparison.InvariantCultureIgnoreCase) >= 0)
            {
                _backuptype = 2;
            }

            pattern = " WITH ";
            string[] words3 = Regex.Split(text, pattern, RegexOptions.IgnoreCase);
            string[] words4 = words3[1].Split(',');
            foreach (string s in words4)
            {
                string parse = " " + s.Trim() + " ";
                if (parse.IndexOf("differential", StringComparison.InvariantCultureIgnoreCase) >= 0)
                {
                    _backuptype = 3;
                }

                if (parse.IndexOf("copy_only", StringComparison.InvariantCultureIgnoreCase) >= 0)
                {
                    _copy_only = 1;
                }

                if (parse.IndexOf("encryption", StringComparison.InvariantCultureIgnoreCase) >= 0)
                {
                    string[] encryption_key = s.Split('=');
                    _encryption_key = encryption_key[1].Trim();
                    _encryption = 1;
                }

                if (parse.IndexOf("compression", StringComparison.InvariantCultureIgnoreCase) >= 0)
                {
                    _compression = 1;
                }

                if (parse.IndexOf("no_checksum", StringComparison.InvariantCultureIgnoreCase) >= 0)
                {
                    _no_checksum = 1;
                }
                else if (parse.IndexOf("checksum", StringComparison.InvariantCultureIgnoreCase) >= 0)
                {
                    _checksum = 1;
                }

                if (parse.IndexOf("continue_after_error", StringComparison.InvariantCultureIgnoreCase) >= 0)
                {
                    _continue_after_error = 1;
                }
                if (parse.IndexOf("blocksize", StringComparison.InvariantCultureIgnoreCase) >= 0)
                {
                    string[] blksz = s.Split('=');
                    _blocksize = Convert.ToInt32(blksz[1].Trim());
                }
                if (parse.IndexOf("buffercount", StringComparison.InvariantCultureIgnoreCase) >= 0)
                {
                    string[] buffcnt = s.Split('=');
                    _buffercount = Convert.ToInt32(buffcnt[1].Trim());
                }
                if (parse.IndexOf("maxtransfersize", StringComparison.InvariantCultureIgnoreCase) >= 0)
                {
                    string[] maxtrans = s.Split('=');
                    _maxtransfersize = Convert.ToInt32(maxtrans[1].Trim());
                }
                if (parse.IndexOf("stats", StringComparison.InvariantCultureIgnoreCase) >= 0)
                {
                    string[] stats = s.Split('=');
                    _stats = Convert.ToInt16(stats[1].Trim());
                }

                if (parse.IndexOf("mediadescription", StringComparison.InvariantCultureIgnoreCase) >= 0)
                {
                    string[] mediadescription = s.Split('=');
                    _mediadescription = mediadescription[1].Trim();
                }
                else if (parse.IndexOf("description", StringComparison.InvariantCultureIgnoreCase) >= 0)
                {
                    string[] desc = s.Split('=');
                    _description = desc[1].Trim();
                }

                if (parse.IndexOf("username", StringComparison.InvariantCultureIgnoreCase) >= 0)
                {
                    string[] name = s.Split('=');
                    _username = name[1].Trim();
                }

                if (parse.IndexOf("medianame", StringComparison.InvariantCultureIgnoreCase) >= 0)
                {
                    string[] medianame = s.Split('=');
                    _medianame = medianame[1].Trim();
                }

                if (parse.IndexOf("instancename", StringComparison.InvariantCultureIgnoreCase) >= 0)
                {
                    string[] name = s.Split('=');
                    _instancename = name[1].Trim();
                }

                if (parse.IndexOf("clusternetworkname", StringComparison.InvariantCultureIgnoreCase) >= 0)
                {
                    string[] name = s.Split('=');
                    _clusternetworkname = name[1].Trim();
                }

                if (
                    (parse.IndexOf("instancename", StringComparison.InvariantCultureIgnoreCase) == -1) &&
                    (parse.IndexOf("medianame", StringComparison.InvariantCultureIgnoreCase) == -1) &&
                    (parse.IndexOf("username", StringComparison.InvariantCultureIgnoreCase) == -1) &&
                    (parse.IndexOf("clusternetworkname", StringComparison.InvariantCultureIgnoreCase) == -1) &&
                    parse.IndexOf("name", StringComparison.InvariantCultureIgnoreCase) >= 0)
                {
                    string[] name = s.Split('=');
                    _name = name[1].Trim();
                }

                if (parse.IndexOf("password", StringComparison.InvariantCultureIgnoreCase) >= 0)
                {
                    string[] name = s.Split('=');
                    _password = name[1].Trim();
                }
                if (parse.IndexOf("stop_on_error", StringComparison.InvariantCultureIgnoreCase) >= 0)
                {
                    _stop_on_error = 1;
                }
            }
            //we ignore MIRROR TO DISK for now
            //we also will only use one TO DISK statement for the backup command
            if (text.IndexOf("MIRROR TO DISK", StringComparison.InvariantCultureIgnoreCase) >= 0)
            {
                _backupdisks = text.Substring(text.IndexOf("TO DISK", StringComparison.InvariantCultureIgnoreCase) + 3, text.IndexOf("MIRROR TO DISK", StringComparison.InvariantCultureIgnoreCase) - text.IndexOf("TO DISK", StringComparison.InvariantCultureIgnoreCase) - 3).Split(',');
            }
            else
            {
                _backupdisks = text.Substring(text.IndexOf("TO DISK", StringComparison.InvariantCultureIgnoreCase) + 3, text.IndexOf(" WITH ", StringComparison.InvariantCultureIgnoreCase) - text.IndexOf("TO DISK", StringComparison.InvariantCultureIgnoreCase) - 3).Split(',');
            }

            foreach (string s in _backupdisks)
            {
                _backupfiles.Add(s.Replace("=", "").ToLower().Replace("disk", "").Replace("'", "").Trim());
            }
        }

        public void ParseSQLRestoreCommand(string sqlCommand)
        {
            _restoretype = 1;
            _recoverytype = 0;
            _backupfiles.Clear();
            _moveoptions.Clear();
            _withoptions.Clear();
            _filegroupoption.Clear();

            string text = Regex.Replace(sqlCommand, @"\s+", " ");

            if ((text.IndexOf("RESTORE FILELISTONLY", StringComparison.InvariantCultureIgnoreCase) >= 0) || (text.IndexOf("RESTORE HEADERONLY", StringComparison.InvariantCultureIgnoreCase) >= 0))
            {
                _backupdisks = text.Substring(text.IndexOf("FROM DISK", StringComparison.InvariantCultureIgnoreCase)).Split(',');

                foreach (string s in _backupdisks)
                {
                    _backupfiles.Add(Regex.Replace(s, "([Ff][Rr][Oo][Mm])", "").Replace("=", "").ToLower().Replace("disk", "").Replace("'", "").Trim());
                }
            }
            else
            {
                string pattern = " DATABASE | LOG ";
                string[] words = Regex.Split(text, pattern, RegexOptions.IgnoreCase);

                pattern = " ";
                string[] words2 = Regex.Split(text, pattern, RegexOptions.IgnoreCase);

                if (words2.Length > 3)
                {
                    _databasename = words2[2].Trim();
                }
                else
                {
                    _databasename = words2[1].Trim();
                }

                string _dbcmd = text.Substring(text.IndexOf("RESTORE DATABASE", StringComparison.InvariantCultureIgnoreCase) + 15, text.IndexOf("FROM DISK", StringComparison.InvariantCultureIgnoreCase) - text.IndexOf("RESTORE DATABASE", StringComparison.InvariantCultureIgnoreCase) - 15).Trim();
                _dbcmd = _dbcmd.Trim();

                if (!string.IsNullOrEmpty(_dbcmd))
                {
                    if (_dbcmd.IndexOf(" ") > 0)
                    {
                        _filegroupcmd = _dbcmd.Substring(_dbcmd.IndexOf(" ")).Trim();
                        string[] _filegroupcmds = _filegroupcmd.Split(',');

                        foreach (string s in _filegroupcmds)
                        {
                            string parse = " " + s.Trim() + " ";
                            if (parse.IndexOf("READ_WRITE_FILEGROUPS", StringComparison.InvariantCultureIgnoreCase) >= 0)
                            {
                                _read_write_filegroups = 1;
                            }
                            else if ((parse.IndexOf("FILE", StringComparison.InvariantCultureIgnoreCase) >= 0) && (parse.IndexOf("FILEGROUP", StringComparison.InvariantCultureIgnoreCase) == -1))
                            {
                                string[] name = parse.Split('=');
                                _filesoption.Add(name[1].Replace("=", "").Replace("'", "").Trim());
                            }
                            else if (parse.IndexOf("FILEGROUP", StringComparison.InvariantCultureIgnoreCase) >= 0)
                            {
                                string[] name = parse.Split('=');
                                _filegroupoption.Add(name[1].Replace("=", "").Replace("'", "").Trim());
                            }
                        }
                    }
                }

                if (text.IndexOf("RESTORE LOG", StringComparison.InvariantCultureIgnoreCase) >= 0)
                {
                    _restoretype = 2;
                }
                pattern = " WITH ";

                string[] words3 = Regex.Split(text, pattern, RegexOptions.IgnoreCase);
                string[] words4 = words3[1].Split(',');
                foreach (string s in words4)
                {
                    string parse = " " + s.Trim() + " ";
                    if (parse.IndexOf("checksum", StringComparison.InvariantCultureIgnoreCase) >= 0)
                    {
                        _checksum = 1;
                    }
                    else if (parse.IndexOf("no_checksum", StringComparison.InvariantCultureIgnoreCase) >= 0)
                    {
                        _no_checksum = 1;
                    }

                    if (parse.IndexOf("continue_after_error", StringComparison.InvariantCultureIgnoreCase) >= 0)
                    {
                        _continue_after_error = 1;
                    }

                    if (parse.IndexOf("blocksize", StringComparison.InvariantCultureIgnoreCase) >= 0)
                    {
                        string[] blksz = s.Split('=');
                        _blocksize = Convert.ToInt32(blksz[1].Trim());
                    }

                    if (parse.IndexOf("buffercount", StringComparison.InvariantCultureIgnoreCase) >= 0)
                    {
                        string[] buffcnt = s.Split('=');
                        _buffercount = Convert.ToInt32(buffcnt[1].Trim());
                    }

                    if (parse.IndexOf("maxtransfersize", StringComparison.InvariantCultureIgnoreCase) >= 0)
                    {
                        string[] maxtrans = s.Split('=');
                        _maxtransfersize = Convert.ToInt32(maxtrans[1].Trim());
                    }

                    if (parse.IndexOf("encryption", StringComparison.InvariantCultureIgnoreCase) >= 0)
                    {
                        _encryption = 1;
                        string[] encryption_key = s.Split('=');
                        _encryption_key = encryption_key[1].Trim();
                    }

                    if (parse.IndexOf("compression", StringComparison.InvariantCultureIgnoreCase) >= 0)
                    {
                        _compression = 1;
                    }

                    if (parse.IndexOf("stats", StringComparison.InvariantCultureIgnoreCase) >= 0)
                    {
                        string[] stats = s.Split('=');
                        _stats = Convert.ToInt16(stats[1].Trim());
                    }

                    if (parse.IndexOf("username", StringComparison.InvariantCultureIgnoreCase) >= 0)
                    {
                        string[] name = s.Split('=');
                        _username = name[1].Trim();
                    }

                    if (parse.IndexOf("medianame", StringComparison.InvariantCultureIgnoreCase) >= 0)
                    {
                        string[] medianame = s.Split('=');
                        _medianame = medianame[1].Trim();
                    }

                    if (parse.IndexOf("instancename", StringComparison.InvariantCultureIgnoreCase) >= 0)
                    {
                        string[] name = s.Split('=');
                        _instancename = name[1].Trim();
                    }

                    if (parse.IndexOf("clusternetworkname", StringComparison.InvariantCultureIgnoreCase) >= 0)
                    {
                        string[] name = s.Split('=');
                        _clusternetworkname = name[1].Trim();
                    }

                    if (
                        (parse.IndexOf("instancename", StringComparison.InvariantCultureIgnoreCase) == -1) &&
                        (parse.IndexOf("medianame", StringComparison.InvariantCultureIgnoreCase) == -1) &&
                        (parse.IndexOf("username", StringComparison.InvariantCultureIgnoreCase) == -1) &&
                        (parse.IndexOf("clusternetworkname", StringComparison.InvariantCultureIgnoreCase) == -1) &&
                        parse.IndexOf("name", StringComparison.InvariantCultureIgnoreCase) >= 0)
                    {
                        string[] name = s.Split('=');
                        _name = name[1].Trim();
                    }

                    if (parse.IndexOf("NORECOVERY", StringComparison.InvariantCultureIgnoreCase) >= 0)
                    {
                        _recoverytype = 2;
                    }
                    else if (parse.IndexOf("RECOVERY", StringComparison.InvariantCultureIgnoreCase) >= 0)
                    {
                        _recoverytype = 1;
                    }

                    if (parse.IndexOf("STANDBY", StringComparison.InvariantCultureIgnoreCase) >= 0)
                    {
                        _recoverytype = 3;
                        string[] name = s.Split('=');
                        _standby_file = name[1].Trim();
                    }

                    if (parse.IndexOf("partial", StringComparison.InvariantCultureIgnoreCase) >= 0)
                    {
                        _partial = 1;
                    }

                    if (parse.IndexOf("replace", StringComparison.InvariantCultureIgnoreCase) >= 0)
                    {
                        _replace = 1;
                    }

                    if (parse.IndexOf("restricted_user", StringComparison.InvariantCultureIgnoreCase) >= 0)
                    {
                        _restricted_user = 1;
                    }

                    if (parse.IndexOf("keep_replicaton", StringComparison.InvariantCultureIgnoreCase) >= 0)
                    {
                        _keep_replicaton = 1;
                    }

                    else if (parse.IndexOf("keep_cdc", StringComparison.InvariantCultureIgnoreCase) >= 0)
                    {
                        _keep_cdc = 1;
                    }

                    if (parse.IndexOf("enable_broker", StringComparison.InvariantCultureIgnoreCase) >= 0)
                    {
                        _enable_broker = 1;
                    }

                    if (parse.IndexOf("error_broker_conversations", StringComparison.InvariantCultureIgnoreCase) >= 0)
                    {
                        _error_broker_conversations = 1;
                    }

                    if (parse.IndexOf("new_broker", StringComparison.InvariantCultureIgnoreCase) >= 0)
                    {
                        _new_broker = 1;
                    }

                    if (parse.IndexOf("restart", StringComparison.InvariantCultureIgnoreCase) >= 0)
                    {
                        _restart = 1;
                    }

                    if (parse.IndexOf("stop_on_error", StringComparison.InvariantCultureIgnoreCase) >= 0)
                    {
                        _stop_on_error = 1;
                    }
                    else if (parse.IndexOf("STOPATMARK", StringComparison.InvariantCultureIgnoreCase) >= 0)
                    {
                        string[] name = parse.Split('=');
                        _stopatmark = name[1].Trim();
                    }
                    else if (parse.IndexOf("STOPBEFOREMARK", StringComparison.InvariantCultureIgnoreCase) >= 0)
                    {
                        string[] name = parse.Split('=');
                        _stopbeforemark = name[1].Trim();
                    }
                    else if (parse.IndexOf("STOPAT", StringComparison.InvariantCultureIgnoreCase) >= 0)
                    {
                        string[] name = parse.Split('=');
                        _stopat = name[1].Trim();
                    }

                    if (parse.IndexOf("PAGE", StringComparison.InvariantCultureIgnoreCase) >= 0)
                    {
                        string[] name = parse.Split('=');
                        _page = name[1].Trim();
                    }

                    if (parse.IndexOf("password", StringComparison.InvariantCultureIgnoreCase) >= 0)
                    {
                        string[] name = s.Split('=');
                        _password = name[1].Trim();
                    }

                    if (parse.IndexOf("move", StringComparison.InvariantCultureIgnoreCase) >= 0)
                    {
                        pattern = " to ";
                        string[] movesplit = Regex.Split(parse.Trim().Remove(0, 4).Trim(), pattern, RegexOptions.IgnoreCase);
                        _moveoptions.Add(movesplit[0].Trim() + "TO" + movesplit[1].Trim());
                    }
                    else if (parse.IndexOf("FILESTREAM", StringComparison.InvariantCultureIgnoreCase) >= 0)
                    {
                        string[] name = parse.Split('=');
                        _filestream = name[1].Trim();
                    }
                    else if (parse.IndexOf("READ_WRITE_FILEGROUPS", StringComparison.InvariantCultureIgnoreCase) >= 0)
                    {
                        _read_write_filegroups = 1;
                    }
                    else if ((parse.IndexOf("FILE", StringComparison.InvariantCultureIgnoreCase) >= 0) && (parse.IndexOf("FILEGROUP", StringComparison.InvariantCultureIgnoreCase) == -1))
                    {
                        string[] name = parse.Split(',');
                        foreach (string fn in name)
                        {
                            _filesoption.Add(fn.Replace("=", "").ToLower().Replace("file", "").Replace("'", "").Trim());
                        }

                    }
                    else if (parse.IndexOf("FILEGROUP", StringComparison.InvariantCultureIgnoreCase) >= 0)
                    {
                        string[] name = parse.Split(',');
                        foreach (string fg in _backupdisks)
                        {
                            _filegroupoption.Add(fg.Replace("=", "").ToLower().Replace("disk", "").Replace("'", "").Trim());
                        }
                    }
                }
                //we ignore DATABASE_SNAPSHOT for now
                //we also will only use one FROM DISK statement for the backup command
                if (text.IndexOf("DATABASE_SNAPSHOT", StringComparison.InvariantCultureIgnoreCase) == -1)
                {
                    _backupdisks = text.Substring(text.IndexOf("FROM DISK", StringComparison.InvariantCultureIgnoreCase) + 4, text.IndexOf(" WITH ", StringComparison.InvariantCultureIgnoreCase) - text.IndexOf("FROM DISK", StringComparison.InvariantCultureIgnoreCase) - 3).Split(',');
                }

                foreach (string s in _backupdisks)
                {
                    _backupfiles.Add(Regex.Replace(s, "([Ff][Rr][Oo][Mm])", "").Replace("=", "").ToLower().Replace("disk", "").Replace("'", "").Trim());
                }
            }
        }
    }

    public class ExecuteBackupRestore
    {
        StringBuilder output;
        public void BuildAndExecuteStatement(string filename, string arguments)
        {
            BackupRestoreParser bpr = new BackupRestoreParser();
            if (string.IsNullOrEmpty(arguments) || string.IsNullOrEmpty(filename))
            {
                throw new Exception("You must specify the path to msbp.exe AND pass in a valid backup command.");
            }
            if (filename.Contains("exe"))
            {
                throw new Exception(filename + " You cannot specifiy the executable name.");
            }

            StringBuilder msbp_command = new StringBuilder();
            msbp_command.Capacity = 0;

            bool execStatus = false;
            if (!string.IsNullOrEmpty(arguments))
            {
                if ((arguments.IndexOf("backup database", StringComparison.InvariantCultureIgnoreCase) >= 0) || (arguments.IndexOf("backup log", StringComparison.InvariantCultureIgnoreCase) >= 0))
                {
                    msbp_command = BuildBackupCommand(arguments);
                    execStatus = true;
                }
                else if (arguments.IndexOf("headeronly", StringComparison.InvariantCultureIgnoreCase) >= 0)
                {
                    ReturnHeaderOnly(arguments);
                }
                else if (arguments.IndexOf("filelistonly", StringComparison.InvariantCultureIgnoreCase) >= 0)
                {
                    ReturnFileListOnly(arguments);
                }
                else if ((arguments.IndexOf("restore database", StringComparison.InvariantCultureIgnoreCase) >= 0) || (arguments.IndexOf("restore log", StringComparison.InvariantCultureIgnoreCase) >= 0))
                {
                    msbp_command = BuildRestoreCommand(arguments);
                    execStatus = true;
                }
                else if ((arguments.IndexOf("restore verifyonly", StringComparison.InvariantCultureIgnoreCase) >= 0))
                {
                    msbp_command = BuildRestoreVerifyCommand(arguments);
                    execStatus = true;
                }
                else
                {
                    throw new Exception("No valid backup or restore command given.");
                }
            }

            if (execStatus)
            {
                string errorMessage = string.Empty;
                output = new StringBuilder();
                SqlPipe outPipe = SqlContext.Pipe;
                using (Process prsProjectTypes = new Process())
                {
                    try
                    {
                        string msbp_path = filename.EndsWith(@"\") ? filename : filename + @"\";
                        msbp_path += "msbp.exe";

                        prsProjectTypes.EnableRaisingEvents = false;
                        prsProjectTypes.StartInfo.UseShellExecute = false;
                        prsProjectTypes.StartInfo.RedirectStandardOutput = true;
                        prsProjectTypes.StartInfo.RedirectStandardError = true;
                        prsProjectTypes.StartInfo.CreateNoWindow = true;
                        prsProjectTypes.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
                        prsProjectTypes.StartInfo.FileName = msbp_path;
                        prsProjectTypes.StartInfo.Arguments = msbp_command.ToString();
                    }
                    catch (Exception e)
                    {
                        throw new Exception("error while building executor " + filename + " " + arguments + ": " + e.Message, e);
                    }

                    try
                    {
                        prsProjectTypes.Start();
                    }
                    catch (Exception e)
                    {
                        outPipe.Send(msbp_command.ToString());
                        throw new Exception("error while executing " + filename + " " + arguments + ": " + e.Message, e);
                    }

                    // Handle Standard Output
                    prsProjectTypes.OutputDataReceived += new DataReceivedEventHandler(prsProjectTypes_OutputDataReceived);
                    // Handle Error Output
                    prsProjectTypes.ErrorDataReceived += new DataReceivedEventHandler(prsProjectTypes_OutputDataReceived);
                    prsProjectTypes.BeginOutputReadLine();

                    //hang out here and wait for completion.
                    prsProjectTypes.WaitForExit();

                    if (prsProjectTypes.ExitCode != 0)
                    {
                        outPipe.Send(msbp_command.ToString());
                        errorMessage = filename + " finished with exit code = " + prsProjectTypes.ExitCode + ": " + arguments;
                        errorMessage += "\n";
                        errorMessage += output.ToString();
                    }
                    else
                    {
                        errorMessage = string.Empty;
                        int strlensplit = 4000;
                        int strlensplithold = 4000;
                        int strhead = 0;
                        List<String> finaloutput = new List<String>();
                        string charhold = "";
                        char charsplit = '\n';
                        bool nosplitter = false;
                        outPipe.Send(msbp_command.ToString());

                        if (output.Length > strlensplit)
                        {
                            string outhold = output.ToString();
                            if ((outhold.IndexOf('\n') == -1) | (outhold.IndexOf('\n') > strlensplit))
                            {
                                if ((outhold.IndexOf(' ') == -1) | (outhold.IndexOf(' ') > strlensplit))
                                {
                                    nosplitter = true;
                                }
                                else
                                {
                                    charsplit = ' ';
                                }
                            }
                            if (nosplitter)
                            {
                                while ((strlensplit < outhold.Length) && (outhold.Length - strlensplit >= strlensplithold) && strlensplit >= 0)
                                {
                                    charhold = new string(outhold.ToCharArray(strlensplit, 1));
                                    if (charhold.IndexOf(charsplit) == -1)
                                    {
                                        strlensplit--;
                                    }
                                    else
                                    {
                                        outPipe.Send(outhold.Substring(strhead, strlensplit - strhead));
                                        strhead = strlensplit;
                                        strlensplit += strlensplithold;
                                        charhold = new string(outhold.ToCharArray(strlensplit, 1));
                                    }
                                }
                            }
                            else
                            {
                                int chunkSize = 4000;
                                int stringLength = outhold.Length;
                                for (int i = 0; i < stringLength; i += chunkSize)
                                {
                                    if (i + 4000 > stringLength) chunkSize = stringLength - i;
                                    outPipe.Send(outhold.Substring(i, chunkSize));

                                }
                            }
                        }
                        else
                        {
                            outPipe.Send(output.ToString());
                        }
                    }

                    prsProjectTypes.Close();
                    prsProjectTypes.Dispose();
                }

                if (!string.IsNullOrEmpty(errorMessage))
                {
                    throw new Exception(errorMessage);
                }
                outPipe = null;
            }
            msbp_command = null;
        }

        private void ReturnHeaderOnly(string _sqlcommand)
        {
            BinaryFormatter bFormat = new BinaryFormatter();
            FileStream fs;
            String metaDataPath = "";
            BackupRestoreParser brp = new BackupRestoreParser();
            try
            {
                brp.ParseSQLRestoreCommand(_sqlcommand);
            }
            catch (Exception e)
            {
                fs = null;
                bFormat = null;
                throw new Exception(_sqlcommand + " cannot parse this command Error: " + e.Message);
            }

            try
            {
                metaDataPath = brp._backupfiles[0];
            }
            catch (Exception e)
            {
                fs = null;
                bFormat = null;
                throw new Exception(_sqlcommand + " does not contain valid meta data. Error: " + e.Message);
            }

            try
            {
                fs = new FileStream(metaDataPath, FileMode.Open);
            }
            catch (Exception te)
            {
                fs = null;
                bFormat = null;
                throw new Exception(metaDataPath + " failed to open. Error: " + te.Message);
            }

            DataSet HeaderOnlyData = new DataSet();
            try
            {
                HeaderOnlyData = (DataSet)bFormat.Deserialize(fs);
            }
            catch (Exception e)
            {
                if (metaDataPath.IndexOf(".hfl", StringComparison.InvariantCultureIgnoreCase) == -1)
                {
                    try
                    {
                        fs.Close();
                        fs.Dispose();
                        metaDataPath += ".hfl";
                        fs = new FileStream(metaDataPath, FileMode.Open);
                        HeaderOnlyData = (DataSet)bFormat.Deserialize(fs);
                    }
                    catch (Exception ef)
                    {
                        fs.Close();
                        fs.Dispose();
                        HeaderOnlyData.Dispose();
                        fs = null;
                        HeaderOnlyData = null;
                        bFormat = null;
                        throw new Exception(metaDataPath + " does not contain a valid meta data file for msbp. Error: " + ef.Message);
                    }
                }
                else
                {
                    fs.Close();
                    fs.Dispose();
                    HeaderOnlyData.Dispose();
                    fs = null;
                    HeaderOnlyData = null;
                    bFormat = null;
                    throw new Exception(metaDataPath + " cannot be opened or does not contain a valid meta data file for msbp. Error: " + e.Message);
                }
            }

            try
            {
                SendDataTable(HeaderOnlyData.Tables[0]);
            }
            catch (Exception e)
            {
                fs.Close();
                fs.Dispose();
                HeaderOnlyData.Dispose();
                fs = null;
                HeaderOnlyData = null;
                bFormat = null;
                throw new Exception(metaDataPath + " failed to read data table. Error: " + e.Message);
            }

            fs.Close();
            fs.Dispose();
            HeaderOnlyData.Dispose();
            fs = null;
            HeaderOnlyData = null;
            metaDataPath = null;
            bFormat = null;
        }

        private void ReturnFileListOnly(string _sqlcommand)
        {
            BinaryFormatter bFormat = new BinaryFormatter();
            FileStream fs;
            String metaDataPath = "";
            BackupRestoreParser brp = new BackupRestoreParser();

            try
            {
                brp.ParseSQLRestoreCommand(_sqlcommand);
            }
            catch (Exception e)
            {
                brp = null;
                metaDataPath = null;
                bFormat = null;
                fs = null;
                throw new Exception(_sqlcommand + " cannot parse this command Error: " + e.Message);
            }
            try
            {
                metaDataPath = brp._backupfiles[0];
            }
            catch (Exception e)
            {
                brp = null;
                metaDataPath = null;
                bFormat = null;
                fs = null;
                throw new Exception(_sqlcommand + " does not contain valid meta data. Error: " + e.Message);
            }
            try
            {
                fs = new FileStream(metaDataPath, FileMode.Open);
            }
            catch (Exception te)
            {
                if (metaDataPath.IndexOf(".hfl", StringComparison.InvariantCultureIgnoreCase) >= 0)
                {
                    throw new Exception(_sqlcommand + " does not contain a valid meta data file for msbp. Error: " + te.Message);
                }
                try
                {
                    metaDataPath = ".hfl";
                    fs = new FileStream(metaDataPath, FileMode.Open);
                }
                catch (Exception e)
                {
                    brp = null;
                    metaDataPath = null;
                    bFormat = null;
                    fs = null;
                    throw new Exception(_sqlcommand + " does not contain a valid meta data file for msbp. Error: " + e.Message);
                }
            }

            DataSet HeaderOnlyData = new DataSet();
            try
            {

                HeaderOnlyData = (DataSet)bFormat.Deserialize(fs);
            }
            catch (Exception e)
            {
                brp = null;
                bFormat = null;
                fs = null;
                HeaderOnlyData = null;
                throw new Exception(metaDataPath + " cannot be opened. Error: " + e.Message);
            }

            try
            {
                SendDataTable(HeaderOnlyData.Tables[1]);
            }
            catch (Exception e)
            {
                fs.Close();
                fs.Dispose();
                HeaderOnlyData.Dispose();
                brp = null;
                bFormat = null;
                fs = null;
                HeaderOnlyData = null;
                throw new Exception(metaDataPath + " failed to read data table. Error: " + e.Message);
            }

            fs.Dispose();
            HeaderOnlyData.Dispose();
            fs = null;
            HeaderOnlyData = null;
            metaDataPath = null;
            bFormat = null;
        }

        public void SendDataTable(DataTable dt)
        {
            bool[] coerceToString;  // Do we need to coerce this column to string?
            SqlMetaData[] metaData = ExtractDataTableColumnMetaData(dt, out coerceToString);

            SqlDataRecord record = new SqlDataRecord(metaData);
            SqlPipe outPipe = SqlContext.Pipe;
            outPipe.SendResultsStart(record);
            try
            {
                foreach (DataRow row in dt.Rows)
                {
                    for (int index = 0; index < record.FieldCount; index++)
                    {
                        object value = row[index];
                        if (null != value && coerceToString[index])
                            value = value.ToString();
                        record.SetValue(index, value);
                    }

                    outPipe.SendResultsRow(record);
                }
            }
            finally
            {
                outPipe.SendResultsEnd();
                metaData = null;
                record = null;
                outPipe = null;
            }
        }

        private SqlMetaData[] ExtractDataTableColumnMetaData(DataTable dt, out bool[] coerceToString)
        {
            SqlMetaData[] metaDataResult = new SqlMetaData[dt.Columns.Count];
            coerceToString = new bool[dt.Columns.Count];
            for (int index = 0; index < dt.Columns.Count; index++)
            {
                DataColumn column = dt.Columns[index];
                metaDataResult[index] = SqlMetaDataFromColumn(column, out coerceToString[index]);
            }

            return metaDataResult;
        }

        private Exception InvalidDataTypeCode(TypeCode code)
        {
            return new ArgumentException("Invalid type: " + code);
        }

        private Exception UnknownDataType(Type clrType)
        {
            return new ArgumentException("Unknown type: " + clrType);
        }

        private SqlMetaData SqlMetaDataFromColumn(DataColumn column, out bool coerceToString)
        {
            coerceToString = false;
            SqlMetaData sql_md = null;
            Type clrType = column.DataType;
            string name = column.ColumnName;
            switch (Type.GetTypeCode(clrType))
            {
                case TypeCode.Boolean: sql_md = new SqlMetaData(name, SqlDbType.Bit); break;
                case TypeCode.Byte: sql_md = new SqlMetaData(name, SqlDbType.TinyInt); break;
                case TypeCode.Char: sql_md = new SqlMetaData(name, SqlDbType.NVarChar, 1); break;
                case TypeCode.DateTime: sql_md = new SqlMetaData(name, SqlDbType.DateTime); break;
                case TypeCode.DBNull: throw InvalidDataTypeCode(TypeCode.DBNull);
                //develop better way to detect this see Adam Mechanic's book 2008 development
                case TypeCode.Decimal: sql_md = new SqlMetaData(name, SqlDbType.Decimal, 25, 0); break;
                case TypeCode.Double: sql_md = new SqlMetaData(name, SqlDbType.Float); break;
                case TypeCode.Empty: throw InvalidDataTypeCode(TypeCode.Empty);
                case TypeCode.Int16: sql_md = new SqlMetaData(name, SqlDbType.SmallInt); break;
                case TypeCode.Int32: sql_md = new SqlMetaData(name, SqlDbType.Int); break;
                case TypeCode.Int64: sql_md = new SqlMetaData(name, SqlDbType.BigInt); break;
                case TypeCode.SByte: throw InvalidDataTypeCode(TypeCode.SByte);
                case TypeCode.Single: sql_md = new SqlMetaData(name, SqlDbType.Real); break;
                case TypeCode.String: sql_md = new SqlMetaData(name, SqlDbType.NVarChar, column.MaxLength == Int32.MaxValue ? -1 : column.MaxLength);
                    break;
                case TypeCode.UInt16: throw InvalidDataTypeCode(TypeCode.UInt16);
                case TypeCode.UInt32: throw InvalidDataTypeCode(TypeCode.UInt32);
                case TypeCode.UInt64: throw InvalidDataTypeCode(TypeCode.UInt64);
                case TypeCode.Object:
                    sql_md = SqlMetaDataFromObjectColumn(name, column, clrType);
                    if (sql_md == null)
                    {
                        // Unknown type, try to treat it as string;
                        sql_md = new SqlMetaData(name, SqlDbType.NVarChar, column.MaxLength);
                        coerceToString = true;
                    }
                    break;

                default: throw UnknownDataType(clrType);
            }

            return sql_md;
        }

        private SqlMetaData SqlMetaDataFromObjectColumn(string name, DataColumn column, Type clrType)
        {
            SqlMetaData sql_md = null;
            if (clrType == typeof(System.Byte[]) || clrType == typeof(SqlBinary) || clrType == typeof(SqlBytes) ||
        clrType == typeof(System.Char[]) || clrType == typeof(SqlString) || clrType == typeof(SqlChars))
                sql_md = new SqlMetaData(name, SqlDbType.VarBinary, column.MaxLength);
            else if (clrType == typeof(System.Guid))
                sql_md = new SqlMetaData(name, SqlDbType.UniqueIdentifier);
            else if (clrType == typeof(System.Object))
                sql_md = new SqlMetaData(name, SqlDbType.Variant);
            else if (clrType == typeof(SqlBoolean))
                sql_md = new SqlMetaData(name, SqlDbType.Bit);
            else if (clrType == typeof(SqlByte))
                sql_md = new SqlMetaData(name, SqlDbType.TinyInt);
            else if (clrType == typeof(SqlDateTime))
                sql_md = new SqlMetaData(name, SqlDbType.DateTime);
            else if (clrType == typeof(SqlDouble))
                sql_md = new SqlMetaData(name, SqlDbType.Float);
            else if (clrType == typeof(SqlGuid))
                sql_md = new SqlMetaData(name, SqlDbType.UniqueIdentifier);
            else if (clrType == typeof(SqlInt16))
                sql_md = new SqlMetaData(name, SqlDbType.SmallInt);
            else if (clrType == typeof(SqlInt32))
                sql_md = new SqlMetaData(name, SqlDbType.Int);
            else if (clrType == typeof(SqlInt64))
                sql_md = new SqlMetaData(name, SqlDbType.BigInt);
            else if (clrType == typeof(SqlMoney))
                sql_md = new SqlMetaData(name, SqlDbType.Money);
            else if (clrType == typeof(SqlDecimal))
                sql_md = new SqlMetaData(name, SqlDbType.Decimal, SqlDecimal.MaxPrecision, 0);
            else if (clrType == typeof(SqlSingle))
                sql_md = new SqlMetaData(name, SqlDbType.Real);
            else if (clrType == typeof(SqlXml))
                sql_md = new SqlMetaData(name, SqlDbType.Xml);
            else
                sql_md = null;

            return sql_md;
        }

        private StringBuilder BuildRestoreVerifyCommand(string _sqlcommand)
        {
            BackupRestoreParser brp = new BackupRestoreParser();
            brp.ParseSQLRestoreCommand(_sqlcommand);

            StringBuilder msbp_command = new StringBuilder();

            msbp_command.Append("restoreverifyonly \"local(");
            foreach (string disk in brp._backupfiles)
            {
                msbp_command.Append("path=");
                msbp_command.Append(disk);
                msbp_command.Append(";");
            }
            msbp_command.Append(")\"");
            msbp_command.Append(" ");

            if (brp._compression == 1)
            {
                msbp_command.Append("\"LZ4\"");
                msbp_command.Append(" ");
            }

            if (brp._encryption == 1)
            {
                msbp_command.Append("\"AES(key=90-1q@W#E$R%T^Y)\"");
                msbp_command.Append(" ");
            }

            msbp_command.Append("\"db(database=");
            msbp_command.Append(brp._databasename);

            if (!string.IsNullOrEmpty(brp._instancename))
            {
                msbp_command.Append(";instancename=");
                msbp_command.Append(brp._instancename);
            }

            if (!string.IsNullOrEmpty(brp._clusternetworkname))
            {
                msbp_command.Append(";clusternetworkname=");
                msbp_command.Append(brp._clusternetworkname);
            }

            foreach (string s in brp._moveoptions)
            {
                msbp_command.Append(";MOVE=");
                msbp_command.Append(s.Trim());
            }
            if (brp._checksum == 1)
                msbp_command.Append(";CHECKSUM");

            if (brp._no_checksum == 1)
                msbp_command.Append(";NO_CHECKSUM");

            if (brp._stop_on_error == 1)
                msbp_command.Append(";STOP_ON_ERROR");

            if (brp._continue_after_error == 1)
                msbp_command.Append(";CONTINUE_AFTER_ERROR");

            if (!string.IsNullOrEmpty(brp._username))
            {
                msbp_command.Append(";username=");
                msbp_command.Append(brp._username);
            }

            if (!string.IsNullOrEmpty(brp._password))
            {
                msbp_command.Append(";password=");
                msbp_command.Append(brp._password);
            }
            msbp_command.Append(")\"");
            msbp_command.Append(" ");

            return msbp_command;
        }

        private StringBuilder BuildRestoreCommand(string _sqlcommand)
        {
            BackupRestoreParser brp = new BackupRestoreParser();
            brp.ParseSQLRestoreCommand(_sqlcommand);

            StringBuilder msbp_command = new StringBuilder();

            msbp_command.Append("restore \"local(");
            foreach (string disk in brp._backupfiles)
            {
                msbp_command.Append("path=");
                msbp_command.Append(disk);
                msbp_command.Append(";");
            }
            msbp_command.Append(")\"");
            msbp_command.Append(" ");

            if (brp._compression == 1)
            {
                msbp_command.Append("\"LZ4\"");
                msbp_command.Append(" ");
            }

            if (brp._encryption == 1)
            {
                msbp_command.Append("\"AES(key=90-1q@W#E$R%T^Y)\"");
                msbp_command.Append(" ");
            }

            msbp_command.Append("\"db(database=");
            msbp_command.Append(brp._databasename);

            if (!string.IsNullOrEmpty(brp._instancename))
            {
                msbp_command.Append(";instancename=");
                msbp_command.Append(brp._instancename);
            }

            if (!string.IsNullOrEmpty(brp._clusternetworkname))
            {
                msbp_command.Append(";clusternetworkname=");
                msbp_command.Append(brp._clusternetworkname);
            }
            msbp_command.Append(";restoretype=");
            switch (brp._restoretype)
            {
                case 1:
                    msbp_command.Append("database");
                    break;
                case 2:
                    msbp_command.Append("log");
                    break;
            }

            foreach (string s in brp._moveoptions)
            {
                msbp_command.Append(";MOVE=");
                msbp_command.Append(s.Trim());
            }
            if (brp._checksum == 1)
                msbp_command.Append(";CHECKSUM");

            if (brp._no_checksum == 1)
                msbp_command.Append(";NO_CHECKSUM");

            if (brp._stop_on_error == 1)
                msbp_command.Append(";STOP_ON_ERROR");

            if (brp._continue_after_error == 1)
                msbp_command.Append(";CONTINUE_AFTER_ERROR");

            if (brp._read_write_filegroups == 1)
                msbp_command.Append(";READ_WRITE_FILEGROUPS");

            if (brp._keep_replicaton == 1)
                msbp_command.Append(";KEEP_REPLICATION");

            if (brp._enable_broker == 1)
                msbp_command.Append(";ENABLE_BROKER");

            if (brp._error_broker_conversations == 1)
                msbp_command.Append(";ERROR_BROKER_CONVERSATIONS");

            if (brp._new_broker == 1)
                msbp_command.Append(";NEW_BROKER");

            if (brp._buffercount > 0)
            {
                msbp_command.Append(";BUFFERCOUNT=");
                msbp_command.Append(brp._buffercount);
            }

            if (brp._maxtransfersize > 0)
            {
                msbp_command.Append(";MAXTRANSFERSIZE=");
                msbp_command.Append(brp._maxtransfersize);
            }

            if (brp._recoverytype == 2)
                msbp_command.Append(";NORECOVERY");

            if (brp._recoverytype == 1)
                msbp_command.Append(";RECOVERY");

            if (brp._replace == 1)
                msbp_command.Append(";REPLACE");

            if (brp._restart == 1)
                msbp_command.Append(";RESTART");

            if (brp._restricted_user == 1)
                msbp_command.Append(";RESTRICTED_USER");

            if (brp._partial == 1)
                msbp_command.Append(";PARTIAL");

            //LOADHISTORY
            //if (brp._loadhistory == 1)
            //    msbp_command.Append(";LOADHISTORY");

            foreach (string s in brp._filesoption)
            {
                msbp_command.Append(";FILE=");
                msbp_command.Append(s.Trim());
            }

            foreach (string s in brp._filegroupoption)
            {
                msbp_command.Append(";FILEGROUP=");
                msbp_command.Append(s.Trim());
            }

            if (!string.IsNullOrEmpty(brp._username))
            {
                msbp_command.Append(";username=");
                msbp_command.Append(brp._username);
            }

            if (!string.IsNullOrEmpty(brp._password))
            {
                msbp_command.Append(";password=");
                msbp_command.Append(brp._password);
            }

            if (!string.IsNullOrEmpty(brp._standby_file))
            {
                msbp_command.Append(";STANDBY=");
                msbp_command.Append(brp._standby_file);
            }

            if (!string.IsNullOrEmpty(brp._stopat))
            {
                msbp_command.Append(";STOPAT=");
                msbp_command.Append(brp._stopat);
            }

            msbp_command.Append(")\"");
            msbp_command.Append(" ");

            return msbp_command;
        }

        private StringBuilder BuildBackupCommand(string sqlcommand)
        {
            BackupRestoreParser brp = new BackupRestoreParser();
            brp.ParseSQLBackupCommand(sqlcommand);
            StringBuilder msbp_command = new StringBuilder();

            msbp_command.Append("backup \"db(database=");
            msbp_command.Append(brp._databasename);

            if (!string.IsNullOrEmpty(brp._instancename))
            {
                msbp_command.Append(";instancename=");
                msbp_command.Append(brp._instancename);
            }

            if (!string.IsNullOrEmpty(brp._clusternetworkname))
            {
                msbp_command.Append(";clusternetworkname=");
                msbp_command.Append(brp._clusternetworkname);
            }

            msbp_command.Append(";backuptype=");
            switch (brp._backuptype)
            {
                case 1:
                    msbp_command.Append("full");
                    break;
                case 2:
                    msbp_command.Append("log");
                    break;
                case 3:
                    msbp_command.Append("differential");
                    break;
            }

            if (brp._copy_only == 1)
                msbp_command.Append(";COPY_ONLY");

            if (brp._checksum == 1)
                msbp_command.Append(";CHECKSUM");

            if (brp._no_checksum == 1)
                msbp_command.Append(";NO_CHECKSUM");

            if (brp._stop_on_error == 1)
                msbp_command.Append(";STOP_ON_ERROR");

            if (brp._continue_after_error == 1)
                msbp_command.Append(";CONTINUE_AFTER_ERROR");

            if (brp._read_write_filegroups == 1)
                msbp_command.Append(";READ_WRITE_FILEGROUPS");

            if (brp._buffercount > 0)
            {
                msbp_command.Append(";BUFFERCOUNT=");
                msbp_command.Append(brp._buffercount);
            }

            if (brp._maxtransfersize > 0)
            {
                msbp_command.Append(";MAXTRANSFERSIZE=");
                msbp_command.Append(brp._maxtransfersize);
            }

            if (brp._blocksize > 0)
            {
                msbp_command.Append(";BLOCKSIZE=");
                msbp_command.Append(brp._blocksize);
            }

            foreach (string s in brp._filesoption)
            {
                msbp_command.Append(";FILE=");
                msbp_command.Append(s.Trim());
            }

            foreach (string s in brp._filegroupoption)
            {
                msbp_command.Append(";FILEGROUP=");
                msbp_command.Append(s.Trim());
            }

            if (!string.IsNullOrEmpty(brp._username))
            {
                msbp_command.Append(";username=");
                msbp_command.Append(brp._username);
            }

            if (!string.IsNullOrEmpty(brp._password))
            {
                msbp_command.Append(";password=");
                msbp_command.Append(brp._password);
            }
            msbp_command.Append("\"");
            msbp_command.Append(" ");

            if (brp._compression == 1)
            {
                msbp_command.Append("\"LZ4\"");
                msbp_command.Append(" ");
            }

            if (brp._encryption == 1)
            {
                msbp_command.Append("\"AES(key=");
                msbp_command.Append(brp._encryption_key);
                msbp_command.Append(")\"");
                msbp_command.Append(" ");

            }

            //storage string
            msbp_command.Append("\"local(");
            foreach (string disk in brp._backupfiles)
            {
                msbp_command.Append("path=");
                msbp_command.Append(disk);
                msbp_command.Append(";");
            }
            msbp_command.Append(")\"");
            brp = null;
            return msbp_command;
        }

        private void prsProjectTypes_OutputDataReceived(object sender, DataReceivedEventArgs e)
        {
            output.AppendLine(e.Data);
        }
    }
}