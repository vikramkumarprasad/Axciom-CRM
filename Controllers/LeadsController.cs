using AcxiomCRM.Data;
using AcxiomCRM.Models;
using AcxiomCRM.Services;
using AcxiomCRM.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace AcxiomCRM.Controllers;

[Authorize]
public class LeadsController : Controller
{
    private static readonly HashSet<string> AllowedStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        "New", "Contacted", "Qualified", "Unqualified", "Converted", "Lost"
    };

    private readonly ApplicationDbContext _context;
    private readonly IAuditService _auditService;
    private readonly ICurrentUserService _currentUser;

    public LeadsController(ApplicationDbContext context, IAuditService auditService, ICurrentUserService currentUser)
    {
        _context = context;
        _auditService = auditService;
        _currentUser = currentUser;
    }

    public async Task<IActionResult> Index(string? search, string? status, string? source, string? assignedTo, int page = 1)
    {
        var query = _context.Leads.AsQueryable();

        // Scope access
        if (_currentUser.IsSalesExecutive && !_currentUser.IsAdmin && !_currentUser.IsManager)
        {
            query = query.Where(l => l.AssignedTo == _currentUser.UserId);
        }

        // Filtering
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLower();
            query = query.Where(l =>
                l.LeadName.ToLower().Contains(s) ||
                l.Email.ToLower().Contains(s) ||
                l.Phone.ToLower().Contains(s) ||
                (l.CompanyName != null && l.CompanyName.ToLower().Contains(s)) ||
                l.LeadCode.ToLower().Contains(s));
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(l => l.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(source))
        {
            query = query.Where(l => l.Source == source);
        }

        if (!string.IsNullOrWhiteSpace(assignedTo))
        {
            query = query.Where(l => l.AssignedTo == assignedTo);
        }

        ViewData["CurrentSearch"] = search;
        ViewData["CurrentStatus"] = status;
        ViewData["CurrentSource"] = source;
        ViewData["CurrentAssignedTo"] = assignedTo;

        int pageSize = 10;
        var totalCount = await query.CountAsync();
        var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);
        page = Math.Max(1, Math.Min(page, Math.Max(1, totalPages)));

        var leads = await query
            .OrderByDescending(l => l.CreatedDate)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        ViewData["CurrentPage"] = page;
        ViewData["TotalPages"] = totalPages;
        ViewData["TotalCount"] = totalCount;

        await PopulateDropdownsAsync();
        return View(leads);
    }

    public async Task<IActionResult> Details(int id)
    {
        var lead = await _context.Leads
            .Include(l => l.FollowUps)
            .Include(l => l.Activities)
            .Include(l => l.ConvertedCustomer)
            .FirstOrDefaultAsync(l => l.LeadId == id);

        if (lead == null)
        {
            return NotFound();
        }

        if (_currentUser.IsSalesExecutive && !_currentUser.IsAdmin && !_currentUser.IsManager && lead.AssignedTo != _currentUser.UserId)
        {
            return Forbid();
        }

        var auditLogs = await _context.AuditLogs
            .Where(a => a.EntityName == "Lead" && a.RecordId == id.ToString())
            .OrderByDescending(a => a.CreatedDate)
            .Take(10)
            .ToListAsync();

        ViewBag.AuditLogs = auditLogs;
        return View(lead);
    }

    public async Task<IActionResult> Create()
    {
        await PopulateDropdownsAsync();
        var lead = new Lead
        {
            LeadCode = await GenerateLeadCodeAsync(),
            Status = "New",
            Source = "Website",
            Priority = "Medium",
            ExpectedValue = 10000m
        };
        return View(lead);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Lead lead)
    {
        // Business Validation: Status must be in allowed set
        if (!AllowedStatuses.Contains(lead.Status))
        {
            ModelState.AddModelError("Status", $"Lead status must be one of: {string.Join(", ", AllowedStatuses)}");
        }

        // Business Validation: Expected value must be > 0
        if (lead.ExpectedValue <= 0)
        {
            ModelState.AddModelError("ExpectedValue", "Expected Value must be greater than 0.");
        }

        if (ModelState.IsValid)
        {
            if (string.IsNullOrWhiteSpace(lead.LeadCode))
            {
                lead.LeadCode = await GenerateLeadCodeAsync();
            }

            lead.CreatedDate = DateTime.UtcNow;
            if (string.IsNullOrEmpty(lead.AssignedTo))
            {
                lead.AssignedTo = _currentUser.UserId;
            }

            _context.Leads.Add(lead);
            await _context.SaveChangesAsync();

            var ip = _currentUser.GetIpAddress();
            await _auditService.LogAsync(
                _currentUser.UserName,
                "Create",
                "Lead",
                lead.LeadId.ToString(),
                null,
                lead.LeadName,
                $"Created lead {lead.LeadCode}: {lead.LeadName} (Val: {lead.ExpectedValue:C})",
                ip);

            TempData["SuccessMessage"] = $"Lead '{lead.LeadName}' created successfully!";
            return RedirectToAction(nameof(Index));
        }

        await PopulateDropdownsAsync();
        return View(lead);
    }

    public async Task<IActionResult> Edit(int id)
    {
        var lead = await _context.Leads.FindAsync(id);
        if (lead == null)
        {
            return NotFound();
        }

        if (_currentUser.IsSalesExecutive && !_currentUser.IsAdmin && !_currentUser.IsManager && lead.AssignedTo != _currentUser.UserId)
        {
            return Forbid();
        }

        await PopulateDropdownsAsync();
        return View(lead);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, Lead lead)
    {
        if (id != lead.LeadId)
        {
            return BadRequest();
        }

        var existing = await _context.Leads.AsNoTracking().FirstOrDefaultAsync(l => l.LeadId == id);
        if (existing == null)
        {
            return NotFound();
        }

        if (_currentUser.IsSalesExecutive && !_currentUser.IsAdmin && !_currentUser.IsManager && existing.AssignedTo != _currentUser.UserId)
        {
            return Forbid();
        }

        // Business Validation: Status must be in allowed set
        if (!AllowedStatuses.Contains(lead.Status))
        {
            ModelState.AddModelError("Status", $"Lead status must be one of: {string.Join(", ", AllowedStatuses)}");
        }

        // Business Validation: Expected value must be > 0
        if (lead.ExpectedValue <= 0)
        {
            ModelState.AddModelError("ExpectedValue", "Expected Value must be greater than 0.");
        }

        if (ModelState.IsValid)
        {
            lead.CreatedDate = existing.CreatedDate;
            lead.LeadCode = existing.LeadCode;
            lead.ConvertedCustomerId = existing.ConvertedCustomerId;
            lead.ConvertedOpportunityId = existing.ConvertedOpportunityId;

            _context.Update(lead);
            await _context.SaveChangesAsync();

            var ip = _currentUser.GetIpAddress();
            var oldVal = $"Status: {existing.Status}, Value: {existing.ExpectedValue:C}";
            var newVal = $"Status: {lead.Status}, Value: {lead.ExpectedValue:C}";

            await _auditService.LogAsync(
                _currentUser.UserName,
                "Update",
                "Lead",
                lead.LeadId.ToString(),
                oldVal,
                newVal,
                $"Updated lead {lead.LeadCode}",
                ip);

            TempData["SuccessMessage"] = $"Lead '{lead.LeadName}' updated successfully!";
            return RedirectToAction(nameof(Index));
        }

        await PopulateDropdownsAsync();
        return View(lead);
    }

    [HttpGet]
    public async Task<IActionResult> Convert(int id)
    {
        var lead = await _context.Leads.FindAsync(id);
        if (lead == null)
        {
            return NotFound();
        }

        if (lead.Status == "Converted")
        {
            TempData["ErrorMessage"] = "This lead has already been converted.";
            return RedirectToAction(nameof(Details), new { id });
        }

        var model = new LeadConversionViewModel
        {
            LeadId = lead.LeadId,
            LeadCode = lead.LeadCode,
            CustomerName = lead.LeadName,
            Email = lead.Email,
            Phone = lead.Phone,
            CompanyName = lead.CompanyName,
            OpportunityName = $"{lead.LeadName} - Deal",
            OpportunityAmount = lead.ExpectedValue > 0 ? lead.ExpectedValue : 10000m,
            ExpectedCloseDate = DateTime.Today.AddDays(30),
            Probability = 50,
            CreateOpportunity = true
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Convert(LeadConversionViewModel model)
    {
        var lead = await _context.Leads.FindAsync(model.LeadId);
        if (lead == null)
        {
            return NotFound();
        }

        // Check if customer email already exists
        if (await _context.Customers.AnyAsync(c => c.Email.ToLower() == model.Email.ToLower()))
        {
            ModelState.AddModelError("Email", "A customer with this email address already exists.");
        }

        if (model.CreateOpportunity)
        {
            if (string.IsNullOrWhiteSpace(model.OpportunityName))
            {
                ModelState.AddModelError("OpportunityName", "Opportunity Name is required when creating an opportunity.");
            }
            if (!model.OpportunityAmount.HasValue || model.OpportunityAmount <= 0)
            {
                ModelState.AddModelError("OpportunityAmount", "Opportunity Amount must be greater than 0.");
            }
        }

        if (ModelState.IsValid)
        {
            // 1. Create Customer
            var lastCust = await _context.Customers.OrderByDescending(c => c.CustomerId).FirstOrDefaultAsync();
            var nextCustCode = $"CUST-{(lastCust?.CustomerId ?? 1000) + 1}";

            var customer = new Customer
            {
                CustomerCode = nextCustCode,
                CustomerName = model.CustomerName,
                Email = model.Email,
                Phone = model.Phone,
                CompanyName = model.CompanyName,
                Address = model.Address,
                City = model.City,
                State = model.State,
                Status = "Active",
                CreatedDate = DateTime.UtcNow,
                CreatedBy = _currentUser.UserName ?? "System",
                AssignedTo = lead.AssignedTo ?? _currentUser.UserId
            };

            _context.Customers.Add(customer);
            await _context.SaveChangesAsync();

            int? newOppId = null;

            // 2. Create Opportunity if requested
            if (model.CreateOpportunity)
            {
                var opp = new Opportunity
                {
                    OpportunityName = model.OpportunityName!,
                    CustomerId = customer.CustomerId,
                    LeadId = lead.LeadId,
                    Amount = model.OpportunityAmount!.Value,
                    Stage = "Qualification",
                    Probability = model.Probability,
                    ExpectedCloseDate = model.ExpectedCloseDate ?? DateTime.Today.AddDays(30),
                    Status = "Open",
                    CreatedDate = DateTime.UtcNow,
                    AssignedTo = lead.AssignedTo ?? _currentUser.UserId,
                    Notes = $"Converted from Lead {lead.LeadCode}"
                };

                _context.Opportunities.Add(opp);
                await _context.SaveChangesAsync();
                newOppId = opp.OpportunityId;
            }

            // 3. Update Lead Status to Converted
            lead.Status = "Converted";
            lead.ConvertedCustomerId = customer.CustomerId;
            lead.ConvertedOpportunityId = newOppId;
            _context.Update(lead);
            await _context.SaveChangesAsync();

            // 4. Record Conversion in Audit Log
            var ip = _currentUser.GetIpAddress();
            await _auditService.LogAsync(
                _currentUser.UserName,
                "Convert",
                "Lead",
                lead.LeadId.ToString(),
                "Qualified",
                "Converted",
                $"Converted Lead {lead.LeadCode} into Customer {customer.CustomerCode} (CustomerId: {customer.CustomerId})" +
                (newOppId.HasValue ? $" and Opportunity (OppId: {newOppId.Value})" : ""),
                ip);

            TempData["SuccessMessage"] = $"Lead successfully converted into Customer '{customer.CustomerName}'!";
            return RedirectToAction("Details", "Customers", new { id = customer.CustomerId });
        }

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var lead = await _context.Leads.FindAsync(id);
        if (lead == null)
        {
            return NotFound();
        }

        if (_currentUser.IsSalesExecutive && !_currentUser.IsAdmin && !_currentUser.IsManager && lead.AssignedTo != _currentUser.UserId)
        {
            return Forbid();
        }

        _context.Leads.Remove(lead);
        await _context.SaveChangesAsync();

        var ip = _currentUser.GetIpAddress();
        await _auditService.LogAsync(
            _currentUser.UserName,
            "Delete",
            "Lead",
            id.ToString(),
            lead.LeadName,
            null,
            $"Deleted lead {lead.LeadCode}",
            ip);

        TempData["SuccessMessage"] = $"Lead '{lead.LeadName}' deleted successfully!";
        return RedirectToAction(nameof(Index));
    }

    private async Task<string> GenerateLeadCodeAsync()
    {
        var lastLead = await _context.Leads.OrderByDescending(l => l.LeadId).FirstOrDefaultAsync();
        int nextNum = (lastLead?.LeadId ?? 2000) + 1;
        return $"LEAD-{nextNum}";
    }

    private async Task PopulateDropdownsAsync()
    {
        var users = await _context.Users.Where(u => u.IsActive).OrderBy(u => u.FullName).ToListAsync();
        ViewBag.Users = new SelectList(users, "Id", "FullName");
        ViewBag.Statuses = new SelectList(AllowedStatuses);
        ViewBag.Sources = new SelectList(new[] { "Website", "Referral", "Cold Call", "Campaign", "Partner", "Social Media" });
        ViewBag.Priorities = new SelectList(new[] { "Low", "Medium", "High" });
    }
}
