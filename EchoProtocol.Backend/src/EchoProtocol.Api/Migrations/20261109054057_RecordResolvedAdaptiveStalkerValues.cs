using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EchoProtocol.Api.Migrations
{
    /// <inheritdoc />
    public partial class RecordResolvedAdaptiveStalkerValues : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "PreviousChaseSpeed",
                table: "AEDV02Decisions",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StalkerValuesJson",
                table: "AEDV02Decisions",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PreviousChaseSpeed",
                table: "AEDV02Decisions");

            migrationBuilder.DropColumn(
                name: "StalkerValuesJson",
                table: "AEDV02Decisions");
        }
    }
}
