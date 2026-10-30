// Modak generator (approved v3): one continuous dough silhouette, pleats twist into the tip.
// Run `node scripts/modak-gen.js` to rebuild src/modak.generated.js after changing the design here.
const fs = require('fs');
const path = require('path');
const OUT = __dirname;

const C = {
  ink: '#3b2216', blush: '#ff8a8a', heart: '#ff4d6d', steam: '#fff6e8', zz: '#b8c4ff',
  ask: '#ffcf6e', work: '#ff9f43', done: '#4ade80', pill: '#0a0a0b', tile: '#141416',
  core: '#fffdf8', light: '#fdf4e3', mid: '#f5e4c4', edge: '#dcb984', shadow: '#6a4018',
  sss: '#f3a25a', saffron: '#e8913a', groove: '#8a5626', flank: '#a46a30',
};

// Right half outline from the tip down, as cubic segments. The left half is its mirror.
const TOP = [32.5, 2.6];
const SEGS = [
  [[33, 4], [34.2, 9.6], [36.4, 13.4]],
  [[39, 17.8], [42.4, 20.8], [46.5, 24.8]],
  [[53.5, 31.6], [58, 39], [57, 46.5]],
  [[56, 54.5], [46, 58.5], [32, 58.5]],
];
const m = ([x, y]) => `${+(64 - x).toFixed(2)} ${y}`;
const BODY = (() => {
  let d = `M31.5 2.6 Q32 1.2 32.5 2.6`;
  SEGS.forEach(s => { d += ` C${s.map(p => p.join(' ')).join(' ')}`; });
  const pts = [TOP, ...SEGS.flat()];
  // walk back up the left side: reverse each segment and mirror it
  for (let i = SEGS.length - 1; i >= 0; i--) {
    const start = i === 0 ? TOP : SEGS[i - 1][2];
    const [c1, c2] = SEGS[i];
    d += ` C${m(c2)} ${m(c1)} ${m(start)}`;
  }
  return d + ' Z';
})();

// sample the right outline (tip to widest belly) as (radius, y)
const bez = (p0, p1, p2, p3, t) => { const u = 1 - t; return [0, 1].map(k => u * u * u * p0[k] + 3 * u * u * t * p1[k] + 3 * u * t * t * p2[k] + t * t * t * p3[k]); };
const PROFILE = (() => {
  const out = [[0, 1.7]];
  let p0 = TOP;
  SEGS.slice(0, 3).forEach(([p1, p2, p3]) => {
    for (let i = 0; i <= 12; i++) { if (i === 0 && out.length > 1) continue; const [x, y] = bez(p0, p1, p2, p3, i / 12); out.push([x - 32, y]); }
    p0 = p3;
  });
  return out;
})();

// 11 pleats around. Each groove is a meridian whose angle twists as it rises (tau grows toward the tip),
// so the pleats themselves wind into the pinched point. Hidden where it turns to the back.
const TAU = 1.35, Y0 = 26;
const twist = (y) => TAU * Math.pow(Math.min(1, Math.max(0, (Y0 - y) / (Y0 - 2))), 1.6);
const THETAS = Array.from({ length: 11 }, (_, k) => -Math.PI + (k + .5) * 2 * Math.PI / 11);
const grooveRuns = THETAS.map(th => {
  const runs = []; let cur = [];
  PROFILE.forEach(([r, y]) => {
    const a = th + twist(y);
    if (Math.cos(a) > .08) cur.push({ x: 32 + r * Math.sin(a), y, r, c: Math.cos(a) });
    else if (cur.length) { runs.push(cur); cur = []; }
  });
  if (cur.length) runs.push(cur);
  return runs.filter(r => r.length > 2);
}).flat();

