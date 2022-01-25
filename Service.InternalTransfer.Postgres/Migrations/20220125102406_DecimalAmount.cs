using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Service.InternalTransfer.Postgres.Migrations
{
    public partial class DecimalAmount : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<decimal>(
                name: "Amount",
                schema: "internal-transfers",
                table: "internal-transfers",
                type: "numeric",
                nullable: false,
                oldClrType: typeof(double),
                oldType: "double precision");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<double>(
                name: "Amount",
                schema: "internal-transfers",
                table: "internal-transfers",
                type: "double precision",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric");
        }
    }
}
