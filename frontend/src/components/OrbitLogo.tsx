import './OrbitLogo.css';

/** Three intersecting orbital planes. The surrounding brand supplies the accessible name. */
export function OrbitLogo({ animated = true }: { animated?: boolean }) {
  return (
    <span className={`orbit-logo${animated ? ' orbit-logo--animated' : ''}`} aria-hidden="true">
      <span className="orbit-logo__ring orbit-logo__ring--one" />
      <span className="orbit-logo__ring orbit-logo__ring--two" />
      <span className="orbit-logo__ring orbit-logo__ring--three" />
    </span>
  );
}
