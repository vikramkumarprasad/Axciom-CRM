# Business Rules and Validation Architecture

## 1. Validation Philosophy

AcxiomCRM implements a dual-layer validation framework:
1. **Client-Side Validation (Fast Feedback)**: Implemented via HTML5 attributes, jQuery Validation, and jQuery Unobtrusive Validation. This provides instant inline error messaging to the user before submitting HTTP forms, preventing unnecessary network trips.
2. **Server-Side Validation (Authoritative Enforcement)**: Enforced inside ASP.NET Core MVC controllers and API controllers. Even if client-side validation is bypassed via direct HTTP calls (e.g. Postman, curl, or automated bots), the server validates the incoming data, returns standard error models (`ModelState`, `400 Bad Request`, `409 Conflict`), and halts unauthorized operations.

---

## 2. Comprehensive Business Rules Catalog

The table below catalogs all business rules specified in the AcxiomCRM Software Requirements Specification (SRS) and explains how each is enforced at both the client and server levels.

| Rule ID | Entity / Module | Business Rule Description | Client-Side Enforcement | Server-Side Enforcement | HTTP / Response Code |
| :--- | :--- | :--- | :--- | :--- | :--- |
| **BR-01** | Security / Auth | Password complexity requires at least 8 characters, 1 uppercase letter, 1 lowercase letter, 1 digit, and 1 special symbol. | Pattern regex and length constraints on registration form. | Identity `PasswordOptions` configured in [Program.cs](file:///c:/Users/vikra/Axciom-CRM/Program.cs). | Returns `IdentityResult.Failed` with error descriptions. |
| **BR-02** | Security / Auth | Account lockout triggers automatically after 5 consecutive failed login attempts, locking out the user for 15 minutes. | Handled gracefully in UI after login rejection. | `SignInManager.PasswordSignInAsync` with `lockoutOnFailure: true`. | `200 OK` (View Error) / `423 Locked` (API). |
| **BR-03** | Security / Auth | Public user registration assigns the new account to the `SalesExecutive` role exclusively. Privilege escalation to `Admin` or `Manager` is disallowed via self-registration. | Role dropdown is omitted from public registration view. | Hardcoded assignment to `"SalesExecutive"` in [AccountController.cs](file:///c:/Users/vikra/Axciom-CRM/Controllers/AccountController.cs). | Assigned in background upon account creation. |
| **BR-04** | Customer | Customer `Email` and `Phone` must be unique across the organization. Duplicate emails or telephone numbers are rejected. | Input format masks and standard HTML5 email/phone validation. | Unique EF Core database indexes + controller duplicate check queries in [CustomersController.cs](file:///c:/Users/vikra/Axciom-CRM/Controllers/CustomersController.cs). | `ModelState.AddModelError` / `409 Conflict` (API). |
| **BR-05** | Customer | Duplicate corporate business registration (same `CustomerName` and `CompanyName`) is prevented to maintain data integrity. | Warning indicators on form submission. | Composite check in [CustomersController.cs](file:///c:/Users/vikra/Axciom-CRM/Controllers/CustomersController.cs). | `ModelState.AddModelError`. |
| **BR-06** | Customer | A Customer with linked open Opportunities cannot be deleted without reassigning or archiving those deals. | Delete confirmation modal warns the user. | Verification of child `Opportunities` count before deletion; soft deactivation is recommended. | `400 Bad Request` or redirect with error message. |
| **BR-07** | Lead | `ExpectedValue` must be strictly greater than 0 (`ExpectedValue > 0`). | HTML5 `min="0.01"` and `type="number"`. | Model validation attribute `[Range(0.01, double.MaxValue)]` + controller guard in [LeadsController.cs](file:///c:/Users/vikra/Axciom-CRM/Controllers/LeadsController.cs). | `ModelState.AddModelError` / `400 Bad Request` (API). |
| **BR-08** | Lead | Lead conversion requires a valid `Qualified` status. Converted leads are locked from further editing and cannot be converted twice. | UI displays "Convert" button only for Qualified leads. | [LeadsController.cs](file:///c:/Users/vikra/Axciom-CRM/Controllers/LeadsController.cs) verifies `lead.Status != "Converted"`. | `ModelState.AddModelError` if already converted. |
| **BR-09** | Opportunity | Opportunity `Amount` must be strictly greater than 0 (`Amount > 0`) for active deals. | HTML5 `min="0.01"` validation on amount input. | `[Range(0.01, double.MaxValue)]` attribute + controller validation in [OpportunitiesController.cs](file:///c:/Users/vikra/Axciom-CRM/Controllers/OpportunitiesController.cs). | `ModelState.AddModelError` / `400 Bad Request` (API). |
| **BR-10** | Opportunity | Opportunity `Probability` must be an integer between 0 and 100 inclusive (`0 <= Probability <= 100`). | HTML5 `min="0" max="100"` attributes. | `[Range(0, 100)]` attribute + controller validation in [OpportunitiesController.cs](file:///c:/Users/vikra/Axciom-CRM/Controllers/OpportunitiesController.cs). | `ModelState.AddModelError` / `400 Bad Request` (API). |
| **BR-11** | Opportunity | `ExpectedCloseDate` cannot be in the past for active, open opportunities. Must be today or a future date. | HTML5 `min` date attribute set to current date. | Explicit date comparison: `deal.ExpectedCloseDate < DateTime.Today` in [OpportunitiesController.cs](file:///c:/Users/vikra/Axciom-CRM/Controllers/OpportunitiesController.cs). | `ModelState.AddModelError` / `400 Bad Request` (API). |
| **BR-12** | Opportunity | Moving an Opportunity stage to `Won` automatically synchronizes `Status = "Won"` and `Probability = 100%`. Moving to `Lost` sets `Status = "Lost"` and `Probability = 0%`. | Client-side JavaScript updates probability slider automatically upon stage change. | Server-side logic explicitly forces status and probability synchronization before saving. | Handled automatically in controller logic. |
| **BR-13** | Opportunity | Weighted Forecast value is automatically computed using the formula: `(Amount * Probability) / 100`. | Computed dynamically in the UI table preview. | Calculated property on `Opportunity.WeightedAmount` and within server-side forecast aggregators. | Returned in ViewModels and API payloads. |
| **BR-14** | Follow-Up | `FollowUpDate` cannot be earlier than today for newly scheduled or planned follow-ups. | HTML5 `min` attribute set to `DateTime.Today.ToString("yyyy-MM-dd")`. | Explicit check: `model.FollowUpDate.Date < DateTime.Today` in [FollowUpsController.cs](file:///c:/Users/vikra/Axciom-CRM/Controllers/FollowUpsController.cs). | `ModelState.AddModelError` / `400 Bad Request` (API). |
| **BR-15** | Follow-Up | Rescheduling an existing follow-up requires entering a new date that is today or later, accompanied by reschedule remarks. | Modal form with date picker and required remarks textarea. | Controller validates `NewDate >= DateTime.Today` and appends history to remarks. | `ModelState.AddModelError` / `400 Bad Request` (API). |
| **BR-16** | Data Scoping | Users with the `SalesExecutive` role can only view, edit, or report on CRM records assigned to their own user ID. Managers and Admins access full organization records. | UI lists display only authorized records. | [CurrentUserService.cs](file:///c:/Users/vikra/Axciom-CRM/Services/CurrentUserService.cs) scopes all database queries by `AssignedTo == UserId` when `IsSalesExecutive == true`. | Automatic server-side query filtering. |
| **BR-17** | Audit Trail | All state changes (Create, Edit, Delete, Convert, Status Change, Reschedule, User Lock/Unlock) must generate an immutable audit log entry. Passwords and sensitive secrets are never logged. | N/A (Executed entirely server-side). | [AuditService.cs](file:///c:/Users/vikra/Axciom-CRM/Services/AuditService.cs) writes records with timestamp, user ID, IP address, old value, and new value. | Asynchronous append to `AuditLogs` table. |

---

## 3. Server-Side Guard Code Examples

### 3.1 Opportunity Amount and Date Validation
```csharp
if (opportunity.Amount <= 0)
{
    ModelState.AddModelError("Amount", "Opportunity Amount must be greater than 0.");
}

if (opportunity.Probability < 0 || opportunity.Probability > 100)
{
    ModelState.AddModelError("Probability", "Probability must be between 0 and 100.");
}

if (opportunity.Status == "Open" && opportunity.ExpectedCloseDate.Date < DateTime.Today)
{
    ModelState.AddModelError("ExpectedCloseDate", "Expected Close Date cannot be in the past for active opportunities.");
}
```

### 3.2 Follow-Up Date Rule
```csharp
if (followUp.Status == "Planned" && followUp.FollowUpDate.Date < DateTime.Today)
{
    ModelState.AddModelError("FollowUpDate", "Follow-up date cannot be earlier than today.");
}
```

### 3.3 Customer Uniqueness Enforcement
```csharp
var duplicateEmail = await _context.Customers
    .AnyAsync(c => c.Email == customer.Email && c.CustomerId != customer.CustomerId);
if (duplicateEmail)
{
    ModelState.AddModelError("Email", "A customer with this email address already exists.");
}

var duplicatePhone = await _context.Customers
    .AnyAsync(c => c.Phone == customer.Phone && c.CustomerId != customer.CustomerId);
if (duplicatePhone)
{
    ModelState.AddModelError("Phone", "A customer with this phone number already exists.");
}
```
