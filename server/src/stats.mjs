// Aggregations for the dashboard. Works on rows from db.query(); events are replayed per session
// into "attempts" (level_start .. level_clear | level_leave | end of session) so every metric shares one definition.
export const QUIT_AFTER_MS = 30 * 60 * 1000; // an attempt with no event for this long counts as abandoned

export const STUCK_UNDO_STREAK = 5; // this many undos in a row counts as being stuck (like a 30s idle)

// Replays rows (ordered by player, session, seq) into attempts. Also tags each row with row.attemptId.
// minMovesFor(levelKey, level) optionally supplies the solver's shortest move count.
export function buildAttempts(rows, now = Date.now(), minMovesFor = () => null) {
  const attempts = [];
  const sessions = [];
  let sess = null;
  let cur = null;
  let closed = null; // the attempt that just ended (events after a clear, e.g. undo, still belong to it)

  const close = (outcome, t, durationMs) => {
    if (!cur) return;
    cur.outcome = outcome;
    cur.tEnd = t;
    cur.durationMs = durationMs ?? Math.max(0, t - cur.t0);
    if (outcome === 'clear' && cur.stuckEnd) {
      cur.lastStuckToClearMoves = cur.moves - cur.stuckEnd.moves;
      cur.lastStuckToClearMs = Math.max(0, t - cur.stuckEnd.t);
    }
    closed = cur;
    cur = null;
  };
  const endSession = () => {
    if (!sess) return;
    if (cur) close(now - sess.lastT > QUIT_AFTER_MS ? 'quit' : 'open', sess.lastT);
    sessions.push(sess);
    sess = null;
  };

  for (const r of rows) {
    if (!sess || sess.sessionId !== r.session_id) {
      endSession();
      sess = {
        sessionId: r.session_id, playerId: r.player_id, client: r.client, build: r.build,
        t0: r.t, lastT: r.t, endReason: null, attempts: [], firstTime: null,
      };
    }
    sess.lastT = Math.max(sess.lastT, r.t);
    const d = r.data;
    r.attemptId = null;
    switch (r.type) {
      case 'level_start': {
        if (cur) close('leave', r.t);
        const prev = sess.attempts.at(-1);
        if (prev && prev.level !== d.level) prev.nextLevelStarted = true;
        cur = {
          id: `${r.session_id}:${r.seq}`,
          playerId: r.player_id, sessionId: r.session_id, client: r.client, build: r.build, sessionFirstTime: sess.firstTime,
          level: d.level, levelName: d.levelName ?? '', levelKey: d.levelKey ?? '', attempt: d.attempt, via: d.via ?? '',
          bodies: d.bodies ?? null, t0: r.t, tEnd: null,
          outcome: 'open', durationMs: 0, moves: 0, blocked: 0, undos: 0, restarts: 0, hints: 0, fails: 0,
          idles: 0, idleMs: 0, idleMsMax: 0, firstHintMs: null, wasted: 0, lost: {}, keeps: 0, switches: 0,
          bodiesUsed: null, minMoves: minMovesFor(d.levelKey, d.level), nextLevelStarted: false,
          undoStreak: 0, stuckEnd: null, lastStuckToClearMoves: null, lastStuckToClearMs: null,
        };
        attempts.push(cur);
        sess.attempts.push(cur);
        r.attemptId = cur.id;
        break;
      }
      case 'session_end':
        sess.endReason = d.reason;
        if (cur) r.attemptId = cur.id;
        break;
      case 'session_start':
        sess.endReason = null;
        if (sess.firstTime == null) sess.firstTime = d.firstTime ?? null;
        break;
      default: {
        if (!cur) { if (closed && closed.sessionId === r.session_id && closed.level === d.level) r.attemptId = closed.id; break; }
        if (d.level !== cur.level) break;
        r.attemptId = cur.id;
        // A run of undos ends when any other input arrives.
        if (r.type !== 'undo') {
          if (cur.undoStreak >= STUCK_UNDO_STREAK) cur.stuckEnd = { t: r.t, moves: cur.moves };
          if (r.type !== 'idle') cur.undoStreak = 0;
        }
        if (r.type === 'move') { if (d.result === 'blocked') cur.blocked++; else cur.moves++; }
        else if (r.type === 'undo') { cur.undos++; cur.undoStreak++; }
        else if (r.type === 'restart') cur.restarts++;
        else if (r.type === 'hint') { cur.hints++; if (cur.firstHintMs == null) cur.firstHintMs = Math.max(0, r.t - cur.t0); }
        else if (r.type === 'keep') cur.keeps++;
        else if (r.type === 'switch') cur.switches++;
        else if (r.type === 'level_fail') cur.fails++;
        else if (r.type === 'idle') {
          cur.idles++; cur.idleMs += d.idleMs; cur.idleMsMax = Math.max(cur.idleMsMax, d.idleMs);
          cur.stuckEnd = { t: r.t, moves: cur.moves }; // idle is sent when the pause ends
        } else if (r.type === 'body_lost') {
          cur.lost[d.reason] = (cur.lost[d.reason] ?? 0) + 1;
          if (d.wasted) cur.wasted++;
        } else if (r.type === 'level_clear') {
          cur.bodiesUsed = d.bodiesUsed ?? null;
          if (d.minMoves != null) cur.minMoves = d.minMoves;
          close('clear', r.t, d.durationMs);
        } else if (r.type === 'level_leave') {
          if (d.toLevel != null && d.toLevel !== cur.level) cur.nextLevelStarted = true;
          close('leave', r.t, d.durationMs);
        }
      }
    }
  }
  endSession();
  return { attempts, sessions };
}

