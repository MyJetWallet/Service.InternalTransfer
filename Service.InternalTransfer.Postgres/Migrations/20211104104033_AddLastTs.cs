using System;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Service.InternalTransfer.Postgres.Migrations
{
    public partial class AddLastTs : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "LastTs",
                schema: "internal-transfers",
                table: "internal-transfers",
                type: "timestamp without time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LastTs",
                schema: "internal-transfers",
                table: "internal-transfers");
        }
    }
}
