using AcxiomCRM.Data;
using AcxiomCRM.DTOs;
using AcxiomCRM.Models;
using AcxiomCRM.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AcxiomCRM.Controllers.Api;

[ApiController]
[Route("api/[controller]")]
[Route("api/opportunities")]
public class OpportunitiesApiController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly IAuditService _auditService;
    private readonly ICurrentUserService _currentUser;

    public OpportunitiesApiController(ApplicationDbContext context, IAuditService auditService, ICurrentUserService currentUser)
    {
        _context = context;
        _auditService = auditService;
        _currentUser = currentUser;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<OpportunityDto>>> GetOpportunities([FromQuery] string? stage = null)
    {
        var query = _context.Opportunities.Include(o => o.Customer).AsQueryable();

        if (_currentUser.IsAuthenticated && _currentUser.IsSalesExecutive && !_currentUser.IsAdmin && !_currentUser.IsManager)
        {
            query = query.Where(o => o.AssignedTo == _currentUser.UserId);
        }

        if (!string.IsNullOrWhiteSpace(stage))
        {
            query = query.Where(o => o.Stage == stage);
        }

        var opportunities = await query
            .OrderByDescending(o => o.CreatedDate)
            .Select(o => new OpportunityDto
            {
                OpportunityId = o.OpportunityId,
                OpportunityName = o.OpportunityName,
                CustomerId = o.CustomerId,
                CustomerName = o.Customer != null ? o.Customer.CustomerName : null,
                LeadId = o.LeadId,
                Amount = o.Amount,
                Stage = o.Stage,
                Probability = o.Probability,
                WeightedAmount = (o.Amount * o.Probability) / 100m,
                ExpectedCloseDate = o.ExpectedCloseDate,
                Status = o.Status,
                CreatedDate = o.CreatedDate,
                AssignedTo = o.AssignedTo,
                Notes = o.Notes
            })
            .ToListAsync();

        return Ok(opportunities);
    }

    [HttpPost]
    public async Task<ActionResult<OpportunityDto>> CreateOpportunity([FromBody] CreateOpportunityDto dto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        if (dto.Amount <= 0)
        {
            return BadRequest(new { message = "Opportunity Amount must be greater than 0." });
        }

        if (dto.Probability < 0 || dto.Probability > 100)
        {
            return BadRequest(new { message = "Probability must be between 0 and 100." });
        }

        var isActive = dto.Stage != "Won" && dto.Stage != "Lost";
        if (isActive && dto.ExpectedCloseDate.Date < DateTime.Today)
        {
            return BadRequest(new { message = "Expected Close Date cannot be in the past." });
        }

        var customer = await _context.Customers.FindAsync(dto.CustomerId);
        if (customer == null)
        {
            return BadRequest(new { message = "Referenced Customer does not exist." });
        }

        var opp = new Opportunity
        {
            OpportunityName = dto.OpportunityName,
            CustomerId = dto.CustomerId,
            LeadId = dto.LeadId,
            Amount = dto.Amount,
            Stage = dto.Stage,
            Probability = dto.Probability,
            ExpectedCloseDate = dto.ExpectedCloseDate,
            Status = string.IsNullOrEmpty(dto.Status) ? (dto.Stage == "Won" ? "Won" : (dto.Stage == "Lost" ? "Lost" : "Open")) : dto.Status,
            CreatedDate = DateTime.UtcNow,
            AssignedTo = dto.AssignedTo ?? _currentUser.UserId,
            Notes = dto.Notes
        };

        _context.Opportunities.Add(opp);
        await _context.SaveChangesAsync();

        var ip = _currentUser.GetIpAddress();
        await _auditService.LogAsync(
            _currentUser.UserName ?? "API",
            "Create",
            "Opportunity",
            opp.OpportunityId.ToString(),
            null,
            opp.OpportunityName,
            $"Created via REST API: {opp.OpportunityName} ({opp.Amount:C})",
            ip);

        var resultDto = new OpportunityDto
        {
            OpportunityId = opp.OpportunityId,
            OpportunityName = opp.OpportunityName,
            CustomerId = opp.CustomerId,
            CustomerName = customer.CustomerName,
            LeadId = opp.LeadId,
            Amount = opp.Amount,
            Stage = opp.Stage,
            Probability = opp.Probability,
            WeightedAmount = (opp.Amount * opp.Probability) / 100m,
            ExpectedCloseDate = opp.ExpectedCloseDate,
            Status = opp.Status,
            CreatedDate = opp.CreatedDate,
            AssignedTo = opp.AssignedTo,
            Notes = opp.Notes
        };

        return CreatedAtAction(nameof(GetOpportunities), new { id = opp.OpportunityId }, resultDto);
    }
}
