using Clinic.Api.Modules.Appointments;
using Clinic.Api.Modules.Clients;
using Clinic.Api.Modules.Identity;
using Clinic.Api.Modules.Messaging;
using Clinic.Api.Modules.Notifications;
using Microsoft.EntityFrameworkCore;

namespace Clinic.Api.Shared.Data;

public sealed class ClinicDbContext(DbContextOptions<ClinicDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<Client> Clients => Set<Client>();
    public DbSet<Appointment> Appointments => Set<Appointment>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<OutboundMessage> Messages => Set<OutboundMessage>();
    public DbSet<MessageTemplate> MessageTemplates => Set<MessageTemplate>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(user => user.Id);
            entity.Property(user => user.Email).HasMaxLength(256).IsRequired();
            entity.HasIndex(user => user.Email).IsUnique();
            entity.Property(user => user.FirstName).HasMaxLength(100).IsRequired();
            entity.Property(user => user.LastName).HasMaxLength(100).IsRequired();
            entity.Property(user => user.PasswordHash).HasMaxLength(500).IsRequired();
            entity.Property(user => user.Role).HasConversion<string>().HasMaxLength(40).IsRequired();
        });

        modelBuilder.Entity<RefreshToken>(entity =>
        {
            entity.HasKey(token => token.Id);
            entity.Property(token => token.Token).HasMaxLength(200).IsRequired();
            entity.HasIndex(token => token.Token).IsUnique();
            entity.HasOne(token => token.User).WithMany(user => user.RefreshTokens).HasForeignKey(token => token.UserId);
        });

        modelBuilder.Entity<Client>(entity =>
        {
            entity.HasKey(client => client.Id);
            entity.Property(client => client.FirstName).HasMaxLength(100).IsRequired();
            entity.Property(client => client.LastName).HasMaxLength(100).IsRequired();
            entity.Property(client => client.Email).HasMaxLength(256).IsRequired();
            entity.HasIndex(client => client.Email).IsUnique();
            entity.Property(client => client.PhoneNumber).HasMaxLength(30);
        });

        modelBuilder.Entity<Appointment>(entity =>
        {
            entity.HasKey(appointment => appointment.Id);
            entity.Property(appointment => appointment.Reason).HasMaxLength(500).IsRequired();
            entity.Property(appointment => appointment.Status).HasConversion<string>().HasMaxLength(40).IsRequired();
            entity.HasIndex(appointment => appointment.ClientId);
            entity.HasIndex(appointment => appointment.ScheduledAt);
        });

        modelBuilder.Entity<Notification>(entity =>
        {
            entity.HasKey(notification => notification.Id);
            entity.Property(notification => notification.Title).HasMaxLength(120).IsRequired();
            entity.Property(notification => notification.Body).HasMaxLength(500).IsRequired();
            entity.Property(notification => notification.Channel).HasMaxLength(40).IsRequired();
        });

        modelBuilder.Entity<OutboundMessage>(entity =>
        {
            entity.HasKey(message => message.Id);
            entity.Property(message => message.Channel).HasMaxLength(40).IsRequired();
            entity.Property(message => message.To).HasMaxLength(256).IsRequired();
            entity.Property(message => message.Subject).HasMaxLength(150).IsRequired();
            entity.Property(message => message.Body).HasMaxLength(4000).IsRequired();
            entity.Property(message => message.Status).HasMaxLength(40).IsRequired();
        });

        modelBuilder.Entity<MessageTemplate>(entity =>
        {
            entity.HasKey(template => template.Id);
            entity.Property(template => template.Name).HasMaxLength(80).IsRequired();
            entity.Property(template => template.Channel).HasMaxLength(40).IsRequired();
            entity.Property(template => template.Body).HasMaxLength(4000).IsRequired();
        });
    }
}
