// EcoQuest UI icon set. Hand-authored SVG on a 64-unit grid, rendered to PNG for Unity.
// Style: flat fills from the game palette, one dark ink outline (3 units, round joins) and small white
// highlights. Run: node icons.js <outDir>   (needs @resvg/resvg-js)
const fs = require('fs');
const path = require('path');
const { Resvg } = require('@resvg/resvg-js');

const INK = '#13262b';
const C = {
  green: '#4fd18b', greenD: '#2e9e63', leaf: '#86d35c', leafD: '#4f9e3a',
  amber: '#ffc94f', orange: '#f59a3c', blue: '#53bdf2', blueD: '#2d7fc1', sky: '#bfe8ff',
  coral: '#f0655a', red: '#d8443b', grey: '#b3c0c4', steel: '#7d9197', dark: '#3b4f55',
  brown: '#a8743f', brownD: '#6e4a2a', white: '#f6faf8', snow: '#e8f6ff', mud: '#8c8f4a',
  purple: '#a98bf0', pink: '#f79ac0', sand: '#e8cf8c',
};
const S = `stroke="${INK}" stroke-width="3" stroke-linejoin="round" stroke-linecap="round"`;
const HL = `fill="none" stroke="${C.white}" stroke-width="2.6" stroke-linecap="round" opacity="0.85"`;
// Attributes in `extra` replace the default outline attributes of the same name.
const attrs = extra => {
  const base = { stroke: INK, 'stroke-width': '3', 'stroke-linejoin': 'round', 'stroke-linecap': 'round' };
  for (const m of extra.matchAll(/([\w-]+)="([^"]*)"/g)) base[m[1]] = m[2];
  return Object.entries(base).map(([k, v]) => `${k}="${v}"`).join(' ');
};
const p = (d, fill, extra = '') => `<path d="${d}" fill="${fill}" ${attrs(extra)}/>`;
const circ = (x, y, r, fill, extra = '') => `<circle cx="${x}" cy="${y}" r="${r}" fill="${fill}" ${attrs(extra)}/>`;
const rect = (x, y, w, h, r, fill, extra = '') => `<rect x="${x}" y="${y}" width="${w}" height="${h}" rx="${r}" fill="${fill}" ${attrs(extra)}/>`;
const line = (d, w = 3, color = INK) => `<path d="${d}" fill="none" stroke="${color}" stroke-width="${w}" stroke-linecap="round" stroke-linejoin="round"/>`;
const hl = d => `<path d="${d}" ${HL}/>`;

const bolt = (dx = 0, dy = 0, s = 1, fill = C.amber) =>
  `<g transform="translate(${dx} ${dy}) scale(${s})">${p('M35 6 L17 34 H29 L25 58 L47 26 H34 Z', fill)}</g>`;
const drop = (fill, dx = 0, dy = 0, s = 1) =>
  `<g transform="translate(${dx} ${dy}) scale(${s})">${p('M32 6 C32 6 14 28 14 40 A18 18 0 0 0 50 40 C50 28 32 6 32 6 Z', fill)}${hl('M22 40 A10 10 0 0 0 28 49')}</g>`;
const cloud = (fill, dx = 0, dy = 0, s = 1) =>
  `<g transform="translate(${dx} ${dy}) scale(${s})">${p('M18 46 A10 10 0 0 1 17 26 A13 13 0 0 1 41 20 A11 11 0 0 1 48 46 Z', fill)}${hl('M20 33 A6 6 0 0 1 26 28')}</g>`;
const sunRays = (cx, cy, r1, r2, n = 8) => {
  let d = '';
  for (let i = 0; i < n; i++) {
    const a = (i / n) * Math.PI * 2;
    d += `M${(cx + Math.cos(a) * r1).toFixed(1)} ${(cy + Math.sin(a) * r1).toFixed(1)} L${(cx + Math.cos(a) * r2).toFixed(1)} ${(cy + Math.sin(a) * r2).toFixed(1)} `;
  }
  return line(d, 4, INK) + line(d, 1.6, C.amber);
};
const leafShape = (fill, dx = 0, dy = 0, s = 1, rot = 0) =>
  `<g transform="translate(${dx} ${dy}) rotate(${rot} 32 32) scale(${s})">${p('M12 52 C10 30 24 12 52 10 C54 36 38 54 12 52 Z', fill)}${line('M14 50 C24 40 34 30 44 20', 2.4)}</g>`;
