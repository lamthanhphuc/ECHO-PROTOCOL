using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EchoProtocol.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddUserAgreementAcceptance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "UserAgreementAcceptances",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    BundleVersion = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    ContentSha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    DocumentVersions = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    AcceptedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Locale = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    AdultConfirmed = table.Column<bool>(type: "boolean", nullable: false),
                    WarningAcknowledged = table.Column<bool>(type: "boolean", nullable: false),
                    TermsAccepted = table.Column<bool>(type: "boolean", nullable: false),
                    PrivacyNoticeAcknowledged = table.Column<bool>(type: "boolean", nullable: false),
                    CommunityAccepted = table.Column<bool>(type: "boolean", nullable: false),
                    TransactionsAccepted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserAgreementAcceptances", x => new { x.UserId, x.BundleVersion });
                    table.ForeignKey(
                        name: "FK_UserAgreementAcceptances_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserAgreementAcceptances");
        }
    }
}
