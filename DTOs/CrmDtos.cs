using System.ComponentModel.DataAnnotations;

namespace AcxiomCRM.DTOs;

public class LoginRequestDto
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    public string Password { get; set; } = string.Empty;
}

public class CustomerDto
{
    public int CustomerId { get; set; }
    public string CustomerCode { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string? CompanyName { get; set; }
    public string? Address { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedDate { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public string? AssignedTo { get; set; }
}

public class CreateCustomerDto
{
    [Required(ErrorMessage = "Customer Name is required.")]
    [StringLength(100)]
    public string CustomerName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Email is required.")]
    [EmailAddress(ErrorMessage = "Enter a valid email address.")]
    [StringLength(150)]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Phone number is required.")]
    [RegularExpression(@"^(\+?\d{1,4}[-.\s]?)?(\d{10}|\d{3}[-.\s]?\d{3}[-.\s]?\d{4})$", ErrorMessage = "Enter a valid phone number.")]
    public string Phone { get; set; } = string.Empty;

    public string? CompanyName { get; set; }
    public string? Address { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string Status { get; set; } = "Active";
    public string? AssignedTo { get; set; }
}

public class UpdateCustomerDto
{
    [Required(ErrorMessage = "Customer Name is required.")]
    [StringLength(100)]
    public string CustomerName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Email is required.")]
    [EmailAddress(ErrorMessage = "Enter a valid email address.")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Phone number is required.")]
    public string Phone { get; set; } = string.Empty;

    public string? CompanyName { get; set; }
    public string? Address { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string Status { get; set; } = "Active";
    public string? AssignedTo { get; set; }
}

public class LeadDto
{
    public int LeadId { get; set; }
    public string LeadCode { get; set; } = string.Empty;
    public string LeadName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string? CompanyName { get; set; }
    public string Source { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
    public decimal ExpectedValue { get; set; }
    public DateTime CreatedDate { get; set; }
    public string? AssignedTo { get; set; }
}

public class CreateLeadDto
{
    [Required(ErrorMessage = "Lead Name is required.")]
    [StringLength(100)]
    public string LeadName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Email is required.")]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Phone is required.")]
    public string Phone { get; set; } = string.Empty;

    public string? CompanyName { get; set; }
    public string Source { get; set; } = "Website";
    public string Status { get; set; } = "New";
    public string Priority { get; set; } = "Medium";

    [Required(ErrorMessage = "Expected Value is required.")]
    [Range(1.0, 100000000.0, ErrorMessage = "Expected Value must be greater than 0.")]
    public decimal ExpectedValue { get; set; }

    public string? AssignedTo { get; set; }
}

public class OpportunityDto
{
    public int OpportunityId { get; set; }
    public string OpportunityName { get; set; } = string.Empty;
    public int CustomerId { get; set; }
    public string? CustomerName { get; set; }
    public int? LeadId { get; set; }
    public decimal Amount { get; set; }
    public string Stage { get; set; } = string.Empty;
    public int Probability { get; set; }
    public decimal WeightedAmount { get; set; }
    public DateTime ExpectedCloseDate { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedDate { get; set; }
    public string? AssignedTo { get; set; }
    public string? Notes { get; set; }
}

public class CreateOpportunityDto
{
    [Required(ErrorMessage = "Opportunity Name is required.")]
    public string OpportunityName { get; set; } = string.Empty;

    [Required(ErrorMessage = "CustomerId is required.")]
    public int CustomerId { get; set; }

    public int? LeadId { get; set; }

    [Required(ErrorMessage = "Amount is required.")]
    [Range(0.01, 100000000.0, ErrorMessage = "Opportunity Amount must be greater than 0.")]
    public decimal Amount { get; set; }

    [Required(ErrorMessage = "Stage is required.")]
    public string Stage { get; set; } = "Qualification";

    [Range(0, 100, ErrorMessage = "Probability must be between 0 and 100.")]
    public int Probability { get; set; } = 25;

    [Required(ErrorMessage = "Expected Close Date is required.")]
    public DateTime ExpectedCloseDate { get; set; } = DateTime.Today.AddDays(30);

    public string Status { get; set; } = "Open";
    public string? AssignedTo { get; set; }
    public string? Notes { get; set; }
}

public class FollowUpDto
{
    public int FollowUpId { get; set; }
    public int? CustomerId { get; set; }
    public string? CustomerName { get; set; }
    public int? LeadId { get; set; }
    public string? LeadName { get; set; }
    public string RelatedType { get; set; } = string.Empty;
    public DateTime FollowUpDate { get; set; }
    public string FollowUpType { get; set; } = string.Empty;
    public string? Remarks { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? AssignedTo { get; set; }
}

public class CreateFollowUpDto
{
    public int? CustomerId { get; set; }
    public int? LeadId { get; set; }
    public string RelatedType { get; set; } = "Customer";

    [Required(ErrorMessage = "FollowUpDate is required.")]
    public DateTime FollowUpDate { get; set; } = DateTime.Today;

    [Required(ErrorMessage = "FollowUpType is required.")]
    public string FollowUpType { get; set; } = "Call";

    public string? Remarks { get; set; }
    public string Status { get; set; } = "Planned";
    public string? AssignedTo { get; set; }
}
