using AcxiomCRM.Data;
using AcxiomCRM.Models;
using AcxiomCRM.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace AcxiomCRM.Controllers;

[Authorize]
public class FollowUpsController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly IAuditService _auditService;
    private readonly ICurrentUserService _currentUser;

    public FollowUpsController(ApplicationDbContext context, IAuditService auditService, ICurrentUserService currentUser)
    {
        _context = context;
        _auditService = auditService;
        _currentUser = currentUser;
    }

    public async Task<IActionResult> Index(string? filter = "all", string? status = null, string? type = null)
    {
        var query = _context.FollowUps
            .Include(f => f.Customer)
            .Include(f => f.Lead)
            .AsQueryable();

        // Scope access
        if (_currentUser.IsSalesExecutive && !_currentUser.IsAdmin && !_currentUser.IsManager)
        {
            query = query.Where(f => f.AssignedTo == _currentUser.UserId);
        }

        var today = DateTime.Today;

        if (filter == "upcoming")
        {
            query = query.Where(f => f.Status == "Planned" && f.FollowUpDate >= today);
        }
        else if (filter == "overdue")
        {
            query = query.Where(f => f.Status == "Planned" && f.FollowUpDate < today);
        }
        else if (filter == "completed")
        {
            query = query.Where(f => f.Status == "Completed");
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(f => f.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(type))
        {
            query = query.Where(f => f.FollowUpType == type);
        }

        ViewData["CurrentFilter"] = filter;
        ViewData["CurrentStatus"] = status;
        ViewData["CurrentType"] = type;

        var followUps = await query.OrderBy(f => f.FollowUpDate).ToListAsync();
        await PopulateDropdownsAsync();

        return View(followUps);
    }

    public async Task<IActionResult> Create(int? customerId = null, int? leadId = null, string? relatedType = "Customer")
    {
        await PopulateDropdownsAsync();
        var model = new FollowUp
        {
            CustomerId = customerId,
            LeadId = leadId,
            RelatedType = leadId.HasValue ? "Lead" : "Customer",
            FollowUpDate = DateTime.Today.AddDays(1),
            Status = "Planned",
            FollowUpType = "Call"
        };
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(FollowUp followUp)
    {
        // Business Validation: Follow-Up Date cannot be earlier than today for new/planned
        if (followUp.Status == "Planned" && followUp.FollowUpDate.Date < DateTime.Today)
        {
            ModelState.AddModelError("FollowUpDate", "Follow-up date cannot be earlier than today.");
        }

        if (ModelState.IsValid)
        {
            if (string.IsNullOrEmpty(followUp.AssignedTo))
            {
                followUp.AssignedTo = _currentUser.UserId;
            }

            _context.FollowUps.Add(followUp);
            await _context.SaveChangesAsync();

            var ip = _currentUser.GetIpAddress();
            await _auditService.LogAsync(
                _currentUser.UserName,
                "Create",
                "FollowUp",
                followUp.FollowUpId.ToString(),
                null,
                followUp.FollowUpType,
                $"Scheduled {followUp.FollowUpType} follow-up for {followUp.FollowUpDate:d}",
                ip);

            TempData["SuccessMessage"] = "Follow-up scheduled successfully!";
            return RedirectToAction(nameof(Index));
        }

        await PopulateDropdownsAsync();
        return View(followUp);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Complete(int id, string? notes = null)
    {
        var followUp = await _context.FollowUps.FindAsync(id);
        if (followUp == null)
        {
            return NotFound();
        }

        followUp.Status = "Completed";
        if (!string.IsNullOrWhiteSpace(notes))
        {
            followUp.Remarks = (followUp.Remarks ?? "") + $"\n[Completed Notes: {notes}]";
        }

        _context.Update(followUp);
        await _context.SaveChangesAsync();

        var ip = _currentUser.GetIpAddress();
        await _auditService.LogAsync(
            _currentUser.UserName,
            "Complete",
            "FollowUp",
            id.ToString(),
            "Planned",
            "Completed",
            $"Completed follow-up ID {id}",
            ip);

        TempData["SuccessMessage"] = "Follow-up marked as completed!";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reschedule(int id, DateTime newDate, string? remarks = null)
    {
        var followUp = await _context.FollowUps.FindAsync(id);
        if (followUp == null)
        {
            return NotFound();
        }

        // Business validation: newDate cannot be earlier than today
        if (newDate.Date < DateTime.Today)
        {
            TempData["ErrorMessage"] = "Follow-up date cannot be earlier than today.";
            return RedirectToAction(nameof(Index));
        }

        var oldDate = followUp.FollowUpDate;
        followUp.FollowUpDate = newDate;
        followUp.Status = "Planned";
        if (!string.IsNullOrWhiteSpace(remarks))
        {
            followUp.Remarks = (followUp.Remarks ?? "") + $"\n[Rescheduled to {newDate:d}: {remarks}]";
        }

        _context.Update(followUp);
        await _context.SaveChangesAsync();

        var ip = _currentUser.GetIpAddress();
        await _auditService.LogAsync(
            _currentUser.UserName,
            "Reschedule",
            "FollowUp",
            id.ToString(),
            oldDate.ToString("d"),
            newDate.ToString("d"),
            $"Rescheduled follow-up to {newDate:d}",
            ip);

        TempData["SuccessMessage"] = $"Follow-up rescheduled to {newDate:d}!";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(int id)
    {
        var followUp = await _context.FollowUps.FindAsync(id);
        if (followUp == null)
        {
            return NotFound();
        }

        followUp.Status = "Cancelled";
        _context.Update(followUp);
        await _context.SaveChangesAsync();

        var ip = _currentUser.GetIpAddress();
        await _auditService.LogAsync(
            _currentUser.UserName,
            "Cancel",
            "FollowUp",
            id.ToString(),
            "Planned",
            "Cancelled",
            $"Cancelled follow-up ID {id}",
            ip);

        TempData["SuccessMessage"] = "Follow-up cancelled.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var followUp = await _context.FollowUps.FindAsync(id);
        if (followUp == null)
        {
            return NotFound();
        }

        _context.FollowUps.Remove(followUp);
        await _context.SaveChangesAsync();

        var ip = _currentUser.GetIpAddress();
        await _auditService.LogAsync(
            _currentUser.UserName,
            "Delete",
            "FollowUp",
            id.ToString(),
            followUp.FollowUpType,
            null,
            $"Deleted follow-up ID {id}",
            ip);

        TempData["SuccessMessage"] = "Follow-up deleted.";
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
        ViewBag.Types = new SelectList(new[] { "Call", "Meeting", "Email", "Demo" });
        ViewBag.Statuses = new SelectList(new[] { "Planned", "Completed", "Missed", "Cancelled" });
    }
}
