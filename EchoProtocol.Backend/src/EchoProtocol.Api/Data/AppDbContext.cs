using Microsoft.EntityFrameworkCore;

namespace EchoProtocol.Api.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    // DbSets will be added in the Auth/Entities phase.
}
