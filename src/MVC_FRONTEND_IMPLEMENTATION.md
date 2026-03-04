# MVC Frontend Implementation - Saas.Infra

## Overview

This document describes the MVC-based frontend implementation for user authentication in Saas.Infra. The system uses proper MVC architecture with clear separation between MVC Controllers (for views) and API Controllers (for REST endpoints).

## Architecture

### Controller Types

#### 1. MVC Controllers (Views)
- **Location**: `Saas.Infra.MVC/Controllers/`
- **Purpose**: Handle user interactions, render views, manage sessions
- **Controllers**:
  - `AccountController` - Login, Register, Forgot Password
  - `DashboardController` - User dashboard
  - `HomeController` - Home page
  - `TestController` - JWT testing

#### 2. API Controllers (REST)
- **Location**: `Saas.Infra.MVC/Controllers/`
- **Purpose**: Provide REST endpoints for API clients
- **Controllers**:
  - `SsoController` - Token generation (API endpoint)
  - `UserController` - User management (API endpoint)

### Key Difference

| Aspect | MVC Controller | API Controller |
|--------|---|---|
| Returns | Views (HTML) | JSON |
| Attribute | `[Controller]` | `[ApiController]` |
| Route | `/account/login` | `/api/users/register` |
| Purpose | User UI | External API clients |
| Session | Uses session/cookies | Uses JWT tokens |

## MVC Controllers

### AccountController

**File**: `Saas.Infra.MVC/Controllers/AccountController.cs`

**Routes**:
- `GET /account/login` - Display login page
- `POST /account/login` - Handle login form
- `GET /account/register` - Display registration page
- `POST /account/register` - Handle registration form
- `GET /account/forgot-password` - Display forgot password page
- `POST /account/forgot-password` - Handle forgot password form
- `POST /account/logout` - Handle logout

**Features**:
- Calls backend API endpoints
- Manages session and cookies
- Validates user input
- Handles errors gracefully
- Logs all operations

**View Models**:
- `LoginViewModel` - Username, Password, RememberMe
- `RegisterViewModel` - Username, Password, ConfirmPassword, AgreeToTerms
- `ForgotPasswordViewModel` - Email
- `TokenResponse` - API response model

### DashboardController

**File**: `Saas.Infra.MVC/Controllers/DashboardController.cs`

**Routes**:
- `GET /dashboard` - Display user dashboard

**Features**:
- Requires valid access token
- Fetches user profile from API
- Displays user information
- Redirects to login if not authenticated

**View Models**:
- `UserProfileViewModel` - User information

## Razor Views

### Login View

**File**: `Saas.Infra.MVC/Views/Account/Login.cshtml`

**Features**:
- Username and password input
- Remember me checkbox
- Forgot password link
- Sign up link
- Error message display
- Responsive design

### Register View

**File**: `Saas.Infra.MVC/Views/Account/Register.cshtml`

**Features**:
- Username input
- Password input
- Confirm password input
- Terms of Service checkbox
- Error message display
- Responsive design

### Forgot Password View

**File**: `Saas.Infra.MVC/Views/Account/ForgotPassword.cshtml`

**Features**:
- Email input
- Info message display
- Back to login link
- Responsive design

### Dashboard View

**File**: `Saas.Infra.MVC/Views/Dashboard/Index.cshtml`

**Features**:
- User profile display
- Account information
- User dropdown menu
- Logout functionality
- Responsive design
- Dashboard cards

## API Integration

### Backend API Endpoints

#### 1. User Registration
```
POST /api/users/register
Content-Type: application/json

Request:
{
  "username": "test",
  "password": "123456",
  "clientId": "Saas.Infra.Client"
}

Response (200):
{
  "id": "guid",
  "username": "test",
  "createdAt": "2026-03-04T10:00:00Z"
}
```

#### 2. Generate Token (Login)
```
POST /sso/generate-token
Content-Type: application/json

Request:
{
  "username": "test",
  "password": "123456",
  "clientId": "Saas.Infra.Clients"
}

Response (200):
{
  "accessToken": "eyJhbGc...",
  "refreshToken": "guid",
  "expiresIn": 3600,
  "tokenType": "Bearer",
  "issuedAt": "2026-03-04T10:00:00Z"
}
```

#### 3. Get User Profile
```
GET /api/users/me
Authorization: Bearer {accessToken}

Response (200):
{
  "id": "guid",
  "username": "test",
  "createdAt": "2026-03-04T10:00:00Z"
}
```

## Session Management

### Session Storage

Tokens are stored in two places:

1. **Session** (Server-side):
   - `AccessToken` - JWT access token
   - `RefreshToken` - Refresh token
   - `ExpiresIn` - Token expiration time

