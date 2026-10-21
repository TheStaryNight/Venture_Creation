import { useEffect, useRef } from 'react';

/** Replay the entrance animation without resetting form or workflow state. */
export function usePageTransition(page: string) {
  const contentRef = useRef<HTMLDivElement>(null);
  useEffect(() => {
    const media = matchMedia('(prefers-reduced-motion: reduce)');
    if (media.matches) return;
    const animation = contentRef.current?.animate(
      [
        { opacity: 0, transform: 'translateY(10px)' },
        { opacity: 1, transform: 'none' },
      ],
      { duration: 380, easing: 'cubic-bezier(0.22, 1, 0.36, 1)' },
    );
    const stop = () => {
      if (media.matches) animation?.cancel();
    };
    media.addEventListener('change', stop);
    return () => {
      animation?.cancel();
      media.removeEventListener('change', stop);
    };
  }, [page]);
  return contentRef;
}
