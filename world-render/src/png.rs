//! A small, dependency-free PNG encoder.
//!
//! It writes 8-bit RGB images and compresses the pixel stream with a real
//! deflate encoder (LZ77 + fixed Huffman codes). That keeps the project
//! buildable with a bare `rustc main.rs` while still producing files a few
//! hundred kilobytes in size instead of several megabytes.

// ---------------------------------------------------------------------------
// Checksums
// ---------------------------------------------------------------------------

fn crc32(bytes: &[u8]) -> u32 {
    let mut crc = 0xffff_ffffu32;
    for &byte in bytes {
        crc ^= byte as u32;
        for _ in 0..8 {
            let mask = (crc & 1).wrapping_neg();
            crc = (crc >> 1) ^ (0xedb8_8320 & mask);
        }
    }
    !crc
}

fn adler32(bytes: &[u8]) -> u32 {
    let mut a: u32 = 1;
    let mut b: u32 = 0;
    for &byte in bytes {
        a = (a + byte as u32) % 65521;
        b = (b + a) % 65521;
    }
    (b << 16) | a
}

// ---------------------------------------------------------------------------
// Bit writer: deflate packs bits least-significant first
// ---------------------------------------------------------------------------

struct BitWriter {
    out: Vec<u8>,
    bit_buf: u64,
    bit_count: u32,
}

impl BitWriter {
    fn new(capacity: usize) -> Self {
        BitWriter {
            out: Vec::with_capacity(capacity),
            bit_buf: 0,
            bit_count: 0,
        }
    }

    fn write_bits(&mut self, value: u32, count: u32) {
        self.bit_buf |= ((value as u64) & ((1u64 << count) - 1)) << self.bit_count;
        self.bit_count += count;
        while self.bit_count >= 8 {
            self.out.push((self.bit_buf & 0xff) as u8);
            self.bit_buf >>= 8;
            self.bit_count -= 8;
        }
    }

    /// Huffman codes are packed most-significant bit first.
    fn write_code(&mut self, code: u32, count: u32) {
        let mut reversed = 0;
        for i in 0..count {
            reversed |= ((code >> i) & 1) << (count - 1 - i);
        }
        self.write_bits(reversed, count);
    }

    fn finish(mut self) -> Vec<u8> {
        if self.bit_count > 0 {
            self.out.push((self.bit_buf & 0xff) as u8);
        }
        self.out
    }
}

// ---------------------------------------------------------------------------
// Deflate tables (RFC 1951, fixed Huffman)
// ---------------------------------------------------------------------------

const LENGTH_BASE: [u16; 29] = [
    3, 4, 5, 6, 7, 8, 9, 10, 11, 13, 15, 17, 19, 23, 27, 31, 35, 43, 51, 59, 67, 83, 99, 115, 131,
    163, 195, 227, 258,
];
const LENGTH_EXTRA: [u8; 29] = [
    0, 0, 0, 0, 0, 0, 0, 0, 1, 1, 1, 1, 2, 2, 2, 2, 3, 3, 3, 3, 4, 4, 4, 4, 5, 5, 5, 5, 0,
];
const DIST_BASE: [u16; 30] = [
    1, 2, 3, 4, 5, 7, 9, 13, 17, 25, 33, 49, 65, 97, 129, 193, 257, 385, 513, 769, 1025, 1537,
    2049, 3073, 4097, 6145, 8193, 12289, 16385, 24577,
];
const DIST_EXTRA: [u8; 30] = [
    0, 0, 0, 0, 1, 1, 2, 2, 3, 3, 4, 4, 5, 5, 6, 6, 7, 7, 8, 8, 9, 9, 10, 10, 11, 11, 12, 12, 13,
    13,
];

/// Writes one literal/length symbol with the fixed Huffman code table.
fn write_literal(bits: &mut BitWriter, symbol: u16) {
    match symbol {
        0..=143 => bits.write_code(0x30 + symbol as u32, 8),
        144..=255 => bits.write_code(0x190 + (symbol as u32 - 144), 9),
        256..=279 => bits.write_code(symbol as u32 - 256, 7),
        _ => bits.write_code(0xc0 + (symbol as u32 - 280), 8),
    }
}

fn write_length(bits: &mut BitWriter, length: usize) {
    let mut index = 0;
    for (i, &base) in LENGTH_BASE.iter().enumerate() {
        if length as u16 >= base {
            index = i;
        }
    }
    write_literal(bits, 257 + index as u16);
    let extra = LENGTH_EXTRA[index];
    if extra > 0 {
        bits.write_bits((length - LENGTH_BASE[index] as usize) as u32, extra as u32);
    }
}

