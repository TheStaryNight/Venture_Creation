# Orbit Free dan Pro

## Model pendapatan untuk Venture Creation

Orbit menjual otomatisasi pekerjaan lintas aplikasi melalui langganan per workspace. Discord gratis membantu kelompok mencoba alur instruksi → review → kirim. Kelompok yang perlu menyiapkan folder, jadwal, dan tautan rapat ditawarkan Pro.

| Paket | Harga usulan | Fitur yang sudah tersedia |
| --- | --- | --- |
| Free | Rp0 | Discord, proyek/checklist lokal, review rencana |
| Pro | Rp29.000/workspace/bulan | Free + folder Drive, Calendar, tautan Meet, meneruskan hasil ke Discord |

Harga ini hipotesis untuk diuji, bukan hasil riset pasar. Google Docs belum tersedia. Kolaborasi anggota dan penyimpanan permanen juga belum tersedia. Paket sekarang ditampilkan sebagai pratinjau; belum menerima pembayaran.

## Cara memvalidasi

1. Wawancarai 10 kelompok mahasiswa tentang frekuensi koordinasi, aplikasi yang dipakai, dan waktu yang terbuang.
2. Minta mereka menyelesaikan satu pekerjaan nyata dengan Orbit dan catat waktu, kegagalan, serta langkah manual yang tersisa.
3. Tawarkan harga yang sama setelah mereka mencoba. Bedakan minat, klik upgrade, dan orang yang benar-benar bersedia membayar.
4. Pantau kelompok aktif mingguan, penggunaan berulang, klik upgrade, konversi berbayar, dan pembatalan setelah pembayaran tersedia.
5. Ukur biaya AI per rencana, hosting, dukungan, serta biaya payment gateway. Tetapkan kuota sebelum menjanjikan paket tanpa batas.

Contoh aritmetika untuk presentasi: 100 workspace berbayar × Rp29.000 = Rp2.900.000 pendapatan bulanan kotor. Ini skenario, bukan proyeksi, dan belum dikurangi biaya.

## Perilaku aplikasi saat ini

- Klik kartu Google yang terkunci atau Orbit Free di sidebar untuk membuka popup paket, lalu ringkasan upgrade.
- Tombol **Bayar — simulasi** membuka akses Pro pada server lokal tanpa payment gateway. UI menyatakan simulasi; tidak ada uang ditagih. Akses tersimpan di memori, berlaku untuk server lokal, dan kembali Free setelah restart kecuali override pengembangan aktif.
- Backend mengembalikan HTTP 402 `upgrade_required` untuk pekerjaan Google nyata pada Free, di tahap perencanaan dan eksekusi. Worker juga memeriksa akses.
- Uji tanpa mengirim tetap bisa dipakai untuk mempelajari rencana Google tanpa membuat data.
- Aktivasi dilakukan melalui endpoint simulasi khusus koneksi loopback, bukan localStorage. Endpoint ini harus dihapus/dinonaktifkan sebelum deployment publik, termasuk saat memakai reverse proxy.

## Menguji integrasi Google sebagai pengembang

Cara termudah: klik **Orbit Free → Lihat ringkasan upgrade → Bayar — simulasi → Mulai gunakan Pro**. Tidak perlu environment variable.

Alternatif override developer: di terminal backend yang menyimpan konfigurasi Google/AI/Discord, hentikan server lalu jalankan dari root repo:

```powershell
$env:ASPNETCORE_ENVIRONMENT='Development'
$env:Billing__DevelopmentPro='true'
dotnet run --project backend/AgentHub.Api --urls http://127.0.0.1:5080
```

Hubungkan ulang Google setelah restart. Override hanya berlaku pada environment Development dan ditampilkan sebagai akses pengembangan, bukan langganan berbayar. Untuk kembali ke Free, isi `Billing__DevelopmentPro='false'` lalu restart. Ini berlaku untuk server lokal, belum merupakan sistem akun atau lisensi per workspace.

Contoh instruksi: `Buat acara Diskusi Orbit pada 9 Oktober 2026 pukul 15.00 sampai 16.00 WIB dengan Google Meet, lalu kirim tautannya ke Discord.` Sesuaikan tanggal sebelum menjalankan.

## Sebelum pembayaran sungguhan

Siapkan identitas akun/workspace, database langganan, akun merchant payment gateway, halaman checkout provider, verifikasi webhook di server, pemrosesan webhook idempotent, masa aktif paket, serta aturan pembatalan/refund. Harga harus ditentukan server. Return URL atau klik tombol tidak boleh dianggap bukti pembayaran. Pilih provider dan kebijakan harga setelah kebutuhan tim disepakati.