export function quantile(xs, q) {
  if (!xs.length) return null;
  const s = [...xs].sort((a, b) => a - b);
  const pos = (s.length - 1) * q;
  const lo = Math.floor(pos);
  const hi = Math.ceil(pos);
  return Math.round(s[lo] + (s[hi] - s[lo]) * (pos - lo));
}

// Per-level funnel and struggle metrics. levels: [{ n, name }] from the current build (optional).
export function levelStats(rows, levels = [], now = Date.now()) {
  const { attempts, sessions } = buildAttempts(rows, now);
  const by = new Map();
  const get = (n) => {
    if (!by.has(n)) {
      by.set(n, {
        level: n, name: levels.find((l) => l.n === n)?.name ?? '', names: new Set(),
        attempts: 0, players: new Set(), clearPlayers: new Set(), clears: 0, leaves: 0, quits: 0, open: 0,
        clearMs: [], allMs: [], clearMoves: [], undos: 0, restarts: 0, hints: 0, fails: 0, blocked: 0,
        idles: 0, idleMs: 0, wasted: 0, lost: {}, sessions: new Set(), sessionEnds: 0, playerQuits: 0, hintPlayers: new Set(),
      });
    }
    return by.get(n);
  };
  for (const l of levels) get(l.n);

  for (const a of attempts) {
    const s = get(a.level);
    if (a.levelName) s.names.add(a.levelName);
    s.attempts++;
    s.players.add(a.playerId);
    s.sessions.add(a.sessionId);
    if (a.outcome === 'clear') { s.clears++; s.clearPlayers.add(a.playerId); s.clearMs.push(a.durationMs); s.clearMoves.push(a.moves); }
    else if (a.outcome === 'leave') s.leaves++;
    else if (a.outcome === 'quit') s.quits++;
    else s.open++;
    if (a.outcome !== 'open') s.allMs.push(a.durationMs);
    s.undos += a.undos; s.restarts += a.restarts; s.hints += a.hints; s.fails += a.fails; s.blocked += a.blocked;
    s.idles += a.idles; s.idleMs += a.idleMs; s.wasted += a.wasted;
    if (a.hints) s.hintPlayers.add(a.playerId);
    for (const [k, v] of Object.entries(a.lost)) s.lost[k] = (s.lost[k] ?? 0) + v;
  }
  // Drop-off: the session's last attempt was this level and it was not cleared.
  for (const sess of sessions) {
    const last = sess.attempts.at(-1);
    if (last && last.outcome !== 'clear' && last.outcome !== 'open') get(last.level).sessionEnds++;
  }
  // Player quit: the player's latest attempt overall is this level, uncleared, and stale.
  const lastByPlayer = new Map();
  for (const a of attempts) {
    const p = lastByPlayer.get(a.playerId);
    if (!p || a.t0 >= p.t0) lastByPlayer.set(a.playerId, a);
  }
  for (const a of lastByPlayer.values()) {
    if (a.outcome === 'quit' || a.outcome === 'leave') get(a.level).playerQuits++;
  }

  const rate = (a, b) => (b ? Math.round((a / b) * 1000) / 1000 : null);
  const per = (a, b) => (b ? Math.round((a / b) * 100) / 100 : null);
  return [...by.values()].sort((a, b) => a.level - b.level).map((s) => ({
    level: s.level,
    name: s.name || [...s.names][0] || '',
    namesSeen: [...s.names],
    attempts: s.attempts,
    players: s.players.size,
    clearPlayers: s.clearPlayers.size,
    clears: s.clears, leaves: s.leaves, quits: s.quits, open: s.open,
    clearRate: rate(s.clearPlayers.size, s.players.size),
    sessionEndRate: rate(s.sessionEnds, s.sessions.size),
    playerQuitRate: rate(s.playerQuits, s.players.size),
    playerQuits: s.playerQuits,
    clearMsMedian: quantile(s.clearMs, 0.5),
    clearMsP75: quantile(s.clearMs, 0.75),
    attemptMsMedian: quantile(s.allMs, 0.5),
    clearMovesMedian: quantile(s.clearMoves, 0.5),
    undos: s.undos, restarts: s.restarts, hints: s.hints, fails: s.fails, blocked: s.blocked,
    undosPerAttempt: per(s.undos, s.attempts), restartsPerAttempt: per(s.restarts, s.attempts),
    hintPlayerRate: rate(s.hintPlayers.size, s.players.size),
    idles: s.idles, idleMs: s.idleMs, wastedBodies: s.wasted, bodyLost: s.lost,
  }));
}

