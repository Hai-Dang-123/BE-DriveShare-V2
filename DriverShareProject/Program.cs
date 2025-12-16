using BLL.Hubs;
using Common.Settings;
using DAL.Context;
using DriverShareProject.Extentions.BuilderExtensions;
using DriverShareProject.Extentions.PolicyExtensions;
using DriverShareProject.Extentions.ServiceRegistration;
using DriverShareProject.Extentions.Startup;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

// Add services
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddHttpContextAccessor();
builder.Services.AddHttpClient();

// ✅ Đăng ký DbContext
builder.Services.AddApplicationServices(builder.Configuration);

builder.Services.AddControllers()
    .AddJsonOptions(opt =>
    {
        opt.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
        opt.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    });

builder.Services.RegisterAllServices(builder.Configuration);

// Add config
builder.AddAppConfiguration();

// Add Authorization policies
builder.Services.AddAuthorizationPolicies();

builder.Services.AddSignalR();
builder.Services.AddCorsPolicy();



// ======================================================================
// ✅ FIX 1: ĐĂNG KÝ CORS GLOBAL
// ======================================================================
//builder.Services.AddCors(options =>
//{
//    options.AddPolicy("AllowFrontend", policy =>
//    {
//        policy
//            .AllowAnyOrigin()
//            .AllowAnyHeader()
//            .AllowAnyMethod();
//    });
//});


// ======================================================================
var app = builder.Build();
// ======================================================================


// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Add custom middlewares
app.UseApplicationMiddlewares();


// ======================================================================
// ✅ FIX 2: BẬT CORS đúng vị trí (PHẢI TRƯỚC Authentication/Authorization)
// ======================================================================
app.UseCors("DefaultCorsPolicy");

// Map Hub Endpoint
app.MapHub<TripTrackingHub>("/hubs/tracking");

app.UseAuthentication();
app.UseAuthorization();

// Map Controllers
app.MapControllers();

app.Run();
