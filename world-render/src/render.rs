//! Ray marcher, shading model, atmosphere and tone mapping.

use crate::camera::{sample_offsets, Camera, Rng};
use crate::vec3::{v3, Vec3};
use crate::world::{fbm, fbm3, smoothstep, World, WATER_LEVEL};

pub struct Scene {
    /// Direction *towards* the sun (or moon), normalised.
    pub sun_dir: Vec3,
    pub sun_color: Vec3,
    pub sky_zenith: Vec3,
    pub sky_horizon: Vec3,
    pub sun_glow: Vec3,
    pub cloud_color: Vec3,
    pub sea_deep: Vec3,
    pub sea_shallow: Vec3,
    pub exposure: f32,
    /// Exponential haze density, per world unit.
    pub haze: f32,
    pub star_amount: f32,
    pub ambient: f32,
    /// Warm point light in the lighthouse lantern.
    pub lantern: f32,
    /// Animatable offset used by clouds and ripples.
    pub time: f32,
}

#[derive(Clone, Copy)]
pub struct RenderSettings {
    pub width: usize,
    pub height: usize,
    pub samples: u32,
}

const MAX_DIST: f32 = 70.0;

// ---------------------------------------------------------------------------
// Atmosphere
// ---------------------------------------------------------------------------

fn hash3(ix: i32, iy: i32, iz: i32, seed: u32) -> f32 {
    let mut h = (ix as u32).wrapping_mul(0x8da6_b343)
        ^ (iy as u32).wrapping_mul(0xd816_3841)
        ^ (iz as u32).wrapping_mul(0xcb1a_b31f)
        ^ seed.wrapping_mul(0x9e37_79b9);
    h ^= h >> 16;
    h = h.wrapping_mul(0x7feb_352d);
    h ^= h >> 15;
    h = h.wrapping_mul(0x846c_a68b);
    h ^= h >> 16;
    (h & 0x00ff_ffff) as f32 / 16_777_215.0
}

fn stars(dir: Vec3) -> Vec3 {
    let scale = 320.0;
    let p = dir * scale;
    let ix = p.x.floor() as i32;
    let iy = p.y.floor() as i32;
    let iz = p.z.floor() as i32;
    let h = hash3(ix, iy, iz, 4242);
    if h < 0.994 {
        return Vec3::ZERO;
    }
    let jitter = v3(
        hash3(ix, iy, iz, 11),
        hash3(ix, iy, iz, 12),
        hash3(ix, iy, iz, 13),
    );
    let center = (v3(ix as f32, iy as f32, iz as f32) + jitter) / scale;
    let d = dir.dot(center.normalized()).clamp(0.0, 1.0);
    // A soft halo, so stars stay uniform instead of grid-aligned blotches.
    let bright = smoothstep(0.99990, 0.99999, d) * (0.25 + (h - 0.994) * 60.0);
    v3(0.95, 0.96, 1.0) * (bright * 1.5)
}

/// Colour of the sky in a direction (also used for water reflections).
pub fn sky_colour(scene: &Scene, dir: Vec3) -> Vec3 {
    let up = dir.y.max(0.0);
    let grad = smoothstep(-0.02, 0.70, up).powf(0.8);
    let mut colour = scene.sky_horizon.lerp(scene.sky_zenith, grad);

    let mu = dir.dot(scene.sun_dir).max(0.0);
    colour += scene.sun_glow * (mu.powf(5.0) * 0.35 + mu.powf(40.0) * 0.9);

    // Cloud deck, projected on a plane above the island.
    if dir.y > 0.012 {
        let t = 1.0 / (dir.y + 0.11);
        let cx = dir.x * t * 0.62 + scene.time * 0.010;
        let cz = dir.z * t * 0.58 - scene.time * 0.006;
        let n = fbm(cx * 1.15 + 3.0, cz * 1.15 - 5.0, 5, 91);
        let cover = smoothstep(0.50, 0.78, n) * smoothstep(0.015, 0.14, dir.y);
        let lit = 0.55 + 0.45 * mu.powf(2.0);
        let cloud = scene.cloud_color * lit + scene.sun_glow * mu.powf(8.0) * 0.35;
        colour = colour.lerp(cloud, cover * 0.92);
    }

    if scene.star_amount > 0.0 && dir.y > -0.02 {
        colour += stars(dir) * scene.star_amount * smoothstep(-0.02, 0.16, dir.y);
    }

    let disc = smoothstep(0.99960, 0.99984, mu);
    colour += scene.sun_glow * disc * 26.0;
    colour
}

