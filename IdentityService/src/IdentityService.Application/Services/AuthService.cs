using IdentityService.Application.DTOs;
using IdentityService.Application.Interfaces;
using IdentityService.Domain.Entities;
using IdentityService.Domain.Repositories;

namespace IdentityService.Application.Services;

public class AuthService : IAuthService
{
    private readonly IUserRepository _userRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtService _jwtService;
    private readonly IUserLoginLogRepository _loginLogRepository;

    public AuthService(
        IUserRepository userRepository,
        IPasswordHasher passwordHasher,
        IJwtService jwtService,
        IUserLoginLogRepository loginLogRepository)
    {
        _userRepository = userRepository;
        _passwordHasher = passwordHasher;
        _jwtService = jwtService;
        _loginLogRepository = loginLogRepository;
    }

    public async Task<RegisterResponseDto> RegisterAsync(RegisterRequestDto request)
    {
        // Check if email already exists
        if (await _userRepository.ExistsAsync(request.Email))
        {
            throw new InvalidOperationException("Email already exists");
        }

        // Check if passwords match (additional server-side validation)
        if (request.Password != request.ConfirmPassword)
        {
            throw new InvalidOperationException("Passwords do not match");
        }

        // Create new user
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = request.Email,
            FullName = request.FullName,
            Phone = request.Phone,
            PasswordHash = _passwordHasher.HashPassword(request.Password),
            RoleId = 5, // Default role: Customer
            Status = "ACTIVE",
            EmailVerified = false,
            OtpAttempts = 0
        };

        await _userRepository.CreateAsync(user);

