# Orbit — AI Agent Hub

**Koneksi akun Google:** [panduan OAuth lokal](docs/GOOGLE.md). Login, pembuatan folder/event, dan penerusan tautan ke Discord tersedia.

**Gemini / Google AI Studio sudah didukung.** Lihat [panduan Gemini](docs/GEMINI.md) untuk konfigurasi provider dan key.

Workspace untuk mengatur proyek dan menjalankan pekerjaan lintas aplikasi. Sedang dalam pengembangan.

Panduan akun, credentials, dan pekerjaan integrasi berikutnya: [docs/SETUP.md](docs/SETUP.md).

## Status implementasi

- React + TypeScript: workspace, proyek, checklist, tenggat, draft otomatis, pengaturan nama, ekspor data lokal, input instruksi, review rencana, progres, koneksi, pencarian dan filter aktivitas.
- Proyek, checklist dan draft disimpan dalam localStorage browser. Belum ada sinkronisasi anggota. Workflow belum berelasi dengan ID proyek pada backend.
- ASP.NET Core: API planner, antrean eksekusi, status workflow, dan penolakan eksekusi ganda.
- Demo berjalan tanpa API key. **Rencana demo tanpa AI selalu menggunakan template tetap**, bukan hasil interpretasi instruksi.
- OpenAI Responses API: planner terstruktur, aktif setelah API key dan model dikonfigurasi. Belum diuji dengan akun nyata.
- Discord: pengiriman nyata melalui webhook, hanya setelah rencana disetujui. Belum diuji dengan akun nyata.
- Google Drive dan Calendar: OAuth, folder My Drive dan event kalender utama. Token masih dalam memori; hubungkan ulang setelah restart atau kedaluwarsa.
- Riwayat tersimpan dalam memori; restart backend menghapusnya. PostgreSQL, login, dan isolasi data pengguna masih tahap berikutnya.

Saat ini aplikasi berjalan lokal. Jangan expose backend ke internet sebelum autentikasi, otorisasi, pembatasan request, dan penyimpanan aman selesai.

## Jalankan lokal

Prasyarat: Node.js 22+ dan .NET SDK 8. SDK yang tersedia saat bootstrap adalah 8.0.425. Target awal .NET 10 ditunda sampai SDK tersebut tersedia.

Terminal backend:

```powershell
cd backend/AgentHub.Api
dotnet run --urls http://127.0.0.1:5080
```

Terminal frontend:

```powershell
cd frontend
npm ci
npm run dev
```

Buka http://127.0.0.1:5173. Frontend mem-proxy `/api` ke backend port 5080.

## Konfigurasi integrasi opsional

Set environment variables di terminal backend sebelum menjalankan server:

```powershell
$env:OpenAI__ApiKey='<API key pribadi>'
$env:OpenAI__Model='<model yang mendukung Responses dan Structured Outputs>'
$env:Discord__WebhookUrl='https://discord.com/api/webhooks/<id>/<token>'
dotnet run --urls http://127.0.0.1:5080
```

Jangan commit nilai rahasia. `appsettings.Development.json` juga diabaikan Git. Jangan menempelkan key ke frontend. Mode demo tetap tidak menghubungi Discord, tetapi jika key OpenAI dikonfigurasi, pembuatan rencana menggunakan API berbayar.

Untuk pengiriman nyata, coba instruksi: `Kirim pesan ke Discord: Halo kelompok, ini uji integrasi Orbit.` Matikan **Uji tanpa mengirim**, periksa detail pesan pada rencana, lalu pilih **Setujui & jalankan**. Untuk folder dan acara, hubungkan akun Google sesuai panduan OAuth.

Eksekusi hanya sekali per workflow. Jika request eksternal timeout, UI menyatakan hasil belum bisa dikonfirmasi dan tidak melakukan retry otomatis. Periksa layanan terkait sebelum membuat pekerjaan baru.

## Verifikasi

```powershell
dotnet build backend/AgentHub.Api
cd frontend
npm run build
```

Dengan backend berjalan tanpa credentials, dari root repo:

```powershell
powershell -ExecutionPolicy Bypass -File scripts/smoke-test.ps1
```

## Pembagian kerja empat orang

1. Frontend: dashboard, edit rencana, aksesibilitas dan pengalaman pengguna.
2. Backend inti: PostgreSQL/EF Core, login, kepemilikan workflow, persistensi antrean.
3. Google: OAuth, penyimpanan token terenkripsi, Drive dan Calendar.
4. AI/integrasi: planner, parameter tool bertipe, klarifikasi informasi yang kurang, Discord dan pengujian.

## Tahap berikutnya

1. Penyimpanan token Google terenkripsi dan pembaruan access token otomatis.
2. UI untuk memperbaiki parameter tindakan sebelum eksekusi. Parameter nama folder, waktu dengan zona waktu, dan isi pesan sudah ditampilkan untuk ditinjau.
3. Persistensi hasil tindakan dan riwayat. Tautan hasil sudah diteruskan ke Discord dalam satu workflow.
4. PostgreSQL, autentikasi dan pemisahan data pengguna sebelum deployment publik.
5. Uji akun nyata, kegagalan API, token kedaluwarsa, waktu ambigu, dan pemulihan tanpa duplikasi.

Nama workspace dapat diubah dalam Pengaturan. Sistem membership belum tersedia.

Tes planner dan integrasi memakai HTTP tiruan (tidak menulis ke layanan): `dotnet run --project tests/PlannerChecks`.

## Paket dan Google Meet

Discord tersedia pada Free. Drive, Calendar, dan pembuatan tautan Meet termasuk Pro. Popup paket dan ringkasan upgrade tersedia; pembayaran belum aktif. Untuk pengujian Google nyata, aktifkan akses Pro khusus Development sesuai [panduan monetisasi](docs/MONETIZATION.md).
