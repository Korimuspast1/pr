//! `world-render` — a procedural island rendered by a CPU ray marcher.
//!
//! The whole project is dependency-free: it builds with cargo, or with a bare
//! `rustc src/main.rs -O` exactly like the "Hello, world!" chapter of the Rust
//! book. Everything you see is generated from noise at run time — there are no
//! textures, meshes or assets of any kind.
//!
//! Usage:
//!   world-render --preset dawn --out renders/dawn.png
//!   world-render --preset sunset --width 1920 --height 1080 --samples 2
//!   world-render --serve --port 8080      # interactive browser preview
//!   world-render --list-presets

mod camera;
mod png;
mod render;
mod server;
mod vec3;
mod world;

use std::path::{Path, PathBuf};
use std::time::Instant;

use camera::Camera;
use render::{render, RenderSettings, Scene};
use vec3::{v3, Vec3};
use world::World;

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
    let preset = presets()
        .into_iter()
        .find(|p| p.name == preset)
        .unwrap_or_else(|| presets().remove(1));
    let camera = Camera::look_at(preset.camera_pos, preset.camera_target, preset.fov);
    (preset.scene, camera)
}

struct Options {
    preset: String,
    out: Option<PathBuf>,
    width: usize,
    height: usize,
    samples: u32,
    seed: u32,
    serve: bool,
    port: u16,
}

fn help() -> String {
    let mut text = String::new();
    text.push_str("world-render — a procedural island, ray marched on the CPU\n\n");
    text.push_str("USAGE:\n    world-render [OPTIONS]\n\nOPTIONS:\n");
    text.push_str("    --preset <name>   lighting preset (default: dawn)\n");
    text.push_str(
        "    --out <path>      output file: .png or .ppm (default: renders/<preset>.png)\n",
    );
    text.push_str("    --width <px>      image width  (default: 1280)\n");
    text.push_str("    --height <px>     image height (default: 720)\n");
    text.push_str("    --samples <1|2|3> 1, 4 or 9 rays per pixel (default: 2)\n");
    text.push_str("    --seed <n>        terrain seed (default: 7)\n");
    text.push_str("    --serve           start the interactive preview server\n");
    text.push_str("    --port <port>     preview server port (default: 8080)\n");
    text.push_str("    --list-presets    print presets and exit\n");
    text.push_str("    -h, --help        print this help and exit\n\nPRESETS:\n");
    for preset in presets() {
        text.push_str(&format!("    {:<8} {}\n", preset.name, preset.description));
    }
    text
}

fn parse_args() -> Result<Options, String> {
    let mut options = Options {
        preset: "dawn".to_string(),
        out: None,
        width: 1280,
        height: 720,
        samples: 2,
        seed: 7,
        serve: false,
        port: 8080,
    };

    let args: Vec<String> = std::env::args().skip(1).collect();
    let mut i = 0;
    while i < args.len() {
        let arg = args[i].clone();
        let take_value = |i: &mut usize| -> Result<String, String> {
            *i += 1;
            args.get(*i)
                .cloned()
                .ok_or_else(|| format!("missing value for {arg}"))
        };
        match args[i].as_str() {
            "-h" | "--help" => {
                print!("{}", help());
                std::process::exit(0);
            }
            "--list-presets" => {
                for preset in presets() {
                    println!("{:<8} {}", preset.name, preset.description);
                }
                std::process::exit(0);
            }
            "--serve" => options.serve = true,
            "--preset" => options.preset = take_value(&mut i)?,
            "--out" => options.out = Some(PathBuf::from(take_value(&mut i)?)),
            "--width" => {
                options.width = take_value(&mut i)?.parse().map_err(|_| "invalid --width")?
            }
            "--height" => {
                options.height = take_value(&mut i)?
                    .parse()
                    .map_err(|_| "invalid --height")?
            }
            "--samples" => {
                options.samples = take_value(&mut i)?
                    .parse()
                    .map_err(|_| "invalid --samples")?
            }
            "--seed" => options.seed = take_value(&mut i)?.parse().map_err(|_| "invalid --seed")?,
            "--port" => options.port = take_value(&mut i)?.parse().map_err(|_| "invalid --port")?,
            other => return Err(format!("unknown option: {other}")),
        }
        i += 1;
    }

    if options.width < 16 || options.height < 16 {
        return Err("image must be at least 16x16".to_string());
    }
    if options.width * options.height > 64_000_000 {
        return Err("image is too large (over 64 megapixels)".to_string());
    }
    if !(1..=3).contains(&options.samples) {
        return Err("--samples must be 1, 2 or 3".to_string());
    }
    Ok(options)
}

fn save(path: &Path, width: usize, height: usize, pixels: &[u8]) -> std::io::Result<()> {
    if let Some(parent) = path.parent() {
        if !parent.as_os_str().is_empty() {
            std::fs::create_dir_all(parent)?;
        }
    }
    let extension = path
        .extension()
        .and_then(|e| e.to_str())
        .unwrap_or("")
        .to_ascii_lowercase();
    let bytes = match extension.as_str() {
        "ppm" => png::encode_ppm(width, height, pixels),
        _ => png::encode_rgb_png(width, height, pixels),
    };
    std::fs::write(path, bytes)
}

fn main() -> Result<(), Box<dyn std::error::Error>> {
    let options = parse_args().map_err(|e| format!("{e}\n\n{}", help()))?;

    if options.serve {
        return Ok(server::serve(options.port)?);
    }

    let preset = presets()
        .into_iter()
        .find(|p| p.name == options.preset)
        .ok_or_else(|| {
            let names: Vec<&str> = presets().iter().map(|p| p.name).collect();
            format!(
                "unknown preset '{}'. Available: {}",
                options.preset,
                names.join(", ")
            )
        })?;

    let started = Instant::now();
    let world = World::build(options.seed);
    let gen_time = started.elapsed();

    let camera = Camera::look_at(preset.camera_pos, preset.camera_target, preset.fov);
    let settings = RenderSettings {
        width: options.width,
        height: options.height,
        samples: options.samples,
    };

    println!(
        "world-render: preset '{}', {}x{}, {} spp, seed {}",
        preset.name,
        options.width,
        options.height,
        options.samples * options.samples,
        options.seed
    );
    println!(
        "geometry: {} props, {:.0}x{:.0} unit height field ({:.0} ms)",
        world.solids.len(),
        world.height_extent() * 2.0,
        world.height_extent() * 2.0,
        gen_time.as_secs_f64() * 1000.0
    );

    let render_started = Instant::now();
    let pixels = render(&world, &preset.scene, &camera, settings);
    let render_time = render_started.elapsed();

    let out = options
        .out
        .unwrap_or_else(|| PathBuf::from(format!("renders/{}.png", preset.name)));
    save(&out, options.width, options.height, &pixels)?;

    println!(
        "rendered in {:.2} s -> {}",
        render_time.as_secs_f64(),
        out.display()
    );
    Ok(())
}
