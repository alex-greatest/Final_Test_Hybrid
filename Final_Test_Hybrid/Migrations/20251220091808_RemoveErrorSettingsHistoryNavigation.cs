using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Final_Test_Hybrid.Migrations
{
    /// <inheritdoc />
    public partial class RemoveErrorSettingsHistoryNavigation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TB_ERROR_SETTINGS_HISTORY_TB_ERROR_SETTINGS_TEMPLATE_ERROR_~",
                table: "TB_ERROR_SETTINGS_HISTORY");

            migrationBuilder.AlterColumn<long>(
                name: "ERROR_SETTINGS_TEMPLATE_ID",
                table: "TB_ERROR_SETTINGS_HISTORY",
                type: "bigint",
                nullable: true,
                oldClrType: typeof(long),
                oldType: "bigint");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<long>(
                name: "ERROR_SETTINGS_TEMPLATE_ID",
                table: "TB_ERROR_SETTINGS_HISTORY",
                type: "bigint",
                nullable: false,
                defaultValue: 0L,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_TB_ERROR_SETTINGS_HISTORY_TB_ERROR_SETTINGS_TEMPLATE_ERROR_~",
                table: "TB_ERROR_SETTINGS_HISTORY",
                column: "ERROR_SETTINGS_TEMPLATE_ID",
                principalTable: "TB_ERROR_SETTINGS_TEMPLATE",
                principalColumn: "ID",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