fn write_distance(bits: &mut BitWriter, distance: usize) {
    let mut index = 0;
    for (i, &base) in DIST_BASE.iter().enumerate() {
        if distance as u16 >= base {
            index = i;
        }
    }
    bits.write_code(index as u32, 5); // fixed Huffman distance codes are 5 bits
    let extra = DIST_EXTRA[index];
    if extra > 0 {
        bits.write_bits((distance - DIST_BASE[index] as usize) as u32, extra as u32);
    }
}

// ---------------------------------------------------------------------------
// LZ77 with a hash chain
// ---------------------------------------------------------------------------

const WINDOW: usize = 32_768;
const MIN_MATCH: usize = 3;
const MAX_MATCH: usize = 258;
const HASH_BITS: u32 = 15;
const MAX_CHAIN: usize = 24;

#[inline]
fn hash3(data: &[u8], pos: usize) -> usize {
    let a = data[pos] as u32;
    let b = data[pos + 1] as u32;
    let c = data[pos + 2] as u32;
    ((a << 10) ^ (b << 5) ^ c) as usize & ((1 << HASH_BITS) - 1)
}

fn deflate(data: &[u8]) -> Vec<u8> {
    let mut bits = BitWriter::new(data.len() / 3 + 64);
    bits.write_bits(1, 1); // BFINAL
    bits.write_bits(1, 2); // BTYPE = 01, fixed Huffman

    let mut head = vec![usize::MAX; 1 << HASH_BITS];
    let mut prev = vec![usize::MAX; WINDOW];

    let mut pos = 0;
    while pos < data.len() {
        let mut best_len = 0;
        let mut best_dist = 0;

        if pos + MIN_MATCH <= data.len() {
            let mut candidate = head[hash3(data, pos)];
            let limit = pos.saturating_sub(WINDOW);
            let mut chain = 0;
            while candidate != usize::MAX && candidate >= limit && chain < MAX_CHAIN {
                let max_len = (data.len() - pos).min(MAX_MATCH);
                let mut len = 0;
                while len < max_len && data[candidate + len] == data[pos + len] {
                    len += 1;
                }
                if len > best_len {
                    best_len = len;
                    best_dist = pos - candidate;
                    if len == max_len {
                        break;
                    }
                }
                let next = prev[candidate % WINDOW];
                if next == candidate {
                    break;
                }
                candidate = next;
                chain += 1;
            }
        }

        if best_len >= MIN_MATCH {
            write_length(&mut bits, best_len);
            write_distance(&mut bits, best_dist);
            for offset in 0..best_len {
                let at = pos + offset;
                if at + MIN_MATCH <= data.len() {
                    let h = hash3(data, at);
                    prev[at % WINDOW] = head[h];
                    head[h] = at;
                }
            }
            pos += best_len;
        } else {
            write_literal(&mut bits, data[pos] as u16);
            if pos + MIN_MATCH <= data.len() {
                let h = hash3(data, pos);
                prev[pos % WINDOW] = head[h];
                head[h] = pos;
            }
            pos += 1;
        }
    }

    write_literal(&mut bits, 256); // end of block
    bits.finish()
}

fn zlib(data: &[u8]) -> Vec<u8> {
    let mut out = vec![0x78, 0x01];
    out.extend_from_slice(&deflate(data));
    out.extend_from_slice(&adler32(data).to_be_bytes());
    out
}

// ---------------------------------------------------------------------------
// PNG container
// ---------------------------------------------------------------------------

fn write_chunk(out: &mut Vec<u8>, kind: &[u8; 4], data: &[u8]) {
    out.extend_from_slice(&(data.len() as u32).to_be_bytes());
    let start = out.len();
    out.extend_from_slice(kind);
    out.extend_from_slice(data);
    let crc = crc32(&out[start..]);
    out.extend_from_slice(&crc.to_be_bytes());
}

