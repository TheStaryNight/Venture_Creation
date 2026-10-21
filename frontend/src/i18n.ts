import translations from './translations.json';
let language = 'id';
export function setLanguage(value: string) {
  language = value;
}
const friendly: Record<string, string> = {
  'Pemahaman instruksi': 'Asisten AI',
  'Susun langkah': 'Buat rencana',
  'Pengiriman nyata': 'Jalankan di aplikasi',
  'Lihat kebutuhan': 'Detail koneksi',
  Dikonfigurasi: 'Siap digunakan',
  'Perlu persetujuan': 'Menunggu persetujuan',
  'Kamu pegang kendali.': 'Kamu yang menentukan.',
  'Kelola aplikasi yang digunakan untuk menjalankan pekerjaan.':
    'Hubungkan aplikasi dan atur kontak untuk pekerjaanmu.',
  'Proyek tersimpan pada akunmu. Undangan anggota belum tersedia.':
    'Proyek tersimpan di akunmu. Workspace bersama belum tersedia.',
};
export function t(text: string): string {
  return language === 'en'
    ? ((translations as Record<string, string>)[text] ?? text)
    : (friendly[text] ?? text);
}

export function locale() {
  return language === 'en' ? 'en-GB' : 'id-ID';
}