// ---------------------------------------------------------------------------
// Lighting helpers
// ---------------------------------------------------------------------------

/// Shadow test towards the sun.
///
/// Terrain and props are handled differently on purpose: a height field is
/// exact and cheap to sample, while props are only available as a distance
/// field. Mixing the two in one march made the soft factor collapse on the
/// first small step, which painted whole hillsides as shadowed.
fn sun_visibility(world: &World, p: Vec3, dir: Vec3, _on_terrain: bool) -> f32 {
    // Nudge the origin off the surface: the SDF normal is not exact along
    // CSG seams (cone tiers, roof slabs), so a zero offset self-shadows.
    let origin = p + dir * 0.02;

    // Terrain occlusion: an analytic ray march over the height field.
    let mut soft = 1.0f32;
    let mut t = 0.30;
    while t < 26.0 {
        let q = origin + dir * t;
        let clearance = q.y - world.height.sample(q.x, q.z);
        if clearance < 0.0 {
            return 0.0;
        }
        soft = soft.min((6.0 * clearance / t).clamp(0.0, 1.0));
        if soft < 0.02 {
            return 0.0;
        }
        t += 0.10 + t * 0.05 + clearance * 0.35;
    }

    // Props occlusion: a distance-field march, skipped when the ray leaves the
    // props' bounding box without coming near anything.
    if world.props_box_distance(origin) < 14.0 {
        let mut res = 1.0f32;
        let mut t = 0.12;
        for _ in 0..96 {
            if world.props_box_distance(origin + dir * t) > 8.0 + t * 0.25 {
                break;
            }
            let d = world.props_exact(origin + dir * t);
            if d < 0.006 {
                return 0.0;
            }
            res = res.min(2.5 * d / t);
            if res < 0.03 {
                return 0.0;
            }
            t += d.clamp(0.03, 0.9);
            if t > 15.0 {
                break;
            }
        }
        soft = soft.min(res);
    }

    soft.clamp(0.0, 1.0)
}

/// Direct sun, sky ambient, a glossy highlight and the lantern point light.
#[allow(clippy::too_many_arguments)]
fn surface_light(
    world: &World,
    scene: &Scene,
    p: Vec3,
    n: Vec3,
    albedo: Vec3,
    specular: f32,
    rd: Vec3,
    ao: f32,
    on_terrain: bool,
) -> Vec3 {
    let sun = scene.sun_dir;
    let ndl = n.dot(sun).max(0.0);
    let shadow = if ndl > 0.0 {
        sun_visibility(world, p, sun, on_terrain)
    } else {
        0.0
    };

    let mut colour = albedo.mul_vec(scene.sun_color) * (ndl * shadow * (0.55 + 0.45 * ao));

    // Hemisphere ambient from the sky plus a warm bounce from the ground.
    let sky_amb = scene.sky_horizon.lerp(scene.sky_zenith, 0.5 + 0.5 * n.y) * scene.ambient;
    colour += albedo.mul_vec(sky_amb + v3(0.02, 0.03, 0.05)) * (0.65 + 0.35 * n.y) * ao;

    // Glossy sun highlight.
    if specular > 0.0 {
        let h = (sun - rd).normalized();
        let spec = n.dot(h).max(0.0).powf(60.0) * specular * shadow;
        colour += scene.sun_color * spec;
    }

    // Lantern point light.
    let to_lantern = world.lantern_pos - p;
    let dist2 = to_lantern.dot(to_lantern).max(1e-4);
    let ldir = to_lantern.normalized();
    let atten = scene.lantern / (1.0 + dist2 * 1.8);
    colour += albedo.mul_vec(v3(1.0, 0.66, 0.32)) * (n.dot(ldir).max(0.0) * atten * ao);

    colour
}

