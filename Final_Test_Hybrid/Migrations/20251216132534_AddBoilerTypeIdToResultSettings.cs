using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Final_Test_Hybrid.Migrations
{
    /// <inheritdoc />
    public partial class AddBoilerTypeIdToResultSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IDX_TB_RESULT_SETTINGS_UNQ_ADDRESS_VALUE",
                table: "TB_RESULT_SETTINGS");

            migrationBuilder.DropIndex(
                name: "IDX_TB_RESULT_SETTINGS_UNQ_PARAMETER_NAME",
                table: "TB_RESULT_SETTINGS");

            migrationBuilder.AddColumn<long>(
                name: "BOILER_TYPE_ID",
                table: "TB_RESULT_SETTINGS",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.CreateIndex(
                name: "IDX_TB_RESULT_SETTINGS_BOILER_TYPE",
                table: "TB_RESULT_SETTINGS",
                column: "BOILER_TYPE_ID");

            migrationBuilder.CreateIndex(
                name: "IDX_TB_RESULT_SETTINGS_UNQ_ADDRESS_VALUE_BOILER_TYPE",
                table: "TB_RESULT_SETTINGS",
                columns: new[] { "ADDRESS_VALUE", "BOILER_TYPE_ID" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IDX_TB_RESULT_SETTINGS_UNQ_PARAMETER_NAME_BOILER_TYPE",
                table: "TB_RESULT_SETTINGS",
                columns: new[] { "PARAMETER_NAME", "BOILER_TYPE_ID" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_TB_RESULT_SETTINGS_TB_BOILER_TYPE_BOILER_TYPE_ID",
                table: "TB_RESULT_SETTINGS",
                column: "BOILER_TYPE_ID",
                principalTable: "TB_BOILER_TYPE",
                principalColumn: "ID",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TB_RESULT_SETTINGS_TB_BOILER_TYPE_BOILER_TYPE_ID",
                table: "TB_RESULT_SETTINGS");

            migrationBuilder.DropIndex(
                name: "IDX_TB_RESULT_SETTINGS_BOILER_TYPE",
                table: "TB_RESULT_SETTINGS");

            migrationBuilder.DropIndex(
                name: "IDX_TB_RESULT_SETTINGS_UNQ_ADDRESS_VALUE_BOILER_TYPE",
                table: "TB_RESULT_SETTINGS");

            migrationBuilder.DropIndex(
                name: "IDX_TB_RESULT_SETTINGS_UNQ_PARAMETER_NAME_BOILER_TYPE",
                table: "TB_RESULT_SETTINGS");

            migrationBuilder.DropColumn(
                name: "BOILER_TYPE_ID",
                table: "TB_RESULT_SETTINGS");

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
    }
}
