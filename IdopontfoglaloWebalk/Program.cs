using IdopontfoglaloWebalk.Context;
using IdopontfoglaloWebalk.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace IdopontfoglaloWebalk
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            builder.Services.AddControllersWithViews();

            var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
            builder.Services.AddDbContext<EfContext>(options =>
                options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString)));

            builder.Services.AddIdentity<Users, IdentityRole>(options =>
            {
                options.SignIn.RequireConfirmedAccount = false;
                options.User.AllowedUserNameCharacters =
                 "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-._@+ ·ÈÌÛˆı˙¸˚¡…Õ”÷’⁄‹€";

                options.Password.RequireDigit = false;
                options.Password.RequiredLength = 8;
                options.Password.RequireNonAlphanumeric = false;
                options.Password.RequireUppercase = false;
                options.Password.RequireLowercase = false;

                options.User.RequireUniqueEmail = true;
            })
            .AddRoles<IdentityRole>()
            .AddEntityFrameworkStores<EfContext>()
            .AddDefaultTokenProviders();

            builder.Services.ConfigureApplicationCookie(options =>
            {
                options.ExpireTimeSpan = TimeSpan.FromDays(14);
                options.SlidingExpiration = true;
                options.LoginPath = "/Home/Login";
                options.LogoutPath = "/Users/Logout";
                options.Cookie.HttpOnly = true;
                options.Cookie.SameSite = SameSiteMode.Lax;
                options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
            });

            builder.Services.AddDistributedMemoryCache();
            builder.Services.AddSession(options =>
            {
                options.IdleTimeout = TimeSpan.FromMinutes(30);
                options.Cookie.HttpOnly = true;
                options.Cookie.IsEssential = true;
            });

            builder.Services.AddScoped<IEmailSenderService, EmailSenderService>();

            var app = builder.Build();

            if (!app.Environment.IsDevelopment())
            {
                app.UseExceptionHandler("/Home/Error");
                app.UseHsts();
            }

            app.UseHttpsRedirection();
            app.UseStaticFiles();
            app.UseRouting();
            app.UseSession();
            app.UseAuthentication();
            app.UseAuthorization();

            app.MapControllerRoute(
                name: "default",
                pattern: "{controller=Home}/{action=Index}/{id?}");

            using (var scope = app.Services.CreateScope())
            {
                var services = scope.ServiceProvider;
                var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
                var userManager = services.GetRequiredService<UserManager<Users>>();

                //Szerepkˆrˆk lÈtrehoz·sa
                string[] roleNames = { "Admin", "User" };
                foreach (var roleName in roleNames)
                {
                    if (!await roleManager.RoleExistsAsync(roleName))
                    {
                        await roleManager.CreateAsync(new IdentityRole(roleName));
                    }
                }

                //Az elsıdleges rendszergazda lÈtrehoz·sa
                var adminEmail = "admin@idopont.hu";
                var defaultAdmin = await userManager.FindByEmailAsync(adminEmail);

                if (defaultAdmin == null)
                {
                    defaultAdmin = new Users
                    {
                        UserName = "admin",
                        Email = adminEmail,
                        EmailConfirmed = true,
                        rating = 5.0
                    };

                    var createResult = await userManager.CreateAsync(defaultAdmin, "Admin123!");
                    if (createResult.Succeeded)
                    {
                        await userManager.AddToRoleAsync(defaultAdmin, "Admin");
                    }
                }
            }

            await app.RunAsync();
        }
    }
}