using System.ComponentModel.DataAnnotations;

namespace EmployeeLeaveManagement.Models
{
    public class LeaveCategory
    {
        public int CategoryID { get; set; }

        [Required]
        [StringLength(100)]
        public string CategoryName { get; set; } = string.Empty;

        [StringLength(500)]
        public string Description { get; set; } = string.Empty;

        [Required]
        [Range(1, 365)]
        public int DefaultDays { get; set; }

        public bool IsActive { get; set; } = true;

        // Parameterless constructor
        public LeaveCategory() { }
    }
}