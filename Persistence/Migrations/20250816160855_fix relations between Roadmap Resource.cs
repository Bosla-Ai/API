using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Persistence.Migrations
{
    /// <inheritdoc />
    public partial class fixrelationsbetweenRoadmapResource : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Roadmap_Customers_CustomerApplicationUserId",
                table: "Roadmap");

            migrationBuilder.DropForeignKey(
                name: "FK_RoadmapResources_Roadmap_RoadmapId",
                table: "RoadmapResources");

            migrationBuilder.DropForeignKey(
                name: "FK_Topic_Track_TrackId",
                table: "Topic");

            migrationBuilder.DropForeignKey(
                name: "FK_TopicTechnology_Technology_TechnologyId",
                table: "TopicTechnology");

            migrationBuilder.DropForeignKey(
                name: "FK_TopicTechnology_Topic_TopicId",
                table: "TopicTechnology");

            migrationBuilder.DropForeignKey(
                name: "FK_Track_Domains_DomainFieldId",
                table: "Track");

            migrationBuilder.DropForeignKey(
                name: "FK_TrackTechnology_Technology_TechnologyId",
                table: "TrackTechnology");

            migrationBuilder.DropForeignKey(
                name: "FK_TrackTechnology_Track_TrackId",
                table: "TrackTechnology");

            migrationBuilder.DropPrimaryKey(
                name: "PK_RoadmapResources",
                table: "RoadmapResources");

            migrationBuilder.DropIndex(
                name: "IX_RoadmapResources_ResourceId",
                table: "RoadmapResources");

            migrationBuilder.DropPrimaryKey(
                name: "PK_TrackTechnology",
                table: "TrackTechnology");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Track",
                table: "Track");

            migrationBuilder.DropPrimaryKey(
                name: "PK_TopicTechnology",
                table: "TopicTechnology");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Topic",
                table: "Topic");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Roadmap",
                table: "Roadmap");

            migrationBuilder.RenameTable(
                name: "TrackTechnology",
                newName: "TrackTechnologies");

            migrationBuilder.RenameTable(
                name: "Track",
                newName: "Tracks");

            migrationBuilder.RenameTable(
                name: "TopicTechnology",
                newName: "TopicTechnologies");

            migrationBuilder.RenameTable(
                name: "Topic",
                newName: "Topics");

            migrationBuilder.RenameTable(
                name: "Roadmap",
                newName: "Roadmaps");

            migrationBuilder.RenameIndex(
                name: "IX_TrackTechnology_TrackId",
                table: "TrackTechnologies",
                newName: "IX_TrackTechnologies_TrackId");

            migrationBuilder.RenameIndex(
                name: "IX_Track_DomainFieldId",
                table: "Tracks",
                newName: "IX_Tracks_DomainFieldId");

            migrationBuilder.RenameIndex(
                name: "IX_TopicTechnology_TopicId",
                table: "TopicTechnologies",
                newName: "IX_TopicTechnologies_TopicId");

            migrationBuilder.RenameIndex(
                name: "IX_Topic_TrackId",
                table: "Topics",
                newName: "IX_Topics_TrackId");

            migrationBuilder.RenameIndex(
                name: "IX_Roadmap_CustomerApplicationUserId",
                table: "Roadmaps",
                newName: "IX_Roadmaps_CustomerApplicationUserId");

            migrationBuilder.AddColumn<int>(
                name: "ResourceId",
                table: "Roadmaps",
                type: "int",
                nullable: true);

            migrationBuilder.AddPrimaryKey(
                name: "PK_RoadmapResources",
                table: "RoadmapResources",
                columns: new[] { "ResourceId", "RoadmapId" });

            migrationBuilder.AddPrimaryKey(
                name: "PK_TrackTechnologies",
                table: "TrackTechnologies",
                columns: new[] { "TechnologyId", "TrackId" });

            migrationBuilder.AddPrimaryKey(
                name: "PK_Tracks",
                table: "Tracks",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_TopicTechnologies",
                table: "TopicTechnologies",
                columns: new[] { "TechnologyId", "TopicId" });

            migrationBuilder.AddPrimaryKey(
                name: "PK_Topics",
                table: "Topics",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Roadmaps",
                table: "Roadmaps",
                column: "Id");

            migrationBuilder.CreateIndex(
                name: "IX_Roadmaps_ResourceId",
                table: "Roadmaps",
                column: "ResourceId");

            migrationBuilder.AddForeignKey(
                name: "FK_RoadmapResources_Roadmaps_RoadmapId",
                table: "RoadmapResources",
                column: "RoadmapId",
                principalTable: "Roadmaps",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Roadmaps_Customers_CustomerApplicationUserId",
                table: "Roadmaps",
                column: "CustomerApplicationUserId",
                principalTable: "Customers",
                principalColumn: "ApplicationUserId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Roadmaps_Resources_ResourceId",
                table: "Roadmaps",
                column: "ResourceId",
                principalTable: "Resources",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Topics_Tracks_TrackId",
                table: "Topics",
                column: "TrackId",
                principalTable: "Tracks",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_TopicTechnologies_Technology_TechnologyId",
                table: "TopicTechnologies",
                column: "TechnologyId",
                principalTable: "Technology",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_TopicTechnologies_Topics_TopicId",
                table: "TopicTechnologies",
                column: "TopicId",
                principalTable: "Topics",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Tracks_Domains_DomainFieldId",
                table: "Tracks",
                column: "DomainFieldId",
                principalTable: "Domains",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_TrackTechnologies_Technology_TechnologyId",
                table: "TrackTechnologies",
                column: "TechnologyId",
                principalTable: "Technology",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_TrackTechnologies_Tracks_TrackId",
                table: "TrackTechnologies",
                column: "TrackId",
                principalTable: "Tracks",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_RoadmapResources_Roadmaps_RoadmapId",
                table: "RoadmapResources");

            migrationBuilder.DropForeignKey(
                name: "FK_Roadmaps_Customers_CustomerApplicationUserId",
                table: "Roadmaps");

            migrationBuilder.DropForeignKey(
                name: "FK_Roadmaps_Resources_ResourceId",
                table: "Roadmaps");

            migrationBuilder.DropForeignKey(
                name: "FK_Topics_Tracks_TrackId",
                table: "Topics");

            migrationBuilder.DropForeignKey(
                name: "FK_TopicTechnologies_Technology_TechnologyId",
                table: "TopicTechnologies");

            migrationBuilder.DropForeignKey(
                name: "FK_TopicTechnologies_Topics_TopicId",
                table: "TopicTechnologies");

            migrationBuilder.DropForeignKey(
                name: "FK_Tracks_Domains_DomainFieldId",
                table: "Tracks");

            migrationBuilder.DropForeignKey(
                name: "FK_TrackTechnologies_Technology_TechnologyId",
                table: "TrackTechnologies");

            migrationBuilder.DropForeignKey(
                name: "FK_TrackTechnologies_Tracks_TrackId",
                table: "TrackTechnologies");

            migrationBuilder.DropPrimaryKey(
                name: "PK_RoadmapResources",
                table: "RoadmapResources");

            migrationBuilder.DropPrimaryKey(
                name: "PK_TrackTechnologies",
                table: "TrackTechnologies");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Tracks",
                table: "Tracks");

            migrationBuilder.DropPrimaryKey(
                name: "PK_TopicTechnologies",
                table: "TopicTechnologies");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Topics",
                table: "Topics");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Roadmaps",
                table: "Roadmaps");

            migrationBuilder.DropIndex(
                name: "IX_Roadmaps_ResourceId",
                table: "Roadmaps");

            migrationBuilder.DropColumn(
                name: "ResourceId",
                table: "Roadmaps");

            migrationBuilder.RenameTable(
                name: "TrackTechnologies",
                newName: "TrackTechnology");

            migrationBuilder.RenameTable(
                name: "Tracks",
                newName: "Track");

            migrationBuilder.RenameTable(
                name: "TopicTechnologies",
                newName: "TopicTechnology");

            migrationBuilder.RenameTable(
                name: "Topics",
                newName: "Topic");

            migrationBuilder.RenameTable(
                name: "Roadmaps",
                newName: "Roadmap");

            migrationBuilder.RenameIndex(
                name: "IX_TrackTechnologies_TrackId",
                table: "TrackTechnology",
                newName: "IX_TrackTechnology_TrackId");

            migrationBuilder.RenameIndex(
                name: "IX_Tracks_DomainFieldId",
                table: "Track",
                newName: "IX_Track_DomainFieldId");

            migrationBuilder.RenameIndex(
                name: "IX_TopicTechnologies_TopicId",
                table: "TopicTechnology",
                newName: "IX_TopicTechnology_TopicId");

            migrationBuilder.RenameIndex(
                name: "IX_Topics_TrackId",
                table: "Topic",
                newName: "IX_Topic_TrackId");

            migrationBuilder.RenameIndex(
                name: "IX_Roadmaps_CustomerApplicationUserId",
                table: "Roadmap",
                newName: "IX_Roadmap_CustomerApplicationUserId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_RoadmapResources",
                table: "RoadmapResources",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_TrackTechnology",
                table: "TrackTechnology",
                columns: new[] { "TechnologyId", "TrackId" });

            migrationBuilder.AddPrimaryKey(
                name: "PK_Track",
                table: "Track",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_TopicTechnology",
                table: "TopicTechnology",
                columns: new[] { "TechnologyId", "TopicId" });

            migrationBuilder.AddPrimaryKey(
                name: "PK_Topic",
                table: "Topic",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Roadmap",
                table: "Roadmap",
                column: "Id");

            migrationBuilder.CreateIndex(
                name: "IX_RoadmapResources_ResourceId",
                table: "RoadmapResources",
                column: "ResourceId");

            migrationBuilder.AddForeignKey(
                name: "FK_Roadmap_Customers_CustomerApplicationUserId",
                table: "Roadmap",
                column: "CustomerApplicationUserId",
                principalTable: "Customers",
                principalColumn: "ApplicationUserId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_RoadmapResources_Roadmap_RoadmapId",
                table: "RoadmapResources",
                column: "RoadmapId",
                principalTable: "Roadmap",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Topic_Track_TrackId",
                table: "Topic",
                column: "TrackId",
                principalTable: "Track",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_TopicTechnology_Technology_TechnologyId",
                table: "TopicTechnology",
                column: "TechnologyId",
                principalTable: "Technology",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_TopicTechnology_Topic_TopicId",
                table: "TopicTechnology",
                column: "TopicId",
                principalTable: "Topic",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Track_Domains_DomainFieldId",
                table: "Track",
                column: "DomainFieldId",
                principalTable: "Domains",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_TrackTechnology_Technology_TechnologyId",
                table: "TrackTechnology",
                column: "TechnologyId",
                principalTable: "Technology",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_TrackTechnology_Track_TrackId",
                table: "TrackTechnology",
                column: "TrackId",
                principalTable: "Track",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
