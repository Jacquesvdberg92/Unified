using Microsoft.EntityFrameworkCore;
using Unified.Data;

namespace Unified.Tests.Helpers;

public static class DbHelper
{
    public static AppDbContext CreateInMemory(string dbName)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;
        return new AppDbContext(options);
    }
}
