# Gmail dan Google Docs

## Fitur

- Gmail: `create_draft` menyimpan draft; `send_email` mengirim email setelah persetujuan rencana. Subjek, seluruh isi teks, dan penerima ditampilkan sebelum persetujuan. Penerima berasal dari email pada instruksi, kontak, atau grup; pilihan ambigu harus diselesaikan dahulu.
- Docs: `create_document` membuat dokumen berisi teks di My Drive; `create_document_in_folder` meminta pemilihan folder sebelum persetujuan. Konten ditampilkan sebelum dijalankan. Izin berbagi tidak diubah.
- Keduanya mengikuti akses Pro/trial dan kuota workflow yang sama dengan integrasi Google lain. Mode uji tidak membuat draft, dokumen, atau mengirim email.
- Kegagalan menghentikan langkah berikutnya. Tidak ada pengiriman ulang otomatis. Jika dokumen sudah dibuat tetapi pengisian gagal, tautannya tetap ditampilkan untuk diperiksa.

## Aktivasi setelah deploy

1. Deploy backend dan frontend dari perubahan yang sama.
2. Pada Google Cloud project OAuth Orbit, aktifkan **Gmail API**, **Google Docs API**, dan **Google Drive API**.
3. Di Google Auth Platform → Data Access, tambahkan `https://www.googleapis.com/auth/gmail.compose`. Docs memakai izin `drive.file` yang sudah dipakai Orbit.
4. Selama aplikasi berstatus Testing, pastikan akun penguji ada di Test users.
5. Di Orbit → Koneksi → Gmail → Hubungkan ulang Google, setujui izin baru. Token lama tidak otomatis mendapat izin Gmail.
6. Uji draft terlebih dahulu dan periksa folder Drafts pada akun Google yang terhubung. Tautan Gmail membuka Drafts/Sent; jika beberapa akun Google terbuka, pilih akun yang terhubung dengan Orbit.

Tidak ada API key baru, perubahan redirect URI, atau migrasi database untuk fitur ini. Scope Gmail compose memiliki persyaratan verifikasi Google sebelum penggunaan publik yang luas; jangan menganggap daftar test user sebagai peluncuran publik.

## Contoh

- “Buat draft email ke teman@example.com, subjek Diskusi proyek, isinya mengajak diskusi proposal pada Jumat sore.”
- “Kirim email ke Grup A, subjek Persiapan presentasi, isinya minta setiap anggota membawa hasil pekerjaannya besok.”
- “Buat Google Docs berjudul Proposal Orbit berisi latar belakang, masalah mahasiswa, solusi, dan model bisnis.”
- “Buat dokumen Catatan Diskusi di folder user testing, berisi agenda pembagian tugas.” Pilih folder yang benar pada pratinjau.

## Batasan

Belum membaca inbox, membalas thread, melampirkan file, mengedit dokumen lama, atau menerapkan format dokumen kompleks. Isi berupa teks biasa. Untuk Docs, folder yang terlihat lewat izin metadata belum tentu dapat ditulisi dengan `drive.file`; folder yang dibuat Orbit dapat dipakai, sedangkan folder lama yang belum diizinkan memerlukan pemberian akses per-file melalui Google Picker pada pengembangan berikutnya. Orbit tidak diam-diam memindahkan dokumen ke My Drive jika folder tujuan ditolak.

## Verifikasi pengembangan

Build frontend/backend dan PlannerChecks. Pengujian menggunakan HTTP tiruan untuk draft vs send, encoding Unicode, penerima rekaan, injeksi header, isolasi token, pemilihan folder, pengisian teks, dan kegagalan parsial. Tidak mengirim email nyata. Tes database opsional memerlukan konfigurasi terpisah.

Referensi: [Gmail drafts](https://developers.google.com/workspace/gmail/api/guides/drafts), [Gmail sending](https://developers.google.com/workspace/gmail/api/guides/sending), [Docs scopes](https://developers.google.com/workspace/docs/api/auth).
