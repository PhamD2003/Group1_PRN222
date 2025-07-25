// Services/CouponService.cs - Centralized Coupon Management
using Group1_PRN222.Models;
using Microsoft.EntityFrameworkCore;

namespace Group1_PRN222.Services
{
    public interface ICouponService
    {
        Task<CouponValidationResult> ValidateCouponAsync(string couponCode, List<CartItem> cartItems);
        Task<decimal> CalculateDiscountAsync(string couponCode, List<CartItem> cartItems);
        Task<List<Coupon>> GetActiveCouponsAsync();
        Task<bool> IsCouponValidAsync(Coupon coupon);
    }

    public class CouponService : ICouponService
    {
        private readonly CloneEbayDbContext _context;

        public CouponService(CloneEbayDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Validate coupon and return detailed result
        /// </summary>
        public async Task<CouponValidationResult> ValidateCouponAsync(string couponCode, List<CartItem> cartItems)
        {
            if (string.IsNullOrWhiteSpace(couponCode))
            {
                return new CouponValidationResult
                {
                    IsValid = false,
                    ErrorMessage = "Vui lòng nhập mã giảm giá."
                };
            }

            // Find coupon by code
            var coupon = await _context.Coupons
                .Include(c => c.Product)
                .FirstOrDefaultAsync(c => c.Code.ToLower() == couponCode.ToLower());

            if (coupon == null)
            {
                return new CouponValidationResult
                {
                    IsValid = false,
                    ErrorMessage = "Mã giảm giá không tồn tại."
                };
            }

            // Check if coupon is active
            if (!await IsCouponValidAsync(coupon))
            {
                return new CouponValidationResult
                {
                    IsValid = false,
                    ErrorMessage = "Mã giảm giá đã hết hạn hoặc chưa có hiệu lực."
                };
            }

            // Check if coupon applies to cart items
            if (coupon.ProductId.HasValue)
            {
                // Product-specific coupon
                var targetProduct = cartItems.FirstOrDefault(item => item.ProductId == coupon.ProductId.Value);
                if (targetProduct == null)
                {
                    return new CouponValidationResult
                    {
                        IsValid = false,
                        ErrorMessage = $"Mã giảm giá này chỉ áp dụng cho sản phẩm '{coupon.Product?.Title}' không có trong giỏ hàng."
                    };
                }

                return new CouponValidationResult
                {
                    IsValid = true,
                    Coupon = coupon,
                    ApplicableItems = new List<CartItem> { targetProduct },
                    DiscountAmount = CalculateProductDiscount(targetProduct, coupon.DiscountPercent ?? 0),
                    SuccessMessage = $"Áp dụng giảm {coupon.DiscountPercent}% cho sản phẩm '{coupon.Product?.Title}'"
                };
            }
            else
            {
                // Order-wide coupon
                decimal discountAmount = CalculateOrderDiscount(cartItems, coupon.DiscountPercent ?? 0);

                return new CouponValidationResult
                {
                    IsValid = true,
                    Coupon = coupon,
                    ApplicableItems = cartItems,
                    DiscountAmount = discountAmount,
                    SuccessMessage = $"Áp dụng giảm {coupon.DiscountPercent}% cho toàn bộ đơn hàng"
                };
            }
        }

        /// <summary>
        /// Calculate discount amount for given coupon and cart items
        /// </summary>
        public async Task<decimal> CalculateDiscountAsync(string couponCode, List<CartItem> cartItems)
        {
            var validationResult = await ValidateCouponAsync(couponCode, cartItems);
            return validationResult.IsValid ? validationResult.DiscountAmount : 0;
        }

        /// <summary>
        /// Get all active coupons
        /// </summary>
        public async Task<List<Coupon>> GetActiveCouponsAsync()
        {
            var now = DateTime.Now;
            return await _context.Coupons
                .Where(c => c.StartDate <= now && c.EndDate >= now)
                .Include(c => c.Product)
                .OrderBy(c => c.Code)
                .ToListAsync();
        }

        /// <summary>
        /// Check if coupon is valid (date range and usage limits)
        /// </summary>
        public async Task<bool> IsCouponValidAsync(Coupon coupon)
        {
            var now = DateTime.Now;

            // Check date range
            if (coupon.StartDate.HasValue && now < coupon.StartDate.Value)
                return false;

            if (coupon.EndDate.HasValue && now > coupon.EndDate.Value)
                return false;

            // TODO: Check usage limits if you implement usage tracking
            // if (coupon.MaxUsage.HasValue && currentUsage >= coupon.MaxUsage.Value)
            //     return false;

            return true;
        }

        #region Private Helper Methods

        /// <summary>
        /// Calculate discount for a specific product
        /// </summary>
        private decimal CalculateProductDiscount(CartItem item, decimal discountPercent)
        {
            return item.TotalPrice * discountPercent / 100;
        }

        /// <summary>
        /// Calculate discount for entire order
        /// </summary>
        private decimal CalculateOrderDiscount(List<CartItem> cartItems, decimal discountPercent)
        {
            var subtotal = cartItems.Sum(item => item.TotalPrice);
            return subtotal * discountPercent / 100;
        }

        #endregion
    }

    /// <summary>
    /// Result of coupon validation
    /// </summary>
    public class CouponValidationResult
    {
        public bool IsValid { get; set; }
        public string? ErrorMessage { get; set; }
        public string? SuccessMessage { get; set; }
        public Coupon? Coupon { get; set; }
        public List<CartItem>? ApplicableItems { get; set; }
        public decimal DiscountAmount { get; set; }
        public decimal DiscountPercent => Coupon?.DiscountPercent ?? 0;
    }

    /// <summary>
    /// Coupon application summary for order
    /// </summary>
    public class CouponApplicationSummary
    {
        public string CouponCode { get; set; } = string.Empty;
        public decimal OriginalAmount { get; set; }
        public decimal DiscountAmount { get; set; }
        public decimal FinalAmount { get; set; }
        public string Description { get; set; } = string.Empty;
        public bool IsProductSpecific { get; set; }
        public int? ProductId { get; set; }
        public string? ProductName { get; set; }
    }
}