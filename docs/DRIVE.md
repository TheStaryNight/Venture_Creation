# Folder Drive yang sudah ada

Orbit membedakan `create_folder`, `use_existing_folder`, dan `list_folder_files`. Rencana folder lama memerlukan pemilihan folder di layar tinjauan. Backend memeriksa folder dengan akun pemilik workflow, menyimpan ID hasil Google, dan memeriksa ulang akses saat eksekusi. Nama folder tidak dipakai sebagai identitas.

Pembuatan folder baru memerlukan checkbox konfirmasi tersendiri. Rencana lama tanpa jenis tindakan Drive harus disusun ulang. Permintaan folder lama yang gagal tidak pernah diganti menjadi pembuatan folder.

Login Google sekarang meminta `https://www.googleapis.com/auth/drive.metadata.readonly` selain `drive.file`. Akun lama perlu menghubungkan ulang Google dan menyetujui izin tambahan. Scope metadata dapat menampilkan metadata seluruh Drive yang bisa diakses akun, termasuk nama file; aplikasi ini hanya memakai metadata untuk pencarian folder dan daftar file. Tambahkan scope pada Google Auth Platform → Data Access. Scope ini termasuk restricted scope menurut dokumentasi Google; persyaratan verifikasi untuk penggunaan publik tetap berlaku. Selama Testing, gunakan akun test user yang disetujui.

Pada tinjauan rencana, cari nama folder, buka tautannya untuk membedakan folder bernama sama, lalu pilih. Pencarian menyediakan halaman berikutnya. Daftar isi saat eksekusi berisi maksimal 20 file langsung dalam folder, tanpa membaca isi dokumen/gambar atau menelusuri subfolder. Jika ada halaman berikutnya, hasil menyebutkan batas tersebut. Link folder dan daftar nama dari Google diteruskan ke Discord jika langkah pesan meminta hasil sebelumnya. Izin berbagi tidak berubah; penerima tetap membutuhkan akses Google Drive. Pesan yang melewati batas Discord dihentikan dengan error, tanpa mengirim pesan parsial.

Referensi: https://developers.google.com/workspace/drive/api/guides/api-specific-auth dan https://developers.google.com/workspace/drive/api/reference/rest/v3/files/list.
