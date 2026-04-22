using Microsoft.EntityFrameworkCore;
using Session.Infrastructure;

namespace Session.UnitTests.Application;

/// <summary>
/// Helpers for building an <see cref="AppDbContext"/> backed by EF InMemory.
/// Every call gets a unique DB name so tests are isolated.
/// </summary>
internal static class InMemoryDbFactory
{
    public static AppDbContext Create()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase("ft-tests-" + Guid.NewGuid())
            // The InMemory provider chokes on OwnsOne value-object mapping by default; this
            // warning is the supported way to silence it in tests.
            .ConfigureWarnings(w => w.Ignore(
                Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        return new AppDbContext(options);
    }
}
