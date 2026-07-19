/*
    Project Manager Backend API
    Required Dapper database objects
    Developer: Muhammad Ali Nawaz

    Run after Entity Framework Core migrations.
    The script is idempotent on SQL Server 2016 SP1 or later because it uses CREATE OR ALTER.
*/

SET NOCOUNT ON;
SET XACT_ABORT ON;
GO

CREATE OR ALTER PROCEDURE dbo.spGetProjects
    @UserId nvarchar(450) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        p.Id,
        p.Name,
        p.Description,
        p.StartDate,
        p.EndDate,
        p.Status,
        p.CreatorId,
        COALESCE(NULLIF(p.CreatorName, ''), creator.FirstName, creator.UserName, 'Unknown') AS CreatorName,
        pu.UserId AS MemberId,
        member.FirstName AS MemberFirstName,
        member.LastName AS MemberLastName,
        member.Email AS MemberEmail
    FROM dbo.Projects AS p
    INNER JOIN dbo.AspNetUsers AS creator
        ON creator.Id = p.CreatorId
    LEFT JOIN dbo.ProjectUsers AS pu
        ON pu.ProjectId = p.Id
    LEFT JOIN dbo.AspNetUsers AS member
        ON member.Id = pu.UserId
    WHERE
        @UserId IS NULL
        OR p.CreatorId = @UserId
        OR EXISTS
        (
            SELECT 1
            FROM dbo.ProjectUsers AS accessMembership
            WHERE accessMembership.ProjectId = p.Id
              AND accessMembership.UserId = @UserId
        )
    ORDER BY p.Id DESC, member.FirstName, member.LastName;
END;
GO

CREATE OR ALTER PROCEDURE dbo.spGetDashboardTasks
    @UserId nvarchar(450) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        t.Id,
        t.Title,
        t.Description,
        t.Status,
        t.Priority,
        t.DueDate,
        t.CreatorId,
        t.ProjectId,
        t.AssignedUserId,
        p.Name AS ProjectName,
        assignedUser.FirstName AS AssignedUserFirstName,
        assignedUser.LastName AS AssignedUserLastName,
        tagData.TagIds,
        tagData.TagNames
    FROM dbo.Tasks AS t
    INNER JOIN dbo.Projects AS p
        ON p.Id = t.ProjectId
    LEFT JOIN dbo.AspNetUsers AS assignedUser
        ON assignedUser.Id = t.AssignedUserId
    OUTER APPLY
    (
        SELECT
            STRING_AGG(CONVERT(varchar(20), tt.TagId), ',') AS TagIds,
            STRING_AGG(tag.Name, ',') AS TagNames
        FROM dbo.TaskTags AS tt
        INNER JOIN dbo.Tags AS tag
            ON tag.Id = tt.TagId
        WHERE tt.TaskId = t.Id
    ) AS tagData
    WHERE
        @UserId IS NULL
        OR p.CreatorId = @UserId
        OR t.AssignedUserId = @UserId
    ORDER BY t.DueDate, t.Id;
END;
GO

CREATE OR ALTER PROCEDURE dbo.spGetProjectTasks
    @ProjectId int,
    @UserId nvarchar(450) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        t.Id,
        t.Title,
        t.Description,
        t.Status,
        t.Priority,
        t.DueDate,
        t.CreatorId,
        t.ProjectId,
        t.AssignedUserId,
        p.Name AS ProjectName,
        assignedUser.FirstName AS AssignedUserFirstName,
        assignedUser.LastName AS AssignedUserLastName,
        tagData.TagIds,
        tagData.TagNames
    FROM dbo.Tasks AS t
    INNER JOIN dbo.Projects AS p
        ON p.Id = t.ProjectId
    LEFT JOIN dbo.AspNetUsers AS assignedUser
        ON assignedUser.Id = t.AssignedUserId
    OUTER APPLY
    (
        SELECT
            STRING_AGG(CONVERT(varchar(20), tt.TagId), ',') AS TagIds,
            STRING_AGG(tag.Name, ',') AS TagNames
        FROM dbo.TaskTags AS tt
        INNER JOIN dbo.Tags AS tag
            ON tag.Id = tt.TagId
        WHERE tt.TaskId = t.Id
    ) AS tagData
    WHERE
        t.ProjectId = @ProjectId
        AND
        (
            @UserId IS NULL
            OR p.CreatorId = @UserId
            OR t.AssignedUserId = @UserId
        )
    ORDER BY t.DueDate, t.Id;
END;
GO

CREATE OR ALTER FUNCTION dbo.fnOverdueTasksCount
(
    @CreatorId nvarchar(450)
)
RETURNS int
AS
BEGIN
    DECLARE @Count int;

    SELECT @Count = COUNT(*)
    FROM dbo.Tasks AS t
    INNER JOIN dbo.Projects AS p
        ON p.Id = t.ProjectId
    WHERE p.CreatorId = @CreatorId
      AND t.DueDate < SYSUTCDATETIME()
      AND t.Status <> 'Done';

    RETURN ISNULL(@Count, 0);
END;
GO
