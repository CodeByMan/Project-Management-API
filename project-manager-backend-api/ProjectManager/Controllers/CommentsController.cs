using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProjectManager.Data;
using ProjectManager.DTOs;
using ProjectManager.Models;
using System.Security.Claims;

namespace ProjectManager.Controllers
{
    [ApiController]
    [Route("api/task/{taskId}/[controller]")]
    [Authorize]
    public class CommentsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IAuthorizationService _authorizationService;

        public CommentsController(ApplicationDbContext context, IAuthorizationService authorizationService)
        {
            _context = context;
            _authorizationService = authorizationService;
        }

        [HttpPost]
        public async Task<IActionResult> AddCommentToTask(int taskId, [FromBody] CreateCommentDto commentDto)
        {
            var task = await GetTaskForAuthorizationAsync(taskId);
            if (task == null)
            {
                return NotFound(new { Message = "Task not found." });
            }

            var taskAccess = await _authorizationService.AuthorizeAsync(User, task, "CanViewTask");
            if (!taskAccess.Succeeded)
            {
                return StatusCode(403, new { Message = "You do not have permission to comment on this task." });
            }

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(userId))
            {
                return Unauthorized();
            }

            var comment = new Comment
            {
                Content = commentDto.Content,
                CreatedAt = DateTimeOffset.UtcNow,
                AuthorId = userId,
                TaskId = taskId
            };

            _context.Comments.Add(comment);
            await _context.SaveChangesAsync();

            return CreatedAtAction(
                nameof(GetCommentById),
                new { taskId, id = comment.Id },
                new { comment.Id, message = "Comment added." });
        }

        [HttpGet]
        public async Task<IActionResult> GetCommentsForTask(int taskId)
        {
            var task = await GetTaskForAuthorizationAsync(taskId);
            if (task == null)
            {
                return NotFound(new { Message = "Task not found." });
            }

            var taskAccess = await _authorizationService.AuthorizeAsync(User, task, "CanViewTask");
            if (!taskAccess.Succeeded)
            {
                return StatusCode(403, new { Message = "You do not have permission to view comments for this task." });
            }

            var commentDtos = await _context.Comments
                .Where(comment => comment.TaskId == taskId)
                .Select(comment => new CommentResponseDto
                {
                    Id = comment.Id,
                    Content = comment.Content,
                    CreatedAt = comment.CreatedAt,
                    AuthorId = comment.AuthorId,
                    AuthorName = _context.Users
                        .Where(user => user.Id == comment.AuthorId)
                        .Select(user => user.UserName)
                        .FirstOrDefault() ?? "Unknown",
                    TaskId = comment.TaskId
                })
                .OrderByDescending(comment => comment.CreatedAt)
                .ToListAsync();

            return Ok(commentDtos);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetCommentById(int taskId, int id)
        {
            var task = await GetTaskForAuthorizationAsync(taskId);
            if (task == null)
            {
                return NotFound(new { Message = "Task not found." });
            }

            var taskAccess = await _authorizationService.AuthorizeAsync(User, task, "CanViewTask");
            if (!taskAccess.Succeeded)
            {
                return StatusCode(403, new { Message = "You do not have permission to view this comment." });
            }

            var comment = await _context.Comments
                .AsNoTracking()
                .FirstOrDefaultAsync(item => item.Id == id && item.TaskId == taskId);
            if (comment == null)
            {
                return NotFound(new { Message = "Comment not found." });
            }

            return Ok(comment);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteComment(int taskId, int id)
        {
            var task = await GetTaskForAuthorizationAsync(taskId);
            if (task == null)
            {
                return NotFound(new { Message = "Task not found." });
            }

            var taskAccess = await _authorizationService.AuthorizeAsync(User, task, "CanViewTask");
            if (!taskAccess.Succeeded)
            {
                return StatusCode(403, new { Message = "You do not have permission to access this task." });
            }

            var comment = await _context.Comments.FirstOrDefaultAsync(item => item.Id == id && item.TaskId == taskId);
            if (comment == null)
            {
                return NotFound(new { Message = "Comment not found." });
            }

            var authCheck = await _authorizationService.AuthorizeAsync(User, comment, "CanDeleteComment");
            if (!authCheck.Succeeded)
            {
                return StatusCode(403, new { Message = "You do not have permission to delete this comment." });
            }

            _context.Comments.Remove(comment);
            await _context.SaveChangesAsync();
            return NoContent();
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> EditComment(int taskId, int id, [FromBody] EditCommentDto commentDto)
        {
            var task = await GetTaskForAuthorizationAsync(taskId);
            if (task == null)
            {
                return NotFound(new { Message = "Task not found." });
            }

            var taskAccess = await _authorizationService.AuthorizeAsync(User, task, "CanViewTask");
            if (!taskAccess.Succeeded)
            {
                return StatusCode(403, new { Message = "You do not have permission to access this task." });
            }

            var comment = await _context.Comments.FirstOrDefaultAsync(item => item.Id == id && item.TaskId == taskId);
            if (comment == null)
            {
                return NotFound(new { Message = "Comment not found." });
            }

            var authCheck = await _authorizationService.AuthorizeAsync(User, comment, "CanEditComment");
            if (!authCheck.Succeeded)
            {
                return StatusCode(403, new { Message = "You do not have permission to edit this comment." });
            }

            comment.Content = commentDto.Content;
            await _context.SaveChangesAsync();

            return Ok(new Comment
            {
                Id = comment.Id,
                Content = comment.Content,
                CreatedAt = comment.CreatedAt,
                AuthorId = comment.AuthorId,
                TaskId = comment.TaskId,
                Task = null!
            });
        }

        private async Task<ProjectTask?> GetTaskForAuthorizationAsync(int taskId)
        {
            return await _context.Tasks
                .Include(task => task.Project)
                    .ThenInclude(project => project.ProjectUsers)
                .FirstOrDefaultAsync(task => task.Id == taskId);
        }
    }
}
