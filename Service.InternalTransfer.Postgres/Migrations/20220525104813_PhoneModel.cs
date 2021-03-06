using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Service.InternalTransfer.Postgres.Migrations
{
    public partial class PhoneModel : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PhoneModel",
                schema: "internal-transfers",
                table: "internal-transfers",
                type: "text",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PhoneModel",
                schema: "internal-transfers",
                table: "internal-transfers");
        }
    }
}
