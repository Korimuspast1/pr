//! The procedural island: value noise, a precomputed height field, and a
//! handful of signed-distance primitives (cottage, lighthouse, pines, rocks,
//! dock) scattered on it. Everything is deterministic for a given seed.

use crate::vec3::{v3, Vec3};

pub const WATER_LEVEL: f32 = 0.0;

/// Extra conservatism for the height-field distance bound.
const SAFETY: f32 = 1.15;

// ---------------------------------------------------------------------------
// Noise
// ---------------------------------------------------------------------------

fn hash2(ix: i32, iz: i32, seed: u32) -> f32 {
    let mut h = (ix as u32).wrapping_mul(0x8da6_b343)
        ^ (iz as u32).wrapping_mul(0xd816_3841)
        ^ seed.wrapping_mul(0xcb1a_b31f);
    h ^= h >> 15;
    h = h.wrapping_mul(0x2c1b_3c6d);
    h ^= h >> 12;
    h = h.wrapping_mul(0x297a_2d39);
    h ^= h >> 15;
    (h & 0x00ff_ffff) as f32 / 16_777_215.0
}

fn fade(t: f32) -> f32 {
    t * t * (3.0 - 2.0 * t)
}

/// 2D value noise in `[0, 1]`.
pub fn noise2(x: f32, z: f32, seed: u32) -> f32 {
    let xi = x.floor();
    let zi = z.floor();
    let fx = fade(x - xi);
    let fz = fade(z - zi);
    let (x0, z0) = (xi as i32, zi as i32);
    let n00 = hash2(x0, z0, seed);
    let n10 = hash2(x0 + 1, z0, seed);
    let n01 = hash2(x0, z0 + 1, seed);
    let n11 = hash2(x0 + 1, z0 + 1, seed);
    let nx0 = n00 + (n10 - n00) * fx;
    let nx1 = n01 + (n11 - n01) * fx;
    nx0 + (nx1 - nx0) * fz
}

/// Fractal Brownian motion in `[0, 1]`.
pub fn fbm(x: f32, z: f32, octaves: u32, seed: u32) -> f32 {
    let mut sum = 0.0;
    let mut amp = 0.5;
    let mut norm = 0.0;
    let mut freq = 1.0;
    let mut ox = 0.0;
    let mut oz = 0.0;
    for i in 0..octaves {
        sum += amp * noise2(x * freq + ox, z * freq + oz, seed.wrapping_add(i * 7919));
        norm += amp;
        amp *= 0.5;
        freq *= 2.03;
        ox += 13.7;
        oz -= 7.3;
    }
    sum / norm
}

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

/// 3D value noise, used to texture props without xz projection smearing.
pub fn noise3(x: f32, y: f32, z: f32, seed: u32) -> f32 {
    let (xi, yi, zi) = (x.floor(), y.floor(), z.floor());
    let (fx, fy, fz) = (fade(x - xi), fade(y - yi), fade(z - zi));
    let (x0, y0, z0) = (xi as i32, yi as i32, zi as i32);
    let c = |dx, dy, dz| hash3(x0 + dx, y0 + dy, z0 + dz, seed);
    let lerp = |a: f32, b: f32, t: f32| a + (b - a) * t;
    let x00 = lerp(c(0, 0, 0), c(1, 0, 0), fx);
    let x10 = lerp(c(0, 1, 0), c(1, 1, 0), fx);
    let x01 = lerp(c(0, 0, 1), c(1, 0, 1), fx);
    let x11 = lerp(c(0, 1, 1), c(1, 1, 1), fx);
    let y0v = lerp(x00, x10, fy);
    let y1v = lerp(x01, x11, fy);
    lerp(y0v, y1v, fz)
}

/// Fractal 3D noise in `[0, 1]`.
pub fn fbm3(x: f32, y: f32, z: f32, octaves: u32, seed: u32) -> f32 {
    let mut sum = 0.0;
    let mut amp = 0.5;
    let mut norm = 0.0;
    let mut f = 1.0;
    for i in 0..octaves {
        sum += amp * noise3(x * f, y * f, z * f, seed.wrapping_add(i * 6151));
        norm += amp;
        amp *= 0.5;
        f *= 2.07;
    }
    sum / norm
}

