// Controllers/OrderController.cs - Updated with Real Authentication
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Group1_PRN222.Models;
using Group1_PRN222.Services;
using System.Text.Json;

namespace Group1_PRN222.Controllers
{
    public class OrderController : Controller
    {
        private readonly CloneEbayDbContext _context;
        private readonly ICouponService _couponService;
        private const string CART_SESSION_KEY = "Cart";
        private const string COUPON_SESSION_KEY = "AppliedCoupon";

        public OrderController(CloneEbayDbContext context, ICouponService couponService)
        {
            _context = context;
            _couponService = couponService;
        }

        #region Authentication & Address Methods (Updated - Real Implementation)

        /// <summary>
        /// Get current logged-in user ID from session
        /// </summary>
        private int? GetCurrentUserId()
        {
            return HttpContext.Session.GetInt32("UserId");
        }

        /// <summary>
        /// Get current logged-in username from session
        /// </summary>
        private string? GetCurrentUsername()
        {
            return HttpContext.Session.GetString("Username");
        }

        /// <summary>
        /// Check if user is authenticated
        /// </summary>
        private bool IsUserAuthenticated()
        {
            var userId = GetCurrentUserId();
            var username = GetCurrentUsername();
            return userId.HasValue && !string.IsNullOrEmpty(username);
        }

        /// <summary>
        /// Get user's default address from database
        /// </summary>
        private async Task<Address?> GetUserDefaultAddressAsync(int userId)
        {
            return await _context.Addresses
                .FirstOrDefaultAsync(a => a.UserId == userId && a.IsDefault == true);
        }

        /// <summary>
        /// Get all user addresses from database
        /// </summary>
        private async Task<List<Address>> GetUserAddressesAsync(int userId)
        {
            return await _context.Addresses
                .Where(a => a.UserId == userId)
                .OrderByDescending(a => a.IsDefault)
                .ThenBy(a => a.Id)
                .ToListAsync();
        }

        /// <summary>
        /// Redirect to login if not authenticated
        /// </summary>
        private IActionResult RedirectToLoginIfNotAuthenticated()
        {
            if (!IsUserAuthenticated())
            {
                TempData["Error"] = "Vui lòng đăng nhập để tiếp tục.";
                return RedirectToAction("Login", "Account");
            }
            return null!; // Continue with normal flow
        }

        #endregion

        #region Coupon Management (AJAX Endpoints)

        /// <summary>
        /// POST: /Order/ValidateCoupon - Validate coupon code via AJAX
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> ValidateCoupon(string couponCode)
        {
            try
            {
                var cartItems = GetCartItemsFromSession();
                if (!cartItems.Any())
                {
                    return Json(new { success = false, message = "Giỏ hàng trống." });
                }

                var result = await _couponService.ValidateCouponAsync(couponCode, cartItems);

                if (result.IsValid)
                {
                    return Json(new
                    {
                        success = true,
                        message = result.SuccessMessage,
                        discountAmount = result.DiscountAmount,
                        discountPercent = result.DiscountPercent,
                        couponCode = result.Coupon?.Code,
                        isProductSpecific = result.Coupon?.ProductId.HasValue ?? false,
                        productName = result.Coupon?.Product?.Title
                    });
                }
                else
                {
                    return Json(new { success = false, message = result.ErrorMessage });
                }
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Có lỗi xảy ra khi kiểm tra mã giảm giá." });
            }
        }

