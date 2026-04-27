using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GreenSwampApp.Migrations
{
    /// <inheritdoc />
    public partial class AddReribbsAndAnswers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "answers_count",
                table: "posts",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "reribbs_count",
                table: "posts",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "answers_count",
                table: "posts");

            migrationBuilder.DropColumn(
                name: "reribbs_count",
                table: "posts");
        }
    }
}
