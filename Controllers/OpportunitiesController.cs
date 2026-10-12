using AcxiomCRM.Data;
using AcxiomCRM.Models;
using AcxiomCRM.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace AcxiomCRM.Controllers;

[Authorize]
public class OpportunitiesController : Controller
{
    private static readonly string[] AllowedStages = { "Qualification", "Proposal", "Negotiation", "Won", "Lost" };

    private readonly ApplicationDbContext _context;
    private readonly IAuditService _auditService;
    private readonly ICurrentUserService _currentUser;

    public OpportunitiesController(ApplicationDbContext context, IAuditService auditService, ICurrentUserService currentUser)
    {
        _context = context;
        _auditService = auditService;
        _currentUser = currentUser;
    }

    public async Task<IActionResult> Index(string? search, string? stage, string? status, int page = 1)
    {
        var query = _context.Opportunities
            .Include(o => o.Customer)
            .AsQueryable();

        // Scope access
        if (_currentUser.IsSalesExecutive && !_currentUser.IsAdmin && !_currentUser.IsManager)
        {
            query = query.Where(o => o.AssignedTo == _currentUser.UserId);
        }

        // Filtering
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLower();
            query = query.Where(o =>
                o.OpportunityName.ToLower().Contains(s) ||
                (o.Customer != null && o.Customer.CustomerName.ToLower().Contains(s)));
        }

        if (!string.IsNullOrWhiteSpace(stage))
        {
            query = query.Where(o => o.Stage == stage);
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(o => o.Status == status);
        }

        ViewData["CurrentSearch"] = search;
        ViewData["CurrentStage"] = stage;
        ViewData["CurrentStatus"] = status;

        // Overall stats
        var openDeals = await query.Where(o => o.Status == "Open").Select(o => new { o.Amount, o.Probability }).ToListAsync();
        ViewBag.TotalPipeline = openDeals.Sum(o => o.Amount);
        ViewBag.TotalWeighted = openDeals.Sum(o => (o.Amount * o.Probability) / 100m);

