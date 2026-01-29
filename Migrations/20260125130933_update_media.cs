using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace timelapse.Migrations
{
    /// <inheritdoc />
    public partial class update_media : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Extenstion",
                table: "Media",
                type: "TEXT",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Extenstion",
                table: "Media");
        }
    }
}
