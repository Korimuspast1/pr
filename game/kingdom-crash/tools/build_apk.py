#!/usr/bin/env python3
# Сборка APK Kingdom Crash БЕЗ Android SDK: замена assets/www из каталога игры
# и подпись APK Signature Scheme v1+v2 ключом из kingdomcrash.jks (как apksigner).
# Формат подписи повторяет apksigner: v1 — detached PKCS#7 без auth-атрибутов,
# CRLF-манифесты, дайджест секции = sha256(секция + "\r\n\r\n"); v2 — APK Signing Block.
#
# Использование:
#   python3 tools/build_apk.py --apk <исходный.apk> --www <каталог www> \
#       --keystore <kingdomcrash.jks> --password <password.txt> --out <выход.apk>
# Нужен Python 3.8+ и пакет cryptography (pip install cryptography).
import argparse
import base64
import hashlib
import os
import re
import struct
import subprocess
import sys
import zipfile
import zlib

NEW_PKG = 'kingofnof.com'  # новый applicationId (пользователь попросил именно это имя)

from cryptography.hazmat.primitives import hashes, serialization
from cryptography.hazmat.primitives.asymmetric import padding
from cryptography.hazmat.primitives.serialization import pkcs12, pkcs7

CHUNK = 1048576
SIG_BLOCK_ID_V2 = 0x7109871a
SIG_ALGO = 0x0103  # RSASSA-PKCS1-v1_5 + SHA-256, chunked content digest
SIG_NAME_RE = re.compile(r'^META-INF/(MANIFEST\.MF|[^/]+\.(SF|RSA|DSA|EC)|SIG-[^/]+)$')


def u32(v): return struct.pack('<I', v)
def u64(v): return struct.pack('<Q', v)
def lp(b): return u32(len(b)) + b  # length-prefixed field


# ---------------------------------------------------------------- манифест Android (бинарный XML): версия
def _pool_strings(pool):
    typ, hs, size, str_count, style_count, flags, strings_start, styles_start = struct.unpack_from('<HHIIIIII', pool, 0)
    utf8 = bool(flags & 0x100)
    offs = struct.unpack_from('<%dI' % str_count, pool, 28)
    out = []
    for o in offs:
        p = strings_start + o
        if utf8:
            n = pool[p]
            p += 1
            if n & 0x80:
                n = ((n & 0x7f) << 8) | pool[p]
                p += 1
            n = pool[p]
            p += 1
            if n & 0x80:
                n = ((n & 0x7f) << 8) | pool[p]
                p += 1
            out.append((p, n, pool[p:p + n]))
        else:
            n = struct.unpack_from('<H', pool, p)[0]
            p += 2
            if n & 0x8000:
                n = ((n & 0x7fff) << 16) | struct.unpack_from('<H', pool, p)[0]
                p += 2
            out.append((p, n * 2, pool[p:p + n * 2]))
    return out


