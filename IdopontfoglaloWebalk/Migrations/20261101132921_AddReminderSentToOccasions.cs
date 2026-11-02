using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IdopontfoglaloWebalk.Migrations
{
    /// <inheritdoc />
    public partial class AddReminderSentToOccasions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "reminder_sent",
                table: "Occasions",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "reminder_sent",
                table: "Occasions");
        }
    }
}
