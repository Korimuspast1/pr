// Минимальная (де)сериализация TL (Binary Data Serialization) — ровно то,
// что нужно для создания auth_key. Writer попутно строит «карту полей»:
// для каждого поля известны смещение и длина, благодаря чему интерфейс
// умеет подсвечивать байты в hex-дампе.

import { concat, hex } from './bytes.js';

export class TLWriter {
  constructor() {
    this.chunks = [];
    this.length = 0;
    this.fields = [];
  }

  _push(bytes, field) {
    const offset = this.length;
    this.chunks.push(bytes);
    this.length += bytes.length;
    if (field) {
      this.fields.push({
        name: field.name,
        type: field.type ?? '',
        offset,
        length: bytes.length,
        hex: hex(bytes),
        display: field.display ?? hex(bytes),
        comment: field.comment ?? '',
      });
    }
    return this;
  }

  /** Номер конструктора (4 байта, little endian). */
  constructorId(name, id, comment = 'номер конструктора из TL-схемы') {
    const bytes = new Uint8Array(4);
    new DataView(bytes.buffer).setUint32(0, id, true);
    return this._push(bytes, { name: `%(${name})`, type: '', display: id.toString(16).padStart(8, '0'), comment });
  }

  int(name, value, comment = '') {
    const bytes = new Uint8Array(4);
    new DataView(bytes.buffer).setInt32(0, value, true);
    return this._push(bytes, { name, type: 'int', display: String(value), comment });
  }

  long(name, value, comment = '') {
    // value: BigInt или Uint8Array(8) little-endian «как есть»
    let bytes;
    if (value instanceof Uint8Array) {
      bytes = value.slice(0, 8);
    } else {
      bytes = new Uint8Array(8);
      new DataView(bytes.buffer).setBigUint64(0, BigInt.asUintN(64, value), true);
    }
    return this._push(bytes, { name, type: 'long', display: hex(bytes), comment });
  }

  /** «Сырые» 16/32 байта (int128 / int256). */
  raw(name, bytes, type = '', comment = '') {
    return this._push(bytes.slice(), { name, type, display: hex(bytes), comment });
  }

  /** TL-строка/bytes: префикс длины + данные + выравнивание до 4 байт. */
  bytes(name, data, comment = '') {
    const parts = [];
    if (data.length <= 253) {
      parts.push(new Uint8Array([data.length]));
    } else {
      parts.push(new Uint8Array([254, data.length & 0xff, (data.length >> 8) & 0xff, (data.length >> 16) & 0xff]));
    }
    parts.push(data);
    const used = parts.reduce((n, p) => n + p.length, 0);
    const pad = (4 - (used % 4)) % 4;
    if (pad) parts.push(new Uint8Array(pad));
    const bytes = concat(...parts);
    return this._push(bytes, {
      name,
      type: 'bytes',
      display: hex(data),
      comment: comment || `префикс длины (${data.length} байт) + данные${pad ? ` + ${pad} байт выравнивания` : ''}`,
    });
  }

  /** vector<long> из массива Uint8Array(8). */
  vectorLong(name, items, comment = '') {
    this.constructorId('vector', 0x1cb5c415, 'конструктор Vector t');
    this.int(`${name}.count`, items.length, 'число элементов');
    items.forEach((item, i) => this.long(`${name}[${i}]`, item, comment));
    return this;
  }

  result() {
    return concat(...this.chunks);
  }
}

export class TLReader {
  constructor(bytes) {
    this.buf = bytes;
    this.offset = 0;
    this.view = new DataView(bytes.buffer, bytes.byteOffset, bytes.byteLength);
  }

  constructorId() {
    const v = this.view.getUint32(this.offset, true);
    this.offset += 4;
    return v;
  }

  int() {
    const v = this.view.getInt32(this.offset, true);
    this.offset += 4;
    return v;
  }

  long() {
    const v = this.buf.slice(this.offset, this.offset + 8);
    this.offset += 8;
    return v;
  }

  raw(n) {
    const v = this.buf.slice(this.offset, this.offset + n);
    this.offset += n;
    return v;
  }

  bytes() {
    let len = this.buf[this.offset];
    let start;
    if (len <= 253) {
      start = this.offset + 1;
    } else {
      len = this.buf[this.offset + 1] | (this.buf[this.offset + 2] << 8) | (this.buf[this.offset + 3] << 16);
      start = this.offset + 4;
    }
    const data = this.buf.slice(start, start + len);
    let end = start + len;
    end += (4 - ((end - this.offset) % 4)) % 4;
    this.offset = end;
    return data;
  }
}

/** Оборачивает тело в незашифрованное сообщение MTProto. */
export function plainMessage(body, messageId, bodyFields = []) {
  const writer = new TLWriter();
  writer.long('auth_key_id', 0n, 'равен 0 — сообщение передаётся открытым текстом');
  writer.long('message_id', messageId, 'идентификатор сообщения: (unixtime << 32) + 4N');
  writer.int('message_length', body.length, 'длина тела сообщения');
  const header = writer.result();
  const fields = writer.fields.concat(
    bodyFields.map((f) => ({ ...f, offset: f.offset + header.length }))
  );
  return { bytes: concat(header, body), fields };
}

let messageCounter = 0;
export function newMessageId() {
  const unixtime = BigInt(Math.floor(Date.now() / 1000));
  messageCounter += 4;
  return (unixtime << 32n) + BigInt(messageCounter);
}
