using Microsoft.AspNetCore.Identity;
using ProjectManager.Models;

namespace ProjectManager.Services
{
    public static class DataSeeder
    {
        private const string AdminRole = "Admin";
        private const string ManagerRole = "Manager";
        private const string MemberRole = "Member";

        public static async Task InitializeAsync(
            UserManager<ApplicationUser> userManager,
            RoleManager<IdentityRole> roleManager,
            IConfiguration configuration,
            ILogger logger)
        {
            await CreateRoleIfNotExists(roleManager, AdminRole);
            await CreateRoleIfNotExists(roleManager, ManagerRole);
            await CreateRoleIfNotExists(roleManager, MemberRole);

            if (!configuration.GetValue<bool>("SeedAdmin:Enabled"))
            {
                return;
            }

            var adminEmail = configuration["SeedAdmin:Email"];
            var adminPassword = configuration["SeedAdmin:Password"];
            var adminPhoneNumber = configuration["SeedAdmin:PhoneNumber"];

            if (string.IsNullOrWhiteSpace(adminEmail) || string.IsNullOrWhiteSpace(adminPassword))
            {
                logger.LogWarning("Admin seeding is enabled, but SeedAdmin credentials are incomplete. The admin account was not created.");
                return;
            }

            if (await userManager.FindByEmailAsync(adminEmail) != null)
            {
                return;
            }

            var adminUser = new ApplicationUser
            {
                UserName = adminEmail,
                Email = adminEmail,
                PhoneNumber = adminPhoneNumber,
                FirstName = "Administrator",
                LastName = "N/A",
                EmailConfirmed = true
            };

            var result = await userManager.CreateAsync(adminUser, adminPassword);
            if (!result.Succeeded)
            {
                logger.LogError("Configured admin account creation failed: {Errors}",
                    string.Join("; ", result.Errors.Select(error => error.Code)));
                return;
            }

            var roleResult = await userManager.AddToRoleAsync(adminUser, AdminRole);
            if (!roleResult.Succeeded)
            {
                await userManager.DeleteAsync(adminUser);
                logger.LogError("Configured admin role assignment failed: {Errors}",
                    string.Join("; ", roleResult.Errors.Select(error => error.Code)));
            }
        }

        private static async Task CreateRoleIfNotExists(RoleManager<IdentityRole> roleManager, string roleName)
        {
            if (await roleManager.FindByNameAsync(roleName) == null)
            {
                await roleManager.CreateAsync(new IdentityRole(roleName));
            }
        }
    }
}