        /// <summary>
        /// POST: /Order/ApplyCoupon - Apply coupon to session
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> ApplyCoupon(string couponCode)
        {
            try
            {
                var cartItems = GetCartItemsFromSession();
                if (!cartItems.Any())
                {
                    return Json(new { success = false, message = "Giỏ hàng trống." });
                }

                var result = await _couponService.ValidateCouponAsync(couponCode, cartItems);

                if (result.IsValid)
                {
                    // Store coupon in session
                    HttpContext.Session.SetString(COUPON_SESSION_KEY, couponCode);

                    // Calculate new totals
                    decimal subtotal = cartItems.Sum(item => item.TotalPrice);
                    decimal discountAmount = result.DiscountAmount;
                    decimal shippingFee = CalculateShippingFee(cartItems);
                    decimal total = subtotal - discountAmount + shippingFee;

                    return Json(new
                    {
                        success = true,
                        message = result.SuccessMessage,
                        discountAmount = discountAmount,
                        subtotal = subtotal,
                        shippingFee = shippingFee,
                        total = total,
                        couponCode = couponCode,
                        discountPercent = result.DiscountPercent,
                        isProductSpecific = result.Coupon?.ProductId.HasValue ?? false
                    });
                }
                else
                {
                    return Json(new { success = false, message = result.ErrorMessage });
                }
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Có lỗi xảy ra khi áp dụng mã giảm giá." });
            }
        }

