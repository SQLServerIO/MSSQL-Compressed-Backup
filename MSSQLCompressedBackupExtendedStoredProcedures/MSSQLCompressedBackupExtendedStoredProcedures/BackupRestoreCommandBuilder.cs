using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlTypes;
using System.Diagnostics;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.SqlServer.Server;

namespace MSSQLCompressedBackupExtendedStoredProcedures
{
    public class BackupRestoreParser
    {
        public Int32 Blocksize;
        public Int32 Buffercount;
        public Int32 Maxtransfersize;
        public Int16 Checksum;
        public Int16 NoChecksum;
        public Int16 ContinueAfterError;
        public Int16 StopOnError;
        public Int16 Stats;
        public Int16 ReadWriteFilegroups;
        public string Description;
        public string Name;
        public string Mediadescription;
        public string Medianame;
        public string Databasename;
        public string Filegroupcmd;
        public string Username;
        public string Password;
        public string Clusternetworkname;
        public string Instancename;
        public string EncryptionKey;

        //backup specific options
        public Int16 Backuptype;
        public string[] Backupdisks;
        public Int16 Compression;
        public Int16 Encryption;
        public Int16 CopyOnly;

        //restore specific variables
        public Int16 Restoretype;
        public Int16 Recoverytype;
        public Int16 Partial;
        public string Page;
        public Int16 Replace;
        public Int16 Restart;
        public Int16 RestrictedUser;
        public Int16 KeepReplicaton;
        public Int16 KeepCdc;
        public Int16 EnableBroker;
        public Int16 ErrorBrokerConversations;
        public Int16 NewBroker;
        public string StandbyFile;
        public string Stopat;
        public string Stopatmark;
        public string Stopbeforemark;
        public string Filestream;

        public List<string> Backupfiles = new List<string>();
        public List<string> Moveoptions = new List<string>();
        public List<string> Withoptions = new List<string>();
        public List<string> Filesoption = new List<string>();
        public List<string> Filegroupoption = new List<string>();

        public void ParseSQLBackupCommand(string sqlCommand)
        {
            Backuptype = 1;
            var text = Regex.Replace(sqlCommand, @"\s+", " ");

            //string pattern = " DATABASE | LOG ";
            //string[] words = Regex.Split(text, pattern, RegexOptions.IgnoreCase);

            var pattern = " ";
            var words2 = Regex.Split(text, pattern, RegexOptions.IgnoreCase);

            Databasename = words2[2].Trim();

            var dbcmd = text.Substring(text.IndexOf("BACKUP DATABASE", StringComparison.InvariantCultureIgnoreCase) + 15, text.IndexOf("TO DISK", StringComparison.InvariantCultureIgnoreCase) - text.IndexOf("BACKUP DATABASE", StringComparison.InvariantCultureIgnoreCase) - 15).Trim();
            dbcmd = dbcmd.Trim();
            if (!string.IsNullOrEmpty(dbcmd))
            {
                if (dbcmd.IndexOf(" ", StringComparison.Ordinal) > 0)
                {
                    Filegroupcmd = dbcmd.Substring(dbcmd.IndexOf(" ", StringComparison.Ordinal)).Trim();
                    var filegroupcmds = Filegroupcmd.Split(',');

                    foreach (string s in filegroupcmds)
                    {
                        var parse = " " + s.Trim() + " ";
                        if (parse.IndexOf("READ_WRITE_FILEGROUPS", StringComparison.InvariantCultureIgnoreCase) >= 0)
                        {
                            ReadWriteFilegroups = 1;
                        }
                        else if ((parse.IndexOf("FILE", StringComparison.InvariantCultureIgnoreCase) >= 0) && (parse.IndexOf("FILEGROUP", StringComparison.InvariantCultureIgnoreCase) == -1))
                        {
                            var name = parse.Split('=');
                            Filesoption.Add(name[1].Replace("=", "").Replace("'", "").Trim());
                        }
                        else if (parse.IndexOf("FILEGROUP", StringComparison.InvariantCultureIgnoreCase) >= 0)
                        {
                            var name = parse.Split('=');
                            Filegroupoption.Add(name[1].Replace("=", "").Replace("'", "").Trim());
                        }
                    }
                }
            }
            if (text.IndexOf("BACKUP LOG", StringComparison.InvariantCultureIgnoreCase) >= 0)
            {
                Backuptype = 2;
            }

            pattern = " WITH ";
            var words3 = Regex.Split(text, pattern, RegexOptions.IgnoreCase);
            var words4 = words3[1].Split(',');
            foreach (var s in words4)
            {
                string parse = " " + s.Trim() + " ";
                if (parse.IndexOf("differential", StringComparison.InvariantCultureIgnoreCase) >= 0)
                {
                    Backuptype = 3;
                }

                if (parse.IndexOf("copy_only", StringComparison.InvariantCultureIgnoreCase) >= 0)
                {
                    CopyOnly = 1;
                }

                if (parse.IndexOf("encryption", StringComparison.InvariantCultureIgnoreCase) >= 0)
                {
                    var encryptionKey = s.Split('=');
                    EncryptionKey = encryptionKey[1].Trim();
                    Encryption = 1;
                }

                if (parse.IndexOf("compression", StringComparison.InvariantCultureIgnoreCase) >= 0)
                {
                    Compression = 1;
                }

                if (parse.IndexOf("no_checksum", StringComparison.InvariantCultureIgnoreCase) >= 0)
                {
                    NoChecksum = 1;
                }
                else if (parse.IndexOf("checksum", StringComparison.InvariantCultureIgnoreCase) >= 0)
                {
                    Checksum = 1;
                }

                if (parse.IndexOf("continue_after_error", StringComparison.InvariantCultureIgnoreCase) >= 0)
                {
                    ContinueAfterError = 1;
                }
                if (parse.IndexOf("blocksize", StringComparison.InvariantCultureIgnoreCase) >= 0)
                {
                    var blksz = s.Split('=');
                    Blocksize = Convert.ToInt32(blksz[1].Trim());
                }
                if (parse.IndexOf("buffercount", StringComparison.InvariantCultureIgnoreCase) >= 0)
                {
                    var buffcnt = s.Split('=');
                    Buffercount = Convert.ToInt32(buffcnt[1].Trim());
                }
                if (parse.IndexOf("maxtransfersize", StringComparison.InvariantCultureIgnoreCase) >= 0)
                {
                    var maxtrans = s.Split('=');
                    Maxtransfersize = Convert.ToInt32(maxtrans[1].Trim());
                }
                if (parse.IndexOf("stats", StringComparison.InvariantCultureIgnoreCase) >= 0)
                {
                    var stats = s.Split('=');
                    Stats = Convert.ToInt16(stats[1].Trim());
                }

                if (parse.IndexOf("mediadescription", StringComparison.InvariantCultureIgnoreCase) >= 0)
                {
                    var mediadescription = s.Split('=');
                    Mediadescription = mediadescription[1].Trim();
                }
                else if (parse.IndexOf("description", StringComparison.InvariantCultureIgnoreCase) >= 0)
                {
                    var desc = s.Split('=');
                    Description = desc[1].Trim();
                }

                if (parse.IndexOf("username", StringComparison.InvariantCultureIgnoreCase) >= 0)
                {
                    var name = s.Split('=');
                    Username = name[1].Trim();
                }

                if (parse.IndexOf("medianame", StringComparison.InvariantCultureIgnoreCase) >= 0)
                {
                    var medianame = s.Split('=');
                    Medianame = medianame[1].Trim();
                }

                if (parse.IndexOf("instancename", StringComparison.InvariantCultureIgnoreCase) >= 0)
                {
                    var name = s.Split('=');
                    Instancename = name[1].Trim();
                }

                if (parse.IndexOf("clusternetworkname", StringComparison.InvariantCultureIgnoreCase) >= 0)
                {
                    var name = s.Split('=');
                    Clusternetworkname = name[1].Trim();
                }

                if (
                    (parse.IndexOf("instancename", StringComparison.InvariantCultureIgnoreCase) == -1) &&
                    (parse.IndexOf("medianame", StringComparison.InvariantCultureIgnoreCase) == -1) &&
                    (parse.IndexOf("username", StringComparison.InvariantCultureIgnoreCase) == -1) &&
                    (parse.IndexOf("clusternetworkname", StringComparison.InvariantCultureIgnoreCase) == -1) &&
                    parse.IndexOf("name", StringComparison.InvariantCultureIgnoreCase) >= 0)
                {
                    var name = s.Split('=');
                    Name = name[1].Trim();
                }

                if (parse.IndexOf("password", StringComparison.InvariantCultureIgnoreCase) >= 0)
                {
                    var name = s.Split('=');
                    Password = name[1].Trim();
                }
                if (parse.IndexOf("stop_on_error", StringComparison.InvariantCultureIgnoreCase) >= 0)
                {
                    StopOnError = 1;
                }
            }
            //we ignore MIRROR TO DISK for now
            //we also will only use one TO DISK statement for the backup command
            Backupdisks = text.IndexOf("MIRROR TO DISK", StringComparison.InvariantCultureIgnoreCase) >= 0 ? text.Substring(text.IndexOf("TO DISK", StringComparison.InvariantCultureIgnoreCase) + 3, text.IndexOf("MIRROR TO DISK", StringComparison.InvariantCultureIgnoreCase) - text.IndexOf("TO DISK", StringComparison.InvariantCultureIgnoreCase) - 3).Split(',') : text.Substring(text.IndexOf("TO DISK", StringComparison.InvariantCultureIgnoreCase) + 3, text.IndexOf(" WITH ", StringComparison.InvariantCultureIgnoreCase) - text.IndexOf("TO DISK", StringComparison.InvariantCultureIgnoreCase) - 3).Split(',');

            foreach (string s in Backupdisks)
            {
                Backupfiles.Add(s.Replace("=", "").ToLower().Replace("disk", "").Replace("'", "").Trim());
            }
        }

