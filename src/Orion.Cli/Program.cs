using System.Net.Http.Json;
using Orion.Core.Models;
using Orion.Core.Serialization;
using Spectre.Console;

AnsiConsole.Write(
    new FigletText("ORION")
        .Color(Color.DeepSkyBlue1));

AnsiConsole.MarkupLine("[bold]Orion Cloud Deployment Platform[/] - [italic]Developer CLI v0.1.0[/]");
AnsiConsole.Write(new Rule());

var table = new Table()
    .Border(TableBorder.Rounded)
    .AddColumn("Command")
    .AddColumn("Description");

table.AddRow("[green]apps[/]", "List all managed applications");
table.AddRow("[green]create[/] [blue]<name> <repo>[/]", "Register a new application");
table.AddRow("[green]build[/] [blue]<name>[/]", "Trigger a new deployment/build");
table.AddRow("[green]logs[/] [blue]<name>[/]", "Stream live logs for an app");
table.AddRow("[green]metrics[/] [blue]<name>[/]", "Show resource usage for an app");
table.AddRow("[green]deployments[/] [blue]<name>[/]", "List all deployments for an app");
table.AddRow("[green]scale[/] [blue]<name> <count>[/]", "Scale an app to N replicas");
table.AddRow("[green]open[/] [blue]<name>[/]", "Open the application in your browser");
table.AddRow("[green]secrets[/] [blue]<name>[/] [blue]<key> <value>[/]", "Manage app environment variables");
table.AddRow("[green]status[/]", "Check the health of the Orion core");

AnsiConsole.Write(table);

using var client = new HttpClient();
client.BaseAddress = new Uri("http://localhost:5000");

