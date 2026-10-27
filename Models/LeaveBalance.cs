namespace EmployeeLeaveManagement.Models
{
    public class LeaveBalance
    {
        public int BalanceID { get; set; }
        public int EmployeeID { get; set; }
        public int CategoryID { get; set; }
        public int TotalDays { get; set; }
        public int UsedDays { get; set; }
        public int RemainingDays { get; set; }
        public int Year { get; set; }
        public string CategoryName { get; set; } = string.Empty;
    }
}