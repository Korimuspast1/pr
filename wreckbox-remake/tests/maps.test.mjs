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
  // the brick house on the left sits around x=42
  const topOfBrick = () => {
    for (let y = 0; y < world.h; y++) {
      for (let x = 0; x < 60; x++) if (world.mat[world.idx(x, y)] === 4) return y;
    }
    return world.h;
  };
  const topBefore = topOfBrick();
  world.rect(0, world.groundY, 58, 6, 0); // undercut the whole footprint of the brick house
  for (let i = 0; i < 300; i++) world.update(1 / 60, i / 60);
  let brickLeft = 0;
  for (let i = 0; i < world.n; i++) if (world.mat[i] === 4) brickLeft++;
  const topAfter = topOfBrick();
  const ok = topAfter > topBefore + 5 && brickLeft < 900;
  console.log(`${ok ? 'OK  ' : 'FAIL'} undercut house collapses: ${brickLeft} brick cells left, top ${topBefore} -> ${topAfter}`);
  if (!ok) failures++;
}

console.log(failures ? `\n${failures} failure(s)` : '\nAll map checks passed');
process.exit(failures ? 1 : 0);
