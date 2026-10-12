using AcxiomCRM.Data;
using AcxiomCRM.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace AcxiomCRM.Controllers;

[Authorize(Roles = "Admin,Manager")]
public class AuditLogsController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public AuditLogsController(ApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<IActionResult> Index(
        [FromQuery(Name = "user")] string? user,
        [FromQuery(Name = "entity")] string? entity,
        [FromQuery(Name = "actionFilter")] string? actionFilter,
        [FromQuery(Name = "fromDate")] DateTime? fromDate,
        [FromQuery(Name = "toDate")] DateTime? toDate,
        [FromQuery(Name = "page")] int page = 1)
    {
        var query = _context.AuditLogs.AsQueryable();

        if (!string.IsNullOrWhiteSpace(user))
        {
            query = query.Where(a => a.UserId != null && a.UserId.Contains(user));
        }

        if (!string.IsNullOrWhiteSpace(entity))
        {
            query = query.Where(a => a.EntityName == entity);
        }

        if (!string.IsNullOrWhiteSpace(actionFilter))
        {
            query = query.Where(a => a.Action == actionFilter);
        }

        if (fromDate.HasValue)
        {
            query = query.Where(a => a.CreatedDate >= fromDate.Value.Date);
        }

        if (toDate.HasValue)
        {
            query = query.Where(a => a.CreatedDate <= toDate.Value.Date.AddDays(1).AddTicks(-1));
        }

        ViewData["CurrentUser"] = user;
        ViewData["CurrentEntity"] = entity;
        ViewData["CurrentAction"] = actionFilter;
        ViewData["CurrentFromDate"] = fromDate?.ToString("yyyy-MM-dd");
        ViewData["CurrentToDate"] = toDate?.ToString("yyyy-MM-dd");

        int pageSize = 15;
        var totalCount = await query.CountAsync();
        var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);
        page = Math.Max(1, Math.Min(page, Math.Max(1, totalPages)));

        var logs = await query
            .OrderByDescending(a => a.CreatedDate)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        ViewData["CurrentPage"] = page;
        ViewData["TotalPages"] = totalPages;
        ViewData["TotalCount"] = totalCount;

        var entities = await _context.AuditLogs.Select(a => a.EntityName).Distinct().ToListAsync();
        var actions = await _context.AuditLogs.Select(a => a.Action).Distinct().ToListAsync();

        ViewBag.Entities = new SelectList(entities, entity);
        ViewBag.Actions = new SelectList(actions, actionFilter);

        return View(logs);
    }
}
