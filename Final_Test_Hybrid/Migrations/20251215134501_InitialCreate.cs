using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Final_Test_Hybrid.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TB_BOILER_TYPE",
                columns: table => new
                {
                    ID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ARTICLE = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    TYPE = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    VERSION = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TB_BOILER_TYPE", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "TB_BOILER_TYPE_CYCLE",
                columns: table => new
                {
                    ID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    BOILER_TYPE_ID = table.Column<long>(type: "bigint", nullable: false),
                    TYPE = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    IS_ACTIVE = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    ARTICLE = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    VERSION = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TB_BOILER_TYPE_CYCLE", x => x.ID);
                });

            migrationBuilder.CreateIndex(
                name: "IDX_TB_BOILER_TYPE_UNQ_ARTICLE",
                table: "TB_BOILER_TYPE",
                column: "ARTICLE",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IDX_TB_BOILER_TYPE_CYCLE_UNQ_ACTIVE",
                table: "TB_BOILER_TYPE_CYCLE",
                column: "BOILER_TYPE_ID",
                unique: true,
                filter: "\"IS_ACTIVE\" = true");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TB_BOILER_TYPE");

            migrationBuilder.DropTable(
                name: "TB_BOILER_TYPE_CYCLE");
        }
    }
}
