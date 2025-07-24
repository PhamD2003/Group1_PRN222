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

        if (ModelState.IsValid)
        {
            try
            {
                _context.Update(address);
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!AddressExists(address.Id))
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

    private bool AddressExists(int id)
    {
        return _context.Addresses.Any(e => e.Id == id);
    }
}
