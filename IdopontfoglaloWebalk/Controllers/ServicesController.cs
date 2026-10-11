using IdopontfoglaloWebalk.Context;
using IdopontfoglaloWebalk.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace IdopontfoglaloWebalk.Controllers
{
    [Authorize]
    public class ServicesController : Controller
    {
        private readonly EfContext _context;
        private readonly UserManager<Users> _userManager;
        private readonly IEmailSenderService _emailSender;

        public ServicesController(EfContext context, UserManager<Users> userManager, IEmailSenderService emailSender)
        {
            _context = context;
            _userManager = userManager;
            _emailSender = emailSender;
        }

        // GET: Services/MyServices
        public async Task<IActionResult> MyServices()
        {
            var currentUserId = _userManager.GetUserId(User);

            var myServices = await _context.Services
                .Where(s => s.owner_id == currentUserId)
                .ToListAsync();

            return View(myServices);
        }

        // GET: Services/Create
        [HttpGet]
        public async Task<IActionResult> Create()
        {
            var categories = await _context.Categories.ToListAsync();
            ViewBag.CategoryList = new Microsoft.AspNetCore.Mvc.Rendering.SelectList(categories, "category_id", "category_name");
            return View();
        }

        // POST: Services/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Services service)
        {
            ModelState.Remove("owner_id");
            ModelState.Remove("Owner");
            ModelState.Remove("Category");
            ModelState.Remove("ServiceCategories");

            if (ModelState.IsValid)
            {
                service.owner_id = _userManager.GetUserId(User);
                service.rating = 0.0;

                _context.Services.Add(service);
                await _context.SaveChangesAsync();

                return RedirectToAction(nameof(MyServices));
            }

            var allCategories = await _context.Categories.ToListAsync();
            ViewBag.CategoryList = new Microsoft.AspNetCore.Mvc.Rendering.SelectList(allCategories, "category_id", "category_name");
            return View(service);
        }

        // GET: Services/Edit/5
        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var service = await _context.Services.FindAsync(id);
            if (service == null) return NotFound();

            if (service.owner_id != _userManager.GetUserId(User))
            {
                return Forbid();
            }

            var categories = await _context.Categories.ToListAsync();
            ViewBag.CategoryList = new Microsoft.AspNetCore.Mvc.Rendering.SelectList(categories, "category_id", "category_name", service.category_id);

            return View(service);
        }

        // POST: Services/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Services service)
        {
            if (id != service.service_id) return NotFound();

            ModelState.Remove("owner_id");
            ModelState.Remove("Owner");
            ModelState.Remove("Category");
            ModelState.Remove("ServiceCategories");

            if (ModelState.IsValid)
            {
                try
                {
                    var originalService = await _context.Services.AsNoTracking().FirstOrDefaultAsync(s => s.service_id == id);
                    if (originalService == null || originalService.owner_id != _userManager.GetUserId(User))
                    {
                        return Forbid();
                    }

                    service.owner_id = originalService.owner_id;
                    service.rating = originalService.rating;

                    _context.Update(service);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!_context.Services.Any(e => e.service_id == service.service_id)) return NotFound();
                    else throw;
                }
                return RedirectToAction(nameof(MyServices));
            }

            var categories = await _context.Categories.ToListAsync();
            ViewBag.CategoryList = new Microsoft.AspNetCore.Mvc.Rendering.SelectList(categories, "category_id", "category_name", service.category_id);
            return View(service);
        }

        // GET: Services/Delete/5
        [HttpGet]
        public async Task<IActionResult> Delete(int id)
        {
            var service = await _context.Services
                .Include(s => s.Category)
                .FirstOrDefaultAsync(m => m.service_id == id);

            if (service == null) return NotFound();

            if (service.owner_id != _userManager.GetUserId(User))
            {
                return Forbid();
            }

            return View(service);
        }

        // POST: Services/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var service = await _context.Services.FindAsync(id);
            if (service == null) return NotFound();

            if (service.owner_id != _userManager.GetUserId(User))
            {
                return Forbid();
            }

            _context.Services.Remove(service);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(MyServices));
        }

        // GET: Services/Calendar/5
        [AllowAnonymous]
        [HttpGet]
        public async Task<IActionResult> Calendar(int id)
        {
            var service = await _context.Services
                .Include(s => s.ServiceCategories)
                .FirstOrDefaultAsync(s => s.service_id == id);

            if (service == null)
            {
                return NotFound();
            }

            if (service.owner_id != _userManager.GetUserId(User))
            {
                return Forbid();
            }

            return View(service);
        }

        // POST: Services/GenerateSlots
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> GenerateSlots(
            int serviceId,
            int serviceCategoryId,
            DateTime startDate,
            DateTime endDate,
            TimeSpan dailyStartTime,
            TimeSpan dailyEndTime,
            int slotDurationMinutes,
            bool excludeWeekends = false)
        {
            var service = await _context.Services.FindAsync(serviceId);
            if (service == null) return NotFound();

            if (service.owner_id != _userManager.GetUserId(User))
            {
                return Forbid();
            }

            var occasions = new List<Occasions>();

            for (var currentDay = startDate.Date; currentDay <= endDate.Date; currentDay = currentDay.AddDays(1))
            {
                if (excludeWeekends && (currentDay.DayOfWeek == DayOfWeek.Saturday || currentDay.DayOfWeek == DayOfWeek.Sunday))
                {
                    continue;
                }

                var currentSlot = currentDay.Add(dailyStartTime);
                var dayEnd = currentDay.Add(dailyEndTime);

                while (currentSlot.AddMinutes(slotDurationMinutes) <= dayEnd)
                {
                    bool exists = await _context.Occasions.AnyAsync(o =>
                        o.service_id == serviceId &&
                        o.date == currentSlot);

                    if (!exists)
                    {
                        occasions.Add(new Occasions
                        {
                            service_id = serviceId,
                            service_category_id = serviceCategoryId,
                            date = currentSlot,
                            user_id = null,
                            reservation_date = null,
                            status = "Szabad"
                        });
                    }

                    currentSlot = currentSlot.AddMinutes(slotDurationMinutes);
                }
            }

            if (occasions.Any())
            {
                await _context.Occasions.AddRangeAsync(occasions);
                await _context.SaveChangesAsync();
            }

            return RedirectToAction(nameof(Calendar), new { id = serviceId });
        }

        [AllowAnonymous]
        [HttpGet]
        public async Task<JsonResult> GetCalendarEvents(int service_id, DateTime? start, DateTime? end)
        {
            var query = _context.Occasions
                .Include(o => o.ServiceCategory)
                .Where(o => o.service_id == service_id);

            if (start.HasValue && end.HasValue)
            {
                query = query.Where(o => o.date >= start.Value && o.date <= end.Value);
            }

            var occasions = await query.ToListAsync();

            var eventList = occasions.Select(o => new
            {
                id = o.reservation_id,
                title = o.status == "reserved"
                    ? "FOGLALT: " + (o.ServiceCategory != null ? o.ServiceCategory.Name : "Szolgáltatás")
                    : (o.ServiceCategory != null ? o.ServiceCategory.Name : "Szabad időpont"),
                start = o.date.ToString("yyyy-MM-ddTHH:mm:ss"),
                end = o.date.AddMinutes(30).ToString("yyyy-MM-ddTHH:mm:ss"),
                backgroundColor = o.status == "reserved" ? "#dc3545" : "#198754",
                borderColor = o.status == "reserved" ? "#dc3545" : "#198754",
                extendedProps = new
                {
                    status = o.status,
                    userId = o.user_id
                }
            });

            return Json(eventList);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddCategory(string categoryName)
        {
            if (!string.IsNullOrWhiteSpace(categoryName))
            {
                var category = new Categories
                {
                    category_name = categoryName
                };

                _context.Categories.Add(category);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(MyServices));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddCategoryServices(int serviceId, string categoryName)
        {
            if (!string.IsNullOrWhiteSpace(categoryName))
            {
                var category = new ServiceCategories
                {
                    service_id = serviceId,
                    Name = categoryName
                };

                _context.ServiceCategories.Add(category);
                await _context.SaveChangesAsync();
            }

            return RedirectToAction(nameof(Calendar), new { id = serviceId });
        }

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> BookOccasion(int occasionId)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
            {
                return Json(new
                {
                    success = false,
                    requireLogin = true,
                    message = "A foglaláshoz be kell jelentkezned!"
                });
            }

            var occasion = await _context.Occasions
                .Include(o => o.ServiceCategory)
                    .ThenInclude(sc => sc.Service)
                .FirstOrDefaultAsync(o => o.reservation_id == occasionId);

            if (occasion == null)
            {
                return Json(new { success = false, message = "A kiválasztott időpont nem található!" });
            }

            if (occasion.status == "reserved" || !string.IsNullOrEmpty(occasion.user_id))
            {
                return Json(new { success = false, message = "Ezt az időpontot időközben már lefoglalták!" });
            }

            // Időpont rögzítése
            occasion.user_id = currentUser.Id;
            occasion.reservation_date = DateTime.Now;
            occasion.status = "reserved";

            await _context.SaveChangesAsync();

            var serviceName = occasion.ServiceCategory?.Service?.name ?? "Szolgáltatás";
            var serviceAddress = occasion.ServiceCategory?.Service?.address ?? "Cím nem elérhető";
            var categoryName = occasion.ServiceCategory?.Name ?? "Általános";

            if (!string.IsNullOrEmpty(currentUser.Email))
            {
                try
                {
                    var subject = $"Sikeres foglalás - {serviceName}";
                    var body = $@"
                        <div style=""font-family: Arial, sans-serif; line-height: 1.6; color: #333;"">
                            <h2 style=""color: #0d6efd;"">Kedves {currentUser.UserName}!</h2>
                            <p>Köszönjük a foglalásodat! Az időpontodat sikeresen rögzítettük rendszerünkben.</p>
                            
                            <div style=""background-color: #f8f9fa; border-left: 4px solid #0d6efd; padding: 15px; margin: 20px 0;"">
                                <p style=""margin: 5px 0;""><strong>Szolgáltatás:</strong> {serviceName}</p>
                                <p style=""margin: 5px 0;""><strong>Kategória:</strong> {categoryName}</p>
                                <p style=""margin: 5px 0;""><strong>Időpont:</strong> {occasion.date:yyyy. MM. dd. HH:mm}</p>
                                <p style=""margin: 5px 0;""><strong>Helyszín:</strong> {serviceAddress}</p>
                            </div>

                            <p>Ha bármi közbejönne, a foglalást a profilodban a <em>Foglalásaim</em> menüpont alatt bármikor lemondhatod.</p>
                            <p>Üdvözlettel,<br />Az Időpontfoglaló Csapata</p>
                        </div>";

                    await _emailSender.SendEmailAsync(currentUser.Email, subject, body);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Sikertelen email küldés: {ex.Message}");
                }
            }

            return Json(new { success = true, message = "Időpont sikeresen lefoglalva!" });
        }
    }
}