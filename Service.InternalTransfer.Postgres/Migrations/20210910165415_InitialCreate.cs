using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

namespace Service.InternalTransfer.Postgres.Migrations
{
    public partial class InitialCreate : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "internal-transfers");

            migrationBuilder.CreateTable(
                name: "internal-transfers",
                schema: "internal-transfers",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    BrokerId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    ClientId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    WalletId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    TransactionId = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    Amount = table.Column<double>(type: "double precision", nullable: false),
                    AssetSymbol = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    SenderPhoneNumber = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    DestinationPhoneNumber = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    DestinationClientId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    DestinationWalletId = table.Column<string>(type: "text", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    MatchingEngineId = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    LastError = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    RetriesCount = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    EventDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    ClientLang = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    ClientIp = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    NotificationTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    WorkflowState = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    RefundTransactionId = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    Cancelling = table.Column<bool>(type: "boolean", nullable: false),
                    MeErrorCode = table.Column<int>(type: "integer", nullable: false, defaultValue: 0)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_internal-transfers", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_internal-transfers_Status",
                schema: "internal-transfers",
                table: "internal-transfers",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_internal-transfers_TransactionId",
                schema: "internal-transfers",
                table: "internal-transfers",
                column: "TransactionId",
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "internal-transfers",
                schema: "internal-transfers");
        }
    }
}
