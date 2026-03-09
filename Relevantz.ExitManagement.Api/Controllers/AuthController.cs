using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Relevantz.ExitManagement.Common.DTOs;
using Relevantz.ExitManagement.Data.DBContexts;

namespace Relevantz.ExitManagement.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly ExitManagementDbContext _context;
    private readonly IConfiguration          _config;

    public AuthController(ExitManagementDbContext context, IConfiguration config)
    {
        _context = context;
        _config  = config;
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequestDto request)
    {
        // ── ModelState check ──
        if (!ModelState.IsValid)
            return BadRequest(new
            {
                message = "Validation failed.",
                errors  = ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage)
            });

        // ── Sanitise ──
        var email = request.Email.Trim().ToLower();

        // ── Find employee by email only first ──
        var user = await _context.Employees
            .FirstOrDefaultAsync(e =>
                e.Email.ToLower() == email &&
                e.IsActive);

        if (user is null)
            return Unauthorized(new { message = "Invalid credentials." });

        // ── Plain-text password check (BCrypt-ready: swap when hashing is added) ──
        var passwordValid = user.Password == request.Password;
        // Future: var passwordValid = BCrypt.Net.BCrypt.Verify(request.Password, user.Password);

        if (!passwordValid)
            return Unauthorized(new { message = "Invalid credentials." });

        // ── Role lookup ──
        var role = await _context.Roles
            .FirstOrDefaultAsync(r => r.Id == user.RoleId);

        if (role is null)
            return Unauthorized(new { message = "User role not configured." });

        // ── Build token ──
        var jwtSecret  = _config["JwtSettings:Secret"]
            ?? throw new InvalidOperationException("JWT secret is not configured.");
        var expiryHours = int.TryParse(_config["JwtSettings:ExpiryHours"], out var h) ? h : 2;

        var claims = new List<Claim>
        {
            new("empId",          user.Id.ToString()),
            new("employeeCode",   user.EmployeeCode),
            new("firstName",      user.FirstName),
            new("lastName",       user.LastName),
            new("email",          user.Email),
            new(ClaimTypes.Role,  role.Name),
        };

        var key   = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            claims:            claims,
            expires:           DateTime.UtcNow.AddHours(expiryHours),
            signingCredentials: creds);

        var tokenString = new JwtSecurityTokenHandler().WriteToken(token);

        return Ok(new
        {
            token        = tokenString,
            employeeId   = user.Id,
            employeeCode = user.EmployeeCode,
            firstName    = user.FirstName,
            lastName     = user.LastName,
            email        = user.Email,
            role         = role.Name,
            expiresAt    = DateTime.UtcNow.AddHours(expiryHours)
        });
    }
}
