using MassTransit;
using Microsoft.EntityFrameworkCore;
using RewardWorker;
using RewardWorker.Consumers;
using RewardWorker.Services;
using Serilog;
using Serilog.Formatting.Json;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddSerilog((ctx, config) =>
{
    config
        .ReadFrom.Configuration(builder.Configuration)
        .Enrich.FromLogContext()
        .Enrich.WithMachineName()
        .Enrich.WithEnvironmentName()
        .WriteTo.Console(new JsonFormatter());
});

// same database as Session service
builder.Services.AddDbContext<RewardDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddScoped<DailyGoalEvaluator>();

builder.Services.AddMassTransit(x =>
{
    x.AddConsumer<SessionCreatedConsumer>();

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

var host = builder.Build();
host.Run();