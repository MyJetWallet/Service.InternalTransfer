using Microsoft.EntityFrameworkCore.Migrations;

namespace Service.InternalTransfer.Postgres.Migrations
{
    public partial class version_3 : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SenderName",
                schema: "internal-transfers",
                table: "internal-transfers",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SenderName",
                schema: "internal-transfers",
                table: "internal-transfers");
        }
    }
}
