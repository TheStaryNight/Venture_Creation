export const statuses: Record<string, string> = {
  planned: 'Perlu persetujuan',
  queued: 'Dalam antrean',
  running: 'Berjalan',
  completed: 'Selesai',
  failed: 'Perlu diperiksa',
  pending: 'Menunggu',
  skipped: 'Dilewati',
};

export const services = [
  {
    id: 'gmail',
    name: 'Gmail',
    action: 'Draft & kirim email',
    description: 'Tinjau penerima dan isi email sebelum dikirim.',
    available: true,
  },
  {
    id: 'docs',
    name: 'Google Docs',
    action: 'Dokumen baru',
    description: 'Buat dokumen berisi teks di Google Drive.',
    available: true,
  },
  {
    id: 'drive',
    name: 'Google Drive',
    action: 'Folder proyek',
    description: 'Simpan dokumen proyek dalam satu tempat.',
    available: true,
  },
  {
    id: 'calendar',
    name: 'Google Calendar',
    action: 'Jadwal pertemuan',
    description: 'Buat jadwal diskusi dari instruksi pekerjaan.',
    available: true,
  },
  {
    id: 'meet',
    name: 'Google Meet',
    action: 'Tautan rapat video',
    description: 'Buat tautan Meet bersama jadwal di Google Calendar.',
    available: true,
  },
  {
    id: 'discord',
    name: 'Discord',
    action: 'Pesan ke kelompok',
    description: 'Kirim pembagian tugas dan update ke channel kelompok.',
    available: true,
  },
];