// Catmull Rom to cubic, chunked so stroke weight can thin out where the dough narrows
const f2 = (n) => +n.toFixed(2);
const chunks = (run) => {
  const out = [];
  for (let i = 0; i < run.length - 1; i += 3) {
    let d = `M${f2(run[i].x)} ${f2(run[i].y)}`;
    const end = Math.min(i + 3, run.length - 1);
    for (let j = i; j < end; j++) {
      const p0 = run[j - 1] || run[j], p1 = run[j], p2 = run[j + 1], p3 = run[j + 2] || p2;
      d += ` C${f2(p1.x + (p2.x - p0.x) / 6)} ${f2(p1.y + (p2.y - p0.y) / 6)} ${f2(p2.x - (p3.x - p1.x) / 6)} ${f2(p2.y - (p3.y - p1.y) / 6)} ${f2(p2.x)} ${f2(p2.y)}`;
    }
    const mid = run[Math.min(i + 1, end)];
    out.push({ d, k: Math.min(1, Math.max(.3, mid.r / 9)), c: mid.c, side: (mid.x - 32) / Math.max(mid.r, 1) });
  }
  return out;
};
const allChunks = grooveRuns.map(chunks).flat();
const pleatsFull = allChunks.map(({ d, k, c, side }) =>
  `<path d="${d}" transform="translate(${f2(-1 * k)} 0)" stroke="${C.flank}" stroke-opacity="${f2((.12 + .07 * side) * c)}" stroke-width="${f2(3 * k)}" filter="url(#m3-blur2)"/>`
  + `<path d="${d}" stroke="${C.groove}" stroke-opacity="${f2((.55 + .15 * side) * (.5 + .5 * c))}" stroke-width="${f2(1.1 * k)}" filter="url(#m3-blur1)"/>`
  + `<path d="${d}" transform="translate(${f2(1.15 * k)} 0)" stroke="#fffdf8" stroke-opacity="${f2((.9 - .35 * side) * c)}" stroke-width="${f2(1.6 * k)}" filter="url(#m3-blur1)"/>`).join('');
const pleatsSmall = allChunks.filter(ch => ch.c > .3).map(({ d, k }) => `<path d="${d}" stroke="${C.groove}" stroke-opacity=".45" stroke-width="${f2(1.4 * k)}"/>`).join('');

const volume = `<path d="${BODY}" fill="url(#m3-core)"/>
    <path d="${BODY}" fill="url(#m3-top)"/>
    <path d="${BODY}" fill="url(#m3-shade)"/>
    <path d="${BODY}" fill="url(#m3-sss)"/>
    <path d="${BODY}" fill="url(#m3-saffron)"/>`;
// rim darkening follows the silhouette, so the pulled up neck gets the same edge as the belly
const rim = (w, o, blur) => `<path d="${BODY}" fill="none" stroke="#6b4420" stroke-opacity="${o}" stroke-width="${w}"${blur ? ' filter="url(#m3-blur2)"' : ''}/>`;

