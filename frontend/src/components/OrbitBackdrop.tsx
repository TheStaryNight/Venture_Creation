/** Decorative only: never captures clicks or participates in layout. */
export function OrbitBackdrop() {
  return (
    <div className="orbit-backdrop" aria-hidden="true">
      <span className="orbit-glow a" />
      <span className="orbit-glow b" />
      <div className="orbit-ring r1">
        <i className="orbit-dot" />
        <i className="orbit-dot b" />
      </div>
      <div className="orbit-ring r2">
        <i className="orbit-dot" />
        <i className="orbit-dot c" />
      </div>
      <div className="orbit-ring r3">
        <i className="orbit-dot" />
        <i className="orbit-dot c" />
      </div>
      <div className="orbit-ring r4">
        <i className="orbit-dot b" />
      </div>
    </div>
  );
}
