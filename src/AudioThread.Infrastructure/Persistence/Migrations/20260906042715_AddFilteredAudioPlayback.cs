using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AudioThread.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddFilteredAudioPlayback : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "filtered_content_type",
                table: "audio_files",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "filtered_public_url",
                table: "audio_files",
                type: "character varying(2048)",
                maxLength: 2048,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "filtered_size_in_bytes",
                table: "audio_files",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "filtered_storage_path",
                table: "audio_files",
                type: "character varying(1024)",
                maxLength: 1024,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "filtered_stored_file_name",
                table: "audio_files",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "original_content_type",
                table: "audio_files",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "original_public_url",
                table: "audio_files",
                type: "character varying(2048)",
                maxLength: 2048,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "original_size_in_bytes",
                table: "audio_files",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "original_storage_path",
                table: "audio_files",
                type: "character varying(1024)",
                maxLength: 1024,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "original_stored_file_name",
                table: "audio_files",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_audio_files_filtered_storage_path",
                table: "audio_files",
                column: "filtered_storage_path",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_audio_files_original_storage_path",
                table: "audio_files",
                column: "original_storage_path",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_audio_files_filtered_storage_path",
                table: "audio_files");

            migrationBuilder.DropIndex(
                name: "IX_audio_files_original_storage_path",
                table: "audio_files");

            migrationBuilder.DropColumn(
                name: "filtered_content_type",
                table: "audio_files");

            migrationBuilder.DropColumn(
                name: "filtered_public_url",
                table: "audio_files");

            migrationBuilder.DropColumn(
                name: "filtered_size_in_bytes",
                table: "audio_files");

            migrationBuilder.DropColumn(
                name: "filtered_storage_path",
                table: "audio_files");

            migrationBuilder.DropColumn(
                name: "filtered_stored_file_name",
                table: "audio_files");

            migrationBuilder.DropColumn(
                name: "original_content_type",
                table: "audio_files");

            migrationBuilder.DropColumn(
                name: "original_public_url",
                table: "audio_files");

            migrationBuilder.DropColumn(
                name: "original_size_in_bytes",
                table: "audio_files");

            migrationBuilder.DropColumn(
                name: "original_storage_path",
                table: "audio_files");

            migrationBuilder.DropColumn(
                name: "original_stored_file_name",
                table: "audio_files");
        }
    }
}
