import { CalendarDays, Command, FileText, Folder, MessageSquare, Video } from 'lucide-react';
const icons: Record<string, typeof Folder> = {
  docs: FileText,
  gmail: MessageSquare,
  meet: Video,
  drive: Folder,
  calendar: CalendarDays,
  discord: MessageSquare,
};
export function ServiceIcon({ id, small = false }: { id: string; small?: boolean }) {
  const Icon = icons[id] || Command;
  return (
    <span className={`service-icon ${id} ${small ? 'small' : ''}`}>
      <Icon size={small ? 15 : 21} />
    </span>
  );
}
