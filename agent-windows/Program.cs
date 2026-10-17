using Fleetwatch.Agent;

// Fleetwatch Windows agent. Runs as a Windows Service (installed by the
// installer) or as a plain console app for testing.
var builder = Host.CreateApplicationBuilder(args);

// Lets the same exe run as a real Windows Service.
builder.Services.AddWindowsService(options =>
{
    options.ServiceName = "FleetwatchAgent";
});

// Bind the "Fleetwatch" section of appsettings.json to AgentOptions.
builder.Services.Configure<AgentOptions>(builder.Configuration.GetSection("Fleetwatch"));

builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
