using ChatPlatform.Core.Interfaces;
using ChatPlatform.Core.Common;
using ChatPlatform.Services.Implementations;
using ChatPlatform.SignalR.Hubs;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Add SignalR
builder.Services.AddSignalR(options =>
{
    options.EnableDetailedErrors = builder.Environment.IsDevelopment();
    options.MaximumReceiveMessageSize = 1024 * 1024; // 1MB
    options.ClientTimeoutInterval = TimeSpan.FromSeconds(30);
    options.KeepAliveInterval = TimeSpan.FromSeconds(15);
});

// Add CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// Add Redis
var redisConnectionString = builder.Configuration.GetConnectionString("Redis") ?? "localhost:6379";
// Add Redis Connection
builder.Services.AddSingleton<IConnectionMultiplexer>(provider =>
{
    try
    {
        var configuration = ConfigurationOptions.Parse(redisConnectionString);
        configuration.ConnectRetry = 3;
        configuration.ReconnectRetryPolicy = new ExponentialRetry(5);
        configuration.ConnectTimeout = 5000;
        configuration.SyncTimeout = 5000;
        
        var connection = ConnectionMultiplexer.Connect(configuration);
        
        // Test connection
        var db = connection.GetDatabase();
        db.Ping();
        
        return connection;
    }
    catch (Exception ex)
    {
        var logger = provider.GetService<ILogger<Program>>();
        logger?.LogError(ex, "Failed to connect to Redis at {ConnectionString}", redisConnectionString);
        throw;
    }
});

// Add services with proper lifetime management
builder.Services.AddSingleton<IRedisService, RedisService>();
builder.Services.AddSingleton<IRateLimiter, RateLimiterService>();
builder.Services.AddSingleton<IConnectionManager, ConnectionManagerService>();
builder.Services.AddSingleton<IChatService, ChatService>();
builder.Services.AddSingleton<ChatPlatform.Core.Interfaces.IIdGenerator, IdGeneratorService>();
builder.Services.AddSingleton<ChatPlatform.Core.Interfaces.IDateTimeProvider, DateTimeProviderService>();



// Add JWT Authentication
var jwtSettings = builder.Configuration.GetSection("JwtSettings");
var key = Encoding.ASCII.GetBytes(jwtSettings["SecretKey"] ?? "your-super-secret-key-with-at-least-32-characters");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(key),
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero
        };

        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                var path = context.HttpContext.Request.Path;
                
                // For SignalR, try to get token from query parameter first
                if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/chathub"))
                {
                    context.Token = accessToken;
                    return Task.CompletedTask;
                }
                
                // For other requests, try to get token from Authorization header
                var authHeader = context.Request.Headers["Authorization"].FirstOrDefault();
                if (!string.IsNullOrEmpty(authHeader) && authHeader.StartsWith("Bearer "))
                {
                    context.Token = authHeader.Substring("Bearer ".Length);
                }
                
                return Task.CompletedTask;
            },
            OnAuthenticationFailed = context =>
            {
                Console.WriteLine($"JWT Authentication failed: {context.Exception.Message}");
                return Task.CompletedTask;
            }
        };
    });

// Add Authorization
builder.Services.AddAuthorization();

// Add Health Checks
builder.Services.AddHealthChecks();

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}


app.UseCors("AllowAll");
app.UseStaticFiles(); // Add this line to serve static files
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHub<ChatHub>("/chathub");
app.MapHealthChecks("/health");

// Map root path to index.html
app.MapGet("/", async context =>
{
    context.Response.Redirect("/index.html");
});

// Start background services
var connectionManager = app.Services.GetRequiredService<IConnectionManager>();
var rateLimiter = app.Services.GetRequiredService<IRateLimiter>();

// Start cleanup task
_ = Task.Run(async () =>
{
    while (true)
    {
        try
        {
            await connectionManager.CleanupInactiveConnectionsAsync();
            await Task.Delay(TimeSpan.FromMinutes(5));
        }
        catch (Exception ex)
        {
            var logger = app.Services.GetRequiredService<ILogger<Program>>();
            logger.LogError(ex, "Error during connection cleanup");
        }
    }
});

app.Run();
