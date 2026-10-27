using EmployeeLeaveManagement.Models;
using EmployeeLeaveManagement.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace EmployeeLeaveManagement.Controllers
{
    [Authorize(Roles = "MasterAdmin,Admin")]
    public class EmployeeController : Controller
    {
        private readonly IEmployeeRepository _employeeRepository;

        public EmployeeController(IEmployeeRepository employeeRepository)
        {
            _employeeRepository = employeeRepository;
        }

        // ===========================================
        // Employee List
        // ===========================================
        public async Task<IActionResult> Index()
        {
            var employees = await _employeeRepository.GetAllEmployeesAsync();

            // ===========================================
            // MASTER ADMIN
            // Can see ALL employees
            // ===========================================
            if (User.IsInRole("MasterAdmin"))
            {
                ViewBag.Admins = await _employeeRepository.GetAdminsAsync();
                return View(employees);
            }

            // ===========================================
            // NORMAL ADMIN
            // Can see ONLY employees reporting to them
            // ===========================================
            if (User.IsInRole("Admin"))
            {
                var currentId = User.FindFirstValue("EmployeeID");

                if (!string.IsNullOrEmpty(currentId) &&
                    int.TryParse(currentId, out int adminId))
                {
                    employees = employees
                        .Where(e =>
                            e.ManagerID == adminId &&
                            e.Role == "Employee")
                        .ToList();
                }
                else
                {
                    employees = new List<User>();
                }
            }

            ViewBag.Admins = await _employeeRepository.GetAdminsAsync();

            return View(employees);
        }


        // ===========================================
        // Create Employee - GET
        // ===========================================
        [HttpGet]
        public async Task<IActionResult> Create()
        {
            ViewBag.Admins = await _employeeRepository.GetAdminsAsync();

            return View();
        }


        // ===========================================
        // Create Employee - POST
        // ===========================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(User employee)
        {
            // ===========================================
            // NORMAL ADMIN
            // Can create Employees ONLY
            // ===========================================
            if (User.IsInRole("Admin") &&
                employee.Role != "Employee")
            {
                ModelState.AddModelError(
                    "",
                    "Admins can create Employees only.");
            }

            // ===========================================
            // Prevent creating another MasterAdmin
            // ===========================================
            if (employee.Role == "MasterAdmin")
            {
                ModelState.AddModelError(
                    "",
                    "Only one Master Admin is allowed.");
            }

            // ===========================================
            // NORMAL ADMIN
            // Force the new employee to report to
            // the currently logged-in Admin.
            // ===========================================
            if (User.IsInRole("Admin"))
            {
                var currentId = User.FindFirstValue("EmployeeID");

                if (!string.IsNullOrEmpty(currentId) &&
                    int.TryParse(currentId, out int adminId))
                {
                    employee.ManagerID = adminId;
                }
                else
                {
                    ModelState.AddModelError(
                        "",
                        "Unable to determine the current Admin.");
                }
            }

            if (!ModelState.IsValid)
            {
                ViewBag.Admins =
                    await _employeeRepository.GetAdminsAsync();

                return View(employee);
            }

            employee.IsActive = true;
            employee.CreatedAt = DateTime.Now;

            bool success =
                await _employeeRepository.CreateEmployeeAsync(
                    employee,
                    employee.PasswordHash);

            if (success)
            {
                TempData["Success"] =
                    "Employee created successfully!";

                return RedirectToAction(nameof(Index));
            }

            ViewBag.Admins =
                await _employeeRepository.GetAdminsAsync();

            ModelState.AddModelError(
                "",
                "Failed to create employee.");

            return View(employee);
        }


        // ===========================================
        // Edit Employee - GET
        // ===========================================
        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var employee =
                await _employeeRepository.GetEmployeeByIdAsync(id);

            if (employee == null)
                return NotFound();

            // ===========================================
            // NORMAL ADMIN
            // Can edit ONLY their own employees
            // ===========================================
            if (User.IsInRole("Admin"))
            {
                var currentId = User.FindFirstValue("EmployeeID");

                if (!string.IsNullOrEmpty(currentId) &&
                    int.TryParse(currentId, out int adminId))
                {
                    if (employee.Role != "Employee" ||
                        employee.ManagerID != adminId)
                    {
                        return Forbid();
                    }
                }
                else
                {
                    return Forbid();
                }
            }

            // ===========================================
            // Nobody can edit MasterAdmin
            // ===========================================
            if (employee.Role == "MasterAdmin")
            {
                TempData["Error"] =
                    "Master Admin cannot be modified.";

                return RedirectToAction(nameof(Index));
            }

            ViewBag.Admins =
                await _employeeRepository.GetAdminsAsync();

            return View(employee);
        }


        // ===========================================
        // Edit Employee - POST
        // ===========================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(User employee)
        {
            // ===========================================
            // NORMAL ADMIN
            // Can modify ONLY Employees
            // belonging to that Admin
            // ===========================================
            if (User.IsInRole("Admin"))
            {
                var currentId = User.FindFirstValue("EmployeeID");

                if (!string.IsNullOrEmpty(currentId) &&
                    int.TryParse(currentId, out int adminId))
                {
                    // Prevent Admin from changing the employee
                    // into another role.
                    if (employee.Role != "Employee")
                    {
                        ModelState.AddModelError(
                            "",
                            "Admins cannot modify Admin accounts.");
                    }

                    // IMPORTANT:
                    // Never trust ManagerID coming from the form.
                    // Force it to remain under the logged-in Admin.
                    employee.ManagerID = adminId;

                    // Verify that this employee actually belongs
                    // to the currently logged-in Admin.
                    var existingEmployee =
                        await _employeeRepository
                            .GetEmployeeByIdAsync(employee.EmployeeID);

                    if (existingEmployee == null)
                    {
                        return NotFound();
                    }

                    if (existingEmployee.Role != "Employee" ||
                        existingEmployee.ManagerID != adminId)
                    {
                        return Forbid();
                    }
                }
                else
                {
                    ModelState.AddModelError(
                        "",
                        "Unable to determine the current Admin.");
                }
            }

            // ===========================================
            // Nobody can edit MasterAdmin
            // ===========================================
            if (employee.Role == "MasterAdmin")
            {
                ModelState.AddModelError(
                    "",
                    "Master Admin cannot be modified.");
            }

            if (!ModelState.IsValid)
            {
                ViewBag.Admins =
                    await _employeeRepository.GetAdminsAsync();

                return View(employee);
            }

            bool success =
                await _employeeRepository.UpdateEmployeeAsync(employee);

            if (success)
            {
                TempData["Success"] =
                    "Employee updated successfully!";

                return RedirectToAction(nameof(Index));
            }

            ViewBag.Admins =
                await _employeeRepository.GetAdminsAsync();

            ModelState.AddModelError(
                "",
                "Unable to update employee.");

            return View(employee);
        }


        // ===========================================
        // Delete Employee
        // ===========================================
        [HttpGet]
        public async Task<IActionResult> Delete(int id)
        {
            var employee =
                await _employeeRepository.GetEmployeeByIdAsync(id);

            if (employee == null)
                return NotFound();

            // ===========================================
            // Prevent deleting yourself
            // ===========================================
            var currentId =
                User.FindFirstValue("EmployeeID");

            if (!string.IsNullOrEmpty(currentId) &&
                int.TryParse(currentId, out int loggedInId) &&
                employee.EmployeeID == loggedInId)
            {
                TempData["Error"] =
                    "You cannot delete your own account.";

                return RedirectToAction(nameof(Index));
            }

            // ===========================================
            // NORMAL ADMIN
            // Can delete ONLY their own Employees
            // ===========================================
            if (User.IsInRole("Admin"))
            {
                if (string.IsNullOrEmpty(currentId) ||
                    !int.TryParse(currentId, out int adminId))
                {
                    TempData["Error"] =
                        "Unable to determine the current Admin.";

                    return RedirectToAction(nameof(Index));
                }

                if (employee.Role != "Employee" ||
                    employee.ManagerID != adminId)
                {
                    TempData["Error"] =
                        "You can only delete employees who report to you.";

                    return RedirectToAction(nameof(Index));
                }
            }

            // ===========================================
            // Prevent deleting MasterAdmin
            // ===========================================
            if (employee.Role == "MasterAdmin")
            {
                TempData["Error"] =
                    "Master Admin cannot be deleted.";

                return RedirectToAction(nameof(Index));
            }

            bool success =
                await _employeeRepository.DeleteEmployeeAsync(id);

            if (success)
            {
                TempData["Success"] =
                    "Employee deleted successfully!";
            }
            else
            {
                TempData["Error"] =
                    "Unable to delete employee.";
            }

            return RedirectToAction(nameof(Index));
        }
    }
}