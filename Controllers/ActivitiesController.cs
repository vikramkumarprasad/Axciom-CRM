using AcxiomCRM.Data;
using AcxiomCRM.Models;
using AcxiomCRM.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace AcxiomCRM.Controllers;

[Authorize]
public class ActivitiesController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly IAuditService _auditService;
    private readonly ICurrentUserService _currentUser;

    public ActivitiesController(ApplicationDbContext context, IAuditService auditService, ICurrentUserService currentUser)
    {
        _context = context;
        _auditService = auditService;
        _currentUser = currentUser;
    }

    public async Task<IActionResult> Index(string? type, string? status, string? search)
    {
        var query = _context.Activities
            .Include(a => a.Customer)
            .Include(a => a.Lead)
            .AsQueryable();

        // Scope access
        if (_currentUser.IsSalesExecutive && !_currentUser.IsAdmin && !_currentUser.IsManager)
        {
            query = query.Where(a => a.AssignedTo == _currentUser.UserId);
        }

        if (!string.IsNullOrWhiteSpace(type))
        {
            query = query.Where(a => a.ActivityType == type);
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(a => a.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLower();
            query = query.Where(a => a.Subject.ToLower().Contains(s) || (a.Description != null && a.Description.ToLower().Contains(s)));
        }

        ViewData["CurrentType"] = type;
        ViewData["CurrentStatus"] = status;
        ViewData["CurrentSearch"] = search;

        var activities = await query.OrderByDescending(a => a.ActivityDate).ToListAsync();
        await PopulateDropdownsAsync();

        return View(activities);
    }

    public async Task<IActionResult> Create(int? customerId = null, int? leadId = null, string type = "Call")
    {
        await PopulateDropdownsAsync();
        var model = new Activity
        {
            CustomerId = customerId,
            LeadId = leadId,
            ActivityType = type,
            ActivityDate = DateTime.UtcNow,
            Status = "Completed"
        };
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Activity activity)
    {
        if (ModelState.IsValid)
        {
            if (string.IsNullOrEmpty(activity.AssignedTo))
            {
                activity.AssignedTo = _currentUser.UserId;
            }

            _context.Activities.Add(activity);
            await _context.SaveChangesAsync();

            var ip = _currentUser.GetIpAddress();
            await _auditService.LogAsync(
                _currentUser.UserName,
                "Create",
                "Activity",
                activity.ActivityId.ToString(),
                null,
                activity.Subject,
                $"Logged {activity.ActivityType}: {activity.Subject}",
                ip);

            TempData["SuccessMessage"] = "Activity logged successfully!";
            if (activity.CustomerId.HasValue)
            {
                return RedirectToAction("Details", "Customers", new { id = activity.CustomerId.Value });
            }
            if (activity.LeadId.HasValue)
            {
                return RedirectToAction("Details", "Leads", new { id = activity.LeadId.Value });
            }
            return RedirectToAction(nameof(Index));
        }

        await PopulateDropdownsAsync();
        return View(activity);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var activity = await _context.Activities.FindAsync(id);
        if (activity == null)
        {
            return NotFound();
        }

        _context.Activities.Remove(activity);
        await _context.SaveChangesAsync();

        var ip = _currentUser.GetIpAddress();
        await _auditService.LogAsync(
            _currentUser.UserName,
            "Delete",
            "Activity",
            id.ToString(),
            activity.Subject,
            null,
            $"Deleted activity {activity.Subject}",
            ip);

        TempData["SuccessMessage"] = "Activity removed.";
        return RedirectToAction(nameof(Index));
    }

    private async Task PopulateDropdownsAsync()
    {
        var customers = await _context.Customers.OrderBy(c => c.CustomerName).ToListAsync();
        var leads = await _context.Leads.OrderBy(l => l.LeadName).ToListAsync();
        var users = await _context.Users.Where(u => u.IsActive).OrderBy(u => u.FullName).ToListAsync();

        ViewBag.Customers = new SelectList(customers, "CustomerId", "CustomerName");
        ViewBag.Leads = new SelectList(leads, "LeadId", "LeadName");
        ViewBag.Users = new SelectList(users, "Id", "FullName");
        ViewBag.Types = new SelectList(new[] { "Call", "Meeting", "Email", "Task" });
        ViewBag.Statuses = new SelectList(new[] { "Completed", "Scheduled", "Pending" });
    }
}
