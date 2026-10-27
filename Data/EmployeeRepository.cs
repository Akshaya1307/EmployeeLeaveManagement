using EmployeeLeaveManagement.Models;
using EmployeeLeaveManagement.Data;
using Microsoft.Data.SqlClient;
using System.Data;

namespace EmployeeLeaveManagement.Repositories
{
    public class EmployeeRepository : IEmployeeRepository
    {
        private readonly DatabaseHelper _dbHelper;

        public EmployeeRepository(DatabaseHelper dbHelper)
        {
            _dbHelper = dbHelper;
        }

        // ===========================
        // Login Authentication
        // ===========================
        public async Task<User?> AuthenticateAsync(string email, string password)
        {
            var query = @"
                SELECT *
                FROM Employees
                WHERE Email = @Email
                AND IsActive = 1";

            var parameters = new SqlParameter[]
            {
                new SqlParameter("@Email", email)
            };

            var dataTable = await _dbHelper.ExecuteQueryAsync(query, parameters);

            if (dataTable.Rows.Count > 0)
            {
                var row = dataTable.Rows[0];

                var storedPassword = row["PasswordHash"]?.ToString();

                // Temporary Plain Text Password Check
                if (storedPassword == password)
                {
                    return MapToUser(row);
                }
            }

            return null;
        }

        // ===========================
        // Get Employee By ID
        // ===========================
        public async Task<User?> GetEmployeeByIdAsync(int employeeId)
        {
            var query = @"
                SELECT *
                FROM Employees
                WHERE EmployeeID = @EmployeeID";

            var parameters = new SqlParameter[]
            {
                new SqlParameter("@EmployeeID", employeeId)
            };

            var table = await _dbHelper.ExecuteQueryAsync(query, parameters);

            if (table.Rows.Count > 0)
                return MapToUser(table.Rows[0]);

            return null;
        }

        // ===========================
        // Get All Employees
        // ===========================
        public async Task<List<User>> GetAllEmployeesAsync()
        {
            var employees = new List<User>();

            var query = @"
                SELECT
                    e.EmployeeID,
                    e.EmployeeCode,
                    e.FirstName,
                    e.LastName,
                    e.Email,
                    e.PasswordHash,
                    e.Role,
                    e.ManagerID,
                    e.JoinDate,
                    e.IsActive,
                    e.CreatedAt,

                    CASE
                        WHEN m.EmployeeID IS NULL THEN NULL
                        ELSE CONCAT(m.FirstName,' ',m.LastName,' (',m.EmployeeCode,')')
                    END AS ManagerName

                FROM Employees e

                LEFT JOIN Employees m
                    ON e.ManagerID = m.EmployeeID

                WHERE e.IsActive = 1

                ORDER BY e.EmployeeID";

            var table = await _dbHelper.ExecuteQueryAsync(query);

            foreach (DataRow row in table.Rows)
            {
                employees.Add(MapToUser(row));
            }

            return employees;
        }

        // ===========================
        // Get Employees Under Manager
        // ===========================
        public async Task<List<User>> GetEmployeesByManagerAsync(int managerId)
        {
            var employees = new List<User>();

            var query = @"
                SELECT *
                FROM Employees
                WHERE ManagerID=@ManagerID
                AND IsActive=1";

            var parameters = new SqlParameter[]
            {
                new SqlParameter("@ManagerID", managerId)
            };

            var table = await _dbHelper.ExecuteQueryAsync(query, parameters);

            foreach (DataRow row in table.Rows)
            {
                employees.Add(MapToUser(row));
            }

            return employees;
        }

        // ===========================
        // Get All Admins
        // ===========================
        public async Task<List<User>> GetAdminsAsync()
        {
            var admins = new List<User>();

            var query = @"
                SELECT *
                FROM Employees
                WHERE Role='Admin'
                AND IsActive=1
                ORDER BY FirstName,LastName";

            var table = await _dbHelper.ExecuteQueryAsync(query);

            foreach (DataRow row in table.Rows)
            {
                admins.Add(MapToUser(row));
            }

            return admins;
        }

        // ===========================
        // Get Master Admin
        // ===========================
        public async Task<User?> GetMasterAdminAsync()
        {
            var query = @"
                SELECT *
                FROM Employees
                WHERE Role='MasterAdmin'
                AND IsActive=1";

            var table = await _dbHelper.ExecuteQueryAsync(query);

            if (table.Rows.Count > 0)
                return MapToUser(table.Rows[0]);

            return null;
        }

