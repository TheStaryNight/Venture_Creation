import { X } from 'lucide-react';
import { useEffect, useId, useRef, type ReactNode } from 'react';
import { t } from '../i18n';

export function Dialog({
  title,
  close,
  children,
}: {
  title: string;
  close: () => void;
  children: ReactNode;
}) {
  const ref = useRef<HTMLDialogElement>(null);
  const titleId = useId();
  useEffect(() => {
    const dialog = ref.current!;
    dialog.showModal();
    return () => dialog.close();
  }, []);
  return (
    <dialog
      ref={ref}
      className="dialog"
      aria-labelledby={titleId}
      onCancel={(e) => {
        e.preventDefault();
        close();
      }}
    >
      <div className="dialog-heading">
        <h2 id={titleId}>{t(title)}</h2>
        <button className="icon-button" aria-label={t('Tutup dialog')} onClick={close}>
          <X size={19} />
        </button>
      </div>
      {children}
    </dialog>
  );
}
