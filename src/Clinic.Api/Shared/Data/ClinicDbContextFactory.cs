using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Clinic.Api.Shared.Data;

public sealed class ClinicDbContextFactory : IDesignTimeDbContextFactory<ClinicDbContext>
{
    public ClinicDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<ClinicDbContext>()
            .UseSqlServer("Server=(localdb)\\MSSQLLocalDB;Database=ClinicDb;Trusted_Connection=True;TrustServerCertificate=True")
            .Options;

        return new ClinicDbContext(options);
    }
}
