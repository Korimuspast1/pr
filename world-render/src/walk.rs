//! First-person walk mode: player physics plus a fast terrain renderer.
//!
//! The still renderer in `render.rs` is a full ray marcher with shadows and
//! reflections. It needs seconds per frame, which is fine for a picture and
//! hopeless for walking, so this module uses a different strategy:
//!
//! * the island is baked once into a small **lit colour map** (albedo times
//!   sunlight and sky fill, with a cheap ambient-occlusion term) and a normal
//!   map — per frame the terrain only needs one texture lookup per band;
//! * the sky is baked into a small **direction lookup table**;
//! * props are **billboards** from a sprite bank baked from the same
//!   signed-distance geometry at start-up;
//! * a per-pixel depth buffer keeps props behind hills and behind each other;
//! * everything is written as linear light into a float buffer and converted
//!   to sRGB once per frame through a tone-mapping table.

use crate::render::Scene;
use crate::vec3::{v3, Vec3};
use crate::world::{fbm, noise2, smoothstep, HeightField, Mat, Prop, Solid, World, WATER_LEVEL};

// ---------------------------------------------------------------------------
// Player
// ---------------------------------------------------------------------------

/// Eye height above the ground, in world units (a person is ~1.7 m tall).
pub const EYE_HEIGHT: f32 = 1.62;

#[derive(Clone, Copy, Default)]
#[allow(dead_code)]
pub struct Input {
    pub forward: bool,
    pub back: bool,
    pub left: bool,
    pub right: bool,
    pub run: bool,
    pub jump: bool,
}

impl Input {
    /// Bit 0 forward, 1 back, 2 left, 3 right, 4 run, 5 jump.
    #[allow(dead_code)]
    pub fn from_bits(bits: u32) -> Self {
        Input {
            forward: bits & 1 != 0,
            back: bits & 2 != 0,
            left: bits & 4 != 0,
            right: bits & 8 != 0,
            run: bits & 16 != 0,
            jump: bits & 32 != 0,
        }
    }
}

#[derive(Clone, Copy, Debug)]
pub struct Player {
    /// Position of the feet.
    pub pos: Vec3,
    /// Heading in radians; the view direction is `(sin(yaw), 0, -cos(yaw))`,
    /// so `yaw = 0` looks towards -Z.
    pub yaw: f32,
    /// Vertical look angle, positive up.
    pub pitch: f32,
    pub vel_y: f32,
    pub on_ground: bool,
    /// Accumulated walk distance, used for the head bob.
    pub travelled: f32,
}

impl Player {
    /// Puts the player on the beach, with the island and its lighthouse ahead.
    pub fn spawn(world: &World) -> Player {
        let landmark = world.lantern_pos;
        let mut best = v3(0.0, world.height.sample(0.0, 6.0), 6.0);
        let mut best_score = f32::INFINITY;

        for i in 0..720 {
            let angle = i as f32 / 720.0 * std::f32::consts::TAU;
            for ring in 0..6 {
                let radius = 4.45 + ring as f32 * 0.35;
                let x = angle.sin() * radius;
                let z = angle.cos() * radius;
                let h = world.height.sample(x, z);
                if !(0.05..=1.10).contains(&h) {
                    continue;
                }
                let ny = world.height.normal(x, z).y;
                if ny < 0.88 {
                    continue;
                }

                // Look towards the middle of the island and the landmark.
                let look_x = landmark.x * 0.6 - x * 0.4;
                let look_z = landmark.z * 0.6 - z * 0.4;
                let look_len = (look_x * look_x + look_z * look_z).sqrt().max(1e-3);
                let dir_x = look_x / look_len;
                let dir_z = look_z / look_len;

                let mut land_ahead = 0.0;
                for step in 1..=6 {
                    let d = step as f32 * 0.8;
                    let ahead = world.height.sample(x + dir_x * d, z + dir_z * d);
                    land_ahead += (ahead - 0.22).max(0.0);
                }

                // Do not plant the player in a grove: the first thing they see
                // would be the back of a pine, not the island.
                let mut blocked = 0.0;
                for step in 1..=8 {
                    let d = step as f32 * 0.7;
                    let probe = v3(x + dir_x * d, h + 1.15, z + dir_z * d);
                    blocked += (1.15 - world.props_exact(probe)).max(0.0);
                }

                let clearance = world.props_exact(v3(x, h + 1.0, z));
                // Refuse to spawn inside a trunk, a rock or the dock.
                if clearance < 0.55 {
                    continue;
                }
                let to_landmark =
                    ((landmark.x - x).powi(2) + (landmark.z - z).powi(2)).sqrt();

                // Low beach rather than the grassy shelf: from there the sea,
                // the shore and the lighthouse are all in the first frame.
                let mut score = (h - 0.25).abs() * 3.0;
                score += (1.0 - ny) * 14.0;
                score += (2.40 - clearance).max(0.0) * 18.0;
                score += blocked * 14.0;
                score += (3.2 - land_ahead).max(0.0) * 4.0;
                // A first frame with the lighthouse comfortably framed: close
                // enough to be the subject, far enough to see the island too.
                // Standing at its foot is not a view, it is a wall.
                score += (to_landmark - 5.0).abs() * 1.6;
                score += (3.8 - to_landmark).max(0.0) * 20.0;

                if score < best_score {
                    best_score = score;
                    best = v3(x, h, z);
                }
            }
        }

        let dx = landmark.x - best.x;
        let dz = landmark.z - best.z;
        Player {
            pos: best,
            yaw: dx.atan2(-dz),
            pitch: 0.02,
            vel_y: 0.0,
            on_ground: true,
            travelled: 0.0,
        }
    }

    /// Unit vector the player is looking along.
    pub fn forward(&self) -> Vec3 {
        v3(self.yaw.sin(), 0.0, -self.yaw.cos())
    }

    /// Unit vector pointing to the player's right.
    pub fn right(&self) -> Vec3 {
        v3(self.yaw.cos(), 0.0, self.yaw.sin())
    }

