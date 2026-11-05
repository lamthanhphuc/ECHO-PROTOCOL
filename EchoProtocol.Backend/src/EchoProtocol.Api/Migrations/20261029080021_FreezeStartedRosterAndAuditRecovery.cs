using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EchoProtocol.Api.Migrations
{
    /// <inheritdoc />
    public partial class FreezeStartedRosterAndAuditRecovery : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "RecoveryAudit",
                table: "MatchResults",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "Participated",
                table: "MatchPlayerBindings",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            // Preserve legacy started matches; historical lobby attendance cannot
            // be reconstructed reliably. New starts freeze only connected members.
            migrationBuilder.Sql("UPDATE \"MatchPlayerBindings\" AS p SET \"Participated\" = TRUE FROM \"MatchAuthorityBindings\" AS m WHERE p.\"MatchId\" = m.\"MatchId\" AND m.\"StartedAtUtc\" IS NOT NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RecoveryAudit",
                table: "MatchResults");

            migrationBuilder.DropColumn(
                name: "Participated",
                table: "MatchPlayerBindings");
        }
    }
}
