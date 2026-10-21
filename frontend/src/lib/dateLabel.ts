import { locale } from '../i18n';
export const dateLabel = (value: string) =>
  new Date(value).toLocaleDateString(locale(), { day: 'numeric', month: 'short' });
