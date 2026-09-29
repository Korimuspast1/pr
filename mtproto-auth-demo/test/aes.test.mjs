// Проверка AES-256 и режима IGE на официальных векторах.
import { aesEncryptBlock, igeDecrypt, igeEncrypt } from '../src/aes.js';
import { fromHex, hex, equal, concat } from '../src/bytes.js';
import { sha1 } from '../src/hash.js';

let failures = 0;
function check(name, ok, extra = '') {
  console.log(`${ok ? 'OK  ' : 'FAIL'} ${name}${extra ? ' — ' + extra : ''}`);
  if (!ok) failures++;
}

// FIPS-197, приложение C.3 (AES-256)
{
  const key = fromHex('000102030405060708090a0b0c0d0e0f101112131415161718191a1b1c1d1e1f');
  const pt = fromHex('00112233445566778899aabbccddeeff');
  const ct = aesEncryptBlock(pt, key);
  check('AES-256 FIPS-197 C.3', hex(ct) === '8EA2B7CA516745BFEAFC49904B496089', hex(ct));
}

// Вектор из официального примера Telegram (samples-auth_key): расшифровка encrypted_answer.
{
  const tmpAesKey = fromHex('F011280887C7BB01DF0FC4E17830E0B91FBB8BE4B2267CB985AE25F33B527253');
  const tmpAesIv = fromHex('3212D579EE35452ED23E0D0C92841AA7D31B2E9BDEF2151E80D15860311C85DB');
  const encryptedAnswer = fromHex(
    '28A92FE20173B347A8BB324B5FAB2667C9A8BBCE6468D5B509A4CBDDC186240AC912CF7006AF8926DE606A2E74C0493C' +
      'AA57741E6C82451F54D3E068F5CCC49B4444124B9666FFB405AAB564A3D01E67F6E912867C8D20D9882707DC330B17B4' +
      'E0DD57CB53BFAAFA9EF5BE76AE6C1B9B6C51E2D6502A47C883095C46C81E3BE25F62427B585488BB3BF239213BF48EB8' +
      'FE34C9A026CC8413934043974DB03556633038392CECB51F94824E140B98637730A4BE79A8F9DAFA39BAE81E1095849E' +
      'A4C83467C92A3A17D997817C8A7AC61C3FF414DA37B7D66E949C0AEC858F048224210FCC61F11C3A910B431CCBD104CC' +
      'CC8DC6D29D4A5D133BE639A4C32BBFF153E63ACA3AC52F2E4709B8AE01844B142C1EE89D075D64F69A399FEB04E656FE' +
      '3675A6F8F412078F3D0B58DA15311C1A9F8E53B3CD6BB5572C294904B726D0BE337E2E21977DA26DD6E33270251C2CA2' +
      '9DFCC70227F0755F84CFDA9AC4B8DD5F84F1D1EB36BA45CDDC70444D8C213E4BD8F63B8AB95A2D0B4180DC91283DC063' +
      'ACFB92D6A4E407CDE7C8C69689F77A007441D4A6A8384B666502D9B77FC68B5B43CC607E60A146223E110FCB43BC3C94' +
      '2EF981930CDC4A1D310C0B64D5E55D308D863251AB90502C3E46CC599E886A927CDA963B9EB16CE62603B68529EE98F9' +
      'F5206419E03FB458EC4BD9454AA8F6BA777573CC54B328895B1DF25EAD9FB4CD5198EE022B2B81F388D281D5E5BC5801' +
      '07CA01A50665C32B552715F335FD76264FAD00DDD5AE45B94832AC79CE7C511D194BC42B70EFA850BB15C2012C5215CA' +
      'BFE97CE66B8D8734D0EE759A638AF013'
  );
  const answerWithHash = igeDecrypt(encryptedAnswer, tmpAesKey, tmpAesIv);
  const answerHead = hex(answerWithHash.slice(20, 24));
  check('IGE decrypt: конструктор server_DH_inner_data', answerHead === 'BA0D89B5', answerHead);

  // server_DH_inner_data: 4 + 16 + 16 + 4 + (4 + 256) + (4 + 256) + 4 = 564 байта
  const answer = answerWithHash.slice(20, 20 + 564);
  const digest = await sha1(answer);
  check('IGE decrypt: SHA1(answer) совпадает', equal(digest, answerWithHash.slice(0, 20)), hex(digest));

  const reencrypted = igeEncrypt(answerWithHash, tmpAesKey, tmpAesIv);
  check('IGE encrypt обратим', equal(reencrypted, encryptedAnswer));

  // dh_prime из примера должен совпасть с общеизвестным
  const dhPrimeHex = hex(answer.slice(44, 44 + 256));
  check('dh_prime из примера начинается с C71CAEB9', dhPrimeHex.startsWith('C71CAEB9'), dhPrimeHex.slice(0, 16));
}

console.log(failures === 0 ? '\nВсе проверки пройдены' : `\nОшибок: ${failures}`);
process.exit(failures === 0 ? 0 : 1);
