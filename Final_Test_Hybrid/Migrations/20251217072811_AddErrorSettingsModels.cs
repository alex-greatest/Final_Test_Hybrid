using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Final_Test_Hybrid.Migrations
{
    /// <inheritdoc />
    public partial class AddErrorSettingsModels : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TB_STEP_FINAL_TEST",
                columns: table => new
                {
                    ID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    NAME = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TB_STEP_FINAL_TEST", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "TB_STEP_FINAL_TEST_HISTORY",
                columns: table => new
                {
                    ID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    STEP_FINAL_TEST_ID = table.Column<long>(type: "bigint", nullable: false),
                    NAME = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    IS_ACTIVE = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TB_STEP_FINAL_TEST_HISTORY", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "TB_ERROR_SETTINGS_TEMPLATE",
                columns: table => new
                {
                    ID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    STEP_ID = table.Column<long>(type: "bigint", nullable: true),
                    ADDRESS_ERROR = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    DESCRIPTION = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TB_ERROR_SETTINGS_TEMPLATE", x => x.ID);
                    table.ForeignKey(
                        name: "FK_TB_ERROR_SETTINGS_TEMPLATE_TB_STEP_FINAL_TEST_STEP_ID",
                        column: x => x.STEP_ID,
                        principalTable: "TB_STEP_FINAL_TEST",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "TB_ERROR_SETTINGS_HISTORY",
                columns: table => new
                {
                    ID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ERROR_SETTINGS_TEMPLATE_ID = table.Column<long>(type: "bigint", nullable: false),
                    STEP_HISTORY_ID = table.Column<long>(type: "bigint", nullable: true),
                    ADDRESS_ERROR = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    DESCRIPTION = table.Column<string>(type: "text", nullable: true),
                    IS_ACTIVE = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TB_ERROR_SETTINGS_HISTORY", x => x.ID);
                    table.ForeignKey(
                        name: "FK_TB_ERROR_SETTINGS_HISTORY_TB_ERROR_SETTINGS_TEMPLATE_ERROR_~",
                        column: x => x.ERROR_SETTINGS_TEMPLATE_ID,
                        principalTable: "TB_ERROR_SETTINGS_TEMPLATE",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TB_ERROR_SETTINGS_HISTORY_TB_STEP_FINAL_TEST_HISTORY_STEP_H~",
                        column: x => x.STEP_HISTORY_ID,
                        principalTable: "TB_STEP_FINAL_TEST_HISTORY",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IDX_TB_ERROR_SETTINGS_HISTORY_ADDRESS_ERROR",
                table: "TB_ERROR_SETTINGS_HISTORY",
                column: "ADDRESS_ERROR");

            migrationBuilder.CreateIndex(
                name: "IDX_TB_ERROR_SETTINGS_HISTORY_STEP_HISTORY",
                table: "TB_ERROR_SETTINGS_HISTORY",
                column: "STEP_HISTORY_ID");

            migrationBuilder.CreateIndex(
                name: "IDX_TB_ERROR_SETTINGS_HISTORY_UNQ_ACTIVE",
                table: "TB_ERROR_SETTINGS_HISTORY",
                column: "ERROR_SETTINGS_TEMPLATE_ID",
                unique: true,
                filter: "\"IS_ACTIVE\" = true");

            migrationBuilder.CreateIndex(
                name: "IDX_TB_ERROR_SETTINGS_TEMPLATE_ADDRESS_ERROR",
                table: "TB_ERROR_SETTINGS_TEMPLATE",
                column: "ADDRESS_ERROR");

            migrationBuilder.CreateIndex(
                name: "IDX_TB_ERROR_SETTINGS_TEMPLATE_STEP",
                table: "TB_ERROR_SETTINGS_TEMPLATE",
                column: "STEP_ID");

            migrationBuilder.CreateIndex(
                name: "IDX_TB_ERROR_SETTINGS_TEMPLATE_UNQ_ADDRESS_ERROR_STEP",
                table: "TB_ERROR_SETTINGS_TEMPLATE",
                columns: new[] { "ADDRESS_ERROR", "STEP_ID" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IDX_TB_STEP_FINAL_TEST_UNQ_NAME",
                table: "TB_STEP_FINAL_TEST",
                column: "NAME",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IDX_TB_STEP_FINAL_TEST_HISTORY_UNQ_ACTIVE",
                table: "TB_STEP_FINAL_TEST_HISTORY",
                column: "STEP_FINAL_TEST_ID",
                unique: true,
                filter: "\"IS_ACTIVE\" = true");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TB_ERROR_SETTINGS_HISTORY");

            migrationBuilder.DropTable(
                name: "TB_ERROR_SETTINGS_TEMPLATE");

            migrationBuilder.DropTable(
                name: "TB_STEP_FINAL_TEST_HISTORY");

            migrationBuilder.DropTable(
                name: "TB_STEP_FINAL_TEST");
        }
    }
}
