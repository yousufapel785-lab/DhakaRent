using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Net.Mail;
using DhakaRent.DataAccess.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace DhakaRent.Web.UI.Controllers;

public class PasswordResetForm
{
    [Required, EmailAddress] public string Email { get; set; } = "";
    [Required] public string Token { get; set; } = "";
    [Required, DataType(DataType.Password), StringLength(100, MinimumLength = 6)]
    public string Password { get; set; } = "";
}

[AllowAnonymous, AutoValidateAntiforgeryToken, EnableRateLimiting("recovery")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public class PasswordRecoveryController(UserManager<ApplicationUser> users, IConfiguration config,
    ILogger<PasswordRecoveryController> logger) : Controller
{
    private Uri? Site => Uri.TryCreate(config["Mail:PublicOrigin"], UriKind.Absolute, out var url) &&
        url.Scheme == Uri.UriSchemeHttps ? url : null;

    [HttpGet]
    public IActionResult Index() => View();

    [HttpPost]
    public async Task<IActionResult> Send([EmailAddress] string email)
    {
        if (Site == null || string.IsNullOrWhiteSpace(config["Mail:Host"]) ||
            !MailAddress.TryCreate(config["Mail:From"], out var sender))
        {
            TempData["RecoveryMessage"] = "Email recovery is awaiting mail service configuration.";
            return RedirectToAction(nameof(Index));
        }
        if (ModelState.IsValid && await users.FindByEmailAsync(email.Trim()) is { } account)
        {
            var token = await users.GeneratePasswordResetTokenAsync(account);
            var path = Url.Action(nameof(Reset), "PasswordRecovery")!;
            var secret = QueryString.Create(new Dictionary<string, string?> { ["email"] = account.Email, ["token"] = token });
            var resetLink = new UriBuilder(new Uri(Site, path)) { Fragment = secret.Value![1..] }.Uri;
            using var mail = new MailMessage(sender, new MailAddress(account.Email!))
            {
                Subject = "Reset your DhakaRent password",
                Body = $"Open this link to choose a new password. If you did not request it, you can ignore this email.\n\n{resetLink.AbsoluteUri}"
            };
            using var smtp = new SmtpClient(config["Mail:Host"], config.GetValue("Mail:Port", 587)) { EnableSsl = true };
            if (config["Mail:Username"] is { Length: > 0 } username)
                smtp.Credentials = new NetworkCredential(username, config["Mail:Password"]);
            try { await smtp.SendMailAsync(mail); }
            catch (SmtpException) { logger.LogError("Password recovery email delivery failed."); }
        }
        TempData["RecoveryMessage"] = "If an account matches that email, check your inbox for the reset link.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public IActionResult Reset() => View(new PasswordResetForm());

    [HttpPost]
    public async Task<IActionResult> Reset(PasswordResetForm form)
    {
        Response.Headers["Referrer-Policy"] = "no-referrer";
        if (!ModelState.IsValid) return View(form);
        var user = await users.FindByEmailAsync(form.Email);
        if (user != null && (await users.ResetPasswordAsync(user, form.Token, form.Password)).Succeeded)
            return RedirectToAction("Login", "Account");
        ModelState.AddModelError("", "Check the password requirements and request a new reset link if this one has expired.");
        return View(form);
    }
}
