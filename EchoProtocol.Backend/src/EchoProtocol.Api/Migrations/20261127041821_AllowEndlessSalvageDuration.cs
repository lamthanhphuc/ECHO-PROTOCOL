using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EchoProtocol.Api.Migrations
{
    /// <inheritdoc />
    public partial class AllowEndlessSalvageDuration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint("CK_MatchResults_DurationSeconds_Range", "MatchResults");
            migrationBuilder.AddCheckConstraint("CK_MatchResults_DurationSeconds_Range", "MatchResults", "\"DurationSeconds\" >= 60 AND (\"DurationSeconds\" <= 7260 OR \"SalvageCompletedDays\" IS NOT NULL)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint("CK_MatchResults_DurationSeconds_Range", "MatchResults");
            migrationBuilder.AddCheckConstraint("CK_MatchResults_DurationSeconds_Range", "MatchResults", "\"DurationSeconds\" >= 60 AND \"DurationSeconds\" <= 7260");
        }
    }
}
