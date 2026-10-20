import { t, locale } from './i18n';
import { useState } from 'react';
import { ArrowLeft, ArrowRight, Check, LockKeyhole } from 'lucide-react';
import { api, type Billing } from './data';

export function UpgradePanel({ billing, close, activated }: { billing: Billing | null; close: () => void; activated: (value: Billing) => void }) {
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  async function pay() {
    setBusy(true); setError('');
    try { activated(await api<Billing>(billing?.trialAvailable !== undefined ? '/billing/start-trial' : '/billing/simulate-payment', {})); }
    catch (e) { setError(e instanceof Error ? e.message : t("Aktivasi belum berhasil. Coba lagi.")); }
    finally { setBusy(false); }
  }
  const [checkout, setCheckout] = useState(false);
  const price = new Intl.NumberFormat('id-ID', { style: 'currency', currency: 'IDR', maximumFractionDigits: 0 }).format(billing?.monthlyPrice ?? 29000);
  return <div className="upgrade-panel">
    <p className="upgrade-intro">{t("Mulai dari Discord. Hubungkan folder, jadwal, dan rapat kelompok dalam satu pekerjaan dengan Pro.")}</p>
    {billing?.developmentAccess && <p className="workflow-notice">{t("Akses Pro untuk pengembangan sedang aktif. Ini bukan langganan berbayar.")}</p>}
    {billing?.plan === 'pro' ? <><div className="checkout-unavailable" role="status"><Check size={20}/><div><strong>{t("Orbit Pro aktif")}</strong><p>{billing.trialAccess ? `${t('Trial Pro aktif sampai')} ${new Date(billing.trialEndsAt!).toLocaleDateString(locale())}. ${billing.trialRemaining} ${t('workflow Google tersisa')}.` : billing.simulationAccess ? t("Pembayaran simulasi selesai. Tidak ada uang yang ditagih. Akses berlaku selama server lokal berjalan.") : t("Akses pengembangan aktif.")}</p><p>{t("Drive, Calendar, dan Meet sudah terbuka. Hubungkan akun Google untuk menggunakannya.")}</p></div></div><button className="button primary full" onClick={close}>{t("Mulai gunakan Pro")}<ArrowRight size={15}/></button></> : !checkout ? <>
      <div className="pricing-grid">
        <section className="price-card"><span className="label">FREE</span><h3>Rp0</h3><p>{t("Untuk memulai")}</p><ul><li><Check size={14}/>{t("Kirim pesan ke Discord")}</li><li><Check size={14}/>{t("20 penyusunan AI per bulan")}</li><li><Check size={14}/>{t("Proyek & checklist lokal")}</li><li><Check size={14}/>{t("Tinjau rencana pekerjaan")}</li></ul><span className="plan-caption">{t("Tetap tersedia gratis")}</span></section>
        <section className="price-card featured"><span className="label">PRO</span><h3>{price}<small>{t("/ bulan")}</small></h3><p>{t("Per workspace · harga usulan")}</p><ul><li><Check size={14}/>{t("Semua fitur Free")}</li><li><Check size={14}/>{t("Folder Google Drive")}</li><li><Check size={14}/>{t("Jadwal Google Calendar")}</li><li><Check size={14}/>{t("Tautan Google Meet")}</li><li><Check size={14}/>{t("Kirim tautan hasil ke Discord")}</li></ul></section>
      </div>
      <p className="upgrade-footnote">{t("Google Docs belum termasuk fitur yang tersedia. Trial Pro: 7 hari, maksimal 10 workflow Google. Penyusunan AI tetap dibatasi 20 per bulan selama tahap awal.")}</p>
      <div className="dialog-actions"><button className="button secondary" onClick={close}>{t("Tetap di Free")}</button><button className="button primary" onClick={() => setCheckout(true)}>{t("Lihat ringkasan upgrade")}<ArrowRight size={15}/></button></div>
    </> : <>
      <button className="text-button" onClick={() => setCheckout(false)}><ArrowLeft size={14}/>{t("Kembali ke paket")}</button>
      <div className="checkout-summary"><span className="label">{t("RINGKASAN UPGRADE")}</span><div><strong>Orbit Pro</strong><span>{t("Bulanan · 1 workspace")}</span></div><div><span>{t("Harga paket yang diusulkan")}</span><strong>{price}{t("/bulan")}</strong></div></div>
      <div className="checkout-unavailable" role="status"><LockKeyhole size={20}/><div><strong>{billing?.trialAvailable !== undefined ? t("Coba Pro gratis") : t("Pembayaran simulasi")}</strong><p>{billing?.trialAvailable !== undefined ? t("Aktifkan trial satu kali selama 7 hari untuk maksimal 10 workflow Google. Tidak ada tagihan otomatis.") : t("Tekan Bayar untuk membuka Pro di server lokal ini. Tidak ada uang yang ditagih.")}</p></div></div>
      {error && <p className="banner error" role="alert">{error}</p>}<button className="button primary full" disabled={busy || billing?.trialAvailable === false} onClick={() => void pay()}>{busy ? t("Mengaktifkan Pro…") : billing?.trialAvailable === true ? t("Aktifkan trial Pro gratis") : billing?.trialAvailable === false ? t("Trial sudah digunakan · pembayaran belum tersedia") : t("Bayar — simulasi")}</button>
      <p className="upgrade-footnote">{billing?.trialAvailable !== undefined ? `${t("Sisa kuota penyusunan AI bulan ini:")} ${billing.plansRemaining ?? 20}. ${t("Workflow yang disetujui mengurangi kuota trial, termasuk jika layanan gagal setelah pekerjaan dimulai.")}` : t("Akses simulasi berlaku selama server lokal berjalan.")}</p>
    </>}
  </div>;
}
