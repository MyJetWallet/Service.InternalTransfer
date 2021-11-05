using Microsoft.EntityFrameworkCore.Migrations;

namespace Service.InternalTransfer.Postgres.Migrations
{
    public partial class AddLastTsIndex : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_internal-transfers_LastTs",
                schema: "internal-transfers",
                table: "internal-transfers",
                column: "LastTs");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_internal-transfers_LastTs",
                schema: "internal-transfers",
                table: "internal-transfers");
        }
    }
}