// Cell counts for one level. rows: events already filtered to that level.
// rows: one level, ordered by session and seq (store.query). Idle after level_clear (the clear screen) is not being stuck.
export function heatmap(rows) {
  const cells = {};
  const cleared = new Set(); // sessions whose current attempt on this level is cleared
  const bump = (x, y, k, n = 1) => {
    if (x == null || y == null) return;
    const c = (cells[`${x},${y}`] ??= { x, y });
    c[k] = (c[k] ?? 0) + n;
  };
  for (const r of rows) {
    const d = r.data;
    if (r.type === 'level_start') cleared.delete(r.session_id);
    else if (r.type === 'level_clear') cleared.add(r.session_id);
    if (r.type === 'idle') {
      if (cleared.has(r.session_id)) continue;
      bump(r.x, r.y, 'idle'); bump(r.x, r.y, 'idleMs', d.idleMs);
    }
    else if (r.type === 'undo') bump(r.x, r.y, 'undo');
    else if (r.type === 'restart') bump(r.x, r.y, 'restart');
    else if (r.type === 'hint') bump(r.x, r.y, 'hint');
    else if (r.type === 'body_lost') { bump(r.x, r.y, `lost_${d.reason}`); if (d.wasted) bump(r.x, r.y, 'wasted'); }
    else if (r.type === 'move' && d.result === 'blocked' && d.from) bump(d.from[0], d.from[1], 'blocked');
    else if (r.type === 'move' && d.result !== 'blocked') bump(r.x, r.y, 'visit');
    else if ((r.type === 'session_end' || r.type === 'level_leave') && d.cleared !== true) bump(r.x, r.y, 'left');
  }
  return Object.values(cells);
}

// Recent players with their sessions and per-attempt outcomes (session timeline view).
export function timelines(rows, { limit = 50, now = Date.now() } = {}) {
  const { sessions } = buildAttempts(rows, now);
  const players = new Map();
  for (const s of sessions) {
    const p = players.get(s.playerId) ?? { playerId: s.playerId, lastT: 0, sessions: [] };
    p.lastT = Math.max(p.lastT, s.lastT);
    p.sessions.push({
      sessionId: s.sessionId, client: s.client, build: s.build, t0: s.t0, lastT: s.lastT, endReason: s.endReason,
      attempts: s.attempts.map((a) => ({
        level: a.level, outcome: a.outcome, t0: a.t0, durationMs: a.durationMs, moves: a.moves,
        undos: a.undos, restarts: a.restarts, hints: a.hints, fails: a.fails, idleMs: a.idleMs, via: a.via,
      })),
    });
    players.set(s.playerId, p);
  }
  return [...players.values()]
    .sort((a, b) => b.lastT - a.lastT)
    .slice(0, limit)
    .map((p) => {
      p.sessions.sort((a, b) => a.t0 - b.t0);
      const all = p.sessions.flatMap((s) => s.attempts);
      const cleared = new Set(all.filter((a) => a.outcome === 'clear').map((a) => a.level));
      const last = all.at(-1);
      return { ...p, cleared: [...cleared].sort((a, b) => a - b), lastLevel: last?.level ?? null, lastOutcome: last?.outcome ?? null };
    });
}

