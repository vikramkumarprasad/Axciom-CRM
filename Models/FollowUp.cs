using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AcxiomCRM.Models;

public class FollowUp
{
    [Key]
    public int FollowUpId { get; set; }

    [Display(Name = "Customer")]
    public int? CustomerId { get; set; }

    [ForeignKey("CustomerId")]
    public virtual Customer? Customer { get; set; }

    [Display(Name = "Lead")]
    public int? LeadId { get; set; }

    [ForeignKey("LeadId")]
    public virtual Lead? Lead { get; set; }

    [StringLength(50)]
    [Display(Name = "Related Type")]
    public string RelatedType { get; set; } = "Customer"; // Customer, Lead, Opportunity

    public int? RelatedId { get; set; }

    [Required(ErrorMessage = "Follow-Up Date is required.")]
    [DataType(DataType.Date)]
    [Display(Name = "Follow-Up Date")]
    public DateTime FollowUpDate { get; set; } = DateTime.Today;

    [Required(ErrorMessage = "Follow-Up Type is required.")]
    [StringLength(50)]
    [Display(Name = "Follow-Up Type")]
    public string FollowUpType { get; set; } = "Call"; // Call, Meeting, Email, Demo

    [StringLength(1000)]
    public string? Remarks { get; set; }

    [Required]
    [StringLength(50)]
    public string Status { get; set; } = "Planned"; // Planned, Completed, Missed, Cancelled

    [StringLength(450)]
    [Display(Name = "Assigned To")]
    public string? AssignedTo { get; set; }
}
