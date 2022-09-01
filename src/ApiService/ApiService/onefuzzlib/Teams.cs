﻿using System.Net.Http;
using System.Text.Json;

namespace Microsoft.OneFuzz.Service;

public interface ITeams {
    Async.Task NotifyTeams(TeamsTemplate config, Container container, string filename, IReport reportOrRegression);
}

public class Teams : ITeams {
    private readonly ILogTracer _logTracer;
    private readonly IOnefuzzContext _context;
    private readonly IHttpClientFactory _httpFactory;

    public Teams(IHttpClientFactory httpFactory, ILogTracer logTracer, IOnefuzzContext context) {
        _logTracer = logTracer;
        _context = context;
        _httpFactory = httpFactory;
    }

    private static string CodeBlock(string data) {
        data = data.Replace("`", "``");
        return $"\n```\n{data}\n```\n";
    }

    private async Async.Task SendTeamsWebhook(TeamsTemplate config, string title, IList<Dictionary<string, string>> facts, string? text) {
        title = MarkdownEscape(title);

        var sections = new List<Dictionary<string, string>>() {
            new() {
                {"activityTitle", title},
                {"facts", JsonSerializer.Serialize(facts)}
            }
        };
        if (text != null) {
            sections.Add(new() {
                { "text", text }
            });
        }

        var message = new Dictionary<string, string>() {
            {"@type", "MessageCard"},
            {"@context", "https://schema.org/extensions"},
            {"summary", title},
            {"sections", JsonSerializer.Serialize(sections)}
        };

        var configUrl = await _context.SecretsOperations.GetSecretStringValue(config.Url);
        var client = new Request(_httpFactory.CreateClient());
        var response = await client.Post(url: new Uri(configUrl!), JsonSerializer.Serialize(message));
        if (response == null || !response.IsSuccessStatusCode) {
            _logTracer.Error($"webhook failed {response?.StatusCode} {response?.Content}");
        }
    }

    public async Async.Task NotifyTeams(TeamsTemplate config, Container container, string filename, IReport reportOrRegression) {
        var facts = new List<Dictionary<string, string>>();
        string? text = null;
        var title = string.Empty;

        if (reportOrRegression is Report report) {
            var task = await _context.TaskOperations.GetByJobIdAndTaskId(report.JobId, report.TaskId);
            if (task == null) {
                _logTracer.Error($"report with invalid task {report.JobId}:{report.TaskId}");
                return;
            }

            title = $"new crash in {report.Executable}: {report.CrashType} @ {report.CrashSite}";

            var links = new List<string> {
                $"[report]({await _context.Containers.AuthDownloadUrl(container, filename)})"
            };

            var setupContainer = Scheduler.GetSetupContainer(task.Config);
            if (setupContainer != null) {
                var setup = "setup/";
                int index = report.Executable.IndexOf(setup, StringComparison.InvariantCultureIgnoreCase);
                var setupFileName = (index < 0) ? report.Executable : report.Executable.Remove(index, setup.Length);
                links.Add(
                    $"[executable]({await _context.Containers.AuthDownloadUrl(setupContainer, setupFileName)})"
                );
            }

            if (report.InputBlob != null) {
                links.Add(
                    $"[input]({await _context.Containers.AuthDownloadUrl(report.InputBlob.Container, report.InputBlob.Name)})"
                );
            }

            facts.AddRange(new List<Dictionary<string, string>> {
                new() {{"name", "Files"}, {"value", string.Join(" | ", links)}},
                new() {
                    {"name", "Task"},
                    {"value", MarkdownEscape(
                        $"job_id: {report.JobId} task_id: {report.TaskId}"
                    )}
                },
                new() {
                    {"name", "Repro"},
                    {"value", CodeBlock($"onefuzz repro create_and_connect {container} {filename}")}
                }
            });

            text = "## Call Stack\n" + string.Join("\n", report.CallStack.Select(cs => CodeBlock(cs)));
        } else {
            title = "new file found";
            var fileUrl = await _context.Containers.AuthDownloadUrl(container, filename);

            facts.Add(new Dictionary<string, string>() {
                {"name", "file"},
                {"value", $"[{MarkdownEscape(container.ContainerName)}/{MarkdownEscape(filename)}]({fileUrl})"}
            });
        }

        await SendTeamsWebhook(config, title, facts, text);
    }

    private static string MarkdownEscape(string data) {
        var values = "\\*_{}[]()#+-.!";
        foreach (var c in values) {
            data = data.Replace(c.ToString(), "\\" + c);
        }
        data = data.Replace("`", "``");
        return data;
    }
}
