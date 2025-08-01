﻿using Group1_PRN222.Controllers;
using Group1_PRN222.Hubs;
using Group1_PRN222.Models;
using Group1_PRN222.Services;
using Microsoft.EntityFrameworkCore;

namespace Group1_PRN222;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        builder.Services.AddHttpContextAccessor();

        // Add services to the container.
        builder.Services.AddControllersWithViews();
        builder.Services.AddSignalR();
        // Add email sender service
        builder.Services.AddTransient<IEmailSender, EmailSender>();

        // Cấu hình Session
        builder.Services.AddDistributedMemoryCache(); // Cần thiết cho IDistributedCache
        builder.Services.AddSession(options =>
        {
            options.IdleTimeout = TimeSpan.FromMinutes(30); // Thời gian hết hạn của session
            options.Cookie.HttpOnly = true; // Cookie chỉ truy cập qua HTTP, không qua client-side script
            options.Cookie.IsEssential = true; // Cần thiết cho chức năng
        });
        builder.Services.AddScoped<ICouponService, CouponService>();

        builder.Services.AddDbContext<CloneEbayDbContext>(options =>
        options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

        var app = builder.Build();

        // Configure the HTTP request pipeline.
        if (!app.Environment.IsDevelopment())
        {
            app.UseExceptionHandler("/Home/Error");
            // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
            app.UseHsts();
        }

        app.UseHttpsRedirection();
        app.UseStaticFiles();

        app.UseRouting();
        app.UseSession();
        app.UseAuthorization();

        app.MapControllerRoute(
            name: "default",
            pattern: "{controller=Home}/{action=Index}/{id?}");
        app.MapHub<AuctionHub>("/auctionHub");
        app.Run();
    }
}
