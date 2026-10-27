using System.Collections.Generic;

namespace EmployeeLeaveManagement.Models
{
    public class LeaveRequestViewModel
    {
        public LeaveRequest LeaveRequest { get; set; }
        public List<LeaveCategory> Categories { get; set; }
        public int AvailableLeaveBalance { get; set; }

        public LeaveRequestViewModel()
        {
            LeaveRequest = new LeaveRequest();
            Categories = new List<LeaveCategory>();
            AvailableLeaveBalance = 0;
        }

        // Optional: parameterized constructor
        public LeaveRequestViewModel(LeaveRequest leaveRequest, List<LeaveCategory> categories)
        {
            LeaveRequest = leaveRequest ?? new LeaveRequest();
            Categories = categories ?? new List<LeaveCategory>();
            AvailableLeaveBalance = 0;
        }
    }
}