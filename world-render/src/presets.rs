//! Lighting presets shared by the picture renderer, the walk mode and the
//! WebAssembly build.

use crate::camera::Camera;
use crate::render::Scene;
use crate::vec3::{v3, Vec3};

pub struct Preset {
    pub name: &'static str,
    pub description: &'static str,
    pub scene: Scene,
    pub camera_pos: Vec3,
    pub camera_target: Vec3,
    pub fov: f32,
}

#[allow(clippy::too_many_arguments)]
fn scene(
    sun: Vec3,
    sun_color: Vec3,
    zenith: Vec3,
    horizon: Vec3,
    glow: Vec3,
    cloud: Vec3,
    sea_deep: Vec3,
    sea_shallow: Vec3,
    exposure: f32,
    haze: f32,
    ambient: f32,
    stars: f32,
    lantern: f32,
    time: f32,
) -> Scene {
    Scene {
        sun_dir: sun.normalized(),
        sun_color,
        sky_zenith: zenith,
        sky_horizon: horizon,
        sun_glow: glow,
        cloud_color: cloud,
        sea_deep,
        sea_shallow,
        exposure,
        haze,
        star_amount: stars,
        ambient,
        lantern,
        time,
    }
}

pub fn presets() -> Vec<Preset> {
    vec![
        Preset {
            name: "dawn",
            description: "Low warm sun behind the island, mist over the water",
            scene: scene(
                v3(-0.58, 0.26, -0.77),
                v3(1.00, 0.74, 0.48) * 1.75,
                v3(0.085, 0.165, 0.400),
                v3(0.95, 0.68, 0.47),
                v3(1.00, 0.62, 0.34),
                v3(0.98, 0.74, 0.63),
                v3(0.012, 0.046, 0.075),
                v3(0.055, 0.265, 0.285),
                1.10,
                0.006,
                0.52,
                0.0,
                1.4,
                9.0,
            ),
            camera_pos: v3(5.20, 2.85, 11.40),
            camera_target: v3(-0.25, 1.05, 0.0),
            fov: 37.0,
        },
        Preset {
            name: "day",
            description: "Clear afternoon light, crisp shadows, turquoise shallows",
            scene: scene(
                v3(0.55, 0.80, 0.30),
                v3(1.00, 0.95, 0.86) * 2.30,
                v3(0.070, 0.215, 0.520),
                v3(0.62, 0.77, 0.90),
                v3(1.00, 0.96, 0.85),
                v3(1.00, 1.00, 1.00),
                v3(0.010, 0.075, 0.140),
                v3(0.050, 0.360, 0.375),
                0.62,
                0.007,
                0.62,
                0.0,
                0.0,
                21.0,
            ),
            camera_pos: v3(5.45, 3.15, 11.75),
            camera_target: v3(-0.20, 1.10, 0.0),
            fov: 37.0,
        },
        Preset {
            name: "sunset",
            description: "Golden hour from the side, lanterns already lit",
            scene: scene(
                v3(0.78, 0.105, -0.56),
                v3(1.00, 0.50, 0.22) * 2.30,
                v3(0.075, 0.055, 0.170),
                v3(1.00, 0.42, 0.15),
                v3(1.00, 0.46, 0.20),
                v3(0.95, 0.50, 0.38),
                v3(0.010, 0.026, 0.050),
                v3(0.090, 0.205, 0.250),
                0.92,
                0.006,
                0.46,
                0.0,
                2.4,
                17.0,
            ),
            camera_pos: v3(5.05, 2.60, 11.15),
            camera_target: v3(-0.30, 1.00, 0.0),
            fov: 37.0,
        },
        Preset {
            name: "night",
            description: "Moonlight, a dense star field, and one lit window",
            scene: scene(
                v3(-0.40, 0.72, -0.60),
                v3(0.30, 0.38, 0.56) * 0.60,
                v3(0.006, 0.011, 0.030),
                v3(0.026, 0.046, 0.088),
                v3(0.36, 0.43, 0.62),
                v3(0.10, 0.13, 0.21),
                v3(0.003, 0.008, 0.017),
                v3(0.008, 0.030, 0.046),
                1.05,
                0.007,
                0.18,
                1.0,
                6.0,
                4.0,
            ),
            camera_pos: v3(5.15, 2.95, 11.35),
            camera_target: v3(-0.25, 1.05, 0.0),
            fov: 37.0,
        },
    ]
}

/// Scene and camera for a preset name; used by both the CLI and the preview.
pub fn scene_from_query(preset: &str, _seed: u32) -> (Scene, Camera) {
    let preset = find(preset);
    let camera = Camera::look_at(preset.camera_pos, preset.camera_target, preset.fov);
    (preset.scene, camera)
}

/// Preset by name, falling back to "day" for unknown names.
pub fn find(name: &str) -> Preset {
    let mut all = presets();
    let index = all.iter().position(|p| p.name == name).unwrap_or(1);
    all.remove(index)
}

/// Preset by index, clamped to the available list. Used by the WebAssembly build.
#[allow(dead_code)]
pub fn by_index(index: usize) -> Preset {
    let mut all = presets();
    let index = index.min(all.len() - 1);
    all.remove(index)
}

/// Every preset name, in order. Used by the WebAssembly build.
#[allow(dead_code)]
pub fn names() -> Vec<&'static str> {
    presets().iter().map(|p| p.name).collect()
}
