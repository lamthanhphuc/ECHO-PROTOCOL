using EchoProtocol.Api.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EchoProtocol.Api.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20261026130000_ExtendMatchDurationToTwoHours")]
public sealed class ExtendMatchDurationToTwoHours : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropCheckConstraint(
            name: "CK_MatchResults_DurationSeconds_Range", table: "MatchResults");
        migrationBuilder.AddCheckConstraint(
            name: "CK_MatchResults_DurationSeconds_Range", table: "MatchResults",
            sql: "\"DurationSeconds\" >= 60 AND \"DurationSeconds\" <= 7260");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropCheckConstraint(
            name: "CK_MatchResults_DurationSeconds_Range", table: "MatchResults");
        migrationBuilder.AddCheckConstraint(
            name: "CK_MatchResults_DurationSeconds_Range", table: "MatchResults",
            sql: "\"DurationSeconds\" >= 60 AND \"DurationSeconds\" <= 2760");
    }
}
