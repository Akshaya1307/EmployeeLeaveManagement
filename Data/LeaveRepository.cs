using EmployeeLeaveManagement.Models;
using EmployeeLeaveManagement.Repositories;
using Microsoft.Data.SqlClient;
using System.Data;
using System.Linq;

namespace EmployeeLeaveManagement.Data
{
    public class LeaveRepository : ILeaveRepository
    {
        private readonly DatabaseHelper _dbHelper;

        public LeaveRepository(DatabaseHelper dbHelper)
        {
            _dbHelper = dbHelper;
        }

        // ======================================================
        // Leave Categories
        // ======================================================
        public async Task<List<LeaveCategory>> GetLeaveCategoriesAsync()
        {
            var list = new List<LeaveCategory>();

            var dt = await _dbHelper.ExecuteQueryAsync(
                "SELECT * FROM LeaveCategories WHERE IsActive = 1 ORDER BY CategoryName");

            foreach (DataRow row in dt.Rows)
            {
                list.Add(new LeaveCategory
                {
                    CategoryID = Convert.ToInt32(row["CategoryID"]),
                    CategoryName = row["CategoryName"]?.ToString() ?? "",
                    Description = row["Description"]?.ToString() ?? "",
                    DefaultDays = Convert.ToInt32(row["DefaultDays"]),
                    IsActive = Convert.ToBoolean(row["IsActive"])
                });
            }

            return list;
        }

        // ======================================================
        // Leave Balance
        // ======================================================
        public async Task<LeaveBalance?> GetLeaveBalanceAsync(
            int employeeId,
            int categoryId,
            int year)
        {
            var query = @"
                SELECT 
                    lb.*,
                    lc.CategoryName
                FROM LeaveBalances lb
                INNER JOIN LeaveCategories lc
                    ON lb.CategoryID = lc.CategoryID
                WHERE lb.EmployeeID = @Emp
                  AND lb.CategoryID = @Cat
                  AND lb.Year = @Year";

            var p = new[]
            {
                new SqlParameter("@Emp", employeeId),
                new SqlParameter("@Cat", categoryId),
                new SqlParameter("@Year", year)
            };

            var dt = await _dbHelper.ExecuteQueryAsync(query, p);

            if (dt.Rows.Count == 0)
                return null;

            var row = dt.Rows[0];

            return new LeaveBalance
            {
                BalanceID = Convert.ToInt32(row["BalanceID"]),
                EmployeeID = Convert.ToInt32(row["EmployeeID"]),
                CategoryID = Convert.ToInt32(row["CategoryID"]),
                TotalDays = Convert.ToInt32(row["TotalDays"]),
                UsedDays = Convert.ToInt32(row["UsedDays"]),
                RemainingDays = Convert.ToInt32(row["RemainingDays"]),
                Year = Convert.ToInt32(row["Year"]),
                CategoryName = row["CategoryName"]?.ToString() ?? ""
            };
        }

        // ======================================================
        // Create Leave Request
        // ======================================================
        public async Task<bool> CreateLeaveRequestAsync(LeaveRequest request)
        {
            var query = @"
                INSERT INTO LeaveRequests
                (
                    EmployeeID,
                    CategoryID,
                    StartDate,
                    EndDate,
                    TotalDays,
                    Reason,
                    Status
                )
                VALUES
                (
                    @Emp,
                    @Cat,
                    @Start,
                    @End,
                    @Days,
                    @Reason,
                    'Pending'
                )";

            var p = new[]
            {
                new SqlParameter("@Emp", request.EmployeeID),
                new SqlParameter("@Cat", request.CategoryID),
                new SqlParameter("@Start", request.StartDate),
                new SqlParameter("@End", request.EndDate),
                new SqlParameter("@Days", request.TotalDays),
                new SqlParameter("@Reason", request.Reason ?? "")
            };

            return await _dbHelper.ExecuteNonQueryAsync(query, p) > 0;
        }

