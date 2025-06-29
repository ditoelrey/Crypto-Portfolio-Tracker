using CryptoApp.Domain.Models.Identity;
using CryptoApp.Web.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CryptoApp.Web.Areas.Identity.Pages.Account;

public class RegisterModel : PageModel
{
    private readonly SignInManager<CryptoApplicationUser> _signInManager;
    private readonly UserManager<CryptoApplicationUser> _userManager;
    private readonly ILogger<RegisterModel> _logger;

    public IList<AuthenticationScheme>? ExternalLogins { get; set; }

    public RegisterModel(
        UserManager<CryptoApplicationUser> userManager,
        SignInManager<CryptoApplicationUser> signInManager,
        ILogger<RegisterModel> logger)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _logger = logger;
    }

    [BindProperty]
    public RegisterDTO Input { get; set; } = new();

    public string? ReturnUrl { get; set; }    public async Task OnGetAsync(string? returnUrl = null)
    {
        ReturnUrl = returnUrl;
        ExternalLogins = (await _signInManager.GetExternalAuthenticationSchemesAsync()).ToList();
    }

    public async Task<IActionResult> OnPostAsync(string? returnUrl = null)
    {
        returnUrl ??= Url.Content("~/");
        ExternalLogins = (await _signInManager.GetExternalAuthenticationSchemesAsync()).ToList();

        if (ModelState.IsValid)
        {
            var user = new CryptoApplicationUser
            {
                UserName = Input.Email,
                Email = Input.Email,
                FirstName = Input.FirstName,
                LastName = Input.LastName,
                Address = Input.Address
            };

            // First create the user
            var result = await _userManager.CreateAsync(user, Input.Password);

            if (result.Succeeded)
            {
                _logger.LogInformation("User created a new account with password.");

                // Create and associate the portfolio after user creation
                user.Portfolio = new Domain.Models.Portfolio
                {
                    Id = Guid.NewGuid().ToString(),
                    Name = $"{Input.LastName}'s Portfolio",
                    Holdings = new List<Domain.Models.Holding>(),
                    Transactions = new List<Domain.Models.Transaction>(),
                    UserId = user.Id // Set the correct UserId
                };

                // Update the user to save the portfolio relationship
                await _userManager.UpdateAsync(user);

                _logger.LogInformation("Created portfolio for user.");

                await _signInManager.SignInAsync(user, isPersistent: false);
                return LocalRedirect(returnUrl);
            }
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }
        }

        // If we got this far, something failed, redisplay form
        return Page();
    }
}
