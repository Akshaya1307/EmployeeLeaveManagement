using Microsoft.Extensions.Configuration;
using Microsoft.Data.SqlClient;
using System.Data;

namespace EmployeeLeaveManagement.Data
{
    public class DatabaseHelper
    {
        private readonly string _connectionString;

        public DatabaseHelper(IConfiguration configuration)
        {
            _connectionString =
                configuration.GetConnectionString("DefaultConnection");
        }

        // ======================================================
        // Get Connection
        // ======================================================
        public SqlConnection GetConnection()
        {
            return new SqlConnection(_connectionString);
        }

        // ======================================================
        // Execute SELECT Query
        // ======================================================
        public async Task<DataTable> ExecuteQueryAsync(
            string query,
            SqlParameter[] parameters = null)
        {
            using var connection = GetConnection();

            using var command =
                new SqlCommand(query, connection);

            if (parameters != null)
            {
                command.Parameters.AddRange(parameters);
            }

            using var adapter =
                new SqlDataAdapter(command);

            var dataTable = new DataTable();

            await Task.Run(() =>
                adapter.Fill(dataTable));

            return dataTable;
        }

        // ======================================================
        // Execute INSERT / UPDATE / DELETE
        // ======================================================
        public async Task<int> ExecuteNonQueryAsync(
            string query,
            SqlParameter[] parameters = null)
        {
            using var connection = GetConnection();

            using var command =
                new SqlCommand(query, connection);

            if (parameters != null)
            {
                command.Parameters.AddRange(parameters);
            }

            await connection.OpenAsync();

            return await command.ExecuteNonQueryAsync();
        }

        // ======================================================
        // Execute Scalar Query
        // ======================================================
        public async Task<object> ExecuteScalarAsync(
            string query,
            SqlParameter[] parameters = null)
        {
            using var connection = GetConnection();

            using var command =
                new SqlCommand(query, connection);

            if (parameters != null)
            {
                command.Parameters.AddRange(parameters);
            }

            await connection.OpenAsync();

            return await command.ExecuteScalarAsync();
        }

        // ======================================================
        // TRANSACTION SUPPORT
        //
        // Used when multiple database operations must succeed
        // together.
        //
        // Example:
        //     Approve Leave
        //          +
        //     Deduct Leave Balance
        //
        // If anything fails, EVERYTHING is rolled back.
        // ======================================================
        public async Task ExecuteTransactionAsync(
            Func<SqlConnection, SqlTransaction, Task> transactionAction)
        {
            using var connection = GetConnection();

            await connection.OpenAsync();

            using var transaction =
                (SqlTransaction)await connection.BeginTransactionAsync();

            try
            {
                await transactionAction(
                    connection,
                    transaction);

                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();

                throw;
            }
        }

        // ======================================================
        // Execute NonQuery inside an existing transaction
        // ======================================================
        public async Task<int> ExecuteNonQueryAsync(
            SqlConnection connection,
            SqlTransaction transaction,
            string query,
            SqlParameter[] parameters = null)
        {
            using var command =
                new SqlCommand(
                    query,
                    connection,
                    transaction);

            if (parameters != null)
            {
                command.Parameters.AddRange(parameters);
            }

            return await command.ExecuteNonQueryAsync();
        }

        // ======================================================
        // Execute Scalar inside an existing transaction
        // ======================================================
        public async Task<object> ExecuteScalarAsync(
            SqlConnection connection,
            SqlTransaction transaction,
            string query,
            SqlParameter[] parameters = null)
        {
            using var command =
                new SqlCommand(
                    query,
                    connection,
                    transaction);

            if (parameters != null)
            {
                command.Parameters.AddRange(parameters);
            }

            return await command.ExecuteScalarAsync();
        }

        // ======================================================
        // Execute SELECT inside an existing transaction
        // ======================================================
        public async Task<DataTable> ExecuteQueryAsync(
            SqlConnection connection,
            SqlTransaction transaction,
            string query,
            SqlParameter[] parameters = null)
        {
            using var command =
                new SqlCommand(
                    query,
                    connection,
                    transaction);

            if (parameters != null)
            {
                command.Parameters.AddRange(parameters);
            }

            using var reader =
                await command.ExecuteReaderAsync();

            var dataTable = new DataTable();

            dataTable.Load(reader);

            return dataTable;
        }
    }
}