    pub fn look(&mut self, dx: f32, dy: f32) {
        let sensitivity = 0.0026;
        self.yaw -= dx * sensitivity;
        self.pitch = (self.pitch - dy * sensitivity).clamp(-1.30, 1.30);
    }

    pub fn eye(&self) -> Vec3 {
        let bob = if self.on_ground {
            (self.travelled * 7.5).sin() * 0.035
        } else {
            0.0
        };
        v3(self.pos.x, self.pos.y + EYE_HEIGHT + bob, self.pos.z)
    }

    /// Integrates one step of walking. `dt` is in seconds.
    pub fn update(&mut self, world: &World, input: &Input, dt: f32) {
        let dt = dt.clamp(0.0, 0.1);

        let speed = if input.run { 7.2 } else { 3.4 };
        let mut wish_x = 0.0;
        let mut wish_z = 0.0;
        let fwd = self.forward();
        let right = self.right();
        if input.forward {
            wish_x += fwd.x;
            wish_z += fwd.z;
        }
        if input.back {
            wish_x -= fwd.x;
            wish_z -= fwd.z;
        }
        if input.left {
            wish_x -= right.x;
            wish_z -= right.z;
        }
        if input.right {
            wish_x += right.x;
            wish_z += right.z;
        }
        let len = (wish_x * wish_x + wish_z * wish_z).sqrt();
        if len > 0.0 {
            wish_x /= len;
            wish_z /= len;
        }

        let step_x = wish_x * speed * dt;
        let step_z = wish_z * speed * dt;

        // Axis-separated movement, so sliding along an obstacle still works.
        if step_x != 0.0 && self.can_stand_at(world, self.pos.x + step_x, self.pos.z, step_x.abs()) {
            self.pos.x += step_x;
            self.travelled += step_x.abs();
        }
        if step_z != 0.0 && self.can_stand_at(world, self.pos.x, self.pos.z + step_z, step_z.abs()) {
            self.pos.z += step_z;
            self.travelled += step_z.abs();
        }

        let ground = world.height.sample(self.pos.x, self.pos.z);

        if self.on_ground && input.jump {
            self.vel_y = 4.4;
            self.on_ground = false;
        }

        if self.on_ground {
            self.pos.y = ground;
        } else {
            self.vel_y -= 11.5 * dt;
            self.pos.y += self.vel_y * dt;
            if self.pos.y <= ground {
                self.pos.y = ground;
                self.vel_y = 0.0;
                self.on_ground = true;
            }
        }
    }

    /// `step` is the horizontal length of the step about to be taken, so the
    /// climb limit is a slope, not a per-frame height: a fixed height would
    /// let a low frame rate walk up any cliff.
    fn can_stand_at(&self, world: &World, x: f32, z: f32, step: f32) -> bool {
        let ground = world.height.sample(x, z);
        // No walking into deep water; the shallows are fine.
        if ground < WATER_LEVEL - 0.35 {
            return false;
        }
        // Steep hillsides block the way (~45 degrees), plus a little ledge
        // tolerance so small terrain bumps never stop the player.
        let rise = ground - self.pos.y;
        if rise > 0.16 + step * 1.0 {
            return false;
        }
        // Keep a little distance from trees, walls and rocks.
        world.props_exact(v3(x, ground + 1.0, z)) > 0.30
    }
}

// ---------------------------------------------------------------------------
// Baked assets
// ---------------------------------------------------------------------------

#[derive(Clone, Copy)]
pub struct Light {
    pub sun_dir: Vec3,
    pub sun_color: Vec3,
    pub sky: Vec3,
    pub zenith: Vec3,
    pub ambient: f32,
    pub haze: f32,
    pub exposure: f32,
    pub stars: f32,
    pub sea_shallow: Vec3,
    pub sea_deep: Vec3,
    /// True for the night preset: the lantern glows and the sky gets stars.
    pub lantern: f32,
    pub lantern_pos: Vec3,
}

impl Light {
    pub fn from_scene(scene: &Scene, lantern_pos: Vec3) -> Light {
        Light {
            sun_dir: scene.sun_dir,
            sun_color: scene.sun_color,
            sky: scene.sky_horizon,
            zenith: scene.sky_zenith,
            ambient: scene.ambient,
            haze: scene.haze,
            exposure: scene.exposure,
            stars: scene.star_amount,
            sea_shallow: scene.sea_shallow,
            sea_deep: scene.sea_deep,
            lantern: scene.lantern,
            lantern_pos,
        }
    }
}

/// Terrain colour baked per texel: albedo × light, in linear space.
pub struct ColourMap {
    size: usize,
    extent: f32,
    data: Vec<f32>,
}

impl ColourMap {
    fn build(world: &World, light: Light, size: usize) -> ColourMap {
        let extent = world.height_extent() - 0.4;
        let mut data = vec![0.0f32; size * size * 3];
        let mut shadow = vec![1.0f32; size * size];
        stamp_prop_shadows(world, light, size, extent, &mut shadow);

        for j in 0..size {
            for i in 0..size {
                let x = -extent + 2.0 * extent * (i as f32 + 0.5) / size as f32;
                let z = -extent + 2.0 * extent * (j as f32 + 0.5) / size as f32;
                let h = world.smooth_height.sample(x, z);
                let n = world.smooth_height.normal(x, z);
                let colour = terrain_light(world, light, x, z, h, n) * shadow[j * size + i];
                let idx = (j * size + i) * 3;
                data[idx] = colour.x;
                data[idx + 1] = colour.y;
                data[idx + 2] = colour.z;
            }
        }
        ColourMap {
            size,
            extent,
            data,
        }
    }

    fn sample(&self, x: f32, z: f32) -> Vec3 {
        let u = (x + self.extent) / (2.0 * self.extent) * self.size as f32 - 0.5;
        let v = (z + self.extent) / (2.0 * self.extent) * self.size as f32 - 0.5;
        let i0 = u.floor();
        let j0 = v.floor();
        let fu = u - i0;
        let fv = v - j0;
        let i0 = (i0 as isize).clamp(0, self.size as isize - 1) as usize;
        let j0 = (j0 as isize).clamp(0, self.size as isize - 1) as usize;
        let i1 = (i0 + 1).min(self.size - 1);
        let j1 = (j0 + 1).min(self.size - 1);
        let at = |i: usize, j: usize| {
            let idx = (j * self.size + i) * 3;
            v3(self.data[idx], self.data[idx + 1], self.data[idx + 2])
        };
        let a = at(i0, j0).lerp(at(i1, j0), fu);
        let b = at(i0, j1).lerp(at(i1, j1), fu);
        a.lerp(b, fv)
    }

