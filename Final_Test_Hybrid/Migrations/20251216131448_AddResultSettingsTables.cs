using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Final_Test_Hybrid.Migrations
{
    /// <inheritdoc />
    public partial class AddResultSettingsTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TB_RESULT_SETTING_HISTORY",
                columns: table => new
                {
                    ID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RESULTS_SETTINGS_ID = table.Column<long>(type: "bigint", nullable: false),
                    BOILER_TYPE_ID = table.Column<long>(type: "bigint", nullable: false),
                    PARAMETER_NAME = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ADDRESS_VALUE = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ADDRESS_MIN = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ADDRESS_MAX = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ADDRESS_STATUS = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    NOMINAL = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    PLC_TYPE = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    UNIT = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    DESCRIPTION = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    AUDIT_TYPE = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    IS_ACTIVE = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TB_RESULT_SETTING_HISTORY", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "TB_RESULT_SETTINGS",
                columns: table => new
                {
                    ID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PARAMETER_NAME = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ADDRESS_VALUE = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ADDRESS_MIN = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ADDRESS_MAX = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ADDRESS_STATUS = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    PLC_TYPE = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    NOMINAL = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    UNIT = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    DESCRIPTION = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    AUDIT_TYPE = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TB_RESULT_SETTINGS", x => x.ID);
                });

            migrationBuilder.CreateIndex(
                name: "IDX_TB_RESULT_SETTING_HISTORY_BOILER_TYPE",
                table: "TB_RESULT_SETTING_HISTORY",
                column: "BOILER_TYPE_ID");

            migrationBuilder.CreateIndex(
                name: "IDX_TB_RESULT_SETTING_HISTORY_UNQ_ACTIVE",
                table: "TB_RESULT_SETTING_HISTORY",
                column: "RESULTS_SETTINGS_ID",
                unique: true,
                filter: "\"IS_ACTIVE\" = true");

            migrationBuilder.CreateIndex(
                name: "IDX_TB_RESULT_SETTINGS_ADDRESS_MIN",
                table: "TB_RESULT_SETTINGS",
                column: "ADDRESS_MIN");

            migrationBuilder.CreateIndex(
                name: "IDX_TB_RESULT_SETTINGS_UNQ_ADDRESS_VALUE",
                table: "TB_RESULT_SETTINGS",
                column: "ADDRESS_VALUE",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IDX_TB_RESULT_SETTINGS_UNQ_PARAMETER_NAME",
                table: "TB_RESULT_SETTINGS",
                column: "PARAMETER_NAME",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TB_RESULT_SETTING_HISTORY");

            migrationBuilder.DropTable(
                name: "TB_RESULT_SETTINGS");
        }
    }
}