        // ======================================================
        // Employee Leave History
        // ======================================================
        public async Task<List<LeaveRequest>> GetLeaveRequestsByEmployeeAsync(
            int employeeId)
        {
            var query = @"
                SELECT 
                    lr.*,
                    lc.CategoryName,
                    e.FirstName + ' ' + e.LastName AS EmployeeName,
                    ISNULL(r.FirstName + ' ' + r.LastName, '') AS ReviewerName
                FROM LeaveRequests lr
                INNER JOIN LeaveCategories lc
                    ON lr.CategoryID = lc.CategoryID
                INNER JOIN Employees e
                    ON lr.EmployeeID = e.EmployeeID
                LEFT JOIN Employees r
                    ON lr.ReviewedBy = r.EmployeeID
                WHERE lr.EmployeeID = @Emp
                ORDER BY lr.AppliedDate DESC";

            var p = new[]
            {
                new SqlParameter("@Emp", employeeId)
            };

            var dt = await _dbHelper.ExecuteQueryAsync(query, p);

            return MapToLeaveRequests(dt);
        }

        // ======================================================
        // Pending Requests for Normal Admin
        // Only employees directly reporting to that Admin
        // ======================================================
        public async Task<List<LeaveRequest>>
            GetPendingLeaveRequestsForManagerAsync(int managerId)
        {
            var query = @"
                SELECT 
                    lr.*,
                    lc.CategoryName,
                    e.FirstName + ' ' + e.LastName AS EmployeeName,
                    ISNULL(r.FirstName + ' ' + r.LastName, '') AS ReviewerName
                FROM LeaveRequests lr
                INNER JOIN LeaveCategories lc
                    ON lr.CategoryID = lc.CategoryID
                INNER JOIN Employees e
                    ON lr.EmployeeID = e.EmployeeID
                LEFT JOIN Employees r
                    ON lr.ReviewedBy = r.EmployeeID
                WHERE lr.Status = 'Pending'
                  AND e.ManagerID = @Mgr
                ORDER BY lr.AppliedDate";

            var p = new[]
            {
                new SqlParameter("@Mgr", managerId)
            };

            var dt = await _dbHelper.ExecuteQueryAsync(query, p);

            return MapToLeaveRequests(dt);
        }

        // ======================================================
        // Pending Requests for Master Admin
        // Master Admin sees EVERYONE
        // ======================================================
        public async Task<List<LeaveRequest>>
            GetAllPendingLeaveRequestsAsync()
        {
            var query = @"
                SELECT 
                    lr.*,
                    lc.CategoryName,
                    e.FirstName + ' ' + e.LastName AS EmployeeName,
                    ISNULL(r.FirstName + ' ' + r.LastName, '') AS ReviewerName
                FROM LeaveRequests lr
                INNER JOIN LeaveCategories lc
                    ON lr.CategoryID = lc.CategoryID
                INNER JOIN Employees e
                    ON lr.EmployeeID = e.EmployeeID
                LEFT JOIN Employees r
                    ON lr.ReviewedBy = r.EmployeeID
                WHERE lr.Status = 'Pending'
                ORDER BY lr.AppliedDate";

            var dt = await _dbHelper.ExecuteQueryAsync(query);

            return MapToLeaveRequests(dt);
        }

