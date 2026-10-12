using AcxiomCRM.Models;
using AcxiomCRM.Services;
using AcxiomCRM.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace AcxiomCRM.Controllers;

[Authorize(Roles = "Admin")]
public class UsersController : Controller
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly IAuditService _auditService;
    private readonly ICurrentUserService _currentUser;

    public UsersController(
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole> roleManager,
        IAuditService auditService,
        ICurrentUserService currentUser)
    {
        _userManager = userManager;
        _roleManager = roleManager;
        _auditService = auditService;
        _currentUser = currentUser;
    }

    public async Task<IActionResult> Index()
    {
        var users = await _userManager.Users.ToListAsync();
        var modelList = new List<UserListItemViewModel>();

        foreach (var u in users)
        {
            var roles = await _userManager.GetRolesAsync(u);
            var isLocked = await _userManager.IsLockedOutAsync(u);

            modelList.Add(new UserListItemViewModel
            {
                Id = u.Id,
                FullName = u.FullName,
                Email = u.Email ?? "",
                Role = roles.FirstOrDefault() ?? "None",
                IsActive = u.IsActive,
                IsLockedOut = isLocked,
                CreatedDate = u.CreatedDate
            });
        }

        return View(modelList);
    }

    public IActionResult Create()
    {
        PopulateRolesDropdown();
        return View(new CreateUserViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateUserViewModel model)
    {
        if (ModelState.IsValid)
        {
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
                if (!string.IsNullOrEmpty(model.Role) && await _roleManager.RoleExistsAsync(model.Role))
                {
                    await _userManager.AddToRoleAsync(user, model.Role);
                }

                var ip = _currentUser.GetIpAddress();
                await _auditService.LogAsync(
                    _currentUser.UserName,
                    "Create User",
                    "User",
                    user.Id,
                    null,
                    model.Role,
                    $"Created user {user.Email} with role {model.Role}",
                    ip);

                TempData["SuccessMessage"] = $"User '{user.Email}' created successfully with role '{model.Role}'!";
                return RedirectToAction(nameof(Index));
            }

            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }
        }

        PopulateRolesDropdown();
        return View(model);
    }

    public async Task<IActionResult> Edit(string id)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user == null)
        {
            return NotFound();
        }

        var roles = await _userManager.GetRolesAsync(user);
        var model = new EditUserViewModel
        {
            Id = user.Id,
            FullName = user.FullName,
            Email = user.Email ?? "",
            Role = roles.FirstOrDefault() ?? "SalesExecutive",
            IsActive = user.IsActive
        };

        PopulateRolesDropdown();
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(string id, EditUserViewModel model)
    {
        if (id != model.Id)
        {
            return BadRequest();
        }

        var user = await _userManager.FindByIdAsync(id);
        if (user == null)
        {
            return NotFound();
        }

        if (ModelState.IsValid)
        {
            user.FullName = model.FullName;
            user.IsActive = model.IsActive;

            var result = await _userManager.UpdateAsync(user);
            if (result.Succeeded)
            {
                var currentRoles = await _userManager.GetRolesAsync(user);
                var oldRole = currentRoles.FirstOrDefault() ?? "None";

                if (oldRole != model.Role)
                {
                    await _userManager.RemoveFromRolesAsync(user, currentRoles);
                    if (await _roleManager.RoleExistsAsync(model.Role))
                    {
                        await _userManager.AddToRoleAsync(user, model.Role);
                    }
                }

                var ip = _currentUser.GetIpAddress();
                await _auditService.LogAsync(
                    _currentUser.UserName,
                    "Update User",
                    "User",
                    user.Id,
                    oldRole,
                    model.Role,
                    $"Updated user {user.Email} (Role: {model.Role}, Active: {model.IsActive})",
                    ip);

                TempData["SuccessMessage"] = $"User '{user.Email}' updated successfully!";
                return RedirectToAction(nameof(Index));
            }

            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }
        }

        PopulateRolesDropdown();
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleLockout(string id)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user == null)
        {
            return NotFound();
        }

        var isLocked = await _userManager.IsLockedOutAsync(user);
        string actionName;

        if (isLocked)
        {
            await _userManager.SetLockoutEndDateAsync(user, null);
            await _userManager.ResetAccessFailedCountAsync(user);
            actionName = "Unlock User";
            TempData["SuccessMessage"] = $"User '{user.Email}' has been unlocked.";
        }
        else
        {
            await _userManager.SetLockoutEndDateAsync(user, DateTimeOffset.UtcNow.AddYears(100));
            actionName = "Lock User";
            TempData["SuccessMessage"] = $"User '{user.Email}' has been locked out.";
        }

        var ip = _currentUser.GetIpAddress();
        await _auditService.LogAsync(
            _currentUser.UserName,
            actionName,
            "User",
            user.Id,
            isLocked ? "Locked" : "Unlocked",
            isLocked ? "Unlocked" : "Locked",
            $"{actionName} performed on {user.Email}",
            ip);

        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> ResetPassword(string id)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user == null)
        {
            return NotFound();
        }

        var model = new ResetUserPasswordViewModel
        {
            UserId = user.Id,
            Email = user.Email ?? ""
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPassword(ResetUserPasswordViewModel model)
    {
        var user = await _userManager.FindByIdAsync(model.UserId);
        if (user == null)
        {
            return NotFound();
        }

        if (ModelState.IsValid)
        {
            var token = await _userManager.GeneratePasswordResetTokenAsync(user);
            var result = await _userManager.ResetPasswordAsync(user, token, model.NewPassword);

            if (result.Succeeded)
            {
                var ip = _currentUser.GetIpAddress();
                await _auditService.LogAsync(
                    _currentUser.UserName,
                    "Reset Password",
                    "Security",
                    user.Id,
                    null,
                    null,
                    $"Administrator reset password for user {user.Email}",
                    ip);

                TempData["SuccessMessage"] = $"Password for '{user.Email}' was reset successfully!";
                return RedirectToAction(nameof(Index));
            }

            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }
        }

        return View(model);
    }

    private void PopulateRolesDropdown()
    {
        var roles = new[] { "Admin", "Manager", "SalesExecutive" };
        ViewBag.Roles = new SelectList(roles);
    }
}
