using IdopontfoglaloWebalk.Context;
using IdopontfoglaloWebalk.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.CodeAnalysis.Scripting;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace IdopontfoglaloWebalk.Controllers
{
    public class UsersController : Controller
    {
        private readonly UserManager<Users> _userManager;
        private readonly SignInManager<Users> _signInManager;

        private readonly EfContext _context;

        public UsersController(UserManager<Users> userManager, SignInManager<Users> signInManager, EfContext context)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _context = context;
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            if (ModelState.IsValid)
            {
                var user = await _userManager.FindByEmailAsync(model.Email);

                if (user != null)
                {
                    var result = await _signInManager.PasswordSignInAsync(
                    user.UserName!,
                    model.Password,
                    model.RememberMe,
                    lockoutOnFailure: false);

                    if (result.Succeeded)
                    {
                        if (model.RememberMe)
                        {
                            Response.Cookies.Append("RememberedEmail", model.Email, new CookieOptions { Expires = DateTime.Now.AddDays(30) });
                        }
                        else
                        {
                            Response.Cookies.Delete("RememberedEmail");
                        }
                        return RedirectToAction("Index", "Home");
                    }
                }

                ViewBag.ErrorMessage = "Helytelen email cím vagy jelszó!";
            }
            return View("~/Views/Users/Login.cshtml");
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Registration(Users user, string password)
        {
            user.rating = 0;
            var result = await _userManager.CreateAsync(user, password);

            if (result.Succeeded)
            {
                await _userManager.AddToRoleAsync(user, "User");
                return RedirectToAction("Login", "Users");
            }

            foreach (var error in result.Errors)
            {
                ModelState.AddModelError("", error.Description);
            }
            return View("Registration", user);
        }
        public async Task<IActionResult> Logout()
        {
            var user = await _userManager.GetUserAsync(User);

            var userEmail = user?.Email;

            await _signInManager.SignOutAsync();
            HttpContext.Session.Clear();

            if (!string.IsNullOrEmpty(userEmail))
            {
                CookieOptions option = new CookieOptions
                {
                    Expires = DateTime.Now.AddDays(30),
                    HttpOnly = true,
                    Secure = true,
                    IsEssential = true
                };
                Response.Cookies.Append("RememberedEmail", userEmail, option);
            }
            return RedirectToAction("Index", "Home");
        }

        public IActionResult Login()
        {
            var model = new LoginViewModel();

            if (Request.Cookies.TryGetValue("RememberedEmail", out string? savedEmail))
            {
                model.Email = savedEmail;
                model.RememberMe = true;
            }

            return View(model);
        }
        public IActionResult Registration()
        {
            return View();
        }
    }
}
