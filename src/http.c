#include "http.h"
#include <winhttp.h>
#include <stdlib.h>
#include <string.h>

BOOL http_get(const WCHAR *url, BYTE **out, DWORD *outLen)
{
    URL_COMPONENTS uc;
    WCHAR          host[256];
    WCHAR          path[2048];
    HINTERNET      hSes = NULL, hCon = NULL, hReq = NULL;
    BYTE          *buf = NULL;
    DWORD          size = 0, cap = 0;
    BOOL           ok = FALSE;
    DWORD          flags = 0;

    if (!url || !out || !outLen) return FALSE;
    *out = NULL;
    *outLen = 0;

    ZeroMemory(&uc, sizeof(uc));
    uc.dwStructSize = sizeof(uc);
    uc.lpszHostName = host;
    uc.dwHostNameLength = (DWORD)(sizeof(host) / sizeof(WCHAR));
    uc.lpszUrlPath = path;
    uc.dwUrlPathLength = (DWORD)(sizeof(path) / sizeof(WCHAR));
    if (!WinHttpCrackUrl(url, 0, 0, &uc)) return FALSE;

    hSes = WinHttpOpen(L"SteamFinder/1.0 (Windows)",
                       WINHTTP_ACCESS_TYPE_DEFAULT_PROXY,
                       WINHTTP_NO_PROXY_NAME, WINHTTP_NO_PROXY_BYPASS, 0);
    if (!hSes) goto done;
    WinHttpSetTimeouts(hSes, 10000, 10000, 20000, 20000);

    hCon = WinHttpConnect(hSes, host, uc.nPort, 0);
    if (!hCon) goto done;

    if (uc.nScheme == INTERNET_SCHEME_HTTPS) flags |= WINHTTP_FLAG_SECURE;
    hReq = WinHttpOpenRequest(hCon, L"GET", path, NULL, WINHTTP_NO_REFERER,
                              WINHTTP_DEFAULT_ACCEPT_TYPES, flags);
    if (!hReq) goto done;

    WinHttpAddRequestHeaders(hReq,
        L"Accept-Language: ru-RU,ru;q=0.9,en;q=0.8\r\nAccept: */*\r\n",
        (DWORD)-1L, WINHTTP_ADDREQ_FLAG_ADD);

    if (!WinHttpSendRequest(hReq, WINHTTP_NO_ADDITIONAL_HEADERS, 0,
                            WINHTTP_NO_REQUEST_DATA, 0, 0, 0)) goto done;
    if (!WinHttpReceiveResponse(hReq, NULL)) goto done;

    {
        DWORD status = 0, len = sizeof(status);
        WinHttpQueryHeaders(hReq, WINHTTP_QUERY_STATUS_CODE | WINHTTP_QUERY_FLAG_NUMBER,
                            WINHTTP_HEADER_NAME_BY_INDEX, &status, &len, WINHTTP_NO_HEADER_INDEX);
        if (status && (status < 200 || status >= 300)) goto done;
    }

    cap = 64 * 1024;
    buf = (BYTE *)malloc(cap);
    if (!buf) goto done;

    for (;;) {
        DWORD avail = 0, got = 0;
        if (!WinHttpQueryDataAvailable(hReq, &avail)) goto done;
        if (avail == 0) break;
        if (size + avail + 1 > cap) {
            BYTE *nb;
            while (size + avail + 1 > cap) {
                if (cap > 64 * 1024 * 1024) goto done; /* защита от гигантских ответов */
                cap *= 2;
            }
            nb = (BYTE *)realloc(buf, cap);
            if (!nb) goto done;
            buf = nb;
        }
        if (!WinHttpReadData(hReq, buf + size, avail, &got)) goto done;
        if (got == 0) break;
        size += got;
    }

    buf[size] = 0;
    *out = buf;
    *outLen = size;
    buf = NULL;
    ok = TRUE;

done:
    if (buf) free(buf);
    if (hReq) WinHttpCloseHandle(hReq);
    if (hCon) WinHttpCloseHandle(hCon);
    if (hSes) WinHttpCloseHandle(hSes);
    return ok;
}