pub fn smoothstep(edge0: f32, edge1: f32, x: f32) -> f32 {
    let t = ((x - edge0) / (edge1 - edge0)).clamp(0.0, 1.0);
    t * t * (3.0 - 2.0 * t)
}

// ---------------------------------------------------------------------------
// Terrain height field
// ---------------------------------------------------------------------------

#[derive(Clone, Copy)]
struct Pad {
    x: f32,
    z: f32,
    radius: f32,
    lift: f32,
}

/// Flat building pads, so no prop ends up floating or half-buried.
const PADS: [Pad; 3] = [
    Pad {
        x: -2.30,
        z: 1.45,
        radius: 1.20,
        lift: 0.02,
    },
    Pad {
        x: 2.75,
        z: 2.55,
        radius: 1.00,
        lift: 0.00,
    },
    Pad {
        x: 0.55,
        z: 1.65,
        radius: 0.72,
        lift: -0.01,
    },
];

fn raw_height(x: f32, z: f32, seed: u32, detail_on: bool) -> f32 {
    // Elliptical island mask; the shoreline sits near r = 0.9.
    let r = ((x * 0.92 / 6.3).powi(2) + (z / 6.0).powi(2)).sqrt();

    let base = fbm(x * 0.075, z * 0.075, 4, seed);
    let ridge_raw = fbm(x * 0.155 + 31.0, z * 0.155 - 12.0, 4, seed ^ 0x9e37_79b9);
    let ridge = 1.0 - (ridge_raw * 2.0 - 1.0).abs();

    let inland = 1.0 - smoothstep(0.14, 0.94, r);
    let massif = inland.powf(1.15) * (ridge + 0.04).powf(2.4) * 3.35;
    let hills = inland * (base - 0.42) * 1.20;
    let dome = inland * inland * 0.35;
    let basin = -1.7 * smoothstep(0.80, 1.28, r) - 0.22;

    let mut h = massif + hills + dome + basin;
    if detail_on {
        let detail = (fbm(x * 0.55 + 5.0, z * 0.55 - 3.0, 4, seed ^ 0x1234_5678) - 0.5) * 0.10;
        h += detail * smoothstep(-0.6, 0.2, h);
    }
    h
}

fn composed_height(x: f32, z: f32, seed: u32, detail_on: bool) -> f32 {
    let mut h = raw_height(x, z, seed, detail_on);
    for pad in PADS.iter() {
        let d = ((x - pad.x).powi(2) + (z - pad.z).powi(2)).sqrt();
        if d < pad.radius {
            let target = raw_height(pad.x, pad.z, seed, detail_on) + pad.lift;
            let t = smoothstep(pad.radius, pad.radius * 0.35, d);
            h += (target - h) * t;
        }
    }
    h
}

/// Bilinear height field over a square, cached once per render.
pub struct HeightField {
    data: Vec<f32>,
    res: usize,
    origin: f32,
    size: f32,
    cell: f32,
}

impl HeightField {
    pub fn build(seed: u32, res: usize, extent: f32, detail_on: bool) -> Self {
        let size = extent * 2.0;
        let cell = size / (res - 1) as f32;
        let mut data = Vec::with_capacity(res * res);
        for j in 0..res {
            let z = -extent + j as f32 * cell;
            for i in 0..res {
                let x = -extent + i as f32 * cell;
                data.push(composed_height(x, z, seed, detail_on));
            }
        }
        let mut field = HeightField {
            data,
            res,
            origin: -extent,
            size,
            cell,
        };
        if detail_on {
            // Seeds vary a lot in amplitude; scale so every island has a
            // summit worth looking at instead of the occasional flat pancake.
            let peak = field.data.iter().copied().fold(0.0f32, f32::max);
            if peak > 0.2 {
                let factor = (3.0 / peak).clamp(1.0, 2.8);
                for h in field.data.iter_mut() {
                    *h *= factor;
                }
            }
        }
        field
    }

