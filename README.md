# Clinic.Api

Single-project ASP.NET Core API for the ClinicFlow AI learning app.

This repository intentionally uses a lightweight modular monolith architecture instead of microservices:

```text
Clinic.Api
  Modules/
    Identity/
    Clients/
    Appointments/
    Notifications/
    Messaging/
    AiAssistant/
  Shared/
    Data/
    Security/
    Common/
```

## Local Development

```powershell
dotnet restore Clinic.Api.slnx
dotnet build Clinic.Api.slnx
dotnet run --project src/Clinic.Api/Clinic.Api.csproj --launch-profile http
```

API:

```text
http://localhost:5290
```

Swagger:

```text
http://localhost:5290/swagger
```

## Main Endpoints

```text
POST /api/auth/register
POST /api/auth/login
GET  /api/auth/me

GET  /api/users
GET  /api/roles

GET    /api/clients
POST   /api/clients
PUT    /api/clients/{id}
DELETE /api/clients/{id}

GET   /api/appointments
POST  /api/appointments
PATCH /api/appointments/{id}/confirm
PATCH /api/appointments/{id}/cancel

POST /api/notifications/send-push
GET  /api/notifications

POST /api/messages/email
POST /api/messages/sms
GET  /api/messages

POST /api/ai/generate-message
```

Development uses EF Core InMemory storage by default. To use SQL Server LocalDB, change:

```json
"Database": {
  "Provider": "SqlServer"
}
```

and keep:

```text
Server=(localdb)\MSSQLLocalDB;Database=ClinicDb;Trusted_Connection=True;TrustServerCertificate=True
```
