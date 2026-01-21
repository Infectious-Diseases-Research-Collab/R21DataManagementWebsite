using System.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.SqlClient;

namespace R21DataManagementWebsite.Pages
{
    public class IndexModel : PageModel
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<IndexModel> _logger;

        public IndexModel(IConfiguration configuration, ILogger<IndexModel> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        public DataTable EnrolleeData { get; set; } = new DataTable();
        public string? ErrorMessage { get; set; }
        public Dictionary<string, int> DeviceCounts { get; set; } = new Dictionary<string, int>();
        public int TotalRecordCount { get; set; } = 0;

        public void OnGet()
        {
            string connectionString = _configuration.GetConnectionString("R21Database");

            // Basic validation to avoid trying to connect with the placeholder
            if (string.IsNullOrEmpty(connectionString) || connectionString.Contains("YOUR_SERVER_NAME"))
            {
                ErrorMessage = "Please configure the Database Connection String in appsettings.json. Replace 'YOUR_SERVER_NAME' with your actual SQL Server instance.";
                return;
            }

            try
            {
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    connection.Open();

                    // Get total record count
                    string countSql = "SELECT COUNT(*) FROM enrollee";
                    using (SqlCommand countCommand = new SqlCommand(countSql, connection))
                    {
                        TotalRecordCount = (int)countCommand.ExecuteScalar();
                    }

                    // Get counts per deviceid
                    string deviceCountSql = "SELECT deviceid, COUNT(*) FROM enrollee GROUP BY deviceid ORDER BY deviceid";
                    using (SqlCommand deviceCountCommand = new SqlCommand(deviceCountSql, connection))
                    {
                        using (SqlDataReader reader = deviceCountCommand.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                string deviceId = reader.IsDBNull(0) ? "Unknown" : reader.GetString(0);
                                int count = reader.GetInt32(1);
                                DeviceCounts.Add(deviceId, count);
                            }
                        }
                    }

                    // Get top 100 recent records for display
                    string top100Sql = "SELECT TOP 100 * FROM enrollee ORDER BY lastmod DESC";
                    using (SqlCommand command = new SqlCommand(top100Sql, connection))
                    {
                        using (SqlDataAdapter adapter = new SqlDataAdapter(command))
                        {
                            adapter.Fill(EnrolleeData);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching data from database.");
                ErrorMessage = $"Error connecting to database: {ex.Message}";
            }
        }
    }
}