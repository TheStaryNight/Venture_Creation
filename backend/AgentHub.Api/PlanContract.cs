using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

static class PlanContract {
    public static string Instructions => "Susun rencana bahasa Indonesia sesuai instruksi pengguna. Hanya drive (buat folder di My Drive), calendar (buat event kalender utama), discord (kirim pesan). Untuk permintaan Google Meet, gunakan agent calendar dengan createMeet=true agar event disertai tautan Meet. Jangan membuat langkah Meet terpisah. createMeet=false jika tidak diminta. Maksimal 5 langkah. Jangan menambahkan tindakan yang tidak diminta. Susun pembuatan folder/event sebelum Discord. Detail Discord adalah pesan lengkap; jangan mengarang tautan. Jika perlu tautan hasil sebelumnya, isi includeLinks=true; aplikasi akan menambahkan tautan asli. name adalah nama folder atau judul event yang diminta. start dan end wajib ISO 8601 dengan offset zona waktu untuk calendar, misalnya 2026-10-08T15:00:00+07:00. Jangan mengarang tanggal, jam, durasi, atau nama yang belum diberikan: isi string kosong agar aplikasi meminta kelengkapan. Field yang tidak berlaku isi string kosong dan includeLinks=false. Tidak mendukung berbagi izin folder, mengundang peserta, atau membuat file dokumen; jika itu diperlukan kembalikan steps kosong. Tidak perlu izin berbagi hanya untuk mengirim tautan. Waktu UTC saat ini: " + DateTimeOffset.UtcNow.ToString("O");
    public static object Schema => new {
        type = "object", properties = new { steps = new { type = "array", items = new {
            type = "object", properties = new {
                agent = new { type = "string", @enum = new[] { "drive", "calendar", "discord" } },
                title = new { type = "string" }, detail = new { type = "string" }, name = new { type = "string" },
                start = new { type = "string" }, end = new { type = "string" }, includeLinks = new { type = "boolean" }, createMeet = new { type = "boolean" }
            }, required = new[] { "agent", "title", "detail", "name", "start", "end", "includeLinks", "createMeet" }, additionalProperties = false
        } } }, required = new[] { "steps" }, additionalProperties = false
    };
    public static List<Step> Parse(string json) {
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.GetProperty("steps").EnumerateArray().Select(s => new Step(s.GetProperty("agent").GetString()!, s.GetProperty("title").GetString()!, s.GetProperty("detail").GetString()!) {
            Name = s.GetProperty("name").GetString() ?? "", Start = s.GetProperty("start").GetString() ?? "", End = s.GetProperty("end").GetString() ?? "", IncludeLinks = s.GetProperty("includeLinks").GetBoolean(), CreateMeet = s.GetProperty("createMeet").GetBoolean()
        }).ToList();
    }
    public static string? Validate(List<Step> steps) {
        bool hasResult = false;
        foreach (var s in steps) {
            if (s.CreateMeet && s.Agent != "calendar") return "Google Meet harus dibuat bersama acara Calendar.";
            if (s.Agent is "drive" or "calendar") {
                if (string.IsNullOrWhiteSpace(s.Name) || s.Name.Length > 200) return "Sebutkan nama folder atau judul meeting dengan jelas (maksimal 200 karakter).";
                hasResult = true;
            }
            if (s.Agent == "calendar" && (!Timestamp(s.Start, out var start) || !Timestamp(s.End, out var end) || end <= start))
                return "Lengkapi tanggal, jam mulai, jam selesai atau durasi, serta zona waktu meeting (misalnya WIB).";
            if (s.Agent == "discord" && s.Detail.Length > 1500) return "Pesan Discord terlalu panjang. Gunakan maksimal 1.500 karakter agar tautan hasil dapat ditambahkan.";
            if (s.IncludeLinks && (s.Agent != "discord" || !hasResult)) return "Tautan hasil harus berasal dari langkah folder atau kalender sebelum pesan Discord.";
        }
        return null;
    }
    private static bool Timestamp(string value, out DateTimeOffset result) {
        result = default;
        return Regex.IsMatch(value, @"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(Z|[+-]\d{2}:\d{2})$") && DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out result);
    }
}