        // ======================================================
        // Review Leave Request
        //
        // MasterAdmin:
        //      Can review anyone
        //
        // Admin:
        //      Can review only employees reporting to them
        //
        // Admin cannot review own leave
        //
        // APPROVAL + BALANCE UPDATE ARE TRANSACTION-SAFE
        // ======================================================
        public async Task<bool> ReviewLeaveRequestAsync(
            int requestId,
            int reviewerId,
            string status,
            string comments)
        {
            // --------------------------------------------------
            // Validate status
            // --------------------------------------------------
            if (status != "Approved" && status != "Rejected")
            {
                return false;
            }

            // --------------------------------------------------
            // Get reviewer information
            // --------------------------------------------------
            var reviewerQuery = @"
                SELECT EmployeeID, Role
                FROM Employees
                WHERE EmployeeID = @ReviewerId
                  AND IsActive = 1";

            var reviewerTable = await _dbHelper.ExecuteQueryAsync(
                reviewerQuery,
                new[]
                {
                    new SqlParameter("@ReviewerId", reviewerId)
                });

            if (reviewerTable.Rows.Count == 0)
            {
                return false;
            }

            var reviewerRole =
                reviewerTable.Rows[0]["Role"]?.ToString() ?? "";

            // --------------------------------------------------
            // Get leave request + employee ManagerID
            // --------------------------------------------------
            var requestQuery = @"
                SELECT
                    lr.RequestID,
                    lr.EmployeeID,
                    lr.CategoryID,
                    lr.TotalDays,
                    lr.Status,
                    e.ManagerID
                FROM LeaveRequests lr
                INNER JOIN Employees e
                    ON lr.EmployeeID = e.EmployeeID
                WHERE lr.RequestID = @RequestId";

            var requestTable = await _dbHelper.ExecuteQueryAsync(
                requestQuery,
                new[]
                {
                    new SqlParameter("@RequestId", requestId)
                });

            if (requestTable.Rows.Count == 0)
            {
                return false;
            }

            var requestRow = requestTable.Rows[0];

            var requestEmployeeId =
                Convert.ToInt32(requestRow["EmployeeID"]);

            var requestCategoryId =
                Convert.ToInt32(requestRow["CategoryID"]);

            var requestTotalDays =
                Convert.ToInt32(requestRow["TotalDays"]);

            var currentStatus =
                requestRow["Status"]?.ToString() ?? "";

            int? managerId =
                requestRow["ManagerID"] == DBNull.Value
                    ? null
                    : Convert.ToInt32(requestRow["ManagerID"]);

            // --------------------------------------------------
            // Only Pending requests can be reviewed
            // --------------------------------------------------
            if (currentStatus != "Pending")
            {
                return false;
            }

            // --------------------------------------------------
            // Prevent Admin from reviewing own leave
            // --------------------------------------------------
            if (requestEmployeeId == reviewerId &&
                reviewerRole == "Admin")
            {
                return false;
            }

            // --------------------------------------------------
            // Normal Admin:
            // Must be the employee's direct manager
            // --------------------------------------------------
            if (reviewerRole == "Admin")
            {
                if (!managerId.HasValue ||
                    managerId.Value != reviewerId)
                {
                    return false;
                }
            }

            // --------------------------------------------------
            // Only MasterAdmin and Admin can review
            // --------------------------------------------------
            if (reviewerRole != "Admin" &&
                reviewerRole != "MasterAdmin")
            {
                return false;
            }

            // ==================================================
            // REJECTED
            //
            // No leave balance is changed.
            // ==================================================
            if (status == "Rejected")
            {
                try
                {
                    var reviewDate = DateTime.Now;

                    var rejectQuery = @"
                        UPDATE LeaveRequests
                        SET
                            Status = 'Rejected',
                            ReviewedBy = @ReviewerId,
                            ReviewedDate = @ReviewDate,
                            Comments = @Comments,
                            ApprovedBy = NULL,
                            ApprovedDate = NULL
                        WHERE RequestID = @RequestId
                          AND Status = 'Pending'";

                    var rejectParams = new[]
                    {
                        new SqlParameter("@ReviewerId", reviewerId),
                        new SqlParameter("@ReviewDate", reviewDate),
                        new SqlParameter("@Comments", comments ?? ""),
                        new SqlParameter("@RequestId", requestId)
                    };

                    var rowsAffected =
                        await _dbHelper.ExecuteNonQueryAsync(
                            rejectQuery,
                            rejectParams);

                    return rowsAffected > 0;
                }
                catch
                {
                    return false;
                }
            }

            // ==================================================
            // APPROVED
            //
            // Leave request update + balance deduction happen
            // inside ONE SQL transaction.
            // ==================================================
            try
            {
                var reviewDate = DateTime.Now;

                await _dbHelper.ExecuteTransactionAsync(
                    async (connection, transaction) =>
                    {
                        // --------------------------------------------------
                        // Step 1:
                        // Approve the leave request
                        // --------------------------------------------------
                        var approveRequestQuery = @"
                            UPDATE LeaveRequests
                            SET
                                Status = 'Approved',
                                ReviewedBy = @ReviewerId,
                                ReviewedDate = @ReviewDate,
                                Comments = @Comments,
                                ApprovedBy = @ReviewerId,
                                ApprovedDate = @ApprovedDate
                            WHERE RequestID = @RequestId
                              AND Status = 'Pending'";

                        var approveRequestParams = new[]
                        {
                            new SqlParameter(
                                "@ReviewerId",
                                reviewerId),

                            new SqlParameter(
                                "@ReviewDate",
                                reviewDate),

                            new SqlParameter(
                                "@Comments",
                                comments ?? ""),

                            new SqlParameter(
                                "@ApprovedDate",
                                reviewDate),

                            new SqlParameter(
                                "@RequestId",
                                requestId)
                        };

                        var requestRows =
                            await _dbHelper.ExecuteNonQueryAsync(
                                connection,
                                transaction,
                                approveRequestQuery,
                                approveRequestParams);

                        // If request was not updated,
                        // rollback the entire transaction.
                        if (requestRows <= 0)
                        {
                            throw new InvalidOperationException(
                                "Leave request could not be approved.");
                        }

                        // --------------------------------------------------
                        // Step 2:
                        // Deduct leave balance
                        //
                        // IMPORTANT:
                        // RemainingDays is a COMPUTED COLUMN.
                        // We update ONLY UsedDays.
                        // --------------------------------------------------
                        var updateBalanceQuery = @"
                            UPDATE LeaveBalances
                            SET
                                UsedDays =
                                    COALESCE(UsedDays, 0)
                                    + @UsedDays
                            WHERE EmployeeID = @EmployeeID
                              AND CategoryID = @CategoryID
                              AND Year = @Year
                              AND RemainingDays >= @UsedDays";

                        var balanceParams = new[]
                        {
                            new SqlParameter(
                                "@UsedDays",
                                requestTotalDays),

                            new SqlParameter(
                                "@EmployeeID",
                                requestEmployeeId),

                            new SqlParameter(
                                "@CategoryID",
                                requestCategoryId),

                            new SqlParameter(
                                "@Year",
                                DateTime.Now.Year)
                        };

                        var balanceRows =
                            await _dbHelper.ExecuteNonQueryAsync(
                                connection,
                                transaction,
                                updateBalanceQuery,
                                balanceParams);

                        // If balance wasn't updated,
                        // rollback the approval too.
                        if (balanceRows <= 0)
                        {
                            throw new InvalidOperationException(
                                "Leave balance could not be updated.");
                        }
                    });

                // If we reach here, transaction committed successfully.
                return true;
            }
            catch
            {
                // DatabaseHelper already rolls back the transaction.
                return false;
            }
        }

