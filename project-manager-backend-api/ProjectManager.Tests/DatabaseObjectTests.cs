using Dapper;
using Microsoft.Data.SqlClient;
using ProjectManager.DTOs;
using System.Data;

namespace ProjectManager.Tests;

public class DatabaseObjectTests
{
    private static readonly string[] RequiredObjects =
    {
        "spGetProjects",
        "spGetDashboardTasks",
        "spGetProjectTasks",
        "fnOverdueTasksCount"
    };

    [Fact]
    public void VersionControlledSql_ContainsEveryDapperObjectReferencedByRepositories()
    {
        var baseDirectory = AppContext.BaseDirectory;
        var script = File.ReadAllText(Path.Combine(baseDirectory, "Database", "Scripts", "001_DapperObjects.sql"));
        var repositorySource = string.Join(Environment.NewLine,
            Directory.GetFiles(Path.Combine(baseDirectory, "Source", "Repositories"), "*.cs")
                .Select(File.ReadAllText));

        foreach (var databaseObject in RequiredObjects)
        {
            Assert.Contains(databaseObject, script, StringComparison.OrdinalIgnoreCase);
            Assert.Contains(databaseObject, repositorySource, StringComparison.OrdinalIgnoreCase);
        }
    }


    [Fact]
    public void VerificationScript_FailsFastForEveryMissingRequiredObject()
    {
        var verificationScript = File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "Database", "Scripts", "002_VerifyDatabaseObjects.sql"));

        foreach (var databaseObject in RequiredObjects)
        {
            Assert.Contains(databaseObject, verificationScript, StringComparison.OrdinalIgnoreCase);
        }

        Assert.Contains("THROW", verificationScript, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task DapperObjects_ExecuteWhenIntegrationConnectionStringIsProvided()
    {
        var connectionString = Environment.GetEnvironmentVariable("PROJECTMANAGER_TEST_CONNECTION_STRING");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return;
        }

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();

        var projects = await connection.QueryAsync<FlatProjectResult>(
            "spGetProjects",
            new { UserId = (string?)null },
            commandType: CommandType.StoredProcedure);
        var dashboardTasks = await connection.QueryAsync<FlatTaskResult>(
            "spGetDashboardTasks",
            new { UserId = (string?)null },
            commandType: CommandType.StoredProcedure);
        var projectTasks = await connection.QueryAsync<FlatTaskResult>(
            "spGetProjectTasks",
            new { ProjectId = -1, UserId = (string?)null },
            commandType: CommandType.StoredProcedure);
        var overdueCount = await connection.ExecuteScalarAsync<int>(
            "SELECT dbo.fnOverdueTasksCount(@CreatorId)",
            new { CreatorId = "integration-test-user" });

        Assert.NotNull(projects);
        Assert.NotNull(dashboardTasks);
        Assert.NotNull(projectTasks);
        Assert.True(overdueCount >= 0);
    }
}
