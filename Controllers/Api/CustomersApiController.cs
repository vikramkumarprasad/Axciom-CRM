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
[Route("api/customers")]
public class CustomersApiController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly IAuditService _auditService;
    private readonly ICurrentUserService _currentUser;

    public CustomersApiController(ApplicationDbContext context, IAuditService auditService, ICurrentUserService currentUser)
    {
        _context = context;
        _auditService = auditService;
        _currentUser = currentUser;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<CustomerDto>>> GetCustomers([FromQuery] string? search = null)
    {
        var query = _context.Customers.AsQueryable();

        if (_currentUser.IsAuthenticated && _currentUser.IsSalesExecutive && !_currentUser.IsAdmin && !_currentUser.IsManager)
        {
            query = query.Where(c => c.AssignedTo == _currentUser.UserId);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLower();
            query = query.Where(c => c.CustomerName.ToLower().Contains(s) || c.Email.ToLower().Contains(s) || c.Phone.ToLower().Contains(s));
        }

        var customers = await query
            .OrderByDescending(c => c.CreatedDate)
            .Select(c => new CustomerDto
            {
                CustomerId = c.CustomerId,
                CustomerCode = c.CustomerCode,
                CustomerName = c.CustomerName,
                Email = c.Email,
                Phone = c.Phone,
                CompanyName = c.CompanyName,
                Address = c.Address,
                City = c.City,
                State = c.State,
                Status = c.Status,
                CreatedDate = c.CreatedDate,
                CreatedBy = c.CreatedBy,
                AssignedTo = c.AssignedTo
            })
            .ToListAsync();

        return Ok(customers);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<CustomerDto>> GetCustomer(int id)
    {
        var customer = await _context.Customers.FindAsync(id);
        if (customer == null)
        {
            return NotFound(new { message = $"Customer with ID {id} was not found." });
        }

        if (_currentUser.IsAuthenticated && _currentUser.IsSalesExecutive && !_currentUser.IsAdmin && !_currentUser.IsManager && customer.AssignedTo != _currentUser.UserId)
        {
            return Forbid();
        }

        var dto = new CustomerDto
        {
            CustomerId = customer.CustomerId,
            CustomerCode = customer.CustomerCode,
            CustomerName = customer.CustomerName,
            Email = customer.Email,
            Phone = customer.Phone,
            CompanyName = customer.CompanyName,
            Address = customer.Address,
            City = customer.City,
            State = customer.State,
            Status = customer.Status,
            CreatedDate = customer.CreatedDate,
            CreatedBy = customer.CreatedBy,
            AssignedTo = customer.AssignedTo
        };

        return Ok(dto);
    }

    [HttpPost]
    public async Task<ActionResult<CustomerDto>> CreateCustomer([FromBody] CreateCustomerDto dto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        // Uniqueness checks (409 Conflict)
        if (await _context.Customers.AnyAsync(c => c.Email.ToLower() == dto.Email.ToLower()))
        {
            return Conflict(new { message = "A customer with this email address already exists." });
        }

        if (await _context.Customers.AnyAsync(c => c.Phone == dto.Phone))
        {
            return Conflict(new { message = "A customer with this phone number already exists." });
        }

        var lastCust = await _context.Customers.OrderByDescending(c => c.CustomerId).FirstOrDefaultAsync();
        var nextCode = $"CUST-{(lastCust?.CustomerId ?? 1000) + 1}";

        var customer = new Customer
        {
            CustomerCode = nextCode,
            CustomerName = dto.CustomerName,
            Email = dto.Email,
            Phone = dto.Phone,
            CompanyName = dto.CompanyName,
            Address = dto.Address,
            City = dto.City,
            State = dto.State,
            Status = dto.Status ?? "Active",
            CreatedDate = DateTime.UtcNow,
            CreatedBy = _currentUser.UserName ?? "API",
            AssignedTo = dto.AssignedTo ?? _currentUser.UserId
        };

        _context.Customers.Add(customer);
        await _context.SaveChangesAsync();

        var ip = _currentUser.GetIpAddress();
        await _auditService.LogAsync(
            _currentUser.UserName ?? "API",
            "Create",
            "Customer",
            customer.CustomerId.ToString(),
            null,
            customer.CustomerName,
            $"Created via REST API: {customer.CustomerCode}",
            ip);

        var resultDto = new CustomerDto
        {
            CustomerId = customer.CustomerId,
            CustomerCode = customer.CustomerCode,
            CustomerName = customer.CustomerName,
            Email = customer.Email,
            Phone = customer.Phone,
            CompanyName = customer.CompanyName,
            Address = customer.Address,
            City = customer.City,
            State = customer.State,
            Status = customer.Status,
            CreatedDate = customer.CreatedDate,
            CreatedBy = customer.CreatedBy,
            AssignedTo = customer.AssignedTo
        };

        return CreatedAtAction(nameof(GetCustomer), new { id = customer.CustomerId }, resultDto);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateCustomer(int id, [FromBody] UpdateCustomerDto dto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var customer = await _context.Customers.FindAsync(id);
        if (customer == null)
        {
            return NotFound(new { message = $"Customer with ID {id} was not found." });
        }

        if (_currentUser.IsAuthenticated && _currentUser.IsSalesExecutive && !_currentUser.IsAdmin && !_currentUser.IsManager && customer.AssignedTo != _currentUser.UserId)
        {
            return Forbid();
        }

        // Uniqueness checks
        if (await _context.Customers.AnyAsync(c => c.CustomerId != id && c.Email.ToLower() == dto.Email.ToLower()))
        {
            return Conflict(new { message = "Another customer already has this email address." });
        }

        if (await _context.Customers.AnyAsync(c => c.CustomerId != id && c.Phone == dto.Phone))
        {
            return Conflict(new { message = "Another customer already has this phone number." });
        }

        var oldVal = $"Name: {customer.CustomerName}, Email: {customer.Email}, Status: {customer.Status}";

        customer.CustomerName = dto.CustomerName;
        customer.Email = dto.Email;
        customer.Phone = dto.Phone;
        customer.CompanyName = dto.CompanyName;
        customer.Address = dto.Address;
        customer.City = dto.City;
        customer.State = dto.State;
        customer.Status = dto.Status ?? customer.Status;
        if (!string.IsNullOrEmpty(dto.AssignedTo))
        {
            customer.AssignedTo = dto.AssignedTo;
        }

        _context.Update(customer);
        await _context.SaveChangesAsync();

        var ip = _currentUser.GetIpAddress();
        var newVal = $"Name: {customer.CustomerName}, Email: {customer.Email}, Status: {customer.Status}";
        await _auditService.LogAsync(
            _currentUser.UserName ?? "API",
            "Update",
            "Customer",
            id.ToString(),
            oldVal,
            newVal,
            $"Updated via REST API: {customer.CustomerCode}",
            ip);

        return Ok(new { message = "Customer updated successfully." });
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteCustomer(int id)
    {
        var customer = await _context.Customers
            .Include(c => c.Opportunities)
            .FirstOrDefaultAsync(c => c.CustomerId == id);

        if (customer == null)
        {
            return NotFound(new { message = $"Customer with ID {id} was not found." });
        }

        if (_currentUser.IsAuthenticated && _currentUser.IsSalesExecutive && !_currentUser.IsAdmin && !_currentUser.IsManager && customer.AssignedTo != _currentUser.UserId)
        {
            return Forbid();
        }

        var ip = _currentUser.GetIpAddress();

        if (customer.Opportunities.Any())
        {
            customer.Status = "Inactive";
            _context.Update(customer);
            await _context.SaveChangesAsync();

            await _auditService.LogAsync(
                _currentUser.UserName ?? "API",
                "Deactivate",
                "Customer",
                id.ToString(),
                "Active",
                "Inactive",
                $"Deactivated customer {customer.CustomerCode} via API (has linked opportunities)",
                ip);

            return Ok(new { message = "Customer has linked opportunities; status changed to Inactive." });
        }

        _context.Customers.Remove(customer);
        await _context.SaveChangesAsync();

        await _auditService.LogAsync(
            _currentUser.UserName ?? "API",
            "Delete",
            "Customer",
            id.ToString(),
            customer.CustomerName,
            null,
            $"Deleted customer {customer.CustomerCode} via API",
            ip);

        return Ok(new { message = "Customer deleted successfully." });
    }
}