        public void ParseSQLRestoreCommand(string sqlCommand)
        {
            Restoretype = 1;
            Recoverytype = 0;
            Backupfiles.Clear();
            Moveoptions.Clear();
            Withoptions.Clear();
            Filegroupoption.Clear();

            string text = Regex.Replace(sqlCommand, @"\s+", " ");

            if ((text.IndexOf("RESTORE FILELISTONLY", StringComparison.InvariantCultureIgnoreCase) >= 0) || (text.IndexOf("RESTORE HEADERONLY", StringComparison.InvariantCultureIgnoreCase) >= 0))
            {
                Backupdisks = text.Substring(text.IndexOf("FROM DISK", StringComparison.InvariantCultureIgnoreCase)).Split(',');

                foreach (var s in Backupdisks)
                {
                    Backupfiles.Add(Regex.Replace(s, "([Ff][Rr][Oo][Mm])", "").Replace("=", "").ToLower().Replace("disk", "").Replace("'", "").Trim());
                }
            }
            else
            {
                //string pattern = " DATABASE | LOG ";
                //string[] words = Regex.Split(text, pattern, RegexOptions.IgnoreCase);

                var pattern = " ";
                var words2 = Regex.Split(text, pattern, RegexOptions.IgnoreCase);

                Databasename = words2.Length > 3 ? words2[2].Trim() : words2[1].Trim();

                var dbcmd = text.Substring(text.IndexOf("RESTORE DATABASE", StringComparison.InvariantCultureIgnoreCase) + 15, text.IndexOf("FROM DISK", StringComparison.InvariantCultureIgnoreCase) - text.IndexOf("RESTORE DATABASE", StringComparison.InvariantCultureIgnoreCase) - 15).Trim();
                dbcmd = dbcmd.Trim();

                if (!string.IsNullOrEmpty(dbcmd))
                {
                    if (dbcmd.IndexOf(" ", StringComparison.Ordinal) > 0)
                    {
                        Filegroupcmd = dbcmd.Substring(dbcmd.IndexOf(" ", StringComparison.Ordinal)).Trim();
                        var filegroupcmds = Filegroupcmd.Split(',');

                        foreach (var s in filegroupcmds)
                        {
                            var parse = " " + s.Trim() + " ";
                            if (parse.IndexOf("READ_WRITE_FILEGROUPS", StringComparison.InvariantCultureIgnoreCase) >= 0)
                            {
                                ReadWriteFilegroups = 1;
                            }
                            else if ((parse.IndexOf("FILE", StringComparison.InvariantCultureIgnoreCase) >= 0) && (parse.IndexOf("FILEGROUP", StringComparison.InvariantCultureIgnoreCase) == -1))
                            {
                                var name = parse.Split('=');
                                Filesoption.Add(name[1].Replace("=", "").Replace("'", "").Trim());
                            }
                            else if (parse.IndexOf("FILEGROUP", StringComparison.InvariantCultureIgnoreCase) >= 0)
                            {
                                var name = parse.Split('=');
                                Filegroupoption.Add(name[1].Replace("=", "").Replace("'", "").Trim());
                            }
                        }
                    }
                }

                if (text.IndexOf("RESTORE LOG", StringComparison.InvariantCultureIgnoreCase) >= 0)
                {
                    Restoretype = 2;
                }
                pattern = " WITH ";

                var words3 = Regex.Split(text, pattern, RegexOptions.IgnoreCase);
                var words4 = words3[1].Split(',');
                foreach (var s in words4)
                {
                    var parse = " " + s.Trim() + " ";
                    if (parse.IndexOf("checksum", StringComparison.InvariantCultureIgnoreCase) >= 0)
                    {
                        Checksum = 1;
                    }
                    else if (parse.IndexOf("no_checksum", StringComparison.InvariantCultureIgnoreCase) >= 0)
                    {
                        NoChecksum = 1;
                    }

                    if (parse.IndexOf("continue_after_error", StringComparison.InvariantCultureIgnoreCase) >= 0)
                    {
                        ContinueAfterError = 1;
                    }

                    if (parse.IndexOf("blocksize", StringComparison.InvariantCultureIgnoreCase) >= 0)
                    {
                        var blksz = s.Split('=');
                        Blocksize = Convert.ToInt32(blksz[1].Trim());
                    }

                    if (parse.IndexOf("buffercount", StringComparison.InvariantCultureIgnoreCase) >= 0)
                    {
                        var buffcnt = s.Split('=');
                        Buffercount = Convert.ToInt32(buffcnt[1].Trim());
                    }

                    if (parse.IndexOf("maxtransfersize", StringComparison.InvariantCultureIgnoreCase) >= 0)
                    {
                        var maxtrans = s.Split('=');
                        Maxtransfersize = Convert.ToInt32(maxtrans[1].Trim());
                    }

                    if (parse.IndexOf("encryption", StringComparison.InvariantCultureIgnoreCase) >= 0)
                    {
                        Encryption = 1;
                        var encryptionKey = s.Split('=');
                        EncryptionKey = encryptionKey[1].Trim();
                    }

                    if (parse.IndexOf("compression", StringComparison.InvariantCultureIgnoreCase) >= 0)
                    {
                        Compression = 1;
                    }

                    if (parse.IndexOf("stats", StringComparison.InvariantCultureIgnoreCase) >= 0)
                    {
                        var stats = s.Split('=');
                        Stats = Convert.ToInt16(stats[1].Trim());
                    }

                    if (parse.IndexOf("username", StringComparison.InvariantCultureIgnoreCase) >= 0)
                    {
                        var name = s.Split('=');
                        Username = name[1].Trim();
                    }

                    if (parse.IndexOf("medianame", StringComparison.InvariantCultureIgnoreCase) >= 0)
                    {
                        var medianame = s.Split('=');
                        Medianame = medianame[1].Trim();
                    }

                    if (parse.IndexOf("instancename", StringComparison.InvariantCultureIgnoreCase) >= 0)
                    {
                        var name = s.Split('=');
                        Instancename = name[1].Trim();
                    }

                    if (parse.IndexOf("clusternetworkname", StringComparison.InvariantCultureIgnoreCase) >= 0)
                    {
                        var name = s.Split('=');
                        Clusternetworkname = name[1].Trim();
                    }

                    if (
                        (parse.IndexOf("instancename", StringComparison.InvariantCultureIgnoreCase) == -1) &&
                        (parse.IndexOf("medianame", StringComparison.InvariantCultureIgnoreCase) == -1) &&
                        (parse.IndexOf("username", StringComparison.InvariantCultureIgnoreCase) == -1) &&
                        (parse.IndexOf("clusternetworkname", StringComparison.InvariantCultureIgnoreCase) == -1) &&
                        parse.IndexOf("name", StringComparison.InvariantCultureIgnoreCase) >= 0)
                    {
                        var name = s.Split('=');
                        Name = name[1].Trim();
                    }

                    if (parse.IndexOf("NORECOVERY", StringComparison.InvariantCultureIgnoreCase) >= 0)
                    {
                        Recoverytype = 2;
                    }
                    else if (parse.IndexOf("RECOVERY", StringComparison.InvariantCultureIgnoreCase) >= 0)
                    {
                        Recoverytype = 1;
                    }

                    if (parse.IndexOf("STANDBY", StringComparison.InvariantCultureIgnoreCase) >= 0)
                    {
                        Recoverytype = 3;
                        var name = s.Split('=');
                        StandbyFile = name[1].Trim();
                    }

                    if (parse.IndexOf("partial", StringComparison.InvariantCultureIgnoreCase) >= 0)
                    {
                        Partial = 1;
                    }

                    if (parse.IndexOf("replace", StringComparison.InvariantCultureIgnoreCase) >= 0)
                    {
                        Replace = 1;
                    }

                    if (parse.IndexOf("restricted_user", StringComparison.InvariantCultureIgnoreCase) >= 0)
                    {
                        RestrictedUser = 1;
                    }

                    if (parse.IndexOf("keep_replicaton", StringComparison.InvariantCultureIgnoreCase) >= 0)
                    {
                        KeepReplicaton = 1;
                    }

                    else if (parse.IndexOf("keep_cdc", StringComparison.InvariantCultureIgnoreCase) >= 0)
                    {
                        KeepCdc = 1;
                    }

                    if (parse.IndexOf("enable_broker", StringComparison.InvariantCultureIgnoreCase) >= 0)
                    {
                        EnableBroker = 1;
                    }

                    if (parse.IndexOf("error_broker_conversations", StringComparison.InvariantCultureIgnoreCase) >= 0)
                    {
                        ErrorBrokerConversations = 1;
                    }

                    if (parse.IndexOf("new_broker", StringComparison.InvariantCultureIgnoreCase) >= 0)
                    {
                        NewBroker = 1;
                    }

                    if (parse.IndexOf("restart", StringComparison.InvariantCultureIgnoreCase) >= 0)
                    {
                        Restart = 1;
                    }

                    if (parse.IndexOf("stop_on_error", StringComparison.InvariantCultureIgnoreCase) >= 0)
                    {
                        StopOnError = 1;
                    }
                    else if (parse.IndexOf("STOPATMARK", StringComparison.InvariantCultureIgnoreCase) >= 0)
                    {
                        var name = parse.Split('=');
                        Stopatmark = name[1].Trim();
                    }
                    else if (parse.IndexOf("STOPBEFOREMARK", StringComparison.InvariantCultureIgnoreCase) >= 0)
                    {
                        var name = parse.Split('=');
                        Stopbeforemark = name[1].Trim();
                    }
                    else if (parse.IndexOf("STOPAT", StringComparison.InvariantCultureIgnoreCase) >= 0)
                    {
                        var name = parse.Split('=');
                        Stopat = name[1].Trim();
                    }

                    if (parse.IndexOf("PAGE", StringComparison.InvariantCultureIgnoreCase) >= 0)
                    {
                        var name = parse.Split('=');
                        Page = name[1].Trim();
                    }

                    if (parse.IndexOf("password", StringComparison.InvariantCultureIgnoreCase) >= 0)
                    {
                        var name = s.Split('=');
                        Password = name[1].Trim();
                    }

                    if (parse.IndexOf("move", StringComparison.InvariantCultureIgnoreCase) >= 0)
                    {
                        pattern = " to ";
                        var movesplit = Regex.Split(parse.Trim().Remove(0, 4).Trim(), pattern, RegexOptions.IgnoreCase);
                        Moveoptions.Add(movesplit[0].Trim() + "TO" + movesplit[1].Trim());
                    }
                    else if (parse.IndexOf("FILESTREAM", StringComparison.InvariantCultureIgnoreCase) >= 0)
                    {
                        var name = parse.Split('=');
                        Filestream = name[1].Trim();
                    }
                    else if (parse.IndexOf("READ_WRITE_FILEGROUPS", StringComparison.InvariantCultureIgnoreCase) >= 0)
                    {
                        ReadWriteFilegroups = 1;
                    }
                    else if ((parse.IndexOf("FILE", StringComparison.InvariantCultureIgnoreCase) >= 0) && (parse.IndexOf("FILEGROUP", StringComparison.InvariantCultureIgnoreCase) == -1))
                    {
                        var name = parse.Split(',');
                        foreach (var fn in name)
                        {
                            Filesoption.Add(fn.Replace("=", "").ToLower().Replace("file", "").Replace("'", "").Trim());
                        }

                    }
                    else if (parse.IndexOf("FILEGROUP", StringComparison.InvariantCultureIgnoreCase) >= 0)
                    {
                        //string[] name = parse.Split(',');
                        foreach (var fg in Backupdisks)
                        {
                            Filegroupoption.Add(fg.Replace("=", "").ToLower().Replace("disk", "").Replace("'", "").Trim());
                        }
                    }
                }
                //we ignore DATABASE_SNAPSHOT for now
                //we also will only use one FROM DISK statement for the backup command
                if (text.IndexOf("DATABASE_SNAPSHOT", StringComparison.InvariantCultureIgnoreCase) == -1)
                {
                    Backupdisks = text.Substring(text.IndexOf("FROM DISK", StringComparison.InvariantCultureIgnoreCase) + 4, text.IndexOf(" WITH ", StringComparison.InvariantCultureIgnoreCase) - text.IndexOf("FROM DISK", StringComparison.InvariantCultureIgnoreCase) - 3).Split(',');
                }

                foreach (var s in Backupdisks)
                {
                    Backupfiles.Add(Regex.Replace(s, "([Ff][Rr][Oo][Mm])", "").Replace("=", "").ToLower().Replace("disk", "").Replace("'", "").Trim());
                }
            }
        }
    }

