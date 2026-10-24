// Hand-authored maps must stand still until the player breaks something.
import { buildCity, buildBase, buildAtomgrad } from '../src/maps.js';

let failures = 0;
for (const [name, build] of [['city', buildCity], ['base', buildBase], ['atomgrad', buildAtomgrad]]) {
  const { world, spawns } = build();
  world.cleanUnsupported();
  world.settle();
  let destroyed = 0;
  world.hooks.destroy = () => { destroyed++; };
  world.hooks.crumble = () => { destroyed++; };
  const solidBefore = world.stats.solid;
  for (let i = 0; i < 600; i++) world.update(1 / 60, i / 60);
  const ok = destroyed === 0 && world.stats.falling === 0;
  console.log(`${ok ? 'OK  ' : 'FAIL'} ${name}: ${solidBefore} cells, ${destroyed} broke on their own, ${world.stats.falling} still falling, ${spawns.length} spawns`);
  if (!ok) failures++;
}

// A nuke must actually level a district.
{
  const { world } = buildCity();
  world.cleanUnsupported();
  world.settle();
  world.destroyedTotal = 0;
  world.explode(270, 190, 40, 4.2, { fireChance: 0.9 });
  for (let i = 0; i < 240; i++) world.update(1 / 60, i / 60);
  const lost = world.destroyedTotal; // rubble that falls still counts as solid, so count actual removals
  const ok = lost > 900;
  console.log(`${ok ? 'OK  ' : 'FAIL'} nuclear blast removes a district: ${lost} cells`);
  if (!ok) failures++;
}

// Digging under a house must topple it.
{
  const { world } = buildCity();
  world.cleanUnsupported();
  world.settle();
  // the brick house on the left sits around x=42
  let groundY = world.groundY;
  for (let y = 0; y < world.h; y++) {
    if (world.mat[world.idx(44, y)] === 4) { groundY = y; break; }
  }
  const topBefore = world.mat[world.idx(44, groundY)];
  world.rect(0, world.groundY, 58, 6, 0); // undercut the whole footprint of the brick house
  for (let i = 0; i < 300; i++) world.update(1 / 60, i / 60);
  let brickLeft = 0;
  for (let i = 0; i < world.n; i++) if (world.mat[i] === 4) brickLeft++;
  const ok = brickLeft < 400;
  console.log(`${ok ? 'OK  ' : 'FAIL'} undercut house collapses: ${brickLeft} brick cells left`);
  if (!ok) failures++;
}

console.log(failures ? `\n${failures} failure(s)` : '\nAll map checks passed');
process.exit(failures ? 1 : 0);