    /// World-space extent covered by the map (half size).
    pub fn extent(&self) -> f32 {
        self.extent
    }
}

/// Stamps a soft ground shadow for every tall prop, offset away from the sun.
///
/// The billboards themselves cannot receive real shadows in this fast path, so
/// the baked colour map carries their footprints instead. Without it trees and
/// buildings look like stickers floating over the grass.
fn stamp_prop_shadows(world: &World, light: Light, size: usize, extent: f32, shadow: &mut [f32]) {
    let sun = light.sun_dir;
    if sun.y < 0.12 {
        return; // the sun is too low for a meaningful footprint
    }
    let cell = 2.0 * extent / size as f32;
    let to_texel = |v: f32| (v + extent) / cell;

    for solid in world.solids.iter() {
        let (height, footprint) = match solid.prop {
            Prop::Pine { height, radius } => (height * 0.95, radius * 0.55),
            Prop::Cottage { .. } => (1.3, 1.05),
            Prop::Lighthouse => (3.4, 0.75),
            Prop::Boulder { radius } => (radius, radius * 1.1),
            Prop::Dock { .. } => continue,
        };
        // Shadow centre: half way along the sun ray through the prop.
        let reach = height / sun.y * 0.55;
        let cx = solid.anchor.x - sun.x * reach;
        let cz = solid.anchor.z - sun.z * reach;
        let radius = footprint + reach * 0.35;

        let (fi, fj) = (to_texel(cx), to_texel(cz));
        let r = (radius / cell).max(1.0);
        let i0 = ((fi - r).floor().max(0.0)) as usize;
        let i1 = ((fi + r).ceil() as isize).clamp(0, size as isize) as usize;
        let j0 = ((fj - r).floor().max(0.0)) as usize;
        let j1 = ((fj + r).ceil() as isize).clamp(0, size as isize) as usize;
        for j in j0..j1 {
            for i in i0..i1 {
                let dx = (i as f32 + 0.5 - fi) / r;
                let dj = (j as f32 + 0.5 - fj) / r;
                let d = (dx * dx + dj * dj).sqrt();
                if d >= 1.0 {
                    continue;
                }
                let softness = 1.0 - smoothstep(0.35, 1.0, d);
                let strength = 0.62 * softness;
                let slot = &mut shadow[j * size + i];
                *slot = slot.min(1.0 - strength);
            }
        }
    }
}

/// Sky colour baked per (elevation, azimuth-relative-to-sun).
pub struct SkyLut {
    cols: usize,
    rows: usize,
    data: Vec<f32>,
}

impl SkyLut {
    fn build(light: Light, cols: usize, rows: usize) -> SkyLut {
        let mut data = vec![0.0f32; cols * rows * 3];
        for r in 0..rows {
            // elevation: -10° at the bottom row, +90° at the top.
            let e = -0.18 + (1.0 + 0.18) * (r as f32 + 0.5) / rows as f32;
            let elevation = (e * std::f32::consts::FRAC_PI_2).min(std::f32::consts::FRAC_PI_2);
            for c in 0..cols {
                let azimuth = (c as f32 + 0.5) / cols as f32 * std::f32::consts::TAU;
                let dir = v3(
                    elevation.cos() * azimuth.sin(),
                    elevation.sin(),
                    -elevation.cos() * azimuth.cos(),
                );
                let colour = sky_colour(light, dir);
                let idx = (r * cols + c) * 3;
                data[idx] = colour.x;
                data[idx + 1] = colour.y;
                data[idx + 2] = colour.z;
            }
        }
        SkyLut { cols, rows, data }
    }

    /// `azimuth` is measured from the sun's direction (0 = looking at the sun).
    fn sample(&self, elevation: f32, azimuth: f32) -> Vec3 {
        let v = ((elevation + 0.18) / 1.18).clamp(0.0, 0.999) * self.rows as f32;
        let u = (azimuth / std::f32::consts::TAU).rem_euclid(1.0) * self.cols as f32;
        let j0 = v as usize;
        let i0 = u as usize;
        let i1 = (i0 + 1) % self.cols;
        let j1 = (j0 + 1).min(self.rows - 1);
        let fu = u - i0 as f32;
        let fv = v - j0 as f32;
        let at = |i: usize, j: usize| {
            let idx = (j * self.cols + i) * 3;
            v3(self.data[idx], self.data[idx + 1], self.data[idx + 2])
        };
        let a = at(i0, j0).lerp(at(i1, j0), fu);
        let b = at(i0, j1).lerp(at(i1, j1), fu);
        a.lerp(b, fv)
    }
}

/// Tone mapping table: linear light to sRGB bytes.
pub struct ToneLut {
    data: Vec<u8>,
}

impl ToneLut {
    fn build() -> ToneLut {
        let mut data = vec![0u8; TONE_STEPS];
        for (i, slot) in data.iter_mut().enumerate() {
            let linear = i as f32 / (TONE_STEPS - 1) as f32 * TONE_MAX;
            *slot = encode_direct(linear);
        }
        ToneLut { data }
    }

    #[inline]
    fn map(&self, linear: f32) -> u8 {
        let idx = (linear / TONE_MAX * (TONE_STEPS - 1) as f32) as isize;
        self.data[idx.clamp(0, TONE_STEPS as isize - 1) as usize]
    }
}

const TONE_STEPS: usize = 2048;
const TONE_MAX: f32 = 12.0;

fn aces(x: f32) -> f32 {
    let (a, b, c, d, e) = (2.51, 0.03, 2.43, 0.59, 0.14);
    ((x * (a * x + b)) / (x * (c * x + d) + e)).clamp(0.0, 1.0)
}