/// Cheap ambient occlusion from the distance field: darkens creases, the
/// ground under trees, and every contact area that has no bounce light.
fn ambient_occlusion(world: &World, p: Vec3, n: Vec3) -> f32 {
    let mut occ = 0.0;
    let mut weight = 1.0;
    for i in 1..=6 {
        // Large radii on purpose: smaller ones would pick up the fine terrain
        // detail and paint the hillsides with noise bands.
        let h = 0.05 + 0.085 * i as f32 * i as f32;
        let d = world.distance(p + n * h);
        occ += ((h - d).max(0.0) / h) * weight;
        weight *= 0.62;
    }
    (1.0 - 0.55 * occ).clamp(0.25, 1.0)
}

fn apply_haze(scene: &Scene, colour: Vec3, rd: Vec3, t: f32) -> Vec3 {
    let f = (1.0 - (-t * scene.haze).exp()).clamp(0.0, 1.0);
    let horizon = scene.sky_horizon.lerp(scene.sea_deep, 0.15);
    let glow = scene.sun_glow * rd.dot(scene.sun_dir).max(0.0).powf(6.0) * 0.8;
    colour.lerp(horizon + glow, f)
}

// ---------------------------------------------------------------------------
// Surfaces
// ---------------------------------------------------------------------------

fn terrain_albedo(world: &World, p: Vec3) -> Vec3 {
    let h = world.height.sample(p.x, p.z);
    // Slope from a wide stencil: material masks must not follow every ripple of
    // the fine detail, or the hillsides turn into marbled bands.
    let wide = world.smooth_height.normal_wide(p.x, p.z, 0.35);
    let slope = 1.0 - wide.y;
    let tint = fbm(p.x * 3.1, p.z * 3.1, 3, 7);
    let patch = fbm(p.x * 0.45 - 9.0, p.z * 0.45 + 4.0, 3, 21);
    let grit = fbm(p.x * 11.0 + 2.0, p.z * 11.0 - 6.0, 2, 133);

    let grass = v3(0.055, 0.175, 0.048).lerp(
        v3(0.155, 0.235, 0.055),
        (tint * 0.65 + patch * 0.35).clamp(0.0, 1.0),
    );
    let sand = v3(0.60, 0.50, 0.32) * (0.88 + 0.22 * tint);
    let rock = v3(0.16, 0.155, 0.145).lerp(v3(0.34, 0.32, 0.29), patch);

    let mut c = grass;
    c = c.lerp(sand, smoothstep(0.42, 0.06, h));
    c = c.lerp(
        rock,
        smoothstep(0.42, 0.85, slope) * smoothstep(0.05, 0.55, h),
    );
    // High ground turns rocky anyway: gives the summit a different character.
    c = c.lerp(
        rock,
        smoothstep(1.55, 2.75, h) * 0.75 * smoothstep(0.12, 0.30, slope),
    );
    // The summit turns to bare rock.
    c = c.lerp(v3(0.30, 0.28, 0.26), smoothstep(2.30, 3.10, h) * 0.85);
    // Fine grain: breaks the "inflated clay" look of a smooth field.
    c * (0.86 + 0.28 * grit)
}

fn shade_solid(world: &World, scene: &Scene, p: Vec3, rd: Vec3, t: f32) -> Vec3 {
    let terrain = world.is_terrain(p);
    let n = if terrain {
        world
            .smooth_height
            .shading_normal(&world.height, p.x, p.z, 0.5)
    } else {
        world.props_normal(p)
    };

    let (albedo, specular, emission) = if terrain {
        (terrain_albedo(world, p), 0.02, Vec3::ZERO)
    } else {
        // A fine, low-contrast grain; large blobs read as marble.
        let grain = 0.88 + 0.24 * fbm3(p.x * 9.0, p.y * 9.0, p.z * 9.0, 3, 55);
        let low = 0.92 + 0.16 * fbm3(p.x * 2.0 + 7.0, p.y * 2.0, p.z * 2.0, 2, 71);
        let mat = world.closest_material(p);
        (mat.albedo() * (grain * low), mat.specular(), mat.emission())
    };

    let ao = ambient_occlusion(world, p, n);
    let colour = surface_light(world, scene, p, n, albedo, specular, rd, ao, terrain);
    apply_haze(scene, colour + emission, rd, t)
}

