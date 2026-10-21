import { useState } from 'react';
export function useStored<T>(key: string, initial: T): [T, (value: T) => void] {
  const [value, setValue] = useState<T>(() => {
    try {
      const raw = localStorage.getItem(key);
      return raw ? JSON.parse(raw) : initial;
    } catch {
      return initial;
    }
  });
  return [
    value,
    (next) => {
      localStorage.setItem(key, JSON.stringify(next));
      setValue(next);
    },
  ];
}