fn encode_direct(x: f32) -> u8 {
    (aces(x).powf(1.0 / 2.2) * 255.0 + 0.5).clamp(0.0, 255.0) as u8
}

// ---------------------------------------------------------------------------
// Sprites baked from the signed-distance geometry
// ---------------------------------------------------------------------------

pub struct Sprite {
    pub size: usize,
    /// Linear RGB, three floats per texel.
    pub linear: Vec<f32>,
    /// Alpha mask per texel (0 or 255), for fast rejection.
    pub mask: Vec<u8>,
    /// Depth behind the sprite plane, in canonical world units.
    pub delta: Vec<f32>,
}

pub struct SpriteBank {
    pub pines: Vec<Sprite>,
    pub cottages: Vec<Sprite>,
    pub lighthouses: Vec<Sprite>,
    pub boulders: Vec<Sprite>,
    pub docks: Vec<Sprite>,
}

const ANGLES: usize = 12;
const SPRITE_SIZE: usize = 112;

pub fn canonical_prop(kind: u8) -> Prop {
    match kind {
        0 => Prop::Pine {
            height: 2.0,
            radius: 0.56,
        },
        1 => Prop::Cottage { yaw: 0.0 },
        2 => Prop::Lighthouse,
        3 => Prop::Boulder { radius: 0.30 },
        _ => Prop::Dock { yaw: 0.0 },
    }
}

fn empty_slots() -> Vec<Option<Sprite>> {
    (0..ANGLES).map(|_| None).collect()
}

pub struct BakeJob {
    /// (prop kind, angle index)
    tasks: Vec<(u8, usize)>,
    next: usize,
    pub pines: Vec<Option<Sprite>>,
    pub cottages: Vec<Option<Sprite>>,
    pub lighthouses: Vec<Option<Sprite>>,
    pub boulders: Vec<Option<Sprite>>,
    pub docks: Vec<Option<Sprite>>,
    light: Light,
}

impl BakeJob {
    pub fn new(light: Light) -> BakeJob {
        let mut tasks = Vec::new();
        for kind in 0..5u8 {
            for angle in 0..ANGLES {
                tasks.push((kind, angle));
            }
        }
        BakeJob {
            tasks,
            next: 0,
            pines: empty_slots(),
            cottages: empty_slots(),
            lighthouses: empty_slots(),
            boulders: empty_slots(),
            docks: empty_slots(),
            light,
        }
    }

    pub fn total(&self) -> usize {
        self.tasks.len()
    }

    pub fn done(&self) -> usize {
        self.next
    }

    /// Bakes up to `budget` sprites. Returns `true` when everything is ready.
    pub fn step(&mut self, budget: usize) -> bool {
        for _ in 0..budget {
            if self.next >= self.tasks.len() {
                break;
            }
            let (kind, angle) = self.tasks[self.next];
            let azimuth = angle as f32 / ANGLES as f32 * std::f32::consts::TAU;
            let sprite = bake_sprite(canonical_prop(kind), azimuth, &self.light);
            match kind {
                0 => self.pines[angle] = Some(sprite),
                1 => self.cottages[angle] = Some(sprite),
                2 => self.lighthouses[angle] = Some(sprite),
                3 => self.boulders[angle] = Some(sprite),
                _ => self.docks[angle] = Some(sprite),
            }
            self.next += 1;
        }
        self.next >= self.tasks.len()
    }

    /// Collects the finished sprites. Only valid once [`BakeJob::step`] is done.
    pub fn finish(self) -> Option<SpriteBank> {
        let unwrap_all = |v: Vec<Option<Sprite>>| v.into_iter().collect::<Option<Vec<_>>>();
        Some(SpriteBank {
            pines: unwrap_all(self.pines)?,
            cottages: unwrap_all(self.cottages)?,
            lighthouses: unwrap_all(self.lighthouses)?,
            boulders: unwrap_all(self.boulders)?,
            docks: unwrap_all(self.docks)?,
        })
    }
}

fn bake_sprite(prop: Prop, azimuth: f32, light: &Light) -> Sprite {
    let solid = Solid::new(v3(0.0, 0.0, 0.0), prop);
    let radius = solid.radius;
    let distance = radius * 2.9;
    let tan_half = radius * 1.15 / distance;

    let center = solid.center;
    let cam = center + v3(azimuth.sin() * distance, 0.0, azimuth.cos() * distance);
    let forward = (center - cam).normalized();
    let right = forward.cross(Vec3::UP).normalized();
    let up = right.cross(forward);

    let size = SPRITE_SIZE;
    let mut linear = vec![0.0f32; size * size * 3];
    let mut mask = vec![0u8; size * size];
    let mut delta = vec![0.0f32; size * size];

    for py in 0..size {
        let ndc_y = 1.0 - 2.0 * (py as f32 + 0.5) / size as f32;
        for px in 0..size {
            let ndc_x = 2.0 * (px as f32 + 0.5) / size as f32 - 1.0;
            let dir = (forward + right * (ndc_x * tan_half) + up * (ndc_y * tan_half)).normalized();

            let mut t = (distance - radius * 1.6).max(0.0);
            let max_t = distance + radius * 1.6;
            let mut hit = false;
            for _ in 0..80 {
                let d = solid.primitive_distance(cam + dir * t);
                if d < 0.0022 {
                    hit = true;
                    break;
                }
                t += d.max(0.0035);
                if t > max_t {
                    break;
                }
            }
            if !hit {
                continue;
            }

            let p = cam + dir * t;
            let n = prop_normal(&solid, p);
            let mat = solid.material(p);
            let albedo = mat.albedo();
            let grain = 0.92 + 0.16 * fbm(p.x * 7.0, p.z * 7.0, 2, 55);
            let ndl = n.dot(light.sun_dir).max(0.0);
            let sky = light.sky.lerp(light.zenith, 0.5 + 0.5 * n.y) * light.ambient;
            let mut colour = albedo.mul_vec(light.sun_color) * (ndl * 0.95 + 0.05);
            colour += albedo.mul_vec(sky) * (0.72 + 0.28 * n.y);
            colour += mat.emission();

            // Contact darkening near the ground: without it a billboard looks
            // pasted onto the grass instead of standing on it.
            let ground_depth = p.y / 0.45;
            let contact = 0.55 + 0.45 * smoothstep(0.0, 1.0, ground_depth);
            colour = colour * grain * light.exposure * contact;

            let idx = py * size + px;
            linear[idx * 3] = colour.x;
            linear[idx * 3 + 1] = colour.y;
            linear[idx * 3 + 2] = colour.z;
            mask[idx] = 1;
            delta[idx] = t - distance;
        }
    }

    Sprite {
        size,
        linear,
        mask,
        delta,
    }
}

