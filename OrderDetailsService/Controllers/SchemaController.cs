using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;

namespace OrderDetailsService.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class SchemaController : ControllerBase
    {
        private readonly IConfiguration _configuration;

        public SchemaController(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        [HttpGet("tables")]
        public IActionResult GetTables()
        {
            var connString = _configuration.GetConnectionString("DefaultConnection");

            try
            {
                using var connection = new SqliteConnection(connString);
                connection.Open();

                using var cmd = connection.CreateCommand();
                cmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table' ORDER BY name";

                var tables = new List<string>();
                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    tables.Add(reader.GetString(0));
                }

                return Ok(new
                {
                    databasePath = connString,
                    tables
                });
            }
            catch (Exception ex)
            {
                return Ok(new
                {
                    error = ex.Message
                });
            }
        }
    }
}