    #[inline]
    pub fn sample(&self, x: f32, z: f32) -> f32 {
        let u = (x - self.origin) / self.cell;
        let v = (z - self.origin) / self.cell;
        if u < 0.0 || v < 0.0 || u >= (self.res - 1) as f32 || v >= (self.res - 1) as f32 {
            // Outside the detailed island: deep open sea.
            return -2.4;
        }
        let i = u as usize;
        let j = v as usize;
        let fx = u - i as f32;
        let fz = v - j as f32;
        let r = self.res;
        let h00 = self.data[j * r + i];
        let h10 = self.data[j * r + i + 1];
        let h01 = self.data[(j + 1) * r + i];
        let h11 = self.data[(j + 1) * r + i + 1];
        let a = h00 + (h10 - h00) * fx;
        let b = h01 + (h11 - h01) * fx;
        a + (b - a) * fz
    }

    /// Half-size of the square covered by the field, in world units.
    pub fn extent(&self) -> f32 {
        self.size * 0.5
    }

    pub fn gradient(&self, x: f32, z: f32) -> (f32, f32) {
        // A wider stencil hides the per-cell steps of a bilinear field.
        let e = self.cell.max(0.28);
        let gx = (self.sample(x + e, z) - self.sample(x - e, z)) / (2.0 * e);
        let gz = (self.sample(x, z + e) - self.sample(x, z - e)) / (2.0 * e);
        (gx, gz)
    }

    pub fn normal(&self, x: f32, z: f32) -> Vec3 {
        let (gx, gz) = self.gradient(x, z);
        v3(-gx, 1.0, -gz).normalized()
    }

    /// Shading normal: the smooth island shape plus the fine detail field
    /// applied as a bump. Sampling the detailed field directly would give
    /// per-cell normals and a speckled surface.
    pub fn shading_normal(&self, detailed: &HeightField, x: f32, z: f32, bump: f32) -> Vec3 {
        let gx = (self.sample(x + 0.28, z) - self.sample(x - 0.28, z)) / 0.56;
        let gz = (self.sample(x, z + 0.28) - self.sample(x, z - 0.28)) / 0.56;
        let e = 0.05;
        let dx = (detailed.sample(x + e, z)
            - detailed.sample(x - e, z)
            - (self.sample(x + e, z) - self.sample(x - e, z)))
            / (2.0 * e);
        let dz = (detailed.sample(x, z + e)
            - detailed.sample(x, z - e)
            - (self.sample(x, z + e) - self.sample(x, z - e)))
            / (2.0 * e);
        v3(-gx - dx * bump, 1.0, -gz - dz * bump).normalized()
    }

    /// Normal from a wider stencil: the large-scale shape, without the
    /// high-frequency wobble of the detail noise.
    pub fn normal_wide(&self, x: f32, z: f32, radius: f32) -> Vec3 {
        let gx = (self.sample(x + radius, z) - self.sample(x - radius, z)) / (2.0 * radius);
        let gz = (self.sample(x, z + radius) - self.sample(x, z - radius)) / (2.0 * radius);
        v3(-gx, 1.0, -gz).normalized()
    }

    /// Conservative distance from a point to the height field surface.
    #[inline]
    pub fn distance(&self, p: Vec3) -> f32 {
        let h = self.sample(p.x, p.z);
        // Narrow stencil: the wider shading stencil underestimates slopes on
        // ridges, which would make the marcher overshoot and self-shadow.
        let e = self.cell;
        let gx = (self.sample(p.x + e, p.z) - self.sample(p.x - e, p.z)) / (2.0 * e);
        let gz = (self.sample(p.x, p.z + e) - self.sample(p.x, p.z - e)) / (2.0 * e);
        let lip = (1.0 + gx * gx + gz * gz).sqrt() * SAFETY;
        (p.y - h) / lip
    }
}

// ---------------------------------------------------------------------------
// Materials
// ---------------------------------------------------------------------------