fn prop_normal(solid: &Solid, p: Vec3) -> Vec3 {
    let e = 0.004;
    let d = |q: Vec3| solid.primitive_distance(q);
    v3(
        d(p + v3(e, 0.0, 0.0)) - d(p - v3(e, 0.0, 0.0)),
        d(p + v3(0.0, e, 0.0)) - d(p - v3(0.0, e, 0.0)),
        d(p + v3(0.0, 0.0, e)) - d(p - v3(0.0, 0.0, e)),
    )
    .normalized()
}

impl SpriteBank {
    fn for_prop(&self, prop: Prop) -> &Vec<Sprite> {
        match prop {
            Prop::Pine { .. } => &self.pines,
            Prop::Cottage { .. } => &self.cottages,
            Prop::Lighthouse => &self.lighthouses,
            Prop::Boulder { .. } => &self.boulders,
            Prop::Dock { .. } => &self.docks,
        }
    }
}

// ---------------------------------------------------------------------------
// Shading helpers shared by the bake and the ray marcher
// ---------------------------------------------------------------------------

fn terrain_light(world: &World, light: Light, x: f32, z: f32, h: f32, n: Vec3) -> Vec3 {
    let patch = fbm(x * 0.45, z * 0.45, 2, 21);
    let tint = noise2(x * 3.0, z * 3.0, 7);
    let grit = noise2(x * 11.0, z * 11.0, 133);

    let grass = v3(0.055, 0.175, 0.048).lerp(v3(0.155, 0.235, 0.055), tint * 0.85);
    let sand = v3(0.60, 0.50, 0.32) * (0.86 + 0.24 * tint);
    let rock = v3(0.19, 0.18, 0.17).lerp(v3(0.33, 0.31, 0.29), patch);

    let slope = 1.0 - n.y;
    let mut albedo = grass;
    albedo = albedo.lerp(sand, smoothstep(0.42, 0.06, h));
    albedo = albedo.lerp(rock, smoothstep(0.42, 0.85, slope) * smoothstep(0.05, 0.55, h));
    albedo = albedo.lerp(v3(0.30, 0.28, 0.26), smoothstep(2.30, 3.10, h) * 0.85);
    albedo = albedo * (0.90 + 0.20 * grit);

    let ndl = n.dot(light.sun_dir).max(0.0);
    let sky = light.sky.lerp(light.zenith, 0.5 + 0.5 * n.y) * light.ambient;

    // A cheap occlusion term: is there high ground towards the sun?
    let probe = 0.9;
    let towards_sun = world
        .smooth_height
        .sample(x + light.sun_dir.x * probe, z + light.sun_dir.z * probe);
    let rise = (towards_sun - h) / probe;
    let sun_up = light.sun_dir.y.max(0.05);
    let shadow = smoothstep(sun_up * 0.4, sun_up * 1.5, rise);

    let mut colour = albedo.mul_vec(light.sun_color) * (ndl * (1.0 - shadow) * 0.98 + 0.02);
    colour += albedo.mul_vec(sky) * (0.72 + 0.28 * n.y);

    // Warm light spilling out of the lighthouse lantern at night.
    if light.lantern > 0.0 {
        let d = light.lantern_pos - v3(x, h, z);
        let dist2 = d.dot(d).max(0.5);
        let nd = n.dot(d.normalized()).max(0.0);
        let warm = light.lantern * 0.06 / (1.0 + dist2 * 0.35);
        colour += albedo.mul_vec(v3(1.0, 0.66, 0.30)) * (nd * warm);
    }

    colour * light.exposure
}

fn sky_colour(light: Light, dir: Vec3) -> Vec3 {
    let up = dir.y.max(0.0);
    let grad = smoothstep(-0.02, 0.75, up).powf(0.8);
    let mut colour = light.sky.lerp(light.zenith, grad);

    // The sun (or moon) itself is drawn per pixel: a disc sharper than a
    // lookup-table cell turns into a foggy blob once the table is sampled.
    let mu = dir.dot(light.sun_dir).max(0.0);
    colour += light.sun_color * (mu.powf(8.0) * 0.035);

    colour * light.exposure
}

/// One star per cell of a spherical grid, most cells empty.
///
/// Stars are drawn per pixel rather than baked into the sky lookup table: a
/// table cell is far wider than a star, so the whole sky washes out grey.
fn stars(dir: Vec3) -> Vec3 {
    let scale = 620.0;
    let p = dir * scale;
    let (ix, iy, iz) = (
        p.x.floor() as i32,
        p.y.floor() as i32,
        p.z.floor() as i32,
    );
    let h = hash3(ix, iy, iz);
    if h < 0.988 {
        return Vec3::ZERO;
    }

    // Jitter inside the cell so the field does not read as a grid.
    let jitter = v3(
        hash3(ix.wrapping_add(11), iy, iz),
        hash3(ix, iy.wrapping_add(23), iz),
        hash3(ix, iy, iz.wrapping_add(37)),
    );
    let center = (v3(ix as f32, iy as f32, iz as f32) + jitter) / scale;
    let d = dir.dot(center.normalized()).clamp(0.0, 1.0);
    let bright = smoothstep(0.99990, 0.99999, d) * (0.25 + (h - 0.988) * 42.0);
    v3(0.92, 0.94, 1.0) * (bright * 1.6)
}