const DEFS = `<defs>
  <radialGradient id="m3-core" gradientUnits="userSpaceOnUse" cx="26" cy="27" r="36">
    <stop offset="0" stop-color="${C.core}"/><stop offset=".4" stop-color="${C.light}"/><stop offset=".72" stop-color="${C.mid}"/><stop offset="1" stop-color="${C.edge}"/>
  </radialGradient>
  <linearGradient id="m3-top" gradientUnits="userSpaceOnUse" x1="0" y1="0" x2="0" y2="30">
    <stop offset="0" stop-color="#fffaf0" stop-opacity=".45"/><stop offset="1" stop-color="#fffaf0" stop-opacity="0"/>
  </linearGradient>
  <linearGradient id="m3-shade" gradientUnits="userSpaceOnUse" x1="22" y1="22" x2="54" y2="60">
    <stop offset=".45" stop-color="${C.shadow}" stop-opacity="0"/><stop offset="1" stop-color="${C.shadow}" stop-opacity=".3"/>
  </linearGradient>
  <radialGradient id="m3-sss" gradientUnits="userSpaceOnUse" cx="36" cy="60" r="26">
    <stop offset="0" stop-color="${C.sss}" stop-opacity=".4"/><stop offset="1" stop-color="${C.sss}" stop-opacity="0"/>
  </radialGradient>
  <radialGradient id="m3-saffron" gradientUnits="userSpaceOnUse" cx="32" cy="1.5" r="12">
    <stop offset="0" stop-color="${C.saffron}" stop-opacity=".75"/><stop offset=".55" stop-color="${C.saffron}" stop-opacity=".18"/><stop offset="1" stop-color="${C.saffron}" stop-opacity="0"/>
  </radialGradient>
  <linearGradient id="m3-fade" gradientUnits="userSpaceOnUse" x1="0" y1="20" x2="0" y2="47">
    <stop offset="0" stop-color="#fff"/><stop offset=".45" stop-color="#fff" stop-opacity=".75"/><stop offset="1" stop-color="#fff" stop-opacity="0"/>
  </linearGradient>
  <mask id="m3-pleat-mask" maskUnits="userSpaceOnUse" x="0" y="0" width="64" height="64"><rect width="64" height="64" fill="url(#m3-fade)"/></mask>
  <!-- tip animation: the body is drawn twice. The base copy is cut at y 10, the tip copy is opaque above y 10.5
       and feathers out by y 18, so at rest the two copies are pixel identical and there is no join. -->
  <mask id="m3-base-mask" maskUnits="userSpaceOnUse" x="-20" y="-20" width="104" height="104"><rect x="-20" y="10" width="104" height="90" fill="#fff"/></mask>
  <linearGradient id="m3-tipfade" gradientUnits="userSpaceOnUse" x1="0" y1="10.5" x2="0" y2="18">
    <stop offset="0" stop-color="#fff"/><stop offset="1" stop-color="#fff" stop-opacity="0"/>
  </linearGradient>
  <mask id="m3-tip-mask" maskUnits="userSpaceOnUse" x="-20" y="-20" width="104" height="104"><rect x="-20" y="-20" width="104" height="104" fill="url(#m3-tipfade)"/></mask>
  <radialGradient id="m3-eye" cx=".4" cy=".7" r=".7">
    <stop offset="0" stop-color="#6e4630"/><stop offset="1" stop-color="#1a0f09"/>
  </radialGradient>
  <radialGradient id="m3-blush">
    <stop offset="0" stop-color="${C.blush}" stop-opacity="1"/><stop offset=".6" stop-color="${C.blush}" stop-opacity=".45"/><stop offset="1" stop-color="${C.blush}" stop-opacity="0"/>
  </radialGradient>
  <clipPath id="m3-clip"><path d="${BODY}"/></clipPath>
  <filter id="m3-blur1" filterUnits="userSpaceOnUse" x="-10" y="-10" width="84" height="84"><feGaussianBlur stdDeviation=".35"/></filter>
  <filter id="m3-blur2" filterUnits="userSpaceOnUse" x="-10" y="-10" width="84" height="84"><feGaussianBlur stdDeviation="1"/></filter>
  <filter id="m3-blur3" x="-50%" y="-50%" width="200%" height="200%"><feGaussianBlur stdDeviation="1.4"/></filter>
  <filter id="m3-grain" x="0" y="0" width="100%" height="100%">
    <feTurbulence type="fractalNoise" baseFrequency="1.3" numOctaves="2" seed="7"/>
    <feColorMatrix values="0 0 0 0 .55  0 0 0 0 .38  0 0 0 0 .2  1.8 0 0 0 -.95"/>
    <feComposite in2="SourceGraphic" operator="in"/>
  </filter>
  <filter id="m3-emboss" x="-30%" y="-30%" width="160%" height="160%">
    <feGaussianBlur in="SourceAlpha" stdDeviation=".7" result="a"/>
    <feFlood flood-color="#7a4e24" flood-opacity=".32"/><feComposite in2="a" operator="in" result="ao"/>
    <feOffset in="SourceAlpha" dy=".75" result="o"/><feGaussianBlur in="o" stdDeviation=".3" result="ob"/>
    <feFlood flood-color="#fff" flood-opacity=".6"/><feComposite in2="ob" operator="in" result="lip"/>
    <feMerge><feMergeNode in="ao"/><feMergeNode in="lip"/><feMergeNode in="SourceGraphic"/></feMerge>
  </filter>
  <symbol id="m3-body" viewBox="0 0 64 64">
    ${volume}
    <g clip-path="url(#m3-clip)">
      ${rim(3.2, .32, true)}
      <g fill="none" stroke-linecap="round" mask="url(#m3-pleat-mask)">${pleatsFull}</g>
      <rect width="64" height="64" fill="#fff" filter="url(#m3-grain)" opacity=".09"/>
      <ellipse cx="21" cy="31" rx="3.2" ry="6.4" transform="rotate(38 21 31)" fill="#fff" opacity=".8" filter="url(#m3-blur2)"/>
      <ellipse cx="19.4" cy="28.6" rx=".9" ry="1.4" transform="rotate(38 19.4 28.6)" fill="#fff" opacity=".9"/>
      <ellipse cx="31.2" cy="8.5" rx=".5" ry="2.6" transform="rotate(16 31.2 8.5)" fill="#fff" opacity=".55" filter="url(#m3-blur1)"/>
    </g>
  </symbol>
  <symbol id="m3-body-s" viewBox="0 0 64 64">
    ${volume}
    <g clip-path="url(#m3-clip)">
      ${rim(2.4, .3, false)}
      <g fill="none" stroke-linecap="round" mask="url(#m3-pleat-mask)">${pleatsSmall}</g>
      <ellipse cx="21" cy="30" rx="2.4" ry="4.8" transform="rotate(38 21 30)" fill="#fff" opacity=".7"/>
    </g>
  </symbol>
</defs>`;

