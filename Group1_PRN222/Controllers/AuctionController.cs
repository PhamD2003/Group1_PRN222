using Group1_PRN222.Models;
using Microsoft.AspNetCore.Mvc;

namespace Group1_PRN222.Controllers
{
    public class AuctionController : Controller
    {
        private readonly CloneEbayDbContext _context;

        public AuctionController(CloneEbayDbContext context)
        {
            _context = context;
        }

        // 1. Hiển thị danh sách sản phẩm đang đấu giá
        public IActionResult Index()
        {
            var auctions = _context.Products.Where(p => p.IsAuction == true && p.AuctionEndTime > DateTime.Now).ToList();
            return View(auctions);
        }

        // 2. Xem chi tiết sản phẩm đấu giá
        public IActionResult Details(int id)
        {
            var product = _context.Products.FirstOrDefault(p => p.Id == id);
            var bids = _context.Bids.Where(b => b.ProductId == id)
                                   .OrderByDescending(b => b.Amount)
                                   .ToList();
            ViewBag.Bids = bids;
            return View(product);
        }

        // 3. Đặt giá đấu
        [HttpPost]
        public IActionResult Bid(int productId, decimal amount)
        {
            var userId = HttpContext.Session.GetInt32("UserId");

            if (userId == null)
                return RedirectToAction("Login", "Account");

            var product = _context.Products.FirstOrDefault(p => p.Id == productId);
            if (product == null || product.AuctionEndTime <= DateTime.Now)
                return BadRequest("Sản phẩm không hợp lệ hoặc đã hết hạn");

            var highestBid = _context.Bids
                .Where(b => b.ProductId == productId)
                .OrderByDescending(b => b.Amount)
                .FirstOrDefault();

            // ⚠️ Kiểm tra nếu chưa ai đặt giá → dùng StartPrice
            if (highestBid == null)
            {
                if (amount < product.StartPrice)
                {
                    TempData["Error"] = $"Giá phải tối thiểu từ giá khởi điểm: {product.StartPrice?.ToString("N0")} VNĐ";
                    return RedirectToAction("Details", new { id = productId });
                }
            }
            else
            {
                if (amount <= highestBid.Amount)
                {
                    TempData["Error"] = $"Giá phải cao hơn giá hiện tại: {highestBid.Amount?.ToString("N0")} VNĐ";
                    return RedirectToAction("Details", new { id = productId });
                }
            }

            var bid = new Bid
            {
                ProductId = productId,
                BidderId = userId.Value,
                Amount = amount,
                BidTime = DateTime.Now
            };

            _context.Bids.Add(bid);
            _context.SaveChanges();

            return RedirectToAction("Details", new { id = productId });
        }



        // 4. Lịch sử đấu giá của bản thân
        public IActionResult MyBids()
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null) return RedirectToAction("Login", "User");

            var myBids = _context.Bids
                .Where(b => b.BidderId == userId)
                .OrderByDescending(b => b.BidTime)
                .ToList();

            return View(myBids);
        }

        // 5. Sản phẩm đã thắng
        public IActionResult WonAuctions()
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null) return RedirectToAction("Login", "User");

            var won = _context.Products
                .Where(p => p.IsAuction == true && p.AuctionEndTime < DateTime.Now)
                .Where(p =>
                    _context.Bids
                    .Where(b => b.ProductId == p.Id)
                    .OrderByDescending(b => b.Amount)
                    .FirstOrDefault().BidderId == userId)
                .ToList();

            return View(won);
        }
    }
}
