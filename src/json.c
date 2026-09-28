#include "json.h"

typedef struct {
    const char *p;
    int         depth;
} JP;

static JV *parse_value(JP *st);

static void skip_ws(JP *st)
{
    while (*st->p == ' ' || *st->p == '\t' || *st->p == '\n' || *st->p == '\r')
        st->p++;
}

static JV *jnew(int t)
{
    JV *v = (JV *)calloc(1, sizeof(JV));
    if (v) v->t = t;
    return v;
}

static void put_utf8(char **out, unsigned cp)
{
    char *o = *out;
    if (cp < 0x80) {
        *o++ = (char)cp;
    } else if (cp < 0x800) {
        *o++ = (char)(0xC0 | (cp >> 6));
        *o++ = (char)(0x80 | (cp & 0x3F));
    } else if (cp < 0x10000) {
        *o++ = (char)(0xE0 | (cp >> 12));
        *o++ = (char)(0x80 | ((cp >> 6) & 0x3F));
        *o++ = (char)(0x80 | (cp & 0x3F));
    } else {
        *o++ = (char)(0xF0 | (cp >> 18));
        *o++ = (char)(0x80 | ((cp >> 12) & 0x3F));
        *o++ = (char)(0x80 | ((cp >> 6) & 0x3F));
        *o++ = (char)(0x80 | (cp & 0x3F));
    }
    *out = o;
}

static int hex4(const char *p, unsigned *out)
{
    unsigned v = 0;
    int i;
    for (i = 0; i < 4; i++) {
        char c = p[i];
        v <<= 4;
        if (c >= '0' && c <= '9')      v |= (unsigned)(c - '0');
        else if (c >= 'a' && c <= 'f') v |= (unsigned)(c - 'a' + 10);
        else if (c >= 'A' && c <= 'F') v |= (unsigned)(c - 'A' + 10);
        else return 0;
    }
    *out = v;
    return 1;
}

/* Разбирает строку в кавычках, возвращает malloc'нутую UTF-8 строку */
static char *parse_string(JP *st)
{
    const char *p = st->p;
    size_t      cap, len = 0;
    char       *buf, *o;

    if (*p != '"') return NULL;
    p++;
    cap = strlen(p) + 8;
    buf = (char *)malloc(cap);
    if (!buf) return NULL;
    o = buf;

    while (*p && *p != '"') {
        if (*p == '\\') {
            p++;
            switch (*p) {
            case 'n': *o++ = '\n'; p++; break;
            case 't': *o++ = '\t'; p++; break;
            case 'r': *o++ = '\r'; p++; break;
            case 'b': *o++ = '\b'; p++; break;
            case 'f': *o++ = '\f'; p++; break;
            case '/': *o++ = '/';  p++; break;
            case '\\': *o++ = '\\'; p++; break;
            case '"': *o++ = '"';  p++; break;
            case 'u': {
                unsigned cp = 0;
                p++;
                if (!hex4(p, &cp)) { free(buf); return NULL; }
                p += 4;
                if (cp >= 0xD800 && cp <= 0xDBFF && p[0] == '\\' && p[1] == 'u') {
                    unsigned lo = 0;
                    if (hex4(p + 2, &lo) && lo >= 0xDC00 && lo <= 0xDFFF) {
                        cp = 0x10000 + ((cp - 0xD800) << 10) + (lo - 0xDC00);
                        p += 6;
                    }
                }
                put_utf8(&o, cp);
                break;
            }
            default:
                if (*p) { *o++ = *p++; }
                break;
            }
        } else {
            *o++ = *p++;
        }
        len = (size_t)(o - buf);
        if (len + 8 >= cap) { /* защита, теоретически недостижимо */
            free(buf);
            return NULL;
        }
    }
    if (*p != '"') { free(buf); return NULL; }
    st->p = p + 1;
    *o = 0;
    return buf;
}

static int push_kid(JV *v, JV *kid, char *key)
{
    JV   **nk = (JV **)realloc(v->kids, sizeof(JV *) * (size_t)(v->n + 1));
    if (!nk) return 0;
    v->kids = nk;
    if (v->t == J_OBJ) {
        char **kk = (char **)realloc(v->keys, sizeof(char *) * (size_t)(v->n + 1));
        if (!kk) return 0;
        v->keys = kk;
        v->keys[v->n] = key;
    }
    v->kids[v->n] = kid;
    v->n++;
    return 1;
}

