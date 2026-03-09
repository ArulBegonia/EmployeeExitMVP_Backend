using Microsoft.AspNetCore.Mvc;
using Relevantz.ExitManagement.Common.DTOs;
using Relevantz.ExitManagement.Core.IService;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;

namespace Relevantz.ExitManagement.Api.Controllers;
 
[Authorize]
[ApiController]
[Route("api/[controller]")]
public class NotificationController : ControllerBase
{
    private readonly IExitService _exitService;
 
    public NotificationController(IExitService exitService)
    {
        _exitService = exitService;
    }
 
    [HttpGet("my")]
    public async Task<IActionResult> GetMyNotifications()
    {
        var employeeId = int.Parse(User.FindFirst("empId")!.Value);
 
        var notifications =
            await _exitService.GetMyNotificationsAsync(employeeId);
 
        return Ok(notifications);
    }
 
    [HttpPost("mark-read/{id}")]
    public async Task<IActionResult> MarkAsRead(int id)
    {
        await _exitService.MarkNotificationAsReadAsync(id);
        return Ok();
    }
}
 