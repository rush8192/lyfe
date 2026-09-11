export function WorldKnowledgeLegend() {
  return (
    <section className="world-knowledge-key" aria-labelledby="world-knowledge-key-title">
      <h3 id="world-knowledge-key-title">Map key</h3>
      <ul className="world-knowledge-legend">
        <li><i className="live" aria-hidden="true" /><span><strong>Live</strong> current</span></li>
        <li><i className="reduced" aria-hidden="true" /><span><strong>Last known</strong> retained</span></li>
        <li><i className="unknown" aria-hidden="true" /><span><strong>Unknown</strong> unavailable</span></li>
      </ul>
      <ul className="species-color-legend" aria-label="Visible species colors">
        <li><i className="controlled" aria-hidden="true" />Our species</li>
        <li><i className="other" aria-hidden="true" />Other species</li>
      </ul>
      <p className="volcanic-vent-legend">
        <i aria-hidden="true" />Volcanic vents <span>more marks = stronger baseline activity</span>
      </p>
    </section>
  );
}
