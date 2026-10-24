# Orbit — AI Agent Hub

Orbit membantu mengatur project dan menjalankan pekerjaan lintas aplikasi dari instruksi sehari-hari. Pengguna meninjau rencana, penerima, waktu, dan isi pesan sebelum menyetujui eksekusi.

Contoh: “Buat meeting besok jam 7–9 pagi dengan Grup A, tambahkan Google Meet, lalu ingatkan di Discord.”

Aplikasi masih dalam pengembangan. Frontend production: [orbitworkspaces.vercel.app](https://orbitworkspaces.vercel.app/). Fitur yang tersedia di deployment mengikuti versi terakhir yang berhasil di-deploy.

## Fitur

- **Dashboard dan project:** project, checklist, tenggat, draft instruksi, serta percakapan terakhir. Project dan percakapan dapat dihapus.
- **Rencana AI:** Gemini atau OpenAI menyusun langkah terstruktur. Pengguna memeriksa rencana sebelum menjalankannya; informasi penerima yang ambigu perlu dilengkapi.
- **Google Drive:** membuat folder, menggunakan folder yang sudah ada, dan melihat isinya sesuai izin akun. Memilih folder lama tidak otomatis membuat folder pengganti.
- **Google Calendar dan Meet:** membuat event, tautan Meet, serta undangan peserta dari email, kontak, atau grup.
- **Gmail:** membuat draft atau mengirim email setelah persetujuan. Penerima, subjek, dan isi ditampilkan dalam rencana.
- **Google Docs:** membuat dokumen berisi teks, termasuk di folder yang dipilih dan dapat diakses Orbit.
- **Discord:** mengirim pesan dan tautan hasil pekerjaan, serta reminder meeting terjadwal. Default reminder 30 menit sebelum meeting.
- **Kontak & Grup:** kontak manual dan grup pribadi per akun untuk memudahkan pemilihan peserta.
- **Pengaturan:** Indonesia/English, tema terang/gelap/ikuti perangkat, zona waktu mengikuti perangkat atau pilihan UTC, serta ekspor data lokal.

Mode **Uji tanpa mengirim** tidak menjalankan aksi nyata pada Google atau Discord. Jika provider AI dikonfigurasi, penyusunan rencana tetap dapat menggunakan API berbayar. Tanpa API key, rencana demo memakai template tetap, bukan interpretasi instruksi.

## Teknologi dan penyimpanan

| Bagian | Teknologi |
| --- | --- |
| Frontend | React, TypeScript, Vite |
| Backend | ASP.NET Core / .NET 8 |
| Database | PostgreSQL melalui Npgsql; deployment menggunakan Supabase |
| Login dan integrasi Google | Google OAuth |
| Hosting | Vercel untuk frontend, Railway untuk backend |

Pada mode database, data akun, project, workflow, kontak, grup, pengaturan, dan reminder disimpan berdasarkan pemilik akun. Token Google dan webhook Discord disimpan terenkripsi; refresh token digunakan untuk memperbarui akses Google.

Backend saat ini mendukung **satu replica per database**. Worker memakai lock database agar dua instance tidak menjalankan pekerjaan yang sama. Jangan menjalankan backend lokal terhadap database production yang sedang digunakan Railway.

Reminder disimpan setelah event berhasil dibuat. Worker memeriksa jadwal setiap 30 detik; reminder terlambat masih dikirim sebelum meeting dimulai, lalu ditandai terlewat setelah waktunya. Hasil pengiriman yang tidak pasti perlu diperiksa dan tidak dikirim ulang otomatis.

Menghapus project atau percakapan tidak menghapus file Google, event, atau pesan yang sudah dikirim. Penghapusan percakapan juga menghapus reminder terkait; pekerjaan yang sedang antre/berjalan dan reminder yang sedang dikirim tidak dapat dihapus.

## Jalankan lokal

Prasyarat: **Node.js 22+**, **.NET SDK 8**, dan PowerShell. Jalankan perintah berikut dari root repository, yaitu folder yang berisi `frontend`, `backend`, dan `scripts`.

Terminal pertama — backend demo:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\start-backend-local.ps1
```

Terminal kedua — frontend:

```powershell
npm ci --prefix frontend
npm run dev --prefix frontend -- --port 5173 --strictPort
```

Buka [http://127.0.0.1:5173](http://127.0.0.1:5173). Frontend meneruskan `/api` ke backend pada port `5080`. Biarkan kedua terminal berjalan; gunakan `Ctrl+C` untuk menghentikannya sebelum menjalankan ulang.

Launcher lokal menonaktifkan koneksi database production dan, secara default, panggilan AI/Google/Discord. Mode ini dapat digunakan saat hosting tidak tersedia.

### Data pada mode lokal

- Project dan draft menggunakan penyimpanan browser.
- Riwayat workflow disimpan di `.local/workflows` dan tetap tersedia setelah restart. Menghapus percakapan juga menghapus file lokalnya.
- Kontak, pengaturan akun, dan reminder memerlukan mode database untuk penyimpanan yang tahan restart. Cache pengaturan browser tidak menggantikan database.
- Data lokal tidak otomatis tersinkronisasi dengan akun production. Ini lingkungan terpisah, bukan perpindahan otomatis saat Railway bermasalah.

### Menguji integrasi nyata

Siapkan credentials Google OAuth, provider AI, dan webhook Discord di konfigurasi backend lokal. Setelah itu jalankan:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\start-backend-local.ps1 -LiveIntegrations
```

Opsi ini memakai credentials lokal tetapi tetap menonaktifkan database production. Google OAuth lokal perlu mengizinkan callback `http://127.0.0.1:5080/api/google/callback`. Periksa akses Pro/trial untuk aksi Google, hubungkan akun, lalu tinjau rencana sebelum mengirim.

Simpan secrets di environment backend atau konfigurasi lokal yang diabaikan Git. Jangan commit `appsettings.Local.json`, API key, client secret, password database, atau webhook. Jangan memasukkan secrets ke kode frontend maupun variable `VITE_*`.

## Deployment

- Railway menjalankan backend dari `backend/AgentHub.Api`, dengan satu replica.
- Vercel membangun frontend dari `frontend`; `/api` diteruskan ke backend melalui konfigurasi rewrite.
- Domain frontend, `Deployment__FrontendUrl`, dan callback yang terdaftar pada Google OAuth harus konsisten.
- Perubahan kontrak API perlu deployment frontend dan backend yang sesuai. Build lokal yang berhasil belum membuktikan konfigurasi production benar.

## Pemeriksaan sebelum commit

Dari root repository:

```powershell
npm run format:check --prefix frontend
npm run typecheck --prefix frontend
npm run build --prefix frontend
dotnet build tests/PlannerChecks -p:UseSharedCompilation=false -p:UseAppHost=false
dotnet tests/PlannerChecks/bin/Debug/net8.0/PlannerChecks.dll
```

`PlannerChecks` menguji planner, integrasi, kepemilikan data, reminder, serta persistensi/penghapusan workflow lokal. Panggilan layanan eksternal memakai HTTP tiruan dan tidak mengirim email nyata. Tes PostgreSQL hanya berjalan jika database pengujian terpisah dikonfigurasi; jika tidak, tes tersebut dilewati. Jangan arahkan tes ke database production.

## Batasan saat ini

- Belum ada kolaborasi anggota atau berbagi kontak antar akun.
- Gmail belum membaca inbox, membalas thread, atau melampirkan file.
- Docs membuat dokumen baru dengan teks biasa; belum mengedit dokumen lama atau mengatur format kompleks.
- Akses folder Drive mengikuti izin OAuth. Folder yang terlihat belum tentu dapat ditulisi.
- Hasil aksi eksternal yang tidak pasti tidak diulang otomatis. Periksa layanan tujuan sebelum membuat pekerjaan pengganti.
- Pembayaran sungguhan belum aktif. Mode database menyediakan Free dan trial Pro; aktivasi simulasi/pengembangan bukan pembayaran.
