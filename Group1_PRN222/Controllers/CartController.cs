using Microsoft.AspNetCore.Mvc;
using Group1_PRN222.Models;
using Microsoft.EntityFrameworkCore;
using System.Text.Json; // Để serialize/deserialize JSON cho Session

namespace Group1_PRN222.Controllers
{
    public class CartController : Controller
    {
        private readonly CloneEbayDbContext _context;
        private const string CART_SESSION_KEY = "Cart";
        private const string COUPON_SESSION_KEY = "AppliedCoupon";

        public CartController(CloneEbayDbContext context)
        {
            _context = context;
        }

        // Lấy giỏ hàng từ Session
        private List<CartItem> GetCartItems()
        {
            var cartJson = HttpContext.Session.GetString(CART_SESSION_KEY);
            if (cartJson == null)
            {
                return new List<CartItem>();
            }
            // Không truyền options
            return JsonSerializer.Deserialize<List<CartItem>>(cartJson) ?? new List<CartItem>();
        }

        // Lưu giỏ hàng vào Session
        private void SaveCartItems(List<CartItem> cartItems)
        {
            // Không truyền options
            var cartJson = JsonSerializer.Serialize(cartItems);
            HttpContext.Session.SetString(CART_SESSION_KEY, cartJson);
        }

        // --- HÀNH ĐỘNG HIỂN THỊ GIỎ HÀNG ---
        public async Task<IActionResult> Index()
        {
            var cartItems = GetCartItems();
            // Lấy lại thông tin Product đầy đủ cho từng CartItem để hiển thị
            foreach (var item in cartItems)
            {
                // Chỉ lấy các thuộc tính cần thiết, tránh navigation property gây chu trình
                item.Product = await _context.Products
                                            // .Include(p => p.Inventories) // Có thể không cần include nếu chỉ hiển thị thông tin sản phẩm
                                            .FirstOrDefaultAsync(p => p.Id == item.ProductId);
                // Cập nhật lại thông tin nếu cần (tên, giá, ảnh,...)
                if (item.Product != null)
                {
                    item.ProductName = item.Product.Title ?? "Unknown Product";
                    item.Price = item.Product.Price ?? 0M;
                    item.ImageUrl = item.Product.Images ?? "/images/default.png";
                }
            }
            decimal subtotal = cartItems.Sum(item => item.TotalPrice);
            decimal discount = 0;
            Coupon? appliedCoupon = null;

            string? couponCode = HttpContext.Session.GetString(COUPON_SESSION_KEY);
            if (!string.IsNullOrEmpty(couponCode))
            {
                appliedCoupon = await _context.Coupons.FirstOrDefaultAsync(c => c.Code == couponCode);
                if (appliedCoupon != null && IsCouponValid(appliedCoupon))
                {
                    if (appliedCoupon.ProductId.HasValue)
                    {
                        // Giảm giá theo từng sản phẩm cụ thể
                        var targetItem = cartItems.FirstOrDefault(item => item.ProductId == appliedCoupon.ProductId.Value);
                        if (targetItem != null)
                        {
                            discount = targetItem.TotalPrice * (appliedCoupon.DiscountPercent ?? 0) / 100;
                        }
                    }
                    else
                    {
                        // Giảm giá toàn bộ đơn hàng
                        discount = subtotal * (appliedCoupon.DiscountPercent ?? 0) / 100;
                    }
                }
                else
                {
                    // Nếu mã giảm giá không hợp lệ nữa, xoá khỏi session
                    HttpContext.Session.Remove(COUPON_SESSION_KEY);
                }
            }


            // Lấy thông tin mã giảm giá đã áp dụng (nếu có)


            ViewBag.CartItems = cartItems;
            ViewBag.Subtotal = subtotal;
            ViewBag.Discount = discount;
            ViewBag.Total = subtotal - discount;
            ViewBag.AppliedCoupon = appliedCoupon; // Truyền đối tượng coupon để hiển thị chi tiết

            return View();
        }

