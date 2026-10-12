using AcxiomCRM.Data;
using AcxiomCRM.Models;
using AcxiomCRM.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace AcxiomCRM.Controllers;

[Authorize]
public class CustomersController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly IAuditService _auditService;
    private readonly ICurrentUserService _currentUser;

    public CustomersController(ApplicationDbContext context, IAuditService auditService, ICurrentUserService currentUser)
    {
        _context = context;
        _auditService = auditService;
        _currentUser = currentUser;
    }

    public async Task<IActionResult> Index(string? search, string? status, int page = 1)
    {
        var query = _context.Customers.AsQueryable();

        // Scope access
        if (_currentUser.IsSalesExecutive && !_currentUser.IsAdmin && !_currentUser.IsManager)
        {
            query = query.Where(c => c.AssignedTo == _currentUser.UserId);
        }

        // Filtering
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLower();
            query = query.Where(c =>
                c.CustomerName.ToLower().Contains(s) ||
                c.Email.ToLower().Contains(s) ||
                c.Phone.ToLower().Contains(s) ||
                (c.CompanyName != null && c.CompanyName.ToLower().Contains(s)) ||
                c.CustomerCode.ToLower().Contains(s));
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(c => c.Status == status);
        }

        ViewData["CurrentSearch"] = search;
        ViewData["CurrentStatus"] = status;

        int pageSize = 10;
        var totalCount = await query.CountAsync();
        var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);
        page = Math.Max(1, Math.Min(page, Math.Max(1, totalPages)));

        var customers = await query
            .OrderByDescending(c => c.CreatedDate)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        ViewData["CurrentPage"] = page;
        ViewData["TotalPages"] = totalPages;
        ViewData["TotalCount"] = totalCount;

        return View(customers);
    }

    public async Task<IActionResult> Details(int id)
    {
        var customer = await _context.Customers
            .Include(c => c.Opportunities)
            .Include(c => c.FollowUps)
            .Include(c => c.Activities)
            .FirstOrDefaultAsync(c => c.CustomerId == id);

        if (customer == null)
        {
            return NotFound();
        }

        if (_currentUser.IsSalesExecutive && !_currentUser.IsAdmin && !_currentUser.IsManager && customer.AssignedTo != _currentUser.UserId)
        {
            return Forbid();
        }

        // Fetch recent audit logs for this customer
        var auditLogs = await _context.AuditLogs
            .Where(a => a.EntityName == "Customer" && a.RecordId == id.ToString())
            .OrderByDescending(a => a.CreatedDate)
            .Take(10)
            .ToListAsync();

        ViewBag.AuditLogs = auditLogs;

        return View(customer);
    }

    public async Task<IActionResult> Create()
    {
        await PopulateUsersDropdownAsync();
        var model = new Customer
        {
            CustomerCode = await GenerateCustomerCodeAsync(),
            Status = "Active"
        };
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Customer customer)
    {
        // Server-side validation: Check unique email
        if (await _context.Customers.AnyAsync(c => c.Email.ToLower() == customer.Email.ToLower()))
        {
            ModelState.AddModelError("Email", "A customer with this email address already exists.");
        }

        // Server-side validation: Check unique phone
        if (await _context.Customers.AnyAsync(c => c.Phone == customer.Phone))
        {
            ModelState.AddModelError("Phone", "A customer with this phone number already exists.");
        }

        // Server-side duplicate customer check (same business name & company)
        if (!string.IsNullOrWhiteSpace(customer.CompanyName) &&
            await _context.Customers.AnyAsync(c => c.CustomerName.ToLower() == customer.CustomerName.ToLower() &&
                                                  c.CompanyName != null && c.CompanyName.ToLower() == customer.CompanyName.ToLower()))
        {
            ModelState.AddModelError(string.Empty, "A customer with the exact same name and company already exists.");
        }

        if (ModelState.IsValid)
        {
            if (string.IsNullOrWhiteSpace(customer.CustomerCode))
            {
                customer.CustomerCode = await GenerateCustomerCodeAsync();
            }

            customer.CreatedDate = DateTime.UtcNow;
            customer.CreatedBy = _currentUser.UserName ?? "System";

            if (string.IsNullOrEmpty(customer.AssignedTo))
            {
                customer.AssignedTo = _currentUser.UserId;
            }

            _context.Customers.Add(customer);
            await _context.SaveChangesAsync();

            var ip = _currentUser.GetIpAddress();
            await _auditService.LogAsync(
                _currentUser.UserName,
                "Create",
                "Customer",
                customer.CustomerId.ToString(),
                null,
                customer.CustomerName,
                $"Created customer {customer.CustomerCode}: {customer.CustomerName} ({customer.Email})",
                ip);

            TempData["SuccessMessage"] = $"Customer '{customer.CustomerName}' created successfully!";
            return RedirectToAction(nameof(Index));
        }

        await PopulateUsersDropdownAsync();
        return View(customer);
    }

    public async Task<IActionResult> Edit(int id)
    {
        var customer = await _context.Customers.FindAsync(id);
        if (customer == null)
        {
            return NotFound();
        }

        if (_currentUser.IsSalesExecutive && !_currentUser.IsAdmin && !_currentUser.IsManager && customer.AssignedTo != _currentUser.UserId)
        {
            return Forbid();
        }

        await PopulateUsersDropdownAsync();
        return View(customer);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, Customer customer)
    {
        if (id != customer.CustomerId)
        {
            return BadRequest();
        }

        var existing = await _context.Customers.AsNoTracking().FirstOrDefaultAsync(c => c.CustomerId == id);
        if (existing == null)
        {
            return NotFound();
        }

        if (_currentUser.IsSalesExecutive && !_currentUser.IsAdmin && !_currentUser.IsManager && existing.AssignedTo != _currentUser.UserId)
        {
            return Forbid();
        }

        // Server-side uniqueness check excluding current id
        if (await _context.Customers.AnyAsync(c => c.CustomerId != id && c.Email.ToLower() == customer.Email.ToLower()))
        {
            ModelState.AddModelError("Email", "Another customer already has this email address.");
        }

        if (await _context.Customers.AnyAsync(c => c.CustomerId != id && c.Phone == customer.Phone))
        {
            ModelState.AddModelError("Phone", "Another customer already has this phone number.");
        }

        if (ModelState.IsValid)
        {
            try
            {
                customer.CreatedDate = existing.CreatedDate;
                customer.CreatedBy = existing.CreatedBy;
                customer.CustomerCode = existing.CustomerCode;

                _context.Update(customer);
                await _context.SaveChangesAsync();

                var ip = _currentUser.GetIpAddress();
                var oldVal = $"Name: {existing.CustomerName}, Email: {existing.Email}, Status: {existing.Status}";
                var newVal = $"Name: {customer.CustomerName}, Email: {customer.Email}, Status: {customer.Status}";

                await _auditService.LogAsync(
                    _currentUser.UserName,
                    "Update",
                    "Customer",
                    customer.CustomerId.ToString(),
                    oldVal,
                    newVal,
                    $"Updated customer {customer.CustomerCode}",
                    ip);

                TempData["SuccessMessage"] = $"Customer '{customer.CustomerName}' updated successfully!";
                return RedirectToAction(nameof(Index));
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!await _context.Customers.AnyAsync(e => e.CustomerId == id))
                {
                    return NotFound();
                }
                throw;
            }
        }

        await PopulateUsersDropdownAsync();
        return View(customer);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var customer = await _context.Customers
            .Include(c => c.Opportunities)
            .FirstOrDefaultAsync(c => c.CustomerId == id);

        if (customer == null)
        {
            return NotFound();
        }

        if (_currentUser.IsSalesExecutive && !_currentUser.IsAdmin && !_currentUser.IsManager && customer.AssignedTo != _currentUser.UserId)
        {
            return Forbid();
        }

        var ip = _currentUser.GetIpAddress();

        // If customer has linked opportunities, softly deactivate to maintain integrity
        if (customer.Opportunities.Any())
        {
            customer.Status = "Inactive";
            _context.Update(customer);
            await _context.SaveChangesAsync();

            await _auditService.LogAsync(
                _currentUser.UserName,
                "Deactivate",
                "Customer",
                id.ToString(),
                "Active",
                "Inactive",
                $"Deactivated customer {customer.CustomerCode} (had active opportunities)",
                ip);

            TempData["SuccessMessage"] = $"Customer '{customer.CustomerName}' deactivated (has linked opportunities).";
        }
        else
        {
            _context.Customers.Remove(customer);
            await _context.SaveChangesAsync();

            await _auditService.LogAsync(
                _currentUser.UserName,
                "Delete",
                "Customer",
                id.ToString(),
                customer.CustomerName,
                null,
                $"Deleted customer {customer.CustomerCode}",
                ip);

            TempData["SuccessMessage"] = $"Customer '{customer.CustomerName}' deleted successfully!";
        }

        return RedirectToAction(nameof(Index));
    }

    private async Task<string> GenerateCustomerCodeAsync()
    {
        var lastCustomer = await _context.Customers.OrderByDescending(c => c.CustomerId).FirstOrDefaultAsync();
        int nextNum = (lastCustomer?.CustomerId ?? 1000) + 1;
        return $"CUST-{nextNum}";
    }

    private async Task PopulateUsersDropdownAsync()
    {
        var users = await _context.Users.Where(u => u.IsActive).OrderBy(u => u.FullName).ToListAsync();
        ViewBag.Users = new SelectList(users, "Id", "FullName");
    }
}
