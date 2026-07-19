using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using ProjectManager.Data;
using ProjectManager.DTOs;
using ProjectManager.Models;
using System.Security.Claims;

namespace ProjectManager.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AdminController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;

        public AdminController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            RoleManager<IdentityRole> roleManager)
        {
            _context = context;
            _userManager = userManager;
            _roleManager = roleManager;
        }

        [Authorize(Roles = "Admin")]
        [HttpPost("create-user")]
        public async Task<IActionResult> CreateUser([FromBody] AdminCreateUserDto userDto)
        {
            if (!await _roleManager.RoleExistsAsync(userDto.Role))
            {
                return BadRequest(new { Message = "The requested role does not exist." });
            }

            var existingUser = await _userManager.FindByEmailAsync(userDto.Email);
            if (existingUser != null)
            {
                return BadRequest(new { Message = "Email address is already in use." });
            }

            var user = new ApplicationUser
            {
                UserName = userDto.Email,
                Email = userDto.Email,
                FirstName = userDto.FirstName,
                LastName = userDto.LastName,
                EmailConfirmed = true
            };

            var addUserResult = await _userManager.CreateAsync(user, userDto.Password);
            if (!addUserResult.Succeeded)
            {
                return BadRequest(new { Message = "User creation failed.", Errors = addUserResult.Errors });
            }

            var addUserRole = await _userManager.AddToRoleAsync(user, userDto.Role);
            if (!addUserRole.Succeeded)
            {
                await _userManager.DeleteAsync(user);
                return BadRequest(new { Message = "Assigning role failed.", Errors = addUserRole.Errors });
            }

            return Ok(new { Message = "User created successfully." });
        }

        [Authorize(Roles = "Admin,Manager")]
        [HttpGet("users")]
        public async Task<IActionResult> GetAllUsers()
        {
            IList<ApplicationUser> users;

            if (User.IsInRole("Manager") && !User.IsInRole("Admin"))
            {
                users = await _userManager.GetUsersInRoleAsync("Member");
            }
            else
            {
                var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                users = await _userManager.Users
                    .Where(user => user.Id != currentUserId)
                    .ToListAsync();
            }

            var usersWithRoles = new List<object>();
            foreach (var user in users)
            {
                var roles = await _userManager.GetRolesAsync(user);
                usersWithRoles.Add(new
                {
                    user.Id,
                    user.Email,
                    user.FirstName,
                    user.LastName,
                    Role = roles.FirstOrDefault() ?? "None"
                });
            }

            return Ok(usersWithRoles);
        }

        [Authorize(Roles = "Admin")]
        [HttpDelete("delete-user/{userId}")]
        public async Task<IActionResult> DeleteUser(string userId)
        {
            var adminUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (adminUserId == userId)
            {
                return BadRequest(new { Message = "Administrators cannot delete their own account." });
            }

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                return NotFound(new { Message = "User not found." });
            }

            var deleteResult = await _userManager.DeleteAsync(user);
            if (!deleteResult.Succeeded)
            {
                return BadRequest(new { Message = "User deletion failed.", Errors = deleteResult.Errors });
            }

            return Ok(new { Message = "User deleted successfully." });
        }

        [Authorize(Roles = "Admin")]
        [HttpPost("change-role/{userId}")]
        public async Task<IActionResult> ChangeUserRole(string userId, [FromBody] ChangeUserRoleDto roleDto)
        {
            var adminUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (adminUserId == userId)
            {
                return BadRequest(new { Message = "Administrators cannot change their own role." });
            }

            if (!await _roleManager.RoleExistsAsync(roleDto.NewRole))
            {
                return BadRequest(new { Message = "The requested role does not exist." });
            }

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                return NotFound(new { Message = "User not found." });
            }

            IdentityResult? removeRolesResult = null;
            IdentityResult? addRoleResult = null;
            IdentityResult? securityStampResult = null;
            var executionStrategy = _context.Database.CreateExecutionStrategy();

            await executionStrategy.ExecuteAsync(async () =>
            {
                await using var transaction = await _context.Database.BeginTransactionAsync();
                var currentRoles = await _userManager.GetRolesAsync(user);

                removeRolesResult = await _userManager.RemoveFromRolesAsync(user, currentRoles);
                if (!removeRolesResult.Succeeded)
                {
                    await transaction.RollbackAsync();
                    return;
                }

                addRoleResult = await _userManager.AddToRoleAsync(user, roleDto.NewRole);
                if (!addRoleResult.Succeeded)
                {
                    await transaction.RollbackAsync();
                    return;
                }

                securityStampResult = await _userManager.UpdateSecurityStampAsync(user);
                if (!securityStampResult.Succeeded)
                {
                    await transaction.RollbackAsync();
                    return;
                }

                await transaction.CommitAsync();
            });

            if (removeRolesResult is { Succeeded: false })
            {
                return BadRequest(new { Message = "Removing existing roles failed.", Errors = removeRolesResult.Errors });
            }

            if (addRoleResult is { Succeeded: false })
            {
                return BadRequest(new { Message = "Assigning new role failed.", Errors = addRoleResult.Errors });
            }

            if (securityStampResult is { Succeeded: false })
            {
                return BadRequest(new { Message = "Invalidating existing access tokens failed.", Errors = securityStampResult.Errors });
            }

            return Ok(new { Message = "User role updated successfully." });
        }
    }
}
