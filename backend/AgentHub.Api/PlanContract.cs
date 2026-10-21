using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

static class PlanContract
{
    public static string Instructions => "Susun rencana bahasa Indonesia sesuai instruksi pengguna. Agent tambahan: docs (action create_document untuk My Drive, create_document_in_folder bila folder " +
        "tujuan disebut; pengguna memilih folder setelah rencana dibuat). name adalah judul dokumen, detail adalah SELURUH isi teks dokumen, bukan deskripsi tindakan. " +
        "gmail (action create_draft default, send_email HANYA jika pengguna eksplisit meminta kirim email). name adalah subjek email, detail SELURUH isi email, " +
        "participants email atau nama kontak/grup penerima persis dari pengguna. Jangan mengarang penerima. Gmail tidak membaca inbox atau melampirkan file. Semua isi " +
        "ditinjau sebelum persetujuan. Selain itu drive (buat folder, gunakan folder lama, atau daftar file di folder), calendar (buat event kalender utama), discord " +
        "(kirim pesan). Untuk permintaan Google Meet, gunakan agent calendar dengan createMeet=true agar event disertai tautan Meet. Jangan membuat langkah Meet " +
        "terpisah. createMeet=false jika tidak diminta. Maksimal 5 langkah. Jangan menambahkan tindakan yang tidak diminta. Susun pembuatan folder/event sebelum Discord. " +
        "Detail Discord adalah pesan lengkap; jangan mengarang tautan. Jika perlu tautan hasil sebelumnya, isi includeLinks=true; aplikasi akan menambahkan tautan asli. " +
        "Untuk drive/calendar, name adalah nama folder atau judul event yang diminta. start dan end wajib ISO 8601 dengan offset zona waktu untuk calendar, misalnya " +
        "2026-10-08T15:00:00+07:00. Jangan mengarang tanggal, jam, durasi, atau nama yang belum diberikan: isi string kosong agar aplikasi meminta kelengkapan. Field " +
        "yang tidak berlaku isi string kosong dan includeLinks=false. Jika tidak ada pertanyaan, clarification harus string kosong. participants harus array kosong jika " +
        "tidak ada peserta atau penerima email. reminderMinutes harus null jika reminder tidak diminta. Peserta meeting didukung: participants berisi email atau nama " +
        "kontak/grup persis yang disebut pengguna, jangan mengarang email. Reminder Discord terjadwal: isi reminderMinutes pada calendar (default 30 jika diminta tanpa " +
        "waktu), null jika tidak diminta. Jangan buat langkah discord untuk reminder terjadwal; agent discord hanya untuk pesan yang diminta dikirim sekarang. Judul " +
        "meeting boleh Meeting jika pengguna tidak menyebut judul. Untuk permintaan yang belum lengkap/tidak didukung (berbagi izin folder atau membaca inbox), isi " +
        "clarification berupa pertanyaan atau alasan spesifik dan steps kosong. Tidak perlu izin berbagi hanya untuk mengirim tautan. Waktu UTC saat ini: " + DateTimeOffset.UtcNow.ToString("O") + DriveInstructions;
    public const string DriveInstructions = " Untuk drive, field action WAJIB salah satu create_folder (hanya bila pengguna secara eksplisit meminta membuat folder BARU), use_existing_folder (gunakan/kirim " +
        "link folder yang sudah ada), list_folder_files (lihat/daftar isi folder). Permintaan kirim isi Drive atau folder tidak boleh menjadi create_folder. Jangan " +
        "mengarang isi file: daftar file ditambahkan backend dari Google. Folder lama dipilih pengguna setelah rencana dibuat; jangan mengarang ID folder. action untuk " +
        "calendar/discord adalah string kosong. Jangan menyalin daftar file ke detail Discord. Jika diminta mengirim isi folder, gunakan list_folder_files lalu discord " +
        "includeLinks=true. Membaca konten dokumen/gambar belum didukung; daftar isi hanya nama file. ";
    public static object Schema => new
    {
        type = "object",
        properties = new
        {
            clarification = new
            {
                type = "string"
            },
            steps = new
            {
                type = "array",
                items = new
                {
                    type = "object",
                    properties = new
                    {
                        agent = new
                        {
                            type = "string",
                            @enum = new[] { "drive", "calendar", "discord", "gmail", "docs" }
                        },
                        title = new
                        {
                            type = "string"
                        },
                        detail = new
                        {
                            type = "string"
                        },
                        name = new
                        {
                            type = "string"
                        },
                        action = new
                        {
                            type = "string",
                            @enum = new[] { "", "create_folder", "use_existing_folder", "list_folder_files", "create_document", "create_document_in_folder", "create_draft", "send_email" }
                        },
                        participants = new
                        {
                            type = "array",
                            items = new
                            {
                                type = "string"
                            }
                        },
                        reminderMinutes = new
                        {
                            type = new[] { "integer", "null" }
                        },
                        start = new
                        {
                            type = "string"
                        },
                        end = new
                        {
                            type = "string"
                        },
                        includeLinks = new
                        {
                            type = "boolean"
                        },
                        createMeet = new
                        {
                            type = "boolean"
                        }
                    },
                    required = new[] { "agent", "title", "detail", "name", "action", "start", "end", "includeLinks", "createMeet", "participants", "reminderMinutes" },
                    additionalProperties = false
                }
            }
        },
        required = new[] { "steps", "clarification" },
        additionalProperties = false
    };
    public static List<Step> Parse(string json)
    {
        using var doc = JsonDocument.Parse(json);
        if (doc.RootElement.TryGetProperty("clarification", out var clarification) && !string.IsNullOrWhiteSpace(clarification.GetString()))
            throw new PlannerException(clarification.GetString()!);
        return doc.RootElement.GetProperty("steps").EnumerateArray().Select(s => new Step(s.GetProperty("agent").GetString()!, s.GetProperty("title").GetString()!, s.GetProperty("detail").GetString()!)
        {
            Participants = s.TryGetProperty("participants", out var people) ? people.EnumerateArray().Select(p => p.GetString() ?? "").ToArray() : [],
            ReminderMinutes = s.TryGetProperty("reminderMinutes", out var reminder) && reminder.ValueKind == JsonValueKind.Number ? reminder.GetInt32() : null,
            Action = s.TryGetProperty("action", out var action) ? action.GetString() ?? "" : "",
            Name = s.GetProperty("name").GetString() ?? "",
            Start = s.GetProperty("start").GetString() ?? "",
            End = s.GetProperty("end").GetString() ?? "",
            IncludeLinks = s.GetProperty("includeLinks").GetBoolean(),
            CreateMeet = s.GetProperty("createMeet").GetBoolean()
        }).ToList();
    }
    public static string? Validate(List<Step> steps)
    {
        bool hasResult = false;
        foreach (var s in steps)
        {
            if (s.Participants.Length > 200 || s.Participants.Any(p => string.IsNullOrWhiteSpace(p) || p.Length > 254))
                return "Daftar peserta terlalu panjang atau belum lengkap.";
            if (s.ReminderMinutes is < 0 or > 10080 || (s.Agent != "calendar" && s.ReminderMinutes is not null) || (s.Agent is not ("calendar" or "gmail") && s.Participants.Length > 0))
                return "Peserta dan reminder harus terkait dengan langkah meeting.";
            if (s.Agent == "drive" && s.Action is not ("create_folder" or "use_existing_folder" or "list_folder_files"))
                return "Jenis tindakan Drive tidak valid. Susun rencana baru.";
            if (s.CreateMeet && s.Agent != "calendar")
                return "Google Meet harus dibuat bersama acara Calendar.";
            if (s.Agent == "gmail" && (s.Action is not ("create_draft" or "send_email") || s.Participants.Length == 0 || s.Name.Contains('\r') || s.Name.Contains('\n')))
                return "Lengkapi penerima, subjek, dan tindakan email (draft atau kirim).";
            if (s.Agent == "docs" && s.Action is not ("create_document" or "create_document_in_folder"))
                return "Pilih tindakan membuat dokumen yang valid.";
            if (s.Agent is "gmail" or "docs" && (string.IsNullOrWhiteSpace(s.Detail) || s.Detail.Length > 20000))
                return "Isi email atau dokumen harus 1–20.000 karakter.";
            if (s.Agent is "drive" or "calendar" or "docs" or "gmail")
            {
                if (string.IsNullOrWhiteSpace(s.Name) || s.Name.Length > 200)
                    return "Sebutkan nama folder atau judul meeting dengan jelas (maksimal 200 karakter).";
                hasResult = true;
            }
            if (s.Agent == "calendar" && (!Timestamp(s.Start, out var start) || !Timestamp(s.End, out var end) || end <= start))
                return "Lengkapi tanggal, jam mulai, jam selesai atau durasi, serta zona waktu meeting (misalnya WIB).";
            if (s.Agent == "discord" && s.Detail.Length > 1500)
                return "Pesan Discord terlalu panjang. Gunakan maksimal 1.500 karakter agar tautan hasil dapat ditambahkan.";
            if (s.IncludeLinks && (s.Agent != "discord" || !hasResult))
                return "Tautan hasil harus berasal dari langkah folder atau kalender sebelum pesan Discord.";
        }
        return null;
    }
    private static bool Timestamp(string value, out DateTimeOffset result)
    {
        result = default;
        return Regex.IsMatch(value, @"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(Z|[+-]\d{2}:\d{2})$") && DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out result);
    }
}
