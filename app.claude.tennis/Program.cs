using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using SocialTennis;
using SocialTennis.Navigation;
using SocialTennis.Players;
using SocialTennis.Scheduling;
using SocialTennis.Settings;
using SocialTennis.Storage;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped<IKeyValueStorage, LocalStorage>();
builder.Services.AddScoped<PlayerService>();
builder.Services.AddScoped<ScheduleService>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<AppStart>();
builder.Services.AddScoped<ThemeService>();
builder.Services.AddScoped<VersionService>();
builder.Services.AddScoped<BackupFiles>();
builder.Services.AddScoped(_ => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });

await builder.Build().RunAsync();