fn shade_water(world: &World, scene: &Scene, p: Vec3, rd: Vec3, t: f32) -> Vec3 {
    let depth = (WATER_LEVEL - world.height.sample(p.x, p.z)).max(0.0);
    let distance_fade = 1.0 / (1.0 + t * 0.10);

    // Ripples from two scrolling noise fields.
    let e = 0.06;
    let time = scene.time;
    let ripple = |x: f32, z: f32| {
        fbm(x * 1.7 + time * 0.09, z * 1.7 - time * 0.07, 3, 31) * 0.9
            + fbm(x * 4.3 - time * 0.13, z * 4.3 + time * 0.11, 2, 47) * 0.35
    };
    let gx = (ripple(p.x + e, p.z) - ripple(p.x - e, p.z)) / (2.0 * e);
    let gz = (ripple(p.x, p.z + e) - ripple(p.x, p.z - e)) / (2.0 * e);
    let amp = 0.055 * distance_fade;
    let n = v3(-gx * amp, 1.0, -gz * amp).normalized();

    let water_colour = scene
        .sea_shallow
        .lerp(scene.sea_deep, smoothstep(0.0, 0.85, depth));
    let sunlight = scene.sun_dir.y.max(0.0);
    let mut colour = water_colour * (0.35 + 0.45 * sunlight);

    // Reflections and Fresnel.
    let cos_theta = (-rd).dot(n).max(0.0);
    let fresnel = 0.03 + 0.97 * (1.0 - cos_theta).powi(4);
    let reflected = sky_colour(scene, rd.reflect(n)).clamp_max(10.0);
    colour = colour.lerp(reflected, fresnel.clamp(0.0, 0.95));

    // Sun glitter, stretched along the view.
    let h = (scene.sun_dir - rd).normalized();
    let glint = n.dot(h).max(0.0).powf(260.0);
    let rough = 0.35 + 0.65 * fbm(p.x * 5.0 + 1.5, p.z * 5.0 - 2.5, 3, 63);
    let sparkle = 0.5 + 0.5 * fbm(p.x * 34.0, p.z * 34.0, 2, 91);
    colour += scene.sun_color * (glint * rough * sparkle * 42.0);

    // Foam where the water meets the beach.
    let shore = smoothstep(0.11, 0.015, depth);
    let foam_noise = fbm(p.x * 3.4 + time * 0.25, p.z * 3.4 - time * 0.2, 3, 77);
    let foam = shore * smoothstep(0.40, 0.70, foam_noise) * 0.80;
    let foam_color = v3(0.85, 0.89, 0.92) * (0.45 + 0.55 * sunlight);
    colour = colour.lerp(foam_color, foam);

    // Shallow water shows the sandy bottom.
    let bottom = smoothstep(0.45, 0.02, depth);
    colour = colour.lerp(
        scene.sea_shallow * 1.15 + v3(0.06, 0.05, 0.03),
        bottom * 0.45,
    );

    apply_haze(scene, colour, rd, t)
}

// ---------------------------------------------------------------------------
// Ray marching
// ---------------------------------------------------------------------------

fn trace(world: &World, scene: &Scene, origin: Vec3, rd: Vec3) -> Vec3 {
    let mut t = 0.03;
    let mut previous_t = 0.0;
    let mut hit = false;

    for _ in 0..512 {
        let p = origin + rd * t;

        // The height field's distance bound can be optimistic on a sharp ridge,
        // so a step may jump straight through the surface. Detect that and
        // bisect back to the crossing instead of losing the hit.
        let ground = world.height.sample(p.x, p.z);
        if p.y < ground {
            let (mut a, mut b) = (previous_t, t);
            for _ in 0..14 {
                let mid = 0.5 * (a + b);
                let q = origin + rd * mid;
                if q.y < world.height.sample(q.x, q.z) {
                    b = mid;
                } else {
                    a = mid;
                }
            }
            t = 0.5 * (a + b);
            hit = true;
            break;
        }

        let d = world.distance(p);
        let eps = 0.0009 + t * 0.0006;
        if d < eps {
            // A bounding volume can be close while the real geometry is not.
            if world.near_surface(p, eps * 4.0) {
                hit = true;
                break;
            }
            t += eps * 2.0;
            continue;
        }

        previous_t = t;
        t += d * 0.85;
        if t > MAX_DIST {
            break;
        }
    }

    // The ocean is an implicit plane, not part of the SDF.
    let water_t = if rd.y < -1e-4 && origin.y > WATER_LEVEL {
        Some((WATER_LEVEL - origin.y) / rd.y)
    } else {
        None
    };

    let solid_t = if hit && t <= MAX_DIST { Some(t) } else { None };

    match (solid_t, water_t) {
        (Some(st), Some(wt)) if wt < st => shade_water(world, scene, origin + rd * wt, rd, wt),
        (Some(st), _) => shade_solid(world, scene, origin + rd * st, rd, st),
        (None, Some(wt)) if wt < MAX_DIST => shade_water(world, scene, origin + rd * wt, rd, wt),
        (None, _) => sky_colour(scene, rd),
    }
}

