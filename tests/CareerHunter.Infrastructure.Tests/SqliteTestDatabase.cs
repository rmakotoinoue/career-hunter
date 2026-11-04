using CareerHunter.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace CareerHunter.Infrastructure.Tests;

/// <summary>
/// Test-only database host. Production targets PostgreSQL, where <see cref="DateTimeOffset"/>
/// is a first-class, orderable type. SQLite has no comparison support for it, so the tests map
/// the value to UTC ticks. This keeps the ordering semantics identical without changing the
/// production model or the domain.
/// </summary>
internal sealed class SqliteTestDatabase : IAsyncDisposable
{
    private readonly SqliteConnection _connection;

    private SqliteTestDatabase(SqliteConnection connection, CareerHunterDbContext context)
    {
        _connection = connection;
        Context = context;
    }

    public CareerHunterDbContext Context { get; }

    public static async Task<SqliteTestDatabase> CreateAsync()
    {
        var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<CareerHunterDbContext>()
            .UseSqlite(connection)
            .ReplaceService<IModelCustomizer, SqliteTestModelCustomizer>()
            // A distinct model customizer must not share the cached model/service provider
            // with the production configuration.
            .EnableServiceProviderCaching(false)
            .Options;

        var context = new CareerHunterDbContext(options);
        await context.Database.EnsureCreatedAsync();

        return new SqliteTestDatabase(connection, context);
    }

    public async ValueTask DisposeAsync()
    {
        await Context.DisposeAsync();
        await _connection.DisposeAsync();
    }

    private sealed class SqliteTestModelCustomizer(ModelCustomizerDependencies dependencies)
        : RelationalModelCustomizer(dependencies)
    {
        public override void Customize(ModelBuilder modelBuilder, DbContext context)
        {
            ArgumentNullException.ThrowIfNull(modelBuilder);
            ArgumentNullException.ThrowIfNull(context);

            // Runs the context's own OnModelCreating plus every IEntityTypeConfiguration.
            base.Customize(modelBuilder, context);

            foreach (var property in modelBuilder.Model.GetEntityTypes().SelectMany(type => type.GetProperties()))
            {
                if (property.ClrType == typeof(DateTimeOffset))
                {
                    property.SetValueConverter(new ValueConverter<DateTimeOffset, long>(
                        value => value.UtcTicks,
                        ticks => new DateTimeOffset(ticks, TimeSpan.Zero)));
                }
                else if (property.ClrType == typeof(DateTimeOffset?))
                {
                    property.SetValueConverter(new ValueConverter<DateTimeOffset?, long?>(
                        value => value.HasValue ? value.Value.UtcTicks : null,
                        ticks => ticks.HasValue ? new DateTimeOffset(ticks.Value, TimeSpan.Zero) : null));
                }
            }
        }
    }
}