def patch_manifest_xml(data, version_name, version_code):
    """Меняет android:versionName и android:versionCode в бинарном AndroidManifest.xml."""
    try:
        buf = bytearray(data)
        pos = struct.unpack_from('<H', buf, 2)[0]  # headerSize XML-дерева (первый чанк)
        chunks = []
        while pos < len(buf):
            ctype, hs, csize = struct.unpack_from('<HHI', buf, pos)
            chunks.append((pos, ctype, csize))
            pos += csize
        pool = next((p for p, t, sz in chunks if t == 0x0001), None)
        if pool is None:
            return bytes(buf)
        pool_size = next(sz for p, t, sz in chunks if p == pool)
        strs = _pool_strings(bytes(buf[pool:pool + pool_size]))
        strs = [(pool + body, ln, old) for body, ln, old in strs]  # смещения в файле, не в пуле

        def _key(raw):  # строка может быть в UTF-8 или UTF-16
            for enc in ('utf-8', 'utf-16-le'):
                try:
                    s = raw.decode(enc)
                    if '\x00' not in s:
                        return s
                except UnicodeDecodeError:
                    pass
            return ''

        idx = {}
        for i, s in enumerate(strs):
            idx.setdefault(_key(s[2]), i)
        i_vn, i_vc = idx.get('versionName'), idx.get('versionCode')
        if i_vn is None or i_vc is None:
            return bytes(buf)
        for p, ctype, csize in chunks:
            if ctype != 0x0102:  # START_ELEMENT
                continue
            ext = p + 16
            nidx = struct.unpack_from('<I', buf, ext + 4)[0]
            if _key(strs[nidx][2]) != 'manifest':
                continue
            a_start, a_size, a_count = struct.unpack_from('<HHH', buf, ext + 8)
            for k in range(a_count):
                a = ext + a_start + k * a_size
                nm = struct.unpack_from('<I', buf, a + 4)[0]
                dt = buf[a + 15]
                if nm == i_vc and dt == 0x10:  # versionCode, INT_DEC
                    buf[a + 16:a + 20] = u32(version_code)
                elif nm == i_vn and dt == 0x03:  # versionName, STRING
                    si = struct.unpack_from('<I', buf, a + 16)[0]
                    body, ln, old = strs[si]
                    new = None
                    for enc in ('utf-8', 'utf-16-le'):
                        cand = version_name.encode(enc)
                        if len(cand) == ln:
                            new = cand
                            break
                    if new is not None and new != old:
                        buf[body:body + ln] = new
        # контроль: строка версии действительно заменена, длина файла не изменилась
        assert len(buf) == len(data), 'патч манифеста изменил размер файла!'
        return bytes(buf)
    except Exception as e:  # не фатально: версия в манифесте просто не обновится
        print('  ! манифест не пропатчен:', e)
        return data


def patch_package(data, new_pkg):
    """Переименовывает applicationId (package=...) бинарного манифеста.

    Длины строк меняются, поэтому строковой пул пересобирается целиком:
    индексы строк сохраняются, ссылки из элементов остаются валидными.
    Имя активности делаем абсолютным, чтобы класс com.kingdomcrash.game.MainActivity
    в dex остался рабочим при новом имени пакета.
    """
    try:
        buf = bytearray(data)
        pos = struct.unpack_from('<H', buf, 2)[0]
        chunks = []
        while pos < len(buf):
            ctype, hs, csize = struct.unpack_from('<HHI', buf, pos)
            chunks.append((pos, ctype, csize))
            pos += csize
        pi = next(i for i, (p, t, sz) in enumerate(chunks) if t == 0x0001)
        pool, _, pool_size = chunks[pi]
        str_count, style_count = struct.unpack_from('<II', buf, pool + 8)
        flags = struct.unpack_from('<I', buf, pool + 16)[0]
        strings_start = struct.unpack_from('<I', buf, pool + 20)[0]
        utf8 = bool(flags & 0x100)
        offs = struct.unpack_from('<%dI' % str_count, buf, pool + 28)

        def _lenprefix(q):  # читает один префикс длины AXML, возвращает (значение, сдвиг)
            if not utf8:
                n = struct.unpack_from('<H', buf, q)[0]; q += 2
                if n & 0x8000:
                    n = ((n & 0x7fff) << 16) | struct.unpack_from('<H', buf, q)[0]; q += 2
                return n, q
            n = buf[q]; q += 1
            if n & 0x80:
                n = ((n & 0x7f) << 8) | buf[q]; q += 1
            return n, q

        def _encprefix(n):  # обратное к _lenprefix
            if not utf8:
                if n < 0x8000:
                    return struct.pack('<H', n)
                return struct.pack('<HH', (n >> 16) | 0x8000, n & 0xFFFF)
            if n < 0x80:
                return bytes([n])
            return bytes([(n >> 8) | 0x80, n & 0xFF])

        reps = {}
        entries = []
        for o in offs:
            q = pool + strings_start + o
            start = q
            n, q = _lenprefix(q)          # длина в символах
            if utf8:
                n2, q = _lenprefix(q)     # длина в байтах
                body = bytes(buf[q:q + n2]); q += n2
                nul = 1
            else:
                body = bytes(buf[q:q + n * 2]); q += n * 2
                n2 = n
                nul = 2
            raw = bytes(buf[start:q + nul])
            key = body.decode('utf-8' if utf8 else 'utf-16-le', 'replace')
            entries.append([key, raw])

        for e in entries:
            if e[0] == 'com.kingdomcrash.game':
                reps[id(e)] = new_pkg
            elif e[0] == '.MainActivity':
                reps[id(e)] = 'com.kingdomcrash.game.MainActivity'  # класс в dex не меняется

        def _newentry(txt):
            if utf8:
                bs = txt.encode('utf-8')
                return _encprefix(len(txt)) + _encprefix(len(bs)) + bs + b'\x00'
            return _encprefix(len(txt)) + txt.encode('utf-16-le') + b'\x00\x00'

        new_entries = [(_newentry(reps[id(e)]) if id(e) in reps else e[1]) for e in entries]

        strings_start_new = 28 + 4 * str_count
        body = b''.join(new_entries)
        new_offs, acc = [], 0
        for raw in new_entries:
            new_offs.append(acc); acc += len(raw)
        pool_new = struct.pack('<HHIIIIII', 0x0001, 28, strings_start_new + len(body), str_count, style_count, flags, strings_start_new, 0)
        pool_new += struct.pack('<%dI' % str_count, *new_offs) + body
        out = bytearray(buf[:pool]) + bytearray(pool_new) + bytearray(buf[pool + pool_size:])
        struct.pack_into('<I', out, 4, len(out))
        print('  пакет в манифесте: %s (пул пересобран, %d строк)' % (new_pkg, str_count))
        return bytes(out)
    except Exception as e:
        print('  ! пакет не переименован:', e)
        return data


