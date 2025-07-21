using Microsoft.AspNetCore.Mvc;
using Group1_PRN222.Models;
using Microsoft.EntityFrameworkCore;

namespace Group1_PRN222.Controllers
{
    public class ReviewController : Controller
    {
        private readonly CloneEbayDbContext _context;

        public ReviewController(CloneEbayDbContext context)
        {
            _context = context;
        }

        // GET: Review/Create?productId=5
        public IActionResult Create(int productId)
        {
            TempData["ProductId"] = productId;
            return View();
        }

        // POST: Review/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Rating,Comment")] Review review)
        {
            // Lấy ProductId từ TempData và chuyển sang int?
            if (TempData["ProductId"] != null && int.TryParse(TempData["ProductId"].ToString(), out var productId))
            {
                review.ProductId = productId;
            }
            else
            {
                ModelState.AddModelError("", "ProductId không hợp lệ.");
                return View(review);
            }

            // Gán ReviewerId tạm thời (ví dụ người dùng có ID = 1, bạn nên dùng user đăng nhập thực tế)
            review.ReviewerId = 1;

            review.CreatedAt = DateTime.Now;

            if (ModelState.IsValid)
            {
                _context.Add(review);
                await _context.SaveChangesAsync();
                return RedirectToAction("Details", "Product", new { id = review.ProductId });
            }

            return View(review);
        }
    }
}
