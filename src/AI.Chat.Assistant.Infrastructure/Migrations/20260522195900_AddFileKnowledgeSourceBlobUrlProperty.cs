using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AIChatAssistant.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddFileKnowledgeSourceBlobUrlProperty : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BlobUrl",
                table: "FileKnowledgeSources",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BlobUrl",
                table: "FileKnowledgeSources");
        }
    }
}
