using Group1_PRN222.Hubs;
using Group1_PRN222.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace Group1_PRN222.Controllers
{
    public class AuctionController : Controller
    {
        private readonly CloneEbayDbContext _context;
        private readonly IHubContext<AuctionHub> _hubContext;
        public AuctionController(CloneEbayDbContext context, IHubContext<AuctionHub> hubContext)
        {
            _context = context;
            _hubContext = hubContext;
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
        public async Task<IActionResult> Bid(int productId, decimal amount)
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null) return RedirectToAction("Login", "Account");

            var user = _context.Users.FirstOrDefault(u => u.Id == userId);
            var product = _context.Products.FirstOrDefault(p => p.Id == productId);
            if (product == null || product.AuctionEndTime <= DateTime.Now)
            {
                TempData["Error"] = "Sản phẩm không tồn tại hoặc đã kết thúc.";
                return RedirectToAction("Details", new { id = productId });
            }

            var highestBid = _context.Bids
                .Where(b => b.ProductId == productId)
                .OrderByDescending(b => b.Amount)
                .FirstOrDefault();

            if ((highestBid == null && amount < product.StartPrice) ||
                (highestBid != null && amount <= highestBid.Amount))
            {
                TempData["Error"] = "Giá đấu không hợp lệ.";
                return RedirectToAction("Details", new { id = productId });
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

            await _hubContext.Clients
                .Group($"Product_{productId}")
                .SendAsync("ReceiveBid", user.Username, amount);

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
