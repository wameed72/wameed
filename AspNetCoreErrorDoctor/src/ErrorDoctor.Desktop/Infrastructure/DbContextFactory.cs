using ErrorDoctor.Core.Data;
using Microsoft.EntityFrameworkCore;

namespace ErrorDoctor.Desktop.Infrastructure;

/// <summary>
/// Creates short-lived <see cref="ErrorDoctorDbContext"/> instances bound to the configured SQL Server.
/// </summary>
public class DbContextFactory
{
    private readonly string _connectionString;

    public DbContextFactory(string connectionString)
    {
        _connectionString = connectionString;
    }

    public ErrorDoctorDbContext Create()
    {
        var options = new DbContextOptionsBuilder<ErrorDoctorDbContext>()
            .UseSqlServer(_connectionString)
            .Options;
        return new ErrorDoctorDbContext(options);
    }
}