        // ======================================================
        // Get Leave Request By ID
        // ======================================================
        private async Task<LeaveRequest?> GetLeaveRequestByIdAsync(
            int requestId)
        {
            var query = @"
                SELECT *
                FROM LeaveRequests
                WHERE RequestID = @Id";

            var dt = await _dbHelper.ExecuteQueryAsync(
                query,
                new[]
                {
                    new SqlParameter("@Id", requestId)
                });

            if (dt.Rows.Count == 0)
                return null;

            var row = dt.Rows[0];

            return new LeaveRequest
            {
                RequestID =
                    Convert.ToInt32(row["RequestID"]),

                EmployeeID =
                    Convert.ToInt32(row["EmployeeID"]),

                CategoryID =
                    Convert.ToInt32(row["CategoryID"]),

                StartDate =
                    Convert.ToDateTime(row["StartDate"]),

                EndDate =
                    Convert.ToDateTime(row["EndDate"]),

                TotalDays =
                    Convert.ToInt32(row["TotalDays"]),

                Reason =
                    row["Reason"]?.ToString() ?? "",

                Status =
                    row["Status"]?.ToString() ?? "Pending",

                AppliedDate =
                    Convert.ToDateTime(row["AppliedDate"]),

                ReviewedBy =
                    row["ReviewedBy"] == DBNull.Value
                        ? null
                        : Convert.ToInt32(row["ReviewedBy"]),

                ReviewedDate =
                    row["ReviewedDate"] == DBNull.Value
                        ? null
                        : Convert.ToDateTime(row["ReviewedDate"]),

                ApprovedBy =
                    row["ApprovedBy"] == DBNull.Value
                        ? null
                        : Convert.ToInt32(row["ApprovedBy"]),

                ApprovedDate =
                    row["ApprovedDate"] == DBNull.Value
                        ? null
                        : Convert.ToDateTime(row["ApprovedDate"]),

                Comments =
                    row["Comments"]?.ToString() ?? ""
            };
        }

