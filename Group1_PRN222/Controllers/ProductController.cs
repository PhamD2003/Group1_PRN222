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
        public async Task<IActionResult> Index(string search, string category, int page = 1, int pageSize = 5)
        {
            var query = _context.Products
                .Include(p => p.Category)
                .Include(p => p.Inventories) // THÊM DÒNG NÀY ĐỂ TẢI DỮ LIỆU TỒN KHO
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

            return View(products);
        }


        // GET: /Product/Details/5
        public IActionResult Details(int id, int? rating)
        {
            var product = _context.Products
                .Include(p => p.Category)
                .Include(p => p.Reviews)
                .Include(p => p.Inventories) // THÊM DÒNG NÀY ĐỂ TẢI DỮ LIỆU TỒN KHO
                .FirstOrDefault(p => p.Id == id);

            if (product == null) return NotFound();

            // Lọc đánh giá (nếu có tham số rating)
            if (rating.HasValue)
            {
                product.Reviews = product.Reviews
                    .Where(r => r.Rating == rating.Value)
                    .ToList();
            }
            // Sắp xếp đánh giá để hiển thị mới nhất trước
            if (product.Reviews != null)
            {
                product.Reviews = product.Reviews.OrderByDescending(r => r.CreatedAt).ToList();
            }

            // Sản phẩm tương tự
            var relatedProducts = _context.Products
                .Where(p => p.CategoryId == product.CategoryId && p.Id != product.Id)
                .Include(p => p.Inventories) // CŨNG THÊM DÒNG NÀY CHO SẢN PHẨM TƯƠNG TỰ NẾU BẠN MUỐN HIỂN THỊ TỒN KHO CỦA CHÚNG
                .Take(4)
                .ToList();

            ViewBag.Related = relatedProducts;

            return View(product);
        }
    }
}