#[derive(Clone, Copy, PartialEq, Debug)]
pub enum Mat {
    Plaster,
    Roof,
    Stone,
    Trunk,
    Foliage,
    Lamp,
    Wood,
}

impl Mat {
    pub fn albedo(self) -> Vec3 {
        match self {
            Mat::Plaster => v3(0.82, 0.77, 0.66),
            Mat::Roof => v3(0.44, 0.15, 0.11),
            Mat::Stone => v3(0.42, 0.40, 0.37),
            Mat::Trunk => v3(0.24, 0.15, 0.09),
            Mat::Foliage => v3(0.11, 0.26, 0.12),
            Mat::Lamp => v3(1.0, 0.85, 0.55),
            Mat::Wood => v3(0.34, 0.23, 0.14),
        }
    }

    pub fn emission(self) -> Vec3 {
        match self {
            Mat::Lamp => v3(6.0, 3.6, 1.5),
            _ => Vec3::ZERO,
        }
    }

    pub fn specular(self) -> f32 {
        match self {
            Mat::Lamp => 0.35,
            Mat::Roof => 0.14,
            Mat::Plaster => 0.05,
            Mat::Wood => 0.06,
            Mat::Stone => 0.05,
            Mat::Trunk => 0.02,
            Mat::Foliage => 0.02,
        }
    }
}

// ---------------------------------------------------------------------------
// Signed-distance primitives
// ---------------------------------------------------------------------------

pub fn sd_box(p: Vec3, half: Vec3) -> f32 {
    let d = v3(p.x.abs() - half.x, p.y.abs() - half.y, p.z.abs() - half.z);
    let outside = v3(d.x.max(0.0), d.y.max(0.0), d.z.max(0.0)).length();
    outside + d.x.max(d.y).max(d.z).min(0.0)
}

pub fn sd_cylinder_y(p: Vec3, radius: f32, half_height: f32) -> f32 {
    let radial = (p.x * p.x + p.z * p.z).sqrt() - radius;
    let vertical = p.y.abs() - half_height;
    let outside = v3(radial.max(0.0), vertical.max(0.0), 0.0).length();
    outside + radial.max(vertical).min(0.0)
}

/// Capped cone centred at the origin, axis along +Y.
pub fn sd_capped_cone(p: Vec3, half_height: f32, r_bottom: f32, r_top: f32) -> f32 {
    let q = v3((p.x * p.x + p.z * p.z).sqrt(), p.y, 0.0);
    let k1 = v3(r_top, half_height, 0.0);
    let k2 = v3(r_top - r_bottom, 2.0 * half_height, 0.0);
    let ca = v3(
        q.x - q.x.min(if q.y < 0.0 { r_bottom } else { r_top }),
        q.y.abs() - half_height,
        0.0,
    );
    let t = ((k1 - q).dot(k2) / k2.dot(k2)).clamp(0.0, 1.0);
    let cb = q - k1 + k2 * t;
    let s = if cb.x < 0.0 && ca.y < 0.0 { -1.0 } else { 1.0 };
    s * ca.dot(ca).min(cb.dot(cb)).sqrt()
}

fn rot_y(p: Vec3, angle: f32) -> Vec3 {
    let (s, c) = angle.sin_cos();
    v3(c * p.x + s * p.z, p.y, -s * p.x + c * p.z)
}

fn rot_x(p: Vec3, angle: f32) -> Vec3 {
    let (s, c) = angle.sin_cos();
    v3(p.x, c * p.y + s * p.z, -s * p.y + c * p.z)
}

// ---------------------------------------------------------------------------
// Props
// ---------------------------------------------------------------------------

#[derive(Clone, Copy, Debug)]
pub enum Prop {
    Pine { height: f32, radius: f32 },
    Cottage { yaw: f32 },
    Lighthouse,
    Boulder { radius: f32 },
    Dock { yaw: f32 },
}

#[derive(Clone, Copy, Debug)]
pub struct Solid {
    /// Anchor on the terrain (y is the ground height at `x`, `z`).
    pub anchor: Vec3,
    /// Bounding sphere, used to skip far props cheaply.
    pub center: Vec3,
    pub radius: f32,
    pub prop: Prop,
}

