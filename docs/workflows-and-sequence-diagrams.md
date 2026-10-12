# CRM Workflows and Sequence Diagrams

## 1. Overview

This document illustrates the end-to-end business workflows and runtime sequence diagrams of the core processes in AcxiomCRM. Each diagram maps the communication between actors, controllers, services, database contexts, and audit logging pipelines.

---

## 2. Authentication, Session Creation, and Account Lockout Workflow

This workflow illustrates how user login credentials are validated, how consecutive failure counts trigger automatic account lockout, and how audit logs record every security event.

```mermaid
sequenceDiagram
    autonumber
    actor User as User / Browser
    participant AC as AccountController
    participant SM as SignInManager<ApplicationUser>
    participant UM as UserManager<ApplicationUser>
    participant AS as IAuditService
    participant DB as ApplicationDbContext (SQLite)

    User->>AC: POST /Account/Login (Email, Password, Token)
    AC->>AC: Validate Anti-Forgery Token & ModelState
    alt ModelState Invalid
        AC-->>User: 200 OK (Return Login View with Validation Summary)
    end

    AC->>UM: FindByEmailAsync(Email)
    UM->>DB: Query AspNetUsers by NormalizedEmail
    DB-->>UM: Return ApplicationUser record
    UM-->>AC: User entity

    alt User Is Inactive (IsActive == false)
        AC->>AS: LogAsync(Email, "Failed Login", "Auth", null, "Account deactivated")
        AS->>DB: INSERT AuditLog
        AC-->>User: 200 OK (Error: Account has been deactivated)
    end

    AC->>SM: PasswordSignInAsync(Email, Password, RememberMe, lockoutOnFailure: true)
    SM->>DB: Check Password Hash & AccessFailedCount
    
    alt Password Valid & Not Locked Out
        SM->>DB: Reset AccessFailedCount = 0
        SM-->>AC: SignInResult.Success
        AC->>AS: LogAsync(Email, "Login", "Auth", UserId, "User successfully logged in")
        AS->>DB: INSERT AuditLog
        AC-->>User: 302 Found (Redirect to /Dashboard)
    else Account is Locked Out (AccessFailedCount >= 5)
        SM-->>AC: SignInResult.IsLockedOut
        AC->>AS: LogAsync(Email, "Account Locked", "Auth", UserId, "Exceeded 5 failed attempts")
        AS->>DB: INSERT AuditLog
        AC-->>User: 200 OK (Error: Account locked for 15 minutes)
    else Password Invalid
        SM->>DB: Increment AccessFailedCount += 1
        SM-->>AC: SignInResult.Failed
        AC->>AS: LogAsync(Email, "Failed Login", "Auth", UserId, "Invalid credentials")
        AS->>DB: INSERT AuditLog
        AC-->>User: 200 OK (Error: Invalid login attempt)
    end
```

---

## 3. Lead Qualification and Conversion Workflow

This workflow represents the transition of a prospect from qualification to an active Customer and Opportunity, maintaining referential integrity across entities.

```mermaid
sequenceDiagram
    autonumber
    actor Sales as Sales Executive
    participant LC as LeadsController
    participant DB as ApplicationDbContext
    participant AS as IAuditService

    Sales->>LC: GET /Leads/Convert/{leadId}
    LC->>DB: Query Lead by Id (Include Assigned User)
    DB-->>LC: Lead record (ExpectedValue, Company, Contact info)
    LC-->>Sales: 200 OK (Render Conversion Wizard Form)

    Sales->>LC: POST /Leads/Convert (LeadId, CustomerName, OpportunityName, Amount, Stage)
    LC->>LC: Validate Business Rules (Amount > 0, Valid Names)
    
    rect rgb(240, 248, 255)
    Note over LC,DB: Database Transaction Scope
    LC->>DB: Check existing Customer by Email/Phone
    alt Customer does not exist
        LC->>DB: Generate Unique CustomerCode (e.g. CUST-00010)
        LC->>DB: INSERT Customer (Status="Active", AssignedTo=Lead.AssignedTo)
        DB-->>LC: Created CustomerId
    else Existing Customer found
        LC-->>DB: Reuse existing CustomerId
    end

    opt Create Linked Opportunity
        LC->>DB: INSERT Opportunity (CustomerId, LeadId, Amount, Stage, Probability, Status="Open")
        DB-->>LC: Created OpportunityId
    end

    LC->>DB: UPDATE Lead SET Status="Converted", ConvertedCustomerId, ConvertedOpportunityId
    LC->>AS: LogAsync("Lead Conversion", "Lead", LeadId, OldStatus="Qualified", NewStatus="Converted")
    LC->>AS: LogAsync("Customer Creation", "Customer", CustomerId, Details="Created via Lead conversion")
    LC->>DB: SaveChangesAsync()
    end

    LC-->>Sales: 302 Found (Redirect to /Customers/Details/{customerId})
```

---

## 4. Opportunity Pipeline Stage Progression and Weighted Forecast Workflow

This workflow shows the transition of an Opportunity through sales stages, automatic recalculation of win probabilities, and dynamic forecast aggregation.

