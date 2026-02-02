using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Final_Test_Hybrid.Migrations
{
    /// <inheritdoc />
    public partial class AddAdminInterrupted : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ADMIN_INTERRUPTED",
                table: "TB_OPERATION",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ADMIN_INTERRUPTED",
                table: "TB_OPERATION");
        }
    }
}
