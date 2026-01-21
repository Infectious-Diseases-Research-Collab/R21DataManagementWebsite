using FluentFTP;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace R21DataManagementWebsite.Pages
{
    public class FtpFilesModel : PageModel
    {
        public string FtpHost { get; } = "0f7a55b.netsolhost.com";

        [BindProperty]
        public string FtpUsername { get; set; } = string.Empty;

        [BindProperty]
        public string FtpPassword { get; set; } = string.Empty;

        // Used to bind the filename for the download action
        [BindProperty]
        public string FileName { get; set; } = string.Empty;

        public List<FtpListItem> Files { get; set; } = new List<FtpListItem>();

        public string ErrorMessage { get; set; } = string.Empty;

        public void OnGet()
        {
        }

        public async Task OnPostListAsync()
        {
            if (string.IsNullOrWhiteSpace(FtpHost) || string.IsNullOrWhiteSpace(FtpUsername) || string.IsNullOrWhiteSpace(FtpPassword))
            {
                ErrorMessage = "Please enter Username, and Password.";
                return;
            }

            try
            {
                // Configured for slow/unstable connections
                var config = new FtpConfig 
                { 
                    ConnectTimeout = 60000, // 60 seconds to connect
                    ReadTimeout = 120000,   // 120 seconds for data read
                    RetryAttempts = 5,       // Increase retries for unstable links
                }; 
                using var client = new AsyncFtpClient(FtpHost, FtpUsername, FtpPassword, config: config);
                
                // AutoConnect handles determining encryption, etc.
                await client.AutoConnect();
                
                // Get listing of the 'data' directory as requested
                var items = await client.GetListing("/data");
                
                // Filter to only show files > 0 bytes, and sort by modified date descending
                Files = items.Where(i => i.Type == FtpObjectType.File && i.Size > 0)
                             .OrderByDescending(i => i.Modified)
                             .ToList();
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Connection failed: {ex.Message}";
            }
        }

        public async Task<IActionResult> OnPostDownloadAsync()
        {
            if (string.IsNullOrWhiteSpace(FileName))
            {
                ErrorMessage = "Invalid file name.";
                return Page();
            }

            try
            {
                var config = new FtpConfig 
                { 
                    ConnectTimeout = 60000, 
                    ReadTimeout = 300000, // 5 minutes for downloads on slow links
                    RetryAttempts = 5 
                };
                using var client = new AsyncFtpClient(FtpHost, FtpUsername, FtpPassword, config: config);
                
                await client.AutoConnect();

                var stream = new MemoryStream();
                // Download the file to the memory stream
                bool success = await client.DownloadStream(stream, FileName);

                if (success)
                {
                    stream.Position = 0; // Reset stream position to the beginning
                    // Use a generic name if FileName contains path, or just use the name
                    var downloadName = Path.GetFileName(FileName);
                    return File(stream, "application/zip", downloadName); // Assuming zip files
                }
                else
                {
                    ErrorMessage = $"Failed to download {FileName}.";
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Error during download: {ex.Message}";
            }

            return Page();
        }

        public async Task<IActionResult> OnPostDownloadCsvAsync()
        {
            if (string.IsNullOrWhiteSpace(FileName))
            {
                ErrorMessage = "Invalid file name.";
                return Page();
            }

            string tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(tempDir);

            try
            {
                var config = new FtpConfig
                {
                    ConnectTimeout = 60000,
                    ReadTimeout = 300000,
                    RetryAttempts = 5
                };
                using var client = new AsyncFtpClient(FtpHost, FtpUsername, FtpPassword, config: config);
                await client.AutoConnect();

                string localZipPath = Path.Combine(tempDir, "data.zip");
                var status = await client.DownloadFile(localZipPath, FileName);

                if (status != FtpStatus.Success)
                {
                    ErrorMessage = $"Failed to download {FileName} for processing. Status: {status}";
                    return Page();
                }

                // Extract Zip
                System.IO.Compression.ZipFile.ExtractToDirectory(localZipPath, tempDir);

                // Find .db file
                var dbFile = Directory.GetFiles(tempDir, "*.db").FirstOrDefault() 
                             ?? Directory.GetFiles(tempDir, "*.sqlite").FirstOrDefault();

                if (dbFile == null)
                {
                    ErrorMessage = "No SQLite database found in the zip file.";
                    return Page();
                }

                // Export to CSV
                var enrolleeCsv = ExportTableToCsv(dbFile, "enrollee");
                var formChangesCsv = ExportTableToCsv(dbFile, "formchanges");

                // Zip the CSVs
                var csvZipStream = new MemoryStream();
                using (var archive = new System.IO.Compression.ZipArchive(csvZipStream, ZipArchiveMode.Create, true))
                {
                    AddFileToZip(archive, "enrollee.csv", enrolleeCsv);
                    AddFileToZip(archive, "formchanges.csv", formChangesCsv);
                }

                csvZipStream.Position = 0;
                string downloadName = Path.GetFileNameWithoutExtension(FileName) + "_csvs.zip";
                return File(csvZipStream, "application/zip", downloadName);
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Error processing CSVs: {ex.Message}";
                return Page();
            }
            finally
            {
                // Ensure all SQLite locks are released
                SqliteConnection.ClearAllPools();
                
                if (Directory.Exists(tempDir))
                {
                    Directory.Delete(tempDir, true);
                }
            }
        }

        private string ExportTableToCsv(string dbPath, string tableName)
        {
            var sb = new StringBuilder();
            // Disable pooling to ensure the file lock is released immediately
            using (var connection = new SqliteConnection($"Data Source={dbPath};Pooling=False"))
            {
                connection.Open();
                var command = connection.CreateCommand();
                command.CommandText = $"SELECT * FROM {tableName}";
                
                using (var reader = command.ExecuteReader())
                {
                    // Header
                    for (int i = 0; i < reader.FieldCount; i++)
                    {
                        sb.Append(EscapeCsv(reader.GetName(i) ?? ""));
                        if (i < reader.FieldCount - 1) sb.Append(",");
                    }
                    sb.AppendLine();

                    // Rows
                    while (reader.Read())
                    {
                        for (int i = 0; i < reader.FieldCount; i++)
                        {
                            var val = reader.IsDBNull(i) ? "" : reader.GetValue(i).ToString() ?? "";
                            sb.Append(EscapeCsv(val));
                            if (i < reader.FieldCount - 1) sb.Append(",");
                        }
                        sb.AppendLine();
                    }
                }
            }
            return sb.ToString();
        }

        private string EscapeCsv(string field)
        {
            if (string.IsNullOrEmpty(field)) return "";
            if (field.Contains(",") || field.Contains("\"") || field.Contains("\n") || field.Contains("\r"))
            {
                return $"\"{field.Replace("\"", "\"\"")}\"";
            }
            return field;
        }

        private void AddFileToZip(ZipArchive archive, string fileName, string content)
        {
            var entry = archive.CreateEntry(fileName);
            using (var entryStream = entry.Open())
            using (var writer = new StreamWriter(entryStream))
            {
                writer.Write(content);
            }
        }
    }
}