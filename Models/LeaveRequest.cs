using System;
using System.ComponentModel.DataAnnotations;

namespace EmployeeLeaveManagement.Models
{
    public class LeaveRequest
    {
        public int RequestID { get; set; }

        public int EmployeeID { get; set; }

        public int CategoryID { get; set; }

        [Required]
        [DataType(DataType.Date)]
        public DateTime StartDate { get; set; }

        [Required]
        [DataType(DataType.Date)]
        public DateTime EndDate { get; set; }

        public int TotalDays { get; set; }

        [Required]
        [StringLength(1000)]
        public string Reason { get; set; } = string.Empty;

        public string Status { get; set; } = "Pending";

        public DateTime AppliedDate { get; set; } = DateTime.Now;


        // ======================================================
        // Review Information
        // ======================================================

        public int? ReviewedBy { get; set; }

        public DateTime? ReviewedDate { get; set; }


        // ======================================================
        // Approval Information
        // ======================================================

        public int? ApprovedBy { get; set; }

        public DateTime? ApprovedDate { get; set; }


        // ======================================================
        // Review Comments
        // ======================================================

        [StringLength(1000)]
        public string Comments { get; set; } = string.Empty;


        // ======================================================
        // Navigation / Display Properties
        // ======================================================

        public string EmployeeName { get; set; } = string.Empty;

        public string CategoryName { get; set; } = string.Empty;

        public string ReviewerName { get; set; } = string.Empty;


        // ======================================================
        // Constructor
        // ======================================================

        public LeaveRequest()
        {
        }
    }
}