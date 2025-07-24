using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Group1_PRN222.Models;

namespace Group1_PRN222.Controllers;

public class AddressesController : Controller
{
    private readonly CloneEbayDbContext _context;

    public AddressesController(CloneEbayDbContext context)
    {
        _context = context;
    }

    // GET: Addresses
    public async Task<IActionResult> Index()
    {
        var addresses = await _context.Addresses.ToListAsync();
        return View(addresses);
    }

    // GET: Addresses/Details/5
    public async Task<IActionResult> Details(int? id)
    {
        if (id == null)
            return NotFound();

        var address = await _context.Addresses
            .FirstOrDefaultAsync(m => m.Id == id);
        if (address == null)
            return NotFound();

        return View(address);
    }

    // GET: Addresses/Create
    public IActionResult Create()
    {
        return View();
    }

    // POST: Addresses/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([Bind("FullName,Phone,Street,City,State,Country")] Address address)
    {
        // Get the current user's ID from session
        var userId = HttpContext.Session.GetInt32("UserId");
        if (userId == null)
        {
            // Not logged in or session expired
            return RedirectToAction("Login", "Account");
        }

        address.UserId = userId.Value;

        if (ModelState.IsValid)
        {
            _context.Add(address);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }
        return View(address);
    }

    // GET: Addresses/Edit/5
    public async Task<IActionResult> Edit(int? id)
    {
        if (id == null)
            return NotFound();

        var address = await _context.Addresses.FindAsync(id);
        if (address == null)
            return NotFound();

        return View(address);
    }

    // POST: Addresses/Edit/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, [Bind("Id,FullName,Phone,Street,City,State,Country")] Address address)
    {
        if (id != address.Id)
            return NotFound();

        var userId = HttpContext.Session.GetInt32("UserId");
        if (userId == null)
            return RedirectToAction("Login", "Account");

        address.UserId = userId.Value;

        if (ModelState.IsValid)
        {
            try
            {
                _context.Update(address);
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!_context.Addresses.Any(e => e.Id == address.Id))
                    return NotFound();
                else
                    throw;
            }
            return RedirectToAction(nameof(Index));
        }
        return View(address);
    }

    // GET: Addresses/Delete/5
    public async Task<IActionResult> Delete(int? id)
    {
        if (id == null)
            return NotFound();

        var address = await _context.Addresses
            .FirstOrDefaultAsync(m => m.Id == id);
        if (address == null)
            return NotFound();

        return View(address);
    }

    // POST: Addresses/Delete/5
    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var address = await _context.Addresses.FindAsync(id);
        if (address != null)
            _context.Addresses.Remove(address);

        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetDefault(int? DefaultAddressId)
    {
        if (DefaultAddressId == null)
        {
            // Optionally, add a message to TempData or ModelState
            return RedirectToAction(nameof(Index));
        }

        // Find the address to set as default
        var address = await _context.Addresses
            .Include(a => a.User)
            .FirstOrDefaultAsync(a => a.Id == DefaultAddressId.Value);

        if (address == null)
        {
            return RedirectToAction(nameof(Index));
        }

        // Get all addresses for the same user
        var userId = address.UserId;
        var userAddresses = await _context.Addresses
            .Where(a => a.UserId == userId)
            .ToListAsync();

        // Set all to not default, then set the selected one as default
        foreach (var addr in userAddresses)
        {
            addr.IsDefault = (addr.Id == address.Id);
        }

        await _context.SaveChangesAsync();

        return RedirectToAction("Edit", "Account", new { id = userId });
    }
    private bool AddressExists(int id)
    {
        return _context.Addresses.Any(e => e.Id == id);
    }
}