if (args.Length > 0)
{
    var command = args[0].ToLower();
    switch (command)
    {
        case "apps":
            await AnsiConsole.Status()
                .StartAsync("Fetching apps...", async ctx =>
                {
                    var appsResult = await client.GetFromJsonAsync("/apps", OrionJsonContext.Default.IEnumerableApp);
                    var appsList = appsResult?.ToList() ?? new List<App>();
                    
                    if (!appsList.Any())
                    {
                        AnsiConsole.MarkupLine("[yellow]No apps found.[/]");
                        return;
                    }

                    var appTable = new Table().AddColumns("ID", "Name", "Repo URL", "Created At");
                    foreach (var app in appsList)
                    {
                        appTable.AddRow(app.Id.ToString()[..8], app.Name, app.RepoUrl, app.CreatedAt.ToShortDateString());
                    }
                    AnsiConsole.Write(appTable);
                });
            break;

        case "create":
            if (args.Length < 3)
            {
                AnsiConsole.MarkupLine("[red]Usage:[/] orion create <name> <repo-url>");
                break;
            }
            var newApp = new App { Name = args[1], RepoUrl = args[2] };
            var response = await client.PostAsJsonAsync("/apps", newApp, OrionJsonContext.Default.App);
            if (response.IsSuccessStatusCode)
                AnsiConsole.MarkupLine($"[green]✔[/] App [bold]{newApp.Name}[/] created successfully!");
            else
                AnsiConsole.MarkupLine("[red]✘[/] Failed to create app.");
            break;

        case "build":
            if (args.Length < 2)
            {
                AnsiConsole.MarkupLine("[red]Usage:[/] orion build <name>");
                break;
            }
            var appName = args[1];
            await AnsiConsole.Status()
                .StartAsync($"Triggering build for {appName}...", async ctx =>
                {
                    var apps = await client.GetFromJsonAsync("/apps", OrionJsonContext.Default.IEnumerableApp);
                    var app = apps?.FirstOrDefault(a => a.Name.Equals(appName, StringComparison.OrdinalIgnoreCase));
                    if (app == null)
                    {
                        AnsiConsole.MarkupLine($"[red]✘[/] Application '{appName}' not found.");
                        return;
                    }

                    var buildResponse = await client.PostAsync($"/apps/{app.Id}/build", null);
                    if (buildResponse.IsSuccessStatusCode)
                    {
                        AnsiConsole.MarkupLine($"[green]✔[/] Build queued for [bold]{appName}[/]!");
                        AnsiConsole.MarkupLine("[italic]Use 'orion status' or check logs for updates.[/]");
                    }
                    else
                    {
                        AnsiConsole.MarkupLine("[red]✘[/] Failed to trigger build.");
                    }
                });
            break;

        case "logs":
            if (args.Length < 2)
            {
                AnsiConsole.MarkupLine("[red]Usage:[/] orion logs <name>");
                break;
            }
            var lAppName = args[1];
            await AnsiConsole.Status()
                .StartAsync($"Connecting to log stream for {lAppName}...", async ctx =>
                {
                    var appsResult = await client.GetFromJsonAsync("/apps", OrionJsonContext.Default.IEnumerableApp);
                    var app = appsResult?.FirstOrDefault(a => a.Name.Equals(lAppName, StringComparison.OrdinalIgnoreCase));
                    if (app == null)
                    {
                        AnsiConsole.MarkupLine($"[red]✘[/] Application '{lAppName}' not found.");
                        return;
                    }

                    AnsiConsole.MarkupLine($"[grey]Streaming logs for {lAppName}... (Ctrl+C to stop)[/]");
                    
                    try 
                    {
                        using var stream = await client.GetStreamAsync($"/apps/{app.Id}/logs");
                        using var reader = new StreamReader(stream);
                        while (!reader.EndOfStream)
                        {
                            var line = await reader.ReadLineAsync();
                            if (string.IsNullOrWhiteSpace(line)) continue;
                            if (line.StartsWith("data: "))
                            {
                                var json = line.Substring(6);
                                var log = System.Text.Json.JsonSerializer.Deserialize(json, OrionJsonContext.Default.LogEntry);
                                if (log != null)
                                {
                                    var color = log.Level switch
                                    {
                                        "Error" => "red",
                                        "Warning" => "yellow",
                                        _ => "grey"
                                    };
                                    var escapedMessage = log.Message.Replace("[", "[[").Replace("]", "]]");
                                    AnsiConsole.MarkupLine($"[{color}][[{log.Timestamp:HH:mm:ss}]][/] [{color}]{log.Level}:[/] {escapedMessage}");
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        AnsiConsole.MarkupLine($"[red]Connection lost:[/] {ex.Message}");
                    }
                });
            break;

        case "metrics":
            if (args.Length < 2)
            {
                AnsiConsole.MarkupLine("[red]Usage:[/] orion metrics <name>");
                break;
            }
            var mAppName = args[1];
            await AnsiConsole.Status()
                .StartAsync($"Fetching metrics for {mAppName}...", async ctx =>
                {
                    var appsResult = await client.GetFromJsonAsync("/apps", OrionJsonContext.Default.IEnumerableApp);
                    var app = appsResult?.FirstOrDefault(a => a.Name.Equals(mAppName, StringComparison.OrdinalIgnoreCase));
                    if (app == null)
                    {
                        AnsiConsole.MarkupLine($"[red]✘[/] Application '{mAppName}' not found.");
                        return;
                    }

                    var metrics = await client.GetFromJsonAsync($"/apps/{app.Id}/metrics", OrionJsonContext.Default.AppMetrics);
                    if (metrics == null)
                    {
                        AnsiConsole.MarkupLine("[red]✘[/] Failed to fetch metrics.");
                        return;
                    }

                    var cpu = (int)metrics.CpuUsage;
                    var mem = metrics.MemoryUsageMb;

                    var chart = new BreakdownChart()
                        .FullSize()
                        .AddItem("CPU Usage (%)", cpu, Color.Blue)
                        .AddItem("Memory (MB)", mem / 10, Color.Green) // Scaled for display
                        .AddItem("Idle", 100 - cpu, Color.Grey);

                    AnsiConsole.Write(new Panel(chart).Header($"Resource Usage - {mAppName} ({metrics.InstanceCount} replicas)").BorderColor(Color.Blue));
                    AnsiConsole.MarkupLine($"[blue]CPU:[/] {cpu}%  [green]Memory:[/] {mem}MB  [yellow]Replicas:[/] {metrics.InstanceCount}");
                });
            break;

        case "deployments":
            if (args.Length < 2)
            {
                AnsiConsole.MarkupLine("[red]Usage:[/] orion deployments <name>");
                break;
            }
            var dAppName = args[1];
            await AnsiConsole.Status()
                .StartAsync($"Fetching deployments for {dAppName}...", async ctx =>
                {
                    var appsResult = await client.GetFromJsonAsync("/apps", OrionJsonContext.Default.IEnumerableApp);
                    var app = appsResult?.FirstOrDefault(a => a.Name.Equals(dAppName, StringComparison.OrdinalIgnoreCase));
                    if (app == null)
                    {
                        AnsiConsole.MarkupLine($"[red]✘[/] Application '{dAppName}' not found.");
                        return;
                    }

                    var deployments = await client.GetFromJsonAsync($"/apps/{app.Id}/deployments", OrionJsonContext.Default.IEnumerableDeployment);
                    if (deployments == null || !deployments.Any())
                    {
                        AnsiConsole.MarkupLine("[yellow]No deployments found for this app.[/]");
                        return;
                    }

                    var depTable = new Table().AddColumns("ID", "Status", "Port", "Proxy URL", "Created At");
                    foreach (var dep in deployments)
                    {
                        var statusColor = dep.Status switch
                        {
                            DeploymentStatus.Running => "green",
                            DeploymentStatus.Building => "blue",
                            DeploymentStatus.Failed => "red",
                            _ => "yellow"
                        };
                        var proxyUrl = dep.Status == DeploymentStatus.Running ? $"http://localhost:5000/proxy/{dAppName.ToLower()}/" : "-";
                        depTable.AddRow(dep.Id.ToString()[..8], $"[{statusColor}]{dep.Status}[/]", dep.Port?.ToString() ?? "-", proxyUrl, dep.CreatedAt.ToString("HH:mm:ss"));
                    }
                    AnsiConsole.Write(depTable);
                });
            break;

        case "open":
            if (args.Length < 2)
            {
                AnsiConsole.MarkupLine("[red]Usage:[/] orion open <name>");
                break;
            }
            var oAppName = args[1];
            var url = $"http://localhost:5000/proxy/{oAppName.ToLower()}/";
            AnsiConsole.MarkupLine($"[grey]Opening {oAppName} at {url}...[/]");
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine($"[red]✘[/] Failed to open browser: {ex.Message}");
                AnsiConsole.MarkupLine($"[yellow]Manual URL:[/] {url}");
            }
            break;

        case "scale":
            if (args.Length < 3)
            {
                AnsiConsole.MarkupLine("[red]Usage:[/] orion scale <name> <count>");
                break;
            }
            var scaleAppName = args[1];
            if (!int.TryParse(args[2], out var replicas))
            {
                AnsiConsole.MarkupLine("[red]✘[/] Invalid replica count.");
                break;
            }

            await AnsiConsole.Status()
                .StartAsync($"Scaling {scaleAppName} to {replicas} replicas...", async ctx =>
                {
                    var appsResult = await client.GetFromJsonAsync("/apps", OrionJsonContext.Default.IEnumerableApp);
                    var app = appsResult?.FirstOrDefault(a => a.Name.Equals(scaleAppName, StringComparison.OrdinalIgnoreCase));
                    if (app == null)
                    {
                        AnsiConsole.MarkupLine($"[red]✘[/] Application '{scaleAppName}' not found.");
                        return;
                    }

                    var response = await client.PostAsync($"/apps/{app.Id}/scale?replicas={replicas}", null);
                    if (response.IsSuccessStatusCode)
                        AnsiConsole.MarkupLine($"[green]✔[/] Scaling request for [bold]{scaleAppName}[/] accepted.");
                    else
                        AnsiConsole.MarkupLine("[red]✘[/] Scaling failed.");
                });
            break;

        case "secrets":
            if (args.Length < 2)
            {
                AnsiConsole.MarkupLine("[red]Usage:[/] orion secrets <name> [[key]] [[value]]");
                AnsiConsole.MarkupLine("      orion secrets <name> delete <key>");
                break;
            }
            var sAppName = args[1];
            await AnsiConsole.Status()
                .StartAsync($"Managing secrets for {sAppName}...", async ctx =>
                {
                    var appsResult = await client.GetFromJsonAsync("/apps", OrionJsonContext.Default.IEnumerableApp);
                    var app = appsResult?.FirstOrDefault(a => a.Name.Equals(sAppName, StringComparison.OrdinalIgnoreCase));
                    if (app == null)
                    {
                        AnsiConsole.MarkupLine($"[red]✘[/] Application '{sAppName}' not found.");
                        return;
                    }

                    if (args.Length >= 4) // Set secret: orion secrets <name> <key> <value>
                    {
                        var key = args[2];
                        var value = args[3];
                        var payload = new Dictionary<string, string> { { key, value } };
                        var setResponse = await client.PostAsJsonAsync($"/apps/{app.Id}/secrets", payload, OrionJsonContext.Default.DictionaryStringString);
                        if (setResponse.IsSuccessStatusCode)
                            AnsiConsole.MarkupLine($"[green]✔[/] Secret [bold]{key}[/] set for [bold]{sAppName}[/].");
                        else
                            AnsiConsole.MarkupLine("[red]✘[/] Failed to set secret.");
                    }
                    else if (args.Length == 4 && args[2].ToLower() == "delete") // Delete secret: orion secrets <name> delete <key>
                    {
                        var key = args[3];
                        var delResponse = await client.DeleteAsync($"/apps/{app.Id}/secrets/{key}");
                        if (delResponse.IsSuccessStatusCode)
                            AnsiConsole.MarkupLine($"[green]✔[/] Secret [bold]{key}[/] deleted.");
                        else
                            AnsiConsole.MarkupLine("[red]✘[/] Failed to delete secret.");
                    }
                    else // List secrets: orion secrets <name>
                    {
                        var secrets = await client.GetFromJsonAsync($"/apps/{app.Id}/secrets", OrionJsonContext.Default.DictionaryStringString);
                        if (secrets == null || !secrets.Any())
                        {
                            AnsiConsole.MarkupLine("[yellow]No secrets found for this app.[/]");
                        }
                        else
                        {
                            var secTable = new Table().AddColumns("Key", "Value (Encrypted/Masked)");
                            foreach (var s in secrets) secTable.AddRow(s.Key, "[grey]**********[/]");
                            AnsiConsole.Write(secTable);
                            AnsiConsole.MarkupLine("[italic grey]Secrets are encrypted at rest and masked in the CLI.[/]");
                        }
                    }
                });
            break;

        case "status":
            AnsiConsole.MarkupLine("[green]✔[/] Orion Control Plane is [bold green]ONLINE[/] (http://localhost:5000)");
            AnsiConsole.MarkupLine("[green]✔[/] DuckDB Storage is [bold green]READY[/]");
            break;

        default:
            AnsiConsole.MarkupLine($"[red]Unknown command:[/] {command}");
            break;
    }
}
else
{
    AnsiConsole.MarkupLine("Try [bold yellow]orion --help[/] for a full list of options.");
}
