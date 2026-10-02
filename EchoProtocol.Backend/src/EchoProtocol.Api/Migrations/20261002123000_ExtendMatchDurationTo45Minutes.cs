using EchoProtocol.Api.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EchoProtocol.Api.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20261002123000_ExtendMatchDurationTo45Minutes")]
public sealed class ExtendMatchDurationTo45Minutes : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropCheckConstraint(
            name: "CK_MatchResults_DurationSeconds_Range",
            table: "MatchResults");

        migrationBuilder.AddCheckConstraint(
            name: "CK_MatchResults_DurationSeconds_Range",
            table: "MatchResults",
            sql: "\"DurationSeconds\" >= 60 AND \"DurationSeconds\" <= 2760");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropCheckConstraint(
            name: "CK_MatchResults_DurationSeconds_Range",
            table: "MatchResults");

        migrationBuilder.AddCheckConstraint(
            name: "CK_MatchResults_DurationSeconds_Range",
            table: "MatchResults",
            sql: "\"DurationSeconds\" >= 60 AND \"DurationSeconds\" <= 900");
    }
}