static JV *parse_value(JP *st)
{
    if (st->depth > 64) return NULL;
    skip_ws(st);

    if (*st->p == '{') {
        JV *v = jnew(J_OBJ);
        if (!v) return NULL;
        st->p++;
        st->depth++;
        skip_ws(st);
        if (*st->p == '}') { st->p++; st->depth--; return v; }
        for (;;) {
            char *key;
            JV   *kid;
            skip_ws(st);
            key = parse_string(st);
            if (!key) { json_free(v); return NULL; }
            skip_ws(st);
            if (*st->p != ':') { free(key); json_free(v); return NULL; }
            st->p++;
            kid = parse_value(st);
            if (!kid) { free(key); json_free(v); return NULL; }
            if (!push_kid(v, kid, key)) { free(key); json_free(kid); json_free(v); return NULL; }
            skip_ws(st);
            if (*st->p == ',') { st->p++; continue; }
            if (*st->p == '}') { st->p++; break; }
            json_free(v);
            return NULL;
        }
        st->depth--;
        return v;
    }

    if (*st->p == '[') {
        JV *v = jnew(J_ARR);
        if (!v) return NULL;
        st->p++;
        st->depth++;
        skip_ws(st);
        if (*st->p == ']') { st->p++; st->depth--; return v; }
        for (;;) {
            JV *kid = parse_value(st);
            if (!kid) { json_free(v); return NULL; }
            if (!push_kid(v, kid, NULL)) { json_free(kid); json_free(v); return NULL; }
            skip_ws(st);
            if (*st->p == ',') { st->p++; continue; }
            if (*st->p == ']') { st->p++; break; }
            json_free(v);
            return NULL;
        }
        st->depth--;
        return v;
    }

    if (*st->p == '"') {
        JV   *v;
        char *s = parse_string(st);
        if (!s) return NULL;
        v = jnew(J_STR);
        if (!v) { free(s); return NULL; }
        v->s = s;
        return v;
    }

    if (!strncmp(st->p, "true", 4)) {
        JV *v = jnew(J_BOOL);
        if (v) { v->b = 1; st->p += 4; }
        return v;
    }
    if (!strncmp(st->p, "false", 5)) {
        JV *v = jnew(J_BOOL);
        if (v) { v->b = 0; st->p += 5; }
        return v;
    }
    if (!strncmp(st->p, "null", 4)) {
        JV *v = jnew(J_NULL);
        if (v) st->p += 4;
        return v;
    }

    {
        char *end = NULL;
        double d = strtod(st->p, &end);
        if (end && end != st->p) {
            JV *v = jnew(J_NUM);
            if (!v) return NULL;
            v->num = d;
            st->p = end;
            return v;
        }
    }
    return NULL;
}

JV *json_parse(const char *text)
{
    JP st;
    if (!text) return NULL;
    st.p = text;
    st.depth = 0;
    /* пропускаем BOM */
    if ((unsigned char)st.p[0] == 0xEF && (unsigned char)st.p[1] == 0xBB &&
        (unsigned char)st.p[2] == 0xBF)
        st.p += 3;
    return parse_value(&st);
}

void json_free(JV *v)
{
    int i;
    if (!v) return;
    for (i = 0; i < v->n; i++) {
        json_free(v->kids[i]);
        if (v->keys) free(v->keys[i]);
    }
    free(v->kids);
    free(v->keys);
    free(v->s);
    free(v);
}

JV *jget(JV *o, const char *key)
{
    int i;
    if (!o || o->t != J_OBJ || !key) return NULL;
    for (i = 0; i < o->n; i++)
        if (o->keys[i] && !strcmp(o->keys[i], key))
            return o->kids[i];
    return NULL;
}

JV *jidx(JV *a, int i)
{
    if (!a || (a->t != J_ARR && a->t != J_OBJ)) return NULL;
    if (i < 0 || i >= a->n) return NULL;
    return a->kids[i];
}

const char *jstr(JV *v) { return (v && v->t == J_STR) ? v->s : NULL; }
double      jnum(JV *v) { return (v && v->t == J_NUM) ? v->num : 0.0; }
int         jbool(JV *v) { return (v && v->t == J_BOOL) ? v->b : 0; }
