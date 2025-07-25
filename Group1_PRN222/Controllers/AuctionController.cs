using Group1_PRN222.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

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
            var bids = _context.Bids
        .Include(b => b.Bidder) // Nạp thêm thông tin User (Bidder)
        .Where(b => b.ProductId == id)
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
            if (product == null)
            {
                TempData["Error"] = "Sản phẩm không tồn tại.";
                return RedirectToAction("Details", new { id = productId });
            }

            if (product.AuctionEndTime <= DateTime.Now)
            {
                TempData["Error"] = "Phiên đấu giá đã kết thúc.";
                return RedirectToAction("Details", new { id = productId });
            }

            var highestBid = _context.Bids
                .Where(b => b.ProductId == productId)
                .OrderByDescending(b => b.Amount)
                .FirstOrDefault();

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
                .Include(b => b.Product)
                    .ThenInclude(p => p.Bids)
                .Where(b => b.BidderId == userId)
                .OrderByDescending(b => b.BidTime)
                .ToList();

            // Lấy danh sách sản phẩm đã thắng
            var wonProducts = _context.Products
                .Where(p => p.IsAuction == true && p.AuctionEndTime < DateTime.Now)
                .Include(p => p.Bids)
                .ToList()
                .Where(p => p.Bids.OrderByDescending(b => b.Amount).FirstOrDefault()?.BidderId == userId)
                .ToList();

            ViewBag.WonProducts = wonProducts;

            return View(myBids);
        }


    }
}
