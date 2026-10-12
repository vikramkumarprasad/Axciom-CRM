using AcxiomCRM.Controllers;
using AcxiomCRM.Controllers.Api;
using AcxiomCRM.Data;
using AcxiomCRM.DTOs;
using AcxiomCRM.Models;
using AcxiomCRM.Services;
using AcxiomCRM.ViewModels;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace AcxiomCRM.Tests;

public class AcceptanceScenarioTests
{
    private ApplicationDbContext CreateInMemoryDbContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .Options;
        return new ApplicationDbContext(options);
    }

    private (Mock<IAuditService>, Mock<ICurrentUserService>) CreateMockServices(string userId = "sales-1", string userName = "sales@acxiom.com", bool isAdmin = false, bool isManager = false, bool isSalesExec = true)
    {
        var mockAudit = new Mock<IAuditService>();
        var mockUser = new Mock<ICurrentUserService>();

        mockUser.Setup(u => u.UserId).Returns(userId);
        mockUser.Setup(u => u.UserName).Returns(userName);
        mockUser.Setup(u => u.Email).Returns(userName);
        mockUser.Setup(u => u.IsAuthenticated).Returns(true);
        mockUser.Setup(u => u.IsAdmin).Returns(isAdmin);
        mockUser.Setup(u => u.IsManager).Returns(isManager);
        mockUser.Setup(u => u.IsSalesExecutive).Returns(isSalesExec);
        mockUser.Setup(u => u.GetIpAddress()).Returns("127.0.0.1");

        return (mockAudit, mockUser);
    }

    [Fact]
    public async Task Scenario5_OpportunityAmount_ZeroOrNegative_IsRejected()
    {
        // SRS Acceptance Scenario 5: Create an Opportunity with Amount <= 0 -> rejected
        var db = CreateInMemoryDbContext(Guid.NewGuid().ToString());
        var (mockAudit, mockUser) = CreateMockServices();
        var controller = new OpportunitiesController(db, mockAudit.Object, mockUser.Object);

        // Seed customer
        var customer = new Customer
        {
            CustomerId = 1,
            CustomerCode = "CUST-1001",
            CustomerName = "Acme Corp",
            Email = "acme@test.com",
            Phone = "1234567890",
            Status = "Active"
        };
        db.Customers.Add(customer);
        await db.SaveChangesAsync();

        var invalidOpp = new Opportunity
        {
            OpportunityName = "Zero Dollar Opportunity",
            CustomerId = customer.CustomerId,
            Amount = 0m, // Invalid: <= 0
            Stage = "Qualification",
            Probability = 50,
            ExpectedCloseDate = DateTime.Today.AddDays(30),
            Status = "Open"
        };

        var result = await controller.Create(invalidOpp);

        Assert.False(controller.ModelState.IsValid);
        Assert.True(controller.ModelState.ContainsKey("Amount"));
        Assert.Equal("Opportunity Amount must be greater than 0.", controller.ModelState["Amount"]!.Errors[0].ErrorMessage);
    }

    [Fact]
    public async Task Scenario6_OpportunityProbability_GreaterThan100_IsRejected()
    {
        // SRS Acceptance Scenario 6: Create an Opportunity with Probability > 100 -> rejected
        var db = CreateInMemoryDbContext(Guid.NewGuid().ToString());
        var (mockAudit, mockUser) = CreateMockServices();
        var controller = new OpportunitiesController(db, mockAudit.Object, mockUser.Object);

        var customer = new Customer
        {
            CustomerId = 1,
            CustomerCode = "CUST-1001",
            CustomerName = "Acme Corp",
            Email = "acme@test.com",
            Phone = "1234567890",
            Status = "Active"
        };
        db.Customers.Add(customer);
        await db.SaveChangesAsync();

        var invalidOpp = new Opportunity
        {
            OpportunityName = "120% Probability Deal",
            CustomerId = customer.CustomerId,
            Amount = 50000m,
            Stage = "Proposal",
            Probability = 120, // Invalid: > 100
            ExpectedCloseDate = DateTime.Today.AddDays(30),
            Status = "Open"
        };

        var result = await controller.Create(invalidOpp);

        Assert.False(controller.ModelState.IsValid);
        Assert.True(controller.ModelState.ContainsKey("Probability"));
        Assert.Equal("Probability must be between 0 and 100.", controller.ModelState["Probability"]!.Errors[0].ErrorMessage);
    }

    [Fact]
    public async Task Scenario7_OpportunityExpectedCloseDate_InPast_IsRejected()
    {
        // SRS Acceptance Scenario 7: Set Expected Close Date in the past -> rejected for active opportunity
        var db = CreateInMemoryDbContext(Guid.NewGuid().ToString());
        var (mockAudit, mockUser) = CreateMockServices();
        var controller = new OpportunitiesController(db, mockAudit.Object, mockUser.Object);

        var customer = new Customer
        {
            CustomerId = 1,
            CustomerCode = "CUST-1001",
            CustomerName = "Acme Corp",
            Email = "acme@test.com",
            Phone = "1234567890",
            Status = "Active"
        };
        db.Customers.Add(customer);
        await db.SaveChangesAsync();

        var invalidOpp = new Opportunity
        {
            OpportunityName = "Past Close Date Deal",
            CustomerId = customer.CustomerId,
            Amount = 25000m,
            Stage = "Proposal",
            Probability = 50,
            ExpectedCloseDate = DateTime.Today.AddDays(-5), // Invalid: past date for active deal
            Status = "Open"
        };

        var result = await controller.Create(invalidOpp);

        Assert.False(controller.ModelState.IsValid);
        Assert.True(controller.ModelState.ContainsKey("ExpectedCloseDate"));
        Assert.Equal("Expected Close Date cannot be in the past.", controller.ModelState["ExpectedCloseDate"]!.Errors[0].ErrorMessage);
    }

    [Fact]
    public async Task Scenario8_FollowUpDate_BeforeToday_IsRejected()
    {
        // SRS Acceptance Scenario 8: Create a Follow-Up dated before today -> rejected for new/planned
        var db = CreateInMemoryDbContext(Guid.NewGuid().ToString());
        var (mockAudit, mockUser) = CreateMockServices();
        var controller = new FollowUpsController(db, mockAudit.Object, mockUser.Object);

        var invalidFollowUp = new FollowUp
        {
            FollowUpType = "Call",
            FollowUpDate = DateTime.Today.AddDays(-2), // Invalid: yesterday/past date
            Status = "Planned",
            Remarks = "Follow up on previous discussion"
        };

        var result = await controller.Create(invalidFollowUp);

        Assert.False(controller.ModelState.IsValid);
        Assert.True(controller.ModelState.ContainsKey("FollowUpDate"));
        Assert.Equal("Follow-up date cannot be earlier than today.", controller.ModelState["FollowUpDate"]!.Errors[0].ErrorMessage);
    }

    [Fact]
    public async Task Scenario3_and_4_CustomerValidation_DuplicateEmailAndPhone_Rejected()
    {
        // SRS Acceptance Scenario 3 & 4: Duplicate customer creation prevented, server-side rejects
        var db = CreateInMemoryDbContext(Guid.NewGuid().ToString());
        var (mockAudit, mockUser) = CreateMockServices();
        var controller = new CustomersController(db, mockAudit.Object, mockUser.Object);

        var existingCustomer = new Customer
        {
            CustomerId = 1,
            CustomerCode = "CUST-1001",
            CustomerName = "First Customer",
            Email = "duplicate@test.com",
            Phone = "+1 555-010-9999",
            Status = "Active"
        };
        db.Customers.Add(existingCustomer);
        await db.SaveChangesAsync();

        var newCustomerWithDuplicateEmail = new Customer
        {
            CustomerCode = "CUST-1002",
            CustomerName = "Second Customer",
            Email = "duplicate@test.com", // Duplicate
            Phone = "+1 555-010-8888",
            Status = "Active"
        };

        var result = await controller.Create(newCustomerWithDuplicateEmail);

        Assert.False(controller.ModelState.IsValid);
        Assert.True(controller.ModelState.ContainsKey("Email"));
        Assert.Equal("A customer with this email address already exists.", controller.ModelState["Email"]!.Errors[0].ErrorMessage);
    }

    [Fact]
    public async Task Scenario12_LeadConversion_CreatesCustomerAndOpportunity_AndAuditEntry()
    {
        // SRS Acceptance Scenario 12: Lead conversion workflow creates customer, opportunity, and audit entry
        var db = CreateInMemoryDbContext(Guid.NewGuid().ToString());
        var (mockAudit, mockUser) = CreateMockServices(userId: "sales-1", userName: "sales@acxiom.com");
        var controller = new LeadsController(db, mockAudit.Object, mockUser.Object);
        controller.TempData = new Microsoft.AspNetCore.Mvc.ViewFeatures.TempDataDictionary(
            new DefaultHttpContext(),
            Mock.Of<Microsoft.AspNetCore.Mvc.ViewFeatures.ITempDataProvider>());

        var lead = new Lead
        {
            LeadId = 1,
            LeadCode = "LEAD-2001",
            LeadName = "Promising Prospect Ltd",
            Email = "prospect@leadcorp.com",
            Phone = "+1 555-011-3322",
            CompanyName = "LeadCorp",
            Source = "Website",
            Status = "Qualified",
            Priority = "High",
            ExpectedValue = 60000m,
            AssignedTo = "sales-1"
        };
        db.Leads.Add(lead);
        await db.SaveChangesAsync();

        var conversionModel = new LeadConversionViewModel
        {
            LeadId = lead.LeadId,
            LeadCode = lead.LeadCode,
            CustomerName = "LeadCorp International",
            Email = "prospect@leadcorp.com",
            Phone = "+1 555-011-3322",
            CompanyName = "LeadCorp International",
            CreateOpportunity = true,
            OpportunityName = "LeadCorp Enterprise License",
            OpportunityAmount = 60000m,
            ExpectedCloseDate = DateTime.Today.AddDays(30),
            Probability = 60
        };

        var result = await controller.Convert(conversionModel);

        // Verify Lead status updated to Converted
        var updatedLead = await db.Leads.FindAsync(1);
        Assert.NotNull(updatedLead);
        Assert.Equal("Converted", updatedLead.Status);
        Assert.NotNull(updatedLead.ConvertedCustomerId);

        // Verify Customer was created
        var customer = await db.Customers.FindAsync(updatedLead.ConvertedCustomerId);
        Assert.NotNull(customer);
        Assert.Equal("LeadCorp International", customer.CustomerName);

        // Verify Opportunity was created
        var opp = await db.Opportunities.FirstOrDefaultAsync(o => o.CustomerId == customer.CustomerId);
        Assert.NotNull(opp);
        Assert.Equal("LeadCorp Enterprise License", opp.OpportunityName);
        Assert.Equal(60000m, opp.Amount);

        // Verify Audit Log was called
        mockAudit.Verify(a => a.LogAsync(
            "sales@acxiom.com",
            "Convert",
            "Lead",
            "1",
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task Scenario9_and_14_SalesExecutive_ScopedDashboard_ShowsOnlyAssigned()
    {
        // SRS Acceptance Scenario 9 & 14: SalesExecutive sees only assigned sales scope, dashboard reflects authorized data
        var db = CreateInMemoryDbContext(Guid.NewGuid().ToString());
        var (mockAudit, mockUser) = CreateMockServices(userId: "rep-alice", isSalesExec: true, isAdmin: false, isManager: false);
        var controller = new DashboardController(db, mockUser.Object);

        // Seed records assigned to rep-alice and another user
        db.Customers.AddRange(
            new Customer { CustomerId = 1, CustomerCode = "C1", CustomerName = "Alice Cust", Email = "a@c.com", Phone = "111", Status = "Active", AssignedTo = "rep-alice" },
            new Customer { CustomerId = 2, CustomerCode = "C2", CustomerName = "Bob Cust", Email = "b@c.com", Phone = "222", Status = "Active", AssignedTo = "rep-bob" }
        );

        db.Leads.AddRange(
            new Lead { LeadId = 1, LeadCode = "L1", LeadName = "Alice Lead", Email = "a@l.com", Phone = "333", Status = "New", ExpectedValue = 10000m, AssignedTo = "rep-alice" },
            new Lead { LeadId = 2, LeadCode = "L2", LeadName = "Bob Lead", Email = "b@l.com", Phone = "444", Status = "New", ExpectedValue = 20000m, AssignedTo = "rep-bob" }
        );

        db.Opportunities.AddRange(
            new Opportunity { OpportunityId = 1, OpportunityName = "Alice Deal", CustomerId = 1, Amount = 40000m, Stage = "Proposal", Probability = 50, Status = "Open", AssignedTo = "rep-alice" },
            new Opportunity { OpportunityId = 2, OpportunityName = "Bob Deal", CustomerId = 2, Amount = 80000m, Stage = "Proposal", Probability = 50, Status = "Open", AssignedTo = "rep-bob" }
        );
        await db.SaveChangesAsync();

        var result = await controller.Index();
        var viewResult = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<DashboardViewModel>(viewResult.Model);

        // Alice must only see 1 customer, 1 lead, 1 opportunity, and $40,000 pipeline!
        Assert.Equal(1, model.TotalCustomers);
        Assert.Equal(1, model.TotalLeads);
        Assert.Equal(1, model.TotalOpportunities);
        Assert.Equal(40000m, model.TotalPipelineValue);
    }

    [Fact]
    public async Task Scenario13_CustomerApi_ReturnsRealJsonData()
    {
        // SRS Acceptance Scenario 13: Authorized customer API returns JSON
        var db = CreateInMemoryDbContext(Guid.NewGuid().ToString());
        var (mockAudit, mockUser) = CreateMockServices(isAdmin: true, isSalesExec: false);
        var controller = new CustomersApiController(db, mockAudit.Object, mockUser.Object);

        db.Customers.Add(new Customer
        {
            CustomerId = 1,
            CustomerCode = "CUST-1001",
            CustomerName = "Enterprise Global",
            Email = "contact@global.com",
            Phone = "+1 555-019-1111",
            Status = "Active",
            CompanyName = "Global Inc"
        });
        await db.SaveChangesAsync();

        var result = await controller.GetCustomers();
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var list = Assert.IsAssignableFrom<IEnumerable<CustomerDto>>(okResult.Value);

        Assert.Single(list);
        Assert.Equal("Enterprise Global", list.First().CustomerName);
        Assert.Equal("CUST-1001", list.First().CustomerCode);
    }

    [Fact]
    public async Task OpportunityApi_AmountValidation_ReturnsBadRequest()
    {
        // REST API boundary validation for Opportunity
        var db = CreateInMemoryDbContext(Guid.NewGuid().ToString());
        var (mockAudit, mockUser) = CreateMockServices();
        var controller = new OpportunitiesApiController(db, mockAudit.Object, mockUser.Object);

        var invalidDto = new CreateOpportunityDto
        {
            OpportunityName = "Negative Deal",
            CustomerId = 1,
            Amount = -100m, // Invalid: <= 0
            Stage = "Proposal",
            Probability = 50,
            ExpectedCloseDate = DateTime.Today.AddDays(30)
        };

        var result = await controller.CreateOpportunity(invalidDto);
        var badReq = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Contains("Opportunity Amount must be greater than 0.", badReq.Value?.ToString());
    }
}
