using System.Text.Json;

/// <summary>Runs approved workflows sequentially and persists every step transition.</summary>
sealed class WorkflowWorker(
    WorkflowStore store,
    IHttpClientFactory clients,
    IConfiguration config,
    GoogleTokenStore google,
    FeatureAccess access,
    OrbitDatabase? database = null) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var id in store.Queue.Reader.ReadAllAsync(stoppingToken))
        {
            await RunWorkflow(store.Items[id], stoppingToken);
        }
    }

    private async Task RunWorkflow(Workflow workflow, CancellationToken cancellationToken)
    {
        database?.VerifyWorkerLease();
        lock (workflow.Sync)
        {
            workflow.Status = "running";
            store.Persist(workflow);
        }

        foreach (var step in workflow.Steps)
        {
            database?.VerifyWorkerLease();
            lock (workflow.Sync)
            {
                step.Status = "running";
                store.Persist(workflow);
            }

            try
            {
                var result = await ExecuteStep(workflow, step, cancellationToken);
                lock (workflow.Sync)
                {
                    step.Status = "completed";
                    step.Result = result;
                    store.Persist(workflow);
                }
            }
            catch (Exception exception) when (IsStepFailure(exception))
            {
                FailWorkflow(workflow, step, exception);
                return;
            }
        }

        lock (workflow.Sync)
        {
            workflow.Status = "completed";
            store.Persist(workflow);
        }
    }

    private async Task<string> ExecuteStep(Workflow workflow, Step step, CancellationToken cancellationToken)
    {
        if (workflow.Mode == "demo")
        {
            await Task.Delay(900, cancellationToken);
            return "Simulasi selesai. Tidak ada data yang dibuat atau pesan yang dikirim ke layanan.";
        }

        if (step.Agent is "drive" or "calendar" or "docs" or "gmail")
        {
            return await ExecuteGoogleStep(workflow, step, cancellationToken);
        }

        return await SendDiscordMessage(workflow, step, cancellationToken);
    }

    private async Task<string> ExecuteGoogleStep(Workflow workflow, Step step, CancellationToken cancellationToken)
    {
        if (database?.Enabled != true && access.Blocks([step], workflow.Owner))
        {
            throw new ActionFailure("Integrasi Google memerlukan Orbit Pro.");
        }

        var output = await GoogleActions.Execute(
            step, workflow.Owner, google, clients.CreateClient("external"), cancellationToken);
        lock (workflow.Sync)
        {
            step.ResultUrl = output.Url;
            step.MeetUrl = output.MeetUrl;
        }

        // An event must exist before its reminder can be persisted.
        if (step.Agent == "calendar")
        {
            database?.ScheduleReminder(workflow, step);
        }

        return output.Message;
    }

    private async Task<string> SendDiscordMessage(Workflow workflow, Step step, CancellationToken cancellationToken)
    {
        var webhookUrl = database?.Enabled == true
            ? database.ReadConnection<string>(workflow.Owner!, "discord")
            : config["Discord:WebhookUrl"];
        if (step.Agent != "discord" || !Webhook.IsValid(webhookUrl))
        {
            throw new InvalidOperationException();
        }

        var content = GoogleActions.DiscordContent(step, workflow.Steps);
        if (content.Length > 2000)
        {
            throw new ActionFailure("Pesan beserta tautan melebihi batas Discord. Hasil Google tetap tersedia pada langkah sebelumnya.");
        }

        // Wait for Discord's confirmation; disable mentions in generated content.
        var endpoint = webhookUrl!.Split('?')[0] + "?wait=true";
        using var response = await clients.CreateClient("external").PostAsJsonAsync(endpoint, new
        {
            content,
            allowed_mentions = new { parse = Array.Empty<string>() }
        }, cancellationToken);
        response.EnsureSuccessStatusCode();
        return "Discord mengonfirmasi pesan berhasil dibuat.";
    }

    private static bool IsStepFailure(Exception exception) =>
        exception is HttpRequestException or InvalidOperationException or TaskCanceledException
            or ActionFailure or JsonException or KeyNotFoundException;

    private void FailWorkflow(Workflow workflow, Step failedStep, Exception exception)
    {
        lock (workflow.Sync)
        {
            failedStep.Status = "failed";
            if (exception is ActionFailure actionFailure)
            {
                failedStep.ResultUrl = actionFailure.ResultUrl;
            }

            // An uncertain external result must never be retried automatically.
            failedStep.Result = exception is ActionFailure
                ? exception.Message
                : "Tidak dapat mengonfirmasi hasil. Periksa layanan sebelum mencoba ulang agar tidak membuat pesan ganda.";
            foreach (var remaining in workflow.Steps.Where(step => step.Status == "pending"))
            {
                remaining.Status = "skipped";
            }

            workflow.Status = "failed";
            store.Persist(workflow);
        }
    }
}
