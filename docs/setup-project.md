# Local Setup and Review Guide

## 1. Overview

This document provides step-by-step instructions for engineering leads, evaluators, and developers to clone, build, run, and review the **AcxiomCRM** project in a local environment.

The application has zero external service dependencies. It uses an embedded **SQLite** database that automatically initializes, applies schema migrations, and seeds test data on first launch.

---

## 2. System Requirements

- **Operating System**: Windows 10/11, macOS (Intel/Apple Silicon), or Linux (Ubuntu 20.04+, Debian, Fedora, etc.).
- **Software Development Kit**: .NET 8.0 SDK (version `8.0.400` or higher).
  - Verify your installation in a terminal:
    ```bash
    dotnet --version
    ```
    If not installed, download the official installer from [https://dotnet.microsoft.com/download/dotnet/8.0](https://dotnet.microsoft.com/download/dotnet/8.0).
- **Web Browser**: Google Chrome, Microsoft Edge, Mozilla Firefox, or Safari.

---

## 3. Clone and Build Instructions

### Step 1: Clone the Repository
```bash
git clone <repository-url>
cd Axciom-CRM
```

### Step 2: Restore Dependencies
Restore all required NuGet packages for the web application and the test suite:
```bash
dotnet restore
```

### Step 3: Compile the Solution
Build the project to verify that there are zero compilation errors or warnings:
```bash
dotnet build
```
Expected output:
```
Build succeeded.
    0 Warning(s)
    0 Error(s)
```

---

## 4. Database Setup and Automated Seeding

No manual SQL scripts, Docker containers, or database server configurations are required.

When the application starts, [DbInitializer.cs](file:///c:/Users/vikra/Axciom-CRM/Data/DbInitializer.cs) automatically executes the following:
1. Applies Entity Framework Core schema migrations (`context.Database.MigrateAsync()`).
2. Seeds Identity security roles (`Admin`, `Manager`, `SalesExecutive`).
3. Seeds default user accounts with hashed credentials and lockout policies.
4. Populates realistic test data across Customers, Leads, Opportunities, Follow-Ups, Activities, and the immutable Audit Trail ledger.

### Resetting to a Clean State (Optional)
If you wish to reset all data back to the default factory state at any time:
1. Stop the running application (`Ctrl + C`).
2. Delete the local SQLite database file:
   - On Windows: `del AcxiomCRM.db*`
   - On Linux/macOS: `rm -f AcxiomCRM.db*`
3. Restart the application. A clean database will be automatically created and seeded.

---

## 5. Running the Web Application

Launch the application using the predefined HTTP launch profile:

```bash
dotnet run --launch-profile http
```

The application starts Kestrel and listens on:
**`http://localhost:5085`**

Open your web browser and navigate to [http://localhost:5085](http://localhost:5085). You will be presented with the AcxiomCRM login page.

---

## 6. Pre-Seeded Demonstration Accounts

The database comes pre-configured with four role accounts to facilitate immediate evaluation of security boundaries and workflow permissions:

| Security Role | Username / Email | Password | Intended Evaluation Workflow |
| :--- | :--- | :--- | :--- |
| **System Administrator** | `admin@acxiom.com` | `Admin@12345` | Evaluate User and Role administration (`/Users`), lockout unlocking, password resets, system audit logs, and organization-wide reports. |
| **Sales Manager** | `manager@acxiom.com` | `Manager@12345` | Evaluate team-wide customer views, executive pipeline stage forecasting, and supervisory activity reports. |
| **Sales Executive** | `sales@acxiom.com` | `Sales@12345` | Evaluate row-level data scoping: view only assigned customers/deals, convert assigned leads, schedule follow-ups, and log sales activities. |
| **Sales Executive 2** | `sales2@acxiom.com` | `Sales@12345` | Secondary sales representative account to verify data isolation against `sales@acxiom.com`. |

### 1-Click Login Feature
On the login screen (`/Account/Login`), click any of the **Demo Credentials** buttons at the bottom of the card to instantly pre-fill credentials for that role.

---

## 7. Running the Automated Test Suite

AcxiomCRM includes an automated xUnit test suite ([AcceptanceScenarioTests.cs](file:///c:/Users/vikra/Axciom-CRM/AcxiomCRM.Tests/AcceptanceScenarioTests.cs)) that programmatically verifies the 14 Acceptance Scenarios specified on Page 19 of the SRS.

Run the test suite from the terminal:

```bash
dotnet test AcxiomCRM.Tests/AcxiomCRM.Tests.csproj
```

Expected output:
```
Passed!  - Failed: 0, Passed: 9, Skipped: 0, Total: 9, Duration: ~260 ms
```

---

## 8. Exploring the REST API

All REST API endpoints accept and return JSON using dedicated Data Transfer Objects (DTOs).

### Example 1: Authenticate via API
```bash
curl -X POST http://localhost:5085/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"email":"admin@acxiom.com","password":"Admin@12345"}'
```

### Example 2: Retrieve Aggregated Pipeline Analytics
```bash
curl -X GET http://localhost:5085/api/reports/pipeline
```

### Example 3: Retrieve Customers List
```bash
curl -X GET http://localhost:5085/api/customers
```

---

## 9. Troubleshooting and FAQs

### Port 5085 Is Already in Use
If port `5085` is occupied by another process on your machine:
1. Open [Properties/launchSettings.json](file:///c:/Users/vikra/Axciom-CRM/Properties/launchSettings.json).
2. Modify the `"applicationUrl"` in the `"http"` profile to an alternative available port (for example, `http://localhost:5090`).
3. Re-run `dotnet run --launch-profile http`.

### Database Locked (`busy` or `locked` SQLite Error)
Ensure no other process (such as an external SQLite GUI tool or another running terminal instance) is holding an exclusive write lock on `AcxiomCRM.db`.

### Antiforgery Token Issues in Browser
If session cookies become stale during testing, perform a hard refresh (`Ctrl + Shift + R` or `Cmd + Shift + R`) or open the site in a private/incognito window.

---

## 10. Key Architecture Documentation

For in-depth technical analysis, design rationale, and sequence diagrams, refer to the files in the `docs/` directory:
- [System Architecture](file:///c:/Users/vikra/Axciom-CRM/docs/architecture.md)
- [Workflows and Sequence Diagrams](file:///c:/Users/vikra/Axciom-CRM/docs/workflows-and-sequence-diagrams.md)
- [Business Rules and Validation](file:///c:/Users/vikra/Axciom-CRM/docs/business-rules-and-validation.md)
- [Role-Based Access Control Matrix](file:///c:/Users/vikra/Axciom-CRM/docs/rbac-matrix.md)
- [REST API Specification](file:///c:/Users/vikra/Axciom-CRM/docs/api-specification.md)
- [Acceptance Testing Report](file:///c:/Users/vikra/Axciom-CRM/docs/acceptance-testing.md)