impl Solid {
    fn new(anchor: Vec3, prop: Prop) -> Self {
        let (offset, radius) = match prop {
            // The sphere must fully contain the geometry: a bound that is too
            // small would report a distance larger than the real one and let
            // the marcher step straight through the prop. The `distance_bounds`
            // test keeps this honest.
            Prop::Pine { height, radius } => (
                v3(0.0, height * 0.5, 0.0),
                ((height * 0.62).powi(2) + radius * radius).sqrt() + 0.05,
            ),
            Prop::Cottage { .. } => (v3(0.0, 0.85, 0.0), 1.50),
            Prop::Lighthouse => (v3(0.0, 1.75, 0.0), 2.10),
            Prop::Boulder { radius } => (v3(0.0, 0.0, 0.0), radius + 0.05),
            Prop::Dock { .. } => (v3(0.0, -0.12, 0.0), 1.30),
        };
        Solid {
            anchor,
            center: anchor + offset,
            radius,
            prop,
        }
    }

    /// Distance to this prop.
    ///
    /// The flag tells whether the value is the real geometry distance (`true`)
    /// or only the distance to the bounding sphere (`false`). A ray marcher
    /// must never report a hit on a mere bound: the bound can be close to a ray
    /// that still passes far away from the shape.
    #[inline]
    pub fn distance(&self, p: Vec3) -> (f32, bool) {
        let outside = (p - self.center).length() - self.radius;
        if outside > 0.0 {
            (outside, false)
        } else {
            (self.primitive_distance(p), true)
        }
    }

    /// Exact signed distance to the prop's geometry.
    #[inline]
    pub fn primitive_distance(&self, p: Vec3) -> f32 {
        let l = p - self.anchor;
        match self.prop {
            Prop::Pine { height, radius } => pine_distance(l, height, radius),
            Prop::Cottage { yaw } => cottage_distance(l, yaw),
            Prop::Lighthouse => lighthouse_distance(l),
            Prop::Boulder { radius } => l.length() - radius,
            Prop::Dock { yaw } => dock_distance(l, yaw),
        }
    }

    fn material(&self, p: Vec3) -> Mat {
        let l = p - self.anchor;
        match self.prop {
            Prop::Pine { height, radius } => {
                let radial = (l.x * l.x + l.z * l.z).sqrt();
                if l.y < height * 0.42 && radial < radius * 0.24 {
                    Mat::Trunk
                } else {
                    Mat::Foliage
                }
            }
            Prop::Cottage { yaw } => {
                let r = rot_y(l, -yaw);
                if r.y > 1.08 {
                    Mat::Roof
                } else if r.y > 0.42 && r.y < 0.80 && r.z.abs() > 0.55 && r.x.abs() < 0.24 {
                    Mat::Lamp
                } else {
                    Mat::Plaster
                }
            }
            Prop::Lighthouse => {
                if l.y < 0.42 {
                    Mat::Stone
                } else if l.y > 3.16 {
                    Mat::Roof
                } else if l.y > 2.70 {
                    Mat::Lamp
                } else {
                    Mat::Plaster
                }
            }
            Prop::Boulder { .. } => Mat::Stone,
            Prop::Dock { .. } => Mat::Wood,
        }
    }
}

fn pine_distance(l: Vec3, height: f32, radius: f32) -> f32 {
    let trunk = sd_cylinder_y(
        l - v3(0.0, height * 0.30, 0.0),
        radius * 0.10,
        height * 0.30,
    );
    let mut d = trunk;
    for tier in 0..3 {
        let base = height * (0.22 + 0.235 * tier as f32);
        let cone_h = height * 0.34;
        let r = radius * (1.0 - 0.235 * tier as f32);
        let cone = sd_capped_cone(
            l - v3(0.0, base + cone_h * 0.5, 0.0),
            cone_h * 0.5,
            r,
            r * 0.06,
        );
        d = d.min(cone);
    }
    d
}