        // ======================================================
        // Map DataTable → LeaveRequest List
        // ======================================================
        private List<LeaveRequest> MapToLeaveRequests(
            DataTable dt)
        {
            var list = new List<LeaveRequest>();

            foreach (DataRow row in dt.Rows)
            {
                list.Add(new LeaveRequest
                {
                    RequestID =
                        Convert.ToInt32(row["RequestID"]),

                    EmployeeID =
                        Convert.ToInt32(row["EmployeeID"]),

                    CategoryID =
                        Convert.ToInt32(row["CategoryID"]),

                    StartDate =
                        Convert.ToDateTime(row["StartDate"]),

                    EndDate =
                        Convert.ToDateTime(row["EndDate"]),

                    TotalDays =
                        Convert.ToInt32(row["TotalDays"]),

                    Reason =
                        row["Reason"]?.ToString() ?? "",

                    Status =
                        row["Status"]?.ToString() ?? "Pending",

                    AppliedDate =
                        Convert.ToDateTime(row["AppliedDate"]),

                    ReviewedBy =
                        row["ReviewedBy"] == DBNull.Value
                            ? null
                            : Convert.ToInt32(row["ReviewedBy"]),

                    ReviewedDate =
                        row["ReviewedDate"] == DBNull.Value
                            ? null
                            : Convert.ToDateTime(row["ReviewedDate"]),

                    ApprovedBy =
                        row["ApprovedBy"] == DBNull.Value
                            ? null
                            : Convert.ToInt32(row["ApprovedBy"]),

                    ApprovedDate =
                        row["ApprovedDate"] == DBNull.Value
                            ? null
                            : Convert.ToDateTime(row["ApprovedDate"]),

                    Comments =
                        row["Comments"]?.ToString() ?? "",

                    EmployeeName =
                        row["EmployeeName"]?.ToString() ?? "",

                    CategoryName =
                        row["CategoryName"]?.ToString() ?? "",

                    ReviewerName =
                        row["ReviewerName"]?.ToString() ?? ""
                });
            }

            return list;
        }

