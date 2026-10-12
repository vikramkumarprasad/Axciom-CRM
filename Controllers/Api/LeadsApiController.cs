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
[Route("api/leads")]
public class LeadsApiController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly IAuditService _auditService;
    private readonly ICurrentUserService _currentUser;

    public LeadsApiController(ApplicationDbContext context, IAuditService auditService, ICurrentUserService currentUser)
    {
        _context = context;
        _auditService = auditService;
        _currentUser = currentUser;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<LeadDto>>> GetLeads([FromQuery] string? status = null)
    {
        var query = _context.Leads.AsQueryable();

        if (_currentUser.IsAuthenticated && _currentUser.IsSalesExecutive && !_currentUser.IsAdmin && !_currentUser.IsManager)
        {
            query = query.Where(l => l.AssignedTo == _currentUser.UserId);
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(l => l.Status == status);
        }

        var leads = await query
            .OrderByDescending(l => l.CreatedDate)
            .Select(l => new LeadDto
            {
                LeadId = l.LeadId,
                LeadCode = l.LeadCode,
                LeadName = l.LeadName,
                Email = l.Email,
                Phone = l.Phone,
                CompanyName = l.CompanyName,
                Source = l.Source,
                Status = l.Status,
                Priority = l.Priority,
                ExpectedValue = l.ExpectedValue,
                CreatedDate = l.CreatedDate,
                AssignedTo = l.AssignedTo
            })
            .ToListAsync();

        return Ok(leads);
    }

    [HttpPost]
    public async Task<ActionResult<LeadDto>> CreateLead([FromBody] CreateLeadDto dto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        if (dto.ExpectedValue <= 0)
        {
            return BadRequest(new { message = "Expected Value must be greater than 0." });
        }

        var lastLead = await _context.Leads.OrderByDescending(l => l.LeadId).FirstOrDefaultAsync();
        var nextCode = $"LEAD-{(lastLead?.LeadId ?? 2000) + 1}";

        var lead = new Lead
        {
            LeadCode = nextCode,
            LeadName = dto.LeadName,
            Email = dto.Email,
            Phone = dto.Phone,
            CompanyName = dto.CompanyName,
            Source = string.IsNullOrEmpty(dto.Source) ? "Website" : dto.Source,
            Status = string.IsNullOrEmpty(dto.Status) ? "New" : dto.Status,
            Priority = string.IsNullOrEmpty(dto.Priority) ? "Medium" : dto.Priority,
            ExpectedValue = dto.ExpectedValue,
            CreatedDate = DateTime.UtcNow,
            AssignedTo = dto.AssignedTo ?? _currentUser.UserId
        };

        _context.Leads.Add(lead);
        await _context.SaveChangesAsync();

        var ip = _currentUser.GetIpAddress();
        await _auditService.LogAsync(
            _currentUser.UserName ?? "API",
            "Create",
            "Lead",
            lead.LeadId.ToString(),
            null,
            lead.LeadName,
            $"Created via REST API: {lead.LeadCode}",
            ip);

        var resultDto = new LeadDto
        {
            LeadId = lead.LeadId,
            LeadCode = lead.LeadCode,
            LeadName = lead.LeadName,
            Email = lead.Email,
            Phone = lead.Phone,
            CompanyName = lead.CompanyName,
            Source = lead.Source,
            Status = lead.Status,
            Priority = lead.Priority,
            ExpectedValue = lead.ExpectedValue,
            CreatedDate = lead.CreatedDate,
            AssignedTo = lead.AssignedTo
        };

        return CreatedAtAction(nameof(GetLeads), new { id = lead.LeadId }, resultDto);
    }
}
