using Clinic.Api.Modules.AiAssistant;
using Clinic.Api.Modules.Appointments;
using Clinic.Api.Modules.Clients;
using Clinic.Api.Modules.Identity;
using Clinic.Api.Modules.Messaging;
using Clinic.Api.Modules.Notifications;
using Clinic.Api.Shared.Data;
using Clinic.Api.Shared.Security;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo { Title = "Clinic API", Version = "v1" });
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Paste only the JWT access token returned by /api/auth/login."
    });
    options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecuritySchemeReference("Bearer", document, "Bearer"),
            []
        }
    });
});
builder.Services.AddHealthChecks();

builder.Services.AddOptions<JwtOptions>()
    .Bind(builder.Configuration.GetRequiredSection(JwtOptions.SectionName))
    .Validate(options => !string.IsNullOrWhiteSpace(options.Issuer), "JWT issuer is required.")
    .Validate(options => !string.IsNullOrWhiteSpace(options.Audience), "JWT audience is required.")
    .Validate(options => !string.IsNullOrWhiteSpace(options.SigningKey), "JWT signing key is required.")
    .ValidateOnStart();

if (builder.Configuration.GetValue<string>("Database:Provider")?.Equals("InMemory", StringComparison.OrdinalIgnoreCase) is true)
{
    builder.Services.AddDbContext<ClinicDbContext>(options => options.UseInMemoryDatabase("ClinicDb"));
}
else
{
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
    if (string.IsNullOrWhiteSpace(connectionString))
    {
        throw new InvalidOperationException("Connection string 'DefaultConnection' is required.");
    }

    builder.Services.AddDbContext<ClinicDbContext>(options =>
        options.UseSqlServer(connectionString, sql => sql.EnableRetryOnFailure()));
}

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        var jwtOptions = builder.Configuration.GetRequiredSection(JwtOptions.SectionName).Get<JwtOptions>()
            ?? throw new InvalidOperationException("JWT configuration is required.");

        options.TokenValidationParameters = JwtTokenService.CreateValidationParameters(jwtOptions);
    });

builder.Services.AddAuthorization();
builder.Services.AddSingleton<JwtTokenService>();
builder.Services.AddIdentityModule();
builder.Services.AddClientsModule();
builder.Services.AddAppointmentsModule();
builder.Services.AddNotificationsModule();
builder.Services.AddMessagingModule();
builder.Services.AddAiAssistantModule();
builder.Services.AddControllers();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "Clinic API v1");
    });
}

app.UseExceptionHandler();
app.UseAuthentication();
app.UseAuthorization();
app.MapHealthChecks("/health");
app.MapControllers();

app.Run();
