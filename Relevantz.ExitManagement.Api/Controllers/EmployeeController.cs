using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Relevantz.ExitManagement.Common.DTOs;
using Relevantz.ExitManagement.Core.IService;

namespace Relevantz.ExitManagement.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class EmployeeController : ControllerBase
{
    private readonly IEmployeeService _employeeService;

    public EmployeeController(IEmployeeService employeeService)
    {
        _employeeService = employeeService;
    }

    /// <summary>
    /// Live lookup used on resignation form for handover buddy validation.
    /// Returns only name + department — no sensitive data.
    /// </summary>
    [Authorize(Roles = "EMPLOYEE,MANAGER,HR,ADMIN")]
    [HttpGet("lookup")]
    public async Task<IActionResult> LookupByCode([FromQuery] string code)
    {
        if (string.IsNullOrWhiteSpace(code) || code.Length < 3)
            return BadRequest(new { message = "Employee code must be at least 3 characters." });

        var result = await _employeeService.LookupByCodeAsync(code.Trim().ToUpper());

        if (result == null)
            return NotFound(new { message = $"No employee found with code '{code}'." });

        return Ok(ApiResponse<EmployeeLookupDto>.SuccessResponse(result));
    }
}