        // ===========================
        // Create Employee
        // ===========================
        public async Task<bool> CreateEmployeeAsync(User employee, string password)
        {
            var query = @"
                INSERT INTO Employees
                (
                    EmployeeCode,
                    FirstName,
                    LastName,
                    Email,
                    PasswordHash,
                    Role,
                    ManagerID,
                    JoinDate,
                    IsActive,
                    CreatedAt
                )
                VALUES
                (
                    @EmployeeCode,
                    @FirstName,
                    @LastName,
                    @Email,
                    @PasswordHash,
                    @Role,
                    @ManagerID,
                    @JoinDate,
                    @IsActive,
                    @CreatedAt
                )";

            var parameters = new SqlParameter[]
            {
                new SqlParameter("@EmployeeCode", employee.EmployeeCode),
                new SqlParameter("@FirstName", employee.FirstName),
                new SqlParameter("@LastName", employee.LastName),
                new SqlParameter("@Email", employee.Email),
                new SqlParameter("@PasswordHash", password),
                new SqlParameter("@Role", employee.Role),
                new SqlParameter("@ManagerID", employee.ManagerID ?? (object)DBNull.Value),
                new SqlParameter("@JoinDate", employee.JoinDate),
                new SqlParameter("@IsActive", employee.IsActive),
                new SqlParameter("@CreatedAt", employee.CreatedAt)
            };

            return await _dbHelper.ExecuteNonQueryAsync(query, parameters) > 0;
        }

        // ===========================
        // Update Employee
        // ===========================
        public async Task<bool> UpdateEmployeeAsync(User employee)
        {
            var query = @"
                UPDATE Employees
                SET
                    FirstName=@FirstName,
                    LastName=@LastName,
                    Email=@Email,
                    Role=@Role,
                    ManagerID=@ManagerID,
                    JoinDate=@JoinDate,
                    IsActive=@IsActive
                WHERE EmployeeID=@EmployeeID";

            var parameters = new SqlParameter[]
            {
                new SqlParameter("@FirstName", employee.FirstName),
                new SqlParameter("@LastName", employee.LastName),
                new SqlParameter("@Email", employee.Email),
                new SqlParameter("@Role", employee.Role),
                new SqlParameter("@ManagerID", employee.ManagerID ?? (object)DBNull.Value),
                new SqlParameter("@JoinDate", employee.JoinDate),
                new SqlParameter("@IsActive", employee.IsActive),
                new SqlParameter("@EmployeeID", employee.EmployeeID)
            };

            return await _dbHelper.ExecuteNonQueryAsync(query, parameters) > 0;
        }

        // ===========================
        // Soft Delete Employee
        // ===========================
        public async Task<bool> DeleteEmployeeAsync(int employeeId)
        {
            var query = @"
                UPDATE Employees
                SET IsActive = 0
                WHERE EmployeeID=@EmployeeID";

            var parameters = new SqlParameter[]
            {
                new SqlParameter("@EmployeeID", employeeId)
            };

            return await _dbHelper.ExecuteNonQueryAsync(query, parameters) > 0;
        }

        // ===========================
        // Convert DataRow → User
        // ===========================
        private User MapToUser(DataRow row)
        {
            return new User
            {
                EmployeeID = Convert.ToInt32(row["EmployeeID"]),
                EmployeeCode = row["EmployeeCode"].ToString() ?? "",
                FirstName = row["FirstName"].ToString() ?? "",
                LastName = row["LastName"].ToString() ?? "",
                Email = row["Email"].ToString() ?? "",
                PasswordHash = row["PasswordHash"].ToString() ?? "",
                Role = row["Role"].ToString() ?? "",

                ManagerID = row["ManagerID"] == DBNull.Value
                    ? null
                    : Convert.ToInt32(row["ManagerID"]),

                ManagerName = row.Table.Columns.Contains("ManagerName")
                    ? row["ManagerName"]?.ToString()
                    : null,

                JoinDate = Convert.ToDateTime(row["JoinDate"]),
                IsActive = Convert.ToBoolean(row["IsActive"]),
                CreatedAt = Convert.ToDateTime(row["CreatedAt"])
            };
        }
    }
}