// ---------- faces (ink part gets the emboss filter, blush is separate) ----------
const I = C.ink;
const eye = (cx) => `<g class="eye"><ellipse cx="${cx}" cy="45" rx="2.7" ry="3.2" fill="url(#m3-eye)"/><ellipse cx="${cx + .95}" cy="43.7" rx="1" ry="1.1" fill="#fff"/><circle cx="${cx - .9}" cy="46.6" r=".45" fill="#fff" opacity=".75"/></g>`;
const bigEye = (cx) => `<g class="eye"><circle cx="${cx}" cy="45" r="3.7" fill="url(#m3-eye)"/><circle cx="${cx + 1.3}" cy="43.6" r="1.35" fill="#fff"/><circle cx="${cx - 1.1}" cy="46.4" r=".6" fill="#fff" opacity=".8"/></g>`;
const st = (w = 1.4) => `fill="none" stroke="${I}" stroke-width="${w}" stroke-linecap="round" stroke-linejoin="round"`;
const SPARK = 'M0 -3 Q.5 -.5 3 0 Q.5 .5 0 3 Q-.5 .5 -3 0 Q-.5 -.5 0 -3 Z';
const HEART = 'M0 2.6 C-3.4 .4 -2.6 -2.4 -1.1 -2.4 C-.4 -2.4 0 -1.9 0 -1.5 C0 -1.9 .4 -2.4 1.1 -2.4 C2.6 -2.4 3.4 .4 0 2.6 Z';
const spark = (x, y, cls = '') => `<g transform="translate(${x} ${y})"><path class="spark ${cls}" d="${SPARK}" fill="#fff4c2"/></g>`;
const spiral = (cx) => `<path class="spiral" d="M${cx} 45 a.7 .7 0 0 1 1.4 0 a1.4 1.4 0 0 1 -2.8 0 a2.1 2.1 0 0 1 4.2 0 a2.8 2.8 0 0 1 -5.6 0" ${st(1.2)}/>`;
const tongue = (y) => `<path d="M30.4 ${y} Q32 ${y - 1.3} 33.6 ${y} Q32 ${y + 1.6} 30.4 ${y} Z" fill="#ff7f8f"/>`;
const heartEye = (cx) => `<g transform="translate(${cx} 45) scale(1.4)"><path class="heart" d="${HEART}" fill="${C.heart}"/><ellipse cx="-1.1" cy="-1.2" rx=".55" ry=".4" fill="#fff" opacity=".85"/></g>`;

