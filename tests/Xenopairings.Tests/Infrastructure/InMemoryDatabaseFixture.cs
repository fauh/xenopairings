using Microsoft.EntityFrameworkCore;
using Xenopairings.Data;

namespace Xenopairings.Tests.Infrastructure;

// CA1001: disposed via IAsyncLifetime.DisposeAsync — xUnit calls it; analyzer can't see it
#pragma warning disable CA1001
public sealed class InMemoryDatabaseFixture : IAsyncLifetime
#pragma warning restore CA1001
{
    private DbContextOptions<AppDbContext>? _options;

    public Task InitializeAsync()
    {
        // Use a unique database name per fixture instance so parallel test classes don't share state.
        var dbName = $"xenopairings_test_{Guid.NewGuid():N}";
        _options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;

        using var ctx = new AppDbContext(_options);
        ctx.Database.EnsureCreated();  // In-memory provider: EnsureCreated applies the current model
        return Task.CompletedTask;
    }

    public AppDbContext CreateDbContext()
    {
        ArgumentNullException.ThrowIfNull(_options);
        return new AppDbContext(_options);
    }

    public Task DisposeAsync() => Task.CompletedTask;
}
