using System.Net;
using System.Security.Claims;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Mvc;
using Panaderia.MVC.Filters;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls("http://127.0.0.1:0");
builder.Logging.ClearProviders();
builder.Services.AddControllersWithViews();
builder.Services.AddAntiforgery();
builder.Services.AddDataProtection().UseEphemeralDataProtectionProvider();
await using var app = builder.Build();
// Identidad controlada solo en este servidor de pruebas; no usa datos ni servicios de producción.
app.Use(async (context, next) =>
{
    if (context.Request.Headers["X-Test-Authenticated"] == "true")
        context.User = new ClaimsPrincipal(new ClaimsIdentity(
            new[] { new Claim(ClaimTypes.NameIdentifier, "test-admin") }, "test"));
    await next();
});
app.MapControllers();
await app.StartAsync();
var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
using var client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false, CookieContainer = new() }) { BaseAddress = new Uri(address) };

void Check(bool condition, string message)
{
    if (!condition) throw new Exception(message);
    Console.WriteLine("PASS: " + message);
}

async Task<HttpResponseMessage> Submit(string token) => await client.PostAsync("/Account/Login",
    new FormUrlEncodedContent(new Dictionary<string, string> { ["__RequestVerificationToken"] = token }));

var token = await client.GetStringAsync("/Account/Login");
using (var valid = await Submit(token))
    Check(valid.StatusCode == HttpStatusCode.OK && AccountController.Executions == 1, "Valid token executes the login action");

using (var invalid = await Submit("invalid"))
{
    Check(invalid.StatusCode == HttpStatusCode.Redirect && AccountController.Executions == 1,
        "Invalid token redirects without executing login");
    Check(invalid.Headers.Location!.OriginalString.Contains("formularioVencido=True", StringComparison.OrdinalIgnoreCase),
        "Recovery uses a GET with an expired-form indicator");
    using var fresh = await client.GetAsync(invalid.Headers.Location);
    Check(fresh.IsSuccessStatusCode, "Anonymous recovery returns a fresh form instead of HTTP 400");
}

client.DefaultRequestHeaders.Add("X-Test-Authenticated", "true");
using (var stale = await Submit(token))
{
    Check(stale.StatusCode == HttpStatusCode.Redirect && AccountController.Executions == 1,
        "Anonymous token submitted after authentication is rejected and recovered");
    var destination = await client.GetStringAsync(stale.Headers.Location);
    Check(destination == "panel", "Authenticated recovery reaches the panel without resubmitting credentials");
}

using (var other = await client.GetAsync("/Account/OtherBadRequest"))
    Check(other.StatusCode == HttpStatusCode.BadRequest, "Unrelated bad requests remain HTTP 400");
using (var otherPost = await client.PostAsync("/Account/OtherPost", new FormUrlEncodedContent(new Dictionary<string, string>())))
    Check(otherPost.StatusCode == HttpStatusCode.BadRequest, "Other forms retain normal antiforgery rejection");

await app.StopAsync();

[Route("Account")]
public class AccountController : Controller
{
    public static int Executions;

    [HttpGet("Login")]
    [ActionName("Login")]
    public IActionResult Form([FromServices] IAntiforgery antiforgery) =>
        Content(User.Identity?.IsAuthenticated == true ? "panel" : antiforgery.GetAndStoreTokens(HttpContext).RequestToken!);

    [HttpPost("Login")]
    [ValidateAntiForgeryToken]
    [RecoverLoginAntiforgery]
    public IActionResult Submit() { Executions++; return Ok(); }

    [HttpGet("OtherBadRequest")]
    [RecoverLoginAntiforgery]
    public IActionResult OtherBadRequest() => BadRequest();

    [HttpPost("OtherPost")]
    [ValidateAntiForgeryToken]
    public IActionResult OtherPost() => Ok();
}


