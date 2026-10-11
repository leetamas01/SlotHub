using IdopontfoglaloWebalk.Context;
using IdopontfoglaloWebalk.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;

[Authorize]
public class ProfileController : Controller
{
    private readonly UserManager<Users> _userManager;
    private readonly SignInManager<Users> _signInManager;
    private readonly EfContext _context;
    private readonly IEmailSenderService _emailSender;

    public ProfileController(
        UserManager<Users> userManager,
        SignInManager<Users> signInManager,
        EfContext context,
        IEmailSenderService emailSender)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _context = context;
        _emailSender = emailSender;
    }

    [HttpGet]
    public async Task<IActionResult> Profile()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return RedirectToAction("Login", "Home");
        }

        var reservations = await _context.Occasions
            .Include(o => o.ServiceCategory)
                .ThenInclude(sc => sc.Service)
                    .ThenInclude(s => s.Owner)
            .Where(o => o.user_id == user.Id)
            .OrderByDescending(o => o.date)
            .ToListAsync();

        ViewBag.Feedbacks = await _context.Feedbacks
            .Where(f => f.guest_id == user.Id)
            .ToListAsync();

        var viewModel = new ProfileViewModel
        {
            User = user,
            Reservations = reservations
        };

        return View(viewModel);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CancelReservation(int reservationId)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return NotFound();

        var reservation = await _context.Occasions
            .Include(o => o.ServiceCategory)
                .ThenInclude(sc => sc.Service)
            .FirstOrDefaultAsync(o => o.reservation_id == reservationId && o.user_id == user.Id);

        if (reservation == null)
        {
            TempData["ErrorMessage"] = "A lemondani kívánt foglalás nem található!";
            return RedirectToAction(nameof(Profile));
        }

        var serviceName = reservation.ServiceCategory?.Service?.name ?? "Szolgáltatás";
        var serviceAddress = reservation.ServiceCategory?.Service?.address ?? "Cím nem elérhető";
        var categoryName = reservation.ServiceCategory?.Name ?? "Általános";
        var reservationDateStr = reservation.date.ToString("yyyy. MM. dd. HH:mm");

        // Időpont felszabadítása
        reservation.user_id = null;
        reservation.reservation_date = null;
        reservation.status = "Szabad";

        await _context.SaveChangesAsync();

        // Lemondási visszaigazoló email kiküldése
        if (!string.IsNullOrEmpty(user.Email))
        {
            try
            {
                var subject = $"Foglalás lemondva - {serviceName}";
                var body = $@"
                    <div style=""font-family: Arial, sans-serif; line-height: 1.6; color: #333;"">
                        <h2 style=""color: #dc3545;"">Kedves {user.UserName}!</h2>
                        <p>Értesítünk, hogy az alábbi időpontfoglalásodat sikeresen lemondtad:</p>
                        
                        <div style=""background-color: #f8f9fa; border-left: 4px solid #dc3545; padding: 15px; margin: 20px 0;"">
                            <p style=""margin: 5px 0;""><strong>Szolgáltatás:</strong> {serviceName}</p>
                            <p style=""margin: 5px 0;""><strong>Kategória:</strong> {categoryName}</p>
                            <p style=""margin: 5px 0;""><strong>Időpont:</strong> {reservationDateStr}</p>
                            <p style=""margin: 5px 0;""><strong>Helyszín:</strong> {serviceAddress}</p>
                        </div>

                        <p>Ha meggondolnád magad, bármikor foglalhatsz új időpontot az oldalunkon.</p>
                        <p>Üdvözlettel,<br />Az Időpontfoglaló Csapata</p>
                    </div>";

                await _emailSender.SendEmailAsync(user.Email, subject, body);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Sikertelen lemondási email küldés: {ex.Message}");
            }
        }

        TempData["StatusMessage"] = "A foglalás sikeresen lemondva! A visszaigazolást elküldtük emailben.";
        return RedirectToAction(nameof(Profile));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SubmitFeedback(int reservationId, double rating, string? comment)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return NotFound();

        var reservation = await _context.Occasions
            .Include(o => o.ServiceCategory)
                .ThenInclude(sc => sc.Service)
            .FirstOrDefaultAsync(o => o.reservation_id == reservationId && o.user_id == user.Id);

        if (reservation == null)
        {
            TempData["ErrorMessage"] = "A foglalás nem található!";
            return RedirectToAction(nameof(Profile));
        }

        if (reservation.date > DateTime.Now)
        {
            TempData["ErrorMessage"] = "Csak lezajlott időpontot értékelhetsz!";
            return RedirectToAction(nameof(Profile));
        }

        var feedback = await _context.Feedbacks
            .FirstOrDefaultAsync(f => f.reservation_Id == reservationId);

        if (feedback != null && feedback.rating_guest > 0)
        {
            TempData["ErrorMessage"] = "Ezt az időpontot már értékelted korábban!";
            return RedirectToAction(nameof(Profile));
        }

        var targetService = reservation.ServiceCategory?.Service;

        if (feedback == null)
        {
            feedback = new Feedbacks
            {
                reservation_Id = reservationId,
                guest_id = user.Id,
                service_owner_id = targetService?.owner_id,
                rating_guest = rating,
                feedbacktext_guest = comment
            };
            _context.Feedbacks.Add(feedback);
        }
        else
        {
            feedback.rating_guest = rating;
            feedback.feedbacktext_guest = comment;
        }

        await _context.SaveChangesAsync();

        if (targetService != null)
        {
            var serviceRatings = await (from f in _context.Feedbacks
                                        join o in _context.Occasions on f.reservation_Id equals o.reservation_id
                                        join sc in _context.ServiceCategories on o.service_category_id equals sc.Id
                                        where sc.service_id == targetService.service_id && f.rating_guest > 0
                                        select f.rating_guest).ToListAsync();

            if (serviceRatings.Any())
            {
                targetService.rating = Math.Round(serviceRatings.Average(), 1, MidpointRounding.AwayFromZero);
                _context.Services.Update(targetService);
            }

            var ownerId = targetService.owner_id;
            if (!string.IsNullOrEmpty(ownerId))
            {
                var ownerRatings = await _context.Feedbacks
                    .Where(f => f.service_owner_id == ownerId && f.rating_guest > 0)
                    .Select(f => f.rating_guest)
                    .ToListAsync();

                if (ownerRatings.Any())
                {
                    var owner = await _userManager.FindByIdAsync(ownerId);
                    if (owner != null)
                    {
                        owner.rating = Math.Round(ownerRatings.Average(), 1, MidpointRounding.AwayFromZero);
                        await _userManager.UpdateAsync(owner);
                    }
                }
            }
        }

        await _context.SaveChangesAsync();

        TempData["StatusMessage"] = "Köszönjük az értékelést!";
        return RedirectToAction(nameof(Profile));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Update(string email, string phoneNumber)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return NotFound();

        user.Email = email;
        user.NormalizedEmail = email.ToUpper();
        user.PhoneNumber = phoneNumber;

        var result = await _userManager.UpdateAsync(user);

        if (result.Succeeded)
        {
            await _signInManager.RefreshSignInAsync(user);
            TempData["StatusMessage"] = "A profilod adatai sikeresen frissültek!";
            return RedirectToAction(nameof(Profile));
        }

        TempData["ErrorMessage"] = string.Join(" ", result.Errors.Select(e => e.Description));
        return RedirectToAction(nameof(Profile));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangePassword(string currentPassword, string newPassword, string confirmPassword)
    {
        if (currentPassword == newPassword)
        {
            TempData["ErrorMessage"] = "A régi és az új jelszó megegyezik!";
            return RedirectToAction(nameof(Profile));
        }

        if (newPassword != confirmPassword)
        {
            TempData["ErrorMessage"] = "Az új jelszó és a megerősítés nem egyezik meg!";
            return RedirectToAction(nameof(Profile));
        }

        var user = await _userManager.GetUserAsync(User);
        if (user == null) return NotFound();

        var result = await _userManager.ChangePasswordAsync(user, currentPassword, newPassword);
        if (result.Succeeded)
        {
            await _signInManager.RefreshSignInAsync(user);
            TempData["StatusMessage"] = "A jelszavad sikeresen megváltozott!";
            return RedirectToAction(nameof(Profile));
        }

        TempData["ErrorMessage"] = string.Join(" ", result.Errors.Select(e => e.Description));
        return RedirectToAction(nameof(Profile));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteProfile(string password)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return NotFound();

        if (await _userManager.IsInRoleAsync(user, "Admin"))
        {
            TempData["ErrorMessage"] = "A rendszergazdai (Admin) fiók biztonsági okokból nem törölhető!";
            return RedirectToAction(nameof(Profile));
        }

        if (string.IsNullOrWhiteSpace(password) || !await _userManager.CheckPasswordAsync(user, password))
        {
            TempData["ErrorMessage"] = "A törléshez megadott jelszó helytelen!";
            return RedirectToAction(nameof(Profile));
        }

        bool hasServices = await _context.Services.AnyAsync(s => s.owner_id == user.Id);
        if (hasServices)
        {
            TempData["ErrorMessage"] = "Nem törölheted a fiókodat, amíg van aktív szolgáltatásod!";
            return RedirectToAction(nameof(Profile));
        }

        var reservations = await _context.Occasions.Where(o => o.user_id == user.Id).ToListAsync();
        foreach (var r in reservations)
        {
            r.user_id = null;
            r.reservation_date = null;
            r.status = "Szabad";
        }
        await _context.SaveChangesAsync();

        await _signInManager.SignOutAsync();
        await _userManager.DeleteAsync(user);

        return RedirectToAction("Index", "Home");
    }
}