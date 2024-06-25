using Microsoft.OpenApi.Models;
using WebApiApp;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseWindowsService();
builder.Services.AddWindowsService();
if (Environment.UserInteractive)
{
    Directory.SetCurrentDirectory(@"C:\OPCUA\");
}
else
{
    Directory.SetCurrentDirectory(AppDomain.CurrentDomain.BaseDirectory);
}

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "Web OPC Api", Description = "Web Api for modular opc ua server", Version = "v1" });
});

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", builder =>
    {
        builder.AllowAnyOrigin()
               .AllowAnyHeader()
               .AllowAnyMethod();
    });
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "WebAPI for OPCUA server v1");
    });
    app.MapGet("/", () => Results.Redirect("/swagger/"));
}

app.UseCors("AllowAll");
var configBuilder = new ConfigurationBuilder().AddJsonFile("appSettings.json", false, true);
var config = configBuilder.Build();

OpcUaServerController server = new(config);

app.MapPost("/Opcua/start", () => server.StartServer());
app.MapPost("/Opcua/stop", () => server.StopServer());

app.MapGet("/Opcua/modules", () => server.GetModules());
app.MapPost("/Opcua/modules/stop/{id}", (string id) => server.StopThread(id));
app.MapPost("/Opcua/modules/stop", () => server.StopAllModules());
app.MapPost("/Opcua/modules/start/{id}", (string id) => server.StartThread(id));
app.MapPost("/Opcua/modules/start", () => server.StartAllModules());
app.MapPost("/Opcua/modulewatcher/stop", () => server.StopModuleWatcher());
app.MapPost("/Opcua/modulewatcher/start", () => server.StartModuleWatcher());

app.MapGet("/Opcua/spacenames", () => server.GetSpaceNames());
app.MapDelete("/Opcua/spacenames/delete/{id}", (string id) => server.DeleteSpaceName(id));

app.MapGet("/Opcua/nodes", () => server.GetNodes());
app.MapGet("/Opcua/nodes/{id}", (int id) => server.GetNode(id));

app.MapGet("/Opcua/port", () => server.GetOpcPort());
app.MapPost("/Opcua/port/{port}", (int port) => server.SetOpcPort(port));

app.MapGet("/Opcua/status", () => server.GetOpcServerStatus());

app.Run();
