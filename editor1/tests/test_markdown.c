#include <assert.h>
#include <wchar.h>
#include "../markdown.h"
int main(void){
 wchar_t out[256];MarkdownSpan spans[16];size_t n;
 const wchar_t *s=L"Привет, **мир** и *текст* `код`!";
 markdown_inline(s,wcslen(s),out,spans,16,&n);
 assert(!wcscmp(out,L"Привет, мир и текст код!"));assert(n==3);
 assert(spans[0].kind==1 && spans[0].start==8 && spans[0].length==3);
 assert(spans[1].kind==2 && spans[2].kind==3);
 s=L"незакрытый **текст";markdown_inline(s,wcslen(s),out,spans,16,&n);assert(!wcscmp(out,s)&&n==0);
 s=L"**жирный**";markdown_inline(s,wcslen(s),out,spans,0,&n);assert(!wcscmp(out,s)&&n==0);
 return 0;
}
