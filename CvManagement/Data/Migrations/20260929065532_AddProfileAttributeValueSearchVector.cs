using Microsoft.EntityFrameworkCore.Migrations;
using NpgsqlTypes;

#nullable disable

namespace CvManagement.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddProfileAttributeValueSearchVector : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<NpgsqlTsVector>(
                name: "SearchVector",
                table: "ProfileAttributeValues",
                type: "tsvector",
                nullable: false);

            migrationBuilder.CreateIndex(
                name: "IX_ProfileAttributeValues_SearchVector",
                table: "ProfileAttributeValues",
                column: "SearchVector")
                .Annotation("Npgsql:IndexMethod", "gin");

            migrationBuilder.Sql(
                """
                CREATE OR REPLACE FUNCTION "fn_update_profile_value_search_vector"() RETURNS trigger AS $fn$
                BEGIN
                    NEW."SearchVector" :=
                        setweight(to_tsvector('english', coalesce(NEW."StringValue", '')), 'A') ||
                        setweight(to_tsvector('english', coalesce(NEW."TextValue", '')), 'A') ||
                        setweight(to_tsvector('english', coalesce(NEW."ImageUrl", '')), 'C') ||
                        setweight(to_tsvector('english', coalesce(NEW."NumericValue"::text, '')), 'B') ||
                        setweight(to_tsvector('english', coalesce(NEW."DateValue"::text, '')), 'B') ||
                        setweight(to_tsvector('english', coalesce(NEW."PeriodStart"::text, '')), 'C') ||
                        setweight(to_tsvector('english', coalesce(NEW."PeriodEnd"::text, '')), 'C') ||
                        setweight(to_tsvector('english', coalesce(NEW."BoolValue"::text, '')), 'C') ||
                        setweight(to_tsvector('english', coalesce(
                            (SELECT o."Label" FROM "AttributeOptions" o WHERE o."Id" = NEW."SelectedOptionId"), '')), 'A');
                    RETURN NEW;
                END;
                $fn$ LANGUAGE plpgsql;

                CREATE TRIGGER "trg_profile_value_search_vector"
                BEFORE INSERT OR UPDATE ON "ProfileAttributeValues"
                FOR EACH ROW EXECUTE FUNCTION "fn_update_profile_value_search_vector"();

                -- Backfill existing rows; the trigger only fires on future writes.
                UPDATE "ProfileAttributeValues" SET "SearchVector" = "SearchVector";
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DROP TRIGGER IF EXISTS "trg_profile_value_search_vector" ON "ProfileAttributeValues";
                DROP FUNCTION IF EXISTS "fn_update_profile_value_search_vector"();
                """);

            migrationBuilder.DropIndex(
                name: "IX_ProfileAttributeValues_SearchVector",
                table: "ProfileAttributeValues");

            migrationBuilder.DropColumn(
                name: "SearchVector",
                table: "ProfileAttributeValues");
        }
    }
}
