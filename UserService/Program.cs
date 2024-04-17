using Microsoft.EntityFrameworkCore;
using UserService;
using UserService.Data;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddDbContext<UserServiceContext>(options =>
    options.UseSqlite(@"Data Source=user.db"));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddSingleton<IntegrationEventService>();
builder.Services.AddHostedService<IntegrationEventService>(provider => provider.GetService<IntegrationEventService>());

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    using (var scope = app.Services.CreateScope())
    {
        var dbContext = scope.ServiceProvider.GetRequiredService<UserServiceContext>();
        dbContext.Database.EnsureCreated();
    }

    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthorization();

app.MapControllers();

app.Run();
