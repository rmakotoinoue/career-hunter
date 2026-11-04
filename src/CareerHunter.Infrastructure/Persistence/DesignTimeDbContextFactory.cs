using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace CareerHunter.Infrastructure.Persistence;

/// <summary>
/// Used by <c>dotnet ef</c>. Reads the same environment variable as the hosts so migrations are
/// generated against the real provider without hard-coding credentials in source control.
/// </summary>
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<CareerHunterDbContext>
{
    public CareerHunterDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__CareerHunter")
                               ?? "Host=localhost;Port=5432;Database=careerhunter;Username=careerhunter;Password=local-only";

        var options = new DbContextOptionsBuilder<CareerHunterDbContext>()
            .UseNpgsql(connectionString, npgsql =>
                npgsql.MigrationsAssembly(typeof(CareerHunterDbContext).Assembly.FullName))
            .Options;

        return new CareerHunterDbContext(options);
    }
}