const turbine = (dx = 0, dy = 0, s = 1) => `<g transform="translate(${dx} ${dy}) scale(${s})">
  ${p('M28 30 L26 58 H38 L36 30 Z', C.white)}
  ${p('M32 28 L28 6 C29 2 35 2 36 6 Z', C.white)}${p('M34 31 L54 40 C57 43 54 47 50 46 Z', C.white)}${p('M30 31 L10 40 C7 43 10 47 14 46 Z', C.white)}
  ${circ(32, 29, 5, C.grey)}</g>`;
// Three chunky arrows chasing each other around a circle (recycling).
const cycleArrows = (fill, cx = 32, cy = 33, r = 19) => {
  let out = '';
  for (let k = 0; k < 3; k++) {
    const a0 = (-90 + k * 120 + 14) * Math.PI / 180, a1 = (-90 + k * 120 + 92) * Math.PI / 180;
    const P = a => [(cx + Math.cos(a) * r).toFixed(2), (cy + Math.sin(a) * r).toFixed(2)];
    const [x0, y0] = P(a0), [x1, y1] = P(a1);
    const arc = `M${x0} ${y0} A${r} ${r} 0 0 1 ${x1} ${y1}`;
    out += line(arc, 11) + line(arc, 5.5, fill);
    // Arrow head at the end, pointing along the tangent.
    const t = a1 + Math.PI / 2, n = a1;
    const tip = [cx + Math.cos(a1 + 0.2) * r, cy + Math.sin(a1 + 0.2) * r];
    const b1 = [cx + Math.cos(a1 - 0.08) * (r + 8), cy + Math.sin(a1 - 0.08) * (r + 8)];
    const b2 = [cx + Math.cos(a1 - 0.08) * (r - 8), cy + Math.sin(a1 - 0.08) * (r - 8)];
    out += p(`M${tip[0].toFixed(2)} ${tip[1].toFixed(2)} L${b1[0].toFixed(2)} ${b1[1].toFixed(2)} L${b2[0].toFixed(2)} ${b2[1].toFixed(2)} Z`, fill);
    void t; void n;
  }
  return out;
};
const house = (fill, roof, dx = 0, dy = 0, s = 1) => `<g transform="translate(${dx} ${dy}) scale(${s})">
  ${p('M14 30 V54 H50 V30', fill)}${p('M8 32 L32 10 L56 32 Z', roof)}${rect(27, 38, 10, 16, 2, C.brownD)}</g>`;

