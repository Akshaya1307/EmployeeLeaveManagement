using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using EmployeeLeaveManagement.Models;
using EmployeeLeaveManagement.Repositories;
using EmployeeLeaveManagement.Services;
using System.Security.Claims;

namespace EmployeeLeaveManagement.Controllers
{
    [Authorize]
    public class LeaveController : Controller
    {
        private readonly ILeaveRepository _leaveRepository;
        private readonly LeaveCalculationService _calc;

        public LeaveController(
            ILeaveRepository repo,
            LeaveCalculationService calc)
        {
            _leaveRepository = repo;
            _calc = calc;
        }

        // ======================================================
        // Get Current Employee ID
        // ======================================================
        private int? GetCurrentEmployeeId()
        {
            // First try the custom EmployeeID claim
            var employeeIdClaim =
                User.FindFirstValue("EmployeeID");

            if (!string.IsNullOrEmpty(employeeIdClaim) &&
                int.TryParse(employeeIdClaim, out int employeeId))
            {
                return employeeId;
            }

            // Fallback to NameIdentifier
            var nameIdentifier =
                User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (!string.IsNullOrEmpty(nameIdentifier) &&
                int.TryParse(nameIdentifier, out int nameIdentifierId))
            {
                return nameIdentifierId;
            }

            return null;
        }


        // ======================================================
        // Apply Leave - GET
        // ======================================================
        [HttpGet]
        public async Task<IActionResult> Apply()
        {
            var vm = new LeaveRequestViewModel
            {
                Categories =
                    await _leaveRepository.GetLeaveCategoriesAsync()
            };

            return View(vm);
        }


        // ======================================================
        // Apply Leave - POST
        // ======================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Apply(
            LeaveRequestViewModel model)
        {
            if (!ModelState.IsValid)
            {
                model.Categories =
                    await _leaveRepository.GetLeaveCategoriesAsync();

                return View(model);
            }

            var empId = GetCurrentEmployeeId();

            if (empId == null)
            {
                return RedirectToAction("Login", "Account");
            }

            var totalDays =
                await _calc.CalculateTotalDaysAsync(
                    model.LeaveRequest.StartDate,
                    model.LeaveRequest.EndDate);

            // ==================================================
            // Validate Leave Balance
            // ==================================================
            var hasBalance =
                await _calc.ValidateLeaveBalanceAsync(
                    empId.Value,
                    model.LeaveRequest.CategoryID,
                    totalDays);

            if (!hasBalance)
            {
                ModelState.AddModelError(
                    "",
                    "Insufficient leave balance.");

                model.Categories =
                    await _leaveRepository.GetLeaveCategoriesAsync();

                return View(model);
            }

            // ==================================================
            // Check Date Overlap
            // ==================================================
            var overlap =
                await _calc.CheckDateOverlapAsync(
                    empId.Value,
                    model.LeaveRequest.StartDate,
                    model.LeaveRequest.EndDate);

            if (overlap)
            {
                ModelState.AddModelError(
                    "",
                    "Overlapping leave request exists.");

                model.Categories =
                    await _leaveRepository.GetLeaveCategoriesAsync();

                return View(model);
            }

            // ==================================================
            // Set Employee + Total Days
            // ==================================================
            model.LeaveRequest.EmployeeID = empId.Value;
            model.LeaveRequest.TotalDays = totalDays;

            bool success =
                await _leaveRepository.CreateLeaveRequestAsync(
                    model.LeaveRequest);

            if (success)
            {
                TempData["Success"] =
                    "Leave request submitted successfully.";

                return RedirectToAction(nameof(MyRequests));
            }

            ModelState.AddModelError(
                "",
                "Submission failed.");

            model.Categories =
                await _leaveRepository.GetLeaveCategoriesAsync();

            return View(model);
        }


        // ======================================================
        // Employee Leave History
        // ======================================================
        [HttpGet]
        public async Task<IActionResult> MyRequests()
        {
            var empId = GetCurrentEmployeeId();

            if (empId == null)
            {
                return RedirectToAction("Login", "Account");
            }

            var requests =
                await _leaveRepository
                    .GetLeaveRequestsByEmployeeAsync(empId.Value);

            return View(requests);
        }


