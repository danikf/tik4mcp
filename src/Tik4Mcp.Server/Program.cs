using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Tik4Mcp.Server.Configuration;
using Tik4Mcp.Server.Connections;
using Tik4Mcp.Server.Security;
using Tik4Mcp.Server.Tools;

var builder = Host.CreateApplicationBuilder(args);

// MCP uses the stdio transport — stdout carries the protocol, so all logging goes to stderr.
builder.Logging.ClearProviders();
builder.Logging.AddConsole(o => o.LogToStandardErrorThreshold = LogLevel.Trace);
builder.Logging.SetMinimumLevel(LogLevel.Information);

// Configuration: appsettings.json holds the (non-secret) inventory under the "Tik4Mcp" section.
// TIK4MCP_-prefixed environment variables overlay it for secrets/overrides — and the part AFTER the
// prefix maps straight onto the options, so the documented short form works:
//   TIK4MCP_ReadOnly=false
//   TIK4MCP_Routers__core__ReadOnly=false
//   TIK4MCP_Routers__core__Password=secret
// (We bind the section first, then overlay only the TIK4MCP_-prefixed vars — built as an isolated
// source so unrelated machine environment variables can never leak into the options.)
var envOverrides = new ConfigurationBuilder()
    .AddEnvironmentVariables(prefix: "TIK4MCP_")
    .Build();

builder.Services.Configure<Tik4McpOptions>(options =>
{
    builder.Configuration.GetSection(Tik4McpOptions.SectionName).Bind(options);
    envOverrides.Bind(options);
});

builder.Services.AddSingleton<AccessPolicy>();
builder.Services.AddSingleton<ConnectionResolver>();

builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithTools<RouterCommandTools>()
    .WithTools<SafeBatchTools>()
    .WithTools<SystemTools>()
    .WithTools<DiscoveryTools>()
    .WithTools<InventoryTools>()
    .WithTools<ConnectionInfoTools>();

await builder.Build().RunAsync();
