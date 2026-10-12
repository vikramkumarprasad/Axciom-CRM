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
[Route("api/followups")]
public class FollowUpsApiController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly IAuditService _auditService;
    private readonly ICurrentUserService _currentUser;

    public FollowUpsApiController(ApplicationDbContext context, IAuditService auditService, ICurrentUserService currentUser)
    {
        _context = context;
        _auditService = auditService;
        _currentUser = currentUser;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<FollowUpDto>>> GetFollowUps([FromQuery] string? status = null)
    {
        var query = _context.FollowUps
            .Include(f => f.Customer)
            .Include(f => f.Lead)
            .AsQueryable();

        if (_currentUser.IsAuthenticated && _currentUser.IsSalesExecutive && !_currentUser.IsAdmin && !_currentUser.IsManager)
        {
            query = query.Where(f => f.AssignedTo == _currentUser.UserId);
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(f => f.Status == status);
        }

        var list = await query
            .OrderBy(f => f.FollowUpDate)
            .Select(f => new FollowUpDto
            {
                FollowUpId = f.FollowUpId,
                CustomerId = f.CustomerId,
                CustomerName = f.Customer != null ? f.Customer.CustomerName : null,
                LeadId = f.LeadId,
                LeadName = f.Lead != null ? f.Lead.LeadName : null,
                RelatedType = f.RelatedType,
                FollowUpDate = f.FollowUpDate,
                FollowUpType = f.FollowUpType,
                Remarks = f.Remarks,
                Status = f.Status,
                AssignedTo = f.AssignedTo
            })
            .ToListAsync();

        return Ok(list);
    }

    [HttpPost]
    public async Task<ActionResult<FollowUpDto>> CreateFollowUp([FromBody] CreateFollowUpDto dto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        if (dto.Status == "Planned" && dto.FollowUpDate.Date < DateTime.Today)
        {
            return BadRequest(new { message = "Follow-up date cannot be earlier than today." });
        }

        var followUp = new FollowUp
        {
            CustomerId = dto.CustomerId,
            LeadId = dto.LeadId,
            RelatedType = dto.RelatedType,
            FollowUpDate = dto.FollowUpDate,
            FollowUpType = dto.FollowUpType,
            Remarks = dto.Remarks,
            Status = string.IsNullOrEmpty(dto.Status) ? "Planned" : dto.Status,
            AssignedTo = dto.AssignedTo ?? _currentUser.UserId
        };

        _context.FollowUps.Add(followUp);
        await _context.SaveChangesAsync();

        var ip = _currentUser.GetIpAddress();
        await _auditService.LogAsync(
            _currentUser.UserName ?? "API",
            "Create",
            "FollowUp",
            followUp.FollowUpId.ToString(),
            null,
            followUp.FollowUpType,
            $"Scheduled via REST API: {followUp.FollowUpType} on {followUp.FollowUpDate:d}",
            ip);

        var resultDto = new FollowUpDto
        {
            FollowUpId = followUp.FollowUpId,
            CustomerId = followUp.CustomerId,
            LeadId = followUp.LeadId,
            RelatedType = followUp.RelatedType,
            FollowUpDate = followUp.FollowUpDate,
            FollowUpType = followUp.FollowUpType,
            Remarks = followUp.Remarks,
            Status = followUp.Status,
            AssignedTo = followUp.AssignedTo
        };

        return CreatedAtAction(nameof(GetFollowUps), new { id = followUp.FollowUpId }, resultDto);
    }
}
