using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using SocialTennis;
using SocialTennis.Players;
using SocialTennis.Storage;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped<IKeyValueStorage, LocalStorage>();
builder.Services.AddScoped<PlayerService>();

await builder.Build().RunAsync();
