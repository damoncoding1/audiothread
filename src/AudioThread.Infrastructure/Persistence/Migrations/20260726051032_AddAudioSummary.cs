using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AudioThread.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAudioSummary : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "summarized_at_utc",
                table: "audio_files",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "summary",
                table: "audio_files",
                type: "character varying(512)",
                maxLength: 512,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "summarized_at_utc",
                table: "audio_files");

            migrationBuilder.DropColumn(
                name: "summary",
                table: "audio_files");
        }
    }
}
