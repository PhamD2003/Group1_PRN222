using Group1_PRN222.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace Group1_PRN222.Controllers;

public class AccountController : Controller
{
    private readonly CloneEbayDbContext _context;
    private readonly IEmailSender _emailSender; 

    public AccountController(CloneEbayDbContext context, IEmailSender emailSender)
    {
        _context = context;
        _emailSender = emailSender;
    }

    [HttpGet]
    public IActionResult Register() => View();

    [HttpPost]
    public async Task<IActionResult> Register(User model)
    {
        if (!ModelState.IsValid)
            return View(model);

        if (await _context.Users.AnyAsync(u => u.Email == model.Email))
        {
            ModelState.AddModelError("", "Email already registered.");
            return View(model);
        }

        //Hash password
        model.Password = HashPassword(model);

        // Generate token
        model.EmailConfirmationToken = Guid.NewGuid().ToString();
        model.IsEmailConfirmed = 0;

        //Save data
        _context.Users.Add(model);
        await _context.SaveChangesAsync();

        // Build confirmation link
        var confirmationLink = Url.Action("ConfirmEmail", "Account",
            new { userId = model.Id, token = model.EmailConfirmationToken }, Request.Scheme);

        // Send email with confirmation link
        await _emailSender.SendEmailAsync(
            model.Email,
            "Confirm your email",
            $"Please confirm your account by <a href='{confirmationLink}'>clicking here</a>."
        );

        //Reduct to a notice page
        return RedirectToAction("ConfirmEmailNotice");
    }

    public IActionResult ConfirmEmailNotice() => View();

    [HttpGet]
    public async Task<IActionResult> ConfirmEmail(int userId, string token)
    {
        var user = await _context.Users.FindAsync(userId);
        if (user == null || user.EmailConfirmationToken != token)
            return BadRequest("Invalid confirmation link.");

        user.IsEmailConfirmed = 1;
        user.EmailConfirmationToken = null;
        await _context.SaveChangesAsync();

        return View(); // Show a "confirmed" page
    }

    [HttpGet]
    public IActionResult Login() => View();

    [HttpPost]
    public async Task<IActionResult> Login(User model)
    {
        if (!ModelState.IsValid)
            return View(model);

        //TODO: Use password hashing function here

        var pwd = HashPassword(model);

        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.Email == model.Email && u.Password == pwd);

        if (user == null)
        {
            ModelState.AddModelError("", "Invalid email or password.");
            return View(model);
        }

        if (user.IsEmailConfirmed == 0)
        {
            ModelState.AddModelError("", "Please confirm your email before logging in.");
            return View(model);
        }

        return RedirectToAction("Index", "Home");
    }

    string HashPassword(User model)
    {
        StringBuilder sb = new StringBuilder();

        using (SHA256 hash = SHA256.Create())
        {
            Encoding enc = Encoding.UTF8;
            Byte[] result = hash.ComputeHash(enc.GetBytes($"{model.Id}-{model.Password}"));

            foreach (Byte b in result)
                sb.Append(b.ToString("x2"));
        }

        return sb.ToString();
    }
}