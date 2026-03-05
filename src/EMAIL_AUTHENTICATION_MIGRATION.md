# Email-Based Authentication Migration

## Overview
Migrated the authentication system from username-based login to email-based login with optional auto-generated usernames.

## Changes Made

### 1. Backend - Data Layer (`Saas.Infra.Data`)

#### UserRepository.cs
- Added `GetByEmailAsync(string email)` method to query users by email address
- Maintains existing `GetByUsernameAsync()` for backward compatibility

#### IUserRepository.cs (Interface)
- Added `GetByEmailAsync(string email)` method signature with XML documentation

### 2. Backend - SSO Service (`Saas.Infra.SSO`)

#### SsoService.cs
- **GenerateTokensAsync()**: Changed parameter from `userId` (username) to `email`
  - Now queries user by email instead of username
  - Validates credentials against email-based lookup
  
- **RegisterUserAsync()**: Updated signature
  - Changed first parameter from `username` to `email` (required)
  - Changed `displayName` parameter to `username` (optional)
  - Added auto-generation logic: if username not provided, generates `user_{8-char-guid}`
  - Ensures generated username is unique by checking database
  - Validates email uniqueness before registration
  
- **GenerateUsername()**: New private helper method
  - Generates random usernames in format: `user_{8-char-guid}`
  - Ensures uniqueness by checking database

#### ISsoService.cs (Interface)
- Updated `GenerateTokensAsync()` signature: `email` parameter instead of `userId`
- Updated `RegisterUserAsync()` signature: `email` (required), `username` (optional)
- Updated XML documentation for both methods

### 3. Backend - API Controllers (`Saas.Infra.MVC/Controllers/Api`)

#### SsoController.cs
- Updated `GenerateToken()` endpoint to use `LoginRequest.Email` instead of `LoginRequest.Username`
- Updated logging to reference email instead of username
- Updated error messages to reference email

#### UserController.cs
- Updated `Register()` method to accept `RegisterRequest` instead of `LoginRequest`
- Updated method to pass email and optional username to `RegisterUserAsync()`
- Updated logging and error messages to reference email

### 4. Backend - Models (`Saas.Infra.MVC/Models`)

#### LoginRequest.cs
- Changed `Username` property to `Email` (required, email validation)
- Updated XML documentation (bilingual: Chinese + English)
- Updated validation error messages to English

#### RegisterRequest.cs (New File)
- Created new model for registration requests
- Properties:
  - `Email` (required, email validation)
  - `Username` (optional, 3-100 characters)
  - `Password` (required, 6-100 characters)
  - `ClientId` (optional)
- Includes comprehensive XML documentation (bilingual)

### 5. Frontend - MVC Controllers (`Saas.Infra.MVC/Controllers/Mvc`)

#### AccountController.cs
- **Login()** method:
  - Updated to send `email` instead of `username` to backend
  - Updated logging to reference email
  - Updated error messages to reference email
  - Updated cookie name from `RememberUsername` to `RememberEmail`
  
- **Register()** method:
  - Updated to send `email` and optional `username` to backend
  - Updated logging to reference email
  - Updated error messages to reference email

- **LoginViewModel**:
  - Already uses `Email` property (was updated previously)
  
- **RegisterViewModel**:
  - Already has `Email` (required) and `Username` (optional) properties (was updated previously)

### 6. Frontend - Views (No Changes Required)
- `Saas.Infra.MVC/Views/Account/Login.cshtml` - Already uses email field
- `Saas.Infra.MVC/Views/Account/Register.cshtml` - Already uses email + optional username fields

## Database Schema
- No database schema changes required
- Existing `Email` column in `Users` table is used for login
- `UserName` column remains for internal username storage

## API Endpoint Changes

### Login Endpoint: `POST /sso/generate-token`
**Before:**
```json
{
  "username": "john_doe",
  "password": "password123",
  "clientId": "Saas.Infra.Clients"
}
```

**After:**
```json
{
  "email": "john@example.com",
  "password": "password123",
  "clientId": "Saas.Infra.Clients"
}
```

### Registration Endpoint: `POST /api/users/register`
**Before:**
```json
{
  "username": "john_doe",
  "password": "password123",
  "clientId": "Saas.Infra.Client"
}
```

**After:**
```json
{
  "email": "john@example.com",
  "username": "john_doe",  // optional - auto-generated if not provided
  "password": "password123",
  "clientId": "Saas.Infra.Client"
}
```

## Build Status
- ✅ Build succeeded with 0 warnings and 0 errors
- All projects compile successfully:
  - Saas.Infra.Core
  - Saas.Infra.Data
  - Saas.Infra.SSO
  - Saas.Infra.MVC

## Testing Recommendations

### Login Flow
1. Register new user with email: `test@example.com`, optional username
2. System auto-generates username if not provided
3. Login with email and password
4. Verify token generation and session storage

### Registration Flow
1. Register with email only (username auto-generated)
2. Register with email and custom username
3. Verify duplicate email rejection
4. Verify duplicate username rejection

### Edge Cases
1. Email already exists → Conflict error
2. Username already exists → Conflict error
3. Missing email → Bad request
4. Invalid email format → Bad request
5. Missing password → Bad request
6. Password too short → Bad request

## Files Modified
1. `Saas.Infra.Core/IUserRepository.cs`
2. `Saas.Infra.Data/UserRepository.cs`
3. `Saas.Infra.SSO/ISsoService.cs`
4. `Saas.Infra.SSO/SsoService.cs`
5. `Saas.Infra.MVC/Models/LoginRequest.cs`
6. `Saas.Infra.MVC/Models/RegisterRequest.cs` (new)
7. `Saas.Infra.MVC/Controllers/Api/SsoController.cs`
8. `Saas.Infra.MVC/Controllers/Api/UserController.cs`
9. `Saas.Infra.MVC/Controllers/Mvc/AccountController.cs`

## Backward Compatibility
- Username-based queries still supported via `GetByUsernameAsync()`
- Existing usernames in database are preserved
- Auto-generated usernames follow consistent format: `user_{8-char-guid}`