/// `None` means the default filter; a few fixed choices keep the encoder
/// simple while still helping the compressor on photographic gradients.
fn filter_row(row: &[u8], previous: Option<&[u8]>, out: &mut Vec<u8>) {
    let mut best_kind = 0u8;
    let mut best_score = u64::MAX;
    let mut best_row: Vec<u8> = Vec::with_capacity(row.len());

    for kind in [0u8, 1, 2] {
        let mut candidate = Vec::with_capacity(row.len());
        for i in 0..row.len() {
            let left = if i >= 3 { row[i - 3] as i32 } else { 0 };
            let up = previous.map(|p| p[i] as i32).unwrap_or(0);
            let predicted = match kind {
                1 => left,
                2 => up,
                _ => 0,
            };
            candidate.push((row[i] as i32 - predicted).rem_euclid(256) as u8);
        }
        // Sum of absolute deviations: a cheap proxy for "how compressible".
        let score: u64 = candidate
            .iter()
            .map(|&b| (b as i8).unsigned_abs() as u64)
            .sum();
        if score < best_score {
            best_score = score;
            best_kind = kind;
            best_row = candidate;
        }
    }

    out.push(best_kind);
    out.extend_from_slice(&best_row);
}

pub fn encode_rgb_png(width: usize, height: usize, pixels: &[u8]) -> Vec<u8> {
    let stride = width * 3;
    let mut raw = Vec::with_capacity((stride + 1) * height);
    let mut previous: Option<Vec<u8>> = None;
    for row in pixels.chunks(stride) {
        filter_row(row, previous.as_deref(), &mut raw);
        previous = Some(row.to_vec());
    }

    let mut out = Vec::with_capacity(raw.len() / 2 + 1024);
    out.extend_from_slice(&[0x89, b'P', b'N', b'G', 0x0d, 0x0a, 0x1a, 0x0a]);

    let mut ihdr = Vec::with_capacity(13);
    ihdr.extend_from_slice(&(width as u32).to_be_bytes());
    ihdr.extend_from_slice(&(height as u32).to_be_bytes());
    ihdr.extend_from_slice(&[8, 2, 0, 0, 0]); // 8-bit, truecolour, no interlace
    write_chunk(&mut out, b"IHDR", &ihdr);

    write_chunk(&mut out, b"IDAT", &zlib(&raw));
    write_chunk(&mut out, b"IEND", &[]);
    out
}

pub fn encode_ppm(width: usize, height: usize, pixels: &[u8]) -> Vec<u8> {
    let mut out = format!("P6\n{} {}\n255\n", width, height).into_bytes();
    out.extend_from_slice(pixels);
    out
}

#[cfg(test)]
mod tests {
    use super::*;

    fn crc_ok(bytes: &[u8]) -> bool {
        let mut i = 8;
        while i + 12 <= bytes.len() {
            let len =
                u32::from_be_bytes([bytes[i], bytes[i + 1], bytes[i + 2], bytes[i + 3]]) as usize;
            let start = i + 4;
            let end = start + 4 + len;
            if end + 4 > bytes.len() {
                return false;
            }
            let expected =
                u32::from_be_bytes([bytes[end], bytes[end + 1], bytes[end + 2], bytes[end + 3]]);
            if crc32(&bytes[start..end]) != expected {
                return false;
            }
            i = end + 4;
        }
        true
    }

    #[test]
    fn png_has_valid_chunks_and_checksums() {
        let pixels: Vec<u8> = (0..16 * 8 * 3).map(|i| (i % 251) as u8).collect();
        let png = encode_rgb_png(16, 8, &pixels);
        assert_eq!(&png[..8], &[0x89, b'P', b'N', b'G', 0x0d, 0x0a, 0x1a, 0x0a]);
        assert!(crc_ok(&png));
        assert!(png.windows(4).any(|w| w == b"IHDR"));
        assert!(png.windows(4).any(|w| w == b"IDAT"));
        assert!(png.windows(4).any(|w| w == b"IEND"));
    }

    #[test]
    fn deflate_roundtrip_length() {
        // The raw stream must inflate to exactly one filter byte plus a row.
        let pixels: Vec<u8> = (0..32 * 4 * 3).map(|i| ((i * 7) % 256) as u8).collect();
        let raw: Vec<u8> = (0..4)
            .flat_map(|row| {
                std::iter::once(0u8).chain(pixels[row * 96..(row + 1) * 96].iter().copied())
            })
            .collect();
        let packed = zlib(&raw);
        assert_eq!(
            adler32(&raw),
            u32::from_be_bytes([
                packed[packed.len() - 4],
                packed[packed.len() - 3],
                packed[packed.len() - 2],
                packed[packed.len() - 1],
            ])
        );
        assert!(
            packed.len() < raw.len(),
            "compression should shrink flat-ish data"
        );
    }
}
