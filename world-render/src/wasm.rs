//! WebAssembly entry points for the browser walk mode.
//!
//! The module is deliberately built without `wasm-bindgen`: it exports plain
//! C functions and a pointer into its own memory, so the page needs nothing
//! but `WebAssembly.instantiate`.

use std::cell::RefCell;

use crate::presets;
use crate::walk::{BakeJob, Frame, Input, Player, WalkWorld};

struct Session {
    walk: Option<WalkWorld>,
    job: Option<BakeJob>,
    player: Option<Player>,
    frame: Frame,
    rgba: Vec<u8>,
    input: u32,
    /// 0 while building, 100 when the world is ready.
    progress: u32,
}

/// Last panic message, exposed to the page so a crash is not just "unreachable".
static LAST_ERROR: std::sync::Mutex<Vec<u8>> = std::sync::Mutex::new(Vec::new());

fn record_error(message: &str) {
    if let Ok(mut slot) = LAST_ERROR.lock() {
        slot.clear();
        slot.extend_from_slice(message.as_bytes());
        slot.push(0);
    }
}

/// Pointer to the last panic message (NUL-terminated), or an empty string.
#[no_mangle]
pub extern "C" fn wr_last_error() -> *const u8 {
    LAST_ERROR
        .lock()
        .map(|slot| slot.as_ptr())
        .unwrap_or(std::ptr::null())
}

thread_local! {
    static SESSION: RefCell<Session> = RefCell::new(Session {
        walk: None,
        job: None,
        player: None,
        frame: Frame::new(1, 1),
        rgba: vec![0; 4],
        input: 0,
        progress: 0,
    });
}

/// Starts a new world: terrain first, sprites are baked across later calls.
#[no_mangle]
pub extern "C" fn wr_init(preset_index: u32, seed: u32) -> u32 {
    std::panic::set_hook(Box::new(|info| {
        record_error(&format!("{info}"));
    }));
    let preset = presets::by_index(preset_index as usize);
    let (walk, job) = WalkWorld::new(seed, &preset.scene);
    let player = Player::spawn(&walk.world);
    SESSION.with(|session| {
        let mut session = session.borrow_mut();
        session.job = Some(job);
        session.progress = 0;
        session.player = Some(player);
        session.walk = Some(walk);
    });
    presets::names().len() as u32
}

/// Bakes up to `budget` sprites. Returns the progress percentage (100 = ready).
#[no_mangle]
pub extern "C" fn wr_bake_step(budget: u32) -> u32 {
    SESSION.with(|session| {
        let mut session = session.borrow_mut();
        let Some(job) = session.job.as_mut() else {
            return 100;
        };
        let done = job.step(budget as usize);
        session.progress = (job.done() * 100 / job.total().max(1)) as u32;
        if done {
            if let Some(job) = session.job.take() {
                if let Some(bank) = job.finish() {
                    if let Some(walk) = session.walk.as_mut() {
                        walk.install(bank);
                    }
                }
            }
            session.progress = 100;
        }
        session.progress
    })
}

#[no_mangle]
pub extern "C" fn wr_progress() -> u32 {
    SESSION.with(|session| session.borrow().progress)
}

#[no_mangle]
pub extern "C" fn wr_resize(width: u32, height: u32) {
    SESSION.with(|session| {
        let mut session = session.borrow_mut();
        let width = (width as usize).clamp(16, 2560);
        let height = (height as usize).clamp(16, 1440);
        session.frame.resize(width, height);
        session.rgba = vec![0; width * height * 4];
    });
}

/// Bit field: 1 forward, 2 back, 4 left, 8 right, 16 run, 32 jump.
#[no_mangle]
pub extern "C" fn wr_input(bits: u32) {
    SESSION.with(|session| session.borrow_mut().input = bits);
}

#[no_mangle]
pub extern "C" fn wr_look(dx: f32, dy: f32) {
    SESSION.with(|session| {
        let mut session = session.borrow_mut();
        if let Some(player) = session.player.as_mut() {
            player.look(dx, dy);
        }
    });
}

/// Advances the player and renders one frame. Returns a pointer to RGBA bytes.
#[no_mangle]
pub extern "C" fn wr_frame(dt_seconds: f32) -> *const u8 {
    SESSION.with(|session| {
        let mut session = session.borrow_mut();
        let Session {
            walk,
            player,
            frame,
            rgba,
            input,
            ..
        } = &mut *session;
        let (Some(walk), Some(player)) = (walk.as_mut(), player.as_mut()) else {
            return std::ptr::null();
        };
        player.update(&walk.world, &Input::from_bits(*input), dt_seconds);
        crate::walk::render(walk, player, frame);

        if rgba.len() != frame.rgb.len() / 3 * 4 {
            rgba.resize(frame.rgb.len() / 3 * 4, 255);
        }
        for (i, pixel) in frame.rgb.chunks_exact(3).enumerate() {
            rgba[i * 4] = pixel[0];
            rgba[i * 4 + 1] = pixel[1];
            rgba[i * 4 + 2] = pixel[2];
            rgba[i * 4 + 3] = 255;
        }
        rgba.as_ptr()
    })
}

#[no_mangle]
pub extern "C" fn wr_width() -> u32 {
    SESSION.with(|session| session.borrow().frame.width as u32)
}

#[no_mangle]
pub extern "C" fn wr_height() -> u32 {
    SESSION.with(|session| session.borrow().frame.height as u32)
}

#[no_mangle]
pub extern "C" fn wr_player_x() -> f32 {
    SESSION.with(|s| s.borrow().player.map(|p| p.pos.x).unwrap_or(0.0))
}

#[no_mangle]
pub extern "C" fn wr_player_y() -> f32 {
    SESSION.with(|s| s.borrow().player.map(|p| p.pos.y).unwrap_or(0.0))
}

#[no_mangle]
pub extern "C" fn wr_player_z() -> f32 {
    SESSION.with(|s| s.borrow().player.map(|p| p.pos.z).unwrap_or(0.0))
}

/// Resets the player to the shoreline spawn point.
#[no_mangle]
pub extern "C" fn wr_respawn() {
    SESSION.with(|session| {
        let mut session = session.borrow_mut();
        if let Some(walk) = session.walk.as_ref() {
            session.player = Some(Player::spawn(&walk.world));
        }
    })
}

/// Half-size of the walkable island, in world units.
#[no_mangle]
pub extern "C" fn wr_world_extent() -> f32 {
    SESSION.with(|s| {
        s.borrow()
            .walk
            .as_ref()
            .map(|w| w.world.height_extent())
            .unwrap_or(0.0)
    })
}
