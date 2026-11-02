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

        private string EscapeIcsText(string text)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;

            return text
                .Replace(@"\", @"\\")
                .Replace(";", @"\;")
                .Replace(",", @"\,")
                .Replace("\r\n", "\\n")
                .Replace("\n", "\\n")
                .Replace("\r", "\\n");
        }

        private System.Net.Mail.Attachment CreateIcsAttachment(string serviceName, string serviceAddress, string categoryName, DateTime startTime, int durationMinutes)
        {
            var endTime = startTime.AddMinutes(durationMinutes);
            var nowStr = DateTime.UtcNow.ToString("yyyyMMddTHHmmssZ");
            var startUtcStr = startTime.ToUniversalTime().ToString("yyyyMMddTHHmmssZ");
            var endUtcStr = endTime.ToUniversalTime().ToString("yyyyMMddTHHmmssZ");
            var uid = $"{Guid.NewGuid()}@slothub.local";

            var escapedLocation = EscapeIcsText(serviceAddress);
            var escapedSummary = EscapeIcsText($"{serviceName} - {categoryName}");
            var escapedDescription = EscapeIcsText($"Időpontfoglalás a(z) {serviceName} szolgáltatóhoz ({categoryName}).\nHelyszín: {serviceAddress}");

            var icsContent = new System.Text.StringBuilder();
            icsContent.AppendLine("BEGIN:VCALENDAR");
            icsContent.AppendLine("VERSION:2.0");
            icsContent.AppendLine("PRODID:-//Slothub//Idopontfoglalo//HU");
            icsContent.AppendLine("CALSCALE:GREGORIAN");
            icsContent.AppendLine("METHOD:REQUEST");
            icsContent.AppendLine("BEGIN:VEVENT");
            icsContent.AppendLine($"UID:{uid}");
            icsContent.AppendLine($"DTSTAMP:{nowStr}");
            icsContent.AppendLine($"DTSTART:{startUtcStr}");
            icsContent.AppendLine($"DTEND:{endUtcStr}");
            icsContent.AppendLine($"SUMMARY:{escapedSummary}");
            icsContent.AppendLine($"DESCRIPTION:{escapedDescription}");
            icsContent.AppendLine($"LOCATION:{escapedLocation}");
            icsContent.AppendLine("STATUS:CONFIRMED");
            icsContent.AppendLine("END:VEVENT");
            icsContent.AppendLine("END:VCALENDAR");

            var bytes = System.Text.Encoding.UTF8.GetBytes(icsContent.ToString());
            var stream = new System.IO.MemoryStream(bytes);

            var contentType = new System.Net.Mime.ContentType("text/calendar; charset=utf-8");
            contentType.Parameters.Add("method", "REQUEST");
            contentType.Parameters.Add("name", "foglalas.ics");

            var attachment = new System.Net.Mail.Attachment(stream, contentType);
            attachment.ContentDisposition.FileName = "foglalas.ics";

            return attachment;
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
        public async Task<IActionResult> Edit(int id, Services service, string? min_guest_rating)
        {
            if (id != service.service_id) return NotFound();

            var currentUserId = _userManager.GetUserId(User);

            ModelState.Remove("owner_id");
            ModelState.Remove("Owner");
            ModelState.Remove("Category");
            ModelState.Remove("ServiceCategories");
            ModelState.Remove("min_guest_rating");

            double parsedMinRating = 0.0;
            if (!string.IsNullOrWhiteSpace(min_guest_rating))
            {
                var normalized = min_guest_rating.Replace(',', '.');
                double.TryParse(normalized, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out parsedMinRating);
            }

            if (ModelState.IsValid)
            {
                try
                {
                    var existingService = await _context.Services.FirstOrDefaultAsync(s => s.service_id == id);
                    if (existingService == null)
                    {
                        return NotFound();
                    }

                    if (existingService.owner_id != currentUserId)
                    {
                        return Forbid();
                    }

                    existingService.name = service.name;
                    existingService.address = service.address;
                    existingService.description = service.description;
                    existingService.category_id = service.category_id;
                    existingService.min_guest_rating = parsedMinRating;

                    await _context.SaveChangesAsync();

                    TempData["StatusMessage"] = "A szolgáltatás adatai és a vendégszűrő sikeresen frissítve!";
                    return RedirectToAction(nameof(MyServices));
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!_context.Services.Any(e => e.service_id == service.service_id)) return NotFound();
                    else throw;
                }
                catch (Exception ex)
                {
                    TempData["ErrorMessage"] = $"Hiba a mentéskor: {ex.Message}";
                }
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

            var eventList = occasions.Select(o =>
            {
                string color = "#198754"; //zöld
                if (o.status == "reserved")
                {
                    color = "#dc3545"; //piros
                }
                else if (o.status == "pending")
                {
                    color = "#fd7e14"; //narancs
                }

                return new
                {
                    id = o.reservation_id,
                    title = o.ServiceCategory != null ? o.ServiceCategory.Name : "Szolgáltatás",
                    start = o.date.ToString("yyyy-MM-ddTHH:mm:ss"),
                    end = o.date.AddMinutes(30).ToString("yyyy-MM-ddTHH:mm:ss"),
                    backgroundColor = color,
                    borderColor = color,
                    extendedProps = new
                    {
                        status = o.status,
                        userId = o.user_id
                    }
                };
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

            var service = occasion.ServiceCategory?.Service;
            var ownerId = service?.owner_id;

            if (ownerId == currentUser.Id)
            {
                return Json(new { success = false, message = "Saját szolgáltatásodhoz nem foglalhatsz időpontot!" });
            }

            if (occasion.status == "reserved" || occasion.status == "pending" || !string.IsNullOrEmpty(occasion.user_id))
            {
                return Json(new { success = false, message = "Ezt az időpontot időközben már lefoglalták vagy jóváhagyásra vár!" });
            }

            var guestRatings = await _context.Feedbacks
                .Where(f => f.guest_id == currentUser.Id && f.rating_owner > 0)
                .Select(f => f.rating_owner)
                .ToListAsync();

            double actualGuestRating = 0.0;
            bool hasReviews = guestRatings.Any();

            if (hasReviews)
            {
                actualGuestRating = Math.Round(guestRatings.Average(), 1, MidpointRounding.AwayFromZero);
            }

            bool isConditional = false;
            double threshold = service?.min_guest_rating ?? 0.0;

            if (threshold > 0.0 && hasReviews)
            {
                if (actualGuestRating <= threshold)
                {
                    isConditional = true;
                }
            }

            occasion.user_id = currentUser.Id;
            occasion.reservation_date = DateTime.Now;
            occasion.status = isConditional ? "pending" : "reserved";

            await _context.SaveChangesAsync();

            var serviceName = service?.name ?? "Szolgáltatás";
            var serviceAddress = service?.address ?? "Cím nem elérhető";
            var categoryName = occasion.ServiceCategory?.Name ?? "Általános";

            if (!string.IsNullOrEmpty(currentUser.Email))
            {
                try
                {
                    if (isConditional)
                    {
                        var subject = $"Foglalási kérelem rögzítve, a szolgáltató által jóváhagyásra vár - {serviceName}";
                        var body = $@"
                    <div style=""font-family: Arial, sans-serif; line-height: 1.6; color: #333;"">
                        <h2 style=""color: #fd7e14;"">Kedves {currentUser.UserName}!</h2>
                        <p>Foglalási szándékodat rögzítettük. Mivel az értékeléseid ({actualGuestRating:0.0}) alapján a szolgáltató előzetes jóváhagyása szükséges.</p>
                        
                        <div style=""background-color: #fff3cd; border-left: 4px solid #fd7e14; padding: 15px; margin: 20px 0;"">
                            <p style=""margin: 5px 0;""><strong>Szolgáltatás:</strong> {serviceName}</p>
                            <p style=""margin: 5px 0;""><strong>Kategória:</strong> {categoryName}</p>
                            <p style=""margin: 5px 0;""><strong>Időpont:</strong> {occasion.date:yyyy. MM. dd. HH:mm}</p>
                            <p style=""margin: 5px 0;""><strong>Státusz:</strong> Szolgáltatói jóváhagyásra vár</p>
                        </div>

                        <p>Amint a szolgáltató ellenőrzi és megerősíti az időpontot, azonnal küldjük a végleges visszaigazolást.</p>
                        <p>Üdvözlettel,<br />A SlotHub Csapata</p>
                    </div>";

                        await _emailSender.SendEmailAsync(currentUser.Email, subject, body);
                    }
                    else
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

                        <p>Kattints a mellékletre a telefonod naptárába való mentéshez!</p>
                        <p>Ha bármi közbejönne, a foglalást a profilodban a <em>Foglalásaim</em> menüpont alatt bármikor lemondhatod.</p>
                        <p>Üdvözlettel,<br />A SlotHub Csapata</p>
                    </div>";

                        using var icsAttachment = CreateIcsAttachment(serviceName, serviceAddress, categoryName, occasion.date, 30);
                        await _emailSender.SendEmailWithAttachmentAsync(currentUser.Email, subject, body, icsAttachment);
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Sikertelen email küldés: {ex.Message}");
                }
            }

            if (isConditional)
            {
                return Json(new
                {
                    success = true,
                    isPending = true,
                    message = $"Foglalási kérelem elküldve! A korábbi értékeléseid ({actualGuestRating:0.0}) miatt a szolgáltatónak előbb jóvá kell hagynia."
                });
            }

            return Json(new { success = true, isPending = false, message = "Időpont sikeresen lefoglalva!" });
        }

        // GET: Services/GetGuestHistory?guestId=xxx
        [HttpGet]
        public async Task<IActionResult> GetGuestHistory(string guestId)
        {
            var guest = await _userManager.FindByIdAsync(guestId);
            if (guest == null) return NotFound();

            var reviews = await _context.Feedbacks
                .Include(f => f.ServiceOwner)
                .Where(f => f.guest_id == guestId && f.rating_owner > 0)
                .Select(f => new
                {
                    ownerName = f.ServiceOwner != null ? f.ServiceOwner.UserName : "Szolgáltató",
                    rating = f.rating_owner,
                    comment = f.feedbacktext_owner ?? "Nem adott megjegyzést."
                })
                .ToListAsync();

            return Json(new
            {
                guestName = guest.UserName,
                guestEmail = guest.Email,
                guestPhone = guest.PhoneNumber ?? "Nincs megadva",
                averageRating = guest.rating,
                totalReviews = reviews.Count,
                reviews = reviews
            });
        }

        // POST: Services/ApproveBooking
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ApproveBooking(int occasionId)
        {
            var currentUserId = _userManager.GetUserId(User);

            var occasion = await _context.Occasions
                .Include(o => o.ServiceCategory)
                    .ThenInclude(sc => sc.Service)
                .Include(o => o.User)
                .FirstOrDefaultAsync(o => o.reservation_id == occasionId && o.ServiceCategory.Service.owner_id == currentUserId);

            if (occasion == null || occasion.status != "pending")
            {
                return NotFound();
            }

            occasion.status = "reserved";
            await _context.SaveChangesAsync();

            var guest = occasion.User;
            if (guest != null && !string.IsNullOrEmpty(guest.Email))
            {
                try
                {
                    var serviceName = occasion.ServiceCategory?.Service?.name ?? "Szolgáltatás";
                    var serviceAddress = occasion.ServiceCategory?.Service?.address ?? "Cím nem elérhető";
                    var categoryName = occasion.ServiceCategory?.Name ?? "Általános";

                    var subject = $"Foglalás jóváhagyva - {serviceName}";
                    var body = $@"
                        <div style=""font-family: Arial, sans-serif; line-height: 1.6; color: #333;"">
                            <h2 style=""color: #198754;"">Kedves {guest.UserName}!</h2>
                            <p>Örömmel értesítünk, hogy a szolgáltató <strong>jóváhagyta</strong> a foglalásodat!</p>
                            
                            <div style=""background-color: #f8f9fa; border-left: 4px solid #198754; padding: 15px; margin: 20px 0;"">
                                <p style=""margin: 5px 0;""><strong>Szolgáltatás:</strong> {serviceName}</p>
                                <p style=""margin: 5px 0;""><strong>Kategória:</strong> {categoryName}</p>
                                <p style=""margin: 5px 0;""><strong>Időpont:</strong> {occasion.date:yyyy. MM. dd. HH:mm}</p>
                                <p style=""margin: 5px 0;""><strong>Helyszín:</strong> {serviceAddress}</p>
                            </div>

                            <p>Kattints a mellékletre a telefonod naptárába való mentéshez!</p>
                            <p>Ha bármi közbejönne, a foglalást a profilodban a <em>Foglalásaim</em> menüpont alatt bármikor lemondhatod.</p>
                            <p>Üdvözlettel,<br />A SlotHub Csapata</p>
                        </div>";

                    using var icsAttachment = CreateIcsAttachment(serviceName, serviceAddress, categoryName, occasion.date, 30);
                    await _emailSender.SendEmailWithAttachmentAsync(guest.Email, subject, body, icsAttachment);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Sikertelen email küldés: {ex.Message}");
                }
            }

            TempData["StatusMessage"] = "A foglalást sikeresen jóváhagytad!";
            return RedirectToAction(nameof(Calendar), new { id = occasion.service_id });
        }

        // POST: Services/RejectBooking
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RejectBooking(int occasionId)
        {
            var currentUserId = _userManager.GetUserId(User);

            var occasion = await _context.Occasions
                .Include(o => o.ServiceCategory)
                    .ThenInclude(sc => sc.Service)
                .Include(o => o.User)
                .FirstOrDefaultAsync(o => o.reservation_id == occasionId && o.ServiceCategory.Service.owner_id == currentUserId);

            if (occasion == null || occasion.status != "pending")
            {
                return NotFound();
            }

            var guestEmail = occasion.User?.Email;
            var guestName = occasion.User?.UserName;
            var serviceName = occasion.ServiceCategory?.Service?.name ?? "Szolgáltatás";
            var dateStr = occasion.date.ToString("yyyy. MM. dd. HH:mm");

            occasion.user_id = null;
            occasion.reservation_date = null;
            occasion.status = "Szabad";

            await _context.SaveChangesAsync();

            if (!string.IsNullOrEmpty(guestEmail))
            {
                try
                {
                    var subject = $"Foglalási kérelem elutasítva - {serviceName}";
                    var body = $@"
                        <div style=""font-family: Arial, sans-serif; line-height: 1.6; color: #333;"">
                            <h2 style=""color: #dc3545;"">Kedves {guestName}!</h2>
                            <p>Értesítünk, hogy a szolgáltató nem tudta elfogadni a(z) <strong>{dateStr}</strong> időpontra leadott foglalási kérelmedet.</p>
                            <p>Üdvözlettel,<br />A SlotHub Csapata</p>
                        </div>";

                    await _emailSender.SendEmailAsync(guestEmail, subject, body);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Sikertelen email küldés: {ex.Message}");
                }
            }

            TempData["StatusMessage"] = "A kérelmet elutasítottad, az idősáv újra szabaddá vált.";
            return RedirectToAction(nameof(Calendar), new { id = occasion.service_id });
        }

        // POST: Services/SubmitGuestFeedback
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SubmitGuestFeedback(int reservationId, double rating, string? comment)
        {
            var currentUserId = _userManager.GetUserId(User);

            if (reservationId <= 0)
            {
                TempData["ErrorMessage"] = $"Nem érkezett érvényes foglalás azonosító! (ID: {reservationId})";
                return RedirectToAction(nameof(MyServices));
            }

            try
            {
                var occasion = await _context.Occasions
                    .Include(o => o.ServiceCategory)
                        .ThenInclude(sc => sc.Service)
                    .FirstOrDefaultAsync(o => o.reservation_id == reservationId);

                if (occasion == null)
                {
                    TempData["ErrorMessage"] = $"Nem található ilyen időpont az adatbázisban (ID: {reservationId})!";
                    return RedirectToAction(nameof(MyServices));
                }
                if (string.IsNullOrEmpty(occasion.user_id))
                {
                    TempData["ErrorMessage"] = "Ehhez az időponthoz nincs társítva vendég az adatbázisban!";
                    return RedirectToAction(nameof(Calendar), new { id = occasion.service_id });
                }

                var serviceId = occasion.service_id;
                var guestUserId = occasion.user_id;
                var ownerId = occasion.ServiceCategory?.Service?.owner_id ?? currentUserId;

                var feedback = await _context.Feedbacks
                    .FirstOrDefaultAsync(f => f.reservation_Id == reservationId);

                if (feedback == null)
                {
                    feedback = new Feedbacks
                    {
                        reservation_Id = reservationId,
                        guest_id = guestUserId,
                        service_owner_id = ownerId,
                        rating_owner = rating,
                        feedbacktext_owner = comment,
                        rating_guest = 0,
                        feedbacktext_guest = null
                    };
                    _context.Feedbacks.Add(feedback);
                }
                else
                {
                    feedback.rating_owner = rating;
                    feedback.feedbacktext_owner = comment;
                    feedback.service_owner_id = ownerId;
                    feedback.guest_id = guestUserId;
                    _context.Feedbacks.Update(feedback);
                }

                await _context.SaveChangesAsync();

                try
                {
                    var guestRatings = await _context.Feedbacks
                        .Where(f => f.guest_id == guestUserId && f.rating_owner > 0)
                        .Select(f => f.rating_owner)
                        .ToListAsync();

                    if (guestRatings.Any())
                    {
                        var newAvg = Math.Round(guestRatings.Average(), 1, MidpointRounding.AwayFromZero);
                        var guestUser = await _context.Users.FirstOrDefaultAsync(u => u.Id == guestUserId);
                        if (guestUser != null)
                        {
                            guestUser.rating = newAvg;
                            await _context.SaveChangesAsync();
                        }
                    }
                }
                catch (Exception)
                {
                 
                }

                TempData["StatusMessage"] = "A vendég értékelése sikeresen elmentve!";
                return RedirectToAction(nameof(Calendar), new { id = serviceId });
            }
            catch (Exception ex)
            {
                var msg = ex.InnerException != null ? ex.InnerException.Message : ex.Message;
                TempData["ErrorMessage"] = $"Hiba történt a mentéskor: {msg}";
                return RedirectToAction(nameof(MyServices));
            }
        }
    }
}