```mermaid
sequenceDiagram
    autonumber
    actor Rep as Sales Executive / Manager
    participant OC as OpportunitiesController
    participant DB as ApplicationDbContext
    participant AS as IAuditService

    Rep->>OC: POST /Opportunities/Edit/{id} (Stage="Proposal", Amount=50000, Probability=40, CloseDate)
    OC->>OC: Validate Amount > 0
    OC->>OC: Validate Probability in range [0, 100]
    OC->>OC: Validate ExpectedCloseDate >= Today (for active deals)

    alt Validation Failed
        OC-->>Rep: 200 OK (Return View with inline validation errors)
    end

    OC->>DB: Find Opportunity by Id
    DB-->>OC: Existing Opportunity Entity

    OC->>OC: Compute Weighted Forecast = (Amount * Probability) / 100
    
    alt Stage changed to "Won"
        OC->>OC: Set Status = "Won", Probability = 100
    else Stage changed to "Lost"
        OC->>OC: Set Status = "Lost", Probability = 0
    else Stage is "Qualification", "Proposal", or "Negotiation"
        OC->>OC: Set Status = "Open"
    end

    OC->>DB: UPDATE Opportunity SET Stage, Status, Probability, Amount, Notes
    OC->>AS: LogAsync("Edit", "Opportunity", Id, OldValues, NewValues, Details)
    AS->>DB: INSERT AuditLog
    OC->>DB: SaveChangesAsync()

    OC-->>Rep: 302 Found (Redirect to /Opportunities/Index)
```

---

## 5. Follow-Up Scheduling, Rescheduling, and Overdue Processing Workflow

This workflow handles scheduling client follow-ups, date validation rules, status transitions, and prompt modal rescheduling.

```mermaid
sequenceDiagram
    autonumber
    actor User as Sales Executive
    participant FC as FollowUpsController
    participant DB as ApplicationDbContext
    participant AS as IAuditService

    Note over User,FC: 1. New Follow-Up Creation
    User->>FC: POST /FollowUps/Create (FollowUpDate, Type, CustomerId/LeadId, Remarks)
    FC->>FC: Enforce Rule: FollowUpDate >= DateTime.Today
    alt FollowUpDate < DateTime.Today
        FC-->>User: 200 OK (Error: Follow-up date cannot be in the past)
    end
    FC->>DB: INSERT FollowUp (Status="Planned")
    FC->>AS: LogAsync("Create", "FollowUp", Id, Remarks)
    FC->>DB: SaveChangesAsync()
    FC-->>User: 302 Found (Redirect to /FollowUps/Index)

    Note over User,FC: 2. Follow-Up Completion / Rescheduling
    alt Mark Complete
        User->>FC: POST /FollowUps/Complete/{id} (Remarks)
        FC->>DB: UPDATE FollowUp SET Status="Completed", Remarks
        FC->>AS: LogAsync("Status Change", "FollowUp", Id, "Completed")
        FC->>DB: SaveChangesAsync()
        FC-->>User: 302 Found (Refresh Follow-Ups list)
    else Reschedule Action
        User->>FC: POST /FollowUps/Reschedule/{id} (NewDate, Remarks)
        FC->>FC: Validate NewDate >= DateTime.Today
        FC->>DB: UPDATE FollowUp SET FollowUpDate=NewDate, Remarks, Status="Planned"
        FC->>AS: LogAsync("Reschedule", "FollowUp", Id, OldDate, NewDate)
        FC->>DB: SaveChangesAsync()
        FC-->>User: 302 Found (Refresh Follow-Ups list)
    end
```

---

## 6. Immutable Append-Only Audit Trail Pipeline

Every mutation across security and business domain entities flows into the centralized audit logging service.

```mermaid
sequenceDiagram
    autonumber
    participant Controller as Application Controller
    participant Service as IAuditService
    participant Context as ApplicationDbContext
    participant Ledger as SQLite AuditLogs Table

    Controller->>Service: LogAsync(userId, action, entityName, recordId, oldValue, newValue, details, ip)
    Service->>Service: Capture UTC Timestamp (DateTime.UtcNow)
    Service->>Service: Instantiate AuditLog model (Sanitized: No passwords)
    Service->>Context: AuditLogs.Add(auditEntry)
    Service->>Context: SaveChangesAsync()
    Context->>Ledger: INSERT INTO AuditLogs (Action, EntityName, RecordId, ...)
    Ledger-->>Context: Acknowledged (AuditLogId generated)
    Context-->>Service: Completed Task
    Service-->>Controller: Return Task Complete

    Note over Ledger: Audit records cannot be updated or deleted by any user or API endpoint.
```

---

## 7. REST API Authentication and Protected Request Lifecycle

This workflow documents how external consumers or automated integration systems interact with the AcxiomCRM REST API endpoints.

```mermaid
sequenceDiagram
    autonumber
    actor Client as API Client / Integration Partner
    participant AuthApi as AuthApiController (/api/auth)
    participant CustApi as CustomersApiController (/api/customers)
    participant AuthMiddleware as Authentication & Scoping Middleware
    participant DB as ApplicationDbContext

    Client->>AuthApi: POST /api/auth/login {"email":"admin@acxiom.com", "password":"Admin@12345"}
    AuthApi->>DB: Verify credentials & lockout status
    alt Credentials Invalid
        AuthApi-->>Client: 401 Unauthorized {"message":"Invalid credentials"}
    else Account Locked Out
        AuthApi-->>Client: 423 Locked {"message":"Account locked out"}
    else Success
        AuthApi-->>Client: 200 OK {"userId":"...", "email":"...", "roles":["Admin"]}
    end

    Note over Client,CustApi: Protected Endpoint Call
    Client->>CustApi: POST /api/customers {"customerName":"Acme Inc", "email":"rep@acme.com", "phone":"555-0100"}
    CustApi->>AuthMiddleware: Validate Cookie / Authentication Context
    alt Unauthenticated
        AuthMiddleware-->>Client: 401 Unauthorized
    end

    CustApi->>DB: Check uniqueness: Email == "rep@acme.com" OR Phone == "555-0100"
    alt Conflict Detected
        CustApi-->>Client: 409 Conflict {"message":"Customer with this Email or Phone already exists"}
    else Valid Data
        CustApi->>DB: INSERT Customer
        DB-->>CustApi: CustomerId = 15
        CustApi-->>Client: 201 Created (Location: /api/customers/15, CustomerDto)
    end
```
