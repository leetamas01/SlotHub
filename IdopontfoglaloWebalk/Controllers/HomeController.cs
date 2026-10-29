using IdopontfoglaloWebalk.Context;
using IdopontfoglaloWebalk.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;

namespace IdopontfoglaloWebalk.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly EfContext _context;
        public HomeController(ILogger<HomeController> logger, EfContext context)
        {
            _logger = logger;
            _context = context;
        }

        public IActionResult Index()
        {
            return View();
        }
        [HttpGet]
        public async Task<IActionResult> Search(string searchQuery)
        {
            ViewBag.Query = searchQuery;

            if (string.IsNullOrEmpty(searchQuery))
            {
                return View(new List<Services>());
            }

            var results = await _context.Services
                .Include(s => s.Category)
                .Where(s => s.name.Contains(searchQuery) ||
                            s.description.Contains(searchQuery) ||
                            s.Category.category_name.Contains(searchQuery))
                .ToListAsync();


            var serviceIds = results.Select(s => s.service_id).ToList();

            var feedbacks = await (from f in _context.Feedbacks.Include(f => f.Guest)
                                   join o in _context.Occasions on f.reservation_Id equals o.reservation_id
                                   join sc in _context.ServiceCategories on o.service_category_id equals sc.Id
                                   where serviceIds.Contains(sc.service_id) && f.rating_guest > 0
                                   select new
                                   {
                                       ServiceId = sc.service_id,
                                       Feedback = f
                                   }).ToListAsync();

            ViewBag.FeedbacksByService = feedbacks
                .GroupBy(x => x.ServiceId)
                .ToDictionary(g => g.Key, g => g.Select(x => x.Feedback).ToList());

            return View(results);
        }

        // GET: Home/BookAppointment/5
        [HttpGet]
        public async Task<IActionResult> BookAppointment(int id)
        {
            var service = await _context.Services.FindAsync(id);
            if (service == null)
            {
                return NotFound();
            }

            return View(service);
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
