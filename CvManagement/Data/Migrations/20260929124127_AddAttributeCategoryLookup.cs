using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CvManagement.Data.Migrations
{
    public partial class AddAttributeCategoryLookup : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Category",
                table: "AttributeDefinitions",
                newName: "CategoryId");

            migrationBuilder.CreateTable(
                name: "AttributeCategories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AttributeCategories", x => x.Id);
                });

            migrationBuilder.Sql(
                """
                INSERT INTO "AttributeCategories" ("Id", "Name", "SortOrder") VALUES
                    (1, 'Certification', 1),
                    (2, 'Domain Knowledge', 2),
                    (3, 'Personal Information', 3),
                    (4, 'Soft Skills', 4),
                    (5, 'Technical Skills', 5),
                    (6, 'Education', 6),
                    (7, 'Experience', 7),
                    (8, 'Other', 8);
                """);

            migrationBuilder.Sql(
                """UPDATE "AttributeDefinitions" SET "CategoryId" = "CategoryId" + 1;""");

            migrationBuilder.CreateIndex(
                name: "IX_AttributeDefinitions_CategoryId",
                table: "AttributeDefinitions",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_AttributeCategories_Name",
                table: "AttributeCategories",
                column: "Name",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_AttributeDefinitions_AttributeCategories_CategoryId",
                table: "AttributeDefinitions",
                column: "CategoryId",
                principalTable: "AttributeCategories",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AttributeDefinitions_AttributeCategories_CategoryId",
                table: "AttributeDefinitions");

            migrationBuilder.DropIndex(
                name: "IX_AttributeDefinitions_CategoryId",
                table: "AttributeDefinitions");

            migrationBuilder.Sql(
                """UPDATE "AttributeDefinitions" SET "CategoryId" = "CategoryId" - 1;""");

            migrationBuilder.DropTable(
                name: "AttributeCategories");

            migrationBuilder.RenameColumn(
                name: "CategoryId",
                table: "AttributeDefinitions",
                newName: "Category");
        }
    }
}
