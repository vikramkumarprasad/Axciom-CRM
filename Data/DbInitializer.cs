using AcxiomCRM.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace AcxiomCRM.Data;

public static class DbInitializer
{
    public static async Task InitializeAsync(IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();

        // Ensure database created
        await context.Database.EnsureCreatedAsync();

        // 1. Seed Roles
        string[] roleNames = { "Admin", "Manager", "SalesExecutive" };
        foreach (var roleName in roleNames)
        {
            if (!await roleManager.RoleExistsAsync(roleName))
            {
                await roleManager.CreateAsync(new IdentityRole(roleName));
            }
        }

        // 2. Seed Users
        var adminUser = await userManager.FindByEmailAsync("admin@acxiom.com");
        if (adminUser == null)
        {
            adminUser = new ApplicationUser
            {
                UserName = "admin@acxiom.com",
                Email = "admin@acxiom.com",
                FullName = "System Administrator",
                EmailConfirmed = true,
                IsActive = true
            };
            var result = await userManager.CreateAsync(adminUser, "Admin@12345");
            if (result.Succeeded)
            {
                await userManager.AddToRoleAsync(adminUser, "Admin");
            }
        }

        var managerUser = await userManager.FindByEmailAsync("manager@acxiom.com");
        if (managerUser == null)
        {
            managerUser = new ApplicationUser
            {
                UserName = "manager@acxiom.com",
                Email = "manager@acxiom.com",
                FullName = "Sarah Connor (Manager)",
                EmailConfirmed = true,
                IsActive = true
            };
            var result = await userManager.CreateAsync(managerUser, "Manager@12345");
            if (result.Succeeded)
            {
                await userManager.AddToRoleAsync(managerUser, "Manager");
            }
        }

        var salesUser = await userManager.FindByEmailAsync("sales@acxiom.com");
        if (salesUser == null)
        {
            salesUser = new ApplicationUser
            {
                UserName = "sales@acxiom.com",
                Email = "sales@acxiom.com",
                FullName = "Alex Johnson (Sales Rep)",
                EmailConfirmed = true,
                IsActive = true
            };
            var result = await userManager.CreateAsync(salesUser, "Sales@12345");
            if (result.Succeeded)
            {
                await userManager.AddToRoleAsync(salesUser, "SalesExecutive");
            }
        }

        var salesUser2 = await userManager.FindByEmailAsync("sales2@acxiom.com");
        if (salesUser2 == null)
        {
            salesUser2 = new ApplicationUser
            {
                UserName = "sales2@acxiom.com",
                Email = "sales2@acxiom.com",
                FullName = "Bob Smith (Sales Rep)",
                EmailConfirmed = true,
                IsActive = true
            };
            var result = await userManager.CreateAsync(salesUser2, "Sales@12345");
            if (result.Succeeded)
            {
                await userManager.AddToRoleAsync(salesUser2, "SalesExecutive");
            }
        }

        // 3. Seed Customers if empty
        if (!await context.Customers.AnyAsync())
        {
            var customers = new List<Customer>
            {
                new Customer
                {
                    CustomerCode = "CUST-1001",
                    CustomerName = "Acme Technologies Corp",
                    Email = "contact@acmetech.com",
                    Phone = "+1 555-019-2831",
                    CompanyName = "Acme Corp",
                    Address = "100 Innovation Way, Suite 400",
                    City = "San Jose",
                    State = "California",
                    Status = "Active",
                    CreatedDate = DateTime.UtcNow.AddDays(-60),
                    CreatedBy = "admin@acxiom.com",
                    AssignedTo = salesUser?.Id
                },
                new Customer
                {
                    CustomerCode = "CUST-1002",
                    CustomerName = "Apex Logistics Ltd",
                    Email = "info@apexlogistics.com",
                    Phone = "+1 555-014-9922",
                    CompanyName = "Apex Logistics",
                    Address = "450 Freight Blvd",
                    City = "Chicago",
                    State = "Illinois",
                    Status = "Active",
                    CreatedDate = DateTime.UtcNow.AddDays(-45),
                    CreatedBy = "manager@acxiom.com",
                    AssignedTo = salesUser?.Id
                },
                new Customer
                {
                    CustomerCode = "CUST-1003",
                    CustomerName = "Summit Healthcare Partners",
                    Email = "procurement@summithealth.org",
                    Phone = "+1 555-018-7711",
                    CompanyName = "Summit Health",
                    Address = "820 Medical Center Dr",
                    City = "Boston",
                    State = "Massachusetts",
                    Status = "Active",
                    CreatedDate = DateTime.UtcNow.AddDays(-30),
                    CreatedBy = "sales@acxiom.com",
                    AssignedTo = salesUser2?.Id
                },
                new Customer
                {
                    CustomerCode = "CUST-1004",
                    CustomerName = "Nexus Financial Group",
                    Email = "support@nexusfin.com",
                    Phone = "+1 555-012-3456",
                    CompanyName = "Nexus Financial",
                    Address = "12 Wall Street, 15th Floor",
                    City = "New York",
                    State = "New York",
                    Status = "Active",
                    CreatedDate = DateTime.UtcNow.AddDays(-20),
                    CreatedBy = "sales@acxiom.com",
                    AssignedTo = salesUser?.Id
                }
            };

            await context.Customers.AddRangeAsync(customers);
            await context.SaveChangesAsync();
        }

        // 4. Seed Leads if empty
        if (!await context.Leads.AnyAsync())
        {
            var leads = new List<Lead>
            {
                new Lead
                {
                    LeadCode = "LEAD-2001",
                    LeadName = "Global Quantum Systems",
                    Email = "leads@quantumtech.io",
                    Phone = "+1 555-017-8822",
                    CompanyName = "Quantum Systems",
                    Source = "Website",
                    Status = "New",
                    Priority = "High",
                    ExpectedValue = 75000m,
                    CreatedDate = DateTime.UtcNow.AddDays(-10),
                    AssignedTo = salesUser?.Id
                },
                new Lead
                {
                    LeadCode = "LEAD-2002",
                    LeadName = "Omni Retail Solutions",
                    Email = "inquiries@omniretail.net",
                    Phone = "+1 555-013-4411",
                    CompanyName = "Omni Retail",
                    Source = "Referral",
                    Status = "Contacted",
                    Priority = "Medium",
                    ExpectedValue = 42000m,
                    CreatedDate = DateTime.UtcNow.AddDays(-8),
                    AssignedTo = salesUser?.Id
                },
                new Lead
                {
                    LeadCode = "LEAD-2003",
                    LeadName = "Starlight Mediaworks",
                    Email = "contact@starlightmedia.com",
                    Phone = "+1 555-016-5599",
                    CompanyName = "Starlight Media",
                    Source = "Campaign",
                    Status = "Qualified",
                    Priority = "High",
                    ExpectedValue = 95000m,
                    CreatedDate = DateTime.UtcNow.AddDays(-5),
                    AssignedTo = salesUser?.Id
                },
                new Lead
                {
                    LeadCode = "LEAD-2004",
                    LeadName = "Blue Horizon Ventures",
                    Email = "biz@bluehorizon.co",
                    Phone = "+1 555-011-2233",
                    CompanyName = "Blue Horizon",
                    Source = "Cold Call",
                    Status = "Unqualified",
                    Priority = "Low",
                    ExpectedValue = 18000m,
                    CreatedDate = DateTime.UtcNow.AddDays(-14),
                    AssignedTo = salesUser2?.Id
                },
                new Lead
                {
                    LeadCode = "LEAD-2005",
                    LeadName = "Vanguard Energy Corp",
                    Email = "deals@vanguardenergy.com",
                    Phone = "+1 555-015-7766",
                    CompanyName = "Vanguard Energy",
                    Source = "Partner",
                    Status = "Lost",
                    Priority = "Medium",
                    ExpectedValue = 50000m,
                    CreatedDate = DateTime.UtcNow.AddDays(-25),
                    AssignedTo = salesUser2?.Id
                }
            };

            await context.Leads.AddRangeAsync(leads);
            await context.SaveChangesAsync();
        }

        // 5. Seed Opportunities if empty
        if (!await context.Opportunities.AnyAsync())
        {
            var cust1 = await context.Customers.FirstOrDefaultAsync(c => c.CustomerCode == "CUST-1001");
            var cust2 = await context.Customers.FirstOrDefaultAsync(c => c.CustomerCode == "CUST-1002");
            var cust3 = await context.Customers.FirstOrDefaultAsync(c => c.CustomerCode == "CUST-1003");
            var cust4 = await context.Customers.FirstOrDefaultAsync(c => c.CustomerCode == "CUST-1004");

            var opps = new List<Opportunity>();

            if (cust1 != null)
            {
                opps.Add(new Opportunity
                {
                    OpportunityName = "Acme Cloud ERP Migration",
                    CustomerId = cust1.CustomerId,
                    Amount = 120000m,
                    Stage = "Negotiation",
                    Probability = 80,
                    ExpectedCloseDate = DateTime.Today.AddDays(15),
                    Status = "Open",
                    CreatedDate = DateTime.UtcNow.AddDays(-20),
                    AssignedTo = salesUser?.Id,
                    Notes = "Contract final terms under review by legal."
                });

                opps.Add(new Opportunity
                {
                    OpportunityName = "Acme Security Audit Addon",
                    CustomerId = cust1.CustomerId,
                    Amount = 35000m,
                    Stage = "Won",
                    Probability = 100,
                    ExpectedCloseDate = DateTime.Today.AddDays(-5),
                    Status = "Won",
                    CreatedDate = DateTime.UtcNow.AddDays(-40),
                    AssignedTo = salesUser?.Id,
                    Notes = "Successfully signed PO received."
                });
            }

            if (cust2 != null)
            {
                opps.Add(new Opportunity
                {
                    OpportunityName = "Apex Fleet Tracking Integration",
                    CustomerId = cust2.CustomerId,
                    Amount = 65000m,
                    Stage = "Proposal",
                    Probability = 60,
                    ExpectedCloseDate = DateTime.Today.AddDays(25),
                    Status = "Open",
                    CreatedDate = DateTime.UtcNow.AddDays(-15),
                    AssignedTo = salesUser?.Id,
                    Notes = "Delivered customized architectural demo."
                });
            }

            if (cust3 != null)
            {
                opps.Add(new Opportunity
                {
                    OpportunityName = "Summit Patient Portal Refresh",
                    CustomerId = cust3.CustomerId,
                    Amount = 180000m,
                    Stage = "Qualification",
                    Probability = 30,
                    ExpectedCloseDate = DateTime.Today.AddDays(45),
                    Status = "Open",
                    CreatedDate = DateTime.UtcNow.AddDays(-7),
                    AssignedTo = salesUser2?.Id,
                    Notes = "Stakeholder discovery workshop completed."
                });

                opps.Add(new Opportunity
                {
                    OpportunityName = "Summit Legacy Data Archival",
                    CustomerId = cust3.CustomerId,
                    Amount = 40000m,
                    Stage = "Lost",
                    Probability = 0,
                    ExpectedCloseDate = DateTime.Today.AddDays(-10),
                    Status = "Lost",
                    CreatedDate = DateTime.UtcNow.AddDays(-35),
                    AssignedTo = salesUser2?.Id,
                    Notes = "Budget deferred to next fiscal year."
                });
            }

            if (cust4 != null)
            {
                opps.Add(new Opportunity
                {
                    OpportunityName = "Nexus Trading API Connector",
                    CustomerId = cust4.CustomerId,
                    Amount = 90000m,
                    Stage = "Proposal",
                    Probability = 50,
                    ExpectedCloseDate = DateTime.Today.AddDays(20),
                    Status = "Open",
                    CreatedDate = DateTime.UtcNow.AddDays(-12),
                    AssignedTo = salesUser?.Id,
                    Notes = "Commercial terms sent."
                });
            }

            await context.Opportunities.AddRangeAsync(opps);
            await context.SaveChangesAsync();
        }

        // 6. Seed FollowUps if empty
        if (!await context.FollowUps.AnyAsync())
        {
            var cust1 = await context.Customers.FirstOrDefaultAsync(c => c.CustomerCode == "CUST-1001");
            var lead1 = await context.Leads.FirstOrDefaultAsync(l => l.LeadCode == "LEAD-2001");
            var lead3 = await context.Leads.FirstOrDefaultAsync(l => l.LeadCode == "LEAD-2003");

            var followups = new List<FollowUp>
            {
                new FollowUp
                {
                    CustomerId = cust1?.CustomerId,
                    RelatedType = "Customer",
                    FollowUpDate = DateTime.Today.AddDays(2),
                    FollowUpType = "Meeting",
                    Remarks = "Executive check-in and contract review session.",
                    Status = "Planned",
                    AssignedTo = salesUser?.Id
                },
                new FollowUp
                {
                    LeadId = lead1?.LeadId,
                    RelatedType = "Lead",
                    FollowUpDate = DateTime.Today.AddDays(1),
                    FollowUpType = "Call",
                    Remarks = "Follow up on initial technical inquiry response.",
                    Status = "Planned",
                    AssignedTo = salesUser?.Id
                },
                new FollowUp
                {
                    LeadId = lead3?.LeadId,
                    RelatedType = "Lead",
                    FollowUpDate = DateTime.Today.AddDays(3),
                    FollowUpType = "Demo",
                    Remarks = "Live platform walkthrough with media production team.",
                    Status = "Planned",
                    AssignedTo = salesUser?.Id
                },
                new FollowUp
                {
                    CustomerId = cust1?.CustomerId,
                    RelatedType = "Customer",
                    FollowUpDate = DateTime.Today.AddDays(-3),
                    FollowUpType = "Email",
                    Remarks = "Sent monthly customer satisfaction survey.",
                    Status = "Completed",
                    AssignedTo = salesUser?.Id
                }
            };

            await context.FollowUps.AddRangeAsync(followups);
            await context.SaveChangesAsync();
        }

        // 7. Seed Activities if empty
        if (!await context.Activities.AnyAsync())
        {
            var cust1 = await context.Customers.FirstOrDefaultAsync(c => c.CustomerCode == "CUST-1001");
            var lead1 = await context.Leads.FirstOrDefaultAsync(l => l.LeadCode == "LEAD-2001");

            var activities = new List<Activity>
            {
                new Activity
                {
                    ActivityType = "Meeting",
                    Subject = "Quarterly Business Review",
                    Description = "Reviewed annual milestones and roadmap.",
                    ActivityDate = DateTime.UtcNow.AddDays(-5),
                    CustomerId = cust1?.CustomerId,
                    AssignedTo = salesUser?.Id,
                    Status = "Completed"
                },
                new Activity
                {
                    ActivityType = "Call",
                    Subject = "Discovery call with VP of Engineering",
                    Description = "Discussed infrastructure requirements and timeline.",
                    ActivityDate = DateTime.UtcNow.AddDays(-2),
                    LeadId = lead1?.LeadId,
                    AssignedTo = salesUser?.Id,
                    Status = "Completed"
                },
                new Activity
                {
                    ActivityType = "Task",
                    Subject = "Prepare Customized Security Compliance Deck",
                    Description = "Assemble SOC2 and HIPAA compliance documentation for client review.",
                    ActivityDate = DateTime.UtcNow.AddDays(1),
                    CustomerId = cust1?.CustomerId,
                    AssignedTo = salesUser?.Id,
                    Status = "Scheduled"
                }
            };

            await context.Activities.AddRangeAsync(activities);
            await context.SaveChangesAsync();
        }

        // 8. Seed Initial Audit Logs
        if (!await context.AuditLogs.AnyAsync())
        {
            var initialAudits = new List<AuditLog>
            {
                new AuditLog
                {
                    UserId = "admin@acxiom.com",
                    Action = "System Initialization",
                    EntityName = "System",
                    RecordId = "0",
                    OldValue = null,
                    NewValue = "Seeded roles and demo accounts",
                    Details = "System bootstrap executed successfully.",
                    IpAddress = "127.0.0.1",
                    CreatedDate = DateTime.UtcNow.AddDays(-60)
                },
                new AuditLog
                {
                    UserId = "admin@acxiom.com",
                    Action = "Create",
                    EntityName = "Customer",
                    RecordId = "1",
                    NewValue = "Acme Technologies Corp",
                    Details = "Created customer record CUST-1001",
                    IpAddress = "127.0.0.1",
                    CreatedDate = DateTime.UtcNow.AddDays(-60)
                }
            };

            await context.AuditLogs.AddRangeAsync(initialAudits);
            await context.SaveChangesAsync();
        }
    }
}
