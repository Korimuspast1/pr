// Константы протокола: номера конструкторов из MTProto TL-схемы и dh_prime.

import { fromHex } from './bytes.js';

export const CTOR = {
  req_pq_multi: 0xbe7e8ef1,
  resPQ: 0x05162463,
  p_q_inner_data_dc: 0xa9f55f95,
  req_DH_params: 0xd712e4be,
  server_DH_params_ok: 0xd0e8075c,
  server_DH_params_fail: 0x79cb045d,
  server_DH_inner_data: 0xb5890dba,
  client_DH_inner_data: 0x6643b654,
  set_client_DH_params: 0xf5045f1f,
  dh_gen_ok: 0x3bcbf734,
  dh_gen_retry: 0x46dc1fb9,
  dh_gen_fail: 0xa69dae02,
  vector: 0x1cb5c415,
};

export const SCHEMA = {
  req_pq_multi: 'req_pq_multi#be7e8ef1 nonce:int128 = ResPQ;',
  resPQ: 'resPQ#05162463 nonce:int128 server_nonce:int128 pq:bytes server_public_key_fingerprints:Vector<long> = ResPQ;',
  p_q_inner_data_dc:
    'p_q_inner_data_dc#a9f55f95 pq:bytes p:bytes q:bytes nonce:int128 server_nonce:int128 new_nonce:int256 dc:int = P_Q_inner_data;',
  req_DH_params:
    'req_DH_params#d712e4be nonce:int128 server_nonce:int128 p:bytes q:bytes public_key_fingerprint:long encrypted_data:bytes = Server_DH_Params;',
  server_DH_params_ok: 'server_DH_params_ok#d0e8075c nonce:int128 server_nonce:int128 encrypted_answer:bytes = Server_DH_Params;',
  server_DH_inner_data:
    'server_DH_inner_data#b5890dba nonce:int128 server_nonce:int128 g:int dh_prime:bytes g_a:bytes server_time:int = Server_DH_inner_data;',
  client_DH_inner_data: 'client_DH_inner_data#6643b654 nonce:int128 server_nonce:int128 retry_id:long g_b:bytes = Client_DH_Inner_Data;',
  set_client_DH_params: 'set_client_DH_params#f5045f1f nonce:int128 server_nonce:int128 encrypted_data:bytes = Set_client_DH_params_answer;',
  dh_gen_ok: 'dh_gen_ok#3bcbf734 nonce:int128 server_nonce:int128 new_nonce_hash1:int128 = Set_client_DH_params_answer;',
};

/**
 * Текущее значение dh_prime серверов Telegram (big endian), приведённое в документации.
 * Это безопасное простое: и p, и (p-1)/2 просты, 2^2047 < p < 2^2048.
 */
export const DH_PRIME_HEX =
  'C71CAEB9C6B1C9048E6C522F70F13F73980D40238E3E21C14934D037563D930F' +
  '48198A0AA7C14058229493D22530F4DBFA336F6E0AC925139543AED44CCE7C37' +
  '20FD51F69458705AC68CD4FE6B6B13ABDC9746512969328454F18FAF8C595F64' +
  '2477FE96BB2A941D5BCD1D4AC8CC49880708FA9B378E3C4F3A9060BEE67CF9A4' +
  'A4A695811051907E162753B56B0F6B410DBA74D8A84B2A14B3144E0EF1284754' +
  'FD17ED950D5965B4B9DD46582DB1178D169C6BC465B0D6FF9CA3928FEF5B9AE4' +
  'E418FC15E83EBEA0F87FA9FF5EED70050DED2849F47BF959D956850CE929851F' +
  '0D8115F635B105EE2E4E15D04B2454BF6F4FADF034B10403119CD8E3B92FCC5B';

export const DH_PRIME = fromHex(DH_PRIME_HEX);

/** g выбирается сервером из {2,3,4,5,6,7}; для указанного выше p подходит g = 3 (p mod 3 = 2). */
export const DH_G = 3;
