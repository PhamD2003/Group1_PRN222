// Controllers/OrderController.cs - Updated for Cart Integration
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Group1_PRN222.Models;
using System.Text.Json;

namespace Group1_PRN222.Controllers
{
    public class OrderController : Controller
    {
        private readonly CloneEbayDbContext _context;
        private const string CART_SESSION_KEY = "Cart";

        public OrderController(CloneEbayDbContext context)
        {
            _context = context;
        }

        #region Mock Methods (Temporary - sẽ thay thế khi có authentication)

        /// <summary>
        /// Mock user ID - thay thế khi có authentication từ Member 1
        /// </summary>
        private int GetCurrentUserId() => 1; // TODO: Replace with real authentication

        /// <summary>
        /// Mock address - thay thế khi có address management từ Member 1
        /// </summary>
        private Address GetMockDefaultAddress()
        {
            return new Address
            {
                Id = 1,
                UserId = 1,
                FullName = "Nguyen Van Test",
                Phone = "0123456789",
                Street = "123 Test Street",
                City = "Ho Chi Minh",
                State = "Ho Chi Minh",
                Country = "Vietnam",
                IsDefault = true
            };
        }

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

        #endregion

        #region Main Order Workflow (Tích hợp với Cart của Member 2)

        /// <summary>
        /// GET: /Order/Checkout - Checkout từ Cart (URL từ button "Tiến hành thanh toán")
        /// </summary>
        public async Task<IActionResult> Checkout()
        {
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

            // Tính toán chi phí
            decimal subtotal = cartItems.Sum(item => item.TotalPrice);
            decimal shippingFee = CalculateShippingFee(cartItems);
            decimal total = subtotal + shippingFee;

            ViewBag.CartItems = cartItems;
            ViewBag.Subtotal = subtotal;
            ViewBag.ShippingFee = shippingFee;
            ViewBag.Total = total;
            ViewBag.DefaultAddress = GetMockDefaultAddress();

            return View();
        }

