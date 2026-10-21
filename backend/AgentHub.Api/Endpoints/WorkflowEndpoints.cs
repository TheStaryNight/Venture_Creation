using System.Security.Claims;
using System.Text.Json;

static class WorkflowEndpoints
{
    public static void MapWorkflowEndpoints(this WebApplication app)
    {
        app.MapGet("/api/workflows", (WorkflowStore store, HttpContext context) => store.Items.Values.Where(w => w.Owner == context.User.FindFirstValue("orbit_google_connection")).OrderByDescending(w => w.CreatedAt).Select(w => w.Snapshot()));
        app.MapGet("/api/workflows/{id:guid}", (Guid id, WorkflowStore store, HttpContext context) =>
            store.Items.TryGetValue(id, out var w) && w.Owner == context.User.FindFirstValue("orbit_google_connection") ? Results.Ok(w.Snapshot()) : Results.NotFound());


        app.MapPost("/api/workflows/plan", Plan);
        app.MapPost("/api/workflows/{id:guid}/delete", (Guid id, WorkflowStore store, HttpContext context) =>
        {
            try
            {
                return store.Delete(id, context.User.FindFirstValue("orbit_google_connection"))
                    ? Results.Ok(new { deleted = true }) : Results.NotFound();
            }
            catch (ActionFailure error) { return Results.Conflict(new { error = error.Message }); }
        });
        app.MapPost("/api/workflows/{id:guid}/execute", Execute);
    }
    private static async Task<IResult> Plan(PlanRequest request, WorkflowStore store, IConfiguration config, IHttpClientFactory clients, HttpContext context, GoogleTokenStore google, FeatureAccess access, CancellationToken ct, OrbitDatabase database)
    {
        if (string.IsNullOrWhiteSpace(request.Prompt) || request.Prompt.Length > 2000)
            return Results.BadRequest(new
            {
                error = "Isi instruksi antara 1–2.000 karakter."
            });
        if (request.Mode is not ("demo" or "live"))
            return Results.BadRequest(new
            {
                error = "Mode tidak valid."
            });
        var settings = AiSettings.Read(config);
        if (settings.Error is not null)
            return Results.BadRequest(new
            {
                error = settings.Error
            });
        var aiEnabled = settings.Ready;
        List<Step> steps;
        var source = "template";
        if (aiEnabled)
        {
            if (database.Enabled && !database.ReservePlan(context.User.FindFirstValue("orbit_google_connection")!))
                return Results.Json(new
                {
                    error = "Kuota 20 penyusunan AI bulan ini habis. Kuota direset awal bulan UTC.",
                    code = "quota_exceeded"
                }, statusCode: 429);
            try
            {
                var prefs = database.ReadAccount(AccountFeatures.Owner(context)).Preferences;
                steps = await Planner.Create(request.Prompt, config, clients.CreateClient("external"), ct, AccountFeatures.PlannerContext(prefs));
                source = "ai";
            }
            catch (PlannerException ex)
            {
                return Results.Problem(statusCode: 502, title: ex.Message);
            }
            catch (TaskCanceledException) when (!ct.IsCancellationRequested)
            {
                return Results.Problem(statusCode: 504, title: "Layanan AI tidak merespons dalam 45 detik. Coba lagi atau pilih model lain yang tersedia di akun.");
            }
            catch (HttpRequestException ex)
            {
                var message = ex.StatusCode is null
                    ? "Koneksi ke layanan AI gagal sebelum respons diterima. Periksa internet, DNS, proxy, atau koneksi TLS pada komputer backend."
                    : $"Layanan AI mengembalikan HTTP {(int)ex.StatusCode}. Periksa akses API dan konfigurasi provider.";
                return Results.Problem(statusCode: 502, title: message);
            }
            catch (Exception ex) when (ex is JsonException or InvalidOperationException or KeyNotFoundException or ArgumentException)
            {
                return Results.Problem(statusCode: 502, title: "Respons AI tidak berisi rencana JSON yang valid atau lengkap. Coba instruksi lebih singkat atau model lain yang mendukung Structured Outputs.");
            }
        }
        else
        {
            if (request.Mode == "live")
                return Results.BadRequest(new
                {
                    error = "Pengiriman nyata memerlukan konfigurasi Gemini atau OpenAI. Uji tanpa AI memakai template contoh."
                });
            steps = [
                new("drive", "Buat folder proyek", "Contoh: folder Proyek AI Kelompok untuk menyimpan hasil kerja.") { Action = "create_folder" },
            new("calendar", "Jadwalkan diskusi kelompok", "Contoh: jadwal belum ditetapkan. Mode demo tidak membuat event."),
            new("discord", "Bagikan rangkuman", "Contoh: kirim rangkuman persiapan proyek ke channel kelompok.")
            ];
        }
        if (steps.Count is < 1 or > 5 || steps.Any(s => s.Agent is not ("drive" or "calendar" or "discord" or "docs" or "gmail") || string.IsNullOrWhiteSpace(s.Title) || string.IsNullOrWhiteSpace(s.Detail)))
            return Results.BadRequest(new
            {
                error = "Belum ada langkah yang bisa dijalankan. Sebutkan tindakan, judul meeting, tanggal, serta jam mulai dan selesai."
            });
        if (source == "ai" && PlanContract.Validate(steps) is string validation)
            return Results.BadRequest(new
            {
                error = validation
            });
        foreach (var step in steps.Where(s => s.Agent is "calendar" or "gmail"))
            AccountFeatures.Resolve(step, database.ReadAccount(AccountFeatures.Owner(context)), request.Prompt);
        if (steps.Any(s => s.Attendees.Length > 200))
            return Results.BadRequest(new
            {
                error = "Maksimal 200 peserta per meeting."
            });
        if (request.Mode == "live")
        {
            if (steps.Any(s => s.ReminderMinutes is not null) && !database.Enabled)
                return Results.BadRequest(new
                {
                    error = "Reminder terjadwal membutuhkan database."
                });
            if (access.Blocks(steps, context.User.FindFirstValue("orbit_google_connection")))
                return FeatureAccess.UpgradeRequired();
            if (steps.Any(s => s.Agent == "discord" || s.ReminderMinutes is not null) && !Webhook.IsValid(database.Enabled ? database.ReadConnection<string>(context.User.FindFirstValue("orbit_google_connection")!, "discord") : config["Discord:WebhookUrl"]))
                return Results.BadRequest(new
                {
                    error = "Konfigurasi webhook Discord belum valid."
                });
            if (steps.Any(s => s.Agent is "drive" or "docs") && !google.HasScope(context, GoogleConnection.DriveScope))
                return Results.BadRequest(new
                {
                    error = "Hubungkan akun Google dan berikan izin Drive terlebih dahulu."
                });
            if (steps.Any(s => s.Agent == "gmail") && !google.HasScope(context, GoogleConnection.GmailScope))
                return Results.BadRequest(new
                {
                    error = "Hubungkan ulang Google dan izinkan Gmail terlebih dahulu."
                });
            if (steps.Any(s => s.Agent == "calendar") && !google.HasScope(context, GoogleConnection.CalendarScope))
                return Results.BadRequest(new
                {
                    error = "Hubungkan akun Google dan berikan izin Calendar terlebih dahulu."
                });
        }
        var workflow = new Workflow(request.Prompt.Trim(), request.Mode, source, steps) { Owner = context.User.FindFirstValue("orbit_google_connection"), Language = database.ReadAccount(AccountFeatures.Owner(context)).Preferences.Language, TimeZone = database.ReadAccount(AccountFeatures.Owner(context)).Preferences.TimeZone };
        store.Persist(workflow);
        store.Items[workflow.Id] = workflow;
        return Results.Ok(workflow.Snapshot());
    }
    private static IResult Execute(Guid id, ExecuteRequest request, WorkflowStore store, HttpContext context, FeatureAccess access, OrbitDatabase database)
    {
        if (!store.Items.TryGetValue(id, out var workflow) || workflow.Owner != context.User.FindFirstValue("orbit_google_connection"))
            return Results.NotFound();
        if (workflow.Mode == "live" && access.Blocks(workflow.Steps, workflow.Owner))
            return FeatureAccess.UpgradeRequired();
        lock (workflow.Sync)
        {
            if (workflow.Status != "planned")
                return Results.Conflict(new
                {
                    error = "Rencana ini sudah dijalankan. Lihat status pekerjaan."
                });
            if (workflow.Mode == "live" && workflow.Steps.Any(s => s.Agent == "drive" && s.Action == "create_folder") && !request.ConfirmNewFolder)
                return Results.BadRequest(new
                {
                    error = "Konfirmasi pembuatan folder baru. Untuk folder yang sudah ada, susun ulang instruksi dengan gunakan folder lama."
                });
            if (workflow.Steps.Any(s => s.Agent == "drive" && (s.Action is not ("create_folder" or "use_existing_folder" or "list_folder_files") || (s.Action != "create_folder" && string.IsNullOrEmpty(s.FolderId)))))
                return Results.BadRequest(new
                {
                    error = "Pilih folder untuk setiap langkah Drive yang memakai folder lama. Rencana lama tanpa jenis tindakan harus disusun ulang."
                });
            if (workflow.Steps.Any(s => s.Agent == "docs" && s.Action == "create_document_in_folder" && string.IsNullOrEmpty(s.FolderId)))
                return Results.BadRequest(new
                {
                    error = "Pilih folder tujuan dokumen terlebih dahulu."
                });
            if (workflow.Steps.Any(s => s.Agent == "gmail" && s.Attendees.Length == 0))
                return Results.BadRequest(new
                {
                    error = "Tentukan penerima email terlebih dahulu."
                });
            if (workflow.Steps.Any(s => s.RecipientIssues.Length > 0))
                return Results.BadRequest(new
                {
                    error = "Lengkapi atau pilih peserta meeting terlebih dahulu."
                });
            if (workflow.Steps.Any(s => s.Agent == "calendar" && DateTimeOffset.TryParse(s.Start, out var date) && date <= DateTimeOffset.UtcNow))
                return Results.BadRequest(new
                {
                    error = "Waktu meeting sudah lewat. Susun ulang dengan tanggal yang akan datang."
                });
            workflow.Status = "queued";
            try
            {
                if (!database.QueueWorkflow(workflow))
                {
                    workflow.Status = "planned";
                    return FeatureAccess.UpgradeRequired();
                }
            }
            catch { workflow.Status = "planned"; throw; }
            store.Queue.Writer.TryWrite(id);
        }
        return Results.Accepted($"/api/workflows/{id}", workflow.Snapshot());
    }

}
