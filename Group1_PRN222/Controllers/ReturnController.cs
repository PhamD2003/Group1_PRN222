using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Group1_PRN222.Models;

namespace Group1_PRN222.Controllers
{
    public class ReturnController : Controller
    {
        private readonly CloneEbayDbContext _context;

        public ReturnController(CloneEbayDbContext context)
        {
            _context = context;
        }

        #region Mock Authentication (Replace when Member 1 completes)

        /// <summary>
        /// Mock user ID - replace with real authentication
        /// </summary>
        private int GetCurrentUserId() => 1; // TODO: Replace with real authentication

        #endregion

        #region Return Request Management

        /// <summary>
        /// GET: /Return/Create/{orderId} - Tạo yêu cầu trả hàng
        /// </summary>
        public async Task<IActionResult> Create(int orderId)
        {
            var userId = GetCurrentUserId();

            // Validate order exists and belongs to user
            var order = await _context.OrderTables
                .Where(o => o.Id == orderId && o.BuyerId == userId)
                .Include(o => o.OrderItems)
                .ThenInclude(oi => oi.Product)
                .ThenInclude(p => p.Category)
                .Include(o => o.Address)
                .Include(o => o.Payments)
                .FirstOrDefaultAsync();

            if (order == null)
            {
                TempData["Error"] = "Không tìm thấy đơn hàng hoặc bạn không có quyền truy cập.";
                return RedirectToAction("History", "Order");
            }

            // Check if order is eligible for return
            if (!IsOrderEligibleForReturn(order))
            {
                TempData["Error"] = GetReturnIneligibilityReason(order);
                return RedirectToAction("Details", "Order", new { id = orderId });
            }

            // Check if return request already exists
            var existingReturn = await _context.ReturnRequests
                .FirstOrDefaultAsync(r => r.OrderId == orderId);

            if (existingReturn != null)
            {
                TempData["Error"] = $"Đơn hàng này đã có yêu cầu trả hàng với trạng thái: {existingReturn.Status}";
                return RedirectToAction("Details", "Order", new { id = orderId });
            }

            ViewBag.Order = order;
            ViewBag.ReturnReasons = GetReturnReasons();

            return View();
        }

        /// <summary>
        /// POST: /Return/Create - Xử lý tạo yêu cầu trả hàng
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(int orderId, string reason, string description)
        {
            try
            {
                var userId = GetCurrentUserId();

                // Validate order again
                var order = await _context.OrderTables
                    .Where(o => o.Id == orderId && o.BuyerId == userId)
                    .FirstOrDefaultAsync();

                if (order == null)
                {
                    return Json(new { success = false, message = "Không tìm thấy đơn hàng." });
                }

                if (!IsOrderEligibleForReturn(order))
                {
                    return Json(new { success = false, message = GetReturnIneligibilityReason(order) });
                }

                // Check for existing return request
                var existingReturn = await _context.ReturnRequests
                    .FirstOrDefaultAsync(r => r.OrderId == orderId);

                if (existingReturn != null)
                {
                    return Json(new { success = false, message = "Đơn hàng này đã có yêu cầu trả hàng." });
                }

                // Validate input
                if (string.IsNullOrEmpty(reason))
                {
                    return Json(new { success = false, message = "Vui lòng chọn lý do trả hàng." });
                }

                // Create return request
                var returnRequest = new ReturnRequest
                {
                    OrderId = orderId,
                    UserId = userId,
                    Reason = string.IsNullOrEmpty(description) ? reason : $"{reason}: {description}",
                    Status = "Pending",
                    CreatedAt = DateTime.Now
                };

                _context.ReturnRequests.Add(returnRequest);
                await _context.SaveChangesAsync();

                // Log the return request creation (optional)
                Console.WriteLine($"Return request #{returnRequest.Id} created for order #{orderId} by user #{userId}");

                return Json(new
                {
                    success = true,
                    message = "Yêu cầu trả hàng đã được gửi thành công!",
                    redirectUrl = Url.Action("List")
                });
            }
            catch (Exception ex)
            {
                // Log the error (in production, use proper logging)
                Console.WriteLine($"Error creating return request: {ex.Message}");
                return Json(new { success = false, message = "Có lỗi xảy ra trong hệ thống. Vui lòng thử lại sau." });
            }
        }