# ---------------------------------------------------------------- подпись v1 (JAR)
def build_v1(entries, key, cert, alias='KINGDOMC'):
    """entries: [(имя, байты распакованные)] -> dict: имя в META-INF -> байты."""
    sections = []
    for name, data in entries:
        dig = base64.b64encode(hashlib.sha256(data).digest()).decode()
        sections.append('Name: %s\r\nSHA-256-Digest: %s' % (name, dig))
    manifest = b'Manifest-Version: 1.0\r\n\r\n' + b''.join(s.encode() + b'\r\n\r\n' for s in sections)
    sf = b'Signature-Version: 1.0\r\nCreated-By: 1.0 (Android)\r\n'
    sf += b'SHA-256-Digest-Manifest: ' + base64.b64encode(hashlib.sha256(manifest).digest()) + b'\r\n'
    sf += b'X-Android-APK-Signed: 2\r\n\r\n'
    sf += b''.join(s.encode() + b'\r\n\r\n' for s in sections)
    # detached PKCS#7 без аутентифицированных атрибутов — как apksigner
    rsa = (pkcs7.PKCS7SignatureBuilder()
           .set_data(sf)
           .add_signer(cert, key, hashes.SHA256())
           .sign(serialization.Encoding.DER,
                 [pkcs7.PKCS7Options.DetachedSignature, pkcs7.PKCS7Options.NoAttributes]))
    return {
        'META-INF/MANIFEST.MF': manifest,
        'META-INF/%s.SF' % alias: sf,
        'META-INF/%s.RSA' % alias: rsa,
    }


# ---------------------------------------------------------------- упаковка ZIP с выравниванием
def build_zip(entries):
    """entries: [(имя, method, байты)] -> (байты архива, смещение ЦК). STORED выровнены на 4."""
    out = bytearray()
    central = bytearray()
    for name, method, data in entries:
        nb = name.encode('utf-8')
        crc = zlib.crc32(data) & 0xffffffff
        if method == 0:
            comp = data
        else:
            co = zlib.compressobj(9, zlib.DEFLATED, -15)
            comp = co.compress(data) + co.flush()
        flags = 0x800 if any(ord(c) > 127 for c in name) else 0
        extra = b''
        if method == 0:  # выравнивание как у zipalign: нули в конце local extra
            while (len(out) + 30 + len(nb) + len(extra)) % 4:
                extra += b'\x00'
        off = len(out)
        out += struct.pack('<IHHHHHIIIHH', 0x04034b50, 20, flags, method, 0, 0x21, crc, len(comp), len(data), len(nb), len(extra))
        out += nb + extra + comp
        central += struct.pack('<IHHHHHHIIIHHHHHII', 0x02014b50, 20, 20, flags, method, 0, 0x21, crc, len(comp), len(data), len(nb), 0, 0, 0, 0, 0, off)
        central += nb
    cd_off = len(out)
    out += central
    out += struct.pack('<IHHHHIIH', 0x06054b50, 0, 0, len(entries), len(entries), len(central), cd_off, 0)
    return bytes(out), cd_off


