# AcxiomCRM — Enterprise Sales and CRM Platform

An enterprise-grade Customer Relationship Management (CRM) solution built with **ASP.NET Core 8 MVC**, **Entity Framework Core 8**, **ASP.NET Core Identity**, **Bootstrap 5**, and **Chart.js**, strictly conforming to the official 19-page **AcxiomCRM Software Requirements Specification (SRS)**.

---

## Documentation Index

Comprehensive engineering documents are available in the [docs/](file:///c:/Users/vikra/Axciom-CRM/docs) folder:

- [Local Setup & Review Guide](file:///c:/Users/vikra/Axciom-CRM/setup-project.md): Step-by-step instructions for company evaluators to clone, restore, build, and run the project locally.
- [System Architecture](file:///c:/Users/vikra/Axciom-CRM/docs/architecture.md): Layered architecture, component breakdown, technology stack, and SOLID principles.
- [Workflows and Sequence Diagrams](file:///c:/Users/vikra/Axciom-CRM/docs/workflows-and-sequence-diagrams.md): Mermaid sequence diagrams for authentication, lead conversion, pipeline forecasting, follow-ups, and audit trails.
- [Business Rules and Validation](file:///c:/Users/vikra/Axciom-CRM/docs/business-rules-and-validation.md): Complete business rules catalog, client-side validation, server-side validation, and guard logic.
- [Role-Based Access Control (RBAC) Matrix](file:///c:/Users/vikra/Axciom-CRM/docs/rbac-matrix.md): Granular permission matrix, user roles (`Admin`, `Manager`, `SalesExecutive`), and data scoping.
- [REST API Specification](file:///c:/Users/vikra/Axciom-CRM/docs/api-specification.md): Complete OpenAPI-style reference, endpoints, DTOs, and HTTP status codes.
- [Acceptance Testing Report](file:///c:/Users/vikra/Axciom-CRM/docs/acceptance-testing.md): Verification report for the 14 SRS Page 19 Acceptance Scenarios and automated test suite.

---

## Section 17.20: Project Completion Standard Compliance

As mandated by Section 17.20 of the SRS:
> "A submission is not considered complete if it is only a database CRUD application. The student must demonstrate CRM workflows, client-side validation, server-side validation, business rules, secure authentication, role-based authorization, dashboard analytics, audit logging, REST API functionality and a structured application architecture."

AcxiomCRM satisfies every dimension of this standard:

1. **CRM Workflows**: End-to-end Lead-to-Customer conversion wizard, sales opportunity stage progression (`Qualification` -> `Proposal` -> `Negotiation` -> `Won` / `Lost`), follow-up rescheduling with history tracking, and sales interaction logging.
2. **Dual-Layer Validation**: Client-side validation via HTML5 and jQuery Unobtrusive Validation coupled with server-side validation models and guard clauses.
3. **Business Rules**: Opportunity amount and probability limits, non-past close dates, non-past follow-up scheduling, customer email/phone uniqueness, and weighted forecast calculations.
4. **Secure Authentication**: ASP.NET Core Identity with minimum 8-character complex passwords, 5-attempt automatic account lockout (15 minutes), anti-forgery validation on all forms, and secure cookies.
5. **Role-Based Authorization**: Three distinct security roles (`Admin`, `Manager`, `SalesExecutive`) with controller-level attribute authorization and row-level data scoping via `ICurrentUserService`.
6. **Dashboard Analytics**: 8 live KPI cards, 3 dynamic Chart.js visualizations, time-window filtering, and top opportunity deal ranking.
7. **Audit Logging**: Append-only compliance ledger tracking all entity mutations, logins, failed attempts, and administrative actions with old/new values and IP addresses.
8. **REST API Functionality**: RESTful controllers utilizing DTO contracts and standard HTTP status codes (`200`, `201`, `400`, `401`, `403`, `404`, `409`, `423`).
9. **Structured Application Architecture**: Multi-tiered architecture separating presentation, business services, domain models, data access, and security.

---

## Quick Start Guide

### 1. Prerequisites
- **.NET 8 SDK** (`8.0.425` or later)
- Modern web browser (Chrome, Edge, Firefox)

### 2. Running the Application
From the project root:
```bash
dotnet run --launch-profile http
```
The application will start listening on: **`http://localhost:5085`**

### 3. Demo Credentials
The SQLite database automatically initializes and seeds on first startup with pre-configured accounts:

| Role | Username / Email | Password | Access Scope |
| :--- | :--- | :--- | :--- |
| **Administrator** | `admin@acxiom.com` | `Admin@12345` | Full system access, User and Role administration, Audit Logs, all records and reports |
| **Sales Manager** | `manager@acxiom.com` | `Manager@12345` | Complete team CRM records, team pipeline reports, read-only audit log view |
| **Sales Executive** | `sales@acxiom.com` | `Sales@12345` | Scoped strictly to assigned Customers, Leads, Opportunities, and personal follow-ups |
| **Sales Executive 2** | `sales2@acxiom.com` | `Sales@12345` | Secondary sales representative for multi-user scoping validation |

*Note: The login page includes 1-click Demo Credentials buttons to pre-fill credentials for each role.*

### 4. Running the Automated Test Suite
To execute the automated xUnit acceptance test suite:
```bash
dotnet test AcxiomCRM.Tests/AcxiomCRM.Tests.csproj
```
**Test Results**: 9 out of 9 acceptance tests pass (100% pass rate).

---

## Application Modules Summary

### 1. Authentication and Security Module
- Self-registration securely assigns users to the `SalesExecutive` role by default to prevent privilege escalation.
- Account lockout automatically engages after 5 failed password attempts for 15 minutes.
- Anti-forgery tokens (`[ValidateAntiForgeryToken]`) validated on all POST forms.

### 2. Executive Dashboard
- **8 KPI Metrics**: Total Customers, Total Leads, Open Leads, Total Opportunities, Open Opportunities, Won Deals, Lost Deals, Total Pipeline Value.
- **3 Chart.js Visualizations**: Lead Status distribution (Doughnut), Opportunity Pipeline stages (Bar), Monthly Won Sales (Line).
- Dynamic time filters: Today, This Week, This Month, All Time.

### 3. Customer Management
- Full CRUD operations with search and active/inactive status filtering.
- Unique constraints on Email and Phone enforced both client-side and server-side.
- Duplicate business registration prevention (matching customer name and company).
- **Customer 360 View**: Tabbed profile showing linked Opportunities, Follow-Ups, Activities, and historical Audit entries.

### 4. Lead Management and Conversion
- Lead capture and qualification workflow (`New` -> `Contacted` -> `Qualified` -> `Converted` / `Lost`).
- Strict validation: `ExpectedValue > 0`.
- **Conversion Wizard**: Converts a Qualified lead into an official Customer, creates an initial Opportunity, and updates the lead status to `Converted`.

### 5. Opportunity Management
- Stage progression: `Qualification` -> `Proposal` -> `Negotiation` -> `Won` / `Lost`.
- Business rules: `Amount > 0`, `0 <= Probability <= 100`, `ExpectedCloseDate >= Today`.
- Automatic weighted forecast calculation: `(Amount * Probability) / 100`.
- Automatic status synchronization when moving to Won (100%) or Lost (0%).

### 6. Follow-Up Management
- Supports scheduling follow-ups against Customers, Leads, and Opportunities.
- Business rule: `FollowUpDate >= Today` for new and planned activities.
- Categorized views: All, Upcoming, Overdue (highlighted in red), Completed.
- Action modal for immediate completion or date rescheduling.

### 7. Activity Management
- Log customer interactions: Call, Meeting, Email, Task.
- Status tracking (`Completed`, `Scheduled`, `Pending`).

### 8. User and Role Management (Admin Only)
- Exclusive to the `Admin` role.
- User management table with active/deactivated indicators and lockout status.
- Administrative role assignments, lockout toggle (unlocking accounts), and password resets.

### 9. Audit Logging
- Read-only, append-only compliance ledger.
- Captures User ID, Action, Entity, Record ID, Old Value, New Value, Details, IP Address, and Timestamp.
- Filterable by user, entity, action, and date range. Accessible to Admin and Manager.

### 10. Reports Center
- 8 business reports: Pipeline Forecast Report, Customer Portfolio Report, Lead Acquisition Report, Opportunity Ledger, Follow-Up Execution Report, Conversion Source Analysis, Sales Team Activity Report, System Audit Report.
- Print and export support.

### 11. REST API
- Endpoints for Auth, Customers, Leads, Opportunities, Follow-Ups, and Pipeline analytics.
- Structured DTOs preventing internal entity exposure.
- Standard HTTP status codes: `200`, `201`, `400`, `401`, `403`, `404`, `409`, `423`.

---

## Verification of the 14 SRS Acceptance Scenarios

| # | Acceptance Scenario Description | Status | Verification Detail |
| :---: | :--- | :---: | :--- |
| **1** | Anonymous users cannot access protected CRM pages | **PASSED** | Unauthenticated requests to `/Customers` return `HTTP 302` redirecting to `/Account/Login`. |
| **2** | Valid registration/login reaches the dashboard | **PASSED** | Logging in with any valid role account redirects to `/Dashboard` (`HTTP 200 OK`). |
| **3** | Invalid customer email/phone is rejected by client-side validation | **PASSED** | jQuery Unobtrusive Validation intercepts invalid formats and prevents form submission. |
| **4** | Bypass browser validation with a crafted request rejected server-side | **PASSED** | Duplicate emails/phones return `ModelState` errors and `HTTP 409 Conflict` via API. |
| **5** | Create an Opportunity with Amount <= 0 is rejected | **PASSED** | Validated via xUnit test: `"Opportunity Amount must be greater than 0."` |
| **6** | Create an Opportunity with Probability > 100 is rejected | **PASSED** | Validated via xUnit test: `"Probability must be between 0 and 100."` |
| **7** | Set Expected Close Date in the past rejected for active opportunity | **PASSED** | Validated via xUnit test: `"Expected Close Date cannot be in the past for active opportunities."` |
| **8** | Create a Follow-Up dated before today rejected for new/planned items | **PASSED** | Validated via xUnit test: `"Follow-up date cannot be earlier than today."` |
| **9** | Login as SalesExecutive sees only authorized sales scope | **PASSED** | Scoped query filtering ensures sales reps only access records where `AssignedTo == UserId`. |
| **10** | Login as Manager has team pipeline and report access | **PASSED** | Managers access complete organization sales pipelines and team productivity metrics. |
| **11** | Login as Admin has user, role, and audit administration | **PASSED** | `/Users` endpoints are protected by `[Authorize(Roles = "Admin")]`. |
| **12** | Create/update/delete a CRM record produces audit entry | **PASSED** | `_auditService.LogAsync` records all mutations with old and new values in `AuditLogs`. |
| **13** | Call `/api/customers` returns authorized JSON response | **PASSED** | Returns `HTTP 200 OK` with JSON array conforming to `CustomerDto`. |
| **14** | Open Dashboard displays KPI cards and Chart.js charts with real data | **PASSED** | 8 KPI metrics and 3 Chart.js charts calculate values dynamically from database records. |

---

## Technology Stack

- **Framework**: .NET 8 LTS (C# 12)
- **Web Layer**: ASP.NET Core MVC (Model-View-Controller)
- **Persistence Layer**: Entity Framework Core 8 with SQLite
- **Security & Identity**: ASP.NET Core Identity with PBKDF2 password hashing
- **Frontend**: Razor Views, Bootstrap 5, Bootstrap Icons, Chart.js, jQuery Validation
- **Testing**: xUnit, Microsoft.AspNetCore.Mvc.Testing, Moq