        // --- HÀNH ĐỘNG THÊM SẢN PHẨM VÀO GIỎ HÀNG ---
        [HttpPost]
        [Route("/Cart/Add/{productId:int}")]
        public async Task<IActionResult> Add(int productId, int quantity = 1)
        {
            // Khi lấy Product, bạn không Include các navigation property gây chu trình
            // Hoặc chỉ chọn ra những thuộc tính cần thiết bằng .Select()
            var product = await _context.Products
                                        .Include(p => p.Inventories) // Vẫn cần Include Inventories để kiểm tra tồn kho
                                        .FirstOrDefaultAsync(p => p.Id == productId);

            if (product == null)
            {
                TempData["ErrorMessage"] = "Sản phẩm không tồn tại.";
                return RedirectToAction("Index", "Product");
            }

            var availableQuantity = product.Inventories.FirstOrDefault()?.Quantity ?? 0;

            if (availableQuantity < quantity)
            {
                TempData["ErrorMessage"] = $"Chỉ còn {availableQuantity} sản phẩm trong kho cho '{product.Title}'.";
                return RedirectToAction("Details", "Product", new { id = productId });
            }

            var cartItems = GetCartItems();
            var existingItem = cartItems.FirstOrDefault(item => item.ProductId == productId);

            if (existingItem != null)
            {
                if (existingItem.Quantity + quantity > availableQuantity)
                {
                    TempData["ErrorMessage"] = $"Không đủ số lượng. Bạn đã có {existingItem.Quantity} sản phẩm này trong giỏ. Chỉ còn {availableQuantity - existingItem.Quantity} để thêm.";
                    return RedirectToAction("Details", "Product", new { id = productId });
                }
                existingItem.Quantity += quantity;
            }
            else
            {
                cartItems.Add(new CartItem
                {
                    ProductId = product.Id,
                    ProductName = product.Title ?? "Unknown Product",
                    Price = product.Price ?? 0M,
                    Quantity = quantity,
                    ImageUrl = product.Images ?? "/images/default.png",
                });
            }

            SaveCartItems(cartItems);
            TempData["SuccessMessage"] = $"Đã thêm {quantity} x '{product.Title}' vào giỏ hàng.";
            return RedirectToAction("Index");
        }

        // --- HÀNH ĐỘNG CẬP NHẬT SỐ LƯỢNG ---
        [HttpPost]
        public async Task<IActionResult> UpdateQuantity(int productId, int newQuantity)
        {
            var cartItems = GetCartItems();
            var itemToUpdate = cartItems.FirstOrDefault(item => item.ProductId == productId);

            if (itemToUpdate == null)
            {
                TempData["ErrorMessage"] = "Sản phẩm không tồn tại trong giỏ hàng.";
                return RedirectToAction("Index");
            }

            if (newQuantity <= 0)
            {
                cartItems.Remove(itemToUpdate);
                TempData["SuccessMessage"] = $"Đã xóa '{itemToUpdate.ProductName}' khỏi giỏ hàng.";
            }
            else
            {
                var product = await _context.Products.Include(p => p.Inventories).FirstOrDefaultAsync(p => p.Id == productId);
                var availableQuantity = product?.Inventories.FirstOrDefault()?.Quantity ?? 0;

                if (newQuantity > availableQuantity)
                {
                    TempData["ErrorMessage"] = $"Chỉ còn {availableQuantity} sản phẩm trong kho cho '{itemToUpdate.ProductName}'. Không thể cập nhật lên {newQuantity}.";
                    return RedirectToAction("Index");
                }

                itemToUpdate.Quantity = newQuantity;
                TempData["SuccessMessage"] = $"Đã cập nhật số lượng của '{itemToUpdate.ProductName}' thành {newQuantity}.";
            }

            SaveCartItems(cartItems);
            return RedirectToAction("Index");
        }

