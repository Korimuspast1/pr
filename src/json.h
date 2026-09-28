/* Минимальный JSON-парсер (UTF-8) для SteamFinder */
#ifndef JSON_H
#define JSON_H

#include <stdlib.h>
#include <string.h>

enum { J_NULL = 0, J_BOOL, J_NUM, J_STR, J_ARR, J_OBJ };

typedef struct JV JV;
struct JV {
    int     t;
    double  num;
    int     b;
    char   *s;      /* для J_STR (UTF-8, без экранирования) */
    JV    **kids;
    char  **keys;   /* только для J_OBJ */
    int     n;
};

JV         *json_parse(const char *text);
void        json_free(JV *v);
JV         *jget(JV *o, const char *key);
JV         *jidx(JV *a, int i);
const char *jstr(JV *v);
double      jnum(JV *v);
int         jbool(JV *v);

#endif
