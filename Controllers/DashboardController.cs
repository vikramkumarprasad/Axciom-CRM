using AcxiomCRM.Data;
using AcxiomCRM.Models;
using AcxiomCRM.Services;
using AcxiomCRM.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AcxiomCRM.Controllers;

[Authorize]
public class DashboardController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public DashboardController(ApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<IActionResult> Index(string dateFilter = "AllTime")
    {
        var userId = _currentUser.UserId;
        var isSalesExec = _currentUser.IsSalesExecutive && !_currentUser.IsAdmin && !_currentUser.IsManager;

        // Base scoped queries
        var customerQuery = _context.Customers.AsQueryable();
        var leadQuery = _context.Leads.AsQueryable();
        var oppQuery = _context.Opportunities.Include(o => o.Customer).AsQueryable();
        var followUpQuery = _context.FollowUps.Include(f => f.Customer).Include(f => f.Lead).AsQueryable();
        var activityQuery = _context.Activities.Include(a => a.Customer).Include(a => a.Lead).AsQueryable();

        if (isSalesExec)
        {
            customerQuery = customerQuery.Where(c => c.AssignedTo == userId);
            leadQuery = leadQuery.Where(l => l.AssignedTo == userId);
            oppQuery = oppQuery.Where(o => o.AssignedTo == userId);
            followUpQuery = followUpQuery.Where(f => f.AssignedTo == userId);
            activityQuery = activityQuery.Where(a => a.AssignedTo == userId);
        }

        // Apply date filter if specified
        DateTime? filterDate = null;
        var now = DateTime.UtcNow;
        if (dateFilter == "Today")
        {
            filterDate = DateTime.Today;
            oppQuery = oppQuery.Where(o => o.CreatedDate >= filterDate);
            leadQuery = leadQuery.Where(l => l.CreatedDate >= filterDate);
        }
        else if (dateFilter == "ThisWeek")
        {
            filterDate = DateTime.Today.AddDays(-(int)DateTime.Today.DayOfWeek);
            oppQuery = oppQuery.Where(o => o.CreatedDate >= filterDate);
            leadQuery = leadQuery.Where(l => l.CreatedDate >= filterDate);
        }
        else if (dateFilter == "ThisMonth")
        {
            filterDate = new DateTime(now.Year, now.Month, 1);
            oppQuery = oppQuery.Where(o => o.CreatedDate >= filterDate);
            leadQuery = leadQuery.Where(l => l.CreatedDate >= filterDate);
        }

        var viewModel = new DashboardViewModel
        {
            DateFilter = dateFilter,
            UserName = _currentUser.UserName ?? "User",
            UserRole = _currentUser.IsAdmin ? "Admin" : (_currentUser.IsManager ? "Manager" : "Sales Executive"),

            // 8 KPI Cards
            TotalCustomers = await customerQuery.CountAsync(),
            TotalLeads = await leadQuery.CountAsync(),
            OpenLeads = await leadQuery.CountAsync(l => l.Status != "Converted" && l.Status != "Lost"),
            TotalOpportunities = await oppQuery.CountAsync(),
            OpenOpportunities = await oppQuery.CountAsync(o => o.Status == "Open"),
            WonOpportunities = await oppQuery.CountAsync(o => o.Status == "Won" || o.Stage == "Won"),
            LostOpportunities = await oppQuery.CountAsync(o => o.Status == "Lost" || o.Stage == "Lost"),
            TotalPipelineValue = (await oppQuery.Where(o => o.Status == "Open").Select(o => o.Amount).ToListAsync()).Sum()
        };

        // Chart 1: Lead Status
        var leadStatuses = new[] { "New", "Contacted", "Qualified", "Lost", "Converted" };
        var leadStatusGroups = await leadQuery
            .GroupBy(l => l.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync();

        foreach (var status in leadStatuses)
        {
            viewModel.LeadStatusLabels.Add(status);
            var item = leadStatusGroups.FirstOrDefault(g => string.Equals(g.Status, status, StringComparison.OrdinalIgnoreCase));
            viewModel.LeadStatusCounts.Add(item?.Count ?? 0);
        }

        // Chart 2: Opportunity Pipeline Stages
        var stages = new[] { "Qualification", "Proposal", "Negotiation", "Won", "Lost" };
        var oppsForStages = await oppQuery.Select(o => new { o.Stage, o.Amount }).ToListAsync();
        var oppStageGroups = oppsForStages
            .GroupBy(o => o.Stage)
            .Select(g => new { Stage = g.Key, Count = g.Count(), TotalAmount = g.Sum(x => x.Amount) })
            .ToList();

        foreach (var stage in stages)
        {
            viewModel.OpportunityStageLabels.Add(stage);
            var item = oppStageGroups.FirstOrDefault(g => string.Equals(g.Stage, stage, StringComparison.OrdinalIgnoreCase));
            viewModel.OpportunityStageCounts.Add(item?.Count ?? 0);
            viewModel.OpportunityStageAmounts.Add(item?.TotalAmount ?? 0m);
        }

        // Chart 3: Monthly Sales / Won Totals (last 6 months)
        var wonOppsInScope = await oppQuery
            .Where(o => (o.Status == "Won" || o.Stage == "Won") && o.CreatedDate >= DateTime.Today.AddMonths(-6))
            .Select(o => new { o.CreatedDate, o.Amount })
            .ToListAsync();

        for (int i = 5; i >= 0; i--)
        {
            var monthDate = DateTime.Today.AddMonths(-i);
            var monthName = monthDate.ToString("MMM yyyy");
            viewModel.MonthlySalesLabels.Add(monthName);

            var startOfMonth = new DateTime(monthDate.Year, monthDate.Month, 1);
            var endOfMonth = startOfMonth.AddMonths(1);

            var monthlyWonTotal = wonOppsInScope
                .Where(o => o.CreatedDate >= startOfMonth && o.CreatedDate < endOfMonth)
                .Sum(o => o.Amount);

            viewModel.MonthlySalesAmounts.Add(monthlyWonTotal);
        }

        // Recent items
        viewModel.RecentLeads = await leadQuery.OrderByDescending(l => l.CreatedDate).Take(5).ToListAsync();
        viewModel.TopOpportunities = (await oppQuery.ToListAsync())
            .OrderByDescending(o => o.Amount)
            .Take(5)
            .ToList();
        viewModel.UpcomingFollowUps = await followUpQuery
            .Where(f => f.Status == "Planned" && f.FollowUpDate >= DateTime.Today)
            .OrderBy(f => f.FollowUpDate)
            .Take(5)
            .ToListAsync();
        viewModel.RecentActivities = await activityQuery.OrderByDescending(a => a.ActivityDate).Take(5).ToListAsync();

        return View(viewModel);
    }
}
