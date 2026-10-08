# Mengaktifkan Gemini

Backend mendukung Gemini melalui endpoint kompatibel Chat Completions Google. Key Gemini hanya dikirim ke `generativelanguage.googleapis.com`, bukan OpenAI. Implementasi mengikuti [dokumentasi Google](https://ai.google.dev/gemini-api/docs/openai).

Hentikan backend dengan Ctrl+C. Pada terminal PowerShell backend yang sama:

```powershell
$env:AI__Provider='Gemini'
$env:Gemini__ApiKey='TEMPEL_KEY_GEMINI_BARU'
$env:Gemini__Model='gemini-3.8-flash'
dotnet run --urls http://127.0.0.1:5080
```

Model contoh mengikuti dokumentasi Google yang diperiksa saat implementasi. Jika tidak tersedia untuk project, isi ID model Gemini dengan dukungan structured output yang tersedia pada akun. `Default` adalah nama yang tidak valid untuk konfigurasi model.

Webhook Discord tetap diatur melalui `Discord__WebhookUrl` pada terminal yang sama. Gunakan URL biasa, tanpa format `[URL](URL)`.

Variabel `AI__Provider` menentukan provider secara eksplisit. Jika tidak diisi, Gemini dipilih saat `Gemini__ApiKey` tersedia, selain itu OpenAI. Variabel OpenAI lama diabaikan saat provider Gemini dipilih. Jangan masukkan key Gemini ke `OpenAI__ApiKey`.

Di Orbit, perbarui status Koneksi. Uji menyusun rencana dahulu dengan **Uji tanpa mengirim** aktif. Setelah berhasil, matikan opsi tersebut, periksa rencana baru, lalu setujui untuk mengirim pesan Discord.

Konfigurasi tersedia belum membuktikan key valid. Error akses, model, dan kuota tampil saat pembuatan rencana. Uji otomatis memakai respons tiruan; pengujian Google nyata memerlukan key baru milik pengguna. Cabut credentials yang pernah terpapar sebelum melanjutkan.

Pemeriksaan tanpa key: `dotnet run --project tests/PlannerChecks`.
