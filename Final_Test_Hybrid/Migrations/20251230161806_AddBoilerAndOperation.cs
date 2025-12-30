using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Final_Test_Hybrid.Migrations
{
    /// <inheritdoc />
    public partial class AddBoilerAndOperation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TB_BOILER",
                columns: table => new
                {
                    ID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SERIAL_NUMBER = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    BOILER_TYPE_CYCLE_ID = table.Column<long>(type: "bigint", nullable: false),
                    DATE_CREATE = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DATE_UPDATE = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    STATUS = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    OPERATOR = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TB_BOILER", x => x.ID);
                    table.ForeignKey(
                        name: "FK_TB_BOILER_TB_BOILER_TYPE_CYCLE_BOILER_TYPE_CYCLE_ID",
                        column: x => x.BOILER_TYPE_CYCLE_ID,
                        principalTable: "TB_BOILER_TYPE_CYCLE",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TB_OPERATION",
                columns: table => new
                {
                    ID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DATE_START = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DATE_END = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    BOILER_ID = table.Column<long>(type: "bigint", nullable: false),
                    STATUS = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    NUMBER_SHIFT = table.Column<int>(type: "integer", nullable: false),
                    COMMENT_ = table.Column<string>(type: "text", nullable: true),
                    VERSION = table.Column<int>(type: "integer", nullable: false),
                    OPERATOR = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TB_OPERATION", x => x.ID);
                    table.ForeignKey(
                        name: "FK_TB_OPERATION_TB_BOILER_BOILER_ID",
                        column: x => x.BOILER_ID,
                        principalTable: "TB_BOILER",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IDX_TB_BOILER_BOILER_TYPE_CYCLE",
                table: "TB_BOILER",
                column: "BOILER_TYPE_CYCLE_ID");

            migrationBuilder.CreateIndex(
                name: "IDX_TB_BOILER_DATE_CREATE",
                table: "TB_BOILER",
                column: "DATE_CREATE");

            migrationBuilder.CreateIndex(
                name: "IDX_TB_BOILER_DATE_UPDATE",
                table: "TB_BOILER",
                column: "DATE_UPDATE");

            migrationBuilder.CreateIndex(
                name: "IDX_TB_BOILER_OPERATOR",
                table: "TB_BOILER",
                column: "OPERATOR");

            migrationBuilder.CreateIndex(
                name: "IDX_TB_BOILER_STATUS",
                table: "TB_BOILER",
                column: "STATUS");

            migrationBuilder.CreateIndex(
                name: "IDX_TB_BOILER_UNQ_SERIAL_NUMBER",
                table: "TB_BOILER",
                column: "SERIAL_NUMBER",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IDX_TB_OPERATION_BOILER",
                table: "TB_OPERATION",
                column: "BOILER_ID");

            migrationBuilder.CreateIndex(
                name: "IDX_TB_OPERATION_BOILER_DATE_START",
                table: "TB_OPERATION",
                columns: new[] { "BOILER_ID", "DATE_START" });

            migrationBuilder.CreateIndex(
                name: "IDX_TB_OPERATION_DATE_END",
                table: "TB_OPERATION",
                column: "DATE_END");

            migrationBuilder.CreateIndex(
                name: "IDX_TB_OPERATION_DATE_START",
                table: "TB_OPERATION",
                column: "DATE_START");

            migrationBuilder.CreateIndex(
                name: "IDX_TB_OPERATION_OPERATOR",
                table: "TB_OPERATION",
                column: "OPERATOR");

            migrationBuilder.CreateIndex(
                name: "IDX_TB_OPERATION_STATUS",
                table: "TB_OPERATION",
                column: "STATUS");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TB_OPERATION");

            migrationBuilder.DropTable(
                name: "TB_BOILER");
        }
    }
}
