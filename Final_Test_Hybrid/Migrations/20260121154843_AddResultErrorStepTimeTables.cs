using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Final_Test_Hybrid.Migrations
{
    /// <inheritdoc />
    public partial class AddResultErrorStepTimeTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TB_ERROR",
                columns: table => new
                {
                    ID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ERROR_SETTINGS_HISTORY_ID = table.Column<long>(type: "bigint", nullable: false),
                    OPERATION_ID = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TB_ERROR", x => x.ID);
                    table.ForeignKey(
                        name: "FK_TB_ERROR_TB_ERROR_SETTINGS_HISTORY_ERROR_SETTINGS_HISTORY_ID",
                        column: x => x.ERROR_SETTINGS_HISTORY_ID,
                        principalTable: "TB_ERROR_SETTINGS_HISTORY",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TB_ERROR_TB_OPERATION_OPERATION_ID",
                        column: x => x.OPERATION_ID,
                        principalTable: "TB_OPERATION",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TB_RESULT",
                columns: table => new
                {
                    ID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MIN_ = table.Column<string>(type: "text", nullable: true),
                    VALUE_ = table.Column<string>(type: "text", nullable: false),
                    MAX_ = table.Column<string>(type: "text", nullable: true),
                    STATUS = table.Column<int>(type: "integer", nullable: true),
                    OPERATION_ID = table.Column<long>(type: "bigint", nullable: false),
                    RESULT_SETTING_HISTORY_ID = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TB_RESULT", x => x.ID);
                    table.ForeignKey(
                        name: "FK_TB_RESULT_TB_OPERATION_OPERATION_ID",
                        column: x => x.OPERATION_ID,
                        principalTable: "TB_OPERATION",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TB_RESULT_TB_RESULT_SETTING_HISTORY_RESULT_SETTING_HISTORY_~",
                        column: x => x.RESULT_SETTING_HISTORY_ID,
                        principalTable: "TB_RESULT_SETTING_HISTORY",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TB_STEP_TIME",
                columns: table => new
                {
                    ID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    STEP_FINAL_TEST_HISTORY_ID = table.Column<long>(type: "bigint", nullable: false),
                    OPERATION_ID = table.Column<long>(type: "bigint", nullable: false),
                    DURATION = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TB_STEP_TIME", x => x.ID);
                    table.ForeignKey(
                        name: "FK_TB_STEP_TIME_TB_OPERATION_OPERATION_ID",
                        column: x => x.OPERATION_ID,
                        principalTable: "TB_OPERATION",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TB_STEP_TIME_TB_STEP_FINAL_TEST_HISTORY_STEP_FINAL_TEST_HIS~",
                        column: x => x.STEP_FINAL_TEST_HISTORY_ID,
                        principalTable: "TB_STEP_FINAL_TEST_HISTORY",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IDX_TB_ERROR_ERROR_SETTINGS_HISTORY",
                table: "TB_ERROR",
                column: "ERROR_SETTINGS_HISTORY_ID");

            migrationBuilder.CreateIndex(
                name: "IDX_TB_ERROR_OPERATION",
                table: "TB_ERROR",
                column: "OPERATION_ID");

            migrationBuilder.CreateIndex(
                name: "IDX_TB_RESULT_OPERATION",
                table: "TB_RESULT",
                column: "OPERATION_ID");

            migrationBuilder.CreateIndex(
                name: "IDX_TB_RESULT_RESULT_SETTING_HISTORY",
                table: "TB_RESULT",
                column: "RESULT_SETTING_HISTORY_ID");

            migrationBuilder.CreateIndex(
                name: "IDX_TB_STEP_TIME_OPERATION",
                table: "TB_STEP_TIME",
                column: "OPERATION_ID");

            migrationBuilder.CreateIndex(
                name: "IDX_TB_STEP_TIME_STEP_FINAL_TEST_HISTORY",
                table: "TB_STEP_TIME",
                column: "STEP_FINAL_TEST_HISTORY_ID");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TB_ERROR");

            migrationBuilder.DropTable(
                name: "TB_RESULT");

            migrationBuilder.DropTable(
                name: "TB_STEP_TIME");
        }
    }
}
