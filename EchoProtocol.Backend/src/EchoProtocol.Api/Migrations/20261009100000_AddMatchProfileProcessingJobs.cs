using EchoProtocol.Api.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace EchoProtocol.Api.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20261009100000_AddMatchProfileProcessingJobs")]
public sealed class AddMatchProfileProcessingJobs : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.CreateTable(
        name: "MatchProfileProcessingJobs",
        columns: table => new
        {
            MatchId = table.Column<Guid>(type: "uuid", nullable: false),
            Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
            Attempts = table.Column<int>(type: "integer", nullable: false),
            NextAttemptAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
            LeaseExpiresAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
            LastError = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false)
        },
        constraints: table => table.PrimaryKey("PK_MatchProfileProcessingJobs", x => x.MatchId));

    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.DropTable(name: "MatchProfileProcessingJobs");
}
