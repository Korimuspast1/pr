/*
 * SteamFinder — поиск игр в Steam в стиле клиента Steam.
 * Нативное Win32-приложение (C), без внешних зависимостей.
 * Данные: публичный Steam Store API (storesearch + appdetails).
 */
#define WIN32_LEAN_AND_MEAN
#ifndef UNICODE
#define UNICODE
#endif
#ifndef _UNICODE
#define _UNICODE
#endif
#define COBJMACROS

#include <windows.h>
#include <objbase.h>
#include <objidl.h>
#include <shellapi.h>
#include <windowsx.h>
#include <stdlib.h>
#include <string.h>

#include "json.h"
#include "http.h"

/* ------------------------------------------------------------------ */
/* GDI+ (плоский C-API, объявляем вручную)                             */
/* ------------------------------------------------------------------ */
typedef int GpStatus;

typedef struct {
    UINT32 GdiplusVersion;
    void  *DebugEventCallback;
    BOOL   SuppressBackgroundThread;
    BOOL   SuppressExternalCodecs;
} GdiplusStartupInputC;

GpStatus WINAPI GdiplusStartup(ULONG_PTR *token, const GdiplusStartupInputC *input, void *output);
void     WINAPI GdiplusShutdown(ULONG_PTR token);
GpStatus WINAPI GdipLoadImageFromStream(IStream *stream, void **image);
GpStatus WINAPI GdipDisposeImage(void *image);
GpStatus WINAPI GdipCreateFromHDC(HDC hdc, void **graphics);
GpStatus WINAPI GdipDeleteGraphics(void *graphics);
GpStatus WINAPI GdipDrawImageRectI(void *graphics, void *image, INT x, INT y, INT w, INT h);
GpStatus WINAPI GdipSetInterpolationMode(void *graphics, INT mode);
GpStatus WINAPI GdipGetImageWidth(void *image, UINT *width);
GpStatus WINAPI GdipGetImageHeight(void *image, UINT *height);

/* ------------------------------------------------------------------ */
/* Цвета / размеры                                                     */
/* ------------------------------------------------------------------ */
#define CLR_BG      RGB(0x1b, 0x28, 0x38)
#define CLR_TOP     RGB(0x17, 0x1a, 0x21)
#define CLR_PANEL   RGB(0x16, 0x20, 0x2d)
#define CLR_CARD    RGB(0x1f, 0x2d, 0x3f)
#define CLR_SEL     RGB(0x2a, 0x47, 0x5e)
#define CLR_TXT     RGB(0xc7, 0xd5, 0xe0)
#define CLR_DIM     RGB(0x8f, 0x98, 0xa0)
#define CLR_WHITE   RGB(0xff, 0xff, 0xff)
#define CLR_GREEN   RGB(0xa4, 0xd0, 0x07)
#define CLR_BLUE    RGB(0x66, 0xc0, 0xf4)
#define CLR_FIELD   RGB(0x31, 0x6e, 0x99)
#define CLR_EDITBG  RGB(0x0f, 0x17, 0x20)

#define TOPH        62
#define LISTW       420
#define ITEMH       76
#define MAXI        25

#define WM_APP_SEARCH   (WM_APP + 1)
#define WM_APP_THUMBS   (WM_APP + 2)
#define WM_APP_DETAILS  (WM_APP + 3)
#define WM_APP_STATUS   (WM_APP + 4)

#define ID_EDIT     1001
#define ID_BTN      1002
#define ID_LIST     1003

/* ------------------------------------------------------------------ */
/* Картинки                                                            */
/* ------------------------------------------------------------------ */
typedef struct {
    void    *g;   /* GpImage*  */
    IStream *s;
} Img;

typedef struct {
    int   appid;
    WCHAR name[200];
    WCHAR price[64];
    WCHAR kind[40];
    Img  *thumb;
} Item;

typedef struct {
    int  gen;
    int  n;
    Item items[MAXI];
} SearchRes;

typedef struct {
    int  gen;
    int  n;
    Img *imgs[MAXI];
} ThumbRes;

typedef struct {
    int   gen;
    int   appid;
    WCHAR name[220];
    WCHAR kind[48];
    WCHAR dev[256];
    WCHAR pub[256];
    WCHAR date[96];
    WCHAR price[96];
    WCHAR oldprice[64];
    WCHAR discount[32];
    WCHAR meta[32];
    WCHAR genres[300];
    WCHAR platforms[160];
    WCHAR descr[2400];
    Img  *header;
} Details;

/* ------------------------------------------------------------------ */
/* Глобальное состояние (только UI-поток)                              */
/* ------------------------------------------------------------------ */
static HINSTANCE g_inst;
static HWND      g_hwnd, g_edit, g_btn, g_list;
static HFONT     g_fUI, g_fBold, g_fTitle, g_fSmall, g_fH2;
static HBRUSH    g_brBg, g_brTop, g_brPanel, g_brEdit;
static ULONG_PTR g_gdipTok;

static SearchRes *g_res;
static Details   *g_det;
static volatile LONG g_gen  = 0;   /* поколение поиска  */
static volatile LONG g_dgen = 0;   /* поколение деталей */
static WCHAR      g_status[256];
static BOOL       g_busy, g_dbusy;
static RECT       g_storeBtn;
static BOOL       g_storeHot;
static BOOL       g_btnHot;

/* ------------------------------------------------------------------ */
/* Утилиты для строк                                                   */
/* ------------------------------------------------------------------ */
static void u8w(const char *s, WCHAR *out, int cap)
{
    out[0] = 0;
    if (!s || !*s) return;
    MultiByteToWideChar(CP_UTF8, 0, s, -1, out, cap);
    out[cap - 1] = 0;
}

static void wcopy(WCHAR *dst, int cap, const WCHAR *src)
{
    int i = 0;
    if (cap <= 0) return;
    if (src) {
        for (; src[i] && i < cap - 1; i++) dst[i] = src[i];
    }
    dst[i] = 0;
}

