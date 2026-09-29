using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using REC4.Infrastructure.Data;

namespace REC4.Application.Tests;

internal static class TestDbContextFactory
{
    public static Rec4DbContext CreateInMemory()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();

        var options = new DbContextOptionsBuilder<Rec4DbContext>()
            .UseSqlite(connection)
            .Options;

        var context = new Rec4DbContext(options);
        context.Database.EnsureCreated();
        return context;
    }
}
