//------------------------------------------------------------------------------
//------------------------------------------------------------------------------

using MSSQLCompressedBackupExtendedStoredProcedures;

public partial class StoredProcedures
{
    [Microsoft.SqlServer.Server.SqlProcedure]
    public static void MSSQLCompressedBackup(string filename, string arguments)
    {
        var execmd = new ExecuteBackupRestore();
        execmd.BuildAndExecuteStatement(filename, arguments);
    }
}


