using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace AcxiomCRM.Services;

public class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    private ClaimsPrincipal? User => _httpContextAccessor.HttpContext?.User;

    public string? UserId => User?.FindFirstValue(ClaimTypes.NameIdentifier);
    public string? UserName => User?.Identity?.Name;
    public string? Email => User?.FindFirstValue(ClaimTypes.Email) ?? User?.Identity?.Name;
    public bool IsAuthenticated => User?.Identity?.IsAuthenticated ?? false;
    public bool IsAdmin => User?.IsInRole("Admin") ?? false;
    public bool IsManager => User?.IsInRole("Manager") ?? false;
    public bool IsSalesExecutive => User?.IsInRole("SalesExecutive") ?? false;

    public string? GetIpAddress()
    {
        var context = _httpContextAccessor.HttpContext;
        if (context == null) return null;

        var ip = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (string.IsNullOrEmpty(ip))
        {
            ip = context.Connection.RemoteIpAddress?.ToString();
        }
        return ip;
    }
}
