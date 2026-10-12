using AcxiomCRM.Data;
using AcxiomCRM.Services;
using AcxiomCRM.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace AcxiomCRM.Controllers;

[Authorize]
public class ReportsController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public ReportsController(ApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<IActionResult> Index(
        string type = "Pipeline",
        DateTime? startDate = null,
        DateTime? endDate = null,
        string? owner = null,
        string? status = null)
    {
        var isSalesExec = _currentUser.IsSalesExecutive && !_currentUser.IsAdmin && !_currentUser.IsManager;
        var currentUserId = _currentUser.UserId;

        var model = new ReportsViewModel
        {
            ReportType = type,
            StartDate = startDate,
            EndDate = endDate,
            SelectedOwner = owner,
            SelectedStatus = status
        };

        // Queries
        var custQ = _context.Customers.AsQueryable();
        var leadQ = _context.Leads.AsQueryable();
        var oppQ = _context.Opportunities.Include(o => o.Customer).AsQueryable();
        var followQ = _context.FollowUps.Include(f => f.Customer).Include(f => f.Lead).AsQueryable();
        var auditQ = _context.AuditLogs.AsQueryable();

        // Scope
        if (isSalesExec)
        {
            custQ = custQ.Where(c => c.AssignedTo == currentUserId);
            leadQ = leadQ.Where(l => l.AssignedTo == currentUserId);
            oppQ = oppQ.Where(o => o.AssignedTo == currentUserId);
            followQ = followQ.Where(f => f.AssignedTo == currentUserId);
        }
        else if (!string.IsNullOrEmpty(owner))
        {
            custQ = custQ.Where(c => c.AssignedTo == owner);
            leadQ = leadQ.Where(l => l.AssignedTo == owner);
            oppQ = oppQ.Where(o => o.AssignedTo == owner);
            followQ = followQ.Where(f => f.AssignedTo == owner);
        }

        // Date filter
        if (startDate.HasValue)
        {
            custQ = custQ.Where(c => c.CreatedDate >= startDate.Value.Date);
            leadQ = leadQ.Where(l => l.CreatedDate >= startDate.Value.Date);
            oppQ = oppQ.Where(o => o.CreatedDate >= startDate.Value.Date);
            followQ = followQ.Where(f => f.FollowUpDate >= startDate.Value.Date);
            auditQ = auditQ.Where(a => a.CreatedDate >= startDate.Value.Date);
        }
        if (endDate.HasValue)
        {
            var end = endDate.Value.Date.AddDays(1).AddTicks(-1);
            custQ = custQ.Where(c => c.CreatedDate <= end);
            leadQ = leadQ.Where(l => l.CreatedDate <= end);
            oppQ = oppQ.Where(o => o.CreatedDate <= end);
            followQ = followQ.Where(f => f.FollowUpDate <= end);
            auditQ = auditQ.Where(a => a.CreatedDate <= end);
        }

        // Pipeline Metrics
        var openOpps = await oppQ.Where(o => o.Status == "Open").ToListAsync();
        var wonOpps = await oppQ.Where(o => o.Status == "Won" || o.Stage == "Won").ToListAsync();
        var totalOppsCount = await oppQ.CountAsync();

        model.TotalOpenDeals = openOpps.Count;
        model.TotalPipelineValue = openOpps.Sum(o => o.Amount);
        model.TotalWeightedValue = openOpps.Sum(o => (o.Amount * o.Probability) / 100m);
        model.TotalWonDeals = wonOpps.Count;
        model.TotalWonValue = wonOpps.Sum(o => o.Amount);
        model.WinRatePercentage = totalOppsCount > 0 ? (wonOpps.Count * 100.0 / totalOppsCount) : 0;

        // Stage Summaries
        var stages = new[] { "Qualification", "Proposal", "Negotiation", "Won", "Lost" };
        var allOppsList = await oppQ.ToListAsync();
        foreach (var stg in stages)
        {
            var items = allOppsList.Where(o => string.Equals(o.Stage, stg, StringComparison.OrdinalIgnoreCase)).ToList();
            model.StageSummaries.Add(new StageSummaryItem
            {
                Stage = stg,
                Count = items.Count,
                TotalAmount = items.Sum(o => o.Amount),
                WeightedAmount = items.Sum(o => (o.Amount * o.Probability) / 100m)
            });
        }

        // Owner Summaries
        var users = await _context.Users.ToListAsync();
        foreach (var u in users)
        {
            var userOpps = allOppsList.Where(o => o.AssignedTo == u.Id).ToList();
            if (userOpps.Any() || !isSalesExec)
            {
                model.OwnerSummaries.Add(new OwnerSummaryItem
                {
                    OwnerId = u.Id,
                    OwnerName = u.FullName,
                    OpportunityCount = userOpps.Count,
                    TotalPipeline = userOpps.Where(o => o.Status == "Open").Sum(o => o.Amount),
                    WonPipeline = userOpps.Where(o => o.Status == "Won" || o.Stage == "Won").Sum(o => o.Amount)
                });
            }
        }

        // Conversion Summaries
        var allLeads = await leadQ.ToListAsync();
        var sources = allLeads.Select(l => l.Source).Distinct().ToList();
        foreach (var src in sources)
        {
            var leadsInSrc = allLeads.Where(l => l.Source == src).ToList();
            model.ConversionSummaries.Add(new LeadConversionSummaryItem
            {
                Source = src,
                TotalLeads = leadsInSrc.Count,
                ConvertedCount = leadsInSrc.Count(l => l.Status == "Converted")
            });
        }

        // User Activity Summaries (Admin/Manager)
        if (_currentUser.IsAdmin || _currentUser.IsManager)
        {
            var allActivities = await _context.Activities.ToListAsync();
            var allFollowUps = await _context.FollowUps.ToListAsync();
            var allAudits = await _context.AuditLogs.ToListAsync();

            foreach (var u in users)
            {
                model.UserActivitySummaries.Add(new UserActivitySummaryItem
                {
                    UserId = u.Id,
                    UserName = u.FullName,
                    ActivityCount = allActivities.Count(a => a.AssignedTo == u.Id),
                    FollowUpCount = allFollowUps.Count(f => f.AssignedTo == u.Id),
                    AuditEventsCount = allAudits.Count(a => a.UserId == u.Email || a.UserId == u.UserName)
                });
            }
        }

        // Data for selected table
        switch (type)
        {
            case "Customer":
                if (!string.IsNullOrEmpty(status)) custQ = custQ.Where(c => c.Status == status);
                model.Customers = await custQ.OrderByDescending(c => c.CreatedDate).Take(100).ToListAsync();
                break;
            case "Lead":
                if (!string.IsNullOrEmpty(status)) leadQ = leadQ.Where(l => l.Status == status);
                model.Leads = await leadQ.OrderByDescending(l => l.CreatedDate).Take(100).ToListAsync();
                break;
            case "Opportunity":
                if (!string.IsNullOrEmpty(status)) oppQ = oppQ.Where(o => o.Status == status);
                model.Opportunities = await oppQ.OrderByDescending(o => o.CreatedDate).Take(100).ToListAsync();
                break;
            case "FollowUp":
                if (!string.IsNullOrEmpty(status)) followQ = followQ.Where(f => f.Status == status);
                model.FollowUps = await followQ.OrderBy(f => f.FollowUpDate).Take(100).ToListAsync();
                break;
            case "Audit":
                if (_currentUser.IsAdmin)
                {
                    model.AuditLogs = await auditQ.OrderByDescending(a => a.CreatedDate).Take(100).ToListAsync();
                }
                break;
        }

        ViewBag.Users = new SelectList(users, "Id", "FullName");
        return View(model);
    }
}
