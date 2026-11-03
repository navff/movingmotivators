using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Motivators.Infrastructure.Data;

/// <summary>Для <c>dotnet ef migrations add</c>: подключение к БД не требуется.</summary>
public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>();
        DependencyInjection.ConfigureDb(options, "Host=localhost;Database=motivators;Username=postgres;Password=postgres");
        return new AppDbContext(options.Options);
    }
}
