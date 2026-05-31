using System.Diagnostics;
using AIChatAssistant.MVC.Models;
using Microsoft.AspNetCore.Mvc;

namespace AIChatAssistant.MVC.Controllers;

public class HomeController : Controller
{
    public IActionResult Index() => View();

    public IActionResult Privacy() => View();

    public IActionResult Login() => View();

    public IActionResult Register() => View();

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel
        {
            RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier
        });
    }
}