        // ======================================================
        // Pending Requests
        // ======================================================
        [Authorize(Roles = "MasterAdmin,Admin")]
        [HttpGet]
        public async Task<IActionResult> PendingRequests()
        {
            // ==================================================
            // MASTER ADMIN
            // Can see ALL pending requests
            // including Admin requests
            // ==================================================
            if (User.IsInRole("MasterAdmin"))
            {
                var allPending =
                    await _leaveRepository
                        .GetAllPendingLeaveRequestsAsync();

                return View(allPending);
            }

            // ==================================================
            // NORMAL ADMIN
            // Can see ONLY requests from employees
            // whose ManagerID = current Admin's EmployeeID
            // ==================================================
            var adminId = GetCurrentEmployeeId();

            if (adminId == null)
            {
                return RedirectToAction("Login", "Account");
            }

            var requests =
                await _leaveRepository
                    .GetPendingLeaveRequestsForManagerAsync(
                        adminId.Value);

            return View(requests);
        }


        // ======================================================
        // Review Leave Request
        // ======================================================
        [Authorize(Roles = "MasterAdmin,Admin")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ReviewRequest(
            int requestId,
            string status,
            string comments)
        {
            var reviewerId = GetCurrentEmployeeId();

            if (reviewerId == null)
            {
                return RedirectToAction("Login", "Account");
            }

            // ==================================================
            // Validate Status
            // ==================================================
            if (string.IsNullOrWhiteSpace(status))
            {
                TempData["Error"] =
                    "Status is required.";

                return RedirectToAction(
                    nameof(PendingRequests));
            }

            status = status.Trim();

            if (status != "Approved" &&
                status != "Rejected")
            {
                TempData["Error"] =
                    "Invalid review status.";

                return RedirectToAction(
                    nameof(PendingRequests));
            }

            // ==================================================
            // ADMIN AUTHORIZATION
            //
            // Admin can review ONLY requests belonging to
            // employees who report directly to that Admin.
            //
            // Admin cannot review:
            // - another Admin's employees
            // - another Admin's leave
            // - Master Admin's leave
            // ==================================================
            if (User.IsInRole("Admin"))
            {
                var managedRequests =
                    await _leaveRepository
                        .GetPendingLeaveRequestsForManagerAsync(
                            reviewerId.Value);

                var allowedRequest =
                    managedRequests.FirstOrDefault(
                        r => r.RequestID == requestId);

                if (allowedRequest == null)
                {
                    TempData["Error"] =
                        "You are not authorized to review this leave request.";

                    return RedirectToAction(
                        nameof(PendingRequests));
                }
            }

            // ==================================================
            // MASTER ADMIN
            //
            // No restriction here.
            // Master Admin can review every pending request,
            // including requests submitted by Admins.
            // ==================================================
            bool success =
                await _leaveRepository.ReviewLeaveRequestAsync(
                    requestId,
                    reviewerId.Value,
                    status,
                    comments);

            if (success)
            {
                TempData["Success"] =
                    $"Request {status.ToLower()} successfully.";
            }
            else
            {
                TempData["Error"] =
                    $"Unable to {status.ToLower()} request.";
            }

            return RedirectToAction(
                nameof(PendingRequests));
        }


        // ======================================================
        // Leave Balance
        // ======================================================
        [HttpGet]
        public async Task<IActionResult> LeaveBalance()
        {
            var empId = GetCurrentEmployeeId();

            if (empId == null)
            {
                return RedirectToAction("Login", "Account");
            }

            var categories =
                await _leaveRepository
                    .GetLeaveCategoriesAsync();

            var balances =
                new List<LeaveBalance>();

            foreach (var category in categories)
            {
                var balance =
                    await _leaveRepository
                        .GetLeaveBalanceAsync(
                            empId.Value,
                            category.CategoryID,
                            DateTime.Now.Year);

                if (balance != null)
                {
                    balances.Add(balance);
                }
            }

            return View(balances);
        }
    }
}