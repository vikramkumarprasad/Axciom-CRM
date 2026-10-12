using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AcxiomCRM.Models;

public class Lead
{
    [Key]
    public int LeadId { get; set; }

    [Required]
    [StringLength(50)]
    [Display(Name = "Lead Code")]
    public string LeadCode { get; set; } = string.Empty;

    [Required(ErrorMessage = "Lead Name is required.")]
    [StringLength(100, ErrorMessage = "Lead Name cannot exceed 100 characters.")]
    [Display(Name = "Lead Name")]
    public string LeadName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Email is required.")]
    [EmailAddress(ErrorMessage = "Enter a valid email address.")]
    [StringLength(150, ErrorMessage = "Email cannot exceed 150 characters.")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Phone number is required.")]
    [RegularExpression(@"^(\+?\d{1,4}[-.\s]?)?(\d{10}|\d{3}[-.\s]?\d{3}[-.\s]?\d{4})$", ErrorMessage = "Enter a valid phone number.")]
    [StringLength(20, ErrorMessage = "Phone number cannot exceed 20 characters.")]
    public string Phone { get; set; } = string.Empty;

    [StringLength(150)]
    [Display(Name = "Company Name")]
    public string? CompanyName { get; set; }

    [Required]
    [StringLength(50)]
    public string Source { get; set; } = "Website"; // Website, Referral, Cold Call, Social Media, Campaign, Partner

    [Required(ErrorMessage = "Lead Status is required.")]
    [StringLength(50)]
    public string Status { get; set; } = "New"; // New, Contacted, Qualified, Unqualified, Converted, Lost

    [StringLength(50)]
    public string Priority { get; set; } = "Medium"; // Low, Medium, High

    [Required(ErrorMessage = "Expected Value is required.")]
    [Range(1.0, 100000000.0, ErrorMessage = "Expected Value must be greater than 0.")]
    [Column(TypeName = "decimal(18,2)")]
    [Display(Name = "Expected Value")]
    public decimal ExpectedValue { get; set; }

    [Display(Name = "Created Date")]
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    [StringLength(450)]
    [Display(Name = "Assigned To")]
    public string? AssignedTo { get; set; }

    // Conversion tracking
    public int? ConvertedCustomerId { get; set; }
    [ForeignKey("ConvertedCustomerId")]
    public virtual Customer? ConvertedCustomer { get; set; }

    public int? ConvertedOpportunityId { get; set; }

    // Navigation properties
    public virtual ICollection<FollowUp> FollowUps { get; set; } = new List<FollowUp>();
    public virtual ICollection<Activity> Activities { get; set; } = new List<Activity>();
}
