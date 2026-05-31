using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AIChatAssistant.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddUserChatSettingsConfig : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "UserChatSettings",
                columns: table => new
                {
                    UserId = table.Column<int>(type: "int", nullable: false),
                    CustomSystemPrompt = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserChatSettings", x => x.UserId);
                    table.ForeignKey(
                        name: "FK_UserChatSettings_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserChatSettings");
        }
    }
}
