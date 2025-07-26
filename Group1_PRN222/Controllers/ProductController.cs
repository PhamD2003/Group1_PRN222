using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System; // Thêm namespace này cho Math.Ceiling
using System.Linq;
using System.Threading.Tasks;
using Group1_PRN222.Models;

namespace Group1_PRN222.Controllers
{
    public class ProductController : Controller
    {
        private readonly CloneEbayDbContext _context;

        public ProductController(CloneEbayDbContext context)
        {
            _context = context;
        }

        // GET: /Product
        public async Task<IActionResult> Index(string search, string category, int page = 1, int pageSize = 5, bool isAuction = false)
        {
            var query = _context.Products
                .Include(p => p.Category)
                .Include(p => p.Inventories)
                .AsQueryable();

            // Tìm kiếm
            if (!string.IsNullOrEmpty(search))
            {
                query = query.Where(p => p.Title.Contains(search));
            }

            // Lọc theo danh mục
            if (!string.IsNullOrEmpty(category))
            {
                query = query.Where(p => p.Category.Name == category);
            }
            if (isAuction)
            {
                query = query.Where(p => _context.Bids.Any(b => b.ProductId == p.Id));
            }
            // Tổng số sản phẩm
            int totalProducts = await query.CountAsync();

            // Phân trang
            var products = await query
                .OrderBy(p => p.Id)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            // ViewBag dùng cho phân trang và lọc
            ViewBag.TotalPages = (int)Math.Ceiling((double)totalProducts / pageSize);
            ViewBag.CurrentPage = page;
            ViewBag.Category = category;
            ViewBag.Search = search;
            ViewBag.Categories = await _context.Categories.ToListAsync();
            ViewBag.IsAuction = isAuction;
            ViewBag.AuctionProducts = await _context.Products.Include(p => p.Category)
                                                             .Include(p => p.Inventories)
                                                             .Where(p => _context.Bids.Any(b => b.ProductId == p.Id)) // hoặc p.IsAuction == true nếu có field đó
                                                             .OrderByDescending(p => p.Id)
                                                             .Take(5)
                                                             .ToListAsync();
            return View(products);
        }


        // GET: /Product/Details/5
        public IActionResult Details(int id, int? rating)
        {
            var product = _context.Products
                .Include(p => p.Category)
                .Include(p => p.Inventories)
                .Include(p => p.Reviews)
                .FirstOrDefault(p => p.Id == id);

            if (product == null) return NotFound();

            var bids = _context.Bids
                .Where(b => b.ProductId == id)
                .OrderByDescending(b => b.Amount)
                .ToList();

            ViewBag.Bids = bids;
            ViewBag.CurrentPrice = bids.FirstOrDefault()?.Amount ?? product.StartPrice;

            // Lọc đánh giá nếu có rating
            if (rating.HasValue)
            {
                product.Reviews = product.Reviews
                    .Where(r => r.Rating == rating.Value)
                    .OrderByDescending(r => r.CreatedAt)
                    .ToList();
            }
            else
            {
                product.Reviews = product.Reviews
                    .OrderByDescending(r => r.CreatedAt)
                    .ToList();
            }

            // Gán lại ViewBag nếu có liên quan
            ViewBag.Related = _context.Products
                .Where(p => p.CategoryId == product.CategoryId && p.Id != id)
                .Include(p => p.Inventories)
                .Take(4)
                .ToList();

            return View(product);
        }


    }
}