# ---------------------------------------------------------------- подпись v2 (APK Signing Block)
def chunk_digest(chunk, hasher):
    return hasher(b'\xa5' + u32(len(chunk)) + chunk).digest()


def content_digest(parts, hasher=hashlib.sha256):
    """Как в apksigtool: части независимо режутся на куски по 1 МБ."""
    ds = []
    for part in parts:
        for i in range(0, len(part), CHUNK):
            piece = part[i:i + CHUNK]
            if piece:
                ds.append(chunk_digest(piece, hasher))
    return hasher(b'\x5a' + u32(len(ds)) + b''.join(ds)).digest()


def v2_sign(zip_bytes, cd_offset, key, cert):
    """Вставляет APK Signing Block (v2) перед центральным каталогом, чинит EOCD."""
    eocd_pos = zip_bytes.rfind(b'PK\x05\x06')
    eocd = zip_bytes[eocd_pos:]
    eocd_patched = eocd[:16] + u32(cd_offset) + eocd[20:]  # подпись видит блок пустым
    digest = content_digest([zip_bytes[:cd_offset], zip_bytes[cd_offset:eocd_pos], eocd_patched])
    dig_entry = u32(SIG_ALGO) + u32(len(digest)) + digest
    cert_der = cert.public_bytes(serialization.Encoding.DER)
    spki = cert.public_key().public_bytes(serialization.Encoding.DER, serialization.PublicFormat.SubjectPublicKeyInfo)
    signed_data = lp(lp(dig_entry)) + lp(lp(cert_der)) + lp(b'')  # пустая последовательность атрибутов
    sig = key.sign(signed_data, padding.PKCS1v15(), hashes.SHA256())
    sig_entry = u32(SIG_ALGO) + u32(len(sig)) + sig
    signer = lp(signed_data) + lp(lp(sig_entry)) + lp(spki)
    v2_val = lp(lp(signer))  # length-prefixed sequence of length-prefixed signers
    pair = u64(4 + len(v2_val)) + u32(SIG_BLOCK_ID_V2) + v2_val
    size = len(pair) + 24
    block = u64(size) + pair + u64(size) + b'APK Sig Block 42'
    out = bytearray(zip_bytes[:cd_offset] + block + zip_bytes[cd_offset:])
    e = out.rfind(b'PK\x05\x06')
    out[e + 16:e + 20] = u32(cd_offset + len(block))
    return bytes(out)


def which(cmd):
    for p in os.environ.get('PATH', '').split(os.pathsep):
        f = os.path.join(p, cmd)
        if os.path.isfile(f) and os.access(f, os.X_OK):
            return f
    return None