    public class ExecuteBackupRestore
    {
        StringBuilder _output;
        public void BuildAndExecuteStatement(string filename, string arguments)
        {
            //BackupRestoreParser bpr = new BackupRestoreParser();
            if (string.IsNullOrEmpty(arguments) || string.IsNullOrEmpty(filename))
            {
                throw new Exception("You must specify the path to msbp.exe AND pass in a valid backup command.");
            }
            if (filename.Contains("exe"))
            {
                throw new Exception(filename + " You cannot specifiy the executable name.");
            }

            var msbpCommand = new StringBuilder {Capacity = 0};

            var execStatus = false;
            if (!string.IsNullOrEmpty(arguments))
            {
                if ((arguments.IndexOf("backup database", StringComparison.InvariantCultureIgnoreCase) >= 0) || (arguments.IndexOf("backup log", StringComparison.InvariantCultureIgnoreCase) >= 0))
                {
                    msbpCommand = BuildBackupCommand(arguments);
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
                    msbpCommand = BuildRestoreCommand(arguments);
                    execStatus = true;
                }
                else if ((arguments.IndexOf("restore verifyonly", StringComparison.InvariantCultureIgnoreCase) >= 0))
                {
                    msbpCommand = BuildRestoreVerifyCommand(arguments);
                    execStatus = true;
                }
                else
                {
                    throw new Exception("No valid backup or restore command given.");
                }
            }

            if (execStatus)
            {
                string errorMessage;
                _output = new StringBuilder();
                var outPipe = SqlContext.Pipe;
                using (var prsProjectTypes = new Process())
                {
                    try
                    {
                        var msbpPath = filename.EndsWith(@"\") ? filename : filename + @"\";
                        msbpPath += "msbp.exe";

                        prsProjectTypes.EnableRaisingEvents = false;
                        prsProjectTypes.StartInfo.UseShellExecute = false;
                        prsProjectTypes.StartInfo.RedirectStandardOutput = true;
                        prsProjectTypes.StartInfo.RedirectStandardError = true;
                        prsProjectTypes.StartInfo.CreateNoWindow = true;
                        prsProjectTypes.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
                        prsProjectTypes.StartInfo.FileName = msbpPath;
                        prsProjectTypes.StartInfo.Arguments = msbpCommand.ToString();
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
                        if (outPipe != null) outPipe.Send(msbpCommand.ToString());
                        throw new Exception("error while executing " + filename + " " + arguments + ": " + e.Message, e);
                    }

                    // Handle Standard Output
                    prsProjectTypes.OutputDataReceived += prsProjectTypes_OutputDataReceived;
                    // Handle Error Output
                    prsProjectTypes.ErrorDataReceived += prsProjectTypes_OutputDataReceived;
                    prsProjectTypes.BeginOutputReadLine();

                    //hang out here and wait for completion.
                    prsProjectTypes.WaitForExit();

                    if (prsProjectTypes.ExitCode != 0)
                    {
                        if (outPipe != null) outPipe.Send(msbpCommand.ToString());
                        errorMessage = filename + " finished with exit code = " + prsProjectTypes.ExitCode + ": " + arguments;
                        errorMessage += "\n";
                        errorMessage += _output.ToString();
                    }
                    else
                    {
                        errorMessage = string.Empty;
                        var strlensplit = 4000;
                        const int strlensplithold = 4000;
                        var strhead = 0;
                        //var finaloutput = new List<String>();
                        var charsplit = '\n';
                        var nosplitter = false;
                        if (outPipe != null)
                        {
                            outPipe.Send(msbpCommand.ToString());

                            if (_output.Length > strlensplit)
                            {
                                var outhold = _output.ToString();
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
                                        var charhold = new string(outhold.ToCharArray(strlensplit, 1));
                                        if (charhold.IndexOf(charsplit) == -1)
                                        {
                                            strlensplit--;
                                        }
                                        else
                                        {
                                            outPipe.Send(outhold.Substring(strhead, strlensplit - strhead));
                                            strhead = strlensplit;
                                            strlensplit += strlensplithold;
                                            //charhold = new string(outhold.ToCharArray(strlensplit, 1));
                                        }

                                    }
                                }
                                else
                                {
                                    var chunkSize = 4000;
                                    var stringLength = outhold.Length;
                                    for (var i = 0; i < stringLength; i += chunkSize)
                                    {
                                        if (i + 4000 > stringLength) chunkSize = stringLength - i;
                                        outPipe.Send(outhold.Substring(i, chunkSize));

                                    }
                                }
                            }
                            else
                            {
                                outPipe.Send(_output.ToString());
                            }
                        }
                    }

                    prsProjectTypes.Close();
                    prsProjectTypes.Dispose();
                }

                if (!string.IsNullOrEmpty(errorMessage))
                {
                    throw new Exception(errorMessage);
                }
                //outPipe = null;
            }
            //msbpCommand = null;
        }

