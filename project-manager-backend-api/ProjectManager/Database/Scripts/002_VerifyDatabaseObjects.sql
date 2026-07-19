/* Run after 001_DapperObjects.sql. The script fails when a required object is missing. */
SET NOCOUNT ON;

IF OBJECT_ID(N'dbo.spGetProjects', N'P') IS NULL
    THROW 51000, 'Missing required stored procedure dbo.spGetProjects.', 1;

IF OBJECT_ID(N'dbo.spGetDashboardTasks', N'P') IS NULL
    THROW 51000, 'Missing required stored procedure dbo.spGetDashboardTasks.', 1;

IF OBJECT_ID(N'dbo.spGetProjectTasks', N'P') IS NULL
    THROW 51000, 'Missing required stored procedure dbo.spGetProjectTasks.', 1;

IF OBJECT_ID(N'dbo.fnOverdueTasksCount', N'FN') IS NULL
    THROW 51000, 'Missing required function dbo.fnOverdueTasksCount.', 1;

PRINT 'All required Dapper database objects are installed.';
GO
