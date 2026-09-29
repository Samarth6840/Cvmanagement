using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CvManagement.Data.Migrations
{
    /// <summary>
    /// Spec §13.1: the category becomes a lookup table referenced by <c>category_id</c> instead
    /// of an inline enum value.
    ///
    /// The generated column rename preserves the old 0-7 enum values, so the lookup rows are
    /// seeded and every existing row is shifted by one to the new stable ids *before* the
    /// foreign key is added. Ordering matters: adding the constraint first would fail on the old
    /// values, and rewriting the column afterwards would lose them.
    /// </summary>
    public partial class AddAttributeCategoryLookup : Migration
    {
        /// <inheritdoc />
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

            // Fixed, seeded lookup list. Mirrors AttributeCategoryCatalog; the ids are stable and
            // are referenced from code, so they are inserted explicitly rather than generated.
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

            // Certification = 0 ... Other = 7 all shift to their new 1-8 ids.
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

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AttributeDefinitions_AttributeCategories_CategoryId",
                table: "AttributeDefinitions");

            migrationBuilder.DropIndex(
                name: "IX_AttributeDefinitions_CategoryId",
                table: "AttributeDefinitions");

            // Back into the enum range before the lookup table disappears.
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
