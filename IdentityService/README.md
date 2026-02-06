# Identity Service - Clean Architecture

## Microservice quản lý Identity và Access Management

### Kiến trúc dự án
```
IdentityService/
├── src/
│   ├── IdentityService.API/          # API Layer - Controllers, Middleware
│   ├── IdentityService.Application/  # Application Layer - Services, DTOs
│   ├── IdentityService.Domain/       # Domain Layer - Entities, Interfaces
│   └── IdentityService.Infrastructure/ # Infrastructure - Database, Security
└── IdentityService.sln
```

### Clean Architecture Layers:
- **Domain**: Entities (User, Role), Repository Interfaces
- **Application**: Business Logic, DTOs, Service Interfaces
- **Infrastructure**: EF Core, Repositories, JWT, Password Hashing
- **API**: Controllers, Dependency Injection, Configuration

---

## Công nghệ sử dụng

- **.NET 8.0** - Framework
- **ASP.NET Core Web API** - REST API
- **Entity Framework Core 8.0** - ORM
- **SQL Server** - Database
- **JWT (JSON Web Tokens)** - Authentication
- **ASP.NET Core Identity** - Password Hashing
- **Swagger/OpenAPI** - API Documentation

---

## Cấu hình Database

**Connection String** (trong appsettings.json):
```json
"ConnectionStrings": {
  "IdentityDB": "Server=localhost;Database=IdentityDB;User Id=sa;Password=12345;TrustServerCertificate=True;"
}
```

**Lưu ý**: Đảm bảo đã chạy script SQL để tạo database và seed dữ liệu.

---

## Tính năng đã triển khai

### 1. **Login** (`POST /api/auth/login`)
- Xác thực email và password
- Kiểm tra trạng thái tài khoản (ACTIVE/INACTIVE/SUSPENDED)
- Tạo Access Token (JWT) và Refresh Token
- Lưu thông tin đăng nhập (IP address, thời gian)
- Trả về thông tin user và role

### 2. **Logout** (`POST /api/auth/logout`)
- Vô hiệu hóa Refresh Token
- Xóa session

### 3. **Refresh Token** (`POST /api/auth/refresh`)
- Tạo Access Token mới từ Refresh Token
- Kiểm tra thời hạn và tính hợp lệ
- Tạo Refresh Token mới

### 4. **Get Current User** (`GET /api/auth/me`)
- Endpoint bảo vệ bằng JWT
- Trả về thông tin user đã đăng nhập

---

## JWT Configuration

**appsettings.json**:
```json
"Jwt": {
  "SecretKey": "YourSuperSecretKeyThatIsAtLeast32CharactersLongForHS256Algorithm",
  "Issuer": "IdentityService",
  "Audience": "IdentityServiceClient",
  "ExpiryMinutes": 60,
  "RefreshTokenExpiryDays": 7
}
```

**Claims trong JWT**:
- `sub`: User ID
- `email`: Email
- `role`: Role name (Admin, Manager, Cashier, etc.)
- `role_id`: Role ID
- `status`: User status

---

## Cài đặt và chạy

### 1. Khôi phục packages
```bash
cd "c:\Users\win\Downloads\fsa clean\IdentityService"
dotnet restore
```

### 2. Build solution
```bash
dotnet build
```

### 3. Chạy API
```bash
cd "src\IdentityService.API"
dotnet run
```

API sẽ chạy tại: **http://localhost:5000** hoặc **https://localhost:5001**

Swagger UI: **http://localhost:5000** (trang chủ)

---

## Test API với Swagger

1. Mở browser: `http://localhost:5000`
2. Sử dụng endpoint **POST /api/auth/login**:
```json
{
  "email": "admin@company.com",
  "password": "Password123!"
}
```
3. Copy `accessToken` từ response
4. Click nút **Authorize** (ở góc trên bên phải)
5. Nhập: `Bearer {accessToken}` (có dấu cách sau Bearer)
6. Click **Authorize** → **Close**
7. Giờ có thể test endpoint bảo vệ: `GET /api/auth/me`

---

## Tài khoản test

