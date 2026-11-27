
using DhakaRent.DataAccess.Context;
using DhakaRent.DataAccess.Identity;
using DhakaRent.DataAccess.Repositories;
using DhakaRent.DataAccess.Transactions;
using DhakaRent.Facade.Interfaces;
using DhakaRent.Facade.Services;
using DhakaRent.Web.UI.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

var builder = WebApplication.CreateBuilder(args);
// Repository Dependency Injection
builder.Services.AddScoped<IPropertyRepository, PropertyRepository>();
builder.Services.AddScoped<IPropertyFacade, PropertyFacade>();

// New Building Management SaaS DI
builder.Services.AddScoped<IBuildingRepository, BuildingRepository>();

builder.Services.AddScoped<IBuildingFacade, BuildingFacade>();

// Floor Management DI
builder.Services.AddScoped<IFloorRepository, FloorRepository>();
builder.Services.AddScoped<IFloorFacade, FloorFacade>();

// Rental Unit Management DI
builder.Services.AddScoped<IRentalUnitRepository, RentalUnitRepository>();
builder.Services.AddScoped<IRentalUnitFacade, RentalUnitFacade>();

// Tenant Wishlist Management
builder.Services.AddScoped<ISavedPropertyRepository, SavedPropertyRepository>();
builder.Services.AddScoped<ISavedPropertyFacade, SavedPropertyFacade>();

// Property Inquiry Management
builder.Services.AddScoped<IPropertyInquiryRepository, PropertyInquiryRepository>();
builder.Services.AddScoped<IPropertyInquiryFacade, PropertyInquiryFacade>();

// Conversation and Messaging
builder.Services.AddScoped<IConversationRepository, ConversationRepository>();
builder.Services.AddScoped<IMessageRepository, MessageRepository>();

// Database Transaction
builder.Services.AddScoped<IDatabaseTransactionExecutor, DatabaseTransactionExecutor>();

// Messaging Business Logic
builder.Services.AddScoped<IMessagingFacade, MessagingFacade>();

// Viewing Request Module
builder.Services.AddScoped<IViewingRequestRepository, ViewingRequestRepository>();
builder.Services.AddScoped<IViewingRequestFacade, ViewingRequestFacade>();

// 1. Database Connection
var connectionString = builder.Configuration
    .GetConnectionString("DefaultConnection");

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString));

// 2. ASP.NET Core Identity Configuration
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    options.User.RequireUniqueEmail = true;

    options.Password.RequiredLength = 6;
    options.Password.RequireDigit = true;
    options.Password.RequireUppercase = false;
    options.Password.RequireLowercase = true;
    options.Password.RequireNonAlphanumeric = false;
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders();

// 3. MVC Services
builder.Services.AddScoped<DhakaRent.Web.UI.Services.CommunicationDirectory>();
builder.Services.AddScoped<DhakaRent.Web.UI.Services.ListingAssets>();
builder.Services.AddHostedService<DhakaRent.Web.UI.Services.RentalSchedule>();
builder.Services.AddScoped<DhakaRent.Web.UI.Services.OwnerCapacity>();
builder.Services.AddScoped<IRentalOperations, RentalOperations>();
builder.Services.AddControllersWithViews(options => options.Filters.Add<DhakaRent.Web.UI.Services.ConcurrencyFilter>());

builder.Services.AddHttpClient("geocoding", client =>
{
    client.Timeout = TimeSpan.FromSeconds(5);
    client.MaxResponseContentBufferSize = 64_000;
    client.DefaultRequestHeaders.UserAgent.ParseAdd("DhakaRent/1.0");
}).RemoveAllLoggers();
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("geocoding", _ => RateLimitPartition.GetFixedWindowLimiter("geocoding",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 1, Window = TimeSpan.FromSeconds(1) }));
    options.AddPolicy("recovery", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 12, Window = TimeSpan.FromMinutes(10) }));
});
var app = builder.Build();

// 4. Seed Roles and Admin Account
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;

    var roleManager =
        services.GetRequiredService<RoleManager<IdentityRole>>();

    // Create default roles first
    await RoleSeeder.SeedRolesAsync(roleManager);
}

// Create Admin account and assign Admin role
await AdminSeeder.SeedAsync(app.Services, app.Configuration);

// 5. HTTP Request Pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();
app.UseRateLimiter();

// Authentication must come before Authorization
app.UseAuthentication();
app.UseAuthorization();

// 6. Default MVC Route
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