const CSV_ESC = (v) => {
  const s = v == null ? '' : typeof v === 'object' ? JSON.stringify(v) : String(v);
  return /[",\n]/.test(s) ? `"${s.replace(/"/g, '""')}"` : s;
};

export function toCsv(objs, cols = objs.length ? Object.keys(objs[0]) : []) {
  return [cols.join(','), ...objs.map((o) => cols.map((c) => CSV_ESC(o[c])).join(','))].join('\n') + '\n';
}

// One row per attempt; columns follow design/levels/telemetry-mapping.md section 4.1 (feat/levels).
export const ATTEMPT_COLUMNS = [
  'attemptId', 'playerId', 'sessionId', 'build', 'client', 'level', 'levelName', 'levelKey', 'attempt', 'via', 'bodies',
  'sessionFirstTime', 'startT', 'endT', 'outcome', 'durationMs', 'activeMs', 'moves', 'blocked', 'undos', 'restarts',
  'hints', 'fails', 'bodiesUsed', 'minMoves', 'idleCount', 'idleMsTotal', 'idleMsMax', 'firstHintMs', 'bodiesWasted',
  'lostFell', 'lostVanish', 'lostStatue', 'lostSwitch', 'lostBridge', 'lostSeal', 'keeps', 'switches',
  'lastStuckToClearMoves', 'lastStuckToClearMs', 'nextLevelStarted',
];

export function attemptRows(rows, now = Date.now(), minMovesFor) {
  return buildAttempts(rows, now, minMovesFor).attempts.map((a) => ({
    attemptId: a.id, playerId: a.playerId, sessionId: a.sessionId, build: a.build, client: a.client,
    level: a.level, levelName: a.levelName, levelKey: a.levelKey, attempt: a.attempt, via: a.via, bodies: a.bodies,
    sessionFirstTime: a.sessionFirstTime, startT: a.t0, endT: a.tEnd, outcome: a.outcome,
    durationMs: a.durationMs, activeMs: Math.max(0, a.durationMs - a.idleMs), moves: a.moves, blocked: a.blocked,
    undos: a.undos, restarts: a.restarts, hints: a.hints, fails: a.fails, bodiesUsed: a.bodiesUsed, minMoves: a.minMoves,
    idleCount: a.idles, idleMsTotal: a.idleMs, idleMsMax: a.idleMsMax, firstHintMs: a.firstHintMs, bodiesWasted: a.wasted,
    lostFell: a.lost.fell ?? 0, lostVanish: a.lost.vanish ?? 0, lostStatue: a.lost.statue ?? 0, lostSwitch: a.lost.switch ?? 0,
    lostBridge: a.lost.bridge ?? 0, lostSeal: a.lost.seal ?? 0, keeps: a.keeps, switches: a.switches,
    lastStuckToClearMoves: a.lastStuckToClearMoves, lastStuckToClearMs: a.lastStuckToClearMs, nextLevelStarted: a.nextLevelStarted,
  }));
}

// Raw events, one JSON object per line, with the envelope fields and attemptId (null outside a stage).
export function eventsJsonl(rows, now = Date.now()) {
  buildAttempts(rows, now);
  return rows.map((r) => JSON.stringify({
    playerId: r.player_id, sessionId: r.session_id, client: r.client, build: r.build, seq: r.seq, t: r.t,
    receivedAt: r.received_at, type: r.type, attemptId: r.attemptId, ...r.data,
  })).join('\n') + (rows.length ? '\n' : '');
}