        return new RegisterResponseDto
        {
            UserId = user.Id,
            Email = user.Email,
            FullName = user.FullName ?? string.Empty,
            Message = "Registration successful. Please verify your email."
        };
    }

    public async Task<LoginResponseDto> LoginAsync(LoginRequestDto request, string? ipAddress = null)
    {
        // Find user by email
        var user = await _userRepository.GetByEmailAsync(request.Email);
        
        if (user == null)
        {
            // Log failed login attempt - user not found
            await LogLoginAttemptAsync(Guid.Empty, "FAILED", "User not found", ipAddress, null);
            throw new UnauthorizedAccessException("Invalid email or password");
        }

        // Verify password
        if (!_passwordHasher.VerifyPassword(request.Password, user.PasswordHash))
        {
            // Log failed login attempt - invalid password
            await LogLoginAttemptAsync(user.Id, "FAILED", "Invalid password", ipAddress, null);
            throw new UnauthorizedAccessException("Invalid email or password");
        }

        // Check user status
        if (user.Status != "ACTIVE")
        {
            // Log failed login attempt - account not active
            await LogLoginAttemptAsync(user.Id, "BLOCKED", $"Account is {user.Status}", ipAddress, null);
            throw new UnauthorizedAccessException($"Account is {user.Status.ToLower()}");
        }

        // Generate tokens
        var accessToken = _jwtService.GenerateAccessToken(user);
        var refreshToken = _jwtService.GenerateRefreshToken();
        var refreshTokenExpiry = _jwtService.GetRefreshTokenExpiryTime();

        // Update user with refresh token
        user.RefreshToken = refreshToken;
        user.RefreshTokenExpiresAt = refreshTokenExpiry;

        await _userRepository.UpdateAsync(user);

        // Log successful login
        await LogLoginAttemptAsync(user.Id, "SUCCESS", null, ipAddress, null);

        // Map to response DTO
        return new LoginResponseDto
        {
            AccessToken = accessToken,
            Email = user.Email,
            FullName = user.FullName,
            RoleId = user.RoleId
        };
    }

    public async Task LogoutAsync(string refreshToken)
    {
        if (string.IsNullOrEmpty(refreshToken))
        {
            throw new ArgumentException("Refresh token is required");
        }

        var user = await _userRepository.GetByRefreshTokenAsync(refreshToken);
        if (user == null)
        {
            throw new UnauthorizedAccessException("Invalid refresh token");
        }

        // Clear refresh token
        user.RefreshToken = null;
        user.RefreshTokenExpiresAt = null;

        await _userRepository.UpdateAsync(user);
    }

    public async Task<LoginResponseDto> RefreshTokenAsync(string refreshToken)
    {
        if (string.IsNullOrEmpty(refreshToken))
        {
            throw new ArgumentException("Refresh token is required");
        }

        var user = await _userRepository.GetByRefreshTokenAsync(refreshToken);
        if (user == null)
        {
            throw new UnauthorizedAccessException("Invalid refresh token");
        }

        // Check if refresh token is expired
        if (user.RefreshTokenExpiresAt == null || user.RefreshTokenExpiresAt < DateTime.UtcNow)
        {
            throw new UnauthorizedAccessException("Refresh token has expired");
        }

        // Check user status
        if (user.Status != "ACTIVE")
        {
            throw new UnauthorizedAccessException($"Account is {user.Status.ToLower()}");
        }

        // Generate new tokens
        var newAccessToken = _jwtService.GenerateAccessToken(user);
        var newRefreshToken = _jwtService.GenerateRefreshToken();
        var newRefreshTokenExpiry = _jwtService.GetRefreshTokenExpiryTime();

        // Update user with new refresh token
        user.RefreshToken = newRefreshToken;
        user.RefreshTokenExpiresAt = newRefreshTokenExpiry;

        await _userRepository.UpdateAsync(user);

        return new LoginResponseDto
        {
            AccessToken = newAccessToken,
            Email = user.Email,
            FullName = user.FullName,
            RoleId = user.RoleId
        };
    }

    public async Task<UserDto> UpdateUserAsync(Guid id, UpdateUserRequestDto request)
    {
        var user = await _userRepository.GetByIdAsync(id);
        if (user == null)
        {
            throw new InvalidOperationException("User not found");
        }

        if (request.Email != null)
        {
            if (string.IsNullOrWhiteSpace(request.Email))
            {
                throw new InvalidOperationException("Email cannot be empty");
            }

            var normalizedEmail = request.Email.Trim();
            var existingUser = await _userRepository.GetByEmailAsync(normalizedEmail);
            if (existingUser != null && existingUser.Id != user.Id)
            {
                throw new InvalidOperationException("Email already exists");
            }

            user.Email = normalizedEmail;
        }

        // Update fields if provided
        if (!string.IsNullOrEmpty(request.FullName))
        {
            user.FullName = request.FullName;
        }

        if (!string.IsNullOrEmpty(request.Phone))
        {
            user.Phone = request.Phone;
        }

        if (request.Password != null)
        {
            if (string.IsNullOrWhiteSpace(request.Password))
            {
                throw new InvalidOperationException("Password cannot be empty");
            }

            user.PasswordHash = _passwordHasher.HashPassword(request.Password);
        }

        await _userRepository.UpdateAsync(user);
        return MapToUserDto(user);
    }

    private UserDto MapToUserDto(User user)
    {
        return new UserDto
        {
            Id = user.Id,
            Email = user.Email,
            FullName = user.FullName,
            Phone = user.Phone,
            Status = user.Status,
            EmailVerified = user.EmailVerified,
            Role = new RoleDto
            {
                Id = user.Role.Id,
                Name = user.Role.Name,
                Description = user.Role.Description
            }
        };
    }

    private async Task LogLoginAttemptAsync(Guid userId, string status, string? failureReason, string? ipAddress, string? userAgent)
    {
        try
        {
            var loginLog = new UserLoginLog
            {
                UserId = userId,
                LoginAt = DateTime.UtcNow,
                IpAddress = ipAddress,
                UserAgent = userAgent,
                Status = status,
                FailureReason = failureReason
            };

            await _loginLogRepository.CreateAsync(loginLog);
        }
        catch
        {
            // Logging should not break the login flow
            // Log the error silently
        }
    }
}
