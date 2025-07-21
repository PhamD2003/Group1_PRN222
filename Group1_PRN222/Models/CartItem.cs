using System;
using System.ComponentModel.DataAnnotations;

namespace Group1_PRN222.Models
{
    public class CartItem
    {
        [Key]
        public int ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public int Quantity { get; set; }
        public string ImageUrl { get; set; } = string.Empty;

        // Thuộc tính để tính tổng tiền cho từng mặt hàng
        public decimal TotalPrice => Price * Quantity;

        // Để lưu thông tin chi tiết của sản phẩm, tiện cho việc hiển thị
        public Product? Product { get; set; }
    }
}