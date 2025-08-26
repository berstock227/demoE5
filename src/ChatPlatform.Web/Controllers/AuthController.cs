using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using System.ComponentModel.DataAnnotations;
using ChatPlatform.Core.Interfaces;

namespace ChatPlatform.Web.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<AuthController> _logger;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IIdGenerator _idGenerator;

    public AuthController(
        IConfiguration configuration, 
        ILogger<AuthController> logger,
        IDateTimeProvider dateTimeProvider,
        IIdGenerator idGenerator)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _dateTimeProvider = dateTimeProvider ?? throw new ArgumentNullException(nameof(dateTimeProvider));
        _idGenerator = idGenerator ?? throw new ArgumentNullException(nameof(idGenerator));
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        try
        {
            if (request == null)
            {
                return BadRequest(new { Error = "Request body is required" });
            }

            if (!ModelState.IsValid)
            {
                return BadRequest(new { Error = "Invalid request data", Details = ModelState });
            }

            // In a real application, you would validate against a database
            // For demo purposes, we'll accept any valid email/password combination
            if (string.IsNullOrEmpty(request.Email) || string.IsNullOrEmpty(request.Password))
            {
                return BadRequest(new { Error = "Email and password are required" });
            }

            // Generate a demo user ID and tenant ID
            var userId = _idGenerator.GenerateId("user");
            var tenantId = "demo-tenant";

            var token = GenerateJwtToken(userId, request.Email, tenantId);

            _logger.LogInformation("User {Email} logged in successfully", request.Email);

            return Ok(new
            {
                AccessToken = token,
                TokenType = "Bearer",
                ExpiresIn = 3600,
                UserId = userId,
                Email = request.Email,
                TenantId = tenantId
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during login for email {Email}", request?.Email);
            return StatusCode(500, new { Error = "Internal server error" });
        }
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        try
        {
            if (request == null)
            {
                return BadRequest(new { Error = "Request body is required" });
            }

            if (!ModelState.IsValid)
            {
                return BadRequest(new { Error = "Invalid request data", Details = ModelState });
            }

            if (string.IsNullOrEmpty(request.Email) || string.IsNullOrEmpty(request.Password))
            {
                return BadRequest(new { Error = "Email and password are required" });
            }

            if (request.Password.Length < 6)
            {
                return BadRequest(new { Error = "Password must be at least 6 characters long" });
            }

            // In a real application, you would save to database
            // For demo purposes, we'll just return success
            var userId = _idGenerator.GenerateId("user");
            var tenantId = "demo-tenant";

            var token = GenerateJwtToken(userId, request.Email, tenantId);

            _logger.LogInformation("User {Email} registered successfully", request.Email);

            return Ok(new
            {
                AccessToken = token,
                TokenType = "Bearer",
                ExpiresIn = 3600,
                UserId = userId,
                Email = request.Email,
                TenantId = tenantId
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during registration for email {Email}", request?.Email);
            return StatusCode(500, new { Error = "Internal server error" });
        }
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<IActionResult> GetCurrentUser()
    {
        try
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var email = User.FindFirst(ClaimTypes.Email)?.Value;
            var tenantId = User.FindFirst("tenant_id")?.Value;

            if (string.IsNullOrEmpty(userId))
            {
                _logger.LogWarning("Unauthorized access attempt - missing user ID");
                return Unauthorized();
            }

            _logger.LogDebug("Retrieved current user: {UserId}", userId);

            return Ok(new
            {
                UserId = userId,
                Email = email,
                TenantId = tenantId
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting current user");
            return StatusCode(500, new { Error = "Internal server error" });
        }
    }

    [HttpPost("refresh")]
    [Authorize]
    public async Task<IActionResult> RefreshToken()
    {
        try
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var email = User.FindFirst(ClaimTypes.Email)?.Value;
            var tenantId = User.FindFirst("tenant_id")?.Value;

            if (string.IsNullOrEmpty(userId))
            {
                _logger.LogWarning("Token refresh failed - missing user ID");
                return Unauthorized();
            }

            var token = GenerateJwtToken(userId, email, tenantId);

            _logger.LogDebug("Token refreshed for user: {UserId}", userId);

            return Ok(new
            {
                AccessToken = token,
                TokenType = "Bearer",
                ExpiresIn = 3600
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error refreshing token");
            return StatusCode(500, new { Error = "Internal server error" });
        }
    }

    private string GenerateJwtToken(string userId, string email, string tenantId)
    {
        if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(email) || string.IsNullOrEmpty(tenantId))
        {
            throw new ArgumentException("User ID, email, and tenant ID are required for token generation");
        }

        var jwtSettings = _configuration.GetSection("JwtSettings");
        var key = Encoding.ASCII.GetBytes(jwtSettings["SecretKey"] ?? "your-super-secret-key-with-at-least-32-characters");

        var tokenHandler = new JwtSecurityTokenHandler();
        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, userId),
                new Claim(ClaimTypes.Email, email),
                new Claim("tenant_id", tenantId),
                new Claim(JwtRegisteredClaimNames.Jti, _idGenerator.GenerateId("jti")),
            }),
            Expires = _dateTimeProvider.UtcNow.AddHours(1),
            Issuer = jwtSettings["Issuer"],
            Audience = jwtSettings["Audience"],
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
        };

        var token = tokenHandler.CreateToken(tokenDescriptor);
        return tokenHandler.WriteToken(token);
    }
}

public class LoginRequest
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;
    
    [Required]
    [StringLength(100, MinimumLength = 1)]
    public string Password { get; set; } = string.Empty;
}

public class RegisterRequest
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;
    
    [Required]
    [StringLength(100, MinimumLength = 6)]
    public string Password { get; set; } = string.Empty;
    
    [StringLength(100)]
    public string? Name { get; set; }
}
