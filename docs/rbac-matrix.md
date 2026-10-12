# Role-Based Access Control (RBAC) Matrix

## 1. Overview

AcxiomCRM implements a three-tier Role-Based Access Control (RBAC) architecture enforced at both the endpoint level using ASP.NET Core `[Authorize(Roles = "...")]` attributes and at the data access level via query scoping in [CurrentUserService.cs](file:///c:/Users/vikra/Axciom-CRM/Services/CurrentUserService.cs).

---

## 2. System Roles

1. **Administrator (`Admin`)**:
   - Complete administrative authority across all modules, configuration, and security controls.
   - Exclusive access to user management, role assignments, user status activation/deactivation, account unlock capabilities, and password resets.
   - Unrestricted access to the regulatory audit log ledger and executive reporting.
2. **Sales Manager (`Manager`)**:
   - Supervisory authority across the sales team and pipeline.
   - Access to all team customers, leads, opportunities, follow-ups, and sales activities.
   - Comprehensive pipeline forecasting and team productivity reports.
   - Read-only access to audit trail entries for team oversight.
3. **Sales Executive (`SalesExecutive`)**:
   - Operational sales representative role.
   - Access is strictly scoped to records assigned directly to their user account (`AssignedTo == CurrentUserId`).
   - May create, update, and manage their assigned prospects, customer contacts, sales deals, and follow-ups.
   - Restricted from viewing team-wide records or other representatives' pipelines.
   - Strictly prohibited from accessing administrative modules (`UsersController`, system audit settings).

---

## 3. Granular Module & Action Permission Matrix

| Module / Resource | Action | Administrator (`Admin`) | Sales Manager (`Manager`) | Sales Executive (`SalesExecutive`) | Notes / Security Rule |
| :--- | :--- | :---: | :---: | :---: | :--- |
| **Authentication** | Login / Logout | Allowed | Allowed | Allowed | All roles can access authentication actions. |
| **Authentication** | Self-Registration | Allowed | Allowed | Allowed | Defaults strictly to `SalesExecutive` role. |
| **Dashboard** | View Dashboard | Allowed (Full org) | Allowed (Full team) | Allowed (Scoped) | Sales reps see only their assigned deals and metrics. |
| **Dashboard** | Date Range Filtering | Allowed | Allowed | Allowed | Supports Today, This Week, This Month, All Time. |
| **Customers** | View Customer List | Allowed (All) | Allowed (All) | Allowed (Scoped) | Reps only see customers where `AssignedTo == UserId`. |
| **Customers** | Customer 360 View | Allowed | Allowed | Allowed (Scoped) | Displays linked deals, follow-ups, and activities. |
| **Customers** | Create Customer | Allowed | Allowed | Allowed | Reps create customers assigned to themselves. |
| **Customers** | Edit Customer | Allowed | Allowed | Allowed (Assigned) | Reps can only edit their own customers. |
| **Customers** | Delete Customer | Allowed | Allowed | Prohibited | Deletion is restricted to management and admins. |
| **Leads** | View Lead List | Allowed (All) | Allowed (All) | Allowed (Scoped) | Reps only see leads assigned to them. |
| **Leads** | Create Lead | Allowed | Allowed | Allowed | Reps capture incoming prospects. |
| **Leads** | Edit / Qualify Lead | Allowed | Allowed | Allowed (Assigned) | Reps can advance status to Contacted or Qualified. |
| **Leads** | Convert Lead | Allowed | Allowed | Allowed (Assigned) | Converts Qualified lead to Customer & Opportunity. |
| **Leads** | Delete Lead | Allowed | Allowed | Prohibited | Deletion restricted to management. |
| **Opportunities** | View Pipeline List | Allowed (All) | Allowed (All) | Allowed (Scoped) | Reps only see their assigned deals. |
| **Opportunities** | Create Opportunity | Allowed | Allowed | Allowed | Validates Amount > 0 and Future Close Date. |
| **Opportunities** | Edit / Progress Deal | Allowed | Allowed | Allowed (Assigned) | Reps manage their assigned deals through stages. |
| **Opportunities** | Delete Opportunity | Allowed | Allowed | Prohibited | Deletion restricted to management. |
| **Follow-Ups** | View Follow-Ups | Allowed (All) | Allowed (All) | Allowed (Scoped) | Shows Upcoming, Overdue, and Completed items. |
| **Follow-Ups** | Schedule Follow-Up | Allowed | Allowed | Allowed | Follow-up date must be today or in the future. |
| **Follow-Ups** | Complete / Reschedule | Allowed | Allowed | Allowed (Assigned) | Reps can log outcome or pick a new future date. |
| **Activities** | Log Sales Activity | Allowed | Allowed | Allowed | Logs calls, meetings, emails, tasks against records. |
| **User Administration** | View Users List | Allowed | Prohibited | Prohibited | Endpoint restricted with `[Authorize(Roles = "Admin")]`. |
| **User Administration** | Assign / Change Roles | Allowed | Prohibited | Prohibited | Admins assign `Admin`, `Manager`, or `SalesExecutive`. |
| **User Administration** | Lock / Unlock Account | Allowed | Prohibited | Prohibited | Admin can unlock accounts or manually lock users. |
| **User Administration** | Reset Passwords | Allowed | Prohibited | Prohibited | Admin-assisted credential reset workflow. |
| **Audit Logs** | View Audit Ledger | Allowed | Allowed (View-only) | Prohibited | Read-only ledger; no delete or edit permitted. |
| **Reports** | Pipeline & Forecast | Allowed (Org) | Allowed (Team) | Allowed (Personal) | Displays weighted forecasts and win rates. |
| **Reports** | Team Activity Reports | Allowed | Allowed | Prohibited | Executive productivity reporting. |
| **REST APIs** | `/api/customers` | Allowed | Allowed | Allowed (Scoped) | Fully secured via cookie/bearer auth context. |
| **REST APIs** | `/api/leads` | Allowed | Allowed | Allowed (Scoped) | RESTful operations on prospects. |
| **REST APIs** | `/api/opportunities` | Allowed | Allowed | Allowed (Scoped) | RESTful operations on deals. |
| **REST APIs** | `/api/followups` | Allowed | Allowed | Allowed (Scoped) | RESTful operations on action items. |
| **REST APIs** | `/api/reports/pipeline`| Allowed | Allowed | Allowed (Scoped) | Aggregated pipeline JSON payload. |

---

## 4. Row-Level Data Scoping Implementation

Data scoping is implemented through the `ICurrentUserService` interface and leveraged across all entity controllers.

```csharp
// Example query scoping in CustomersController
var query = _context.Customers.AsQueryable();

// If current user is a Sales Executive, scope query strictly to assigned records
if (_currentUser.IsSalesExecutive)
{
    query = query.Where(c => c.AssignedTo == _currentUser.UserId);
}
```

This pattern ensures that even if a Sales Executive attempts to guess or manually modify URL identifiers (such as `/Customers/Details/5`), the query fails to find the record and returns an `HTTP 404 Not Found` or `HTTP 403 Forbidden` response.
