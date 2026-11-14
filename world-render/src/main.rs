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
mod presets;
mod render;
mod server;
mod vec3;
// The walk mode is driven by the WebAssembly build; the binary only needs a
// couple of entry points from it, so silence dead-code warnings for the rest.
#[allow(dead_code)]
mod walk;
mod world;

use std::path::{Path, PathBuf};
use std::time::Instant;

use camera::Camera;
use presets::{presets, scene_from_query};
use vec3::v3;
use render::{render, RenderSettings};
use walk::{Frame, Player, WalkWorld};
use world::World;

struct Options {
    preset: String,
    out: Option<PathBuf>,
    width: usize,
    height: usize,
    samples: u32,
    seed: u32,
    serve: bool,
    port: u16,
    /// Render one frame of the walk-mode renderer to a PNG (debug/verification).
    walk_frame: bool,
    walk_seed: u32,
    /// Optional camera override for `--walk-frame`: "x,z" and degrees.
    walk_pos: Option<(f32, f32)>,
    walk_yaw: Option<f32>,
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
    text.push_str("    --walk-frame      render one first-person frame (debug)\n");
    text.push_str("    --walk-seed <n>   seed for the first-person frame\n");
    text.push_str("    --walk-pos x,z    camera position for the first-person frame\n");
    text.push_str("    --walk-yaw deg    camera heading for the first-person frame\n");
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
        walk_frame: false,
        walk_seed: 7,
        walk_pos: None,
        walk_yaw: None,
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
            "--walk-frame" => options.walk_frame = true,
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
            "--walk-seed" => {
                options.walk_seed = take_value(&mut i)?
                    .parse()
                    .map_err(|_| "invalid --walk-seed")?
            }
            "--walk-pos" => {
                let value = take_value(&mut i)?;
                let (x, z) = value
                    .split_once(',')
                    .ok_or_else(|| "expected --walk-pos x,z".to_string())?;
                options.walk_pos = Some((
                    x.parse().map_err(|_| "invalid --walk-pos x")?,
                    z.parse().map_err(|_| "invalid --walk-pos z")?,
                ));
            }
            "--walk-yaw" => {
                let value: f32 = take_value(&mut i)?
                    .parse()
                    .map_err(|_| "invalid --walk-yaw")?;
                options.walk_yaw = Some(value.to_radians());
            }
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

    if options.walk_frame {
        let frame_started = Instant::now();
        let (mut walk, mut job) = WalkWorld::new(options.walk_seed, &preset.scene);
        // Native build: bake the whole sprite bank in one go.
        job.step(job.total());
        walk.install(job.finish().expect("sprite bake incomplete"));
        let bake_time = frame_started.elapsed();
        let mut player = Player::spawn(&walk.world);
        if let Some((x, z)) = options.walk_pos {
            player.pos = v3(x, walk.ground(x, z), z);
        }
        if let Some(yaw) = options.walk_yaw {
            player.yaw = yaw;
        }
        // `WR_WALK_DEBUG=1` prints where the debug camera ended up, which is
        // handy when a screenshot looks like the camera is aiming at the sky.
        if std::env::var("WR_WALK_DEBUG").is_ok() {
            println!(
                "player pos {:?} pitch {:.3} yaw {:.1}° eye {:?} ground {:.2}",
                player.pos,
                player.pitch,
                player.yaw.to_degrees(),
                player.eye(),
                walk.ground(player.pos.x, player.pos.z)
            );
        }
        let mut frame = Frame::new(options.width, options.height);
        let render_started = Instant::now();
        walk::render_profiled_for_cli(&walk, &player, &mut frame);
        let render_time = render_started.elapsed();
        let out = options
            .out
            .clone()
            .unwrap_or_else(|| PathBuf::from("renders/walk-frame.png"));
        save(&out, options.width, options.height, &frame.rgb)?;
        println!(
            "walk frame: sprite bake {:.0} ms, render {:.1} ms, player at ({:.2}, {:.2}) -> {}",
            bake_time.as_secs_f64() * 1000.0,
            render_time.as_secs_f64() * 1000.0,
            player.pos.x,
            player.pos.z,
            out.display()
        );
        return Ok(());
    }

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