        /// <summary>
        /// POST: /Order/Checkout - Xử lý checkout và tạo order
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> Checkout(int addressId, string paymentMethod, string notes = "")
        {
            try
            {
                var cartItems = GetCartItemsFromSession();

                if (!cartItems.Any())
                {
                    return Json(new { success = false, message = "Giỏ hàng trống." });
                }

                // Tính toán lại để đảm bảo accuracy
                decimal subtotal = cartItems.Sum(item => item.TotalPrice);
                decimal shippingFee = CalculateShippingFee(cartItems);
                decimal total = subtotal + shippingFee;

                // Tạo đơn hàng
                var order = new OrderTable
                {
                    BuyerId = GetCurrentUserId(),
                    AddressId = addressId,
                    OrderDate = DateTime.Now,
                    TotalPrice = total,
                    Status = "Pending"
                };

                _context.OrderTables.Add(order);
                await _context.SaveChangesAsync();

                // Tạo OrderItems và cập nhật tồn kho
                foreach (var item in cartItems)
                {
                    // Tạo OrderItem
                    var orderItem = new OrderItem
                    {
                        OrderId = order.Id,
                        ProductId = item.ProductId,
                        Quantity = item.Quantity,
                        UnitPrice = item.Price
                    };

                    _context.OrderItems.Add(orderItem);

                    // Cập nhật tồn kho
                    var inventory = await _context.Inventories
                        .FirstOrDefaultAsync(i => i.ProductId == item.ProductId);

                    if (inventory != null)
                    {
                        inventory.Quantity -= item.Quantity;
                        inventory.LastUpdated = DateTime.Now;
                    }
                }

                await _context.SaveChangesAsync();

                // Clear cart sau khi đặt hàng thành công
                ClearCart();

                return Json(new {
                    success = true,
                    message = "Đặt hàng thành công!",
                    orderId = order.Id,
                    redirectUrl = Url.Action("Payment", new { orderId = order.Id, method = paymentMethod })
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Có lỗi xảy ra: " + ex.Message });
            }
        }

        /// <summary>
        /// Calculate shipping fee based on cart items
        /// </summary>
        private decimal CalculateShippingFee(List<CartItem> cartItems)
        {
            // Logic tính phí ship - có thể phức tạp hóa sau
            decimal totalValue = cartItems.Sum(item => item.TotalPrice);

            if (totalValue >= 500000) // Miễn phí ship cho đơn >= 500k
                return 0;

            return 30000; // Phí ship cố định 30k
        }

        #endregion

        #region Payment Processing

        /// <summary>
        /// GET: /Order/Payment/{orderId} - Trang thanh toán
        /// </summary>
        public async Task<IActionResult> Payment(int orderId, string method = "")
        {
            var userId = GetCurrentUserId();

            var order = await _context.OrderTables
                .Where(o => o.Id == orderId && o.BuyerId == userId)
                .Include(o => o.OrderItems)
                .ThenInclude(oi => oi.Product)
                .Include(o => o.Address)
                .FirstOrDefaultAsync();

            if (order == null)
            {
                TempData["Error"] = "Không tìm thấy đơn hàng.";
                return RedirectToAction("History");
            }

            ViewBag.PreselectedMethod = method;
            return View(order);
        }

        /// <summary>
        /// POST: /Order/ProcessPayment - Xử lý thanh toán
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> ProcessPayment(int orderId, string paymentMethod)
        {
            try
            {
                var userId = GetCurrentUserId();
                var order = await _context.OrderTables
                    .FirstOrDefaultAsync(o => o.Id == orderId && o.BuyerId == userId);

                if (order == null)
                {
                    return Json(new { success = false, message = "Không tìm thấy đơn hàng." });
                }

                // Tạo Payment record
                var payment = new Payment
                {
                    OrderId = orderId,
                    UserId = userId,
                    Amount = order.TotalPrice,
                    Method = paymentMethod,
                    Status = paymentMethod == "COD" ? "Pending" : "Processing",
                    PaidAt = paymentMethod == "COD" ? null : DateTime.Now
                };

                _context.Payments.Add(payment);

                // Cập nhật order status
                order.Status = paymentMethod == "COD" ? "Confirmed" : "Processing";

                await _context.SaveChangesAsync();

                // Simulate payment processing time for PayPal
                if (paymentMethod == "PayPal")
                {
                    await Task.Delay(2000); // Simulate API call

                    payment.Status = "Completed";
                    payment.PaidAt = DateTime.Now;
                    order.Status = "Confirmed";

                    await _context.SaveChangesAsync();
                }

                return Json(new {
                    success = true,
                    message = $"Thanh toán {paymentMethod} thành công!",
                    redirectUrl = Url.Action("Confirmation", new { orderId = orderId })
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Lỗi thanh toán: " + ex.Message });
            }
        }

        /// <summary>
        /// GET: /Order/Confirmation/{orderId} - Xác nhận đơn hàng
        /// </summary>
        public async Task<IActionResult> Confirmation(int orderId)
        {
            var userId = GetCurrentUserId();

            var order = await _context.OrderTables
                .Where(o => o.Id == orderId && o.BuyerId == userId)
                .Include(o => o.OrderItems)
                .ThenInclude(oi => oi.Product)
                .Include(o => o.Address)
                .Include(o => o.Payments)
                .FirstOrDefaultAsync();

            if (order == null)
            {
                TempData["Error"] = "Không tìm thấy đơn hàng.";
                return RedirectToAction("History");
            }

            return View(order);
        }

        #endregion

        #region Order Management

        /// <summary>
        /// GET: /Order/History - Lịch sử đơn hàng
        /// </summary>
        public async Task<IActionResult> History(int page = 1, int pageSize = 10)
        {
            var userId = GetCurrentUserId();

            var query = _context.OrderTables
                .Where(o => o.BuyerId == userId)
                .Include(o => o.OrderItems)
                .ThenInclude(oi => oi.Product)
                .Include(o => o.Address)
                .Include(o => o.Payments)
                .Include(o => o.ShippingInfos)
                .OrderByDescending(o => o.OrderDate);

            var totalOrders = await query.CountAsync();
            var orders = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = (int)Math.Ceiling((double)totalOrders / pageSize);
            ViewBag.TotalOrders = totalOrders;

            return View(orders);
        }

        /// <summary>
        /// GET: /Order/Details/{orderId} - Chi tiết đơn hàng
        /// </summary>
        public async Task<IActionResult> Details(int orderId)
        {
            var userId = GetCurrentUserId();

            var order = await _context.OrderTables
                .Where(o => o.Id == orderId && o.BuyerId == userId)
                .Include(o => o.OrderItems)
                .ThenInclude(oi => oi.Product)
                .ThenInclude(p => p.Category)
                .Include(o => o.Address)
                .Include(o => o.Payments)
                .Include(o => o.ShippingInfos)
                .Include(o => o.ReturnRequests)
                .FirstOrDefaultAsync();

            if (order == null)
            {
                TempData["Error"] = "Không tìm thấy đơn hàng.";
                return RedirectToAction("History");
            }

            return View(order);
        }

        /// <summary>
        /// POST: /Order/Cancel/{orderId} - Hủy đơn hàng
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> Cancel(int orderId)
        {
            try
            {
                var userId = GetCurrentUserId();

                var order = await _context.OrderTables
                    .Where(o => o.Id == orderId && o.BuyerId == userId)
                    .Include(o => o.OrderItems)
                    .FirstOrDefaultAsync();

                if (order == null)
                {
                    return Json(new { success = false, message = "Không tìm thấy đơn hàng." });
                }

                if (order.Status != "Pending" && order.Status != "Confirmed")
                {
                    return Json(new { success = false, message = "Không thể hủy đơn hàng này." });
                }

                // Hoàn lại tồn kho
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

                order.Status = "Cancelled";
                await _context.SaveChangesAsync();

                return Json(new { success = true, message = "Đã hủy đơn hàng thành công." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Có lỗi xảy ra: " + ex.Message });
            }
        }

        #endregion
    }
}