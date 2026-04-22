using MassTransit;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Notification.Infrastructure;
using Notification.Infrastructure.Services;
using Notification.Presentation.Hubs;
using Serilog;

var builder = WebApplication.CreateBuilder(args);
builder.Host.UseSerilog((ctx, config) =>
{
    config
        .ReadFrom.Configuration(ctx.Configuration)
        .Enrich.FromLogContext()
        .Enrich.WithMachineName()
        .Enrich.WithEnvironmentName()
        .WriteTo.Console(new Serilog.Formatting.Json.JsonFormatter());
});

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddHealthChecks();
builder.Services.AddSignalR();

builder.Services.AddDbContext<NotificationDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection")));

// services
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddInfrastructure(builder.Configuration);

//MassTransit
builder.Services.AddMassTransit(x =>
{
    x.AddConsumer<Notification.Presentation.Consumers.SessionSharedConsumer>();
    x.AddConsumer<Notification.Presentation.Consumers.DailyGoalAchievedConsumer>();
    x.AddConsumer<Notification.Presentation.Consumers.UserLoggedInConsumer>();


    x.UsingRabbitMq((ctx, cfg) =>
    {
        cfg.Host(builder.Configuration["RabbitMQ:Host"], h =>
        {
            h.Username(builder.Configuration["RabbitMQ:Username"]!);
            h.Password(builder.Configuration["RabbitMQ:Password"]!);
        });

        cfg.ConfigureEndpoints(ctx);
    });
});

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
{
    options.Authority = builder.Configuration["Auth:Authority"];
    options.Audience = builder.Configuration["Auth:Audience"];
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// auto apply migrations
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();
    db.Database.Migrate();
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseSerilogRequestLogging();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHub<NotificationHub>("/hubs/notifications");
app.MapHealthChecks("/healthz");

app.Run();
