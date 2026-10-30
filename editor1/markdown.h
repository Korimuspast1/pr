#ifndef EDITOR1_MARKDOWN_H
#define EDITOR1_MARKDOWN_H
#include <stddef.h>
#include <wchar.h>
/* A deliberately small inline renderer: unmatched markup stays visible. */
typedef struct { size_t start, length; int kind; } MarkdownSpan;
static size_t markdown_inline(const wchar_t *source, size_t len, wchar_t *out, MarkdownSpan *spans, size_t capacity, size_t *count) {
 size_t i=0, n=0; *count=0;
 while(i<len){
  wchar_t marker=source[i];size_t width=0;
  if(marker==L'*' && i+1<len && source[i+1]==L'*')width=2;
  else if(marker==L'*' || marker==L'`')width=1;
  if(width && *count<capacity){
   size_t end=i+width;
   while(end+width<=len){
    if(source[end]==marker && (width==1 || source[end+1]==marker))break;
    end++;
   }
   if(end+width<=len && end>i+width){
    size_t length=end-i-width;
    for(size_t j=0;j<length;j++)out[n+j]=source[i+width+j];
    spans[*count]=(MarkdownSpan){n,length,marker==L'`'?3:(width==2?1:2)};(*count)++;
    n+=length;i=end+width;continue;
   }
  }
  out[n++]=source[i++];
 }
 out[n]=0;return n;
}
#endif
