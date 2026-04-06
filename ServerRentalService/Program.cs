using Microsoft.EntityFrameworkCore;
using ServerRentalService.Data;
using ServerRentalService.HostedServices;
using ServerRentalService.Options;
using ServerRentalService.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();

builder.Services.Configure<ServerRentalOptions>(builder.Configuration.GetSection(ServerRentalOptions.SectionName));
builder.Services.AddSingleton(TimeProvider.System);

builder.Services.AddDbContextFactory<AppDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddScoped<IServerRentalService, ServerRentalService.Services.ServerRentalService>();
builder.Services.AddScoped<ServerLifecycleProcessor>();

builder.Services.AddHostedService<DatabaseInitializationHostedService>();
builder.Services.AddHostedService<RentalLifecycleHostedService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();

public partial class Program;