fn cottage_distance(l: Vec3, yaw: f32) -> f32 {
    let p = rot_y(l, -yaw);
    let mut d = sd_box(p - v3(0.0, 0.55, 0.0), v3(0.88, 0.55, 0.62));

    // Two slanted roof slabs meeting at a ridge that runs along X.
    let slab_half = v3(0.98, 0.055, 0.44);
    let slope = 0.62; // radians
    let left = sd_box(rot_x(p - v3(0.0, 1.02, 0.20), slope), slab_half);
    let right = sd_box(rot_x(p - v3(0.0, 1.02, -0.20), -slope), slab_half);
    d = d.min(left).min(right);

    // Chimney.
    let chimney = sd_box(p - v3(0.52, 1.32, -0.18), v3(0.11, 0.30, 0.11));
    d.min(chimney)
}

fn lighthouse_distance(l: Vec3) -> f32 {
    let mut d = sd_cylinder_y(l - v3(0.0, 0.20, 0.0), 0.62, 0.22);
    d = d.min(sd_capped_cone(l - v3(0.0, 1.55, 0.0), 1.15, 0.36, 0.25));
    d = d.min(sd_cylinder_y(l - v3(0.0, 2.72, 0.0), 0.33, 0.045));
    d = d.min(sd_box(l - v3(0.0, 2.95, 0.0), v3(0.20, 0.20, 0.20)));
    d.min(sd_capped_cone(l - v3(0.0, 3.34, 0.0), 0.18, 0.28, 0.02))
}

fn dock_distance(l: Vec3, yaw: f32) -> f32 {
    let p = rot_y(l, -yaw);
    let mut d = sd_box(p - v3(0.0, 0.10, 0.0), v3(0.55, 0.06, 0.95));
    for side in [-1.0f32, 1.0] {
        d = d.min(sd_cylinder_y(p - v3(side * 0.48, -0.12, 0.75), 0.07, 0.32));
    }
    d
}

// ---------------------------------------------------------------------------
// World assembly
// ---------------------------------------------------------------------------

pub struct World {
    /// Terrain seed; kept so tooling can report which world was rendered.
    #[allow(dead_code)]
    pub seed: u32,
    /// Full field, including the fine detail: used for hit tests.
    pub height: HeightField,
    /// The same island without the fine detail: shading normals come from here,
    /// so the detail bumps never turn into per-pixel lighting speckle.
    pub smooth_height: HeightField,
    pub solids: Vec<Solid>,
    /// Local point light inside the lighthouse lantern.
    pub lantern_pos: Vec3,
    /// Bounding box of every prop, for a cheap "is anything nearby" test.
    props_lo: Vec3,
    props_hi: Vec3,
}