        /// <summary>
        /// GET: /Return/List - Danh sách yêu cầu trả hàng của user
        /// </summary>
        public async Task<IActionResult> List(int page = 1, int pageSize = 10, string status = "")
        {
            var userId = GetCurrentUserId();

            var query = _context.ReturnRequests
                .Where(r => r.UserId == userId)
                .Include(r => r.Order)
                .ThenInclude(o => o.OrderItems)
                .ThenInclude(oi => oi.Product)
                .AsQueryable();

            // Filter by status if provided
            if (!string.IsNullOrEmpty(status))
            {
                query = query.Where(r => r.Status == status);
            }

            query = query.OrderByDescending(r => r.CreatedAt);

            var totalReturns = await query.CountAsync();
            var returns = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = (int)Math.Ceiling((double)totalReturns / pageSize);
            ViewBag.TotalReturns = totalReturns;
            ViewBag.StatusFilter = status;

            return View(returns);
        }

        /// <summary>
        /// GET: /Return/Details/{id} - Chi tiết yêu cầu trả hàng
        /// </summary>
        public async Task<IActionResult> Details(int id)
        {
            var userId = GetCurrentUserId();

            var returnRequest = await _context.ReturnRequests
                .Where(r => r.Id == id && r.UserId == userId)
                .Include(r => r.Order)
                .ThenInclude(o => o.OrderItems)
                .ThenInclude(oi => oi.Product)
                .ThenInclude(p => p.Category)
                .Include(r => r.Order.Address)
                .Include(r => r.Order.Payments)
                .FirstOrDefaultAsync();

            if (returnRequest == null)
            {
                TempData["Error"] = "Không tìm thấy yêu cầu trả hàng.";
                return RedirectToAction("List");
            }

            return View(returnRequest);
        }

        /// <summary>
        /// POST: /Return/Cancel/{id} - Hủy yêu cầu trả hàng
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> Cancel(int id)
        {
            try
            {
                var userId = GetCurrentUserId();

                var returnRequest = await _context.ReturnRequests
                    .Where(r => r.Id == id && r.UserId == userId)
                    .FirstOrDefaultAsync();

                if (returnRequest == null)
                {
                    return Json(new { success = false, message = "Không tìm thấy yêu cầu trả hàng." });
                }

                if (returnRequest.Status != "Pending")
                {
                    return Json(new { success = false, message = "Không thể hủy yêu cầu trả hàng này. Chỉ có thể hủy các yêu cầu đang chờ xử lý." });
                }

                returnRequest.Status = "Cancelled";
                await _context.SaveChangesAsync();

                // Log the cancellation
                Console.WriteLine($"Return request #{id} cancelled by user #{userId}");

                return Json(new { success = true, message = "Đã hủy yêu cầu trả hàng thành công." });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error cancelling return request: {ex.Message}");
                return Json(new { success = false, message = "Có lỗi xảy ra trong hệ thống. Vui lòng thử lại sau." });
            }
        }

