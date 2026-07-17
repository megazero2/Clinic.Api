using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Clinic.Api.Shared.Common;
using Clinic.Api.Shared.Data;
using Clinic.Api.Shared.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Clinic.Api.Modules.Identity;

public static class IdentityModule
{
    public static IServiceCollection AddIdentityModule(this IServiceCollection services)
    {
        services.AddScoped<AuthService>();
        services.AddScoped<UserService>();
        services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();
        return services;
    }
}

public sealed class User
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Email { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public UserRole Role { get; set; } = UserRole.Client;
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? UpdatedUtc { get; set; }
    public List<RefreshToken> RefreshTokens { get; set; } = [];
}

public sealed class RefreshToken
{
    public RefreshToken()
    {
    }

    public RefreshToken(Guid userId, string token, DateTimeOffset expiresUtc)
    {
        UserId = userId;
        Token = token;
        ExpiresUtc = expiresUtc;
    }

    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public string Token { get; set; } = string.Empty;
    public DateTimeOffset ExpiresUtc { get; set; }
    public DateTimeOffset CreatedUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? RevokedUtc { get; set; }
    public User User { get; set; } = null!;
    public bool IsActive => RevokedUtc is null && ExpiresUtc > DateTimeOffset.UtcNow;
}

public enum UserRole { Admin = 1, Staff = 2, Client = 3 }

public sealed record RegisterRequest([Required, EmailAddress] string Email, [Required, MinLength(8)] string Password, [Required] string FirstName, [Required] string LastName);
public sealed record LoginRequest([Required, EmailAddress] string Email, [Required] string Password);
public sealed record RefreshTokenRequest([Required] string RefreshToken);
public sealed record UserResponse(Guid Id, string Email, string FirstName, string LastName, string Role, bool IsActive);
public sealed record AuthResponse(string AccessToken, string RefreshToken, DateTimeOffset ExpiresUtc, UserResponse User);
public sealed record UpdateUserRequest([Required] string FirstName, [Required] string LastName, [Required] UserRole Role);

public sealed class AuthService(ClinicDbContext db, IPasswordHasher<User> hasher, JwtTokenService tokens)
{
    public async Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken ct)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        if (await db.Users.AnyAsync(user => user.Email == email, ct))
        {
            throw new InvalidOperationException("A user with the same email already exists.");
        }

        var role = await db.Users.AnyAsync(ct) ? UserRole.Client : UserRole.Admin;
        var user = new User { Email = email, FirstName = request.FirstName.Trim(), LastName = request.LastName.Trim(), Role = role };
        user.PasswordHash = hasher.HashPassword(user, request.Password);
        db.Users.Add(user);
        return await IssueAsync(user, ct);
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken ct)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var user = await db.Users.Include(user => user.RefreshTokens).FirstOrDefaultAsync(user => user.Email == email, ct);
        if (user is null || !user.IsActive || hasher.VerifyHashedPassword(user, user.PasswordHash, request.Password) == PasswordVerificationResult.Failed)
        {
            throw new UnauthorizedAccessException("Invalid credentials.");
        }

        return await IssueAsync(user, ct);
    }

    public async Task<UserResponse?> MeAsync(ClaimsPrincipal principal, CancellationToken ct)
    {
        var id = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(id, out var userId)
            ? await db.Users.Where(user => user.Id == userId).Select(user => Map(user)).FirstOrDefaultAsync(ct)
            : null;
    }

    private async Task<AuthResponse> IssueAsync(User user, CancellationToken ct)
    {
        var access = tokens.CreateAccessToken(user);
        var refresh = tokens.CreateRefreshToken(user.Id);
        user.RefreshTokens.Add(refresh);
        await db.SaveChangesAsync(ct);
        return new AuthResponse(access.Token, refresh.Token, access.ExpiresUtc, Map(user));
    }

    public static UserResponse Map(User user) => new(user.Id, user.Email, user.FirstName, user.LastName, user.Role.ToString(), user.IsActive);
}

public sealed class UserService(ClinicDbContext db)
{
    public async Task<PagedResponse<UserResponse>> ListAsync(int page, int pageSize, string? search, CancellationToken ct)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var query = db.Users.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(user => user.Email.Contains(search) || user.FirstName.Contains(search) || user.LastName.Contains(search));
        }

        var total = await query.CountAsync(ct);
        var items = await query.OrderBy(user => user.LastName).Skip((page - 1) * pageSize).Take(pageSize).Select(user => AuthService.Map(user)).ToListAsync(ct);
        return new PagedResponse<UserResponse>(items, page, pageSize, total);
    }
}

[ApiController]
[Route("api/auth")]
public sealed class AuthController(AuthService auth) : ControllerBase
{
    [HttpPost("register")]
    public async Task<ActionResult<AuthResponse>> Register(RegisterRequest request, CancellationToken ct)
    {
        try { return Created("", await auth.RegisterAsync(request, ct)); }
        catch (InvalidOperationException ex) { return Conflict(new ProblemDetails { Title = "Registration conflict", Detail = ex.Message }); }
    }

    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest request, CancellationToken ct)
    {
        try { return Ok(await auth.LoginAsync(request, ct)); }
        catch (UnauthorizedAccessException ex) { return Unauthorized(new ProblemDetails { Title = "Unauthorized", Detail = ex.Message }); }
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<ActionResult<UserResponse>> Me(CancellationToken ct) => (await auth.MeAsync(User, ct)) is { } user ? Ok(user) : Unauthorized();
}

[ApiController]
[Route("api/users")]
[Authorize(Roles = "Admin")]
public sealed class UsersController(UserService users) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PagedResponse<UserResponse>>> List([FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] string? search = null, CancellationToken ct = default) =>
        Ok(await users.ListAsync(page, pageSize, search, ct));
}

[ApiController]
[Route("api/roles")]
public sealed class RolesController : ControllerBase
{
    [HttpGet]
    public ActionResult<IReadOnlyList<string>> List() => Ok(Enum.GetNames<UserRole>());
}
