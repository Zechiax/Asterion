using Asterion.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Asterion.Test.Notifications;

/// <summary>
///     Builds a minimal IServiceProvider with an in-memory database.
/// </summary>
internal static class InMemoryDb
{
    // Concrete ServiceProvider (not the IServiceProvider interface) so callers can `await using` it directly.
    public static ServiceProvider BuildServiceProvider(string? databaseName = null)
    {
        // AddDbContext re-invokes the options delegate on every scope's resolution - the name must be
        // computed once, outside the closure, or each scope would get its own isolated random database.
        var name = databaseName ?? Guid.NewGuid().ToString();

        var services = new ServiceCollection();
        services.AddDbContext<DataContext>(options => options.UseInMemoryDatabase(name));
        return services.BuildServiceProvider();
    }
}
