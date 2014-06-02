SET ANSI_NULLS, ANSI_PADDING, ANSI_WARNINGS, ARITHABORT, CONCAT_NULL_YIELDS_NULL, QUOTED_IDENTIFIER ON;
SET NUMERIC_ROUNDABORT OFF;
SET NOCOUNT ON;
GO
/*
 Drop all objects before deployment.
*/
USE master
GO

EXEC sp_configure 'clr enabled', 1 
GO

RECONFIGURE WITH OVERRIDE 
GO

USE DBA
GO

IF EXISTS (SELECT * FROM sys.objects WHERE name = 'MSSQLCompressedBackup')
    DROP PROCEDURE Utility.MSSQLCompressedBackup
GO

IF EXISTS (SELECT * FROM sys.assemblies WHERE name = 'MSSQLCompressedBackupExtendedStoredProcedures')
    DROP ASSEMBLY MSSQLCompressedBackupExtendedStoredProcedures
GO

IF EXISTS (SELECT * FROM sys.database_principals WHERE name = 'Utility')
    DROP USER [Utility]
GO

IF EXISTS (SELECT * FROM sys.server_principals WHERE name = 'Utility')
    DROP LOGIN [Utility]
GO

IF EXISTS(SELECT * FROM sys.schemas WHERE name = 'Utility')
	DROP SCHEMA [Utility]
GO

CREATE LOGIN [Utility]	WITH PASSWORD = 'r$MG0;YhRmeorenjhis4jrpymsFT7_&#$!~<opmvU.kup,zm'
GO

CREATE USER [Utility] FOR LOGIN [Utility]  WITH DEFAULT_SCHEMA = Utility;
GO

GRANT CONNECT TO [Utility]
GO

CREATE SCHEMA [Utility] AUTHORIZATION [dbo];
GO

ALTER DATABASE DBA SET trustworthy ON
GO

EXEC sp_changedbowner 'sa' 
GO

CREATE ASSEMBLY MSSQLCompressedBackupExtendedStoredProcedures
FROM 'C:\DBATools\MSSQLCompressedBackupExtendedStoredProcedures\MSSQLCompressedBackupExtendedStoredProcedures.dll'
WITH PERMISSION_SET = UNSAFE
GO

CREATE PROCEDURE [Utility].[MSSQLCompressedBackup] @pathname [nvarchar](4000), @arguments [nvarchar] (4000)
AS EXTERNAL NAME [MSSQLCompressedBackupExtendedStoredProcedures].[StoredProcedures].[MSSQLCompressedBackup];
GO

