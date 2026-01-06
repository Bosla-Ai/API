using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Added_Complete_Track_Hierarchy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Domains",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Title = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IconUrl = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Domains", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Track",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Title = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IconUrl = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    FixedTagsPayload = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DomainId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Track", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Track_Domains_DomainId",
                        column: x => x.DomainId,
                        principalTable: "Domains",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TrackSection",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Title = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    IsMultiSelect = table.Column<bool>(type: "bit", nullable: false),
                    OrderIndex = table.Column<int>(type: "int", nullable: false),
                    TrackId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TrackSection", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TrackSection_Track_TrackId",
                        column: x => x.TrackId,
                        principalTable: "Track",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TrackChoice",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Label = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    TagsPayload = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsDefault = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    SectionId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TrackChoice", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TrackChoice_TrackSection_SectionId",
                        column: x => x.SectionId,
                        principalTable: "TrackSection",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Track_DomainId",
                table: "Track",
                column: "DomainId");

            migrationBuilder.CreateIndex(
                name: "IX_TrackChoice_SectionId",
                table: "TrackChoice",
                column: "SectionId");

            migrationBuilder.CreateIndex(
                name: "IX_TrackSection_TrackId",
                table: "TrackSection",
                column: "TrackId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TrackChoice");

            migrationBuilder.DropTable(
                name: "TrackSection");

            migrationBuilder.DropTable(
                name: "Track");

            migrationBuilder.DropTable(
                name: "Domains");
        }
    }
}