static void wcat(WCHAR *dst, int cap, const WCHAR *src)
{
    int len = lstrlenW(dst);
    if (len >= cap - 1 || !src) return;
    wcopy(dst + len, cap - len, src);
}

/* Убирает html-теги и раскрывает частые сущности */
static void strip_html(WCHAR *s)
{
    WCHAR *r = s, *w = s;
    while (*r) {
        if (*r == L'<') {
            while (*r && *r != L'>') r++;
            if (*r) r++;
            if (w > s && *(w - 1) != L' ') *w++ = L' ';
            continue;
        }
        if (*r == L'&') {
            if (!wcsncmp(r, L"&amp;", 5))       { *w++ = L'&';  r += 5; continue; }
            if (!wcsncmp(r, L"&quot;", 6))      { *w++ = L'"';  r += 6; continue; }
            if (!wcsncmp(r, L"&#39;", 5))       { *w++ = L'\''; r += 5; continue; }
            if (!wcsncmp(r, L"&lt;", 4))        { *w++ = L'<';  r += 4; continue; }
            if (!wcsncmp(r, L"&gt;", 4))        { *w++ = L'>';  r += 4; continue; }
            if (!wcsncmp(r, L"&nbsp;", 6))      { *w++ = L' ';  r += 6; continue; }
        }
        if (*r == L'\r') { r++; continue; }
        *w++ = *r++;
    }
    *w = 0;
}

