using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddGetDomainsHierarchySP : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Create stored procedure for getting domains with full hierarchy
            migrationBuilder.Sql(@"
CREATE PROCEDURE sp_GetAllDomainsWithHierarchy
    @IsActive BIT = NULL
AS
BEGIN
    SET NOCOUNT ON;
    
    SELECT 
        d.Id AS DomainId,
        d.Title AS DomainTitle,
        d.Description AS DomainDescription,
        d.IconUrl AS DomainIconUrl,
        d.IsActive AS DomainIsActive,
        t.Id AS TrackId,
        t.Title AS TrackTitle,
        t.Description AS TrackDescription,
        t.IconUrl AS TrackIconUrl,
        t.IsActive AS TrackIsActive,
        t.FixedTagsPayload,
        ts.Id AS SectionId,
        ts.Title AS SectionTitle,
        ts.IsMultiSelect,
        ts.OrderIndex,
        tc.Id AS ChoiceId,
        tc.Label AS ChoiceLabel,
        tc.TagsPayload AS ChoiceTagsPayload,
        tc.IsDefault AS ChoiceIsDefault
    FROM Domains d
    LEFT JOIN Track t ON d.Id = t.DomainId
    LEFT JOIN TrackSection ts ON t.Id = ts.TrackId
    LEFT JOIN TrackChoice tc ON ts.Id = tc.SectionId
    WHERE (@IsActive IS NULL OR d.IsActive = @IsActive)
    ORDER BY d.Id, t.Id, ts.OrderIndex, tc.Id;
END
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Drop the stored procedure
            migrationBuilder.Sql("DROP PROCEDURE IF EXISTS sp_GetAllDomainsWithHierarchy");
        }
    }
}