2. **Cookies** (Client-side):
   - `AccessToken` - HttpOnly, Secure, SameSite=Strict
   - `RememberUsername` - Optional, 30-day expiration

### Session Configuration

In `Program.cs`:
```csharp
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
});

app.UseSession();
```

## User Flow

### Registration Flow
1. User visits `/account/register`
2. Fills registration form
3. Submits form to `POST /account/register`
4. Controller calls `POST /api/users/register` API
5. User created successfully
6. Redirects to login page

### Login Flow
1. User visits `/account/login`
2. Enters credentials
3. Submits form to `POST /account/login`
4. Controller calls `POST /sso/generate-token` API
5. Tokens received and stored in session/cookies
6. Redirects to `/dashboard`

### Dashboard Flow
1. User visits `/dashboard`
2. Controller checks for access token
3. If no token, redirects to login
4. If token exists, calls `GET /api/users/me` API
5. Displays user profile

### Logout Flow
1. User clicks logout button
2. Submits form to `POST /account/logout`
3. Session cleared
4. Cookies deleted
5. Redirects to home page

## Error Handling

### Validation Errors
- Client-side: HTML5 validation
- Server-side: ModelState validation
- Display: Error messages in views

### API Errors
- 400 Bad Request: Invalid input
- 401 Unauthorized: Invalid credentials or expired token
- 409 Conflict: Username already exists
- 500 Internal Server Error: Server error

### Error Display
- Errors shown in alert boxes
- Specific field errors shown below inputs
- User-friendly error messages

## Security Features

### Authentication
- JWT-based authentication
- Secure token storage
- HttpOnly cookies
- HTTPS only in production

### Validation
- Input validation on client and server
- CSRF protection with AntiForgeryToken
- Password strength requirements
- Email validation

### Session Security
- 30-minute idle timeout
- Secure cookie flags
- SameSite=Strict policy
- Token expiration handling

## Testing

### Test Credentials
- **Username**: `test`
- **Password**: `123456`

### Test URLs
- Login: `https://localhost:7268/account/login`
- Register: `https://localhost:7268/account/register`
- Forgot Password: `https://localhost:7268/account/forgot-password`
- Dashboard: `https://localhost:7268/dashboard`

### Manual Testing Steps

1. **Registration**:
   - Go to `/account/register`
   - Enter username, password, confirm password
   - Check terms checkbox
   - Click "Create Account"
   - Should redirect to login

2. **Login**:
   - Go to `/account/login`
   - Enter test credentials
   - Click "Sign In"
   - Should redirect to dashboard

3. **Dashboard**:
   - Should see user profile
   - Should see account information
   - Should be able to logout

## Configuration

### API Base URL

In `Program.cs`:
```csharp
_apiBaseUrl = _configuration["ApiSettings:BaseUrl"] ?? "https://localhost:7268";
```

In `appsettings.json`:
```json
{
  "ApiSettings": {
    "BaseUrl": "https://localhost:7268"
  }
}
```

### Session Timeout

In `Program.cs`:
```csharp
options.IdleTimeout = TimeSpan.FromMinutes(30);
```

## Logging

All operations are logged using Serilog:
- Login attempts
- Registration attempts
- API calls
- Errors and exceptions

Logs are written to:
- Console (development)
- File: `Logs/saas-log-*.log` (daily rotation)

## Code Quality

### XML Comments
- All public methods have XML comments
- Comments in both Chinese and English
- Parameter descriptions
- Return value descriptions
- Exception documentation

### Parameter Validation
- All public methods validate parameters
- Null checks with ArgumentNullException
- ModelState validation in controllers
- Input validation in views

### SOLID Principles
- Single Responsibility: Each controller has one purpose
- Open/Closed: Extensible without modification
- Liskov Substitution: Proper inheritance
- Interface Segregation: Focused interfaces
- Dependency Inversion: Depends on abstractions

## Build Status

✅ **Build succeeded** - All files compile without errors

## Next Steps

1. Run the application: `dotnet run`
2. Access login page: `https://localhost:7268/account/login`
3. Test registration and login flows
4. Verify dashboard displays user information
5. Test logout functionality

## Troubleshooting

### Issue: "Invalid username or password"
- Verify credentials are correct
- Check backend API is running
- Check API endpoint is accessible

### Issue: "Unauthorized" on dashboard
- Token may have expired
- Session may have timed out
- Try logging in again

### Issue: CORS errors
- Check CORS configuration in backend
- Verify frontend domain is allowed
- Check request headers

### Issue: Session not persisting
- Check session middleware is registered
- Verify cookies are enabled
- Check session timeout settings

## Support

For issues or questions:
1. Check this documentation
2. Review controller code comments
3. Check backend logs
4. Verify API endpoints are accessible
