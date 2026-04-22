using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddVideoProgressToRoadmapCourse : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CurrentPositionSeconds",
                table: "RoadmapCourse",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TotalDurationSeconds",
                table: "RoadmapCourse",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "VideoId",
                table: "RoadmapCourse",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CurrentPositionSeconds",
                table: "RoadmapCourse");

            migrationBuilder.DropColumn(
                name: "TotalDurationSeconds",
                table: "RoadmapCourse");

            migrationBuilder.DropColumn(
                name: "VideoId",
                table: "RoadmapCourse");
        }
    }
}
