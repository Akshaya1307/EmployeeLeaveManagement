using EmployeeLeaveManagement.Models;

namespace EmployeeLeaveManagement.Repositories
{
    public interface ILeaveRepository
    {
        // ===========================================
        // Leave Categories
        // ===========================================
        Task<List<LeaveCategory>> GetLeaveCategoriesAsync();

        // ===========================================
        // Leave Balance
        // ===========================================
        Task<LeaveBalance?> GetLeaveBalanceAsync(
            int employeeId,
            int categoryId,
            int year);

        // ===========================================
        // Apply Leave
        // ===========================================
        Task<bool> CreateLeaveRequestAsync(
            LeaveRequest request);

        // ===========================================
        // Employee Leave History
        // ===========================================
        Task<List<LeaveRequest>> GetLeaveRequestsByEmployeeAsync(
            int employeeId);

        // ===========================================
        // Pending Requests (Admin)
        // Shows only employees reporting to the Admin
        // ===========================================
        Task<List<LeaveRequest>> GetPendingLeaveRequestsForManagerAsync(
            int managerId);

        // ===========================================
        // Pending Requests (Master Admin)
        // Shows ALL pending requests
        // ===========================================
        Task<List<LeaveRequest>> GetAllPendingLeaveRequestsAsync();

        // ===========================================
        // Approve / Reject Leave
        // ===========================================
        Task<bool> ReviewLeaveRequestAsync(
            int requestId,
            int reviewerId,
            string status,
            string comments);

        // ===========================================
        // Dashboard
        // ===========================================
        Task<DashboardViewModel> GetDashboardDataAsync(
            int employeeId,
            string role);
    }
}