        // ======================================================
        // Dashboard
        // ======================================================
        public async Task<DashboardViewModel>
            GetDashboardDataAsync(
                int employeeId,
                string role)
        {
            var dashboard =
                new DashboardViewModel();

            // ==================================================
            // MASTER ADMIN DASHBOARD
            // Organization-wide view
            //
            // Master Admin:
            // - Can see all active employees
            // - Can see all active Admins
            // - Can see leave balances of Admins + Employees
            // - Can see total pending requests across organization
            // - Does NOT use employee/admin leave-request widgets
            // ==================================================
            if (role == "MasterAdmin")
            {
                // --------------------------------------------------
                // Total active employees
                // --------------------------------------------------
                dashboard.TotalEmployees =
                    Convert.ToInt32(
                        await _dbHelper.ExecuteScalarAsync(
                            @"SELECT COUNT(*)
                              FROM Employees
                              WHERE Role = 'Employee'
                              AND IsActive = 1") ?? 0);

                // --------------------------------------------------
                // Total active admins
                // --------------------------------------------------
                dashboard.TotalAdmins =
                    Convert.ToInt32(
                        await _dbHelper.ExecuteScalarAsync(
                            @"SELECT COUNT(*)
                              FROM Employees
                              WHERE Role = 'Admin'
                              AND IsActive = 1") ?? 0);

                // --------------------------------------------------
                // ✅ ADDED: Total pending requests across organization
                // Master Admin sees ALL pending requests
                // --------------------------------------------------
                dashboard.PendingRequests =
                    Convert.ToInt32(
                        await _dbHelper.ExecuteScalarAsync(
                            @"SELECT COUNT(*)
                              FROM LeaveRequests
                              WHERE Status = 'Pending'") ?? 0);

                // --------------------------------------------------
                // Master Admin should see leave balances for all
                // active Admins and Employees.
                //
                // MasterAdmin itself is excluded because the
                // MasterAdmin account does not have leave balances.
                // --------------------------------------------------
                var employeeBalancesQuery = @"
                    SELECT
                        e.EmployeeID,
                        e.EmployeeCode,
                        e.FirstName + ' ' + e.LastName AS EmployeeName,
                        e.Role,
                        lb.BalanceID,
                        lb.CategoryID,
                        lb.TotalDays,
                        lb.UsedDays,
                        lb.RemainingDays,
                        lb.Year,
                        lc.CategoryName
                    FROM Employees e
                    LEFT JOIN LeaveBalances lb
                        ON e.EmployeeID = lb.EmployeeID
                        AND lb.Year = @Year
                    LEFT JOIN LeaveCategories lc
                        ON lb.CategoryID = lc.CategoryID
                    WHERE e.IsActive = 1
                      AND e.Role <> 'MasterAdmin'
                    ORDER BY
                        CASE
                            WHEN e.Role = 'Admin' THEN 1
                            ELSE 2
                        END,
                        e.FirstName,
                        e.LastName,
                        lc.CategoryName";

                var employeeBalancesTable =
                    await _dbHelper.ExecuteQueryAsync(
                        employeeBalancesQuery,
                        new[]
                        {
                            new SqlParameter(
                                "@Year",
                                DateTime.Now.Year)
                        });

                var employeeBalanceDictionary =
                    new Dictionary<int, EmployeeLeaveBalanceViewModel>();

                foreach (DataRow row in employeeBalancesTable.Rows)
                {
                    var currentEmployeeId =
                        Convert.ToInt32(row["EmployeeID"]);

                    if (!employeeBalanceDictionary.ContainsKey(
                        currentEmployeeId))
                    {
                        employeeBalanceDictionary[currentEmployeeId] =
                            new EmployeeLeaveBalanceViewModel
                            {
                                EmployeeId = currentEmployeeId,

                                EmployeeCode =
                                    row["EmployeeCode"]?.ToString()
                                    ?? "",

                                EmployeeName =
                                    row["EmployeeName"]?.ToString()
                                    ?? "",

                                Role =
                                    row["Role"]?.ToString()
                                    ?? ""
                            };
                    }

                    // LEFT JOIN means an active Admin/Employee can
                    // still appear even when no balance row exists.
                    if (row["BalanceID"] != DBNull.Value)
                    {
                        employeeBalanceDictionary[
                            currentEmployeeId].Balances.Add(
                            new LeaveBalance
                            {
                                BalanceID =
                                    Convert.ToInt32(
                                        row["BalanceID"]),

                                EmployeeID =
                                    currentEmployeeId,

                                CategoryID =
                                    Convert.ToInt32(
                                        row["CategoryID"]),

                                TotalDays =
                                    Convert.ToInt32(
                                        row["TotalDays"]),

                                UsedDays =
                                    Convert.ToInt32(
                                        row["UsedDays"]),

                                RemainingDays =
                                    Convert.ToInt32(
                                        row["RemainingDays"]),

                                Year =
                                    Convert.ToInt32(
                                        row["Year"]),

                                CategoryName =
                                    row["CategoryName"]?.ToString()
                                    ?? ""
                            });
                    }
                }

                dashboard.EmployeeLeaveBalances =
                    employeeBalanceDictionary.Values.ToList();

                // --------------------------------------------------
                // Master Admin does not use the normal
                // "My Pending Requests" / recent-request section.
                // Keep these collections empty so the Master Admin
                // view can render its own organization-wide layout.
                // --------------------------------------------------
                dashboard.RecentRequests =
                    new List<LeaveRequest>();

                dashboard.MyPendingRequests =
                    new List<LeaveRequest>();

                return dashboard;
            }

            // ==================================================
            // NORMAL ADMIN DASHBOARD
            // Only direct reporting employees
            // ==================================================
            if (role == "Admin")
            {
                dashboard.TotalEmployees =
                    Convert.ToInt32(
                        await _dbHelper.ExecuteScalarAsync(
                            @"SELECT COUNT(*)
                              FROM Employees
                              WHERE ManagerID = @Mgr
                              AND IsActive = 1",
                            new[]
                            {
                                new SqlParameter(
                                    "@Mgr",
                                    employeeId)
                            }) ?? 0);

                dashboard.PendingRequests =
                    Convert.ToInt32(
                        await _dbHelper.ExecuteScalarAsync(
                            @"SELECT COUNT(*)
                              FROM LeaveRequests lr
                              INNER JOIN Employees e
                                  ON lr.EmployeeID = e.EmployeeID
                              WHERE e.ManagerID = @Mgr
                              AND lr.Status = 'Pending'",
                            new[]
                            {
                                new SqlParameter(
                                    "@Mgr",
                                    employeeId)
                            }) ?? 0);

                dashboard.ApprovedRequests =
                    Convert.ToInt32(
                        await _dbHelper.ExecuteScalarAsync(
                            @"SELECT COUNT(*)
                              FROM LeaveRequests lr
                              INNER JOIN Employees e
                                  ON lr.EmployeeID = e.EmployeeID
                              WHERE e.ManagerID = @Mgr
                              AND lr.Status = 'Approved'",
                            new[]
                            {
                                new SqlParameter(
                                    "@Mgr",
                                    employeeId)
                            }) ?? 0);

                dashboard.RejectedRequests =
                    Convert.ToInt32(
                        await _dbHelper.ExecuteScalarAsync(
                            @"SELECT COUNT(*)
                              FROM LeaveRequests lr
                              INNER JOIN Employees e
                                  ON lr.EmployeeID = e.EmployeeID
                              WHERE e.ManagerID = @Mgr
                              AND lr.Status = 'Rejected'",
                            new[]
                            {
                                new SqlParameter(
                                    "@Mgr",
                                    employeeId)
                            }) ?? 0);

                var recentQuery = @"
                    SELECT TOP 10
                        lr.*,
                        lc.CategoryName,
                        e.FirstName + ' ' + e.LastName AS EmployeeName,
                        ISNULL(
                            r.FirstName + ' ' + r.LastName,
                            ''
                        ) AS ReviewerName
                    FROM LeaveRequests lr
                    INNER JOIN Employees e
                        ON lr.EmployeeID = e.EmployeeID
                    INNER JOIN LeaveCategories lc
                        ON lr.CategoryID = lc.CategoryID
                    LEFT JOIN Employees r
                        ON lr.ReviewedBy = r.EmployeeID
                    WHERE e.ManagerID = @Mgr
                    ORDER BY lr.AppliedDate DESC";

                var recentRequestsTable =
                    await _dbHelper.ExecuteQueryAsync(
                        recentQuery,
                        new[]
                        {
                            new SqlParameter(
                                "@Mgr",
                                employeeId)
                        });

                dashboard.RecentRequests =
                    MapToLeaveRequests(
                        recentRequestsTable);

                return dashboard;
            }

            // ==================================================
            // EMPLOYEE DASHBOARD
            // ==================================================
            var myRequests =
                await GetLeaveRequestsByEmployeeAsync(
                    employeeId);

            dashboard.MyPendingRequests =
                myRequests
                    .Where(r => r.Status == "Pending")
                    .ToList();

            dashboard.PendingRequests =
                dashboard.MyPendingRequests.Count;

            dashboard.ApprovedRequests =
                myRequests.Count(
                    r => r.Status == "Approved");

            dashboard.RejectedRequests =
                myRequests.Count(
                    r => r.Status == "Rejected");

            var balancesQuery = @"
                SELECT
                    lb.*,
                    lc.CategoryName
                FROM LeaveBalances lb
                INNER JOIN LeaveCategories lc
                    ON lb.CategoryID = lc.CategoryID
                WHERE lb.EmployeeID = @Emp
                  AND lb.Year = @Year";

            var parameters = new[]
            {
                new SqlParameter(
                    "@Emp",
                    employeeId),

                new SqlParameter(
                    "@Year",
                    DateTime.Now.Year)
            };

            var balancesTable =
                await _dbHelper.ExecuteQueryAsync(
                    balancesQuery,
                    parameters);

            dashboard.LeaveBalances =
                new List<LeaveBalance>();

            foreach (DataRow row in balancesTable.Rows)
            {
                dashboard.LeaveBalances.Add(
                    new LeaveBalance
                    {
                        CategoryName =
                            row["CategoryName"]?.ToString()
                            ?? "",

                        TotalDays =
                            Convert.ToInt32(
                                row["TotalDays"]),

                        UsedDays =
                            Convert.ToInt32(
                                row["UsedDays"]),

                        RemainingDays =
                            Convert.ToInt32(
                                row["RemainingDays"])
                    });
            }

            return dashboard;
        }
    }
}