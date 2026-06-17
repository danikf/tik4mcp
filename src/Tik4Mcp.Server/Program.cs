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

// Configuration: appsettings.json (inventory, non-secret) overlaid by TIK4MCP_-prefixed env vars
// (credentials). E.g. TIK4MCP_Routers__core__Password=secret, TIK4MCP_ReadOnly=false.
builder.Configuration.AddEnvironmentVariables("TIK4MCP_");

builder.Services.Configure<Tik4McpOptions>(
    builder.Configuration.GetSection(Tik4McpOptions.SectionName));

builder.Services.AddSingleton<AccessPolicy>();
builder.Services.AddSingleton<ConnectionResolver>();

builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithTools<RouterCommandTools>()
    .WithTools<SystemTools>()
    .WithTools<RouterStateTools>()
    .WithTools<DiscoveryTools>()
    .WithTools<InventoryTools>();

await builder.Build().RunAsync();
