export default function HomePage() {
  return (
    <main className="shell">
      <section className="hero" aria-labelledby="page-title">
        <p className="eyebrow">RAMAI · Foundation</p>
        <h1 id="page-title">La baseline frontend è pronta.</h1>
        <p className="summary">
          Next.js, React e TypeScript sono configurati. Le funzionalità applicative
          verranno introdotte negli sprint successivi.
        </p>
        <dl className="stack" aria-label="Stack frontend">
          <div>
            <dt>Framework</dt>
            <dd>Next.js · App Router</dd>
          </div>
          <div>
            <dt>Linguaggio</dt>
            <dd>TypeScript</dd>
          </div>
          <div>
            <dt>Stato</dt>
            <dd>Foundation</dd>
          </div>
        </dl>
      </section>
    </main>
  );
}
