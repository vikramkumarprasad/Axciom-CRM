using AcxiomCRM.Models;

namespace AcxiomCRM.ViewModels;

public class DashboardViewModel
{
    // 8 KPI Cards
    public int TotalCustomers { get; set; }
    public int TotalLeads { get; set; }
    public int OpenLeads { get; set; }
    public int TotalOpportunities { get; set; }
    public int OpenOpportunities { get; set; }
    public int WonOpportunities { get; set; }
    public int LostOpportunities { get; set; }
    public decimal TotalPipelineValue { get; set; }

    // Chart 1: Lead Status
    public List<string> LeadStatusLabels { get; set; } = new();
    public List<int> LeadStatusCounts { get; set; } = new();

    // Chart 2: Opportunity Pipeline Stage
    public List<string> OpportunityStageLabels { get; set; } = new();
    public List<int> OpportunityStageCounts { get; set; } = new();
    public List<decimal> OpportunityStageAmounts { get; set; } = new();

    // Chart 3: Monthly Sales / Won Totals
    public List<string> MonthlySalesLabels { get; set; } = new();
    public List<decimal> MonthlySalesAmounts { get; set; } = new();

    // Filter and context
    public string DateFilter { get; set; } = "AllTime"; // Today, ThisWeek, ThisMonth, AllTime
    public string UserRole { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;

    // Recent items
    public List<Lead> RecentLeads { get; set; } = new();
    public List<Opportunity> TopOpportunities { get; set; } = new();
    public List<FollowUp> UpcomingFollowUps { get; set; } = new();
    public List<Activity> RecentActivities { get; set; } = new();
}
