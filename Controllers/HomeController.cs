using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using EmployeeLeaveManagement.Repositories;
using System.Security.Claims;

namespace EmployeeLeaveManagement.Controllers
{
    [Authorize]
    public class HomeController : Controller
    {
        private readonly ILeaveRepository _leaveRepository;
        
        public HomeController(ILeaveRepository leaveRepository)
        {
            _leaveRepository = leaveRepository ?? throw new ArgumentNullException(nameof(leaveRepository));
        }
        
        public async Task<IActionResult> Index()
        {
            // Safely get user ID with null checking
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out int userId))
            {
                // Redirect to login if user ID not found
                return RedirectToAction("Login", "Account");
            }
            
            // Safely get user role
            var roleClaim = User.FindFirst(ClaimTypes.Role);
            var userRole = roleClaim?.Value ?? "Employee";
            
            var dashboard = await _leaveRepository.GetDashboardDataAsync(userId, userRole);
            return View(dashboard);
        }
        
        public IActionResult Privacy()
        {
            return View();
        }
        
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View();
        }
    }
}