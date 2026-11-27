/**
 * A calendar file (RFC 5545) for one arranged Game (#9 part 3): an all-day
 * event on its agreed Date, so no time zone enters it, named for its Sides,
 * with the Court as its location. Lines end in CRLF and are folded at 75
 * octets; text values escape backslashes, semicolons, commas and newlines.
 */
export function gameCalendarFile(game: {
  id: string;
  /** The agreed Date, YYYY-MM-DD. */
  date: string;
  summary: string;
  description: string;
  court?: string | null;
  /** When the file is made, for DTSTAMP. */
  now: Date;
}): string {
  const day = game.date.replaceAll("-", "");
  const next = new Date(Date.parse(`${game.date}T00:00:00Z`) + 24 * 60 * 60 * 1000).toISOString().slice(0, 10).replaceAll("-", "");
  const stamp = game.now.toISOString().replace(/[-:]/g, "").replace(/\.\d{3}/, "");
  const lines = [
    "BEGIN:VCALENDAR",
    "VERSION:2.0",
    "PRODID:-//Hits & Giggles//Tennis leagues//EN",
    "CALSCALE:GREGORIAN",
    "METHOD:PUBLISH",
    "BEGIN:VEVENT",
    `UID:${game.id}@hitsandgiggles`,
    `DTSTAMP:${stamp}`,
    `DTSTART;VALUE=DATE:${day}`,
    `DTEND;VALUE=DATE:${next}`,
    `SUMMARY:${text(game.summary)}`,
    ...(game.court ? [`LOCATION:${text(game.court)}`] : []),
    `DESCRIPTION:${text(game.description)}`,
    "END:VEVENT",
    "END:VCALENDAR",
  ];
  return lines.map(fold).join("\r\n") + "\r\n";
}

const text = (value: string) =>
  value.replaceAll("\\", "\\\\").replaceAll(";", "\;").replaceAll(",", "\\,").replace(/\r?\n/g, "\\n");

/** Folds a line longer than 75 octets: a CRLF and a space before each further part, never inside a character. */
function fold(line: string): string {
  const encoder = new TextEncoder();
  const parts: string[] = [];
  let current = "";
  for (const character of line) {
    const limit = parts.length === 0 ? 75 : 74;
    if (encoder.encode(current + character).length > limit) {
      parts.push(current);
      current = "";
    }
    current += character;
  }
  parts.push(current);
  return parts.join("\r\n ");
}
