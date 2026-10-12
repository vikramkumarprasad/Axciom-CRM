# REST API Specification

## 1. Overview and Conventions

AcxiomCRM provides a comprehensive suite of RESTful API endpoints for external integrations and programmatic consumption.

### Key Conventions
- **Base URI**: `http://localhost:5085/api`
- **Payload Format**: `application/json`
- **Data Transfer Objects (DTOs)**: All inputs and outputs utilize dedicated DTOs (defined in [CrmDtos.cs](file:///c:/Users/vikra/Axciom-CRM/DTOs/CrmDtos.cs)) to decouple internal database entities and protect sensitive fields (such as user password hashes and internal concurrency stamps).
- **Authentication**: Secured via cookie-based authentication or bearer token session headers.

---

## 2. Standard HTTP Status Codes

| Status Code | Reason Phrase | Usage Scenario |
| :---: | :--- | :--- |
| `200` | **OK** | Request succeeded. Response body contains requested entity or collection. |
| `201` | **Created** | Resource created successfully. Response contains created entity and `Location` header. |
| `400` | **Bad Request** | Validation failed (e.g., negative Amount, past CloseDate, missing required fields). |
| `401` | **Unauthorized** | Missing, invalid, or expired credentials. |
| `403` | **Forbidden** | Authenticated user lacks sufficient permissions for the requested resource. |
| `404` | **Not Found** | Specified entity identifier does not exist. |
| `409` | **Conflict** | Resource conflict detected (e.g., duplicate customer Email or Phone number). |
| `423` | **Locked** | Account locked out due to exceeding maximum failed login attempts. |

---

## 3. Endpoints Reference

### 3.1 Authentication Endpoints

#### POST `/api/auth/login`
Authenticates a user and establishes an authorized session.

- **Request Body**:
```json
{
  "email": "admin@acxiom.com",
  "password": "Admin@12345",
  "rememberMe": false
}
```
- **Responses**:
  - `200 OK`:
    ```json
    {
      "message": "Login successful",
      "userId": "d1c2b3a4-...",
      "email": "admin@acxiom.com",
      "fullName": "System Administrator",
      "roles": ["Admin"]
    }
    ```
  - `401 Unauthorized`:
    ```json
    {
      "message": "Invalid email or password"
    }
    ```
  - `423 Locked`:
    ```json
    {
      "message": "Account is locked out. Please try again after 15 minutes."
    }
    ```

#### POST `/api/auth/logout`
Terminates the authenticated session and writes an audit log entry.
- **Responses**:
  - `200 OK`:
    ```json
    {
      "message": "Logged out successfully"
    }
    ```

---

### 3.2 Customer Endpoints

#### GET `/api/customers`
Retrieves a list of customers, filterable by search term and status. Scoped by user role.
- **Query Parameters**:
  - `search` (optional, string): Filter by customer name, company, email, or code.
  - `status` (optional, string): Filter by status (`Active` or `Inactive`).
- **Response**: `200 OK` (Array of `CustomerDto`).

#### GET `/api/customers/{id}`
Retrieves details of a single customer by ID.
- **Responses**:
  - `200 OK`: `CustomerDto`
  - `404 Not Found`: Customer not found or outside user's scope.

#### POST `/api/customers`
Creates a new customer. Enforces uniqueness on Email and Phone.
- **Request Body**:
```json
{
  "customerName": "Global Logistics Corp",
  "email": "contact@globallogistics.com",
  "phone": "555-0199",
  "companyName": "Global Logistics Inc",
  "address": "742 Evergreen Terrace",
  "city": "Springfield",
  "state": "IL",
  "status": "Active"
}
```
- **Responses**:
  - `201 Created`: Returns newly created `CustomerDto` with HTTP `Location` header.
  - `400 Bad Request`: Validation failure.
  - `409 Conflict`: Customer with this Email or Phone already exists.

#### PUT `/api/customers/{id}`
Updates an existing customer profile.
- **Responses**:
  - `200 OK`: Updated `CustomerDto`.
  - `400 Bad Request`: Validation failure or mismatched ID.
  - `404 Not Found`: Customer not found.
  - `409 Conflict`: New Email or Phone conflicts with another customer.

#### DELETE `/api/customers/{id}`
Deletes or deactivates a customer.
- **Responses**:
  - `200 OK`: Deletion confirmed.
  - `400 Bad Request`: Cannot delete customer with linked active opportunities.
  - `404 Not Found`: Customer not found.

---

### 3.3 Lead Endpoints

#### GET `/api/leads`
Retrieves a list of leads. Scoped by user role.
- **Query Parameters**:
  - `search` (optional, string): Name, company, or lead code.
  - `status` (optional, string): Status filter (`New`, `Contacted`, `Qualified`, `Converted`, `Lost`).
- **Response**: `200 OK` (Array of `LeadDto`).

#### POST `/api/leads`
Creates a new lead. Enforces `ExpectedValue > 0`.
- **Request Body**:
```json
{
  "leadName": "Jane Doe",
  "email": "jane@techcorp.com",
  "phone": "555-0144",
  "companyName": "TechCorp",
  "source": "Website",
  "status": "New",
  "priority": "High",
  "expectedValue": 45000.00
}
```
- **Responses**:
  - `201 Created`: Created `LeadDto`.
  - `400 Bad Request`: Missing fields or `ExpectedValue <= 0`.

---

### 3.4 Opportunity Endpoints

#### GET `/api/opportunities`
Retrieves a list of opportunities with weighted calculations.
- **Response**: `200 OK` (Array of `OpportunityDto`).

#### POST `/api/opportunities`
Creates a new sales opportunity. Enforces `Amount > 0`, `0 <= Probability <= 100`, and `ExpectedCloseDate >= Today`.
- **Request Body**:
```json
{
  "opportunityName": "Enterprise Cloud Migration",
  "customerId": 1,
  "amount": 75000.00,
  "stage": "Proposal",
  "probability": 60,
  "expectedCloseDate": "2026-12-15T00:00:00Z",
  "status": "Open",
  "notes": "Decision expected by end of quarter."
}
```
- **Responses**:
  - `201 Created`: Created `OpportunityDto` with calculated `weightedAmount: 45000.00`.
  - `400 Bad Request`: Validation error (e.g. past close date, negative amount, probability > 100).

---

### 3.5 Follow-Up Endpoints

#### GET `/api/followups`
Retrieves follow-up tasks.
- **Query Parameters**:
  - `status` (optional, string): `Planned`, `Completed`, `Missed`, or `Cancelled`.
- **Response**: `200 OK` (Array of `FollowUpDto`).

#### POST `/api/followups`
Schedules a follow-up action. Enforces `FollowUpDate >= Today`.
- **Request Body**:
```json
{
  "customerId": 1,
  "followUpDate": "2026-10-25T14:00:00Z",
  "followUpType": "Call",
  "remarks": "Review final proposal details."
}
```
- **Responses**:
  - `201 Created`: Created `FollowUpDto`.
  - `400 Bad Request`: Date earlier than today.

---

### 3.6 Reports & Pipeline Analytics Endpoints

#### GET `/api/reports/pipeline`
Calculates and returns aggregated pipeline metrics, weighted forecast values, and stage breakdowns.
- **Response**: `200 OK`
```json
{
  "totalPipelineValue": 280000.00,
  "totalWeightedPipelineValue": 164000.00,
  "totalOpenOpportunities": 4,
  "totalWonOpportunities": 3,
  "totalWonValue": 185000.00,
  "winRatePercentage": 42.86,
  "stages": [
    { "stage": "Qualification", "count": 1, "totalAmount": 45000.00, "weightedAmount": 9000.00 },
    { "stage": "Proposal", "count": 2, "totalAmount": 125000.00, "weightedAmount": 75000.00 },
    { "stage": "Negotiation", "count": 1, "totalAmount": 110000.00, "weightedAmount": 80000.00 },
    { "stage": "Won", "count": 3, "totalAmount": 185000.00, "weightedAmount": 185000.00 },
    { "stage": "Lost", "count": 0, "totalAmount": 0.00, "weightedAmount": 0.00 }
  ]
}
```