const icons = {
  // ------------------------------------------------------------ vitals & resources
  energy: `${p('M32 56 C18 46 8 37 8 24 A12 12 0 0 1 32 18 A12 12 0 0 1 56 24 C56 37 46 46 32 56 Z', C.green)}
    ${hl('M15 24 A7 7 0 0 1 22 17')}${bolt(13, 12, 0.6)}`,
  electricity: `${rect(10, 18, 40, 28, 6, C.dark)}${rect(50, 26, 6, 12, 2, C.steel)}${rect(14, 22, 32, 20, 3, C.amber)}
    ${p('M33 23 L24 33 H31 L28 41 L37 30 H30 Z', C.white)}`,
  water: drop(C.blue),
  water_dirty: `${drop(C.mud)}${circ(27, 40, 2.5, C.brownD)}${circ(37, 45, 2, C.brownD)}${circ(36, 33, 1.8, C.brownD)}`,
  metal: `${p('M8 44 L14 34 H38 L44 44 Z', C.grey)}${p('M20 32 L26 22 H50 L56 32 Z', C.steel)}${p('M14 56 L20 46 H44 L50 56 Z', C.grey)}
    ${hl('M18 37 H34')}${hl('M30 25 H44')}`,
  plastic: `${rect(25, 6, 14, 8, 2, C.coral)}${p('M24 14 H40 L44 22 V54 A4 4 0 0 1 40 58 H24 A4 4 0 0 1 20 54 V22 Z', C.sky)}
    ${p('M20 30 H44 V44 H20 Z', C.blue)}${hl('M26 24 V28')}${hl('M26 48 V52')}`,
  food: `${circ(32, 36, 20, C.leaf)}${p('M32 16 C22 22 20 36 26 50', 'none')}${p('M32 16 C42 22 44 36 38 50', 'none')}
    ${p('M14 34 C20 30 26 32 32 38 C38 32 44 30 50 34', 'none')}${hl('M20 26 A14 14 0 0 1 27 20')}`,
  compost: `${p('M14 24 C14 18 50 18 50 24 L54 54 C54 58 10 58 10 54 Z', C.brown)}${p('M18 24 C22 14 42 14 46 24', C.brownD)}
    ${leafShape(C.leaf, 16, 22, 0.5)}${hl('M16 32 L15 46')}`,
  fish: `${p('M8 32 C16 20 36 18 48 30 L58 22 L56 32 L58 42 L48 34 C36 46 16 44 8 32 Z', C.blue)}
    ${circ(20, 30, 2.5, INK)}${line('M30 24 C33 29 33 35 30 40', 2.4)}${hl('M16 24 C22 21 28 20 33 21')}`,
  fish_dirty: `${p('M8 32 C16 20 36 18 48 30 L58 22 L56 32 L58 42 L48 34 C36 46 16 44 8 32 Z', C.mud)}
    ${circ(20, 30, 2.5, INK)}${line('M30 24 C33 29 33 35 30 40', 2.4)}${circ(40, 36, 2, C.brownD)}`,
  filter: `${rect(16, 10, 32, 44, 8, C.green)}${rect(12, 8, 40, 8, 3, C.steel)}${rect(12, 48, 40, 8, 3, C.steel)}
    ${line('M22 22 V42 M28 22 V42 M34 22 V42 M40 22 V42', 2.4)}`,
  scrap: `${p('M14 20 L30 12 L50 18 L46 38 L52 50 L30 56 L12 48 L18 34 Z', C.steel)}${line('M24 26 L34 34 L28 44', 2.4)}
    ${line('M40 24 L36 34 L44 42', 2.4)}${hl('M18 22 L28 17')}`,
  organic: `${p('M32 58 C16 50 12 34 20 20 C26 28 30 30 32 30 C34 30 38 28 44 20 C52 34 48 50 32 58 Z', C.brown)}
    ${p('M32 30 C32 22 34 14 40 8', 'none')}${leafShape(C.leaf, 22, -2, 0.42)}`,
  seed: `${p('M32 58 C22 58 18 48 22 40 C26 34 38 34 42 40 C46 48 42 58 32 58 Z', C.brown)}
    ${line('M32 36 V22', 3)}${p('M32 26 C24 26 16 20 16 12 C26 12 32 18 32 26 Z', C.leaf)}${p('M32 22 C38 22 46 16 46 8 C38 8 32 14 32 22 Z', C.green)}`,
  ammo: `${circ(32, 34, 20, C.steel)}${circ(32, 34, 13, C.dark)}${hl('M22 26 A12 12 0 0 1 30 20')}`,
  yep: `${p('M32 4 L39 20 L56 22 L43 34 L47 52 L32 43 L17 52 L21 34 L8 22 L25 20 Z', C.amber)}
    ${leafShape(C.green, 18, 16, 0.44)}`,
  level: `${p('M14 8 H50 V36 C50 48 32 56 32 56 C32 56 14 48 14 36 Z', C.blueD)}${p('M22 30 L32 20 L42 30', 'none')}${p('M22 42 L32 32 L42 42', 'none')}`,
  crop_cabbage: `${circ(32, 36, 20, C.leaf)}${p('M32 16 C22 22 20 36 26 50', 'none')}${p('M32 16 C42 22 44 36 38 50', 'none')}
    ${p('M14 34 C20 30 26 32 32 38 C38 32 44 30 50 34', 'none')}${hl('M20 26 A14 14 0 0 1 27 20')}`,
  crop_carrot: `${p('M38 20 C46 24 46 30 42 34 L16 58 C13 60 10 57 12 54 L32 24 C34 20 36 19 38 20 Z', C.orange)}
    ${line('M24 44 L29 47 M30 35 L35 38', 2.4)}${p('M40 20 C38 12 40 6 44 4 C46 10 44 16 40 20 Z', C.leaf)}${p('M42 22 C48 16 54 16 58 18 C54 24 48 26 42 22 Z', C.green)}
    ${hl('M30 28 L20 44')}`,
  crop_tomato: `${circ(32, 37, 21, C.coral)}${p('M32 14 L36 22 L44 20 L40 27 L46 32 L36 30 L32 36 L28 30 L18 32 L24 27 L20 20 L28 22 Z', C.green, 'stroke-width="2.4"')}
    ${line('M32 14 V8', 3)}${hl('M19 34 A14 14 0 0 1 24 26')}`,
  crop_tree: `${rect(28, 40, 8, 16, 2, C.brownD)}${circ(32, 26, 18, C.leaf)}${circ(22, 32, 9, C.leafD)}${hl('M24 16 A12 12 0 0 1 32 12')}${p('M14 56 H50', 'none')}`,
  // ------------------------------------------------------------ tools
  tool_vacuum: `${p('M8 40 L20 30 L26 38 L14 48 Z', C.steel)}${rect(22, 22, 26, 20, 6, C.green)}${rect(44, 26, 10, 12, 3, C.dark)}
    ${p('M30 42 L28 54 H36 L40 42', C.dark)}${circ(35, 32, 4, C.white)}${p('M4 34 C8 30 8 26 4 22', 'none')}`,
  tool_seed: `${rect(22, 8, 20, 16, 6, C.sky)}${circ(28, 16, 3, C.brown)}${circ(36, 14, 2.6, C.brown)}${circ(33, 20, 2.2, C.brown)}
    ${p('M6 30 H46 A6 6 0 0 1 52 36 V40 A6 6 0 0 1 46 46 H6 Z', C.leafD)}${rect(52, 33, 8, 10, 2, C.dark)}
    ${p('M18 46 L14 58 H26 L30 46', C.brownD)}${line('M27 24 V30 M37 24 V30', 3)}${hl('M10 35 H40')}`,
  tool_recycle: cycleArrows(C.green),
  tool_scanner: `${rect(12, 14, 34, 40, 6, C.dark)}${rect(17, 19, 24, 22, 3, C.blue)}${p('M20 34 L26 28 L30 32 L38 24', 'none')}
    ${circ(29, 48, 3, C.green)}${p('M50 18 C54 22 54 30 50 34', 'none')}${p('M54 12 C60 20 60 32 54 40', 'none')}`,
  tool_hand: `${p('M20 56 C14 50 10 42 10 36 C10 32 16 32 18 36 L20 40 V16 A4 4 0 0 1 28 16 V30 V12 A4 4 0 0 1 36 12 V30 V14 A4 4 0 0 1 44 14 V32 V22 A4 4 0 0 1 52 22 V42 C52 50 46 56 40 56 Z', C.sand)}
    ${hl('M24 20 V28')}`,
  // ------------------------------------------------------------ environment indicators
  air: `${line('M8 24 H38 A7 7 0 1 0 31 17', 7)}${line('M8 24 H38 A7 7 0 1 0 31 17', 3, C.sky)}
    ${line('M8 36 H48 A7 7 0 1 1 41 43', 7)}${line('M8 36 H48 A7 7 0 1 1 41 43', 3, C.sky)}${line('M14 48 H28', 7)}${line('M14 48 H28', 3, C.sky)}`,
  water_quality: `${drop(C.blue, 0, -2)}${p('M18 44 C22 40 26 40 30 44 C34 48 38 48 42 44 C44 42 46 42 48 43', 'none')}`,
  carbon: `${cloud(C.grey, 0, 2)}<text x="32" y="45" font-family="Arial" font-weight="900" font-size="13" text-anchor="middle" fill="${INK}">CO₂</text>`,
  biodiversity: `${p('M32 30 C26 14 8 10 8 22 C8 32 20 36 30 34 C18 38 14 52 24 54 C30 54 32 44 32 38 Z', C.orange)}
    ${p('M32 30 C38 14 56 10 56 22 C56 32 44 36 34 34 C46 38 50 52 40 54 C34 54 32 44 32 38 Z', C.purple)}
    ${line('M32 28 V48', 4)}${line('M31 28 L26 18 M33 28 L38 18', 2.4)}`,
  ecosystem: `${p('M32 56 C18 46 8 37 8 24 A12 12 0 0 1 32 18 A12 12 0 0 1 56 24 C56 37 46 46 32 56 Z', C.coral)}
    ${leafShape(C.leaf, 12, 8, 0.6)}`,
  temperature: `${rect(26, 6, 12, 36, 6, C.white)}${circ(32, 48, 9, C.coral)}${line('M32 44 V20', 4, C.coral)}${line('M42 14 H48 M42 22 H48 M42 30 H48', 2.4)}`,
  // ------------------------------------------------------------ weather
  w_sun: `${sunRays(32, 32, 18, 27, 8)}${circ(32, 32, 13, C.amber)}${hl('M25 27 A8 8 0 0 1 30 23')}`,
  w_cloud: `${sunRays(40, 22, 11, 17, 7)}${circ(40, 22, 8, C.amber)}${cloud(C.white, -2, 8)}`,
  w_overcast: `${cloud(C.grey, 8, -6, 0.8)}${cloud(C.white, -2, 8)}`,
  w_rain: `${cloud(C.white, 0, -4)}${line('M20 50 L17 58 M32 50 L29 58 M44 50 L41 58', 5)}${line('M20 50 L17 58 M32 50 L29 58 M44 50 L41 58', 2.4, C.blue)}`,
  w_storm: `${cloud(C.grey, 0, -6)}${bolt(14, 26, 0.55)}${line('M18 50 L15 58 M44 50 L41 58', 5)}${line('M18 50 L15 58 M44 50 L41 58', 2.4, C.blue)}`,
  w_wind: `${line('M6 22 H36 A7 7 0 1 0 29 15', 7)}${line('M6 22 H36 A7 7 0 1 0 29 15', 3, C.white)}
    ${line('M6 34 H50 A7 7 0 1 1 43 41', 7)}${line('M6 34 H50 A7 7 0 1 1 43 41', 3, C.white)}${line('M12 46 H30', 7)}${line('M12 46 H30', 3, C.white)}`,
  w_snow: `${line('M32 8 V56 M11 20 L53 44 M11 44 L53 20', 7)}${line('M32 8 V56 M11 20 L53 44 M11 44 L53 20', 3, C.snow)}
    ${line('M26 12 L32 18 L38 12 M26 52 L32 46 L38 52', 2.4)}`,
  w_moon: `${p('M40 8 A24 24 0 1 0 56 40 A18 18 0 1 1 40 8 Z', C.amber)}${circ(46, 18, 1.8, C.white)}${circ(54, 28, 1.4, C.white)}`,
  clock: `${circ(32, 32, 24, C.white)}${line('M32 18 V32 L42 38', 4)}${circ(32, 32, 2.5, INK)}`,
  calendar: `${rect(10, 14, 44, 42, 6, C.white)}${p('M10 26 V20 A6 6 0 0 1 16 14 H48 A6 6 0 0 1 54 20 V26 Z', C.coral)}
    ${line('M22 8 V18 M42 8 V18', 4)}${rect(18, 34, 8, 7, 1.5, C.green)}${rect(30, 34, 8, 7, 1.5, C.grey)}${rect(42, 34, 6, 7, 1.5, C.grey)}${rect(18, 45, 8, 6, 1.5, C.grey)}`,
  // ------------------------------------------------------------ regions
  region_glacier: `${p('M4 54 L22 22 L30 32 L40 14 L60 54 Z', C.sky)}${p('M16 33 L22 22 L28 30 L24 34 L20 31 Z', C.white)}${p('M34 25 L40 14 L48 30 L43 28 L39 32 Z', C.white)}
    ${line('M10 48 H22 M36 46 H50', 2.4, C.blueD)}`,
  region_wind: `${p('M4 58 C14 46 26 50 34 46 C44 42 52 46 60 52 V58 Z', C.leaf)}${turbine(4, 0, 0.82)}`,
  region_city: `${rect(6, 26, 16, 30, 2, C.amber)}${rect(22, 12, 18, 44, 2, C.blue)}${rect(40, 30, 18, 26, 2, C.coral)}
    ${line('M27 20 H35 M27 28 H35 M27 36 H35 M27 44 H35', 2.4, C.white)}${line('M10 34 H18 M10 42 H18 M45 38 H53 M45 46 H53', 2.4, C.white)}`,
  region_industry: `${p('M6 56 V32 L18 24 V32 L30 24 V32 L42 24 V56 Z', C.steel)}${rect(44, 12, 10, 44, 2, C.coral)}${line('M44 22 H54', 2.4, C.white)}
    ${cloud(C.grey, 26, -14, 0.5)}${rect(12, 40, 8, 8, 1.5, C.amber)}${rect(26, 40, 8, 8, 1.5, C.amber)}`,
  region_sun: `${sunRays(46, 16, 9, 14, 7)}${circ(46, 16, 7, C.amber)}${p('M6 52 L16 30 H44 L34 52 Z', C.blueD)}${line('M11 41 H39 M24 30 L19 52 M34 30 L29 52', 2.2, C.sky)}
    ${line('M20 52 V58 M30 52 V58', 3)}`,
  region_lake: `${p('M6 38 C6 26 22 20 32 20 C46 20 58 26 58 38 C58 50 44 56 32 56 C18 56 6 50 6 38 Z', C.blue)}
    ${line('M16 36 C20 32 24 32 28 36 C32 40 36 40 40 36', 2.6, C.white)}${line('M24 46 C28 42 32 42 36 46', 2.6, C.white)}${p('M44 16 L48 8 L52 16 Z', C.leaf)}`,
  region_lake_dirty: `${p('M6 38 C6 26 22 20 32 20 C46 20 58 26 58 38 C58 50 44 56 32 56 C18 56 6 50 6 38 Z', C.mud)}
    ${line('M16 36 C20 32 24 32 28 36 C32 40 36 40 40 36', 2.6, C.brownD)}${circ(42, 46, 3, C.brownD)}${circ(20, 46, 2, C.brownD)}`,
  // ------------------------------------------------------------ map points of interest
  poi_market: `${rect(10, 30, 44, 26, 3, C.white)}${p('M6 30 L12 14 H52 L58 30 Z', C.coral)}${line('M20 14 L18 30 M32 14 V30 M44 14 L46 30', 2.4)}
    ${rect(16, 38, 12, 18, 2, C.brownD)}${rect(34, 38, 14, 10, 2, C.sky)}`,
  poi_home: `${house(C.white, C.coral)}${p('M42 42 C38 39 36 37 36 34.5 A3 3 0 0 1 42 33.5 A3 3 0 0 1 48 34.5 C48 37 46 39 42 42 Z', C.coral, 'stroke-width="2"')}`,
  poi_factory: `${p('M6 56 V30 L20 22 V30 L34 22 V56 Z', C.steel)}${rect(38, 10, 12, 46, 2, C.brick || C.coral)}${cloud(C.grey, 24, -16, 0.55)}
    ${rect(12, 38, 7, 7, 1.5, C.amber)}${rect(23, 38, 7, 7, 1.5, C.amber)}${line('M38 20 H50', 2.4, C.white)}`,
  poi_station: `${rect(12, 30, 40, 24, 3, C.coral)}${p('M8 32 L32 18 L56 32 Z', C.white)}${line('M44 22 V6', 3)}${circ(44, 6, 3, C.amber)}
    ${rect(18, 38, 10, 8, 1.5, C.amber)}${rect(34, 38, 10, 16, 1.5, C.brownD)}`,
  poi_charge: `${rect(18, 8, 28, 48, 6, C.dark)}${rect(23, 14, 18, 14, 2, C.blue)}${bolt(15, 26, 0.5)}${line('M46 40 C56 40 56 54 48 56', 3)}`,
  poi_fire: `${p('M32 58 C18 58 12 46 16 34 C18 28 22 26 22 18 C28 22 30 28 30 32 C34 26 36 16 32 6 C46 14 52 28 50 40 C49 52 42 58 32 58 Z', C.orange)}
    ${p('M32 56 C26 56 22 50 24 44 C26 40 30 38 30 34 C36 38 40 44 40 48 C40 53 37 56 32 56 Z', C.amber)}`,
  poi_enemy: `${p('M10 52 C8 34 16 14 32 14 C48 14 56 34 54 52 C50 48 46 54 42 50 C38 56 34 50 32 54 C30 50 26 56 22 50 C18 54 14 48 10 52 Z', C.purple)}
    ${p('M20 30 L28 34', 'none')}${p('M44 30 L36 34', 'none')}${circ(25, 37, 3, C.white)}${circ(39, 37, 3, C.white)}${line('M26 46 C30 43 34 43 38 46', 2.4)}`,
  poi_hydro: `${circ(32, 30, 20, C.white)}${circ(32, 30, 6, C.steel)}${line('M32 10 V50 M12 30 H52 M18 16 L46 44 M46 16 L18 44', 2.4)}
    ${p('M4 52 C12 48 18 56 26 52 C34 48 40 56 48 52 C54 49 58 52 60 53 V60 H4 Z', C.blue)}`,
  poi_inlet: `${rect(4, 18, 30, 14, 3, C.steel)}${rect(30, 14, 8, 22, 2, C.dark)}${p('M38 26 C46 26 50 34 50 40 C50 48 44 52 42 56', 'none')}
    ${drop(C.mud, 26, 22, 0.55)}${line('M12 18 V32 M22 18 V32', 2.2)}`,
  poi_fishing: `${line('M8 30 L50 6', 7)}${line('M8 30 L50 6', 3.4, C.brown)}${line('M49 7 V30', 2.2, C.white)}
    ${line('M49 30 V37 A5 5 0 0 1 40 37', 3, C.white)}
    ${p('M6 48 C12 39 26 39 32 48 L40 42 V54 L32 48 C26 57 12 57 6 48 Z', C.blue)}${circ(13, 47, 2, INK)}${hl('M12 43 C16 41 20 41 24 42')}`,
  poi_quest: `${p('M32 4 L58 32 L32 60 L6 32 Z', C.amber)}${line('M32 18 V36', 6)}${circ(32, 45, 3.5, INK)}`,
  poi_project: `${rect(12, 8, 40, 48, 5, C.white)}${rect(22, 4, 20, 9, 3, C.steel)}${p('M20 26 L24 30 L30 22', 'none')}${line('M34 26 H44', 3)}
    ${p('M20 40 L24 44 L30 36', 'none')}${line('M34 40 H44', 3)}`,
  poi_tree: `${rect(28, 40, 8, 16, 2, C.brownD)}${circ(32, 26, 18, C.leaf)}${circ(22, 32, 9, C.leafD)}${hl('M24 16 A12 12 0 0 1 32 12')}`,
  poi_finale: `${circ(32, 32, 24, C.dark)}${p('M32 10 L38 26 L54 32 L38 38 L32 54 L26 38 L10 32 L26 26 Z', C.green)}${circ(32, 32, 6, C.white)}`,
  poi_solar: `${sunRays(48, 14, 7, 12, 6)}${circ(48, 14, 5.5, C.amber)}${p('M6 50 L14 28 H42 L34 50 Z', C.blueD)}${line('M10 39 H38 M22 28 L18 50 M32 28 L28 50', 2.2, C.sky)}${line('M18 50 V58 M30 50 V58', 3)}`,
  poi_turbine: turbine(),
  poi_glacier: `${p('M6 54 L24 18 L34 30 L42 20 L58 54 Z', C.sky)}${p('M18 30 L24 18 L30 26 L26 30 L22 27 Z', C.white)}`,
  poi_warning: `${p('M32 6 L60 56 H4 Z', C.amber)}${line('M32 22 V38', 5)}${circ(32, 47, 3, INK)}`,
  player: `${p('M32 4 L54 56 L32 44 L10 56 Z', C.green)}${hl('M32 12 L44 42')}`,
  // ------------------------------------------------------------ interface
  ui_close: `${circ(32, 32, 24, C.coral)}${line('M23 23 L41 41 M41 23 L23 41', 5, C.white)}`,
  ui_collapse: `${circ(32, 32, 24, C.dark)}${line('M21 37 L32 26 L43 37', 5, C.white)}`,
  ui_expand: `${circ(32, 32, 24, C.dark)}${line('M21 27 L32 38 L43 27', 5, C.white)}`,
  ui_eye: `${p('M4 32 C14 16 50 16 60 32 C50 48 14 48 4 32 Z', C.white)}${circ(32, 32, 10, C.blueD)}${circ(32, 32, 4, INK)}`,
  ui_eye_off: `${p('M4 32 C14 16 50 16 60 32 C50 48 14 48 4 32 Z', C.grey)}${circ(32, 32, 10, C.steel)}${line('M10 54 L54 10', 7)}${line('M10 54 L54 10', 3, C.coral)}`,
  ui_gear: `${p('M28 4 H36 L38 12 L44 15 L51 10 L56 15 L51 22 L54 28 L62 30 V36 L54 38 L51 44 L56 51 L51 56 L44 51 L38 54 L36 62 H28 L26 54 L20 51 L13 56 L8 51 L13 44 L10 38 L2 36 V30 L10 28 L13 22 L8 15 L13 10 L20 15 L26 12 Z', C.grey)}${circ(32, 33, 9, C.dark)}`,
  ui_map: `${p('M6 14 L22 8 L42 14 L58 8 V50 L42 56 L22 50 L6 56 Z', C.sand)}${line('M22 8 V50 M42 14 V56', 2.4)}
    ${p('M30 22 C30 18 38 18 38 22 C38 26 34 32 34 32 C34 32 30 26 30 22 Z', C.coral, 'stroke-width="2.4"')}${line('M10 40 C16 36 20 44 28 40', 2.4, C.blueD)}`,
  ui_journal: `${p('M10 10 H30 C32 10 32 12 32 12 V56 C32 54 30 52 28 52 H10 Z', C.green)}${p('M54 10 H34 C32 10 32 12 32 12 V56 C32 54 34 52 36 52 H54 Z', C.greenD)}
    ${line('M15 22 H27 M15 30 H27 M37 22 H49 M37 30 H49', 2.4, C.white)}`,
  ui_build: `${p('M10 50 L34 26 L40 32 L16 56 Z', C.brown)}${p('M30 12 L48 6 L58 16 L52 22 L46 18 L40 24 L32 20 Z', C.steel)}`,
  ui_skills: `${circ(32, 14, 8, C.amber)}${circ(14, 48, 8, C.blue)}${circ(50, 48, 8, C.green)}${line('M28 21 L18 41 M36 21 L46 41 M22 48 H42', 3)}`,
  ui_orders: `${rect(12, 10, 40, 48, 5, C.white)}${rect(22, 6, 20, 9, 3, C.amber)}${line('M20 28 H44 M20 36 H44 M20 44 H34', 3)}${circ(44, 46, 6, C.green)}`,
  ui_help: `${circ(32, 32, 24, C.blueD)}${p('M24 25 C24 14 42 14 42 25 C42 32 34 32 33 38', 'none', `stroke="${C.white}" stroke-width="5"`)}${circ(33, 46, 3.2, C.white, 'stroke="none"')}`,
  ui_pause: `${circ(32, 32, 24, C.dark)}${rect(23, 20, 6, 24, 2, C.white, 'stroke="none"')}${rect(35, 20, 6, 24, 2, C.white, 'stroke="none"')}`,
  ui_check: `${circ(32, 32, 24, C.green)}${line('M20 33 L28 41 L44 24', 5, C.white)}`,
  ui_cross: `${circ(32, 32, 24, C.coral)}${line('M23 23 L41 41 M41 23 L23 41', 5, C.white)}`,
  ui_layers: `${p('M32 34 L58 46 L32 58 L6 46 Z', C.blueD)}${p('M32 22 L58 34 L32 46 L6 34 Z', C.amber)}${p('M32 8 L58 20 L32 32 L6 20 Z', C.green)}`,
  ui_hud: `${rect(6, 10, 52, 40, 6, C.dark)}${rect(11, 15, 14, 9, 2, C.green)}${rect(39, 15, 14, 14, 2, C.blue)}${rect(20, 39, 24, 7, 2, C.amber)}
    ${line('M24 56 H40', 4)}`,
};

function svg(body) {
  return `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 64 64" width="64" height="64">${body}</svg>`;
}

const out = process.argv[2] || 'out';
fs.mkdirSync(out, { recursive: true });
const size = parseInt(process.argv[3] || '128', 10);
for (const [name, body] of Object.entries(icons)) {
  const r = new Resvg(svg(body), { fitTo: { mode: 'width', value: size }, background: 'rgba(0,0,0,0)' });
  fs.writeFileSync(path.join(out, name + '.png'), r.render().asPng());
}
// Contact sheet for review.
const names = Object.keys(icons); const cols = 10; const cell = 72;
const rows = Math.ceil(names.length / cols);
let sheet = `<svg xmlns="http://www.w3.org/2000/svg" width="${cols * cell}" height="${rows * cell}"><rect width="100%" height="100%" fill="#0b1a1f"/>`;
names.forEach((n, i) => { sheet += `<g transform="translate(${(i % cols) * cell + 4} ${Math.floor(i / cols) * cell + 4})">${icons[n]}</g>`; });
sheet += '</svg>';
fs.writeFileSync(path.join(out, '_sheet.png'), new Resvg(sheet, { fitTo: { mode: 'width', value: cols * cell * 2 } }).render().asPng());
console.log('icons:', names.length);
