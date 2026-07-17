using System.ComponentModel.DataAnnotations;
using Clinic.Api.Shared.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Clinic.Api.Modules.Clients;

public static class ClientsModule
{
    public static IServiceCollection AddClientsModule(this IServiceCollection services)
    {
        services.AddScoped<ClientsService>();
        return services;
    }
}

public sealed class Client
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public DateTimeOffset CreatedUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? UpdatedUtc { get; set; }
}

public sealed record ClientResponse(Guid Id, string FirstName, string LastName, string Email, string? PhoneNumber, DateTimeOffset CreatedUtc);
public sealed record CreateClientRequest([Required] string FirstName, [Required] string LastName, [Required, EmailAddress] string Email, string? PhoneNumber);
public sealed record UpdateClientRequest([Required] string FirstName, [Required] string LastName, [Required, EmailAddress] string Email, string? PhoneNumber);

public sealed class ClientsService(ClinicDbContext db)
{
    public async Task<IReadOnlyList<ClientResponse>> ListAsync(CancellationToken ct) =>
        await db.Clients.AsNoTracking().OrderBy(client => client.LastName).Select(client => Map(client)).ToListAsync(ct);

    public async Task<ClientResponse?> GetAsync(Guid id, CancellationToken ct) =>
        await db.Clients.AsNoTracking().Where(client => client.Id == id).Select(client => Map(client)).FirstOrDefaultAsync(ct);

    public async Task<ClientResponse> CreateAsync(CreateClientRequest request, CancellationToken ct)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        if (await db.Clients.AnyAsync(client => client.Email == email, ct))
        {
            throw new InvalidOperationException("A client with the same email already exists.");
        }

        var client = new Client { FirstName = request.FirstName.Trim(), LastName = request.LastName.Trim(), Email = email, PhoneNumber = request.PhoneNumber?.Trim() };
        db.Clients.Add(client);
        await db.SaveChangesAsync(ct);
        return Map(client);
    }

    public async Task<ClientResponse?> UpdateAsync(Guid id, UpdateClientRequest request, CancellationToken ct)
    {
        var client = await db.Clients.FirstOrDefaultAsync(client => client.Id == id, ct);
        if (client is null) return null;
        client.FirstName = request.FirstName.Trim();
        client.LastName = request.LastName.Trim();
        client.Email = request.Email.Trim().ToLowerInvariant();
        client.PhoneNumber = request.PhoneNumber?.Trim();
        client.UpdatedUtc = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        return Map(client);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken ct)
    {
        var client = await db.Clients.FirstOrDefaultAsync(client => client.Id == id, ct);
        if (client is null) return false;
        db.Clients.Remove(client);
        await db.SaveChangesAsync(ct);
        return true;
    }

    private static ClientResponse Map(Client client) => new(client.Id, client.FirstName, client.LastName, client.Email, client.PhoneNumber, client.CreatedUtc);
}

[ApiController]
[Route("api/clients")]
public sealed class ClientsController(ClientsService clients) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ClientResponse>>> List(CancellationToken ct) => Ok(await clients.ListAsync(ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ClientResponse>> Get(Guid id, CancellationToken ct) => (await clients.GetAsync(id, ct)) is { } client ? Ok(client) : NotFound();

    [HttpPost]
    public async Task<ActionResult<ClientResponse>> Create(CreateClientRequest request, CancellationToken ct)
    {
        try
        {
            var client = await clients.CreateAsync(request, ct);
            return CreatedAtAction(nameof(Get), new { id = client.Id }, client);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new ProblemDetails { Title = "Client conflict", Detail = ex.Message });
        }
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ClientResponse>> Update(Guid id, UpdateClientRequest request, CancellationToken ct) =>
        (await clients.UpdateAsync(id, request, ct)) is { } client ? Ok(client) : NotFound();

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct) => await clients.DeleteAsync(id, ct) ? NoContent() : NotFound();
}
