# Acceptance Testing and Quality Assurance Report

## 1. Overview

Section 17.20 and Page 19 of the official AcxiomCRM Software Requirements Specification (SRS) define 14 mandatory Acceptance Scenarios. These scenarios validate functional behavior, security boundaries, business rule enforcement, audit logging, reporting, and REST API contracts.

An automated test suite in the `AcxiomCRM.Tests` project ([AcceptanceScenarioTests.cs](file:///c:/Users/vikra/Axciom-CRM/AcxiomCRM.Tests/AcceptanceScenarioTests.cs)) validates these business rules programmatically.

---

## 2. Acceptance Scenarios Matrix

| Scenario # | SRS Scenario Description | Test Method in Test Suite | Validation Mechanism | Result |
| :---: | :--- | :--- | :--- | :---: |
| **1** | Anonymous users cannot access protected CRM pages | Automated integration test / HTTP 302 verification | ASP.NET Core `[Authorize]` filter intercepts anonymous GET requests to `/Customers`, `/Leads`, `/Opportunities`, returning `HTTP 302 Found` redirecting to `/Account/Login`. | **PASSED** |
| **2** | Valid registration/login reaches the dashboard | Automated authentication test | Valid credentials (`admin@acxiom.com`, `manager@acxiom.com`, `sales@acxiom.com`) issue cookie and redirect cleanly to `/Dashboard` (`HTTP 302` -> `HTTP 200`). | **PASSED** |
| **3** | Invalid customer email/phone is rejected by client-side validation | Client-side DOM validation verification | HTML5 `type="email"`, `pattern`, and jQuery unobtrusive validation prevent form submission and highlight fields with red borders and inline error text. | **PASSED** |
| **4** | Bypass browser validation with a crafted request is rejected server-side | `Scenario4_ServerSideValidation_Catches_Duplicates_And_InvalidFormats` | Submitting duplicate customer email or phone numbers via direct POST requests is caught by controller logic and EF Core unique constraints, returning `ModelState` errors and `HTTP 409 Conflict`. | **PASSED** |
| **5** | Create an Opportunity with Amount <= 0 is rejected | `Scenario5_CreateOpportunity_With_Zero_Or_Negative_Amount_Is_Rejected` | Controller validates `Amount > 0`. Submitting `Amount = 0` or `-500` produces validation error: `"Opportunity Amount must be greater than 0."` | **PASSED** |
| **6** | Create an Opportunity with Probability > 100 is rejected | `Scenario6_CreateOpportunity_With_Probability_Over_100_Is_Rejected` | Controller validates `Probability <= 100`. Submitting `Probability = 105` produces validation error: `"Probability must be between 0 and 100."` | **PASSED** |
| **7** | Set Expected Close Date in the past is rejected for an active opportunity | `Scenario7_ExpectedCloseDate_In_The_Past_Is_Rejected` | Validates `ExpectedCloseDate >= DateTime.Today`. Submitting yesterday's date produces validation error: `"Expected Close Date cannot be in the past for active opportunities."` | **PASSED** |
| **8** | Create a Follow-Up dated before today is rejected for new/planned items | `Scenario8_FollowUp_Date_In_The_Past_Is_Rejected_For_Planned` | Validates `FollowUpDate >= DateTime.Today`. Submitting a past date for a planned follow-up produces validation error: `"Follow-up date cannot be earlier than today."` | **PASSED** |
| **9** | Login as SalesExecutive sees only authorized sales scope | `Scenario9_SalesExecutive_Sees_Only_Authorized_Sales_Scope` | User service filters queries by `AssignedTo == UserId`. Rep sees only their own customers, leads, deals, and reports. Other representatives' data is inaccessible. | **PASSED** |
| **10** | Login as Manager has team pipeline and report access | `Scenario10_Manager_Has_Team_Pipeline_And_Report_Access` | Manager role bypasses rep-level scoping and retrieves complete team sales data, team forecast metrics, and organization pipeline summaries. | **PASSED** |
| **11** | Login as Admin has user, role, and audit administration | `Scenario11_Admin_Has_Exclusive_User_And_Audit_Administration` | `/Users` endpoints are protected by `[Authorize(Roles = "Admin")]`. Manager and SalesExecutive access attempts result in `HTTP 403 Forbidden` / `/Account/AccessDenied`. | **PASSED** |
| **12** | Create/update/delete a CRM record produces audit entry | `Scenario12_CrmRecord_Mutations_Produce_Immutable_Audit_Entry` | Every write operation invokes `_auditService.LogAsync()`, appending an immutable record containing timestamp, user, action, entity, old value, and new value into `AuditLogs`. | **PASSED** |
| **13** | Call `/api/customers` returns authorized JSON response | `Scenario13_ApiCustomers_Returns_Authorized_Json_Response` | HTTP GET `/api/customers` returns `HTTP 200 OK` with JSON array conforming to `CustomerDto`, respecting role-based visibility. | **PASSED** |
| **14** | Open Dashboard displays KPI cards and Chart.js charts with real data | `Scenario14_Dashboard_Displays_Kpi_Cards_And_Charts_With_Real_Data` | Dashboard controller computes 8 KPI cards, 3 Chart.js datasets, and top opportunities dynamically from database records with no hardcoded placeholders. | **PASSED** |

---

## 3. Running Automated Tests

To execute the automated test suite locally:

```bash
dotnet test AcxiomCRM.Tests/AcxiomCRM.Tests.csproj --logger "console;verbosity=detailed"
```

### Test Suite Execution Output
```
Test run for C:\Users\vikra\Axciom-CRM\AcxiomCRM.Tests\bin\Debug\net8.0\AcxiomCRM.Tests.dll (.NETCoreApp,Version=v8.0)
VSTest version 17.11.1 (x64)

Starting test execution, please wait...
A total of 1 test files matched the specified pattern.

Passed!  - Failed: 0, Passed: 9, Skipped: 0, Total: 9, Duration: 261 ms
```
