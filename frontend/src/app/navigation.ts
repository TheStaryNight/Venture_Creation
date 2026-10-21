import {
  CalendarDays,
  Clock3,
  FolderPlus,
  Layers2,
  LayoutGrid,
  MessageSquare,
  Plug,
} from 'lucide-react';
import { t } from '../i18n';
export const navigation = [
  { id: 'home', title: t('Ringkasan'), icon: LayoutGrid },
  { id: 'projects', title: t('Project'), icon: Layers2 },
  { id: 'activity', title: t('Percakapan'), icon: Clock3 },
  { id: 'connections', title: t('Koneksi'), icon: Plug },
];
export const shortcuts = [
  {
    title: t('Siapkan project'),
    icon: FolderPlus,
    prompt: 'Buat folder untuk project kelompok dan kirim tautannya ke Discord.',
  },
  {
    title: t('Atur pertemuan'),
    icon: CalendarDays,
    prompt: 'Jadwalkan pertemuan kelompok untuk membahas progres project.',
  },
  {
    title: t('Kirim update'),
    icon: MessageSquare,
    prompt:
      'Kirim pesan ke Discord: Teman-teman, mohon update progres tugas masing-masing sebelum diskusi berikutnya.',
  },
];
