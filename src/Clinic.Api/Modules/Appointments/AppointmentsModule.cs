using System.ComponentModel.DataAnnotations;
using Clinic.Api.Shared.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Clinic.Api.Modules.Appointments;

public static class AppointmentsModule
{
    public static IServiceCollection AddAppointmentsModule(this IServiceCollection services)
    {
        services.AddScoped<AppointmentsService>();
        return services;
    }
}

public sealed class Appointment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ClientId { get; set; }
    public Guid? AssignedUserId { get; set; }
    public DateTimeOffset ScheduledAt { get; set; }
    public string Reason { get; set; } = string.Empty;
    public AppointmentStatus Status { get; set; } = AppointmentStatus.Scheduled;
    public DateTimeOffset CreatedUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? UpdatedUtc { get; set; }
}

public enum AppointmentStatus { Scheduled = 1, Confirmed = 2, Cancelled = 3, Completed = 4, NoShow = 5 }

public sealed record AppointmentResponse(Guid Id, Guid ClientId, Guid? AssignedUserId, DateTimeOffset ScheduledAt, string Reason, AppointmentStatus Status);
public sealed record CreateAppointmentRequest([Required] Guid ClientId, Guid? AssignedUserId, [Required] DateTimeOffset ScheduledAt, [Required] string Reason);
public sealed record UpdateAppointmentRequest(Guid? AssignedUserId, [Required] DateTimeOffset ScheduledAt, [Required] string Reason);

public sealed class AppointmentsService(ClinicDbContext db)
{
    public async Task<IReadOnlyList<AppointmentResponse>> ListAsync(DateTimeOffset? from, DateTimeOffset? to, AppointmentStatus? status, CancellationToken ct)
    {
        var query = db.Appointments.AsNoTracking();
        if (from.HasValue) query = query.Where(appointment => appointment.ScheduledAt >= from.Value);
        if (to.HasValue) query = query.Where(appointment => appointment.ScheduledAt <= to.Value);
        if (status.HasValue) query = query.Where(appointment => appointment.Status == status.Value);
        return await query.OrderBy(appointment => appointment.ScheduledAt).Select(appointment => Map(appointment)).ToListAsync(ct);
    }

    public async Task<AppointmentResponse> CreateAsync(CreateAppointmentRequest request, CancellationToken ct)
    {
        var appointment = new Appointment { ClientId = request.ClientId, AssignedUserId = request.AssignedUserId, ScheduledAt = request.ScheduledAt, Reason = request.Reason.Trim() };
        db.Appointments.Add(appointment);
        await db.SaveChangesAsync(ct);
        return Map(appointment);
    }

    public async Task<AppointmentResponse?> UpdateStatusAsync(Guid id, AppointmentStatus status, CancellationToken ct)
    {
        var appointment = await db.Appointments.FirstOrDefaultAsync(appointment => appointment.Id == id, ct);
        if (appointment is null) return null;
        appointment.Status = status;
        appointment.UpdatedUtc = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        return Map(appointment);
    }

    private static AppointmentResponse Map(Appointment appointment) => new(appointment.Id, appointment.ClientId, appointment.AssignedUserId, appointment.ScheduledAt, appointment.Reason, appointment.Status);
}

[ApiController]
[Route("api/appointments")]
public sealed class AppointmentsController(AppointmentsService appointments) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<AppointmentResponse>>> List([FromQuery] DateTimeOffset? from, [FromQuery] DateTimeOffset? to, [FromQuery] AppointmentStatus? status, CancellationToken ct) =>
        Ok(await appointments.ListAsync(from, to, status, ct));

    [HttpPost]
    public async Task<ActionResult<AppointmentResponse>> Create(CreateAppointmentRequest request, CancellationToken ct) =>
        Ok(await appointments.CreateAsync(request, ct));

    [HttpPatch("{id:guid}/confirm")]
    public async Task<ActionResult<AppointmentResponse>> Confirm(Guid id, CancellationToken ct) =>
        (await appointments.UpdateStatusAsync(id, AppointmentStatus.Confirmed, ct)) is { } appointment ? Ok(appointment) : NotFound();

    [HttpPatch("{id:guid}/cancel")]
    public async Task<ActionResult<AppointmentResponse>> Cancel(Guid id, CancellationToken ct) =>
        (await appointments.UpdateStatusAsync(id, AppointmentStatus.Cancelled, ct)) is { } appointment ? Ok(appointment) : NotFound();
}
