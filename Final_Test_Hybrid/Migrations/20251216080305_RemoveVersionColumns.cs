using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Final_Test_Hybrid.Migrations
{
    /// <inheritdoc />
    public partial class RemoveVersionColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "VERSION",
                table: "TB_RECIPE");

            migrationBuilder.DropColumn(
                name: "VERSION",
                table: "TB_BOILER_TYPE_CYCLE");

            migrationBuilder.DropColumn(
                name: "VERSION",
                table: "TB_BOILER_TYPE");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "VERSION",
                table: "TB_RECIPE",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "VERSION",
                table: "TB_BOILER_TYPE_CYCLE",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "VERSION",
                table: "TB_BOILER_TYPE",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }
    }
}
