MSSQL Compressed Backup

Getting Started:
MSSQL Compressed Backup requires the following components to be installed before you can use it.  These are typcially installed with SQL Server 2005.
 * .Net 2.0
 
To get started, unzip the archive to a folder, open the Windows Command Prompt and type "msbp.exe help"

A standard backup command would be:

msbp.exe backup "db(database=model)" "zip64(level=3)" "local(path=c:\model.bak.zip)"

Pipes:
MSSQL Compressed Backup pipes data from one plugin to the next.  For example, you can pipe the backup data from gzip to bzip2 to a file if you wanted backup like database.bak.gz.bz2.  Piping might one day be used to compress and encrypt the backup file.

One other standard plugin is the "rate" plugin which limits the speed of the backup.  This can be used to ensure that the backups do not overload the system.

