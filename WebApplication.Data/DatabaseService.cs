using System.Data;
using System.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace WebApplication.Data
{
    public interface IDatabaseService
    {
        Task<IDbConnection> GetConnectionAsync();
        Task<T> QuerySingleAsync<T>(string sql, object? parameters = null);
        Task<IEnumerable<T>> QueryAsync<T>(string sql, object? parameters = null);
        Task<int> ExecuteAsync(string sql, object? parameters = null);
    }

    public class DatabaseService : IDatabaseService
    {
        private readonly string _connectionString;

        public DatabaseService(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection") 
                ?? throw new ArgumentException("Connection string 'DefaultConnection' not found.");
        }

        public async Task<IDbConnection> GetConnectionAsync()
        {
            var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();
            return connection;
        }

        public async Task<T> QuerySingleAsync<T>(string sql, object? parameters = null)
        {
            using var connection = (SqlConnection)await GetConnectionAsync();
            using var command = new SqlCommand(sql, connection);

            if (parameters != null)
            {
                AddParameters(command, parameters);
            }

            var result = await command.ExecuteScalarAsync();
            return (T)Convert.ChangeType(result!, typeof(T));
        }

        public async Task<IEnumerable<T>> QueryAsync<T>(string sql, object? parameters = null)
        {
            using var connection = (SqlConnection)await GetConnectionAsync();
            using var command = new SqlCommand(sql, connection);

            if (parameters != null)
            {
                AddParameters(command, parameters);
            }

            using var reader = await command.ExecuteReaderAsync();
            var results = new List<T>();

            while (await reader.ReadAsync())
            {
                var instance = Activator.CreateInstance<T>();
                var properties = typeof(T).GetProperties();

                foreach (var property in properties)
                {
                    if (reader.HasColumn(property.Name) && !reader.IsDBNull(property.Name))
                    {
                        var value = reader[property.Name];
                        property.SetValue(instance, Convert.ChangeType(value, property.PropertyType));
                    }
                }

                results.Add(instance);
            }

            return results;
        }

        public async Task<int> ExecuteAsync(string sql, object? parameters = null)
        {
            using var connection = (SqlConnection)await GetConnectionAsync();
            using var command = new SqlCommand(sql, connection);

            if (parameters != null)
            {
                AddParameters(command, parameters);
            }

            return await command.ExecuteNonQueryAsync();
        }

        private void AddParameters(SqlCommand command, object parameters)
        {
            var properties = parameters.GetType().GetProperties();
            foreach (var property in properties)
            {
                var parameter = command.CreateParameter();
                parameter.ParameterName = $"@{property.Name}";
                parameter.Value = property.GetValue(parameters) ?? DBNull.Value;
                command.Parameters.Add(parameter);
            }
        }
    }

    public static class DataReaderExtensions
    {
        public static bool HasColumn(this IDataReader reader, string columnName)
        {
            for (int i = 0; i < reader.FieldCount; i++)
            {
                if (reader.GetName(i).Equals(columnName, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }
    }
} 