fn hash3(ix: i32, iy: i32, iz: i32) -> f32 {
    let mut h = (ix as u32)
        .wrapping_mul(0x8da6_b343)
        ^ (iy as u32).wrapping_mul(0xd816_3841)
        ^ (iz as u32).wrapping_mul(0xcb1a_b31f);
    h ^= h >> 16;
    h = h.wrapping_mul(0x7feb_352d);
    h ^= h >> 15;
    h = h.wrapping_mul(0x846c_a68b);
    h ^= h >> 16;
    (h & 0x00ff_ffff) as f32 / 16_777_215.0
}

// ---------------------------------------------------------------------------
// The interactive world
// ---------------------------------------------------------------------------

pub struct WalkWorld {
    pub world: World,
    /// Coarser height field: the same island, cheaper to sample.
    pub field: HeightField,
    pub colour: ColourMap,
    pub sky: SkyLut,
    pub tone: ToneLut,
    pub bank: SpriteBank,
    pub light: Light,
}

impl WalkWorld {
    /// Builds everything except the sprite bank, which is baked in steps.
    pub fn new(seed: u32, scene: &Scene) -> (WalkWorld, BakeJob) {
        let world = World::build(seed);
        let light = Light::from_scene(scene, world.lantern_pos);
        let extent = world.height_extent();
        let field = HeightField::build(seed, 700, extent, true);
        let colour = ColourMap::build(&world, light, 448);
        let sky = SkyLut::build(light, 256, 160);
        let tone = ToneLut::build();
        let job = BakeJob::new(light);
        (
            WalkWorld {
                world,
                field,
                colour,
                sky,
                tone,
                bank: SpriteBank {
                    pines: Vec::new(),
                    cottages: Vec::new(),
                    lighthouses: Vec::new(),
                    boulders: Vec::new(),
                    docks: Vec::new(),
                },
                light,
            },
            job,
        )
    }

    pub fn install(&mut self, bank: SpriteBank) {
        self.bank = bank;
    }

    pub fn ground(&self, x: f32, z: f32) -> f32 {
        self.field.sample(x, z)
    }

    /// Deep-water check used by the player.
    pub fn is_walkable(&self, x: f32, z: f32) -> bool {
        self.field.sample(x, z) > WATER_LEVEL - 0.35
    }
}

// ---------------------------------------------------------------------------
// Frame buffer
// ---------------------------------------------------------------------------

pub struct Frame {
    pub width: usize,
    pub height: usize,
    /// sRGB bytes, ready for an image.
    pub rgb: Vec<u8>,
    /// Linear light, three floats per pixel.
    linear: Vec<f32>,
    depth: Vec<f32>,
}

impl Frame {
    pub fn new(width: usize, height: usize) -> Frame {
        Frame {
            width,
            height,
            rgb: vec![0; width * height * 3],
            linear: vec![0.0; width * height * 3],
            depth: vec![f32::INFINITY; width * height],
        }
    }

    pub fn resize(&mut self, width: usize, height: usize) {
        if self.width != width || self.height != height {
            self.width = width;
            self.height = height;
            self.rgb = vec![0; width * height * 3];
            self.linear = vec![0.0; width * height * 3];
            self.depth = vec![f32::INFINITY; width * height];
        }
    }
}

// ---------------------------------------------------------------------------
// Rendering
// ---------------------------------------------------------------------------

/// Vertical field of view of the walk camera, in radians.
pub const FOV_Y: f32 = 1.18;

pub fn render(walk: &WalkWorld, player: &Player, frame: &mut Frame) {
    render_inner(walk, player, frame, false);
}

/// Same as [`render`], with a timing breakdown printed (used by `--walk-frame`).
pub fn render_profiled_for_cli(walk: &WalkWorld, player: &Player, frame: &mut Frame) {
    render_inner(walk, player, frame, true);
}

