using AcxiomCRM.DTOs;
using AcxiomCRM.Models;
using AcxiomCRM.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace AcxiomCRM.Controllers.Api;

[ApiController]
[Route("api/[controller]")]
[Route("api/auth")]
public class AuthApiController : ControllerBase
{
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IAuditService _auditService;
    private readonly ICurrentUserService _currentUser;

    public AuthApiController(
        SignInManager<ApplicationUser> signInManager,
        UserManager<ApplicationUser> userManager,
        IAuditService auditService,
        ICurrentUserService currentUser)
    {
        _signInManager = signInManager;
        _userManager = userManager;
        _auditService = auditService;
        _currentUser = currentUser;
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequestDto model)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var ip = _currentUser.GetIpAddress();
        var user = await _userManager.FindByEmailAsync(model.Email);

        if (user != null && !user.IsActive)
        {
            await _auditService.LogAsync(model.Email, "Failed Login", "Auth", user.Id, null, null, "Account deactivated (via API)", ip);
            return Unauthorized(new { message = "Account has been deactivated." });
        }

        var result = await _signInManager.PasswordSignInAsync(model.Email, model.Password, isPersistent: false, lockoutOnFailure: true);

        if (result.Succeeded)
        {
            var roles = user != null ? await _userManager.GetRolesAsync(user) : new List<string>();
            await _auditService.LogAsync(model.Email, "Login", "Auth", user?.Id, null, null, "API login successful", ip);

            return Ok(new
            {
                message = "Login successful.",
                userId = user?.Id,
                email = user?.Email,
                fullName = user?.FullName,
                roles = roles
            });
        }

        if (result.IsLockedOut)
        {
            await _auditService.LogAsync(model.Email, "Failed Login", "Auth", user?.Id, null, null, "Account locked out (via API)", ip);
            return StatusCode(423, new { message = "Account is locked out due to multiple failed attempts." });
        }

        await _auditService.LogAsync(model.Email, "Failed Login", "Auth", user?.Id, null, null, "Invalid credentials (via API)", ip);
        return Unauthorized(new { message = "Invalid email or password." });
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        var email = _currentUser.Email;
        var userId = _currentUser.UserId;
        var ip = _currentUser.GetIpAddress();

        await _signInManager.SignOutAsync();
        await _auditService.LogAsync(email, "Logout", "Auth", userId, null, null, "API logout successful", ip);

        return Ok(new { message = "Logged out successfully." });
    }
}
