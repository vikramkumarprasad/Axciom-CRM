using AcxiomCRM.Models;

namespace AcxiomCRM.ViewModels;

public class ReportsViewModel
{
    public string ReportType { get; set; } = "Pipeline";
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public string? SelectedOwner { get; set; }
    public string? SelectedStatus { get; set; }

    // Pipeline Summaries
    public decimal TotalPipelineValue { get; set; }
    public decimal TotalWeightedValue { get; set; }
    public int TotalOpenDeals { get; set; }
    public int TotalWonDeals { get; set; }
    public decimal TotalWonValue { get; set; }
    public double WinRatePercentage { get; set; }

    // Breakdown tables
    public List<StageSummaryItem> StageSummaries { get; set; } = new();
    public List<OwnerSummaryItem> OwnerSummaries { get; set; } = new();
    public List<LeadConversionSummaryItem> ConversionSummaries { get; set; } = new();
    public List<UserActivitySummaryItem> UserActivitySummaries { get; set; } = new();

    // Data lists
    public List<Customer> Customers { get; set; } = new();
    public List<Lead> Leads { get; set; } = new();
    public List<Opportunity> Opportunities { get; set; } = new();
    public List<FollowUp> FollowUps { get; set; } = new();
    public List<AuditLog> AuditLogs { get; set; } = new();
}

public class StageSummaryItem
{
    public string Stage { get; set; } = string.Empty;
    public int Count { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal WeightedAmount { get; set; }
}

public class OwnerSummaryItem
{
    public string OwnerId { get; set; } = string.Empty;
    public string OwnerName { get; set; } = string.Empty;
    public int OpportunityCount { get; set; }
    public decimal TotalPipeline { get; set; }
    public decimal WonPipeline { get; set; }
}

public class LeadConversionSummaryItem
{
    public string Source { get; set; } = string.Empty;
    public int TotalLeads { get; set; }
    public int ConvertedCount { get; set; }
    public double ConversionRate => TotalLeads > 0 ? (ConvertedCount * 100.0 / TotalLeads) : 0;
}

public class UserActivitySummaryItem
{
    public string UserId { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public int ActivityCount { get; set; }
    public int FollowUpCount { get; set; }
    public int AuditEventsCount { get; set; }
}