impl World {
    pub fn build(seed: u32) -> World {
        let height = HeightField::build(seed, 1600, 11.0, true);
        let smooth_height = HeightField::build(seed, 1600, 11.0, false);
        let ground = |x: f32, z: f32| height.sample(x, z);
        let mut solids: Vec<Solid> = Vec::new();
        let mut rng = crate::camera::Rng(seed ^ 0x51ed_270b);

        // Cottage on its pad.
        let (cx, cz) = (-2.30f32, 1.45f32);
        solids.push(Solid::new(
            v3(cx, ground(cx, cz), cz),
            Prop::Cottage { yaw: -0.55 },
        ));

        // Lighthouse on the rocky pad near the shore.
        let (lx, lz) = (2.75f32, 2.55f32);
        let ly = ground(lx, lz);
        solids.push(Solid::new(v3(lx, ly, lz), Prop::Lighthouse));
        let lantern_pos = v3(lx, ly + 2.95, lz);

        // Pines: rejection sampling for gentle, dry ground.
        let mut placed = 0;
        let mut tries = 0;
        while placed < 34 && tries < 6000 {
            tries += 1;
            let angle = rng.next_f32() * std::f32::consts::TAU;
            let radius = 1.15 + rng.next_f32() * 3.8;
            let x = angle.cos() * radius * 1.05;
            let z = angle.sin() * radius * 0.95;
            let h = ground(x, z);
            if h < 0.30 || h > 1.55 {
                continue;
            }
            if height.normal(x, z).y < 0.80 {
                continue;
            }
            if ((x - cx).powi(2) + (z - cz).powi(2)).sqrt() < 1.9 {
                continue;
            }
            if ((x - lx).powi(2) + (z - lz).powi(2)).sqrt() < 1.5 {
                continue;
            }
            let crowded = solids.iter().any(|s| {
                matches!(s.prop, Prop::Pine { .. })
                    && ((s.anchor.x - x).powi(2) + (s.anchor.z - z).powi(2)).sqrt() < 0.60
            });
            if crowded {
                continue;
            }
            let size = 0.75 + rng.next_f32() * 0.85;
            solids.push(Solid::new(
                v3(x, h - 0.06, z),
                Prop::Pine {
                    height: size * 1.65,
                    radius: size * 0.44,
                },
            ));
            placed += 1;
        }

        // Boulders along the shoreline.
        for &(x, z, r) in [
            (-2.55f32, 3.55f32, 0.30f32),
            (3.35, -1.35, 0.24),
            (-3.55, 1.05, 0.20),
            (1.55, 3.85, 0.27),
            (4.05, 1.15, 0.22),
            (-1.25, 4.10, 0.18),
        ]
        .iter()
        {
            let h = ground(x, z);
            if h > -0.55 {
                solids.push(Solid::new(
                    v3(x, h.max(-0.12) + r * 0.30, z),
                    Prop::Boulder { radius: r },
                ));
            }
        }

        // Distant islets: barely-there silhouettes that give the sea a horizon.
        for &(x, z, r) in [
            (-7.60f32, -6.40f32, 0.62f32),
            (-8.40, -4.10, 0.40),
            (7.90, -5.20, 0.55),
            (8.60, 2.30, 0.34),
            (-6.90, 6.60, 0.46),
        ]
        .iter()
        {
            solids.push(Solid::new(v3(x, -0.30, z), Prop::Boulder { radius: r }));
        }

        // A small dock reaching into the sea on the eastern shore.
        let (dx, dz) = (4.55f32, 0.55f32);
        solids.push(Solid::new(
            v3(dx, ground(dx, dz) + 0.24, dz),
            Prop::Dock { yaw: 0.85 },
        ));

        let mut lo = v3(f32::INFINITY, f32::INFINITY, f32::INFINITY);
        let mut hi = v3(f32::NEG_INFINITY, f32::NEG_INFINITY, f32::NEG_INFINITY);
        for s in solids.iter() {
            lo = v3(
                lo.x.min(s.center.x - s.radius),
                lo.y.min(s.center.y - s.radius),
                lo.z.min(s.center.z - s.radius),
            );
            hi = v3(
                hi.x.max(s.center.x + s.radius),
                hi.y.max(s.center.y + s.radius),
                hi.z.max(s.center.z + s.radius),
            );
        }

        World {
            seed,
            height,
            smooth_height,
            solids,
            lantern_pos,
            props_lo: lo,
            props_hi: hi,
        }
    }

    /// Rough distance from a point to the props' shared bounding box.
    pub fn props_box_distance(&self, p: Vec3) -> f32 {
        let center = (self.props_lo + self.props_hi) * 0.5;
        let half = (self.props_hi - self.props_lo) * 0.5;
        sd_box(p - center, half)
    }

    pub fn height_extent(&self) -> f32 {
        self.height.extent()
    }

    /// Distance to the props. Outside every bounding sphere this is a safe
    /// lower bound; inside one it is the exact geometry distance.
    #[inline]
    pub fn props_distance(&self, p: Vec3) -> f32 {
        let mut d = f32::INFINITY;
        for s in self.solids.iter() {
            let (sd, _) = s.distance(p);
            if sd < d {
                d = sd;
            }
        }
        d
    }

