type WeatherCardProps = {
  label: string;
  value: string;
  helperText: string;
};

export function WeatherCard({ helperText, label, value }: WeatherCardProps) {
  return (
    <article className="card metric-card">
      <span>{label}</span>
      <strong>{value}</strong>
      <small>{helperText}</small>
    </article>
  );
}
