using System.ComponentModel.DataAnnotations;

namespace AcxiomCRM.ViewModels;

public class LeadConversionViewModel
{
    public int LeadId { get; set; }

    [Display(Name = "Lead Code")]
    public string LeadCode { get; set; } = string.Empty;

    [Required(ErrorMessage = "Customer Name is required.")]
    [StringLength(100)]
    [Display(Name = "Customer Name")]
    public string CustomerName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Email is required.")]
    [EmailAddress(ErrorMessage = "Enter a valid email address.")]
    [StringLength(150)]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Phone is required.")]
    [StringLength(20)]
    public string Phone { get; set; } = string.Empty;

    [StringLength(150)]
    [Display(Name = "Company Name")]
    public string? CompanyName { get; set; }

    [StringLength(250)]
    public string? Address { get; set; }

    [StringLength(100)]
    public string? City { get; set; }

    [StringLength(100)]
    public string? State { get; set; }

    [Display(Name = "Create Initial Opportunity")]
    public bool CreateOpportunity { get; set; } = true;

    [Display(Name = "Opportunity Name")]
    [StringLength(150)]
    public string? OpportunityName { get; set; }

    [Display(Name = "Opportunity Amount")]
    [Range(0.01, 100000000.0, ErrorMessage = "Opportunity Amount must be greater than 0.")]
    public decimal? OpportunityAmount { get; set; }

    [Display(Name = "Expected Close Date")]
    [DataType(DataType.Date)]
    public DateTime? ExpectedCloseDate { get; set; } = DateTime.Today.AddDays(30);

    [Display(Name = "Probability (%)")]
    [Range(0, 100, ErrorMessage = "Probability must be between 0 and 100.")]
    public int Probability { get; set; } = 25;
}