    /// Distance to props and terrain: a safe lower bound for ray marching.
    #[inline]
    pub fn distance(&self, p: Vec3) -> f32 {
        self.props_distance(p).min(self.height.distance(p))
    }

    /// Real surface proximity, ignoring bounding-volume shortcuts.
    ///
    /// A marcher may reach a point where a conservative bound reads ~0 while
    /// the actual geometry is still far away; this confirms a candidate hit.
    pub fn near_surface(&self, p: Vec3, eps: f32) -> bool {
        if (p.y - self.height.sample(p.x, p.z)).abs() < eps * 2.0 {
            return true;
        }
        self.solids.iter().any(|s| s.primitive_distance(p) < eps)
    }

    /// Distance to prop geometry only (no bounding volumes).
    pub fn props_exact(&self, p: Vec3) -> f32 {
        self.solids
            .iter()
            .map(|s| s.primitive_distance(p))
            .fold(f32::INFINITY, f32::min)
    }

    pub fn props_normal(&self, p: Vec3) -> Vec3 {
        let e = 0.003;
        let d = |q: Vec3| self.props_exact(q);
        v3(
            d(p + v3(e, 0.0, 0.0)) - d(p - v3(e, 0.0, 0.0)),
            d(p + v3(0.0, e, 0.0)) - d(p - v3(0.0, e, 0.0)),
            d(p + v3(0.0, 0.0, e)) - d(p - v3(0.0, 0.0, e)),
        )
        .normalized()
    }

    /// Material of the closest prop (only meaningful near a surface hit).
    pub fn closest_material(&self, p: Vec3) -> Mat {
        let mut best = (f32::INFINITY, Mat::Stone);
        for s in self.solids.iter() {
            let sd = s.primitive_distance(p);
            if sd < best.0 {
                best = (sd, s.material(p));
            }
        }
        best.1
    }

    /// True when the closest surface at `p` is terrain rather than a prop.
    pub fn is_terrain(&self, p: Vec3) -> bool {
        (p.y - self.height.sample(p.x, p.z)).abs() <= self.props_exact(p)
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn noise_is_deterministic_and_bounded() {
        for i in 0..200 {
            let x = i as f32 * 0.37;
            let z = i as f32 * -0.21;
            let a = fbm(x, z, 4, 7);
            let b = fbm(x, z, 4, 7);
            assert_eq!(a, b);
            assert!((0.0..=1.0).contains(&a));
        }
        // A different seed must give a different island.
        assert_ne!(fbm(1.0, 2.0, 4, 7), fbm(1.0, 2.0, 4, 8));
    }

    #[test]
    fn terrain_has_land_and_sea() {
        let field = HeightField::build(7, 200, 11.0, true);
        assert!(
            field.sample(0.0, 0.0) > 0.0,
            "the summit should be above water"
        );
        assert!(
            field.sample(9.0, 9.0) < 0.0,
            "far from the island should be sea"
        );
    }

    #[test]
    fn bounding_spheres_are_conservative() {
        // Outside a prop's bounding sphere the marcher may only use the sphere
        // distance, so it must never exceed the real geometry distance.
        let world = World::build(7);
        let mut rng = crate::camera::Rng(1234);
        for _ in 0..400 {
            let p = v3(
                rng.next_f32() * 26.0 - 13.0,
                rng.next_f32() * 9.0 - 1.5,
                rng.next_f32() * 26.0 - 13.0,
            );
            for solid in world.solids.iter() {
                let (sphere, inside) = solid.distance(p);
                if !inside {
                    assert!(
                        sphere <= solid.primitive_distance(p) + 1e-3,
                        "{:?}: sphere bound {sphere} exceeds the geometry distance",
                        solid.prop
                    );
                }
            }
        }
    }

    #[test]
    fn props_are_solid_inside() {
        let world = World::build(7);
        for solid in world.solids.iter() {
            // The anchor point of every prop sits on (or inside) the prop.
            let d = solid.primitive_distance(solid.anchor);
            assert!(
                d < 0.9,
                "prop {:?} has distance {d} at its anchor",
                solid.prop
            );
        }
    }
}
