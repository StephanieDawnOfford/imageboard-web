using Imageboard_illust.Database;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Imageboard_illust.Web.Pages;

public class RegisterModel : PageModel
{
    private readonly AppDatabase _db;

    public RegisterModel(AppDatabase db, OAuthProviderStatus oauth)
    {
        _db = db;
        OAuth = oauth;
    }

    public OAuthProviderStatus OAuth { get; }

    [BindProperty]
    public string Email { get; set; } = string.Empty;

    [BindProperty]
    public string Password { get; set; } = string.Empty;

    [BindProperty]
    public string ConfirmPassword { get; set; } = string.Empty;

    public void OnGet()
    {
    }

    public IActionResult OnPost()
    {
        if (string.IsNullOrWhiteSpace(Email) || string.IsNullOrWhiteSpace(Password) || string.IsNullOrWhiteSpace(ConfirmPassword))
        {
            ModelState.AddModelError(string.Empty, "All fields are required.");
            return Page();
        }

        if (Password != ConfirmPassword)
        {
            ModelState.AddModelError(string.Empty, "Passwords do not match.");
            return Page();
        }

        var user = _db.RegisterUser(Email.Trim(), Password);
        if (user == null)
        {
            ModelState.AddModelError(string.Empty, "An account already exists with that email.");
            return Page();
        }

        HttpContext.Session.SetInt32("UserId", user.Id);
        HttpContext.Session.SetString("UserEmail", user.Email);
        return RedirectToPage("/Index");
    }
}
