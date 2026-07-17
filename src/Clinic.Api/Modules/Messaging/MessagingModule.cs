using System.ComponentModel.DataAnnotations;
using Clinic.Api.Shared.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Clinic.Api.Modules.Messaging;

public static class MessagingModule
{
    public static IServiceCollection AddMessagingModule(this IServiceCollection services)
    {
        services.AddScoped<MessagingService>();
        return services;
    }
}

public sealed class OutboundMessage
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Channel { get; set; } = string.Empty;
    public string To { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string Status { get; set; } = "Queued";
    public DateTimeOffset CreatedUtc { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class MessageTemplate
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string Channel { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
}

public sealed record SendEmailRequest([Required, EmailAddress] string To, [Required] string Subject, [Required] string Body);
public sealed record SendSmsRequest([Required] string To, [Required] string Body);
public sealed record MessageResponse(Guid Id, string Channel, string To, string Subject, string Body, string Status);

public sealed class MessagingService(ClinicDbContext db)
{
    public async Task<MessageResponse> SendEmailAsync(SendEmailRequest request, CancellationToken ct) =>
        await QueueAsync("Email", request.To, request.Subject, request.Body, ct);

    public async Task<MessageResponse> SendSmsAsync(SendSmsRequest request, CancellationToken ct) =>
        await QueueAsync("Sms", request.To, "SMS", request.Body, ct);

    private async Task<MessageResponse> QueueAsync(string channel, string to, string subject, string body, CancellationToken ct)
    {
        var message = new OutboundMessage { Channel = channel, To = to.Trim(), Subject = subject.Trim(), Body = body.Trim() };
        db.Messages.Add(message);
        await db.SaveChangesAsync(ct);
        return new MessageResponse(message.Id, message.Channel, message.To, message.Subject, message.Body, message.Status);
    }

    public async Task<IReadOnlyList<MessageResponse>> ListAsync(CancellationToken ct) =>
        await db.Messages.AsNoTracking().OrderByDescending(message => message.CreatedUtc)
            .Select(message => new MessageResponse(message.Id, message.Channel, message.To, message.Subject, message.Body, message.Status)).ToListAsync(ct);
}

[ApiController]
[Route("api/messages")]
public sealed class MessagesController(MessagingService messaging) : ControllerBase
{
    [HttpPost("email")]
    public async Task<ActionResult<MessageResponse>> SendEmail(SendEmailRequest request, CancellationToken ct) =>
        Ok(await messaging.SendEmailAsync(request, ct));

    [HttpPost("sms")]
    public async Task<ActionResult<MessageResponse>> SendSms(SendSmsRequest request, CancellationToken ct) =>
        Ok(await messaging.SendSmsAsync(request, ct));

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<MessageResponse>>> List(CancellationToken ct) => Ok(await messaging.ListAsync(ct));
}