**Admin**:
- Email: `admin@company.com`
- Password: `Password123!`

**Manager**:
- Email: `manager1@company.com`
- Password: `Password123!`

**Cashier**:
- Email: `cashier1@company.com`
- Password: `Password123!`

**Customer**:
- Email: `customer1@gmail.com`
- Password: `Password123!`

---

## API Endpoints

| Method | Endpoint | Description | Auth Required |
|--------|----------|-------------|---------------|
| POST | `/api/auth/login` | Đăng nhập | No |
| POST | `/api/auth/logout` | Đăng xuất | No |
| POST | `/api/auth/refresh` | Refresh token | No |
| GET | `/api/auth/me` | Lấy thông tin user hiện tại | Yes (JWT) |

---

## Response Format

**Success Response**:
```json
{
  "success": true,
  "message": "Login successful",
  "data": {
    "accessToken": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
    "refreshToken": "base64-encoded-token",
    "expiresAt": "2026-02-10T12:00:00Z",
    "user": {
      "id": "guid",
      "email": "admin@company.com",
      "fullName": "System Administrator",
      "role": {
        "id": "guid",
        "name": "Admin",
        "description": "System Administrator - Full Access"
      },
      "status": "ACTIVE",
      "emailVerified": false
    }
  }
}
```

**Error Response**:
```json
{
  "success": false,
  "message": "Invalid email or password"
}
```

---

## Cấu trúc Clean Architecture

### Domain Layer (Core)
- **Entities**: User, Role
- **Repository Interfaces**: IUserRepository, IRoleRepository
- Không phụ thuộc vào layer nào khác

### Application Layer
- **Services**: AuthService
- **DTOs**: LoginRequestDto, LoginResponseDto, UserDto, RoleDto
- **Interfaces**: IAuthService, IJwtService, IPasswordHasher
- Phụ thuộc: Domain

### Infrastructure Layer
- **DbContext**: IdentityDbContext
- **Repositories**: UserRepository, RoleRepository
- **Security**: JwtService, PasswordHasherService
- Phụ thuộc: Domain, Application

### API Layer (Presentation)
- **Controllers**: AuthController
- **Configuration**: Program.cs, appsettings.json
- Phụ thuộc: Application, Infrastructure

---

## CORS Configuration

Mặc định cho phép các origin:
- `http://localhost:3000` (React)
- `http://localhost:4200` (Angular)
- `http://localhost:5173` (Vite)

Cấu hình trong appsettings.json:
```json
"Cors": {
  "AllowedOrigins": [
    "http://localhost:3000",
    "http://localhost:4200",
    "http://localhost:5173"
  ]
}
```

---

## Security Features

- **Password Hashing**: ASP.NET Core Identity PasswordHasher
- **JWT Authentication**: HS256 algorithm
- **Refresh Token**: 7 days expiry
- **Access Token**: 60 minutes expiry
- **User Status Check**: ACTIVE/INACTIVE/SUSPENDED
- **Last Login Tracking**: IP address, timestamp

---

## Lưu ý quan trọng

1. **JWT Secret Key**: Đổi secret key trong production
2. **Connection String**: Cập nhật thông tin database nếu cần
3. **HTTPS**: Sử dụng HTTPS trong production
4. **CORS**: Cấu hình CORS phù hợp với frontend domain
5. **Password Policy**: Có thể thêm validation mạnh hơn cho password

---

## Next Steps (Tính năng có thể mở rộng)

- [ ] Email verification
- [ ] Password reset with OTP
- [ ] Two-factor authentication (2FA)
- [ ] User registration
- [ ] Role-based authorization
- [ ] User management (CRUD)
- [ ] Audit logging
- [ ] Rate limiting
- [ ] Redis cache cho refresh tokens

---

## Liên hệ & Hỗ trợ

Nếu có vấn đề, kiểm tra:
1. SQL Server đã chạy chưa
2. Database IdentityDB đã tạo chưa
3. Connection string đúng chưa
4. Đã restore packages chưa (`dotnet restore`)

---
1.....
**Happy Coding! 🚀**
