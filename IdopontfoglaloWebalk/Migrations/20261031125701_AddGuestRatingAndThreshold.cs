using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IdopontfoglaloWebalk.Migrations
{
    /// <inheritdoc />
    public partial class AddGuestRatingAndThreshold : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "min_guest_rating",
                table: "Services",
                type: "double",
                nullable: false,
                defaultValue: 0.0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "min_guest_rating",
                table: "Services");
        }
    }
}
