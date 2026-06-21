type PlaceholderPanelProps = {
  description: string;
  title: string;
};

export function PlaceholderPanel({ description, title }: PlaceholderPanelProps) {
  return (
    <section className="page-grid">
      <article className="card placeholder-panel">
        <p className="eyebrow">Màn hình đã dựng route</p>
        <h2>{title}</h2>
        <p>{description}</p>
        <div className="placeholder-grid">
          <div className="placeholder-block" />
          <div className="placeholder-block" />
          <div className="placeholder-block" />
        </div>
      </article>
    </section>
  );
}
