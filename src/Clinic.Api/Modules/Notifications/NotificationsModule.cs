using System.ComponentModel.DataAnnotations;
using Clinic.Api.Shared.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Clinic.Api.Modules.Notifications;

public static class NotificationsModule
{
    public static IServiceCollection AddNotificationsModule(this IServiceCollection services)
    {
        services.AddScoped<NotificationsService>();
        return services;
    }
}

public sealed class Notification
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public string Channel { get; set; } = "Push";
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public bool IsRead { get; set; }
    public DateTimeOffset CreatedUtc { get; set; } = DateTimeOffset.UtcNow;
}

public sealed record SendPushNotificationRequest([Required] Guid UserId, [Required] string Title, [Required] string Body);
public sealed record NotificationResponse(Guid Id, Guid UserId, string Channel, string Title, string Body, bool IsRead, DateTimeOffset CreatedUtc);

public sealed class NotificationsService(ClinicDbContext db)
{
    public async Task<NotificationResponse> SendPushAsync(SendPushNotificationRequest request, CancellationToken ct)
    {
        var notification = new Notification { UserId = request.UserId, Title = request.Title.Trim(), Body = request.Body.Trim() };
        db.Notifications.Add(notification);
        await db.SaveChangesAsync(ct);
        return Map(notification);
    }

    public async Task<IReadOnlyList<NotificationResponse>> ListAsync(Guid? userId, CancellationToken ct)
    {
        var query = db.Notifications.AsNoTracking();
        if (userId.HasValue) query = query.Where(notification => notification.UserId == userId.Value);
        return await query.OrderByDescending(notification => notification.CreatedUtc).Select(notification => Map(notification)).ToListAsync(ct);
    }

    private static NotificationResponse Map(Notification notification) => new(notification.Id, notification.UserId, notification.Channel, notification.Title, notification.Body, notification.IsRead, notification.CreatedUtc);
}

[ApiController]
[Route("api/notifications")]
public sealed class NotificationsController(NotificationsService notifications) : ControllerBase
{
    [HttpPost("send-push")]
    public async Task<ActionResult<NotificationResponse>> SendPush(SendPushNotificationRequest request, CancellationToken ct) =>
        Ok(await notifications.SendPushAsync(request, ct));

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<NotificationResponse>>> List([FromQuery] Guid? userId, CancellationToken ct) =>
        Ok(await notifications.ListAsync(userId, ct));
}
