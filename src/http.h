/* HTTPS-клиент поверх WinHTTP */
#ifndef HTTP_H
#define HTTP_H

#include <windows.h>

/* Скачивает URL целиком. *out — блок памяти (free()), всегда с завершающим нулём. */
BOOL http_get(const WCHAR *url, BYTE **out, DWORD *outLen);

#endif
