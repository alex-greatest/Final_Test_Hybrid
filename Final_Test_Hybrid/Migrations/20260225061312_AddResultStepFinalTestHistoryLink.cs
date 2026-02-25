using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Final_Test_Hybrid.Migrations
{
    /// <inheritdoc />
    public partial class AddResultStepFinalTestHistoryLink : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "STEP_FINAL_TEST_HISTORY_ID",
                table: "TB_RESULT",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IDX_TB_RESULT_STEP_FINAL_TEST_HISTORY",
                table: "TB_RESULT",
                column: "STEP_FINAL_TEST_HISTORY_ID");

            migrationBuilder.AddForeignKey(
                name: "FK_TB_RESULT_TB_STEP_FINAL_TEST_HISTORY_STEP_FINAL_TEST_HISTOR~",
                table: "TB_RESULT",
                column: "STEP_FINAL_TEST_HISTORY_ID",
                principalTable: "TB_STEP_FINAL_TEST_HISTORY",
                principalColumn: "ID",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TB_RESULT_TB_STEP_FINAL_TEST_HISTORY_STEP_FINAL_TEST_HISTOR~",
                table: "TB_RESULT");

            migrationBuilder.DropIndex(
                name: "IDX_TB_RESULT_STEP_FINAL_TEST_HISTORY",
                table: "TB_RESULT");

            migrationBuilder.DropColumn(
                name: "STEP_FINAL_TEST_HISTORY_ID",
                table: "TB_RESULT");
        }
    }
}
