using EmployeeLeaveManagement.Repositories;

namespace EmployeeLeaveManagement.Services
{
    public class LeaveCalculationService
    {
        private readonly ILeaveRepository _leaveRepository;
        public LeaveCalculationService(ILeaveRepository leaveRepository) => _leaveRepository = leaveRepository;

        public async Task<int> CalculateTotalDaysAsync(DateTime start, DateTime end)
        {
            int days = 0;
            for (var d = start; d <= end; d = d.AddDays(1))
                if (d.DayOfWeek != DayOfWeek.Saturday && d.DayOfWeek != DayOfWeek.Sunday)
                    days++;
            return await Task.FromResult(days);
        }

        public async Task<bool> ValidateLeaveBalanceAsync(int employeeId, int categoryId, int requestedDays)
        {
            var balance = await _leaveRepository.GetLeaveBalanceAsync(employeeId, categoryId, DateTime.Now.Year);
            return balance != null && balance.RemainingDays >= requestedDays;
        }

        public async Task<bool> CheckDateOverlapAsync(int employeeId, DateTime start, DateTime end)
        {
            var existing = await _leaveRepository.GetLeaveRequestsByEmployeeAsync(employeeId);
            return existing.Any(r => r.Status == "Approved" &&
                (start <= r.EndDate && end >= r.StartDate));
        }
    }
}