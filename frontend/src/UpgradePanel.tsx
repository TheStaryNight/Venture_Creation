import { useState } from 'react';
import { ArrowLeft, ArrowRight, Check, LockKeyhole } from 'lucide-react';
import { api, type Billing } from './data';

export function UpgradePanel({ billing, close, activated }: { billing: Billing | null; close: () => void; activated: (value: Billing) => void }) {
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  async function pay() {
    setBusy(true); setError('');
    try { activated(await api<Billing>('/billing/simulate-payment', {})); }
    catch (e) { setError(e instanceof Error ? e.message : 'Aktivasi belum berhasil. Coba lagi.'); }
    finally { setBusy(false); }
  }
  const [checkout, setCheckout] = useState(false);
  const price = new Intl.NumberFormat('id-ID', { style: 'currency', currency: 'IDR', maximumFractionDigits: 0 }).format(billing?.monthlyPrice ?? 29000);
  return <div className="upgrade-panel">
    <p className="upgrade-intro">Mulai dari Discord. Hubungkan folder, jadwal, dan rapat kelompok dalam satu pekerjaan dengan Pro.</p>
    {billing?.developmentAccess && <p className="workflow-notice">Akses Pro untuk pengembangan sedang aktif. Ini bukan langganan berbayar.</p>}
    {billing?.plan === 'pro' ? <><div className="checkout-unavailable" role="status"><Check size={20}/><div><strong>Orbit Pro aktif</strong><p>{billing.simulationAccess ? 'Pembayaran simulasi selesai. Tidak ada uang yang ditagih. Akses berlaku selama server lokal berjalan.' : 'Akses pengembangan aktif.'}</p><p>Drive, Calendar, dan Meet sudah terbuka. Hubungkan akun Google untuk menggunakannya.</p></div></div><button className="button primary full" onClick={close}>Mulai gunakan Pro<ArrowRight size={15}/></button></> : !checkout ? <>
      <div className="pricing-grid">
        <section className="price-card"><span className="label">FREE</span><h3>Rp0</h3><p>Untuk memulai</p><ul><li><Check size={14}/>Kirim pesan ke Discord</li><li><Check size={14}/>Proyek & checklist lokal</li><li><Check size={14}/>Tinjau rencana pekerjaan</li></ul><span className="plan-caption">Tetap tersedia gratis</span></section>
        <section className="price-card featured"><span className="label">PRO</span><h3>{price}<small>/ bulan</small></h3><p>Per workspace · harga usulan</p><ul><li><Check size={14}/>Semua fitur Free</li><li><Check size={14}/>Folder Google Drive</li><li><Check size={14}/>Jadwal Google Calendar</li><li><Check size={14}/>Tautan Google Meet</li><li><Check size={14}/>Kirim tautan hasil ke Discord</li></ul></section>
      </div>
      <p className="upgrade-footnote">Google Docs belum termasuk fitur yang tersedia. Harga dan kuota penggunaan akan ditetapkan sebelum peluncuran.</p>
      <div className="dialog-actions"><button className="button secondary" onClick={close}>Tetap di Free</button><button className="button primary" onClick={() => setCheckout(true)}>Lihat ringkasan upgrade<ArrowRight size={15}/></button></div>
    </> : <>
      <button className="text-button" onClick={() => setCheckout(false)}><ArrowLeft size={14}/>Kembali ke paket</button>
      <div className="checkout-summary"><span className="label">RINGKASAN UPGRADE</span><div><strong>Orbit Pro</strong><span>Bulanan · 1 workspace</span></div><div><span>Harga paket yang diusulkan</span><strong>{price}/bulan</strong></div></div>
      <div className="checkout-unavailable" role="status"><LockKeyhole size={20}/><div><strong>Pembayaran simulasi</strong><p>Tekan Bayar untuk membuka Pro di server lokal ini. Tidak ada uang yang ditagih atau data pembayaran yang diminta.</p></div></div>
      {error && <p className="banner error" role="alert">{error}</p>}<button className="button primary full" disabled={busy} onClick={() => void pay()}>{busy ? 'Mengaktifkan Pro…' : 'Bayar — simulasi'}</button>
      <p className="upgrade-footnote">Akses simulasi berlaku selama server lokal berjalan. Setelah restart, kamu bisa mengaktifkannya lagi dari popup ini.</p>
    </>}
  </div>;
}
