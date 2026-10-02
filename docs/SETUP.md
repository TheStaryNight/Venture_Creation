# Kebutuhan untuk mengaktifkan Orbit

## Yang sudah bisa dipakai

- Buat proyek dengan deskripsi dan tenggat.
- Tambah tugas dan tandai selesai; progres proyek mengikuti checklist.
- Simpan draft instruksi otomatis per proyek.
- Cari proyek, cari aktivitas, dan filter status.
- Ganti nama workspace dan unduh data lokal sebagai JSON.
- Susun dan periksa rencana, jalankan uji tanpa mengirim, serta lihat hasil per langkah.

Proyek, checklist, nama workspace, dan draft disimpan di browser ini. Belum ada akun pengguna atau sinkronisasi anggota. Riwayat eksekusi masih tersimpan dalam memori server. UI tidak menghitung uji coba sebagai pekerjaan nyata yang selesai.

## 1. OpenAI — untuk memahami instruksi

Siapkan:

- Akun OpenAI API dan billing/kuota untuk pemakaian API.
- API key milik project tim.
- Nama model yang tersedia di akun dan mendukung Responses API serta Structured Outputs.
- Batas anggaran pengujian yang disepakati tim.

Variabel backend yang sudah didukung:

```text
OpenAI__ApiKey
OpenAI__Model
```

Simpan dalam environment proses backend atau `backend/AgentHub.Api/appsettings.Development.json` yang diabaikan Git. File Development hanya dibaca saat environment ASP.NET Core adalah Development. Jangan menaruh key dalam variabel VITE_ atau chat/instruksi aplikasi.

Tanpa konfigurasi ini, uji menggunakan rencana contoh tetap. Dengan konfigurasi ini, penyusunan rencana memakai API meskipun pengiriman ke aplikasi dinonaktifkan.

Referensi: [OpenAI API quickstart](https://developers.openai.com/api/docs/quickstart).

## 2. Discord — integrasi nyata pertama

Siapkan:

- Server kelompok dan satu channel khusus pengujian, misalnya `orbit-dev`.
- Anggota tim yang memiliki izin **Manage Webhooks** pada channel.
- Webhook untuk channel tersebut.

Variabel backend yang sudah didukung:

```text
Discord__WebhookUrl
```

URL webhook adalah rahasia karena dapat dipakai untuk mengirim pesan. Simpan hanya di backend. Setelah diatur, restart backend, buka Koneksi, dan perbarui status. Status dikonfigurasi belum berarti pengiriman sudah diuji.

Uji nyata: tulis `Kirim pesan ke Discord: Uji integrasi Orbit dari tim Venture Creation.` Matikan **Uji tanpa mengirim**, susun langkah, periksa isi pesan, lalu setujui. Pastikan pesan muncul pada channel yang benar. Jangan melakukan retry otomatis ketika hasil tidak dapat dikonfirmasi.

Referensi: [Discord webhooks](https://docs.discord.com/developers/resources/webhook).

## 3. Google — Drive dan Calendar

Siapkan di Google Cloud project milik tim:

1. Aktifkan **Google Drive API** dan **Google Calendar API**.
2. Konfigurasi OAuth consent screen dengan nama aplikasi dan email kontak tim.
3. Tambahkan akun Google anggota sebagai test user jika aplikasi berada dalam mode testing.
4. Buat OAuth client bertipe **Web application**.
5. Simpan client ID dan client secret secara privat.

**Kode OAuth belum diimplementasikan.** Redirect URI final akan ditetapkan saat endpoint callback dibuat; harus sama persis antara backend dan Google Cloud. Jangan menganggap pengisian credentials saja sudah mengaktifkan Drive/Calendar.

Untuk pengembangan, utamakan akun pengujian yang bisa mengizinkan aplikasi pihak ketiga. Akun kampus dapat dibatasi oleh admin institusi. Saat memakai mode testing, siapkan kemungkinan diminta login ulang sebelum presentasi.

Scope direncanakan sesempit mungkin: `drive.file` untuk file yang digunakan bersama aplikasi; akses event pada kalender yang dipilih untuk Calendar. Implementasi harus menangani state OAuth, refresh token, enkripsi token, dan pemisahan token per pengguna.

Referensi: [Google OAuth untuk aplikasi web](https://developers.google.com/identity/protocols/oauth2/web-server).

## 4. Database, akun, dan deployment

Pekerjaan engineering berikutnya:

- PostgreSQL + EF Core: pengguna, proyek, tugas, koneksi, workflow, langkah dan hasil.
- Login dan pembatasan akses menurut pemilik workspace.
- Hubungkan workflow ke proyek melalui ID, bukan hanya teks instruksi.
- Parameter tool bertipe; UI untuk memperbaiki detail serta meminta informasi yang kurang.
- Jalankan Drive/Calendar dan teruskan ID/tautan hasil ke langkah pesan.
- Persistensi antrean dan pemulihan status setelah server restart.
- Pengujian akun nyata, token kedaluwarsa, request gagal, dan pencegahan duplikasi.
- Deployment dengan HTTPS setelah login, penyimpanan rahasia, dan isolasi data selesai.

Yang perlu diputuskan tim: pemilik akun cloud, batas biaya API/hosting, dan apakah presentasi Kamis memakai localhost atau URL publik. Domain berbayar belum diperlukan untuk pengembangan lokal.

## Pembagian empat orang

| Penanggung jawab | Fokus |
| --- | --- |
| Frontend | UI, perbaikan rencana, aksesibilitas, pengujian pengguna |
| Backend inti | Database, login, workspace, antrean, persistensi |
| Google | OAuth, Drive, Calendar, penanganan token |
| AI dan Discord | Planner, validasi parameter, webhook, pengujian alur |

Prioritas terdekat: OpenAI + Discord nyata, lalu pembuatan folder Google Drive. Proyek dan checklist lokal tetap berguna selama koneksi dikerjakan.