        /// <summary>
        /// GET: /Return/CheckEligibility/{orderId} - Check if order is eligible for return (AJAX)
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> CheckEligibility(int orderId)
        {
            var userId = GetCurrentUserId();

            var order = await _context.OrderTables
                .Where(o => o.Id == orderId && o.BuyerId == userId)
                .FirstOrDefaultAsync();

            if (order == null)
            {
                return Json(new { eligible = false, reason = "Không tìm thấy đơn hàng." });
            }

            var existingReturn = await _context.ReturnRequests
                .FirstOrDefaultAsync(r => r.OrderId == orderId);

            if (existingReturn != null)
            {
                return Json(new
                {
                    eligible = false,
                    reason = "Đơn hàng này đã có yêu cầu trả hàng.",
                    existingReturnId = existingReturn.Id,
                    existingReturnStatus = existingReturn.Status
                });
            }

            var isEligible = IsOrderEligibleForReturn(order);
            var reason = isEligible ? "Đơn hàng đủ điều kiện trả hàng." : GetReturnIneligibilityReason(order);

            return Json(new { eligible = isEligible, reason = reason });
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Check if order is eligible for return
        /// </summary>
        private bool IsOrderEligibleForReturn(OrderTable order)
        {
            // Must be delivered
            if (order.Status != "Delivered" && order.Status != "Confirmed")
                return false;

            // Must be within return period (e.g., 30 days)
            if (order.OrderDate.HasValue)
            {
                var daysSinceOrder = (DateTime.Now - order.OrderDate.Value).Days;
                if (daysSinceOrder > 30)
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Get reason why order is not eligible for return
        /// </summary>
        private string GetReturnIneligibilityReason(OrderTable order)
        {
            if (order.Status != "Delivered" && order.Status != "Confirmed")
                return "Chỉ có thể trả hàng cho đơn hàng đã được xác nhận hoặc giao thành công.";

            if (order.OrderDate.HasValue)
            {
                var daysSinceOrder = (DateTime.Now - order.OrderDate.Value).Days;
                if (daysSinceOrder > 30)
                    return $"Đã quá thời hạn trả hàng (30 ngày kể từ khi đặt hàng). Đơn hàng này đã được đặt {daysSinceOrder} ngày trước.";
            }

            return "Đơn hàng không đủ điều kiện để trả hàng.";
        }

        /// <summary>
        /// Get list of return reasons
        /// </summary>
        private List<(string Code, string Display)> GetReturnReasons()
        {
            return new List<(string, string)>
            {
                ("defective", "Sản phẩm bị lỗi/hư hỏng"),
                ("wrong_item", "Giao sai sản phẩm"),
                ("not_as_described", "Sản phẩm không đúng mô tả"),
                ("changed_mind", "Đổi ý không muốn mua"),
                ("size_issue", "Kích thước không phù hợp"),
                ("quality_issue", "Chất lượng không như mong đợi"),
                ("shipping_damage", "Hư hỏng trong quá trình vận chuyển"),
                ("other", "Lý do khác")
            };
        }

        /// <summary>
        /// Get return statistics for user (for future dashboard)
        /// </summary>
        private async Task<(int Total, int Pending, int Approved, int Rejected)> GetReturnStatsForUser(int userId)
        {
            var returns = await _context.ReturnRequests
                .Where(r => r.UserId == userId)
                .ToListAsync();

            return (
                Total: returns.Count,
                Pending: returns.Count(r => r.Status == "Pending"),
                Approved: returns.Count(r => r.Status == "Approved"),
                Rejected: returns.Count(r => r.Status == "Rejected")
            );
        }

        #endregion

        #region Admin Functions (For future admin panel - User side would not access these)

        /// <summary>
        /// GET: /Return/Manage - Admin manage return requests (future feature)
        /// Note: This would typically be in an Admin area/controller
        /// </summary>
        public async Task<IActionResult> Manage(int page = 1, int pageSize = 20, string status = "")
        {
            // TODO: Add admin authorization check
            // For now, this is just a placeholder for future admin functionality

            var query = _context.ReturnRequests
                .Include(r => r.Order)
                .ThenInclude(o => o.OrderItems)
                .ThenInclude(oi => oi.Product)
                .Include(r => r.User)
                .AsQueryable();

            if (!string.IsNullOrEmpty(status))
            {
                query = query.Where(r => r.Status == status);
            }

            query = query.OrderByDescending(r => r.CreatedAt);

            var totalReturns = await query.CountAsync();
            var returns = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = (int)Math.Ceiling((double)totalReturns / pageSize);
            ViewBag.TotalReturns = totalReturns;
            ViewBag.StatusFilter = status;

            return View(returns);
        }

        /// <summary>
        /// POST: /Return/Approve/{id} - Admin approve return (future feature)
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> Approve(int id, string adminNotes = "")
        {
            try
            {
                // TODO: Add admin authorization check

                var returnRequest = await _context.ReturnRequests
                    .Include(r => r.Order)
                    .FirstOrDefaultAsync(r => r.Id == id);

                if (returnRequest == null)
                {
                    return Json(new { success = false, message = "Không tìm thấy yêu cầu trả hàng." });
                }

                if (returnRequest.Status != "Pending")
                {
                    return Json(new { success = false, message = "Chỉ có thể chấp nhận các yêu cầu đang chờ xử lý." });
                }

                returnRequest.Status = "Approved";
                // TODO: Add admin notes field to model if needed

                await _context.SaveChangesAsync();

                // Log the approval
                Console.WriteLine($"Return request #{id} approved by admin. Order #{returnRequest.OrderId}");

                return Json(new { success = true, message = "Đã chấp nhận yêu cầu trả hàng." });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error approving return request: {ex.Message}");
                return Json(new { success = false, message = "Có lỗi xảy ra trong hệ thống." });
            }
        }

        /// <summary>
        /// POST: /Return/Reject/{id} - Admin reject return (future feature)
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> Reject(int id, string reason = "")
        {
            try
            {
                // TODO: Add admin authorization check

                var returnRequest = await _context.ReturnRequests.FindAsync(id);
                if (returnRequest == null)
                {
                    return Json(new { success = false, message = "Không tìm thấy yêu cầu trả hàng." });
                }

                if (returnRequest.Status != "Pending")
                {
                    return Json(new { success = false, message = "Chỉ có thể từ chối các yêu cầu đang chờ xử lý." });
                }

                returnRequest.Status = "Rejected";
                // TODO: Add rejection reason to model if needed

                await _context.SaveChangesAsync();

                // Log the rejection
                Console.WriteLine($"Return request #{id} rejected by admin. Reason: {reason}");

                return Json(new { success = true, message = "Đã từ chối yêu cầu trả hàng." });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error rejecting return request: {ex.Message}");
                return Json(new { success = false, message = "Có lỗi xảy ra trong hệ thống." });
            }
        }

        #endregion
    }
}