        /// <summary>
        /// POST: /Order/RemoveCoupon - Remove coupon from session
        /// </summary>
        [HttpPost]
        public IActionResult RemoveCoupon()
        {
            try
            {
                HttpContext.Session.Remove(COUPON_SESSION_KEY);

                var cartItems = GetCartItemsFromSession();
                decimal subtotal = cartItems.Sum(item => item.TotalPrice);
                decimal shippingFee = CalculateShippingFee(cartItems);
                decimal total = subtotal + shippingFee;

                return Json(new
                {
                    success = true,
                    message = "Đã gỡ bỏ mã giảm giá",
                    subtotal = subtotal,
                    shippingFee = shippingFee,
                    total = total,
                    discountAmount = 0
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Có lỗi xảy ra khi gỡ bỏ mã giảm giá." });
            }
        }

        /// <summary>
        /// GET: /Order/GetAvailableCoupons - Get available coupons for user
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetAvailableCoupons()
        {
            try
            {
                var cartItems = GetCartItemsFromSession();
                var activeCoupons = await _couponService.GetActiveCouponsAsync();

                // Filter coupons that apply to current cart
                var applicableCoupons = new List<object>();

                foreach (var coupon in activeCoupons)
                {
                    var result = await _couponService.ValidateCouponAsync(coupon.Code ?? "", cartItems);
                    if (result.IsValid)
                    {
                        applicableCoupons.Add(new
                        {
                            Code = coupon.Code,
                            DiscountPercent = coupon.DiscountPercent,
                            Description = result.SuccessMessage,
                            EstimatedDiscount = result.DiscountAmount,
                            IsProductSpecific = coupon.ProductId.HasValue,
                            ProductName = coupon.Product?.Title
                        });
                    }
                }

                return Json(new { success = true, coupons = applicableCoupons });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Có lỗi xảy ra khi lấy danh sách mã giảm giá." });
            }
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Get cart items from session (integration with Member 2)
        /// </summary>
        private List<CartItem> GetCartItemsFromSession()
        {
            var cartJson = HttpContext.Session.GetString(CART_SESSION_KEY);
            if (cartJson == null)
            {
                return new List<CartItem>();
            }
            return JsonSerializer.Deserialize<List<CartItem>>(cartJson) ?? new List<CartItem>();
        }

        /// <summary>
        /// Clear cart after successful order
        /// </summary>
        private void ClearCart()
        {
            HttpContext.Session.Remove(CART_SESSION_KEY);
        }

        /// <summary>
        /// Calculate shipping fee based on cart items
        /// </summary>
        private decimal CalculateShippingFee(List<CartItem> cartItems)
        {
            // Simple shipping calculation - can be enhanced later
            decimal baseShippingFee = 30000; // 30k VND base fee
            decimal weightFee = cartItems.Sum(item => item.Quantity) * 5000; // 5k per item
            return baseShippingFee + weightFee;
        }

        #endregion

        #region Main Order Workflow (Updated with Real Authentication)

        /// <summary>
        /// GET: /Order/Checkout - Checkout từ Cart (URL từ button "Tiến hành thanh toán")
        /// </summary>
        public async Task<IActionResult> Checkout()
        {
            // Check authentication first
            var authCheck = RedirectToLoginIfNotAuthenticated();
            if (authCheck != null) return authCheck;

            var userId = GetCurrentUserId()!.Value;

            // Lấy cart items từ session (Member 2's implementation)
            var cartItems = GetCartItemsFromSession();

            if (!cartItems.Any())
            {
                TempData["Error"] = "Giỏ hàng trống. Vui lòng thêm sản phẩm trước khi thanh toán.";
                return RedirectToAction("Index", "Cart");
            }

            // Validate products still exist and have enough stock
            foreach (var item in cartItems)
            {
                var product = await _context.Products
                    .Include(p => p.Inventories)
                    .FirstOrDefaultAsync(p => p.Id == item.ProductId);

                if (product == null)
                {
                    TempData["Error"] = $"Sản phẩm '{item.ProductName}' không còn tồn tại.";
                    return RedirectToAction("Index", "Cart");
                }

                var inventory = product.Inventories.FirstOrDefault();
                if (inventory == null || inventory.Quantity < item.Quantity)
                {
                    TempData["Error"] = $"Sản phẩm '{item.ProductName}' không đủ số lượng trong kho.";
                    return RedirectToAction("Index", "Cart");
                }
            }

            // Get user's addresses
            var userAddresses = await GetUserAddressesAsync(userId);
            var defaultAddress = await GetUserDefaultAddressAsync(userId);

            if (!userAddresses.Any())
            {
                TempData["Error"] = "Bạn chưa có địa chỉ giao hàng. Vui lòng thêm địa chỉ trước khi đặt hàng.";
                return RedirectToAction("Edit", "Account", new { id = userId });
            }

            // Handle coupon if applied
            string? appliedCouponCode = HttpContext.Session.GetString(COUPON_SESSION_KEY);
            decimal discountAmount = 0;
            CouponValidationResult? couponResult = null;

            if (!string.IsNullOrEmpty(appliedCouponCode))
            {
                couponResult = await _couponService.ValidateCouponAsync(appliedCouponCode, cartItems);
                if (couponResult.IsValid)
                {
                    discountAmount = couponResult.DiscountAmount;
                }
                else
                {
                    // Remove invalid coupon from session
                    HttpContext.Session.Remove(COUPON_SESSION_KEY);
                    TempData["Warning"] = "Mã giảm giá đã hết hạn và được gỡ bỏ.";
                }
            }

            // Tính toán chi phí
            decimal subtotal = cartItems.Sum(item => item.TotalPrice);
            decimal shippingFee = CalculateShippingFee(cartItems);
            decimal total = subtotal - discountAmount + shippingFee;

            ViewBag.CartItems = cartItems;
            ViewBag.Subtotal = subtotal;
            ViewBag.DiscountAmount = discountAmount;
            ViewBag.ShippingFee = shippingFee;
            ViewBag.Total = total;
            ViewBag.UserAddresses = userAddresses;
            ViewBag.DefaultAddress = defaultAddress;
            ViewBag.CouponResult = couponResult;
            ViewBag.AppliedCouponCode = appliedCouponCode;

            return View();
        }

        /// <summary>
        /// POST: /Order/Checkout - Xử lý checkout và tạo order
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> Checkout(int addressId, string paymentMethod, string notes = "")
        {
            // Check authentication first
            var authCheck = RedirectToLoginIfNotAuthenticated();
            if (authCheck != null) return Json(new { success = false, message = "Vui lòng đăng nhập để tiếp tục." });

            var userId = GetCurrentUserId()!.Value;

            try
            {
                var cartItems = GetCartItemsFromSession();

                if (!cartItems.Any())
                {
                    return Json(new { success = false, message = "Giỏ hàng trống." });
                }

                // Validate address belongs to user
                var address = await _context.Addresses
                    .FirstOrDefaultAsync(a => a.Id == addressId && a.UserId == userId);

                if (address == null)
                {
                    return Json(new { success = false, message = "Địa chỉ không hợp lệ." });
                }

                // Handle coupon discount
                string? appliedCouponCode = HttpContext.Session.GetString(COUPON_SESSION_KEY);
                decimal discountAmount = 0;
                string? couponDescription = null;

                if (!string.IsNullOrEmpty(appliedCouponCode))
                {
                    var couponResult = await _couponService.ValidateCouponAsync(appliedCouponCode, cartItems);
                    if (couponResult.IsValid)
                    {
                        discountAmount = couponResult.DiscountAmount;
                        couponDescription = couponResult.SuccessMessage;
                    }
                }

                // Calculate total
                decimal subtotal = cartItems.Sum(item => item.TotalPrice);
                decimal shippingFee = CalculateShippingFee(cartItems);
                decimal total = subtotal - discountAmount + shippingFee;

                // Create order
                var order = new OrderTable
                {
                    BuyerId = userId,
                    AddressId = addressId,
                    OrderDate = DateTime.Now,
                    TotalPrice = total,
                    Status = "Pending"
                };

                _context.OrderTables.Add(order);
                await _context.SaveChangesAsync();

                // Create order items
                foreach (var item in cartItems)
                {
                    var orderItem = new OrderItem
                    {
                        OrderId = order.Id,
                        ProductId = item.ProductId,
                        Quantity = item.Quantity,
                        UnitPrice = item.Price
                    };

                    _context.OrderItems.Add(orderItem);

                    // Update inventory
                    var inventory = await _context.Inventories
                        .FirstOrDefaultAsync(i => i.ProductId == item.ProductId);

                    if (inventory != null)
                    {
                        inventory.Quantity -= item.Quantity;
                        inventory.LastUpdated = DateTime.Now;
                    }
                }

                // Create payment record
                var payment = new Payment
                {
                    OrderId = order.Id,
                    UserId = userId,
                    Amount = total,
                    Method = paymentMethod,
                    Status = paymentMethod == "COD" ? "Pending" : "Paid",
                    PaidAt = paymentMethod == "COD" ? null : DateTime.Now
                };

                _context.Payments.Add(payment);

                // Create shipping info
                var shipping = new ShippingInfo
                {
                    OrderId = order.Id,
                    Carrier = "Giao hàng tiết kiệm",
                    TrackingNumber = $"GHN{order.Id:D8}",
                    Status = "Preparing",
                    EstimatedArrival = DateTime.Now.AddDays(3)
                };

                _context.ShippingInfos.Add(shipping);

                await _context.SaveChangesAsync();

                // Clear cart after successful order
                ClearCart();

                return Json(new { success = true, orderId = order.Id, message = "Đặt hàng thành công!" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Có lỗi xảy ra: {ex.Message}" });
            }
        }

        /// <summary>
        /// GET: /Order/History - Xem lịch sử đơn hàng
        /// </summary>
        public async Task<IActionResult> History(int page = 1)
        {
            // Check authentication first
            var authCheck = RedirectToLoginIfNotAuthenticated();
            if (authCheck != null) return authCheck;

            var userId = GetCurrentUserId()!.Value;

            const int pageSize = 10;
            var totalOrders = await _context.OrderTables.CountAsync(o => o.BuyerId == userId);
            var totalPages = (int)Math.Ceiling(totalOrders / (double)pageSize);

            var orders = await _context.OrderTables
                .Include(o => o.Address)
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.Product)
                .Include(o => o.Payments)
                .Include(o => o.ShippingInfos)
                .Where(o => o.BuyerId == userId)
                .OrderByDescending(o => o.OrderDate)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = totalPages;
            ViewBag.TotalOrders = totalOrders;

            return View(orders);
        }

        /// <summary>
        /// GET: /Order/Details/{id} - Xem chi tiết đơn hàng
        /// </summary>
        public async Task<IActionResult> Details(int id)
        {
            // Check authentication first
            var authCheck = RedirectToLoginIfNotAuthenticated();
            if (authCheck != null) return authCheck;

            var userId = GetCurrentUserId()!.Value;

            var order = await _context.OrderTables
                .Include(o => o.Address)
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.Product)
                .Include(o => o.Payments)
                .Include(o => o.ShippingInfos)
                .FirstOrDefaultAsync(o => o.Id == id && o.BuyerId == userId);

            if (order == null)
            {
                TempData["Error"] = "Không tìm thấy đơn hàng.";
                return RedirectToAction("History");
            }

            return View(order);
        }

        /// <summary>
        /// POST: /Order/Cancel/{id} - Hủy đơn hàng
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> Cancel(int id, string reason = "")
        {
            // Check authentication first
            var authCheck = RedirectToLoginIfNotAuthenticated();
            if (authCheck != null) return Json(new { success = false, message = "Vui lòng đăng nhập để tiếp tục." });

            var userId = GetCurrentUserId()!.Value;

            try
            {
                var order = await _context.OrderTables
                    .Include(o => o.OrderItems)
                    .FirstOrDefaultAsync(o => o.Id == id && o.BuyerId == userId);

                if (order == null)
                {
                    return Json(new { success = false, message = "Không tìm thấy đơn hàng." });
                }

                if (order.Status != "Pending")
                {
                    return Json(new { success = false, message = "Không thể hủy đơn hàng này." });
                }

                // Update order status
                order.Status = "Cancelled";

                // Restore inventory
                foreach (var item in order.OrderItems)
                {
                    var inventory = await _context.Inventories
                        .FirstOrDefaultAsync(i => i.ProductId == item.ProductId);

                    if (inventory != null)
                    {
                        inventory.Quantity += item.Quantity;
                        inventory.LastUpdated = DateTime.Now;
                    }
                }

                await _context.SaveChangesAsync();

                return Json(new { success = true, message = "Hủy đơn hàng thành công!" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Có lỗi xảy ra: {ex.Message}" });
            }
        }

        /// <summary>
        /// GET: /Order/Return/{id} - Trang yêu cầu hoàn trả
        /// </summary>
        public async Task<IActionResult> Return(int id)
        {
            // Check authentication first
            var authCheck = RedirectToLoginIfNotAuthenticated();
            if (authCheck != null) return authCheck;

            var userId = GetCurrentUserId()!.Value;

            var order = await _context.OrderTables
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.Product)
                .FirstOrDefaultAsync(o => o.Id == id && o.BuyerId == userId);

            if (order == null)
            {
                TempData["Error"] = "Không tìm thấy đơn hàng.";
                return RedirectToAction("History");
            }

            if (order.Status != "Delivered")
            {
                TempData["Error"] = "Chỉ có thể yêu cầu hoàn trả đơn hàng đã giao thành công.";
                return RedirectToAction("Details", new { id });
            }

            return View(order);
        }

        /// <summary>
        /// POST: /Order/Return/{id} - Gửi yêu cầu hoàn trả
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> Return(int id, string reason)
        {
            // Check authentication first
            var authCheck = RedirectToLoginIfNotAuthenticated();
            if (authCheck != null) return Json(new { success = false, message = "Vui lòng đăng nhập để tiếp tục." });

            var userId = GetCurrentUserId()!.Value;

            try
            {
                var order = await _context.OrderTables
                    .FirstOrDefaultAsync(o => o.Id == id && o.BuyerId == userId);

                if (order == null)
                {
                    return Json(new { success = false, message = "Không tìm thấy đơn hàng." });
                }

                if (order.Status != "Delivered")
                {
                    return Json(new { success = false, message = "Chỉ có thể yêu cầu hoàn trả đơn hàng đã giao thành công." });
                }

                // Check if return request already exists
                var existingReturn = await _context.ReturnRequests
                    .FirstOrDefaultAsync(r => r.OrderId == id);

                if (existingReturn != null)
                {
                    return Json(new { success = false, message = "Đã tồn tại yêu cầu hoàn trả cho đơn hàng này." });
                }

                var returnRequest = new ReturnRequest
                {
                    OrderId = id,
                    UserId = userId,
                    Reason = reason,
                    Status = "Pending",
                    CreatedAt = DateTime.Now
                };

                _context.ReturnRequests.Add(returnRequest);
                await _context.SaveChangesAsync();

                return Json(new { success = true, message = "Gửi yêu cầu hoàn trả thành công!" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Có lỗi xảy ra: {ex.Message}" });
            }
        }

        #endregion
    }
}