/* URL-кодирование строки поиска (результат — ASCII в WCHAR-буфере) */
static void url_encode_w(const WCHAR *src, WCHAR *out, int cap)
{
    char  utf8[512];
    int   n, i, o = 0;
    static const WCHAR hex[] = L"0123456789ABCDEF";

    out[0] = 0;
    n = WideCharToMultiByte(CP_UTF8, 0, src, -1, utf8, (int)sizeof(utf8) - 1, NULL, NULL);
    if (n <= 0) return;
    utf8[n] = 0;

    for (i = 0; utf8[i] && o < cap - 4; i++) {
        unsigned char c = (unsigned char)utf8[i];
        if ((c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z') ||
            (c >= '0' && c <= '9') || c == '-' || c == '_' || c == '.' || c == '~') {
            out[o++] = (WCHAR)c;
        } else if (c == ' ') {
            out[o++] = L'+';
        } else {
            out[o++] = L'%';
            out[o++] = hex[(c >> 4) & 0xF];
            out[o++] = hex[c & 0xF];
        }
    }
    out[o] = 0;
}

/* ------------------------------------------------------------------ */
/* Картинки: загрузка из памяти / по URL                               */
/* ------------------------------------------------------------------ */
static void img_free(Img *im)
{
    if (!im) return;
    if (im->g) GdipDisposeImage(im->g);
    if (im->s) IStream_Release(im->s);
    free(im);
}

static Img *img_from_mem(const BYTE *data, DWORD len)
{
    HGLOBAL  hg;
    void    *p;
    IStream *st = NULL;
    void    *gp = NULL;
    Img     *im;

    if (!data || len == 0) return NULL;
    hg = GlobalAlloc(GMEM_MOVEABLE, len);
    if (!hg) return NULL;
    p = GlobalLock(hg);
    if (!p) { GlobalFree(hg); return NULL; }
    memcpy(p, data, len);
    GlobalUnlock(hg);

    if (CreateStreamOnHGlobal(hg, TRUE, &st) != S_OK) { GlobalFree(hg); return NULL; }
    if (GdipLoadImageFromStream(st, &gp) != 0 || !gp) {
        IStream_Release(st);
        return NULL;
    }
    im = (Img *)calloc(1, sizeof(Img));
    if (!im) { GdipDisposeImage(gp); IStream_Release(st); return NULL; }
    im->g = gp;
    im->s = st;   /* поток должен жить, пока жива картинка */
    return im;
}

static Img *img_from_url(const WCHAR *url)
{
    BYTE *data = NULL;
    DWORD len = 0;
    Img  *im;
    if (!url || !url[0]) return NULL;
    if (!http_get(url, &data, &len)) return NULL;
    im = img_from_mem(data, len);
    free(data);
    return im;
}

static void draw_img(HDC hdc, Img *im, int x, int y, int w, int h)
{
    void *g = NULL;
    if (!im || !im->g) return;
    if (GdipCreateFromHDC(hdc, &g) != 0 || !g) return;
    GdipSetInterpolationMode(g, 7 /* HighQualityBicubic */);
    GdipDrawImageRectI(g, im->g, x, y, w, h);
    GdipDeleteGraphics(g);
}

/* ------------------------------------------------------------------ */
/* Работа с данными Steam                                              */
/* ------------------------------------------------------------------ */
static void post_status(const WCHAR *text)
{
    WCHAR *copy = (WCHAR *)malloc((size_t)(lstrlenW(text) + 1) * sizeof(WCHAR));
    if (!copy) return;
    lstrcpyW(copy, text);
    PostMessageW(g_hwnd, WM_APP_STATUS, 0, (LPARAM)copy);
}

static void fmt_price_cents(double cents, const char *cur, WCHAR *out, int cap)
{
    int   whole = (int)(cents / 100.0);
    int   frac  = (int)(cents - (double)whole * 100.0);
    WCHAR curw[16];
    u8w(cur ? cur : "", curw, 16);
    if (!lstrcmpiW(curw, L"RUB") || !lstrcmpiW(curw, L"rub"))
        wsprintfW(out, L"%d руб.", whole + (frac >= 50 ? 1 : 0));
    else
        wsprintfW(out, L"%d.%02d %s", whole, frac, curw);
    out[cap - 1] = 0;
}

typedef struct {
    int   gen;
    WCHAR term[200];
} SearchReq;

static DWORD WINAPI SearchThread(LPVOID param)
{
    SearchReq *req = (SearchReq *)param;
    WCHAR      enc[700], url[1100];
    BYTE      *data = NULL;
    DWORD      len = 0;
    JV        *root = NULL, *items;
    SearchRes *res = NULL;
    ThumbRes  *th = NULL;
    WCHAR    (*urls)[512] = NULL;
    int        i;

    url_encode_w(req->term, enc, 700);
    wsprintfW(url, L"https://store.steampowered.com/api/storesearch/?term=%s&l=russian&cc=RU", enc);

    if (!http_get(url, &data, &len)) {
        post_status(L"Не удалось связаться со Steam. Проверьте интернет.");
        goto done;
    }
    root = json_parse((const char *)data);
    free(data);
    data = NULL;
    if (!root) {
        post_status(L"Steam вернул неожиданный ответ.");
        goto done;
    }

    items = jget(root, "items");
    res = (SearchRes *)calloc(1, sizeof(SearchRes));
    urls = (WCHAR (*)[512])calloc(MAXI, sizeof(*urls));
    if (!res || !urls) goto done;
    res->gen = req->gen;

    if (items && items->t == J_ARR) {
        for (i = 0; i < items->n && res->n < MAXI; i++) {
            JV   *it = jidx(items, i);
            JV   *price;
            Item *dst = &res->items[res->n];
            const char *tiny;

            if (!it || it->t != J_OBJ) continue;
            dst->appid = (int)jnum(jget(it, "id"));
            u8w(jstr(jget(it, "name")), dst->name, 200);
            if (!dst->name[0]) continue;

            u8w(jstr(jget(it, "type")), dst->kind, 40);
            if (!lstrcmpiW(dst->kind, L"app"))  wcopy(dst->kind, 40, L"Игра");
            if (!lstrcmpiW(dst->kind, L"dlc"))  wcopy(dst->kind, 40, L"DLC");
            if (!lstrcmpiW(dst->kind, L"bundle")) wcopy(dst->kind, 40, L"Комплект");

            price = jget(it, "price");
            if (price && price->t == J_OBJ) {
                double final_ = jnum(jget(price, "final"));
                if (final_ <= 0.0) wcopy(dst->price, 64, L"Бесплатно");
                else fmt_price_cents(final_, jstr(jget(price, "currency")), dst->price, 64);
            } else {
                wcopy(dst->price, 64, L"Цена не указана");
            }

            tiny = jstr(jget(it, "tiny_image"));
            if (tiny) u8w(tiny, urls[res->n], 512);
            res->n++;
        }
    }

    if (InterlockedCompareExchange(&g_gen, 0, 0) != req->gen) goto done;
    PostMessageW(g_hwnd, WM_APP_SEARCH, 0, (LPARAM)res);
    res = NULL;

    /* Догружаем миниатюры */
    th = (ThumbRes *)calloc(1, sizeof(ThumbRes));
    if (!th) goto done;
    th->gen = req->gen;
    th->n = 0;
    {
        int count = 0;
        JV *tmp = items;
        (void)tmp;
        for (i = 0; i < MAXI; i++) {
            if (!urls[i][0]) continue;
            count = i + 1;
        }
        th->n = count;
        for (i = 0; i < count; i++) {
            if (InterlockedCompareExchange(&g_gen, 0, 0) != req->gen) goto done;
            th->imgs[i] = img_from_url(urls[i]);
        }
    }
    if (InterlockedCompareExchange(&g_gen, 0, 0) != req->gen) goto done;
    PostMessageW(g_hwnd, WM_APP_THUMBS, 0, (LPARAM)th);
    th = NULL;

done:
    if (th) {
        for (i = 0; i < MAXI; i++) img_free(th->imgs[i]);
        free(th);
    }
    if (res) free(res);
    free(urls);
    json_free(root);
    free(req);
    return 0;
}

typedef struct {
    int gen;
    int appid;
} DetReq;

static void join_strings(JV *arr, WCHAR *out, int cap, const char *field)
{
    int i;
    out[0] = 0;
    if (!arr || arr->t != J_ARR) return;
    for (i = 0; i < arr->n && i < 8; i++) {
        WCHAR tmp[200];
        JV   *el = jidx(arr, i);
        const char *s = field ? jstr(jget(el, field)) : jstr(el);
        if (!s) continue;
        u8w(s, tmp, 200);
        if (out[0]) wcat(out, cap, L", ");
        wcat(out, cap, tmp);
    }
}

static DWORD WINAPI DetailsThread(LPVOID param)
{
    DetReq  *req = (DetReq *)param;
    WCHAR    url[512];
    BYTE    *data = NULL;
    DWORD    len = 0;
    JV      *root = NULL, *entry, *d;
    Details *det = NULL;
    WCHAR    hdr[512];

    wsprintfW(url, L"https://store.steampowered.com/api/appdetails?appids=%d&l=russian&cc=ru",
              req->appid);

    if (!http_get(url, &data, &len)) {
        post_status(L"Не удалось загрузить страницу игры.");
        goto done;
    }
    root = json_parse((const char *)data);
    free(data);
    data = NULL;
    if (!root) { post_status(L"Некорректный ответ Steam."); goto done; }

    entry = jidx(root, 0);
    if (!entry || !jbool(jget(entry, "success"))) {
        post_status(L"Steam не отдал данные по этой позиции.");
        goto done;
    }
    d = jget(entry, "data");
    if (!d) { post_status(L"Нет данных по этой позиции."); goto done; }

    det = (Details *)calloc(1, sizeof(Details));
    if (!det) goto done;
    det->gen = req->gen;
    det->appid = req->appid;

    u8w(jstr(jget(d, "name")), det->name, 220);
    u8w(jstr(jget(d, "type")), det->kind, 48);
    if (!lstrcmpiW(det->kind, L"game")) wcopy(det->kind, 48, L"Игра");
    else if (!lstrcmpiW(det->kind, L"dlc")) wcopy(det->kind, 48, L"Дополнение (DLC)");

    join_strings(jget(d, "developers"), det->dev, 256, NULL);
    join_strings(jget(d, "publishers"), det->pub, 256, NULL);
    join_strings(jget(d, "genres"), det->genres, 300, "description");

    {
        JV *rd = jget(d, "release_date");
        if (rd) {
            u8w(jstr(jget(rd, "date")), det->date, 96);
            if (jbool(jget(rd, "coming_soon")) && det->date[0])
                wcat(det->date, 96, L" (скоро)");
        }
    }
    {
        JV *pf = jget(d, "platforms");
        det->platforms[0] = 0;
        if (pf) {
            if (jbool(jget(pf, "windows"))) wcat(det->platforms, 160, L"Windows");
            if (jbool(jget(pf, "mac"))) {
                if (det->platforms[0]) wcat(det->platforms, 160, L", ");
                wcat(det->platforms, 160, L"macOS");
            }
            if (jbool(jget(pf, "linux"))) {
                if (det->platforms[0]) wcat(det->platforms, 160, L", ");
                wcat(det->platforms, 160, L"Linux");
            }
        }
    }
    {
        JV *mc = jget(d, "metacritic");
        if (mc) {
            int sc = (int)jnum(jget(mc, "score"));
            if (sc > 0) wsprintfW(det->meta, L"%d / 100", sc);
        }
    }
    {
        JV *po = jget(d, "price_overview");
        if (jbool(jget(d, "is_free"))) {
            wcopy(det->price, 96, L"Бесплатно");
        } else if (po) {
            const char *f = jstr(jget(po, "final_formatted"));
            const char *o = jstr(jget(po, "initial_formatted"));
            int disc = (int)jnum(jget(po, "discount_percent"));
            if (f) u8w(f, det->price, 96);
            else fmt_price_cents(jnum(jget(po, "final")), jstr(jget(po, "currency")), det->price, 96);
            if (disc > 0) {
                wsprintfW(det->discount, L"-%d%%", disc);
                if (o) u8w(o, det->oldprice, 64);
            }
        } else {
            wcopy(det->price, 96, L"Цена не указана");
        }
    }
    {
        const char *sd = jstr(jget(d, "short_description"));
        if (!sd) sd = jstr(jget(d, "about_the_game"));
        u8w(sd, det->descr, 2400);
        strip_html(det->descr);
    }

    u8w(jstr(jget(d, "header_image")), hdr, 512);
    json_free(root);
    root = NULL;

    if (InterlockedCompareExchange(&g_dgen, 0, 0) != req->gen) { free(det); det = NULL; goto done; }
    if (hdr[0]) det->header = img_from_url(hdr);
    if (InterlockedCompareExchange(&g_dgen, 0, 0) != req->gen) {
        img_free(det->header);
        free(det);
        det = NULL;
        goto done;
    }
    PostMessageW(g_hwnd, WM_APP_DETAILS, 0, (LPARAM)det);
    det = NULL;

done:
    json_free(root);
    if (det) { img_free(det->header); free(det); }
    free(req);
    return 0;
}

/* ------------------------------------------------------------------ */
/* Запуск поиска / загрузки деталей                                    */
/* ------------------------------------------------------------------ */
static void free_results(void)
{
    int i;
    if (!g_res) return;
    for (i = 0; i < g_res->n; i++) img_free(g_res->items[i].thumb);
    free(g_res);
    g_res = NULL;
}

static void free_details(void)
{
    if (!g_det) return;
    img_free(g_det->header);
    free(g_det);
    g_det = NULL;
}

static void start_details(int appid)
{
    DetReq *req;
    HANDLE  th;
    free_details();
    g_dbusy = TRUE;
    req = (DetReq *)calloc(1, sizeof(DetReq));
    if (!req) return;
    req->gen = (int)InterlockedIncrement(&g_dgen);
    req->appid = appid;
    InvalidateRect(g_hwnd, NULL, FALSE);
    th = CreateThread(NULL, 0, DetailsThread, req, 0, NULL);
    if (th) CloseHandle(th);
    else { free(req); g_dbusy = FALSE; }
}

static void start_search(void)
{
    WCHAR      term[200];
    SearchReq *req;
    HANDLE     th;

    GetWindowTextW(g_edit, term, 200);
    /* обрезаем пробелы по краям */
    {
        int a = 0, b = lstrlenW(term);
        while (term[a] == L' ') a++;
        while (b > a && term[b - 1] == L' ') b--;
        term[b] = 0;
        if (a) MoveMemory(term, term + a, (size_t)(b - a + 1) * sizeof(WCHAR));
    }
    if (!term[0]) {
        wcopy(g_status, 256, L"Введите название игры в поле поиска.");
        InvalidateRect(g_hwnd, NULL, FALSE);
        return;
    }

    InterlockedIncrement(&g_dgen);   /* отменяем текущую загрузку деталей */
    free_details();
    free_results();
    SendMessageW(g_list, LB_RESETCONTENT, 0, 0);
    g_busy = TRUE;
    g_dbusy = FALSE;
    wcopy(g_status, 256, L"Ищем в Steam…");
    InvalidateRect(g_hwnd, NULL, FALSE);
    UpdateWindow(g_hwnd);

    req = (SearchReq *)calloc(1, sizeof(SearchReq));
    if (!req) return;
    req->gen = (int)InterlockedIncrement(&g_gen);
    wcopy(req->term, 200, term);

    th = CreateThread(NULL, 0, SearchThread, req, 0, NULL);
    if (th) CloseHandle(th);
    else { free(req); g_busy = FALSE; }
}

/* ------------------------------------------------------------------ */
/* Отрисовка                                                           */
/* ------------------------------------------------------------------ */
static void fill_rect(HDC hdc, int x, int y, int w, int h, COLORREF c)
{
    RECT   r;
    HBRUSH b = CreateSolidBrush(c);
    r.left = x; r.top = y; r.right = x + w; r.bottom = y + h;
    FillRect(hdc, &r, b);
    DeleteObject(b);
}

static void text_out(HDC hdc, HFONT f, COLORREF c, int x, int y, int w, const WCHAR *s, UINT flags)
{
    RECT r;
    HGDIOBJ old;
    if (!s || !s[0]) return;
    r.left = x; r.top = y; r.right = x + w; r.bottom = y + 4000;
    old = SelectObject(hdc, f);
    SetTextColor(hdc, c);
    DrawTextW(hdc, s, -1, &r, flags);
    SelectObject(hdc, old);
}

static int text_height(HDC hdc, HFONT f, const WCHAR *s, int w)
{
    RECT r;
    HGDIOBJ old;
    if (!s || !s[0]) return 0;
    r.left = 0; r.top = 0; r.right = w; r.bottom = 0;
    old = SelectObject(hdc, f);
    DrawTextW(hdc, s, -1, &r, DT_WORDBREAK | DT_CALCRECT);
    SelectObject(hdc, old);
    return r.bottom;
}

/* Одна строка «Ключ: значение» */
static int info_row(HDC hdc, int x, int y, int w, const WCHAR *key, const WCHAR *val, COLORREF vc)
{
    int kw = 150, hgt;
    if (!val || !val[0]) return y;
    text_out(hdc, g_fSmall, CLR_DIM, x, y, kw - 10, key, DT_LEFT | DT_SINGLELINE | DT_END_ELLIPSIS);
    hgt = text_height(hdc, g_fUI, val, w - kw);
    text_out(hdc, g_fUI, vc, x + kw, y - 1, w - kw, val, DT_LEFT | DT_WORDBREAK);
    return y + (hgt > 20 ? hgt : 20) + 8;
}

static void paint_details(HDC hdc, RECT rc)
{
    int x = rc.left + 24;
    int y = rc.top + 22;
    int w = rc.right - rc.left - 48;

    SetBkMode(hdc, TRANSPARENT);
    fill_rect(hdc, rc.left, rc.top, rc.right - rc.left, rc.bottom - rc.top, CLR_PANEL);
    SetRect(&g_storeBtn, 0, 0, 0, 0);

    if (g_dbusy && !g_det) {
        text_out(hdc, g_fH2, CLR_DIM, x, y + 40, w, L"Загружаем информацию…",
                 DT_LEFT | DT_SINGLELINE);
        return;
    }
    if (!g_det) {
        text_out(hdc, g_fH2, CLR_TXT, x, y + 30, w,
                 L"Поиск игр в Steam", DT_LEFT | DT_SINGLELINE);
        text_out(hdc, g_fUI, CLR_DIM, x, y + 70, w,
                 L"Введите название игры сверху и нажмите Enter.\n"
                 L"Слева появится список из магазина Steam — выберите позицию,\n"
                 L"чтобы увидеть обложку, цену, жанры, дату выхода и описание.",
                 DT_LEFT | DT_WORDBREAK);
        if (g_status[0])
            text_out(hdc, g_fUI, CLR_BLUE, x, rc.bottom - 60, w, g_status,
                     DT_LEFT | DT_WORDBREAK);
        return;
    }

    /* Обложка 460x215 */
    {
        int iw = w;
        int ih = iw * 215 / 460;
        if (ih > (rc.bottom - rc.top) / 2) {
            ih = (rc.bottom - rc.top) / 2;
            iw = ih * 460 / 215;
        }
        fill_rect(hdc, x, y, iw, ih, CLR_CARD);
        if (g_det->header) draw_img(hdc, g_det->header, x, y, iw, ih);
        y += ih + 18;
    }

    text_out(hdc, g_fTitle, CLR_WHITE, x, y, w, g_det->name, DT_LEFT | DT_WORDBREAK);
    y += text_height(hdc, g_fTitle, g_det->name, w) + 6;

    /* Цена */
    {
        WCHAR line[200];
        line[0] = 0;
        if (g_det->discount[0]) {
            wcat(line, 200, g_det->discount);
            wcat(line, 200, L"   ");
            if (g_det->oldprice[0]) { wcat(line, 200, g_det->oldprice); wcat(line, 200, L"  →  "); }
        }
        wcat(line, 200, g_det->price);
        text_out(hdc, g_fH2, CLR_GREEN, x, y, w, line, DT_LEFT | DT_SINGLELINE | DT_END_ELLIPSIS);
        y += 34;
    }

    y = info_row(hdc, x, y, w, L"Тип", g_det->kind, CLR_TXT);
    y = info_row(hdc, x, y, w, L"Разработчик", g_det->dev, CLR_TXT);
    y = info_row(hdc, x, y, w, L"Издатель", g_det->pub, CLR_TXT);
    y = info_row(hdc, x, y, w, L"Дата выхода", g_det->date, CLR_TXT);
    y = info_row(hdc, x, y, w, L"Жанры", g_det->genres, CLR_TXT);
    y = info_row(hdc, x, y, w, L"Платформы", g_det->platforms, CLR_TXT);
    y = info_row(hdc, x, y, w, L"Metacritic", g_det->meta, CLR_GREEN);
    {
        WCHAR idbuf[32];
        wsprintfW(idbuf, L"%d", g_det->appid);
        y = info_row(hdc, x, y, w, L"AppID", idbuf, CLR_DIM);
    }

    /* Описание */
    if (g_det->descr[0]) {
        RECT    r;
        HGDIOBJ old;
        int     bottom = rc.bottom - 78;
        y += 6;
        fill_rect(hdc, x, y, w, 1, CLR_SEL);
        y += 14;
        r.left = x; r.top = y; r.right = x + w; r.bottom = bottom;
        if (r.bottom > r.top + 20) {
            old = SelectObject(hdc, g_fUI);
            SetTextColor(hdc, CLR_TXT);
            DrawTextW(hdc, g_det->descr, -1, &r, DT_LEFT | DT_WORDBREAK | DT_END_ELLIPSIS);
            SelectObject(hdc, old);
        }
    }

    /* Кнопка «Открыть в Steam» */
    {
        int bw = 260, bh = 40;
        int bx = x, by = rc.bottom - bh - 20;
        SetRect(&g_storeBtn, bx, by, bx + bw, by + bh);
        fill_rect(hdc, bx, by, bw, bh, g_storeHot ? CLR_BLUE : CLR_SEL);
        text_out(hdc, g_fBold, g_storeHot ? RGB(0x10, 0x20, 0x30) : CLR_WHITE,
                 bx, by + 10, bw, L"Открыть страницу в Steam",
                 DT_CENTER | DT_SINGLELINE);
    }
}

static void paint_window(HWND hwnd, HDC target)
{
    RECT  rc, det;
    HDC   hdc;
    HBITMAP bmp, oldbmp;
    int   W, H;

    GetClientRect(hwnd, &rc);
    W = rc.right;
    H = rc.bottom;

    hdc = CreateCompatibleDC(target);
    bmp = CreateCompatibleBitmap(target, W, H);
    oldbmp = (HBITMAP)SelectObject(hdc, bmp);

    SetBkMode(hdc, TRANSPARENT);
    fill_rect(hdc, 0, 0, W, H, CLR_BG);
    fill_rect(hdc, 0, 0, W, TOPH, CLR_TOP);
    fill_rect(hdc, 0, TOPH - 1, W, 1, RGB(0x0b, 0x0e, 0x13));

    /* Заголовок-логотип справа в топбаре */
    text_out(hdc, g_fBold, CLR_DIM, W - 260, 20, 244, L"STEAM  FINDER",
             DT_RIGHT | DT_SINGLELINE);

    /* Подпись счётчика над списком */
    {
        WCHAR info[128];
        if (g_busy)      wcopy(info, 128, L"Идёт поиск…");
        else if (g_res)  wsprintfW(info, L"Найдено: %d", g_res->n);
        else             wcopy(info, 128, L"Результаты поиска");
        text_out(hdc, g_fSmall, CLR_DIM, 16, TOPH + 8, LISTW, info, DT_LEFT | DT_SINGLELINE);
    }

    det.left = 16 + LISTW + 16;
    det.top = TOPH + 28;
    det.right = W - 16;
    det.bottom = H - 16;
    if (det.right > det.left + 80)
        paint_details(hdc, det);

    BitBlt(target, 0, 0, W, H, hdc, 0, 0, SRCCOPY);
    SelectObject(hdc, oldbmp);
    DeleteObject(bmp);
    DeleteDC(hdc);
}

static void draw_list_item(LPDRAWITEMSTRUCT d)
{
    Item *it;
    RECT  r = d->rcItem;
    int   idx = (int)d->itemID;
    HDC   hdc = d->hDC;
    int   tw = 120, th = 45;

    if (idx < 0 || !g_res || idx >= g_res->n) return;
    it = &g_res->items[idx];

    SetBkMode(hdc, TRANSPARENT);
    fill_rect(hdc, r.left, r.top, r.right - r.left, r.bottom - r.top,
              (d->itemState & ODS_SELECTED) ? CLR_SEL : CLR_PANEL);
    fill_rect(hdc, r.left, r.bottom - 1, r.right - r.left, 1, RGB(0x10, 0x18, 0x22));
    if (d->itemState & ODS_SELECTED)
        fill_rect(hdc, r.left, r.top, 3, r.bottom - r.top, CLR_BLUE);

    fill_rect(hdc, r.left + 12, r.top + 15, tw, th, CLR_CARD);
    if (it->thumb) draw_img(hdc, it->thumb, r.left + 12, r.top + 15, tw, th);

    text_out(hdc, g_fBold, CLR_WHITE, r.left + 12 + tw + 12, r.top + 14,
             r.right - r.left - tw - 40, it->name, DT_LEFT | DT_SINGLELINE | DT_END_ELLIPSIS);
    text_out(hdc, g_fSmall, CLR_DIM, r.left + 12 + tw + 12, r.top + 38,
             120, it->kind, DT_LEFT | DT_SINGLELINE);
    text_out(hdc, g_fSmall,
             (!lstrcmpiW(it->price, L"Бесплатно")) ? CLR_GREEN : CLR_TXT,
             r.left + 12 + tw + 12, r.top + 54,
             r.right - r.left - tw - 40, it->price, DT_LEFT | DT_SINGLELINE | DT_END_ELLIPSIS);
}

static void draw_search_button(LPDRAWITEMSTRUCT d)
{
    RECT r = d->rcItem;
    BOOL pressed = (d->itemState & ODS_SELECTED) != 0;
    COLORREF bg = pressed ? RGB(0x1a, 0x5c, 0x87) : (g_btnHot ? CLR_BLUE : CLR_FIELD);
    SetBkMode(d->hDC, TRANSPARENT);
    fill_rect(d->hDC, r.left, r.top, r.right - r.left, r.bottom - r.top, bg);
    text_out(d->hDC, g_fBold, g_btnHot ? RGB(0x10, 0x20, 0x30) : CLR_WHITE,
             r.left, r.top + 7, r.right - r.left, L"Найти", DT_CENTER | DT_SINGLELINE);
}

/* ------------------------------------------------------------------ */
/* Оконная процедура                                                   */
/* ------------------------------------------------------------------ */
static void layout(HWND hwnd)
{
    RECT rc;
    GetClientRect(hwnd, &rc);
    MoveWindow(g_edit, 18, 15, 520, 32, TRUE);
    MoveWindow(g_btn, 548, 15, 120, 32, TRUE);
    MoveWindow(g_list, 16, TOPH + 28, LISTW, rc.bottom - TOPH - 44, TRUE);
}

static LRESULT CALLBACK WndProc(HWND hwnd, UINT msg, WPARAM wp, LPARAM lp)
{
    switch (msg) {

    case WM_CREATE:
        g_edit = CreateWindowExW(0, L"EDIT", L"",
            WS_CHILD | WS_VISIBLE | ES_AUTOHSCROLL | ES_LEFT,
            0, 0, 10, 10, hwnd, (HMENU)ID_EDIT, g_inst, NULL);
        g_btn = CreateWindowExW(0, L"BUTTON", L"Найти",
            WS_CHILD | WS_VISIBLE | BS_OWNERDRAW,
            0, 0, 10, 10, hwnd, (HMENU)ID_BTN, g_inst, NULL);
        g_list = CreateWindowExW(0, L"LISTBOX", NULL,
            WS_CHILD | WS_VISIBLE | WS_VSCROLL | LBS_OWNERDRAWFIXED |
            LBS_NOTIFY | LBS_NOINTEGRALHEIGHT,
            0, 0, 10, 10, hwnd, (HMENU)ID_LIST, g_inst, NULL);
        SendMessageW(g_edit, WM_SETFONT, (WPARAM)g_fUI, TRUE);
        SendMessageW(g_list, LB_SETITEMHEIGHT, 0, ITEMH);
        SendMessageW(g_edit, EM_SETLIMITTEXT, 180, 0);
        /* подсказка в поле ввода (работает на Vista+ с comctl32 v6) */
        SendMessageW(g_edit, 0x1501 /*EM_SETCUEBANNER*/, TRUE,
                     (LPARAM)L"Например: Portal 2, Cyberpunk, Dota…");
        layout(hwnd);
        SetFocus(g_edit);
        return 0;

    case WM_SIZE:
        layout(hwnd);
        InvalidateRect(hwnd, NULL, FALSE);
        return 0;

    case WM_GETMINMAXINFO: {
        MINMAXINFO *mm = (MINMAXINFO *)lp;
        mm->ptMinTrackSize.x = 940;
        mm->ptMinTrackSize.y = 620;
        return 0;
    }

    case WM_ERASEBKGND:
        return 1;

    case WM_PAINT: {
        PAINTSTRUCT ps;
        HDC hdc = BeginPaint(hwnd, &ps);
        paint_window(hwnd, hdc);
        EndPaint(hwnd, &ps);
        return 0;
    }

    case WM_CTLCOLOREDIT:
        SetTextColor((HDC)wp, CLR_WHITE);
        SetBkColor((HDC)wp, CLR_EDITBG);
        return (LRESULT)g_brEdit;

    case WM_CTLCOLORLISTBOX:
        return (LRESULT)g_brPanel;

    case WM_DRAWITEM: {
        LPDRAWITEMSTRUCT d = (LPDRAWITEMSTRUCT)lp;
        if (d->CtlType == ODT_LISTBOX) draw_list_item(d);
        else if (d->CtlType == ODT_BUTTON) draw_search_button(d);
        return TRUE;
    }

    case WM_COMMAND:
        if (LOWORD(wp) == ID_BTN && HIWORD(wp) == BN_CLICKED) {
            start_search();
            return 0;
        }
        if (LOWORD(wp) == ID_LIST && (HIWORD(wp) == LBN_SELCHANGE)) {
            int sel = (int)SendMessageW(g_list, LB_GETCURSEL, 0, 0);
            if (sel >= 0 && g_res && sel < g_res->n)
                start_details(g_res->items[sel].appid);
            return 0;
        }
        if (LOWORD(wp) == ID_LIST && HIWORD(wp) == LBN_DBLCLK) {
            int sel = (int)SendMessageW(g_list, LB_GETCURSEL, 0, 0);
            if (sel >= 0 && g_res && sel < g_res->n) {
                WCHAR u[160];
                wsprintfW(u, L"https://store.steampowered.com/app/%d/", g_res->items[sel].appid);
                ShellExecuteW(hwnd, L"open", u, NULL, NULL, SW_SHOWNORMAL);
            }
            return 0;
        }
        break;

    case WM_MOUSEMOVE: {
        POINT pt;
        BOOL  hot;
        TRACKMOUSEEVENT tme;
        pt.x = GET_X_LPARAM(lp);
        pt.y = GET_Y_LPARAM(lp);
        hot = (g_det != NULL) && PtInRect(&g_storeBtn, pt);
        if (hot != g_storeHot) {
            g_storeHot = hot;
            SetCursor(LoadCursorW(NULL, hot ? IDC_HAND : IDC_ARROW));
            InvalidateRect(hwnd, &g_storeBtn, FALSE);
        }
        tme.cbSize = sizeof(tme);
        tme.dwFlags = TME_LEAVE;
        tme.hwndTrack = hwnd;
        tme.dwHoverTime = 0;
        TrackMouseEvent(&tme);
        return 0;
    }

    case WM_SETCURSOR:
        if (LOWORD(lp) == HTCLIENT && g_storeHot) {
            SetCursor(LoadCursorW(NULL, IDC_HAND));
            return TRUE;
        }
        break;

    case WM_MOUSELEAVE:
        if (g_storeHot) {
            g_storeHot = FALSE;
            InvalidateRect(hwnd, NULL, FALSE);
        }
        return 0;

    case WM_LBUTTONDOWN: {
        POINT pt;
        pt.x = GET_X_LPARAM(lp);
        pt.y = GET_Y_LPARAM(lp);
        if (g_det && PtInRect(&g_storeBtn, pt)) {
            WCHAR u[160];
            wsprintfW(u, L"https://store.steampowered.com/app/%d/", g_det->appid);
            ShellExecuteW(hwnd, L"open", u, NULL, NULL, SW_SHOWNORMAL);
        }
        return 0;
    }

    case WM_APP_SEARCH: {
        SearchRes *res = (SearchRes *)lp;
        int i;
        if (!res) return 0;
        if (res->gen != InterlockedCompareExchange(&g_gen, 0, 0)) { free(res); return 0; }
        free_results();
        g_res = res;
        g_busy = FALSE;
        SendMessageW(g_list, LB_RESETCONTENT, 0, 0);
        for (i = 0; i < g_res->n; i++)
            SendMessageW(g_list, LB_ADDSTRING, 0, (LPARAM)i);
        if (g_res->n > 0) {
            wcopy(g_status, 256, L"");
            SendMessageW(g_list, LB_SETCURSEL, 0, 0);
            start_details(g_res->items[0].appid);
        } else {
            wcopy(g_status, 256, L"Ничего не найдено. Попробуйте другое название.");
        }
        InvalidateRect(hwnd, NULL, FALSE);
        return 0;
    }

    case WM_APP_THUMBS: {
        ThumbRes *th = (ThumbRes *)lp;
        int i;
        if (!th) return 0;
        if (!g_res || th->gen != InterlockedCompareExchange(&g_gen, 0, 0)) {
            for (i = 0; i < MAXI; i++) img_free(th->imgs[i]);
            free(th);
            return 0;
        }
        for (i = 0; i < th->n && i < g_res->n; i++) {
            img_free(g_res->items[i].thumb);
            g_res->items[i].thumb = th->imgs[i];
            th->imgs[i] = NULL;
        }
        for (i = 0; i < MAXI; i++) img_free(th->imgs[i]);
        free(th);
        InvalidateRect(g_list, NULL, TRUE);
        return 0;
    }

    case WM_APP_DETAILS: {
        Details *det = (Details *)lp;
        if (!det) return 0;
        if (det->gen != InterlockedCompareExchange(&g_dgen, 0, 0)) {
            img_free(det->header);
            free(det);
            return 0;
        }
        free_details();
        g_det = det;
        g_dbusy = FALSE;
        wcopy(g_status, 256, L"");
        InvalidateRect(hwnd, NULL, FALSE);
        return 0;
    }

    case WM_APP_STATUS: {
        WCHAR *text = (WCHAR *)lp;
        if (text) {
            wcopy(g_status, 256, text);
            free(text);
        }
        g_busy = FALSE;
        g_dbusy = FALSE;
        InvalidateRect(hwnd, NULL, FALSE);
        return 0;
    }

    case WM_DESTROY:
        PostQuitMessage(0);
        return 0;
    }
    return DefWindowProcW(hwnd, msg, wp, lp);
}

/* ------------------------------------------------------------------ */
/* Точка входа                                                         */
/* ------------------------------------------------------------------ */
static HFONT mkfont(int size, int weight)
{
    return CreateFontW(-size, 0, 0, 0, weight, FALSE, FALSE, FALSE,
                       DEFAULT_CHARSET, OUT_DEFAULT_PRECIS, CLIP_DEFAULT_PRECIS,
                       CLEARTYPE_QUALITY, DEFAULT_PITCH | FF_DONTCARE, L"Segoe UI");
}

int WINAPI wWinMain(HINSTANCE hInst, HINSTANCE hPrev, PWSTR cmd, int show)
{
    WNDCLASSEXW wc;
    MSG         msg;
    GdiplusStartupInputC gsi;

    (void)hPrev; (void)cmd;
    g_inst = hInst ? hInst : GetModuleHandleW(NULL);

    {
        HMODULE u = GetModuleHandleW(L"user32.dll");
        typedef BOOL (WINAPI *PFN)(void);
        PFN p = u ? (PFN)(void *)GetProcAddress(u, "SetProcessDPIAware") : NULL;
        if (p) p();
    }

    CoInitializeEx(NULL, COINIT_APARTMENTTHREADED);

    ZeroMemory(&gsi, sizeof(gsi));
    gsi.GdiplusVersion = 1;
    if (GdiplusStartup(&g_gdipTok, &gsi, NULL) != 0) {
        MessageBoxW(NULL, L"Не удалось инициализировать GDI+.", L"SteamFinder", MB_ICONERROR);
        return 1;
    }

    g_fUI    = mkfont(15, FW_NORMAL);
    g_fBold  = mkfont(15, FW_SEMIBOLD);
    g_fSmall = mkfont(13, FW_NORMAL);
    g_fH2    = mkfont(19, FW_SEMIBOLD);
    g_fTitle = mkfont(26, FW_BOLD);

    g_brBg    = CreateSolidBrush(CLR_BG);
    g_brTop   = CreateSolidBrush(CLR_TOP);
    g_brPanel = CreateSolidBrush(CLR_PANEL);
    g_brEdit  = CreateSolidBrush(CLR_EDITBG);

    ZeroMemory(&wc, sizeof(wc));
    wc.cbSize = sizeof(wc);
    wc.style = CS_HREDRAW | CS_VREDRAW;
    wc.lpfnWndProc = WndProc;
    wc.hInstance = g_inst;
    wc.hCursor = LoadCursorW(NULL, IDC_ARROW);
    wc.hbrBackground = g_brBg;
    wc.lpszClassName = L"SteamFinderWnd";
    wc.hIcon = LoadIconW(NULL, IDI_APPLICATION);
    wc.hIconSm = LoadIconW(NULL, IDI_APPLICATION);
    if (!RegisterClassExW(&wc)) return 1;

    g_hwnd = CreateWindowExW(0, L"SteamFinderWnd",
        L"SteamFinder — поиск игр в Steam",
        WS_OVERLAPPEDWINDOW,
        CW_USEDEFAULT, CW_USEDEFAULT, 1220, 800,
        NULL, NULL, g_inst, NULL);
    if (!g_hwnd) return 1;

    ShowWindow(g_hwnd, show ? show : SW_SHOWNORMAL);
    UpdateWindow(g_hwnd);

    while (GetMessageW(&msg, NULL, 0, 0) > 0) {
        if (msg.message == WM_KEYDOWN && msg.wParam == VK_RETURN &&
            (msg.hwnd == g_edit || msg.hwnd == g_hwnd)) {
            start_search();
            continue;
        }
        if (msg.message == WM_KEYDOWN && msg.wParam == VK_ESCAPE) {
            SetWindowTextW(g_edit, L"");
            SetFocus(g_edit);
            continue;
        }
        TranslateMessage(&msg);
        DispatchMessageW(&msg);
    }

    free_results();
    free_details();
    if (g_gdipTok) GdiplusShutdown(g_gdipTok);
    CoUninitialize();
    return (int)msg.wParam;
}
