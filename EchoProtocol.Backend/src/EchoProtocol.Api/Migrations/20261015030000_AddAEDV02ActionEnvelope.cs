using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EchoProtocol.Api.Migrations
{
    public partial class AddAEDV02ActionEnvelope : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(name: "ProposalJson", table: "AEDV02Decisions", type: "text", nullable: false, defaultValue: "");
            migrationBuilder.AddColumn<bool>(name: "HadGameplayDisconnect", table: "MatchPlayerBindings", type: "boolean", nullable: false, defaultValue: false);
            migrationBuilder.AddColumn<string>(name: "ActionFingerprint", table: "AEDV02Decisions", type: "character varying(64)", maxLength: 64, nullable: false, defaultValue: "");
            migrationBuilder.CreateTable(name: "AEDV02Cancellations", columns: table => new
            {
                DecisionId = table.Column<System.Guid>(type: "uuid", nullable: false),
                MatchId = table.Column<System.Guid>(type: "uuid", nullable: false),
                HostUserId = table.Column<System.Guid>(type: "uuid", nullable: false),
                CreatedAtUtc = table.Column<System.DateTime>(type: "timestamp with time zone", nullable: false)
            }, constraints: table =>
            {
                table.PrimaryKey("PK_AEDV02Cancellations", x => x.DecisionId);
                table.ForeignKey(name: "FK_AEDV02Cancellations_MatchAuthorityBindings_MatchId", column: x => x.MatchId,
                    principalTable: "MatchAuthorityBindings", principalColumn: "MatchId", onDelete: ReferentialAction.Restrict);
            });
            migrationBuilder.CreateIndex(name: "IX_AEDV02Cancellations_MatchId", table: "AEDV02Cancellations", column: "MatchId");
        }
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "AEDV02Cancellations");
            migrationBuilder.DropColumn(name: "HadGameplayDisconnect", table: "MatchPlayerBindings");
            migrationBuilder.DropColumn(name: "ActionFingerprint", table: "AEDV02Decisions");
            migrationBuilder.DropColumn(name: "ProposalJson", table: "AEDV02Decisions");
        }
    }
}