const MOODS = {
  idle: { ring: null, blush: .55, face: `${eye(24.5)}${eye(39.5)}<path d="M29.6 49.3 Q32 51.7 34.4 49.3" ${st(1.3)}/>`, fx: '' },
  working: {
    ring: C.work, blush: .4,
    face: `<g class="scan"><circle cx="24.5" cy="45.4" r="2.6" fill="url(#m3-eye)"/><circle cx="25.4" cy="44.4" r=".85" fill="#fff"/><circle cx="39.5" cy="45.4" r="2.6" fill="url(#m3-eye)"/><circle cx="40.4" cy="44.4" r=".85" fill="#fff"/></g>`
      + `<path d="M20.6 40.4 L27.2 42 M43.4 40.4 L36.8 42" ${st(1.5)}/><path d="M30 50.4 Q32 49.6 34 50.4" ${st(1.3)}/>`,
    fx: `<circle class="puff p1" cx="35" cy="4.5" r="1.7" fill="${C.steam}" filter="url(#m3-blur1)"/><circle class="puff p2" cx="34" cy="5" r="1.3" fill="${C.steam}" filter="url(#m3-blur1)"/><circle class="puff p3" cx="36" cy="5.5" r="1.1" fill="${C.steam}" filter="url(#m3-blur1)"/>`,
  },
  asking: {
    ring: C.ask, blush: .55,
    face: `<path d="M21.4 39 Q24.5 37.2 27.6 38.8 M36.4 38.8 Q39.5 37.2 42.6 39" ${st(1.3)}/>${bigEye(24.5)}${bigEye(39.5)}<ellipse cx="32" cy="50.8" rx="1.3" ry="1.6" fill="${I}"/>`,
    fx: `<g class="q"><path d="M49.5 12.4 C49.5 9.6 51.4 8 53.5 8 C55.7 8 57.4 9.5 57.4 11.6 C57.4 13.5 55.9 14.3 54.7 15.1 C53.9 15.7 53.5 16.5 53.5 18" fill="none" stroke="${C.ask}" stroke-width="2" stroke-linecap="round"/><circle cx="53.5" cy="21.4" r="1.25" fill="${C.ask}"/></g>`,
  },
  done: {
    ring: C.done, blush: .7,
    face: `<path d="M21.5 46 Q24.5 42 27.5 46 M36.5 46 Q39.5 42 42.5 46" ${st(1.6)}/><path d="M28.6 48.6 Q32 54.6 35.4 48.6 Z" fill="${I}" stroke="${I}" stroke-width=".6" stroke-linejoin="round"/>${tongue(51.9)}`,
    fx: `${spark(11, 24, 's1')}${spark(53, 21, 's2')}`,
  },
  love: {
    ring: C.work, blush: .8,
    face: `${heartEye(24.5)}${heartEye(39.5)}<path d="M29.4 49.6 Q32 52.6 34.6 49.6" ${st(1.3)}/>`,
    fx: `<g transform="translate(51 20)"><path class="float-heart" d="${HEART}" fill="${C.heart}"/></g>`,
  },
  wink: {
    ring: null, blush: .6,
    face: `${eye(24.5)}<path d="M36.6 45.8 Q39.5 42.6 42.4 45.8" ${st(1.6)}/><path d="M29 49.2 Q32 53.4 35 49.2" ${st(1.3)}/>${tongue(51.6)}`,
    fx: spark(47.5, 39, 's1'),
  },
  dizzy: {
    ring: null, blush: .4,
    face: `${spiral(24.5)}${spiral(39.5)}<path d="M27.6 51 q1.1 -1.3 2.2 0 t2.2 0 t2.2 0 t2.2 0" ${st(1.2)}/>`,
    fx: `<g transform="translate(32 7) scale(1 .35)"><g class="orbit"><circle cx="13" cy="0" r="1.6" fill="#fff4c2"/><circle cx="-6.5" cy="11.3" r="1.3" fill="#fff4c2"/><circle cx="-6.5" cy="-11.3" r="1.1" fill="#fff4c2"/></g></g>`,
  },
  sleepy: {
    ring: null, blush: .5,
    face: `<path d="M21.8 45 Q24.5 47.4 27.2 45 M36.8 45 Q39.5 47.4 42.2 45" ${st(1.5)}/><ellipse cx="32" cy="50.8" rx="1" ry="1.2" fill="${I}"/>`,
    fx: `<g transform="translate(46 15)"><path class="z z1" d="M-1.8 -2 H1.8 L-1.8 2 H1.8" fill="none" stroke="${C.zz}" stroke-width="1.2" stroke-linecap="round" stroke-linejoin="round"/></g>`
      + `<g transform="translate(46 15) scale(.7)"><path class="z z2" d="M-1.8 -2 H1.8 L-1.8 2 H1.8" fill="none" stroke="${C.zz}" stroke-width="1.4" stroke-linecap="round" stroke-linejoin="round"/></g>`,
  },
};
const NAMES = Object.keys(MOODS);
const blush = (o) => `<g opacity="${o}"><ellipse cx="18.4" cy="49.8" rx="4.4" ry="2.8" fill="url(#m3-blush)"/><ellipse cx="45.6" cy="49.8" rx="4.4" ry="2.8" fill="url(#m3-blush)"/></g>`;

