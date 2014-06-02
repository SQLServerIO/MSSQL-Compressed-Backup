USE master
GO

EXEC sp_configure 'clr enabled', 1 
GO

RECONFIGURE WITH OVERRIDE 
GO

USE master
GO 

IF EXISTS (SELECT * FROM sys.server_principals WHERE name = 'FileSystemHelperLogin')
    DROP LOGIN [FileSystemHelperLogin]
GO

IF EXISTS (SELECT * FROM sys.asymmetric_keys WHERE name = 'FileSystemHelperKey')
    DROP ASYMMETRIC KEY [FileSystemHelperKey]
GO

USE [AdventureWorks]
GO

IF EXISTS (SELECT * FROM sys.objects WHERE name = 'DirectoryList')
    DROP FUNCTION Utility.DirectoryList
GO

IF EXISTS (SELECT * FROM sys.objects WHERE name = 'DirectoryCreate')
    DROP PROCEDURE Utility.DirectoryCreate
GO

IF EXISTS (SELECT * FROM sys.objects WHERE name = 'DirectoryDelete')
    DROP PROCEDURE Utility.DirectoryDelete
GO

IF EXISTS (SELECT * FROM sys.objects WHERE name = 'DirectoryDeleteContents')
    DROP PROCEDURE Utility.DirectoryDeleteContents
GO

IF EXISTS (SELECT * FROM sys.assemblies WHERE name = 'FileSystemHelper')
    DROP ASSEMBLY FileSystemHelper
GO

IF EXISTS (SELECT * FROM sys.database_principals WHERE name = 'FileSystemHelperLogin')
    DROP USER [FileSystemHelperLogin]
GO

USE master
GO 

-- First Create the Asymmetric Key from the Assembly
CREATE ASYMMETRIC KEY FileSystemHelperKey
FROM EXECUTABLE FILE = 'C:\FileSystemHelper\FileSystemHelper\bin\Debug\FileSystemHelper.dll'
GO

-- Create the Login from the Asymmetric Key
CREATE LOGIN FileSystemHelperLogin FROM ASYMMETRIC KEY FileSystemHelperKey
GO

-- Grant the External Access Priviledge to the Login
GRANT EXTERNAL ACCESS ASSEMBLY TO FileSystemHelperLogin
GO

USE [AdventureWorks]
GO

IF NOT EXISTS(SELECT * FROM sys.schemas WHERE name = 'Utility')
    EXEC ('CREATE SCHEMA [Utility]')
GO

-- Add a database user in the SQLCLR_Net Database for the Login
CREATE USER [FileSystemHelperLogin] FOR LOGIN [FileSystemHelperLogin]
GO

CREATE ASSEMBLY FileSystemHelper
FROM 'C:\FileSystemHelper\FileSystemHelper\bin\Debug\FileSystemHelper.dll'
WITH PERMISSION_SET = EXTERNAL_ACCESS
GO

CREATE FUNCTION Utility.DirectoryList(@Path nvarchar(255), @Filter nvarchar(255))
RETURNS TABLE (
	[name] [nvarchar](max) NULL,
	[directory] [bit] NULL,
	[size] [bigint] NULL,
	[date_created] [datetime] NULL,
	[date_modified] [datetime] NULL,
	[extension] [nvarchar](max) NULL
) WITH EXECUTE AS CALLER
AS
EXTERNAL NAME FileSystemHelper.UserDefinedFunctions.DirectoryList
GO

CREATE PROCEDURE Utility.DirectoryCreate(@Path nvarchar(255))
AS
EXTERNAL NAME FileSystemHelper.StoredProcedures.DirectoryCreate
GO

CREATE PROCEDURE Utility.DirectoryDelete(@Path nvarchar(255))
AS
EXTERNAL NAME FileSystemHelper.StoredProcedures.DirectoryDelete
GO

CREATE PROCEDURE Utility.DirectoryDeleteContents(@Path nvarchar(255), @DaysToKeep smallint, @FileExtension nvarchar(255))
AS
EXTERNAL NAME FileSystemHelper.StoredProcedures.DirectoryDeleteContents
go