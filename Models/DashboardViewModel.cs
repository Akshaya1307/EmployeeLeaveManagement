using System;
using System.Collections.Generic;

namespace EmployeeLeaveManagement.Models
{
    public class DashboardViewModel
    {
        // ===========================
        // Dashboard Statistics
        // ===========================

        public int TotalEmployees { get; set; }

        // Master Admin only
        public int TotalAdmins { get; set; }

        public int PendingRequests { get; set; }

        public int ApprovedRequests { get; set; }

        public int RejectedRequests { get; set; }


        // ===========================
        // Dashboard Lists
        // ===========================

        // Recent requests for Admin / Employee dashboards
        public List<LeaveRequest> RecentRequests { get; set; }
            = new List<LeaveRequest>();

        // Leave balances for the logged-in Employee/Admin
        public List<LeaveBalance> LeaveBalances { get; set; }
            = new List<LeaveBalance>();

        // Pending leave requests of the logged-in Employee
        public List<LeaveRequest> MyPendingRequests { get; set; }
            = new List<LeaveRequest>();


        // ===========================
        // MASTER ADMIN DASHBOARD
        // ===========================

        // Organization-wide leave balances.
        // Used ONLY by Master Admin.
        public List<EmployeeLeaveBalanceViewModel> EmployeeLeaveBalances { get; set; }
            = new List<EmployeeLeaveBalanceViewModel>();


        // ===========================
        // Statistics for Charts
        // ===========================

        public Dictionary<string, int> LeaveByCategory { get; set; }
            = new Dictionary<string, int>();

        public Dictionary<string, int> LeaveByMonth { get; set; }
            = new Dictionary<string, int>();
    }


    // ==========================================================
    // Master Admin - Employee Leave Balance
    // ==========================================================

    public class EmployeeLeaveBalanceViewModel
    {
        public int EmployeeId { get; set; }

        public string EmployeeCode { get; set; }
            = string.Empty;

        public string EmployeeName { get; set; }
            = string.Empty;

        public string Role { get; set; }
            = string.Empty;

        // Leave balances grouped by leave category
        public List<LeaveBalance> Balances { get; set; }
            = new List<LeaveBalance>();
    }
}