// ---------- CSS ----------
const BASE_CSS = `.modak{overflow:visible;display:block}
.modak .rig{transform-box:view-box;transform-origin:32px 58px}
.modak .tip{transform-box:view-box;transform-origin:32px 18px}
.modak .eye,.modak .spiral,.modak .heart,.modak .float-heart,.modak .spark,.modak .z,.modak .puff{transform-box:fill-box;transform-origin:center}
.modak .orbit{transform-origin:0 0}
@media (prefers-reduced-motion:reduce){.modak *{animation:none!important}}`;

const KEYFRAMES = `@keyframes mk-blink{0%,90%,100%{transform:scaleY(1)}94%{transform:scaleY(.1)}}
@keyframes mk-breathe{0%,100%{transform:scale(1,1)}50%{transform:scale(1.015,.985)}}
@keyframes mk-scan{0%,12%{transform:translateX(-1.6px)}38%,62%{transform:translateX(1.6px)}88%,100%{transform:translateX(-1.6px)}}
@keyframes mk-quiver{from{transform:rotate(-2.5deg)}to{transform:rotate(2.5deg)}}
@keyframes mk-puff{0%{transform:translate(0,0) scale(.4);opacity:0}20%{opacity:.9}100%{transform:translate(4px,-9px) scale(1.4);opacity:0}}
@keyframes mk-tilt{0%,100%{transform:rotate(0)}25%,75%{transform:rotate(-9deg)}}
@keyframes mk-bob{from{transform:translateY(0)}to{transform:translateY(-2px)}}
@keyframes mk-hop{0%{transform:translateY(0) scale(1.05,.95)}30%{transform:translateY(-5px) scale(.97,1.04)}60%{transform:translateY(0) scale(1.06,.94)}75%,100%{transform:translateY(0) scale(1,1)}}
@keyframes mk-wiggle{from{transform:rotate(-6deg)}to{transform:rotate(6deg)}}
@keyframes mk-twinkle{0%,100%{opacity:0;transform:scale(.4)}50%{opacity:1;transform:scale(1)}}
@keyframes mk-pulse{0%,100%{transform:scale(1)}50%{transform:scale(1.22)}}
@keyframes mk-rise{0%{transform:translateY(0) scale(.6);opacity:0}20%{opacity:1}100%{transform:translateY(-10px) scale(1);opacity:0}}
@keyframes mk-spin{to{transform:rotate(360deg)}}
@keyframes mk-wobble{0%,100%{transform:rotate(-6deg)}50%{transform:rotate(6deg)}}
@keyframes mk-droop{0%,100%{transform:rotate(7deg)}50%{transform:rotate(10deg)}}
@keyframes mk-z{0%{transform:translate(0,0) scale(.5);opacity:0}20%{opacity:1}100%{transform:translate(6px,-12px) scale(1.1);opacity:0}}
@keyframes mk-nudge{0%,30%,100%{transform:translateY(0) rotate(0)}10%{transform:translateY(-2px) rotate(-4deg)}20%{transform:translateY(0) rotate(2deg)}}`;

