# Orbit: Supabase → Railway → Vercel

Target awal: beta dengan satu workspace pribadi per akun Google, satu backend replica. Pembayaran sungguhan dan undangan anggota belum tersedia.

## 1. Supabase

1. Buat project Orbit pada organization pilihanmu, paket Free, region dekat pengguna.
2. Terapkan `backend/AgentHub.Api/Database/001_initial.sql` lewat SQL Editor atau tool migrasi Supabase. Skema `orbit` bersifat privat; jangan tambahkan ke daftar exposed schemas pada Data API.
3. Klik **Connect → Session pooler**, port **5432**. Catat host dan username. Session pooler dapat dipakai melalui IPv4. Jangan gunakan transaction pooler 6543 karena backend ini memegang advisory lock sepanjang proses.
4. Dari root repo, jalankan `powershell -ExecutionPolicy Bypass -File scripts/configure-database.ps1`. Masukkan host, username, dan password database di terminal lokal. Script membuat key enkripsi dan menyimpan konfigurasi pada `appsettings.Local.json`, diabaikan Git dan tidak ikut publish.
5. Simpan key enkripsi yang sama untuk deployment dan backup. Mengganti key tanpa migrasi akan membuat koneksi tersimpan tidak dapat dibaca.

Alternatif migrasi melalui backend setelah koneksi tersedia:

```powershell
dotnet run --project backend/AgentHub.Api -- --migrate
```

Backend menggunakan PostgreSQL langsung melalui Npgsql; Supabase anon/service-role API key tidak diperlukan. Password database hanya masuk backend. Login memakai Google OAuth, bukan Supabase Auth pada tahap ini.

## 2. Jalankan lokal dengan database

```powershell
dotnet run --project backend/AgentHub.Api --urls http://127.0.0.1:5080
```

Masuk dengan Google, lalu buat proyek. Akun Google yang sama memakai workspace yang sama setelah login ulang. Proyek lama di localStorage tidak diimpor otomatis ke akun; unduh cadangan melalui versi lokal sebelum migrasi bila diperlukan.

Tiap akun mengisi webhook Discord miliknya lewat Koneksi → Discord. Webhook dan token Google tersimpan sebagai AES-GCM ciphertext dengan owner/service sebagai authenticated data. Google refresh token dipakai untuk memperbarui access token. Error provider tidak mengembalikan secret.

Free: 20 percobaan penyusunan AI per bulan kalender UTC (termasuk respons gagal setelah request dimulai). Trial Pro: satu kali 7 hari, maksimal 10 workflow Google yang disetujui; kegagalan layanan setelah antrean diterima tetap memakai kuota. Eksekusi uji tanpa mengirim tidak memakai kuota workflow trial. Pro berbayar belum dibuka.

## 3. Railway backend

- New project → Deploy from GitHub → pilih repo Venture_Creation.
- Root directory: `/backend/AgentHub.Api`.
- Builder Railpack, satu replica, start command `./out/AgentHub.Api`, health check `/api/health`.
- Config as code file jika diperlukan: `/backend/AgentHub.Api/railway.json`.
- Nonaktifkan sleep/serverless untuk worker. Jangan jalankan dua backend memakai database yang sama, termasuk lokal dan hosting sekaligus.
- Aktifkan domain Railway. Simpan origin HTTPS untuk langkah Vercel.

Variables backend:

| Nama | Isi |
| --- | --- |
| `ConnectionStrings__Orbit` | Connection string Npgsql Session Pooler dari konfigurasi lokal |
| `Security__EncryptionKey` | Key enkripsi yang sama dengan konfigurasi lokal |
| `Deployment__Public` | `true` |
| `Deployment__FrontendUrl` | Origin HTTPS frontend Vercel yang stabil |
| `Google__ClientId` / `Google__ClientSecret` | OAuth web client milik tim |
| `AI__Provider` | `Gemini` |
| `Gemini__ApiKey` / `Gemini__Model` | Key dan ID model yang tersedia di project |

Jangan memasukkan secret ke variable frontend `VITE_*`. Jangan mengaktifkan mode public tanpa database dan Google OAuth; aplikasi akan menolak startup.

Worker memegang advisory lock untuk mencegah eksekusi oleh dua instance. Deployment pengganti mungkin perlu menunggu instance lama berhenti; gunakan penggantian dengan downtime singkat. Pekerjaan yang sempat `running` saat proses mati ditandai gagal dan harus diperiksa pengguna, tidak diulang otomatis. Pekerjaan `queued` dipulihkan. Saat koneksi lock hilang, worker berhenti sebelum memulai langkah berikutnya.

Cookie login dapat hilang setelah redeploy jika data-protection key ring tidak dipersistenkan; login ulang tetap memuat akun dan data yang sama. Untuk sesi lintas redeploy, pasang volume persisten pada direktori `.local/keys` di content root backend.

## 4. Vercel frontend

- Import repo, root directory `frontend`, framework Vite, build `npm run build`, output `dist`.
- Jalankan `powershell -ExecutionPolicy Bypass -File scripts/configure-deploy.ps1 -BackendUrl https://DOMAIN_RAILWAY` untuk menghasilkan `frontend/vercel.json` yang merutekan `/api` ke Railway. Commit file ini sebelum deploy.
- Ambil domain production Vercel yang stabil dan pasang sebagai `Deployment__FrontendUrl` di Railway.
- Backend menggunakan origin frontend sebagai origin OAuth kanonis. Browser selalu memakai `/api` pada domain frontend, termasuk callback, sehingga cookie tetap pada satu origin. Tidak perlu CORS lintas domain.
- Jangan memakai cache untuk `/api`. Preview Vercel dengan domain berbeda tidak digunakan untuk OAuth production.

## 5. Google Cloud

Tambahkan Authorized redirect URI persis `https://DOMAIN_FRONTEND/api/google/callback`. Gunakan frontend Vercel, bukan domain Railway. Aktifkan Drive API dan Calendar API. Masukkan akun beta sebagai test users selama consent screen masih Testing. Publikasi OAuth/verification adalah proses terpisah dari deployment website.

## 6. Verifikasi sebelum membuka beta

- `/api/session` mengembalikan `database: true`; endpoint workspace/workflows menolak pengguna anonim.
- Login dua akun: proyek, workflow, webhook, dan trial tidak saling terlihat.
- Simpan proyek → refresh → logout/login → datanya tetap ada; perubahan tab yang sudah usang ditolak dengan 409.
- Jalankan satu workflow Discord dan satu Calendar/Meet, hanya setelah review dan persetujuan pemilik akun.
- Restart backend: riwayat tersimpan; tindakan yang belum pasti tidak terkirim ulang.
- Trial hanya bisa dimulai satu kali; kuota tidak melewati batas saat request paralel.
- Endpoint pembayaran simulasi ditolak pada mode database/public.
- Aktifkan backup database sesuai paket dan uji restore; simpan key enkripsi terpisah dari backup database.

Referensi: [Supabase connections](https://supabase.com/docs/guides/database/connecting-to-postgres), [Railpack .NET](https://railpack.com/languages/dotnet), [Vercel rewrites](https://vercel.com/docs/routing/rewrites).
