// Time filters for the stats API and exports (since/from, to).
// Accepted: epoch ms ("1794112353226"), ISO with an offset or Z ("2026-11-10T09:00+09:00", taken as is), and a
// date or date-time without an offset ("2026-11-10", "2026-11-10T09:00"), read as wall-clock time in a time zone:
// the request's ?tz= (IANA name like Asia/Seoul, or an offset like +09:00), else the server's local zone.
// So ?since=2026-11-10 means local midnight, not UTC midnight (which is 09:00 in Korea).

export const SERVER_TZ = Intl.DateTimeFormat().resolvedOptions().timeZone;

const OFFSET = /^([+-])(\d{2}):?(\d{2})$/;
const LOCAL = /^(\d{4})-(\d{2})-(\d{2})(?:[T ](\d{2}):(\d{2})(?::(\d{2})(?:\.(\d{1,3}))?)?)?$/;

// Throws RangeError for an unknown zone.
export function checkTz(tz) {
  if (OFFSET.test(tz)) return tz;
  new Intl.DateTimeFormat('en-US', { timeZone: tz });
  return tz;
}

// Offset of zone tz from UTC at instant t, in ms (Asia/Seoul -> +9h).
function offsetAt(tz, t) {
  const m = OFFSET.exec(tz);
  if (m) return (m[1] === '-' ? -1 : 1) * (Number(m[2]) * 60 + Number(m[3])) * 60000;
  const parts = Object.fromEntries(new Intl.DateTimeFormat('en-US', {
    timeZone: tz, hourCycle: 'h23', year: 'numeric', month: '2-digit', day: '2-digit', hour: '2-digit', minute: '2-digit', second: '2-digit',
  }).formatToParts(new Date(t)).map((p) => [p.type, p.value]));
  const asUtc = Date.UTC(+parts.year, +parts.month - 1, +parts.day, +parts.hour, +parts.minute, +parts.second);
  return asUtc - Math.floor(t / 1000) * 1000;
}

// Epoch ms for v, or null when v is not a time. tz applies only to values without an offset.
export function parseTime(v, tz = SERVER_TZ) {
  if (v == null || v === '') return null;
  if (/^\d+$/.test(v)) return Number(v);
  const m = LOCAL.exec(v);
  if (m) {
    const wall = Date.UTC(+m[1], +m[2] - 1, +m[3], +(m[4] ?? 0), +(m[5] ?? 0), +(m[6] ?? 0), +(m[7] ?? 0).toString().padEnd(3, '0'));
    const d = new Date(wall);
    if (d.getUTCMonth() + 1 !== +m[2] || d.getUTCDate() !== +m[3] || +(m[4] ?? 0) > 23 || +(m[5] ?? 0) > 59) return null; // 2026-13-45, 25:00
    // Two passes so a date next to a daylight-saving change lands on the right offset.
    let t = wall - offsetAt(tz, wall);
    t = wall - offsetAt(tz, t);
    return t;
  }
  const t = Date.parse(v);
  return Number.isNaN(t) ? null : t;
}