        // --- HÀNH ĐỘNG XÓA SẢN PHẨM KHỎI GIỎ HÀNG ---
        [HttpPost]
        public IActionResult Remove(int productId)
        {
            var cartItems = GetCartItems();
            var itemToRemove = cartItems.FirstOrDefault(item => item.ProductId == productId);

            if (itemToRemove != null)
            {
                cartItems.Remove(itemToRemove);
                SaveCartItems(cartItems);
                TempData["SuccessMessage"] = $"Đã xóa '{itemToRemove.ProductName}' khỏi giỏ hàng.";
            }
            else
            {
                TempData["ErrorMessage"] = "Sản phẩm không tồn tại trong giỏ hàng.";
            }

            return RedirectToAction("Index");
        }

        // --- HÀNH ĐỘNG ÁP DỤNG MÃ GIẢM GIÁ ---
        [HttpPost]
        public async Task<IActionResult> ApplyCoupon(string couponCode)
        {
            if (string.IsNullOrEmpty(couponCode))
            {
                TempData["ErrorMessage"] = "Vui lòng nhập mã giảm giá.";
                return RedirectToAction("Index");
            }

            var coupon = await _context.Coupons.FirstOrDefaultAsync(c => c.Code == couponCode);

            if (coupon == null)
            {
                TempData["ErrorMessage"] = "Mã giảm giá không hợp lệ.";
                HttpContext.Session.Remove(COUPON_SESSION_KEY); // Xóa mã cũ nếu có
                return RedirectToAction("Index");
            }

            // Kiểm tra tính hợp lệ của mã giảm giá
            if (!IsCouponValid(coupon))
            {
                TempData["ErrorMessage"] = "Mã giảm giá đã hết hạn hoặc chưa có hiệu lực.";
                HttpContext.Session.Remove(COUPON_SESSION_KEY);
                return RedirectToAction("Index");
            }

            // Kiểm tra xem mã giảm giá có áp dụng cho sản phẩm cụ thể trong giỏ hàng không
            var cartItems = GetCartItems();
            bool couponAppliesToCart = false;

            if (coupon.ProductId.HasValue) // Mã giảm giá áp dụng cho một sản phẩm cụ thể
            {
                if (cartItems.Any(item => item.ProductId == coupon.ProductId.Value))
                {
                    couponAppliesToCart = true;
                }
                else
                {
                    TempData["ErrorMessage"] = $"Mã giảm giá này chỉ áp dụng cho sản phẩm khác, không có trong giỏ hàng của bạn.";
                    HttpContext.Session.Remove(COUPON_SESSION_KEY);
                    return RedirectToAction("Index");
                }
            }
            else // Mã giảm giá áp dụng cho toàn bộ đơn hàng
            {
                couponAppliesToCart = true;
            }

            if (couponAppliesToCart)
            {
                HttpContext.Session.SetString(COUPON_SESSION_KEY, couponCode);
                TempData["SuccessMessage"] = $"Đã áp dụng mã giảm giá '{couponCode}' thành công!";
            }
            else
            {
                TempData["ErrorMessage"] = "Mã giảm giá không áp dụng cho các sản phẩm trong giỏ hàng của bạn.";
                HttpContext.Session.Remove(COUPON_SESSION_KEY);
            }

            return RedirectToAction("Index");
        }

        // --- HÀNH ĐỘNG XÓA MÃ GIẢM GIÁ ---
        [HttpPost]
        public IActionResult RemoveCoupon()
        {
            HttpContext.Session.Remove(COUPON_SESSION_KEY);
            TempData["SuccessMessage"] = "Đã gỡ bỏ mã giảm giá.";
            return RedirectToAction("Index");
        }


        // --- HÀM HỖ TRỢ KIỂM TRA MÃ GIẢM GIÁ ---
        private bool IsCouponValid(Coupon coupon)
        {
            var now = DateTime.Now;
            bool isValid = now >= coupon.StartDate && now <= coupon.EndDate;
            // Thêm kiểm tra MaxUsage nếu bạn có trường này và muốn giới hạn số lần sử dụng
            // if (coupon.MaxUsage.HasValue && coupon.UsageCount >= coupon.MaxUsage.Value) isValid = false;
            return isValid;
        }
    }
}