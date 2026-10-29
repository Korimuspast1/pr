//! A very small HTTP server for the interactive preview.
//!
//! Only two routes exist: the page itself and `/api/render`, which returns a
//! freshly ray-marched PNG for the requested parameters. No crates, no async,
//! no keep-alive: one request, one response, one thread.

use std::io::{BufRead, BufReader, Write};
use std::net::{TcpListener, TcpStream};
use std::sync::Arc;

use crate::render::{render, RenderSettings};
use crate::scene_from_query;
use crate::World;

const PAGE: &str = include_str!("../web/index.html");

pub fn serve(port: u16) -> std::io::Result<()> {
    let listener = TcpListener::bind(("0.0.0.0", port))?;
    println!("world-render preview: http://localhost:{port}");
    println!("press Ctrl+C to stop");
    for stream in listener.incoming() {
        let Ok(stream) = stream else { continue };
        std::thread::spawn(move || {
            if let Err(err) = handle(stream) {
                eprintln!("request failed: {err}");
            }
        });
    }
    Ok(())
}

fn handle(stream: TcpStream) -> std::io::Result<()> {
    let mut writer = stream.try_clone()?;
    let mut reader = BufReader::new(stream);

    let mut request_line = String::new();
    if reader.read_line(&mut request_line)? == 0 {
        return Ok(());
    }
    // Drain the headers; this server never reads a body.
    loop {
        let mut line = String::new();
        if reader.read_line(&mut line)? == 0 || line == "\r\n" || line == "\n" {
            break;
        }
    }

    let mut parts = request_line.split_whitespace();
    let method = parts.next().unwrap_or("");
    let target = parts.next().unwrap_or("/");
    if method != "GET" && method != "HEAD" {
        return respond_text(&mut writer, 405, "method not allowed");
    }

    let (path, query) = match target.split_once('?') {
        Some((path, query)) => (path, query),
        None => (target, ""),
    };

    match path {
        "/" | "/index.html" => respond(
            &mut writer,
            200,
            "text/html; charset=utf-8",
            PAGE.as_bytes(),
        ),
        "/api/render" => {
            let params = PreviewParams::from_query(query);
            let bytes = render_png(&params);
            respond(&mut writer, 200, "image/png", &bytes)
        }
        "/favicon.ico" => respond(&mut writer, 204, "image/x-icon", &[]),
        _ => respond_text(&mut writer, 404, "not found"),
    }
}

#[derive(Clone, Debug)]
struct PreviewParams {
    preset: String,
    seed: u32,
    width: usize,
    height: usize,
    samples: u32,
}

impl PreviewParams {
    fn from_query(query: &str) -> Self {
        let mut params = PreviewParams {
            preset: "day".to_string(),
            seed: 7,
            width: 960,
            height: 540,
            samples: 1,
        };
        for pair in query.split('&') {
            let Some((key, value)) = pair.split_once('=') else {
                continue;
            };
            let value = percent_decode(value);
            match key {
                "preset" => params.preset = value,
                "seed" => params.seed = value.parse().unwrap_or(7),
                "width" => params.width = value.parse().unwrap_or(960),
                "height" => params.height = value.parse().unwrap_or(540),
                "samples" => params.samples = value.parse().unwrap_or(1),
                _ => {}
            }
        }
        params.width = params.width.clamp(160, 2560);
        params.height = params.height.clamp(90, 1440);
        params.samples = params.samples.clamp(1, 3);
        params
    }

    fn cache_key(&self) -> String {
        format!(
            "{}:{}:{}x{}:{}",
            self.preset, self.seed, self.width, self.height, self.samples
        )
    }
}

fn percent_decode(value: &str) -> String {
    let bytes = value.as_bytes();
    let mut out = Vec::with_capacity(bytes.len());
    let mut i = 0;
    while i < bytes.len() {
        match bytes[i] {
            b'%' if i + 2 < bytes.len() => {
                let hex = std::str::from_utf8(&bytes[i + 1..i + 3]).unwrap_or("");
                match u8::from_str_radix(hex, 16) {
                    Ok(byte) => {
                        out.push(byte);
                        i += 3;
                    }
                    Err(_) => {
                        out.push(bytes[i]);
                        i += 1;
                    }
                }
            }
            b'+' => {
                out.push(b' ');
                i += 1;
            }
            byte => {
                out.push(byte);
                i += 1;
            }
        }
    }
    String::from_utf8_lossy(&out).into_owned()
}

fn render_png(params: &PreviewParams) -> Arc<Vec<u8>> {
    let started = std::time::Instant::now();
    let (scene, camera) = scene_from_query(&params.preset, params.seed);
    let world = World::build(params.seed);
    let pixels = render(
        &world,
        &scene,
        &camera,
        RenderSettings {
            width: params.width,
            height: params.height,
            samples: params.samples,
        },
    );
    let png = crate::png::encode_rgb_png(params.width, params.height, &pixels);
    println!(
        "preview {} in {:.2}s ({} bytes)",
        params.cache_key(),
        started.elapsed().as_secs_f64(),
        png.len()
    );
    Arc::new(png)
}

fn respond(
    stream: &mut TcpStream,
    status: u16,
    content_type: &str,
    body: &[u8],
) -> std::io::Result<()> {
    let reason = match status {
        200 => "OK",
        204 => "No Content",
        404 => "Not Found",
        405 => "Method Not Allowed",
        _ => "OK",
    };
    let header = format!(
        "HTTP/1.1 {status} {reason}\r\nContent-Type: {content_type}\r\nContent-Length: {}\r\nCache-Control: no-store\r\nConnection: close\r\n\r\n",
        body.len()
    );
    stream.write_all(header.as_bytes())?;
    stream.write_all(body)?;
    stream.flush()
}

fn respond_text(stream: &mut TcpStream, status: u16, body: &str) -> std::io::Result<()> {
    respond(stream, status, "text/plain; charset=utf-8", body.as_bytes())
}
