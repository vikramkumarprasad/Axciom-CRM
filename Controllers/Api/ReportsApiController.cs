using AcxiomCRM.Data;
using AcxiomCRM.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AcxiomCRM.Controllers.Api;

[ApiController]
[Route("api/[controller]")]
[Route("api/reports")]
public class ReportsApiController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public ReportsApiController(ApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    [HttpGet("pipeline")]
    public async Task<IActionResult> GetPipelineReport()
    {
        var query = _context.Opportunities.AsQueryable();

        if (_currentUser.IsAuthenticated && _currentUser.IsSalesExecutive && !_currentUser.IsAdmin && !_currentUser.IsManager)
        {
            query = query.Where(o => o.AssignedTo == _currentUser.UserId);
        }

        var openOpps = await query.Where(o => o.Status == "Open").ToListAsync();
        var wonOpps = await query.Where(o => o.Status == "Won" || o.Stage == "Won").ToListAsync();
        var allOpps = await query.ToListAsync();

        var stageBreakdown = allOpps
            .GroupBy(o => o.Stage)
            .Select(g => new
            {
                Stage = g.Key,
                Count = g.Count(),
                TotalAmount = g.Sum(x => x.Amount),
                WeightedAmount = g.Sum(x => (x.Amount * x.Probability) / 100m)
            })
            .ToList();

        var result = new
        {
            TotalPipelineValue = openOpps.Sum(o => o.Amount),
            TotalWeightedPipelineValue = openOpps.Sum(o => (o.Amount * o.Probability) / 100m),
            TotalOpenOpportunities = openOpps.Count,
            TotalWonOpportunities = wonOpps.Count,
            TotalWonValue = wonOpps.Sum(o => o.Amount),
            WinRatePercentage = allOpps.Count > 0 ? (wonOpps.Count * 100.0 / allOpps.Count) : 0,
            Stages = stageBreakdown
        };

        return Ok(result);
    }
}
