using AcxiomCRM.Models;
using AcxiomCRM.Services;
using AcxiomCRM.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace AcxiomCRM.Controllers;

public class AccountController : Controller
{
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IAuditService _auditService;
    private readonly ICurrentUserService _currentUserService;

    public AccountController(
        SignInManager<ApplicationUser> signInManager,
        UserManager<ApplicationUser> userManager,
        IAuditService auditService,
        ICurrentUserService currentUserService)
    {
        _signInManager = signInManager;
        _userManager = userManager;
        _auditService = auditService;
        _currentUserService = currentUserService;
    }

    [HttpGet]
    [AllowAnonymous]
    public IActionResult Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToAction("Index", "Dashboard");
        }

        ViewData["ReturnUrl"] = returnUrl;
        return View(new LoginViewModel { ReturnUrl = returnUrl });
    }

    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var ip = _currentUserService.GetIpAddress();
        var user = await _userManager.FindByEmailAsync(model.Email);

        if (user != null && !user.IsActive)
        {
            await _auditService.LogAsync(model.Email, "Failed Login", "Auth", user.Id, null, null, "Account is deactivated", ip);
            ModelState.AddModelError(string.Empty, "This account has been deactivated. Please contact an administrator.");
            return View(model);
        }

        // Perform sign-in with lockout tracking
        var result = await _signInManager.PasswordSignInAsync(
            model.Email,
            model.Password,
            model.RememberMe,
            lockoutOnFailure: true);

        if (result.Succeeded)
        {
            await _auditService.LogAsync(model.Email, "Login", "Auth", user?.Id, null, null, "User successfully logged in", ip);

            if (!string.IsNullOrEmpty(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
            {
                return Redirect(model.ReturnUrl);
            }
            return RedirectToAction("Index", "Dashboard");
        }

        if (result.IsLockedOut)
        {
            await _auditService.LogAsync(model.Email, "Failed Login", "Auth", user?.Id, null, null, "Account locked out due to multiple failed attempts", ip);
            ModelState.AddModelError(string.Empty, "This account is temporarily locked out due to multiple failed login attempts. Try again later.");
            return View(model);
        }

        await _auditService.LogAsync(model.Email, "Failed Login", "Auth", user?.Id, null, null, "Invalid credentials provided", ip);
        ModelState.AddModelError(string.Empty, "Invalid email or password.");
        return View(model);
    }

    [HttpGet]
    [AllowAnonymous]
    public IActionResult Register()
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToAction("Index", "Dashboard");
        }
        return View(new RegisterViewModel());
    }

    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        // Enforce safe public registration: public users always get SalesExecutive role
        var assignedRole = "SalesExecutive";

        var user = new ApplicationUser
        {
            UserName = model.Email,
            Email = model.Email,
            FullName = model.FullName,
            EmailConfirmed = true,
            IsActive = true,
            CreatedDate = DateTime.UtcNow
        };

        var result = await _userManager.CreateAsync(user, model.Password);
        if (result.Succeeded)
        {
            await _userManager.AddToRoleAsync(user, assignedRole);

            var ip = _currentUserService.GetIpAddress();
            await _auditService.LogAsync(user.Email, "Register", "Auth", user.Id, null, assignedRole, $"User registered with role {assignedRole}", ip);

            await _signInManager.SignInAsync(user, isPersistent: false);
            return RedirectToAction("Index", "Dashboard");
        }

        foreach (var error in result.Errors)
        {
            ModelState.AddModelError(string.Empty, error.Description);
        }

        return View(model);
    }

    [HttpPost]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        var email = _currentUserService.Email;
        var ip = _currentUserService.GetIpAddress();
        var userId = _currentUserService.UserId;

        await _signInManager.SignOutAsync();
        await _auditService.LogAsync(email, "Logout", "Auth", userId, null, null, "User logged out", ip);

        return RedirectToAction("Login", "Account");
    }

    [HttpGet]
    [AllowAnonymous]
    public IActionResult AccessDenied()
    {
        return View();
    }
}
