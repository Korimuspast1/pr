//! Pinhole camera plus a tiny deterministic PRNG.

use crate::vec3::Vec3;

#[derive(Clone, Copy, Debug)]
pub struct Camera {
    pub pos: Vec3,
    pub forward: Vec3,
    pub right: Vec3,
    pub up: Vec3,
    /// Vertical field of view in degrees.
    pub fov_deg: f32,
}

impl Camera {
    pub fn look_at(pos: Vec3, target: Vec3, fov_deg: f32) -> Self {
        let forward = (target - pos).normalized();
        let right = forward.cross(Vec3::UP).normalized();
        let up = right.cross(forward);
        Camera {
            pos,
            forward,
            right,
            up,
            fov_deg,
        }
    }

    /// Primary ray for normalised screen coordinates in `[-1, 1]`.
    ///
    /// `(0, 0)` is the centre of the frame, `(-1, -1)` the lower-left corner.
    pub fn ray(&self, sx: f32, sy: f32, aspect: f32) -> Vec3 {
        let half_height = (self.fov_deg.to_radians() * 0.5).tan();
        let dir =
            self.forward + self.right * (sx * half_height * aspect) + self.up * (sy * half_height);
        dir.normalized()
    }
}

/// Offsets of a rotated grid: better anti-aliasing than a naive square grid.
pub fn sample_offsets(samples: u32) -> Vec<(f32, f32)> {
    match samples {
        0 | 1 => vec![(0.0, 0.0)],
        2 => vec![
            (-0.375, -0.125),
            (0.125, -0.375),
            (0.375, 0.125),
            (-0.125, 0.375),
        ],
        _ => {
            let mut out = Vec::with_capacity(9);
            for y in -1..=1 {
                for x in -1..=1 {
                    out.push((x as f32 / 3.0, y as f32 / 3.0));
                }
            }
            out
        }
    }
}

/// Small deterministic PRNG (xorshift32) so renders are reproducible.
pub struct Rng(pub u32);

impl Rng {
    pub fn next_u32(&mut self) -> u32 {
        let mut x = self.0;
        x ^= x << 13;
        x ^= x >> 17;
        x ^= x << 5;
        self.0 = x;
        x
    }

    pub fn next_f32(&mut self) -> f32 {
        (self.next_u32() >> 8) as f32 / 16_777_216.0
    }
}
