using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Final_Test_Hybrid.Migrations
{
    /// <inheritdoc />
    public partial class AddRecipeTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TB_RECIPE",
                columns: table => new
                {
                    ID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    BOILER_TYPE_ID = table.Column<long>(type: "bigint", nullable: false),
                    PLC_TYPE = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    IS_PLC = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    ADDRESS = table.Column<string>(type: "text", nullable: false),
                    TAG_NAME = table.Column<string>(type: "text", nullable: false),
                    VALUE = table.Column<string>(type: "text", nullable: false),
                    DESCRIPTION = table.Column<string>(type: "text", nullable: true),
                    UNIT = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    VERSION = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TB_RECIPE", x => x.ID);
                    table.ForeignKey(
                        name: "FK_TB_RECIPE_TB_BOILER_TYPE_BOILER_TYPE_ID",
                        column: x => x.BOILER_TYPE_ID,
                        principalTable: "TB_BOILER_TYPE",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IDX_TB_RECIPE_BOILER_TYPE",
                table: "TB_RECIPE",
                column: "BOILER_TYPE_ID");

            migrationBuilder.CreateIndex(
                name: "IDX_TB_RECIPE_UNQ_ADDRESS_BOILER_TYPE",
                table: "TB_RECIPE",
                columns: new[] { "ADDRESS", "BOILER_TYPE_ID" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IDX_TB_RECIPE_UNQ_TAG_NAME_BOILER_TYPE",
                table: "TB_RECIPE",
                columns: new[] { "TAG_NAME", "BOILER_TYPE_ID" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TB_RECIPE");
        }
    }
}