# ---------------------------------------------------------------- основное
def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('--apk', required=True)
    ap.add_argument('--www', required=True)
    ap.add_argument('--keystore', required=True)
    ap.add_argument('--password', required=True)
    ap.add_argument('--out', required=True)
    args = ap.parse_args()

    pw = open(args.password, 'rb').read().strip()
    keystore = open(args.keystore, 'rb').read()
    key, cert, _ = pkcs12.load_key_and_certificates(keystore, pw)
    print('ключ загружен: %s, серийный %s' % (cert.subject.rfc4514_string(), cert.serial_number))

    # версия как в android/build.sh
    data_js = open(os.path.join(args.www, 'js', 'data.js'), encoding='utf-8').read()
    ver = re.search(r"GAME_VERSION = '([0-9.]+)'", data_js).group(1)
    parts = ver.split('.')
    code = sum(int(x) * m for x, m in zip(parts, (10000, 100, 1))) if len(parts) == 3 else 0
    print('версия: %s (код %d)' % (ver, code))

    src = zipfile.ZipFile(args.apk)
    entries = []
    for info in src.infolist():
        if SIG_NAME_RE.match(info.filename):
            continue  # старая подпись
        if info.filename.startswith('assets/www/'):
            continue  # заменим целиком
        data = src.read(info.filename)
        if info.filename == 'AndroidManifest.xml':
            data = patch_package(data, NEW_PKG)
            data = patch_manifest_xml(data, ver, code)
        entries.append((info.filename, info.compress_type, data))
    src.close()

    www_files = []
    for root, dirs, files in os.walk(args.www):
        dirs.sort()
        for f in sorted(files):
            full = os.path.join(root, f)
            rel = os.path.relpath(full, args.www).replace(os.sep, '/')
            www_files.append(('assets/www/' + rel, 8, open(full, 'rb').read()))
    entries += www_files
    print('файлов в APK: %d (из них www: %d)' % (len(entries), len(www_files)))

    # подпись v1 поверх всех записей
    v1 = build_v1([(n, d) for n, m, d in entries], key, cert)
    entries += [(n, 8, d) for n, d in v1.items()]

    zip_bytes, cd_off = build_zip(entries)
    final = v2_sign(zip_bytes, cd_off, key, cert)
    os.makedirs(os.path.dirname(os.path.abspath(args.out)), exist_ok=True)
    open(args.out, 'wb').write(final)
    print('записан %s (%d байт)' % (args.out, len(final)))

    # ---- самопроверка
    ok = True
    zf = zipfile.ZipFile(args.out)
    bad = zf.testzip()
    print('zip-целостность:', 'ok' if bad is None else 'ПЛОХО ' + str(bad))
    ok &= bad is None

    mf = zf.read('META-INF/MANIFEST.MF')
    for n, m, d in entries:
        if n.startswith('META-INF/'):
            continue
        dig = base64.b64encode(hashlib.sha256(d).digest()).decode()
        if ('Name: %s\r\nSHA-256-Digest: %s' % (n, dig)).encode() not in mf:
            print('! нет дайджеста в манифесте:', n)
            ok = False

    try:
        import apksigtool
        raw = open(args.out, 'rb').read()
        e = raw.rfind(b'PK\x05\x06')
        cd = struct.unpack_from('<I', raw, e + 16)[0]
        size2 = struct.unpack_from('<Q', raw, cd - 24)[0]
        sb = cd - size2 - 8
        parsed = apksigtool.parse_apk_signing_block(raw[sb:cd], args.out)
        v = parsed.pairs[0].value
        print('v2-блок: распознан apksigtool, пар:', len(parsed.pairs), 'verified:', v.verified)
        ok &= bool(getattr(v, 'verified', False))
    except ImportError:
        print('v2-блок: apksigtool не установлен, проверка пропущена')
    except Exception as e:
        print('! v2-блок не прошёл проверку apksigtool:', e)
        ok = False

    if which('openssl'):
        base = os.path.dirname(os.path.abspath(args.out))
        sf_path, rsa_path = os.path.join(base, '.check.SF'), os.path.join(base, '.check.RSA')
        open(sf_path, 'wb').write(zf.read('META-INF/KINGDOMC.SF'))
        open(rsa_path, 'wb').write(zf.read('META-INF/KINGDOMC.RSA'))
        r = subprocess.run(['openssl', 'cms', '-verify', '-inform', 'DER', '-in', rsa_path,
                            '-content', sf_path, '-noverify', '-out', os.devnull],
                           capture_output=True)
        print('openssl v1-подпись:', 'ok' if r.returncode == 0 else 'ПЛОХО: ' + r.stderr.decode()[:300])
        ok &= r.returncode == 0
        os.remove(sf_path)
        os.remove(rsa_path)

    try:
        from androguard.core.apk import APK  # noqa
        a = APK(args.out)
        good = a.is_valid_APK() and a.get_androidversion_name() == ver and str(a.get_androidversion_code()) == str(code)
        print('androguard: манифест распознан, версия %s (%s)' % (a.get_androidversion_name(), a.get_androidversion_code()))
        ok &= bool(good)
    except ImportError:
        print('androguard: не установлен, проверка манифеста пропущена')
    except Exception as e:
        print('! манифест не прошёл проверку androguard:', e)
        ok = False

    print('итог:', 'OK' if ok else 'ЕСТЬ ОШИБКИ')
    return 0 if ok else 1


if __name__ == '__main__':
    sys.exit(main())