// ---------------------------------------------------------------------------
// Tone mapping and output
// ---------------------------------------------------------------------------

fn aces(x: f32) -> f32 {
    let a = 2.51;
    let b = 0.03;
    let c = 2.43;
    let d = 0.59;
    let e = 0.14;
    ((x * (a * x + b)) / (x * (c * x + d) + e)).clamp(0.0, 1.0)
}

fn encode(x: f32) -> u8 {
    let s = aces(x).powf(1.0 / 2.2);
    (s * 255.0 + 0.5).clamp(0.0, 255.0) as u8
}

fn render_strip(
    world: &World,
    scene: &Scene,
    cam: &Camera,
    settings: &RenderSettings,
    y0: usize,
    buf: &mut [u8],
) {
    let aspect = settings.width as f32 / settings.height as f32;
    let offsets = sample_offsets(settings.samples);
    let inv_samples = 1.0 / offsets.len() as f32;
    let mut rng = Rng(0x2545_f491 ^ (y0 as u32).wrapping_mul(2_654_435_761));

    for (row, line) in buf.chunks_mut(settings.width * 3).enumerate() {
        let y = y0 + row;
        for x in 0..settings.width {
            let mut acc = Vec3::ZERO;
            for &(ox, oy) in offsets.iter() {
                let sx = 2.0 * (x as f32 + 0.5 + ox) / settings.width as f32 - 1.0;
                let sy = 1.0 - 2.0 * (y as f32 + 0.5 + oy) / settings.height as f32;
                acc += trace(world, scene, cam.pos, cam.ray(sx, sy, aspect));
            }
            let mut colour = acc * inv_samples * scene.exposure;

            // Gentle vignette and a hint of grain: keeps gradients from banding.
            let vx = 2.0 * x as f32 / settings.width as f32 - 1.0;
            let vy = 2.0 * y as f32 / settings.height as f32 - 1.0;
            let vignette = 1.0 - 0.14 * (vx * vx + vy * vy).min(1.5);
            colour = colour * vignette;
            let grain = (rng.next_f32() - 0.5) * (1.0 / 255.0);

            line[x * 3] = encode(colour.x + grain);
            line[x * 3 + 1] = encode(colour.y + grain);
            line[x * 3 + 2] = encode(colour.z + grain);
        }
    }
}

/// Renders an RGB8 image, one row strip per worker thread.
pub fn render(world: &World, scene: &Scene, cam: &Camera, settings: RenderSettings) -> Vec<u8> {
    let width = settings.width;
    let height = settings.height;
    let mut out = vec![0u8; width * height * 3];
    let threads = std::thread::available_parallelism()
        .map(|n| n.get())
        .unwrap_or(4)
        .clamp(1, 32);
    let rows_per = (height + threads - 1) / threads;
    let stride = width * 3 * rows_per;

    std::thread::scope(|scope| {
        let mut handles = Vec::new();
        for (chunk_index, chunk) in out.chunks_mut(stride).enumerate() {
            let y0 = chunk_index * rows_per;
            let camera = *cam;
            handles.push(scope.spawn(move || {
                render_strip(world, scene, &camera, &settings, y0, chunk);
            }));
        }
        for handle in handles {
            handle.join().expect("render thread panicked");
        }
    });

    out
}