fn render_inner(walk: &WalkWorld, player: &Player, frame: &mut Frame, profile: bool) {
    // `std::time` is not available on wasm32-unknown-unknown, so timing only
    // exists in the native build.
    #[cfg(not(target_arch = "wasm32"))]
    let began = std::time::Instant::now();
    #[cfg(target_arch = "wasm32")]
    let _began = ();
    let w = frame.width;
    let h = frame.height;
    if w == 0 || h == 0 {
        return;
    }

    let light = walk.light;
    let eye = player.eye();
    let focal = (h as f32 * 0.5) / (FOV_Y * 0.5).tan();
    let horizon = (h as f32 * 0.5 + player.pitch.tan() * focal)
        .clamp(-(h as f32) * 2.0, h as f32 * 3.0);
    let sun_azimuth = light.sun_dir.x.atan2(-light.sun_dir.z);

    frame.depth.iter_mut().for_each(|d| *d = f32::INFINITY);

    // Sun azimuth relative to the view, used by the sky lookup.
    for x in 0..w {
        let offset = (x as f32 + 0.5) - w as f32 * 0.5;
        let ang = player.yaw + (offset / focal).atan();
        let (sin_a, cos_a) = ang.sin_cos();
        let dir_x = sin_a;
        let dir_z = -cos_a;
        let sky_azimuth = (ang - sun_azimuth).rem_euclid(std::f32::consts::TAU);

        let mut ymin = h;
        let mut t = 0.28f32;

        while t < 105.0 {
            let px = eye.x + dir_x * t;
            let pz = eye.z + dir_z * t;
            let ground = walk.field.sample(px, pz);
            let surface = ground.max(WATER_LEVEL);
            let dy = eye.y - surface;
            if dy > 0.0 {
                let row = horizon + dy * focal / t;
                if row < ymin as f32 {
                    let top = row.max(0.0) as usize;
                    let bottom = ymin.min(h);
                    if top < bottom {
                        if ground < WATER_LEVEL {
                            paint_water(walk, eye, dir_x, dir_z, t, x, top, bottom, frame);
                        } else {
                            let colour =
                                fog(walk, walk.colour.sample(px, pz), t);
                            paint_column(frame, x, top, bottom, colour, t);
                        }
                    }
                    ymin = top;
                    if ymin == 0 {
                        break;
                    }
                }
            }
            // Geometric stepping keeps a roughly uniform density on screen.
            t *= 1.0125;
            t += 0.005;
        }

        // Everything above the first hit is sky.
        for y in 0..ymin {
            let ndc_y = (horizon - y as f32) / focal;
            let elevation = ndc_y.atan();
            let mut colour = walk
                .sky
                .sample(elevation, sky_azimuth + std::f32::consts::PI);
            if light.stars > 0.0 || light.sun_dir.y > -0.1 {
                let (sin_e, cos_e) = elevation.sin_cos();
                let dir = v3(cos_e * sin_a, sin_e, -cos_e * cos_a);
                if light.stars > 0.0 {
                    colour += stars(dir) * (light.stars * smoothstep(0.0, 0.18, sin_e));
                }
                // Sun or moon: a crisp disc with a soft halo around it.
                let mu = dir.dot(light.sun_dir).max(0.0);
                let disc = smoothstep(0.99986, 0.99994, mu);
                colour += light.sun_color * (disc * 6.0);
            }
            let idx = (y * w + x) * 3;
            frame.linear[idx] = colour.x;
            frame.linear[idx + 1] = colour.y;
            frame.linear[idx + 2] = colour.z;
        }
    }

    #[cfg(not(target_arch = "wasm32"))]
    let terrain_done = began.elapsed();

    // ---- props as billboards --------------------------------------------
    let forward = player.forward();
    let right = player.right();
    let up = Vec3::UP;

    let mut visible: Vec<(f32, &Solid)> = Vec::new();
    for solid in walk.world.solids.iter() {
        let d = solid.center - eye;
        let cz = d.dot(forward);
        if cz <= 0.2 {
            continue;
        }
        if d.length() - solid.radius > 90.0 {
            continue;
        }
        visible.push((cz, solid));
    }
    visible.sort_by(|a, b| b.0.partial_cmp(&a.0).unwrap_or(std::cmp::Ordering::Equal));
    let props_total = visible.len();

    for (_, solid) in visible {
        draw_prop(frame, walk, solid, eye, forward, right, up, focal, horizon);
    }

    #[cfg(not(target_arch = "wasm32"))]
    let props_done = began.elapsed();

    // ---- tone mapping ----------------------------------------------------
    for (i, chunk) in frame.linear.chunks_exact(3).enumerate() {
        frame.rgb[i * 3] = walk.tone.map(chunk[0]);
        frame.rgb[i * 3 + 1] = walk.tone.map(chunk[1]);
        frame.rgb[i * 3 + 2] = walk.tone.map(chunk[2]);
    }

    #[cfg(not(target_arch = "wasm32"))]
    if profile {
        println!(
            "profile: terrain+sky {:.1} ms, props {:.1} ms ({} visible), tone {:.1} ms",
            terrain_done.as_secs_f64() * 1000.0,
            (props_done - terrain_done).as_secs_f64() * 1000.0,
            props_total,
            (began.elapsed() - props_done).as_secs_f64() * 1000.0
        );
    }
    #[cfg(target_arch = "wasm32")]
    let _ = (profile, props_total);
}

#[inline]
fn paint_column(frame: &mut Frame, x: usize, top: usize, bottom: usize, colour: Vec3, depth: f32) {
    let w = frame.width;
    for y in top..bottom {
        let idx = (y * w + x) * 3;
        frame.linear[idx] = colour.x;
        frame.linear[idx + 1] = colour.y;
        frame.linear[idx + 2] = colour.z;
        frame.depth[y * w + x] = depth;
    }
}

#[allow(clippy::too_many_arguments)]
fn paint_water(
    walk: &WalkWorld,
    eye: Vec3,
    dir_x: f32,
    dir_z: f32,
    t: f32,
    x: usize,
    top: usize,
    bottom: usize,
    frame: &mut Frame,
) {
    let light = walk.light;
    let px = eye.x + dir_x * t;
    let pz = eye.z + dir_z * t;
    let depth = (WATER_LEVEL - walk.field.sample(px, pz)).max(0.0);
    let deep = smoothstep(0.0, 0.95, depth);
    // Shallows take their colour from the preset, so night water is not
    // tropical turquoise.
    let sunlight = light.sun_dir.y.max(0.0);
    let mut base = light
        .sea_shallow
        .lerp(light.sea_deep, deep)
        * (0.75 + 0.55 * sunlight);

    // Grazing angles mirror the sky, which is what makes the far sea pale.
    let mirror = smoothstep(5.0, 45.0, t) * 0.38;
    base = base.lerp(light.sky, mirror);

    // The sandy bottom shows through the shallows.
    let seabed = 1.0 - smoothstep(0.0, 0.50, depth);
    base = base.lerp(v3(0.30, 0.29, 0.20) * (0.55 + 0.45 * sunlight), seabed * 0.45);

    // Sun glitter reflecting off a flat surface.
    let dy = (WATER_LEVEL - eye.y) / t;
    let inv = 1.0 / (dir_x * dir_x + dy * dy + dir_z * dir_z).sqrt();
    let reflected = v3(-dir_x * inv, dy * inv, -dir_z * inv);
    let spec = reflected.dot(light.sun_dir).max(0.0).powf(70.0);
    let shimmer = 0.45 + 0.55 * fbm(px * 1.6, pz * 1.6, 2, 63);
    base += light.sun_color * (spec * shimmer * 2.6);

    // Foam along the shoreline, broken up so it is not a clean band.
    let foam_band = smoothstep(0.20, 0.015, depth);
    if foam_band > 0.0 {
        let ripple = fbm(px * 2.4, pz * 2.4, 2, 77);
        let foam = foam_band * smoothstep(0.35, 0.62, ripple);
        base = base.lerp(v3(0.80, 0.85, 0.87), foam * 0.75);
    }

    let colour = fog(walk, base * light.exposure, t);
    paint_column(frame, x, top, bottom, colour, t);
}

fn fog(walk: &WalkWorld, colour: Vec3, t: f32) -> Vec3 {
    let f = (1.0 - (-t * walk.light.haze * 1.3).exp()).clamp(0.0, 1.0);
    colour.lerp(walk.light.sky * walk.light.exposure, f)
}

