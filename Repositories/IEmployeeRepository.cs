using EmployeeLeaveManagement.Models;

namespace EmployeeLeaveManagement.Repositories
{
    public interface IEmployeeRepository
    {
        // Authentication
        Task<User?> AuthenticateAsync(string email, string password);

        // Employee Details
        Task<User?> GetEmployeeByIdAsync(int employeeId);
        Task<List<User>> GetAllEmployeesAsync();
        Task<List<User>> GetEmployeesByManagerAsync(int managerId);

        // Management
        Task<List<User>> GetAdminsAsync();
        Task<User?> GetMasterAdminAsync();

        // CRUD
        Task<bool> CreateEmployeeAsync(User employee, string password);
        Task<bool> UpdateEmployeeAsync(User employee);

        // Soft Delete
        Task<bool> DeleteEmployeeAsync(int employeeId);
    }
}