// per mood rules; S is the mood scope selector
const RULES = {
  idle: S => `${S} .eye{animation:mk-blink 4s infinite}
${S} .rig{animation:mk-breathe 3.6s ease-in-out infinite}`,
  working: S => `${S} .scan{animation:mk-scan 2.2s ease-in-out infinite}
${S} .tip{animation:mk-quiver .35s ease-in-out infinite alternate}
${S} .puff{animation:mk-puff 1.8s ease-out infinite}
${S} .p2{animation-delay:.6s}
${S} .p3{animation-delay:1.2s}`,
  asking: S => `${S} .rig{animation:mk-tilt 3s ease-in-out infinite}
${S} .q{animation:mk-bob .9s ease-in-out infinite alternate}
${S} .eye{animation:mk-blink 5s infinite}`,
  done: S => `${S} .rig{animation:mk-hop 1s cubic-bezier(.3,.7,.4,1) infinite}
${S} .tip{animation:mk-wiggle .25s ease-in-out infinite alternate}
${S} .spark{animation:mk-twinkle 1.2s ease-in-out infinite}
${S} .s2{animation-delay:.6s}`,
  love: S => `${S} .heart{animation:mk-pulse .8s ease-in-out infinite}
${S} .float-heart{animation:mk-rise 2s ease-out infinite}
${S} .tip{animation:mk-wiggle .6s ease-in-out infinite alternate}`,
  wink: S => `${S} .rig{animation:mk-nudge 2s ease-in-out infinite}
${S} .spark{animation:mk-twinkle 2s ease-in-out infinite}`,
  dizzy: S => `${S} .spiral{animation:mk-spin 1s linear infinite}
${S} .rig{animation:mk-wobble 1.2s ease-in-out infinite}
${S} .orbit{animation:mk-spin 1.6s linear infinite}`,
  sleepy: S => `${S} .tip{animation:mk-droop 4s ease-in-out infinite}
${S} .rig{animation:mk-breathe 4s ease-in-out infinite}
${S} .z{animation:mk-z 3s ease-out infinite}
${S} .z2{animation-delay:1.5s}`,
};
const moodCss = (alias) => NAMES.map(n => {
  const lines = RULES[n]('§').split('\n');
  return lines.map(l => {
    const sel = l.slice(0, l.indexOf('{'));
    const body = l.slice(l.indexOf('{'));
    const sels = [sel.replace('§', `.m-${n}`)];
    if (alias) sels.push(sel.replace('§', alias(n)));
    return sels.join(',') + body;
  }).join('\n');
}).join('\n');


// ---------- SVG ----------
const LOD_PX = 40; // below this, use the simplified body and tip, no grain, no face emboss
const ground = '<ellipse cx="32" cy="60.2" rx="20" ry="2.6" fill="#000" opacity=".5" filter="url(#m3-blur3)"/><ellipse cx="32" cy="58.9" rx="14" ry="1.1" fill="#000" opacity=".55" filter="url(#m3-blur1)"/>';
const faceG = (n, small) => `<g class="face">${blush(MOODS[n].blush)}<g${small ? '' : ' filter="url(#m3-emboss)"'}>${MOODS[n].face}</g></g><g class="fx">${MOODS[n].fx}</g>`;
const shell = (cls, size, inner, small, shadow) => `<svg class="modak ${cls}" viewBox="0 0 64 64" width="${size}" height="${size}" role="img" aria-label="Modak">${shadow ? ground : ''}<g class="rig">${bodyLayers(small)}${inner}</g></svg>`;
const bodyLayers = (small) => { const b = `<use href="#m3-body${small ? '-s' : ''}"/>`; return `<g mask="url(#m3-base-mask)">${b}</g><g class="tip"><g mask="url(#m3-tip-mask)">${b}</g></g>`; };
const svg = (n, size, shadow = size >= LOD_PX) => { const small = size < LOD_PX; return shell(`m-${n}`, size, faceG(n, small), small, shadow); };
const heroSvg = shell('hero-svg', 160, NAMES.map(n => `<g class="mood mood-${n}">${faceG(n, false)}</g>`).join(''), false, true);

// ---------- emit the app module ----------
// Markup keeps a __S__ size placeholder; the component swaps in the pixel size.
const sized = (s) => s.replace(/width="\d+" height="\d+"/, 'width="__S__" height="__S__"')
const markup = Object.fromEntries(NAMES.map((n) => [n, { full: sized(svg(n, 64, true)), small: sized(svg(n, 26, false)) }]))
const mod = `// Generated by scripts/modak-gen.js. Do not edit by hand.
export const LOD_PX = ${LOD_PX}
export const MODAK_DEFS = ${JSON.stringify(`<svg width="0" height="0" style="position:absolute" aria-hidden="true">${DEFS}</svg>`)}
export const MODAK_CSS = ${JSON.stringify(BASE_CSS + '\n' + KEYFRAMES + '\n' + moodCss())}
export const MODAK = ${JSON.stringify(markup, null, 1)}
`
if (/[\u2013\u2014]/.test(mod)) throw new Error('dash in output')
fs.writeFileSync(path.join(__dirname, '..', 'src', 'modak.generated.js'), mod)
console.log('wrote src/modak.generated.js', mod.length, 'bytes')
