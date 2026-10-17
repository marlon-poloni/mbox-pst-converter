const integer = new Intl.NumberFormat("pt-BR", { maximumFractionDigits: 0 });
const oneDecimal = new Intl.NumberFormat("pt-BR", { minimumFractionDigits: 1, maximumFractionDigits: 1 });

export function formatInt(value: number): string {
  return integer.format(value);
}

export function formatBytes(bytes: number): string {
  const kb = 1024;
  const mb = kb * 1024;
  const gb = mb * 1024;
  if (bytes >= gb) return `${oneDecimal.format(bytes / gb)} GB`;
  if (bytes >= mb) return `${integer.format(Math.round(bytes / mb))} MB`;
  return `${integer.format(Math.max(1, Math.round(bytes / kb)))} KB`;
}

export function formatSeconds(seconds: number): string {
  if (seconds < 1) return "menos de 1 s";
  const s = Math.round(seconds);
  if (s < 60) return `${s} s`;
  if (s < 3600) {
    const rest = s % 60;
    return rest ? `${Math.floor(s / 60)} min ${rest} s` : `${Math.floor(s / 60)} min`;
  }
  const minutes = Math.floor((s % 3600) / 60);
  return minutes ? `${Math.floor(s / 3600)} h ${minutes} min` : `${Math.floor(s / 3600)} h`;
}

export function formatBytesPair(part: number, total: number): string {
  const [, unit] = formatBytes(total).split(" ");
  const [partValue, partUnit] = formatBytes(part).split(" ");
  return partUnit === unit ? `${partValue} de ${formatBytes(total)}` : `${formatBytes(part)} de ${formatBytes(total)}`;
}