        int pageSize = 10;
        var totalCount = await query.CountAsync();
        var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);
        page = Math.Max(1, Math.Min(page, Math.Max(1, totalPages)));

        var opportunities = await query
            .OrderByDescending(o => o.CreatedDate)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        ViewData["CurrentPage"] = page;
        ViewData["TotalPages"] = totalPages;
        ViewData["TotalCount"] = totalCount;

        await PopulateDropdownsAsync();
        return View(opportunities);
    }

    public async Task<IActionResult> Details(int id)
    {
        var opp = await _context.Opportunities
            .Include(o => o.Customer)
            .Include(o => o.Lead)
            .Include(o => o.FollowUps)
            .FirstOrDefaultAsync(o => o.OpportunityId == id);

        if (opp == null)
        {
            return NotFound();
        }

        if (_currentUser.IsSalesExecutive && !_currentUser.IsAdmin && !_currentUser.IsManager && opp.AssignedTo != _currentUser.UserId)
        {
            return Forbid();
        }

        var auditLogs = await _context.AuditLogs
            .Where(a => a.EntityName == "Opportunity" && a.RecordId == id.ToString())
            .OrderByDescending(a => a.CreatedDate)
            .Take(10)
            .ToListAsync();

        ViewBag.AuditLogs = auditLogs;
        return View(opp);
    }

    public async Task<IActionResult> Create(int? customerId = null)
    {
        await PopulateDropdownsAsync();
        var opp = new Opportunity
        {
            CustomerId = customerId ?? 0,
            ExpectedCloseDate = DateTime.Today.AddDays(30),
            Stage = "Qualification",
            Probability = 25,
            Status = "Open",
            Amount = 10000m
        };
        return View(opp);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Opportunity opportunity)
    {
        // Business Validation 1: Amount must be greater than 0
        if (opportunity.Amount <= 0)
        {
            ModelState.AddModelError("Amount", "Opportunity Amount must be greater than 0.");
        }

        // Business Validation 2: Probability must be between 0 and 100
        if (opportunity.Probability < 0 || opportunity.Probability > 100)
        {
            ModelState.AddModelError("Probability", "Probability must be between 0 and 100.");
        }

        // Business Validation 3: Expected Close Date cannot be in the past for active opportunity
        var isActive = opportunity.Stage != "Won" && opportunity.Stage != "Lost" && opportunity.Status == "Open";
        if (isActive && opportunity.ExpectedCloseDate.Date < DateTime.Today)
        {
            ModelState.AddModelError("ExpectedCloseDate", "Expected Close Date cannot be in the past.");
        }

        // Validate customer exists
        if (!await _context.Customers.AnyAsync(c => c.CustomerId == opportunity.CustomerId))
        {
            ModelState.AddModelError("CustomerId", "Please select a valid customer.");
        }

        if (ModelState.IsValid)
        {
            // Sync status with stage
            if (opportunity.Stage == "Won")
            {
                opportunity.Status = "Won";
                opportunity.Probability = 100;
            }
            else if (opportunity.Stage == "Lost")
            {
                opportunity.Status = "Lost";
                opportunity.Probability = 0;
            }
            else
            {
                opportunity.Status = "Open";
            }

            opportunity.CreatedDate = DateTime.UtcNow;
            if (string.IsNullOrEmpty(opportunity.AssignedTo))
            {
                opportunity.AssignedTo = _currentUser.UserId;
            }

            _context.Opportunities.Add(opportunity);
            await _context.SaveChangesAsync();

            var ip = _currentUser.GetIpAddress();
            await _auditService.LogAsync(
                _currentUser.UserName,
                "Create",
                "Opportunity",
                opportunity.OpportunityId.ToString(),
                null,
                opportunity.OpportunityName,
                $"Created opportunity {opportunity.OpportunityName} (Amount: {opportunity.Amount:C}, Stage: {opportunity.Stage})",
                ip);

            TempData["SuccessMessage"] = $"Opportunity '{opportunity.OpportunityName}' created successfully!";
            return RedirectToAction(nameof(Index));
        }

        await PopulateDropdownsAsync();
        return View(opportunity);
    }

    public async Task<IActionResult> Edit(int id)
    {
        var opp = await _context.Opportunities.FindAsync(id);
        if (opp == null)
        {
            return NotFound();
        }

        if (_currentUser.IsSalesExecutive && !_currentUser.IsAdmin && !_currentUser.IsManager && opp.AssignedTo != _currentUser.UserId)
        {
            return Forbid();
        }

        await PopulateDropdownsAsync();
        return View(opp);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, Opportunity opportunity)
    {
        if (id != opportunity.OpportunityId)
        {
            return BadRequest();
        }

        var existing = await _context.Opportunities.AsNoTracking().FirstOrDefaultAsync(o => o.OpportunityId == id);
        if (existing == null)
        {
            return NotFound();
        }

        if (_currentUser.IsSalesExecutive && !_currentUser.IsAdmin && !_currentUser.IsManager && existing.AssignedTo != _currentUser.UserId)
        {
            return Forbid();
        }

        // Business Validation 1: Amount must be greater than 0
        if (opportunity.Amount <= 0)
        {
            ModelState.AddModelError("Amount", "Opportunity Amount must be greater than 0.");
        }

        // Business Validation 2: Probability must be between 0 and 100
        if (opportunity.Probability < 0 || opportunity.Probability > 100)
        {
            ModelState.AddModelError("Probability", "Probability must be between 0 and 100.");
        }

        // Business Validation 3: Expected Close Date cannot be in the past for active opportunity
        var isActive = opportunity.Stage != "Won" && opportunity.Stage != "Lost";
        if (isActive && opportunity.ExpectedCloseDate.Date < DateTime.Today)
        {
            ModelState.AddModelError("ExpectedCloseDate", "Expected Close Date cannot be in the past.");
        }

        if (ModelState.IsValid)
        {
            // Update status according to stage
            if (opportunity.Stage == "Won")
            {
                opportunity.Status = "Won";
                opportunity.Probability = 100;
            }
            else if (opportunity.Stage == "Lost")
            {
                opportunity.Status = "Lost";
                opportunity.Probability = 0;
            }
            else
            {
                opportunity.Status = "Open";
            }

            opportunity.CreatedDate = existing.CreatedDate;

            _context.Update(opportunity);
            await _context.SaveChangesAsync();

            var ip = _currentUser.GetIpAddress();
            var oldVal = $"Stage: {existing.Stage}, Amount: {existing.Amount:C}, Prob: {existing.Probability}%";
            var newVal = $"Stage: {opportunity.Stage}, Amount: {opportunity.Amount:C}, Prob: {opportunity.Probability}%";

            await _auditService.LogAsync(
                _currentUser.UserName,
                "Update",
                "Opportunity",
                opportunity.OpportunityId.ToString(),
                oldVal,
                newVal,
                $"Updated opportunity {opportunity.OpportunityName} (Stage: {opportunity.Stage}, Status: {opportunity.Status})",
                ip);

            TempData["SuccessMessage"] = $"Opportunity '{opportunity.OpportunityName}' updated successfully!";
            return RedirectToAction(nameof(Index));
        }

        await PopulateDropdownsAsync();
        return View(opportunity);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var opp = await _context.Opportunities.FindAsync(id);
        if (opp == null)
        {
            return NotFound();
        }

        if (_currentUser.IsSalesExecutive && !_currentUser.IsAdmin && !_currentUser.IsManager && opp.AssignedTo != _currentUser.UserId)
        {
            return Forbid();
        }

        _context.Opportunities.Remove(opp);
        await _context.SaveChangesAsync();

        var ip = _currentUser.GetIpAddress();
        await _auditService.LogAsync(
            _currentUser.UserName,
            "Delete",
            "Opportunity",
            id.ToString(),
            opp.OpportunityName,
            null,
            $"Deleted opportunity {opp.OpportunityName}",
            ip);

        TempData["SuccessMessage"] = $"Opportunity '{opp.OpportunityName}' deleted successfully!";
        return RedirectToAction(nameof(Index));
    }

    private async Task PopulateDropdownsAsync()
    {
        var customers = await _context.Customers.OrderBy(c => c.CustomerName).ToListAsync();
        var users = await _context.Users.Where(u => u.IsActive).OrderBy(u => u.FullName).ToListAsync();
        ViewBag.Customers = new SelectList(customers, "CustomerId", "CustomerName");
        ViewBag.Users = new SelectList(users, "Id", "FullName");
        ViewBag.Stages = new SelectList(AllowedStages);
    }
}
