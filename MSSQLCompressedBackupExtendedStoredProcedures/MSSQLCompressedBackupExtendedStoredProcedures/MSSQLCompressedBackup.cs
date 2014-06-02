//------------------------------------------------------------------------------
//------------------------------------------------------------------------------
using BackupRestoreCommandBuilder;

public partial class StoredProcedures
{
    [Microsoft.SqlServer.Server.SqlProcedure]
    public static void MSSQLCompressedBackup(string filename, string arguments)
    {
        ExecuteBackupRestore execmd = new ExecuteBackupRestore();
        execmd.BuildAndExecuteStatement(filename, arguments);
    }
}


