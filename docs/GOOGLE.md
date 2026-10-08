# Menghubungkan akun Google pada development lokal

## Google Cloud

Edit OAuth client bertipe Web application, lalu tambahkan **Authorized redirect URI** berikut persis:

```text
http://127.0.0.1:5080/api/google/callback
```

Alur ini berjalan di server; JavaScript origin tidak diperlukan. Buka frontend dengan `http://127.0.0.1:5173` agar cookie host sama. Jangan mengganti salah satunya menjadi localhost.

Aktifkan Drive API dan Calendar API. Untuk OAuth consent screen berstatus Testing, masukkan akun Google yang akan login ke daftar test users. Client secret bukan Gemini API key.

## Backend

Di terminal backend, Ctrl+C lalu isi:

```powershell
$env:Google__ClientId='CLIENT_ID_DARI_GOOGLE_CLOUD'
$env:Google__ClientSecret='CLIENT_SECRET_DARI_GOOGLE_CLOUD'
dotnet run --urls http://127.0.0.1:5080
```

Gunakan terminal yang sama agar konfigurasi Gemini dan Discord tetap tersedia. Jangan commit secret. Jangan bagikan secret ke chat.

## Frontend

Koneksi → Google Drive atau Google Calendar → Lihat kebutuhan → **Hubungkan Google**. Pilih akun dan tinjau permintaan izin di Google. Setelah kembali, masing-masing kartu menunjukkan izin yang benar-benar diberikan. Jika hanya satu izin diberikan, aplikasi lain tetap belum terhubung.

Izin yang diminta: `drive.file` untuk file yang digunakan bersama aplikasi, serta `calendar.events.owned` untuk event pada kalender milik pengguna. OAuth state/correlation dan PKCE ditangani middleware ASP.NET Core. Session cookie HttpOnly hanya berisi identitas koneksi yang dilindungi; token disimpan dalam memori backend. Data-protection keys di `.local/keys` diabaikan Git dan dilindungi DPAPI pada Windows.

## Menguji tindakan nyata

Setelah memperbarui kode, restart backend di terminal yang menyimpan konfigurasi, lalu hubungkan Google kembali. Buat rencana baru agar parameter tindakan ikut tersimpan.

1. Matikan **Uji tanpa mengirim**.
2. Tulis: `Buat folder Google Drive bernama user testing dan kirim tautannya ke Discord.`
3. Pilih **Susun langkah**. Periksa nama folder dan isi pesan, lalu **Setujui & jalankan**.
4. Buka tautan hasil di detail pekerjaan dan periksa channel Discord.

Untuk Calendar, sebutkan tanggal, jam mulai, jam selesai, dan zona waktu. Contoh: `Buat acara Diskusi Orbit pada 8 Oktober 2026 pukul 15.00 sampai 16.00 WIB di Google Calendar dan kirim tautannya ke Discord.` Sesuaikan tanggal sebelum menjalankan.

Jika langkah gagal, langkah selanjutnya dihentikan. Hasil langkah sebelumnya tetap tersedia. Periksa layanan sebelum membuat rencana ulang untuk menghindari duplikasi. Tidak ada retry otomatis.

## Batas implementasi saat ini

- Workflow dapat membuat folder My Drive dan event kalender utama, lalu menambahkan tautan hasil ke pesan Discord.
- Folder tetap memakai izin Google bawaannya. Mengirim tautan tidak memberikan izin berbagi. Event dibuat tanpa undangan peserta.
- Token belum disimpan permanen atau diperbarui otomatis. Setelah backend restart atau access token kedaluwarsa, hubungkan ulang.
- Koneksi terikat pada cookie browser, bukan akses Google global untuk semua browser.
- URL lokal dan kebijakan cookie ini khusus development. Deployment publik memerlukan HTTPS dan konfigurasi origin/callback tersendiri.
- Verifikasi login Google nyata membutuhkan credentials serta persetujuan pemilik akun; build dan tes lokal tidak membuktikan consent berhasil.

Troubleshooting: `redirect_uri_mismatch` berarti callback di Cloud berbeda. `access_denied` dapat berarti akun belum menjadi test user atau izin ditolak. Pesan kegagalan OAuth pada frontend sengaja tidak menyertakan code, token, atau secret.

Referensi: [Google OAuth web server](https://developers.google.com/identity/protocols/oauth2/web-server).

## Google Meet dan paket Pro

Permintaan Meet memakai event Calendar dengan `conferenceData.createRequest` dan `conferenceDataVersion=1`, melalui izin Calendar yang sama. Akun/calendar harus mendukung Google Meet. Orbit menunggu hasil pembuatan konferensi secara terbatas; jika belum tersedia atau gagal, event yang sudah dibuat tetap ditautkan dan langkah berikutnya dihentikan. Jangan membuat ulang sebelum memeriksa event tersebut.

Integrasi Google nyata termasuk Pro. Lihat [paket dan akses pengembangan](MONETIZATION.md) untuk menjalankan pengujian lokal. Tidak ada pembayaran aktif.

Referensi: [Membuat acara dengan konferensi Google Meet](https://developers.google.com/workspace/calendar/api/guides/create-events).
