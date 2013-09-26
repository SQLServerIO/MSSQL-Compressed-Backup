MSSQL Compressed Backup
By
Clay Lenhart <clay@lenharts.net>

Contributions By:
Wes Brown <wes@planetarydb.com>

Getting Started:
MSSQL Compressed Backup requires the following components to be installed before you can use it.  These are typically installed with SQL Server 2005.
 * .Net 2.0
 
To get started, unzip the archive to a folder, open the Windows Command Prompt and type "msbp.exe help"

A standard backup command consists of the operation source database any plugins you wish to use and a target file or files.
Eample:
msbp.exe backup "db(database=model)" "zip64(level=3)" "local(path=c:\model.bak.zip)"

To restore your database you must reverse the order of the source and destination adding any plugins as normal.
Example:
msbp.exe restore "local(path=c:\model.bak.zip)" LZ4 "db(database = ds2;)"

Pipes:
MSSQL Compressed Backup pipes data from one plug-in to the next.  For example, you can pipe the backup data from gzip to bzip2 to a file if you wanted backup like database.bak.gz.bz2.  
Every pipe follows the "<pipe>(<option=value>;<option2=value>)" format. There shouldn't be any quotes inside the () part of the command statement.

Currently Implemented Pipes:

Database:
This includes the standard backup and restore commands that normally follow the WITH clause.
This is always first in a backup command and always last in a restore command

Compression:
zip64
msbp.exe backup "db(database=model)" "zip64(level=3)" "local(path=c:\model.bak.zip)"
bzip2
msbp.exe backup "db(database=model)" "bzip2" "local(path=c:\model.bak.zip)"
gzip
msbp.exe backup "db(database=model)" "gzip(level=3)" "local(path=c:\model.bak.zip)"
LZ4
msbp.exe backup "db(database=model)" "LZ4" "local(path=c:\model.bak.zip)"

Encryption:
AES
msbp.exe backup "db(database=model)" "AES(key=123456)" "local(path=c:\model.bak.zip)"
Blowfish
msbp.exe backup "db(database=model)" "Blowfish(key=123456)" "local(path=c:\model.bak.zip)"
Rijndael
msbp.exe backup "db(database=model)" "Rijndael(key=123456)" "local(path=c:\model.bak.zip)"
TripleDES
msbp.exe backup "db(database=model)" "TripleDES(key=123456)" "local(path=c:\model.bak.zip)"

Filters:
rate
Limits the speed of the backup.  This can be used to ensure that the backups do not overload the system.
msbp.exe backup "db(database=model)" "(rateMB=10.0)" "local(path=c:\model.bak.zip)"

Storage:
This is always the last pipe for the backup command and always the first for a restore command. 
It contains the destination of every file you are writing or reading from.
This would be equal to the backup database <db> TO DISK = c:\model.bak.zip statement.
If you want to increase the throughput you can specify more than one target.
Example:
msbp.exe backup "db(database=model)" "local(path=c:\model1.bak.zip;path=c:\model2.bak.zip;path=c:\model3.bak.zip)"

You can combine pipes
msbp.exe backup "db(database=model)" "zip64(level=3)" "AES(key=123456)" "rate(rateMB=10.0)" "local(path=c:\model.bak.zip)"