#[allow(clippy::too_many_arguments)]
fn draw_prop(
    frame: &mut Frame,
    walk: &WalkWorld,
    solid: &Solid,
    eye: Vec3,
    forward: Vec3,
    right: Vec3,
    up: Vec3,
    focal: f32,
    horizon: f32,
) {
    let light = walk.light;
    let to_center = solid.center - eye;
    let cz = to_center.dot(forward);
    if cz <= 0.25 {
        return;
    }
    let cx = to_center.dot(right);
    let cy = to_center.dot(up);

    let canonical = Solid::new(v3(0.0, 0.0, 0.0), canonical_of(solid.prop));
    let scale = solid.radius / canonical.radius;

    let half = focal * solid.radius * 1.15 / cz;
    if half < 0.75 {
        return;
    }
    let screen_cx = frame.width as f32 * 0.5 + cx / cz * focal;
    let screen_cy = horizon - cy / cz * focal;

    let x0 = (screen_cx - half).floor().max(0.0) as usize;
    let x1 = ((screen_cx + half).ceil() as isize).clamp(0, frame.width as isize) as usize;
    let y0 = (screen_cy - half).floor().max(0.0) as usize;
    let y1 = ((screen_cy + half).ceil() as isize).clamp(0, frame.height as isize) as usize;
    if x1 <= x0 || y1 <= y0 {
        return;
    }

    let prop_yaw = match solid.prop {
        Prop::Cottage { yaw } | Prop::Dock { yaw } => yaw,
        _ => 0.0,
    };
    let view = (eye - solid.center).normalized();
    let azimuth = view.x.atan2(view.z) - prop_yaw;
    let bank = walk.bank.for_prop(solid.prop);
    let count = bank.len() as f32;
    if count == 0.0 {
        return;
    }
    let mut index = (azimuth / std::f32::consts::TAU * count).round() as i32;
    index = index.rem_euclid(count as i32);
    let sprite = &bank[index as usize];

    let fog_amount = (1.0 - (-cz * light.haze * 1.3).exp()).clamp(0.0, 1.0);
    let fog_colour = light.sky * light.exposure;
    let size = sprite.size as f32;
    let step = 2.0 * half / size;
    let inv_step = 1.0 / step;
    let left = screen_cx - half;
    let top_screen = screen_cy - half;
    let w = frame.width;

    for y in y0..y1 {
        let sy = ((y as f32 + 0.5 - top_screen) * inv_step) as usize;
        if sy >= sprite.size {
            continue;
        }
        let row = sy * sprite.size;
        let mut pixel = y * w + x0;
        let mut sx_pos = (x0 as f32 + 0.5 - left) * inv_step;
        for _ in x0..x1 {
            let sx = sx_pos as usize;
            let sidx = row + sx.min(sprite.size - 1);
            if sprite.mask[sidx] == 0 {
                sx_pos += inv_step;
                pixel += 1;
                continue;
            }
            let prop_depth = cz + sprite.delta[sidx] * scale;
            if frame.depth[pixel] <= prop_depth {
                sx_pos += inv_step;
                pixel += 1;
                continue;
            }
            let mut colour = v3(
                sprite.linear[sidx * 3],
                sprite.linear[sidx * 3 + 1],
                sprite.linear[sidx * 3 + 2],
            );
            if fog_amount > 0.0 {
                colour = colour.lerp(fog_colour, fog_amount);
            }
            let base = pixel * 3;
            frame.linear[base] = colour.x;
            frame.linear[base + 1] = colour.y;
            frame.linear[base + 2] = colour.z;
            frame.depth[pixel] = prop_depth;
            sx_pos += inv_step;
            pixel += 1;
        }
    }
}

fn canonical_of(prop: Prop) -> Prop {
    match prop {
        Prop::Pine { .. } => canonical_prop(0),
        Prop::Cottage { .. } => canonical_prop(1),
        Prop::Lighthouse => canonical_prop(2),
        Prop::Boulder { .. } => canonical_prop(3),
        Prop::Dock { .. } => canonical_prop(4),
    }
}

/// Kept for material work in the fast path.
#[allow(dead_code)]
fn material_tint(mat: Mat) -> Vec3 {
    mat.albedo()
}

#[cfg(test)]
mod tests {
    use super::*;

    fn day_world(seed: u32) -> WalkWorld {
        let scene = crate::presets::find("day").scene;
        let (mut walk, mut job) = WalkWorld::new(seed, &scene);
        job.step(job.total());
        walk.install(job.finish().expect("sprite bake"));
        walk
    }

    /// The first thing a player does is press W. If the spawn point is boxed
    /// in by a prop or a slope, the world looks fine and is still unplayable.
    #[test]
    fn player_can_walk_away_from_the_spawn() {
        let walk = day_world(7);
        let mut player = Player::spawn(&walk.world);
        let start = player.pos;
        let input = Input {
            forward: true,
            ..Input {
                forward: false,
                back: false,
                left: false,
                right: false,
                run: false,
                jump: false,
            }
        };
        for _ in 0..120 {
            player.update(&walk.world, &input, 1.0 / 60.0);
        }
        let moved = (player.pos - start).length();
        assert!(moved > 1.0, "player only moved {moved:.2} m from {start:?}");
    }

    /// Props are seated on the lowest ground of their footprint and carry a
    /// plinth, so a building must never be floating above the terrain.
    #[test]
    fn buildings_are_seated_on_the_ground() {
        let walk = day_world(7);
        for solid in walk.world.solids.iter() {
            // The dock is a pier over the water and is lifted on purpose.
            if !matches!(solid.prop, Prop::Cottage { .. } | Prop::Lighthouse) {
                continue;
            }
            let mut low = f32::INFINITY;
            for dx in [-0.5f32, 0.0, 0.5] {
                for dz in [-0.5f32, 0.0, 0.5] {
                    low = low.min(walk.world.height.sample(
                        solid.anchor.x + dx,
                        solid.anchor.z + dz,
                    ));
                }
            }
            assert!(
                solid.anchor.y <= low + 0.02,
                "prop {:?} sits {:.2} m above the ground around it",
                solid.prop,
                solid.anchor.y - low
            );
        }
    }
}
