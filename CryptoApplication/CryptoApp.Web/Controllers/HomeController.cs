using Microsoft.AspNetCore.Mvc;
using CryptoApp.Application.Interfaces;

namespace CryptoApp.Web.Controllers;

public class HomeController : Controller
{
    private readonly ICryptocurrencyService _cryptoService;

    public HomeController(ICryptocurrencyService cryptoService)
    {
        _cryptoService = cryptoService;
    }

    public async Task<IActionResult> Index()
    {
        var prices = await _cryptoService.GetAllCryptocurrenciesAsync();
        return View(prices);
    }

    public IActionResult Privacy()
    {
        return View();
    }
}