        private void ReturnHeaderOnly(string sqlcommand)
        {
            var bFormat = new BinaryFormatter();
            FileStream fs;
            string metaDataPath;
            var brp = new BackupRestoreParser();
            try
            {
                brp.ParseSQLRestoreCommand(sqlcommand);
            }
            catch (Exception e)
            {
                //fs = null;
                //bFormat = null;
                throw new Exception(sqlcommand + " cannot parse this command Error: " + e.Message);
            }

            try
            {
                metaDataPath = brp.Backupfiles[0];
            }
            catch (Exception e)
            {
                //fs = null;
                //bFormat = null;
                throw new Exception(sqlcommand + " does not contain valid meta data. Error: " + e.Message);
            }

            try
            {
                fs = new FileStream(metaDataPath, FileMode.Open);
            }
            catch (Exception te)
            {
                //fs = null;
                //bFormat = null;
                throw new Exception(metaDataPath + " failed to open. Error: " + te.Message);
            }

            var headerOnlyData = new DataSet();
            try
            {
                headerOnlyData = (DataSet)bFormat.Deserialize(fs);
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
                        headerOnlyData = (DataSet)bFormat.Deserialize(fs);
                    }
                    catch (Exception ef)
                    {
                        fs.Close();
                        fs.Dispose();
                        headerOnlyData.Dispose();
                        //fs = null;
                        //headerOnlyData = null;
                        //bFormat = null;
                        throw new Exception(metaDataPath + " does not contain a valid meta data file for msbp. Error: " + ef.Message);
                    }
                }
                else
                {
                    fs.Close();
                    fs.Dispose();
                    headerOnlyData.Dispose();
                    //fs = null;
                    //headerOnlyData = null;
                    //bFormat = null;
                    throw new Exception(metaDataPath + " cannot be opened or does not contain a valid meta data file for msbp. Error: " + e.Message);
                }
            }

            try
            {
                SendDataTable(headerOnlyData.Tables[0]);
            }
            catch (Exception e)
            {
                fs.Close();
                fs.Dispose();
                headerOnlyData.Dispose();
                //fs = null;
                //headerOnlyData = null;
                //bFormat = null;
                throw new Exception(metaDataPath + " failed to read data table. Error: " + e.Message);
            }

            fs.Close();
            fs.Dispose();
            headerOnlyData.Dispose();
            //fs = null;
            //headerOnlyData = null;
            //metaDataPath = null;
            //bFormat = null;
        }

        private void ReturnFileListOnly(string sqlcommand)
        {
            var bFormat = new BinaryFormatter();
            FileStream fs;
            String metaDataPath;
            var brp = new BackupRestoreParser();

            try
            {
                brp.ParseSQLRestoreCommand(sqlcommand);
            }
            catch (Exception e)
            {
                //brp = null;
                //metaDataPath = null;
                //bFormat = null;
                //fs = null;
                throw new Exception(sqlcommand + " cannot parse this command Error: " + e.Message);
            }
            try
            {
                metaDataPath = brp.Backupfiles[0];
            }
            catch (Exception e)
            {
                //brp = null;
                //metaDataPath = null;
                //bFormat = null;
                //fs = null;
                throw new Exception(sqlcommand + " does not contain valid meta data. Error: " + e.Message);
            }
            try
            {
                fs = new FileStream(metaDataPath, FileMode.Open);
            }
            catch (Exception te)
            {
                if (metaDataPath.IndexOf(".hfl", StringComparison.InvariantCultureIgnoreCase) >= 0)
                {
                    throw new Exception(sqlcommand + " does not contain a valid meta data file for msbp. Error: " + te.Message);
                }
                try
                {
                    metaDataPath = ".hfl";
                    fs = new FileStream(metaDataPath, FileMode.Open);
                }
                catch (Exception e)
                {
                    //brp = null;
                    //metaDataPath = null;
                    //bFormat = null;
                    //fs = null;
                    throw new Exception(sqlcommand + " does not contain a valid meta data file for msbp. Error: " + e.Message);
                }
            }

            DataSet headerOnlyData;
            try
            {

                headerOnlyData = (DataSet)bFormat.Deserialize(fs);
            }
            catch (Exception e)
            {
                //brp = null;
                //bFormat = null;
                //fs = null;
                //headerOnlyData = null;
                throw new Exception(metaDataPath + " cannot be opened. Error: " + e.Message);
            }

            try
            {
                SendDataTable(headerOnlyData.Tables[1]);
            }
            catch (Exception e)
            {
                fs.Close();
                fs.Dispose();
                headerOnlyData.Dispose();
                //brp = null;
                //bFormat = null;
                //fs = null;
                //headerOnlyData = null;
                throw new Exception(metaDataPath + " failed to read data table. Error: " + e.Message);
            }

            fs.Dispose();
            headerOnlyData.Dispose();
            //fs = null;
            //headerOnlyData = null;
            //metaDataPath = null;
            //bFormat = null;
        }

        public void SendDataTable(DataTable dt)
        {
            bool[] coerceToString;  // Do we need to coerce this column to string?
            var metaData = ExtractDataTableColumnMetaData(dt, out coerceToString);

            var record = new SqlDataRecord(metaData);
            var outPipe = SqlContext.Pipe;
            if (outPipe == null) return;
            outPipe.SendResultsStart(record);
            try
            {
                foreach (DataRow row in dt.Rows)
                {
                    for (var index = 0; index < record.FieldCount; index++)
                    {
                        var value = row[index];
                        if (null != value && coerceToString[index])
                            value = value.ToString();
                        if (value != null) record.SetValue(index, value);
                    }

                    outPipe.SendResultsRow(record);
                }
            }
            finally
            {
                outPipe.SendResultsEnd();
                //metaData = null;
                //record = null;
               //outPipe = null;
            }
        }

        private SqlMetaData[] ExtractDataTableColumnMetaData(DataTable dt, out bool[] coerceToString)
        {
            var metaDataResult = new SqlMetaData[dt.Columns.Count];
            coerceToString = new bool[dt.Columns.Count];
            for (var index = 0; index < dt.Columns.Count; index++)
            {
                var column = dt.Columns[index];
                metaDataResult[index] = SqlMetaDataFromColumn(column, out coerceToString[index]);
            }

            return metaDataResult;
        }

        private static Exception InvalidDataTypeCode(TypeCode code)
        {
            return new ArgumentException("Invalid type: " + code);
        }

        private static Exception UnknownDataType(Type clrType)
        {
            return new ArgumentException("Unknown type: " + clrType);
        }

        private SqlMetaData SqlMetaDataFromColumn(DataColumn column, out bool coerceToString)
        {
            coerceToString = false;
            SqlMetaData sqlMd;
            var clrType = column.DataType;
            var name = column.ColumnName;
            switch (Type.GetTypeCode(clrType))
            {
                case TypeCode.Boolean: sqlMd = new SqlMetaData(name, SqlDbType.Bit); break;
                case TypeCode.Byte: sqlMd = new SqlMetaData(name, SqlDbType.TinyInt); break;
                case TypeCode.Char: sqlMd = new SqlMetaData(name, SqlDbType.NVarChar, 1); break;
                case TypeCode.DateTime: sqlMd = new SqlMetaData(name, SqlDbType.DateTime); break;
                case TypeCode.DBNull: throw InvalidDataTypeCode(TypeCode.DBNull);
                //develop better way to detect this see Adam Mechanic's book 2008 development
                case TypeCode.Decimal: sqlMd = new SqlMetaData(name, SqlDbType.Decimal, 25, 0); break;
                case TypeCode.Double: sqlMd = new SqlMetaData(name, SqlDbType.Float); break;
                case TypeCode.Empty: throw InvalidDataTypeCode(TypeCode.Empty);
                case TypeCode.Int16: sqlMd = new SqlMetaData(name, SqlDbType.SmallInt); break;
                case TypeCode.Int32: sqlMd = new SqlMetaData(name, SqlDbType.Int); break;
                case TypeCode.Int64: sqlMd = new SqlMetaData(name, SqlDbType.BigInt); break;
                case TypeCode.SByte: throw InvalidDataTypeCode(TypeCode.SByte);
                case TypeCode.Single: sqlMd = new SqlMetaData(name, SqlDbType.Real); break;
                case TypeCode.String: sqlMd = new SqlMetaData(name, SqlDbType.NVarChar, column.MaxLength == Int32.MaxValue ? -1 : column.MaxLength);
                    break;
                case TypeCode.UInt16: throw InvalidDataTypeCode(TypeCode.UInt16);
                case TypeCode.UInt32: throw InvalidDataTypeCode(TypeCode.UInt32);
                case TypeCode.UInt64: throw InvalidDataTypeCode(TypeCode.UInt64);
                case TypeCode.Object:
                    sqlMd = SqlMetaDataFromObjectColumn(name, column, clrType);
                    if (sqlMd == null)
                    {
                        // Unknown type, try to treat it as string;
                        sqlMd = new SqlMetaData(name, SqlDbType.NVarChar, column.MaxLength);
                        coerceToString = true;
                    }
                    break;

                default: throw UnknownDataType(clrType);
            }

            return sqlMd;
        }

        private static SqlMetaData SqlMetaDataFromObjectColumn(string name, DataColumn column, Type clrType)
        {
            SqlMetaData sqlMd;
            if (clrType == typeof(Byte[]) || clrType == typeof(SqlBinary) || clrType == typeof(SqlBytes) ||
        clrType == typeof(Char[]) || clrType == typeof(SqlString) || clrType == typeof(SqlChars))
                sqlMd = new SqlMetaData(name, SqlDbType.VarBinary, column.MaxLength);
            else if (clrType == typeof(Guid))
                sqlMd = new SqlMetaData(name, SqlDbType.UniqueIdentifier);
            else if (clrType == typeof(Object))
                sqlMd = new SqlMetaData(name, SqlDbType.Variant);
            else if (clrType == typeof(SqlBoolean))
                sqlMd = new SqlMetaData(name, SqlDbType.Bit);
            else if (clrType == typeof(SqlByte))
                sqlMd = new SqlMetaData(name, SqlDbType.TinyInt);
            else if (clrType == typeof(SqlDateTime))
                sqlMd = new SqlMetaData(name, SqlDbType.DateTime);
            else if (clrType == typeof(SqlDouble))
                sqlMd = new SqlMetaData(name, SqlDbType.Float);
            else if (clrType == typeof(SqlGuid))
                sqlMd = new SqlMetaData(name, SqlDbType.UniqueIdentifier);
            else if (clrType == typeof(SqlInt16))
                sqlMd = new SqlMetaData(name, SqlDbType.SmallInt);
            else if (clrType == typeof(SqlInt32))
                sqlMd = new SqlMetaData(name, SqlDbType.Int);
            else if (clrType == typeof(SqlInt64))
                sqlMd = new SqlMetaData(name, SqlDbType.BigInt);
            else if (clrType == typeof(SqlMoney))
                sqlMd = new SqlMetaData(name, SqlDbType.Money);
            else if (clrType == typeof(SqlDecimal))
                sqlMd = new SqlMetaData(name, SqlDbType.Decimal, SqlDecimal.MaxPrecision, 0);
            else if (clrType == typeof(SqlSingle))
                sqlMd = new SqlMetaData(name, SqlDbType.Real);
            else if (clrType == typeof(SqlXml))
                sqlMd = new SqlMetaData(name, SqlDbType.Xml);
            else
                sqlMd = null;

            return sqlMd;
        }

        private static StringBuilder BuildRestoreVerifyCommand(string sqlcommand)
        {
            var brp = new BackupRestoreParser();
            brp.ParseSQLRestoreCommand(sqlcommand);

            var msbpCommand = new StringBuilder();

            msbpCommand.Append("restoreverifyonly \"local(");
            foreach (var disk in brp.Backupfiles)
            {
                msbpCommand.Append("path=");
                msbpCommand.Append(disk);
                msbpCommand.Append(";");
            }
            msbpCommand.Append(")\"");
            msbpCommand.Append(" ");

            if (brp.Compression == 1)
            {
                msbpCommand.Append("\"LZ4\"");
                msbpCommand.Append(" ");
            }

            if (brp.Encryption == 1)
            {
                msbpCommand.Append("\"AES(key=90-1q@W#E$R%T^Y)\"");
                msbpCommand.Append(" ");
            }

            msbpCommand.Append("\"db(database=");
            msbpCommand.Append(brp.Databasename);

            if (!string.IsNullOrEmpty(brp.Instancename))
            {
                msbpCommand.Append(";instancename=");
                msbpCommand.Append(brp.Instancename);
            }

            if (!string.IsNullOrEmpty(brp.Clusternetworkname))
            {
                msbpCommand.Append(";clusternetworkname=");
                msbpCommand.Append(brp.Clusternetworkname);
            }

            foreach (var s in brp.Moveoptions)
            {
                msbpCommand.Append(";MOVE=");
                msbpCommand.Append(s.Trim());
            }
            if (brp.Checksum == 1)
                msbpCommand.Append(";CHECKSUM");

            if (brp.NoChecksum == 1)
                msbpCommand.Append(";NO_CHECKSUM");

            if (brp.StopOnError == 1)
                msbpCommand.Append(";STOP_ON_ERROR");

            if (brp.ContinueAfterError == 1)
                msbpCommand.Append(";CONTINUE_AFTER_ERROR");

            if (!string.IsNullOrEmpty(brp.Username))
            {
                msbpCommand.Append(";username=");
                msbpCommand.Append(brp.Username);
            }

            if (!string.IsNullOrEmpty(brp.Password))
            {
                msbpCommand.Append(";password=");
                msbpCommand.Append(brp.Password);
            }
            msbpCommand.Append(")\"");
            msbpCommand.Append(" ");

            return msbpCommand;
        }

        private static StringBuilder BuildRestoreCommand(string sqlcommand)
        {
            var brp = new BackupRestoreParser();
            brp.ParseSQLRestoreCommand(sqlcommand);

            var msbpCommand = new StringBuilder();

            msbpCommand.Append("restore \"local(");
            foreach (var disk in brp.Backupfiles)
            {
                msbpCommand.Append("path=");
                msbpCommand.Append(disk);
                msbpCommand.Append(";");
            }
            msbpCommand.Append(")\"");
            msbpCommand.Append(" ");

            if (brp.Compression == 1)
            {
                msbpCommand.Append("\"LZ4\"");
                msbpCommand.Append(" ");
            }

            if (brp.Encryption == 1)
            {
                msbpCommand.Append("\"AES(key=90-1q@W#E$R%T^Y)\"");
                msbpCommand.Append(" ");
            }

            msbpCommand.Append("\"db(database=");
            msbpCommand.Append(brp.Databasename);

            if (!string.IsNullOrEmpty(brp.Instancename))
            {
                msbpCommand.Append(";instancename=");
                msbpCommand.Append(brp.Instancename);
            }

            if (!string.IsNullOrEmpty(brp.Clusternetworkname))
            {
                msbpCommand.Append(";clusternetworkname=");
                msbpCommand.Append(brp.Clusternetworkname);
            }
            msbpCommand.Append(";restoretype=");
            switch (brp.Restoretype)
            {
                case 1:
                    msbpCommand.Append("database");
                    break;
                case 2:
                    msbpCommand.Append("log");
                    break;
            }

            foreach (var s in brp.Moveoptions)
            {
                msbpCommand.Append(";MOVE=");
                msbpCommand.Append(s.Trim());
            }
            if (brp.Checksum == 1)
                msbpCommand.Append(";CHECKSUM");

            if (brp.NoChecksum == 1)
                msbpCommand.Append(";NO_CHECKSUM");

            if (brp.StopOnError == 1)
                msbpCommand.Append(";STOP_ON_ERROR");

            if (brp.ContinueAfterError == 1)
                msbpCommand.Append(";CONTINUE_AFTER_ERROR");

            if (brp.ReadWriteFilegroups == 1)
                msbpCommand.Append(";READ_WRITE_FILEGROUPS");

            if (brp.KeepReplicaton == 1)
                msbpCommand.Append(";KEEP_REPLICATION");

            if (brp.EnableBroker == 1)
                msbpCommand.Append(";ENABLE_BROKER");

            if (brp.ErrorBrokerConversations == 1)
                msbpCommand.Append(";ERROR_BROKER_CONVERSATIONS");

            if (brp.NewBroker == 1)
                msbpCommand.Append(";NEW_BROKER");

            if (brp.Buffercount > 0)
            {
                msbpCommand.Append(";BUFFERCOUNT=");
                msbpCommand.Append(brp.Buffercount);
            }

            if (brp.Maxtransfersize > 0)
            {
                msbpCommand.Append(";MAXTRANSFERSIZE=");
                msbpCommand.Append(brp.Maxtransfersize);
            }

            if (brp.Recoverytype == 2)
                msbpCommand.Append(";NORECOVERY");

            if (brp.Recoverytype == 1)
                msbpCommand.Append(";RECOVERY");

            if (brp.Replace == 1)
                msbpCommand.Append(";REPLACE");

            if (brp.Restart == 1)
                msbpCommand.Append(";RESTART");

            if (brp.RestrictedUser == 1)
                msbpCommand.Append(";RESTRICTED_USER");

            if (brp.Partial == 1)
                msbpCommand.Append(";PARTIAL");

            //LOADHISTORY
            //if (brp._loadhistory == 1)
            //    msbp_command.Append(";LOADHISTORY");

            foreach (var s in brp.Filesoption)
            {
                msbpCommand.Append(";FILE=");
                msbpCommand.Append(s.Trim());
            }

            foreach (var s in brp.Filegroupoption)
            {
                msbpCommand.Append(";FILEGROUP=");
                msbpCommand.Append(s.Trim());
            }

            if (!string.IsNullOrEmpty(brp.Username))
            {
                msbpCommand.Append(";username=");
                msbpCommand.Append(brp.Username);
            }

            if (!string.IsNullOrEmpty(brp.Password))
            {
                msbpCommand.Append(";password=");
                msbpCommand.Append(brp.Password);
            }

            if (!string.IsNullOrEmpty(brp.StandbyFile))
            {
                msbpCommand.Append(";STANDBY=");
                msbpCommand.Append(brp.StandbyFile);
            }

            if (!string.IsNullOrEmpty(brp.Stopat))
            {
                msbpCommand.Append(";STOPAT=");
                msbpCommand.Append(brp.Stopat);
            }

            msbpCommand.Append(")\"");
            msbpCommand.Append(" ");

            return msbpCommand;
        }

        private static StringBuilder BuildBackupCommand(string sqlcommand)
        {
            var brp = new BackupRestoreParser();
            brp.ParseSQLBackupCommand(sqlcommand);
            var msbpCommand = new StringBuilder();

            msbpCommand.Append("backup \"db(database=");
            msbpCommand.Append(brp.Databasename);

            if (!string.IsNullOrEmpty(brp.Instancename))
            {
                msbpCommand.Append(";instancename=");
                msbpCommand.Append(brp.Instancename);
            }

            if (!string.IsNullOrEmpty(brp.Clusternetworkname))
            {
                msbpCommand.Append(";clusternetworkname=");
                msbpCommand.Append(brp.Clusternetworkname);
            }

            msbpCommand.Append(";backuptype=");
            switch (brp.Backuptype)
            {
                case 1:
                    msbpCommand.Append("full");
                    break;
                case 2:
                    msbpCommand.Append("log");
                    break;
                case 3:
                    msbpCommand.Append("differential");
                    break;
            }

            if (brp.CopyOnly == 1)
                msbpCommand.Append(";COPY_ONLY");

            if (brp.Checksum == 1)
                msbpCommand.Append(";CHECKSUM");

            if (brp.NoChecksum == 1)
                msbpCommand.Append(";NO_CHECKSUM");

            if (brp.StopOnError == 1)
                msbpCommand.Append(";STOP_ON_ERROR");

            if (brp.ContinueAfterError == 1)
                msbpCommand.Append(";CONTINUE_AFTER_ERROR");

            if (brp.ReadWriteFilegroups == 1)
                msbpCommand.Append(";READ_WRITE_FILEGROUPS");

            if (brp.Buffercount > 0)
            {
                msbpCommand.Append(";BUFFERCOUNT=");
                msbpCommand.Append(brp.Buffercount);
            }

            if (brp.Maxtransfersize > 0)
            {
                msbpCommand.Append(";MAXTRANSFERSIZE=");
                msbpCommand.Append(brp.Maxtransfersize);
            }

            if (brp.Blocksize > 0)
            {
                msbpCommand.Append(";BLOCKSIZE=");
                msbpCommand.Append(brp.Blocksize);
            }

            foreach (var s in brp.Filesoption)
            {
                msbpCommand.Append(";FILE=");
                msbpCommand.Append(s.Trim());
            }

            foreach (var s in brp.Filegroupoption)
            {
                msbpCommand.Append(";FILEGROUP=");
                msbpCommand.Append(s.Trim());
            }

            if (!string.IsNullOrEmpty(brp.Username))
            {
                msbpCommand.Append(";username=");
                msbpCommand.Append(brp.Username);
            }

            if (!string.IsNullOrEmpty(brp.Password))
            {
                msbpCommand.Append(";password=");
                msbpCommand.Append(brp.Password);
            }
            msbpCommand.Append("\"");
            msbpCommand.Append(" ");

            if (brp.Compression == 1)
            {
                msbpCommand.Append("\"LZ4\"");
                msbpCommand.Append(" ");
            }

            if (brp.Encryption == 1)
            {
                msbpCommand.Append("\"AES(key=");
                msbpCommand.Append(brp.EncryptionKey);
                msbpCommand.Append(")\"");
                msbpCommand.Append(" ");

            }

            //storage string
            msbpCommand.Append("\"local(");
            foreach (var disk in brp.Backupfiles)
            {
                msbpCommand.Append("path=");
                msbpCommand.Append(disk);
                msbpCommand.Append(";");
            }
            msbpCommand.Append(")\"");
           // brp = null;
            return msbpCommand;
        }

        private void prsProjectTypes_OutputDataReceived(object sender, DataReceivedEventArgs e)
        {
            _output.AppendLine(e.Data);
        }
    }
}