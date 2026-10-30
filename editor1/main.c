#define _UNICODE
#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#include <commdlg.h>
#include <shlobj.h>
#include <richedit.h>
#include <wchar.h>
#include <stdio.h>
#include <stdlib.h>
#include <wctype.h>
#include <stdarg.h>
#include <shellapi.h>
#include <math.h>
#include "icons.h"
#include "animation.h"
#include "markdown.h"
#include "preferences.h"

#define ID_LIST 101
#define ID_SEARCH 102
#define ID_EDITOR 103
#define ID_NEW 104
#define ID_OPEN 105
#define ID_SAVE 106
#define ID_DELETE 107
#define ID_RENAME 108
#define ID_EDIT 109
#define ID_READ 110
#define ID_PROMPT 111
#define ID_OK 112
#define ID_CANCEL 113
#define ID_DEV_LOG 114
#define ID_SETTINGS 115
#define ID_DUPLICATE 116
#define ID_FIND_NOTES 117
#define ID_FMT_BASE 200
#define MAX_NOTE (4 * 1024 * 1024)

static HWND window, list, search, editor, formatBar, buttons[8], promptInput;
static HFONT ui, uiSmall, uiBold, bodyFont, titleFont, brandFont;
static WNDPROC oldButtonProc, oldSearchProc, oldPromptButtonProc, oldListProc;
static HWND promptButtons[2], settingsButtons[13];
static WNDPROC oldSettingsButtonProc;static int settingsDone,settingsApplied,settingsHot[13];
static int promptHover[2];
static int hovered[8], hoverAmount[8], hoveredList=-1, hoveredMode=-1;
static Animation hoverAnim[8], settingsHover[13], promptAnim[2], formatAnim, modeHoverAnim, modeAnim={1.0f,1.0f,1.0f,0,0,0}, listHoverAnim, selectionAnim={1.0f,1.0f,1.0f,0,0,0}, searchAnim;
static int devMode; static HANDLE devFile=INVALID_HANDLE_VALUE;
static HWND devWindow, devEdit; static wchar_t devBuffer[32768];
static HBRUSH bgBrush, sideBrush, inputBrush;
static Preferences prefs={14,0,1,700}, pendingPrefs;
static wchar_t directory[MAX_PATH], current[MAX_PATH], *text;
static size_t wordCount, charCount;static int formatHover=-1,formatFocus=0;
static int dirty, loading, reading = 1, promptDone, promptAccepted;
static wchar_t promptValue[180];
static const COLORREF BG = RGB(24,27,38), SIDE = RGB(19,23,34), BORDER = RGB(45,52,68), TEXT = RGB(239,243,252), MUTED = RGB(148,160,184), ACCENT = RGB(145,191,255), TEAL = RGB(133,205,195);
static const wchar_t *welcome = L"# Мой дом\r\n\r\n## Пространство для мыслей\r\n\r\nДобро пожаловать в Editor 1. Здесь можно собирать идеи, вести заметки и сосредоточиться на том, что действительно важно.\r\n\r\n## Заметки\r\n\r\n- Создайте новую заметку в боковой панели\r\n- Переключитесь в режим редактирования, чтобы писать\r\n- Откройте или экспортируйте Markdown и текстовые файлы\r\n\r\n## Идеи\r\n\r\nВаше пространство — ваши правила. Начните с одной мысли.\r\n\r\n> Всё большое начинается с маленькой заметки.\r\n";

static void devLog(const wchar_t *format, ...) {
 if(!devMode)return;
 wchar_t message[1024],line[1200];va_list args;va_start(args,format);_vsnwprintf(message,1023,format,args);va_end(args);message[1023]=0;
 SYSTEMTIME t;GetLocalTime(&t);_snwprintf(line,1199,L"[%02u:%02u:%02u.%03u] %ls\r\n",t.wHour,t.wMinute,t.wSecond,t.wMilliseconds,message);line[1199]=0;
 size_t used=wcslen(devBuffer), added=wcslen(line);
 if(used+added>=32767){size_t drop=used/2;wmemmove(devBuffer,devBuffer+drop,used-drop+1);used-=drop;}
 wmemcpy(devBuffer+used,line,added+1);
 if(devEdit){SendMessageW(devEdit,EM_SETREADONLY,FALSE,0);int len=GetWindowTextLengthW(devEdit);if(len>30000){SendMessageW(devEdit,EM_SETSEL,0,15000);SendMessageW(devEdit,EM_REPLACESEL,FALSE,(LPARAM)L"");}SendMessageW(devEdit,EM_SETSEL,(WPARAM)-1,(LPARAM)-1);SendMessageW(devEdit,EM_REPLACESEL,FALSE,(LPARAM)line);SendMessageW(devEdit,EM_SETREADONLY,TRUE,0);SendMessageW(devEdit,EM_SCROLLCARET,0,0);}
 DWORD ignored;if(devFile!=INVALID_HANDLE_VALUE){char utf8[4800];int n=WideCharToMultiByte(CP_UTF8,0,line,-1,utf8,sizeof(utf8),NULL,NULL);if(n>1)WriteFile(devFile,utf8,(DWORD)n-1,&ignored,NULL);}
}
static void initDevMode(void){
 int argc=0;LPWSTR *argv=CommandLineToArgvW(GetCommandLineW(),&argc);
 if(argv){for(int i=1;i<argc;i++)if(!_wcsicmp(argv[i],L"-dev")||!_wcsicmp(argv[i],L"--dev"))devMode=1;LocalFree(argv);}
 if(!devMode)return;
 wchar_t appdata[MAX_PATH],folder[MAX_PATH],path[MAX_PATH];
 if(SUCCEEDED(SHGetFolderPathW(NULL,CSIDL_APPDATA|CSIDL_FLAG_CREATE,NULL,SHGFP_TYPE_CURRENT,appdata))){
  _snwprintf(folder,MAX_PATH,L"%ls\\Editor1",appdata);folder[MAX_PATH-1]=0;CreateDirectoryW(folder,NULL);
  if(wcslen(folder)+20<MAX_PATH){_snwprintf(path,MAX_PATH,L"%ls\\debug.log",folder);path[MAX_PATH-1]=0;
   devFile=CreateFileW(path,FILE_APPEND_DATA,FILE_SHARE_READ|FILE_SHARE_WRITE,NULL,OPEN_ALWAYS,FILE_ATTRIBUTE_NORMAL,NULL);
  }
 }
 DWORD fileError=devFile==INVALID_HANDLE_VALUE?GetLastError():0;
 devLog(L"Editor 1 v1.6 started in developer mode; PID=%lu",GetCurrentProcessId());
 devLog(L"Logs contain operational events, errors, and document sizes; document text is not logged.");
 if(devFile==INVALID_HANDLE_VALUE)devLog(L"WARNING: could not open debug.log, Windows error=%lu",fileError);
}
static void startAnimation(Animation *a,float target,unsigned duration){
 BOOL enabled=TRUE;SystemParametersInfoW(SPI_GETCLIENTAREAANIMATION,0,&enabled,0);
 animation_start(a,target,GetTickCount64(),duration,enabled);
 if(window){if(!enabled){for(int i=0;i<8;i++){if(i==5)continue;hoverAmount[i]=(int)(hoverAnim[i].value*100);if(buttons[i])InvalidateRect(buttons[i],NULL,FALSE);}if(buttons[5])InvalidateRect(buttons[5],NULL,FALSE);if(formatBar)InvalidateRect(formatBar,NULL,FALSE);for(int i=0;i<2;i++)if(promptButtons[i])InvalidateRect(promptButtons[i],NULL,FALSE);for(int i=0;i<13;i++)if(settingsButtons[i])InvalidateRect(settingsButtons[i],NULL,FALSE);if(list)InvalidateRect(list,NULL,FALSE);InvalidateRect(window,NULL,FALSE);}SetTimer(window,2,16,NULL);}
}
static void error(const wchar_t *message) { devLog(L"ERROR: %ls",message); MessageBoxW(window, message, L"Editor 1", MB_ICONERROR); }
static wchar_t *duptext(const wchar_t *s) { size_t n = (wcslen(s)+1)*sizeof(wchar_t); wchar_t *p=malloc(n); if(p) memcpy(p,s,n); return p; }
static void pathFor(const wchar_t *name, wchar_t *out, size_t cap) { _snwprintf(out,cap,L"%ls\\%ls.md",directory,name); out[cap-1]=0; }
static int validName(const wchar_t *name) {
 if (!name[0] || wcslen(name)>120 || name[0]==L' ' || name[wcslen(name)-1]==L' ' || name[wcslen(name)-1]==L'.') return 0;
 if (wcspbrk(name,L"\\/:*?\"<>|")) return 0;
 for(const wchar_t *p=name; *p; p++) if(*p<32) return 0;
 wchar_t base[128]; wcsncpy(base,name,127); base[127]=0; wchar_t *dot=wcschr(base,L'.'); if(dot)*dot=0;
 if(!_wcsicmp(base,L"CON")||!_wcsicmp(base,L"PRN")||!_wcsicmp(base,L"AUX")||!_wcsicmp(base,L"NUL")) return 0;
 if ((wcslen(base)==4) && (!_wcsnicmp(base,L"COM",3)||!_wcsnicmp(base,L"LPT",3)) && base[3]>=L'1'&&base[3]<=L'9') return 0;
 return 1;
}
static int writeUtf8(const wchar_t *path, const wchar_t *content) {
 int size=WideCharToMultiByte(CP_UTF8,0,content,-1,NULL,0,NULL,NULL); if(!size || size-1>MAX_NOTE){devLog(L"Write rejected: invalid encoding or size limit");return 0;}
 char *bytes=malloc(size); if(!bytes)return 0; WideCharToMultiByte(CP_UTF8,0,content,-1,bytes,size,NULL,NULL);
 wchar_t temp[MAX_PATH]; if(wcslen(path)+5>=MAX_PATH){free(bytes);return 0;} swprintf(temp,MAX_PATH,L"%ls.tmp",path);
 HANDLE h=CreateFileW(temp,GENERIC_WRITE,0,NULL,CREATE_ALWAYS,FILE_ATTRIBUTE_NORMAL,NULL); if(h==INVALID_HANDLE_VALUE){devLog(L"Cannot create temporary file: Windows error=%lu",GetLastError());free(bytes);return 0;}
 DWORD written=0, failure=0;int ok=WriteFile(h,bytes,(DWORD)(size-1),&written,NULL);if(!ok)failure=GetLastError();else if(written!=(DWORD)(size-1)){ok=0;failure=ERROR_WRITE_FAULT;}
 if(ok&&!FlushFileBuffers(h)){ok=0;failure=GetLastError();}CloseHandle(h);free(bytes);
 if(ok&&!MoveFileExW(temp,path,MOVEFILE_REPLACE_EXISTING|MOVEFILE_WRITE_THROUGH)){ok=0;failure=GetLastError();}if(!ok){devLog(L"Write or atomic rename failed: Windows error=%lu",failure);DeleteFileW(temp);}return ok;
}
static void settingsPath(wchar_t *path){_snwprintf(path,MAX_PATH,L"%ls\\settings.ini",directory);path[MAX_PATH-1]=0;}
static void loadPreferences(void){
 wchar_t path[MAX_PATH];settingsPath(path);
 int size=(int)GetPrivateProfileIntW(L"editor",L"font_size",14,path),face=(int)GetPrivateProfileIntW(L"editor",L"font_face",0,path),spacing=(int)GetPrivateProfileIntW(L"editor",L"spacing",1,path),delay=(int)GetPrivateProfileIntW(L"editor",L"save_delay",700,path);
 prefs=sanitize_preferences(size,face,spacing,delay);
 devLog(L"Preferences loaded");
}
static int savePreferences(Preferences value){
 wchar_t path[MAX_PATH],buffer[256];settingsPath(path);
 _snwprintf(buffer,256,L"[editor]\r\nfont_size=%d\r\nfont_face=%d\r\nspacing=%d\r\nsave_delay=%d\r\n",value.fontSize,value.fontFace,value.spacing,value.saveDelay);buffer[255]=0;
 if(!writeUtf8(path,buffer))return 0;devLog(L"Preferences saved");return 1;
}
static wchar_t *readUtf8(const wchar_t *path) {
 HANDLE h=CreateFileW(path,GENERIC_READ,FILE_SHARE_READ,NULL,OPEN_EXISTING,0,NULL); if(h==INVALID_HANDLE_VALUE){devLog(L"Cannot open file: Windows error=%lu",GetLastError());return NULL;}
 LARGE_INTEGER size; if(!GetFileSizeEx(h,&size)||size.QuadPart>MAX_NOTE||size.QuadPart<0){CloseHandle(h);return NULL;}
 DWORD got=0; char *buf=malloc((size_t)size.QuadPart+1); if(!buf){CloseHandle(h);return NULL;}
 if(!ReadFile(h,buf,(DWORD)size.QuadPart,&got,NULL)){free(buf);CloseHandle(h);return NULL;} CloseHandle(h); buf[got]=0;
 char *start=buf; if(got>=3&&(unsigned char)buf[0]==0xef&&(unsigned char)buf[1]==0xbb&&(unsigned char)buf[2]==0xbf)start+=3;
 int chars=MultiByteToWideChar(CP_UTF8,MB_ERR_INVALID_CHARS,start,-1,NULL,0); if(!chars){free(buf);return NULL;}
 wchar_t *result=malloc(chars*sizeof(wchar_t)); if(result)MultiByteToWideChar(CP_UTF8,0,start,-1,result,chars); free(buf); return result;
}
static int capture(void) { if(reading)return 1; int n=GetWindowTextLengthW(editor); wchar_t *next=malloc(((size_t)n+1)*sizeof(wchar_t)); if(!next){devLog(L"Cannot capture document: out of memory");return 0;} GetWindowTextW(editor,next,n+1); free(text);text=next;return 1; }
static int saveNote(int showError) { if(!current[0] || !dirty)return 1; ULONGLONG started=GetTickCount64(); if(!capture()){if(showError)error(L"Недостаточно памяти для сохранения заметки.");return 0;} wchar_t path[MAX_PATH]; pathFor(current,path,MAX_PATH); if(!writeUtf8(path,text?text:L"")){ if(showError)error(L"Не удалось сохранить заметку. Проверьте доступ к папке документов.");return 0;} dirty=0; devLog(L"Autosave OK: %lu characters, %llu ms",(unsigned long)(text?wcslen(text):0),GetTickCount64()-started); InvalidateRect(window,NULL,FALSE); return 1; }
static void refreshList(void) {
 wchar_t filter[200]; GetWindowTextW(search,filter,200); CharLowerBuffW(filter,(DWORD)wcslen(filter));
 hoveredList=-1;listHoverAnim.value=listHoverAnim.target=0;listHoverAnim.active=0;SendMessageW(list,LB_RESETCONTENT,0,0); wchar_t pattern[MAX_PATH]; swprintf(pattern,MAX_PATH,L"%ls\\*.md",directory); WIN32_FIND_DATAW data; HANDLE h=FindFirstFileW(pattern,&data);
 if(h!=INVALID_HANDLE_VALUE){do {if(data.dwFileAttributes&FILE_ATTRIBUTE_DIRECTORY)continue; size_t n=wcslen(data.cFileName);if(n<3)continue;data.cFileName[n-3]=0;wchar_t lower[MAX_PATH];wcsncpy(lower,data.cFileName,MAX_PATH-1);lower[MAX_PATH-1]=0;CharLowerBuffW(lower,(DWORD)wcslen(lower));if(*filter&&!wcsstr(lower,filter)){wchar_t notePath[MAX_PATH];pathFor(data.cFileName,notePath,MAX_PATH);wchar_t *body=readUtf8(notePath);int match=0;if(body){CharLowerBuffW(body,(DWORD)wcslen(body));match=wcsstr(body,filter)!=NULL;free(body);}if(!match)continue;}int i=(int)SendMessageW(list,LB_ADDSTRING,0,(LPARAM)data.cFileName);if(!_wcsicmp(current,data.cFileName))SendMessageW(list,LB_SETCURSEL,i,0);} while(FindNextFileW(h,&data)); FindClose(h);}
 InvalidateRect(window,NULL,FALSE);
}
static void styleRange(LONG start,LONG end,COLORREF color,int size,int bold) {
 CHARRANGE range={start,end}; SendMessageW(editor,EM_EXSETSEL,0,(LPARAM)&range); CHARFORMAT2W f={0};f.cbSize=sizeof(f);f.dwMask=CFM_COLOR|CFM_SIZE|CFM_BOLD|CFM_ITALIC;f.crTextColor=color;f.yHeight=size*20;f.dwEffects=bold?CFE_BOLD:0;SendMessageW(editor,EM_SETCHARFORMAT,SCF_SELECTION,(LPARAM)&f);
}
static void measureText(void){
 const wchar_t *source=text?text:L"";wchar_t *temporary=NULL;
 if(!reading&&current[0]){int n=GetWindowTextLengthW(editor);temporary=malloc(((size_t)n+1)*sizeof(wchar_t));if(temporary){GetWindowTextW(editor,temporary,n+1);source=temporary;}}
 wordCount=charCount=0;int inWord=0;
 for(const wchar_t *p=source;*p;p++){if(*p!=L'\r')charCount++;int space=iswspace(*p)!=0;if(!space&&!inWord)wordCount++;inWord=!space;}
 free(temporary);if(window)InvalidateRect(window,NULL,FALSE);
}
static void applyDocumentStyle(void){
 static const wchar_t *faces[]={L"Segoe UI",L"Georgia",L"Consolas"};
 CHARRANGE all={0,-1};SendMessageW(editor,EM_EXSETSEL,0,(LPARAM)&all);
 CHARFORMAT2W f={0};f.cbSize=sizeof(f);f.dwMask=CFM_FACE;wcsncpy(f.szFaceName,faces[prefs.fontFace],LF_FACESIZE-1);SendMessageW(editor,EM_SETCHARFORMAT,SCF_SELECTION,(LPARAM)&f);
 PARAFORMAT2 p={0};p.cbSize=sizeof(p);p.dwMask=PFM_LINESPACING;p.bLineSpacingRule=5;p.dyLineSpacing=prefs.spacing==0?20:prefs.spacing==1?26:32;SendMessageW(editor,EM_SETPARAFORMAT,0,(LPARAM)&p);
}
static void inlineStyle(LONG start,LONG end,int kind,int heading){
 CHARRANGE r={start,end};SendMessageW(editor,EM_EXSETSEL,0,(LPARAM)&r);CHARFORMAT2W f={0};f.cbSize=sizeof(f);
 if(kind==1){f.dwMask=CFM_BOLD;f.dwEffects=CFE_BOLD;}else if(kind==2){f.dwMask=CFM_ITALIC;f.dwEffects=CFE_ITALIC;}else{f.dwMask=CFM_COLOR;f.crTextColor=ACCENT;}
 (void)heading;SendMessageW(editor,EM_SETCHARFORMAT,SCF_SELECTION,(LPARAM)&f);
}
static void showContent(void) {
 if(modeAnim.target!=(reading?1.0f:0.0f)){startAnimation(&modeAnim,reading?1.0f:0.0f,260);startAnimation(&modeHoverAnim,hoveredMode==(reading?1:0)?1.0f:0.0f,180);}
 loading=1; EnableWindow(editor,current[0]!=0); for(int i=2;i<6;i++)EnableWindow(buttons[i],current[0]!=0);if(formatBar)EnableWindow(formatBar,current[0]&&!reading);if(buttons[7])EnableWindow(buttons[7],current[0]!=0); SendMessageW(editor,WM_SETREDRAW,FALSE,0); SendMessageW(editor,EM_SETREADONLY,FALSE,0);
 if (!reading) { SetWindowTextW(editor,text?text:L""); styleRange(0,-1,TEXT,prefs.fontSize,0); }
 else {
  // Render a simple, readable Markdown view without altering the original text.
  SetWindowTextW(editor,L""); const wchar_t *p=text?text:L""; LONG position=0;
  while(*p){const wchar_t *end=wcschr(p,L'\n');size_t len=end?(size_t)(end-p):wcslen(p);if(len&&p[len-1]==L'\r')len--;
   int level=0;while(level<3&&(size_t)level<len&&p[level]==L'#')level++;size_t skip=(level&&(size_t)level<len&&p[level]==L' ')?(size_t)level+1:0;if(!skip)level=0;
   wchar_t bullet=0;if(!level&&len>=2&&p[0]==L'>'&&p[1]==L' ')skip=2;
   else if(!level&&len>=6&&p[0]==L'-'&&p[1]==L' '&&p[2]==L'['&&p[4]==L']'&&p[5]==L' '){bullet=(p[3]==L'x'||p[3]==L'X')?L'☑':L'☐';skip=6;}
   else if(!level&&len>=2&&p[0]==L'-'&&p[1]==L' '){bullet=L'•';skip=2;}
   size_t inputLen=len-skip,spanCap=inputLen<4096?inputLen+1:4096,spanCount=0;wchar_t *line=malloc((inputLen+5)*sizeof(wchar_t));MarkdownSpan *spans=malloc(spanCap*sizeof(MarkdownSpan));
   if(!line||!spans){free(line);free(spans);devLog(L"Preview stopped: out of memory");break;}
   size_t prefix=bullet?2:0;if(bullet){line[0]=bullet;line[1]=L' ';}
   size_t count=markdown_inline(p+skip,inputLen,line+prefix,spans,spanCap,&spanCount)+prefix;line[count]=L'\r';line[count+1]=L'\n';line[count+2]=0;
   SendMessageW(editor,EM_SETSEL,(WPARAM)-1,(LPARAM)-1);SendMessageW(editor,EM_REPLACESEL,FALSE,(LPARAM)line);
   styleRange(position,position+(LONG)count,level==1?RGB(213,230,255):(level?ACCENT:(p[0]==L'>'?MUTED:TEXT)),prefs.fontSize+(level==1?9:level==2?4:level==3?2:0),level>0);
   for(size_t i=0;i<spanCount;i++)inlineStyle(position+(LONG)prefix+(LONG)spans[i].start,position+(LONG)prefix+(LONG)(spans[i].start+spans[i].length),spans[i].kind,level);
   position+=(LONG)count+1;free(line);free(spans);if(!end)break;p=end+1;
  }
 }
 applyDocumentStyle();if(reading)SendMessageW(editor,EM_SETREADONLY,TRUE,0);
 measureText();SendMessageW(editor,EM_SETSEL,0,0); SendMessageW(editor,WM_SETREDRAW,TRUE,0);InvalidateRect(editor,NULL,TRUE); loading=0;InvalidateRect(window,NULL,FALSE);InvalidateRect(buttons[5],NULL,FALSE);
}
static int loadNote(const wchar_t *name) { if(!saveNote(1))return 0; wchar_t path[MAX_PATH];pathFor(name,path,MAX_PATH);wchar_t *content=readUtf8(path);if(!content){error(L"Не удалось открыть заметку. Поддерживаются UTF-8 файлы размером до 4 МБ.");return 0;} free(text);text=content;wcsncpy(current,name,MAX_PATH-1);current[MAX_PATH-1]=0;dirty=0;selectionAnim.value=selectionAnim.target=0;selectionAnim.active=0;startAnimation(&selectionAnim,1.0f,220);devLog(L"Opened note: %lu characters",(unsigned long)wcslen(content));showContent();refreshList();return 1; }
static void createNoteWith(const wchar_t *content) {
 if(!saveNote(1))return;wchar_t name[128],path[MAX_PATH];int found=0;
 for(int i=1;i<10000;i++){swprintf(name,128,L"Новая заметка %d",i);pathFor(name,path,MAX_PATH);HANDLE reservation=CreateFileW(path,GENERIC_WRITE,0,NULL,CREATE_NEW,FILE_ATTRIBUTE_NORMAL,NULL);
  if(reservation!=INVALID_HANDLE_VALUE){CloseHandle(reservation);found=1;break;}DWORD failure=GetLastError();if(failure!=ERROR_FILE_EXISTS&&failure!=ERROR_ALREADY_EXISTS)break;}
 if(!found){error(L"Не удалось создать заметку.");return;}
 if(!writeUtf8(path,content)){DeleteFileW(path);error(L"Не удалось создать заметку.");return;}devLog(L"Created note: %lu characters",(unsigned long)wcslen(content));SetWindowTextW(search,L"");loadNote(name);reading=0;showContent();SetFocus(editor);
}
static void importNote(void) { wchar_t file[MAX_PATH]=L"";OPENFILENAMEW of={0};of.lStructSize=sizeof(of);of.hwndOwner=window;of.lpstrFilter=L"Текст и Markdown\0*.md;*.txt;*.markdown\0Все файлы\0*.*\0";of.lpstrFile=file;of.nMaxFile=MAX_PATH;of.Flags=OFN_FILEMUSTEXIST|OFN_PATHMUSTEXIST|OFN_NOCHANGEDIR;if(GetOpenFileNameW(&of)){devLog(L"Import selected");wchar_t *value=readUtf8(file);if(!value){error(L"Не удалось прочитать файл. Используйте UTF-8 файл размером до 4 МБ.");return;}createNoteWith(value);free(value);} }
static void exportNote(void) { if(!current[0])return;if(!saveNote(1))return;wchar_t file[MAX_PATH];swprintf(file,MAX_PATH,L"%ls.md",current);OPENFILENAMEW of={0};of.lStructSize=sizeof(of);of.hwndOwner=window;of.lpstrFilter=L"Markdown\0*.md\0Текст\0*.txt\0";of.lpstrDefExt=L"md";of.lpstrFile=file;of.nMaxFile=MAX_PATH;of.Flags=OFN_OVERWRITEPROMPT|OFN_PATHMUSTEXIST|OFN_NOCHANGEDIR;if(GetSaveFileNameW(&of)){if(!writeUtf8(file,text?text:L""))error(L"Не удалось экспортировать файл.");else devLog(L"Export completed");} }
static void drawText(HDC dc,const wchar_t *str,RECT r,HFONT font,COLORREF color,UINT flags);
static void fill(HDC dc,RECT r,COLORREF color);
static void rounded(HDC dc,RECT r,COLORREF color,int radius);
static void glassSurface(HDC dc,RECT r,COLORREF base,COLORREF behind,int radius,int hover);
static void darkTitle(HWND h);
static void roundControl(HWND h,int width,int height,int diameter){
 HRGN region=CreateRoundRectRgn(0,0,width+1,height+1,diameter,diameter);
 if(region&&!SetWindowRgn(h,region,TRUE))DeleteObject(region);
}
static LRESULT CALLBACK promptButtonProc(HWND h,UINT message,WPARAM w,LPARAM l){
 int i=(h==promptButtons[0])?0:1;
 if(message==WM_MOUSEMOVE&&!promptHover[i]){promptHover[i]=1;TRACKMOUSEEVENT event={sizeof(event),TME_LEAVE,h,0};TrackMouseEvent(&event);startAnimation(&promptAnim[i],1.0f,190);InvalidateRect(h,NULL,FALSE);}
 if(message==WM_MOUSELEAVE){promptHover[i]=0;startAnimation(&promptAnim[i],0.0f,220);InvalidateRect(h,NULL,FALSE);}
 if(message==WM_SETFOCUS||message==WM_KILLFOCUS)InvalidateRect(h,NULL,FALSE);
 return CallWindowProcW(oldPromptButtonProc,h,message,w,l);
}
static LRESULT CALLBACK PromptProc(HWND h,UINT m,WPARAM w,LPARAM l) {
 switch(m) {
 case WM_CREATE: {
  HINSTANCE instance=GetModuleHandleW(NULL);darkTitle(h);
  promptInput=CreateWindowExW(0,L"EDIT",promptValue,WS_CHILD|WS_VISIBLE|WS_TABSTOP|ES_AUTOHSCROLL,34,85,360,24,h,(HMENU)ID_PROMPT,instance,NULL);
  SendMessageW(promptInput,WM_SETFONT,(WPARAM)ui,TRUE);SendMessageW(promptInput,EM_LIMITTEXT,120,0);
  const wchar_t *labels[]={L"Отмена",L"Сохранить"};const int ids[]={ID_CANCEL,ID_OK};const int x[]={192,302};
  for(int i=0;i<2;i++){
   promptHover[i]=0;promptAnim[i]=(Animation){0};
   promptButtons[i]=CreateWindowW(L"BUTTON",labels[i],WS_CHILD|WS_VISIBLE|WS_TABSTOP|BS_OWNERDRAW,x[i],137,100,38,h,(HMENU)(INT_PTR)ids[i],instance,NULL);
   SendMessageW(promptButtons[i],WM_SETFONT,(WPARAM)uiBold,TRUE);
   WNDPROC previous=(WNDPROC)SetWindowLongPtrW(promptButtons[i],GWLP_WNDPROC,(LONG_PTR)promptButtonProc);if(i==0)oldPromptButtonProc=previous;
   roundControl(promptButtons[i],100,38,36);
  }
  SetFocus(promptInput);SendMessageW(promptInput,EM_SETSEL,0,-1);return 0;
 }
 case WM_ERASEBKGND:return 1;
 case WM_PAINT: {
  PAINTSTRUCT ps;HDC dc=BeginPaint(h,&ps);RECT client;GetClientRect(h,&client);fill(dc,client,BG);
  RECT heading={24,17,client.right-24,43};drawText(dc,L"Новое название заметки",heading,brandFont,TEXT,DT_LEFT|DT_SINGLELINE|DT_VCENTER);
  RECT subtitle={24,46,client.right-24,68};drawText(dc,L"Выберите новое название для заметки",subtitle,uiSmall,MUTED,DT_LEFT|DT_SINGLELINE|DT_VCENTER);
  RECT inputBox={24,76,client.right-24,117};rounded(dc,inputBox,RGB(35,43,59),18);
  EndPaint(h,&ps);return 0;
 }
 case WM_CTLCOLOREDIT: {HDC dc=(HDC)w;SetTextColor(dc,TEXT);SetBkColor(dc,RGB(35,43,59));return (LRESULT)inputBrush;}
 case WM_DRAWITEM: {
  DRAWITEMSTRUCT *original=(DRAWITEMSTRUCT*)l, copy=*original;int width=original->rcItem.right-original->rcItem.left,height=original->rcItem.bottom-original->rcItem.top;
  HDC memory=CreateCompatibleDC(original->hDC);HBITMAP bmp=memory?CreateCompatibleBitmap(original->hDC,width,height):NULL;HGDIOBJ oldBitmap=bmp?SelectObject(memory,bmp):NULL;
  if(bmp){copy.hDC=memory;SetRect(&copy.rcItem,0,0,width,height);}DRAWITEMSTRUCT *d=bmp?&copy:original;
  int primary=(d->CtlID==ID_OK),i=primary?1:0;fill(d->hDC,d->rcItem,BG);
  COLORREF base=primary?RGB(55,94,154):RGB(42,51,67);if(d->itemState&ODS_SELECTED)base=RGB(79,111,157);
  RECT pill=d->rcItem;InflateRect(&pill,-1,-1);glassSurface(d->hDC,pill,base,BG,18,(int)(promptAnim[i].value*70));
  wchar_t label[32];GetWindowTextW(d->hwndItem,label,32);drawText(d->hDC,label,d->rcItem,uiBold,TEXT,DT_SINGLELINE|DT_CENTER|DT_VCENTER);
  if(d->itemState&ODS_FOCUS){RECT focus=d->rcItem;focus.left+=25;focus.right-=25;focus.top=focus.bottom-5;focus.bottom-=3;rounded(d->hDC,focus,ACCENT,2);}
  if(bmp){BitBlt(original->hDC,original->rcItem.left,original->rcItem.top,width,height,memory,0,0,SRCCOPY);SelectObject(memory,oldBitmap);DeleteObject(bmp);}if(memory)DeleteDC(memory);return TRUE;
 }
 case WM_COMMAND:
  if(LOWORD(w)==ID_OK){GetWindowTextW(promptInput,promptValue,180);if(!validName(promptValue)){MessageBoxW(h,L"Введите корректное название без символов \\ / : * ? \" < > |.",L"Editor 1",MB_ICONWARNING);return 0;}promptAccepted=1;DestroyWindow(h);return 0;}
  if(LOWORD(w)==ID_CANCEL){DestroyWindow(h);return 0;}break;
 case WM_CLOSE:DestroyWindow(h);return 0;
 case WM_DESTROY:promptDone=1;for(int i=0;i<2;i++){promptAnim[i].active=0;promptButtons[i]=NULL;}return 0;
 }
 return DefWindowProcW(h,m,w,l);
}
static void renameNote(void) {
 if(!current[0]||!saveNote(1))return;wcscpy(promptValue,current);promptDone=promptAccepted=0;
 WNDCLASSW wc={0};wc.lpfnWndProc=PromptProc;wc.hInstance=GetModuleHandleW(NULL);wc.lpszClassName=L"Editor1Prompt";wc.hCursor=LoadCursor(NULL,IDC_ARROW);wc.hbrBackground=bgBrush;RegisterClassW(&wc);
 RECT r;GetWindowRect(window,&r);int width=440,height=230;int x=r.left+((r.right-r.left)-width)/2,y=r.top+((r.bottom-r.top)-height)/2;
 HWND dialog=CreateWindowExW(WS_EX_DLGMODALFRAME,L"Editor1Prompt",L"Новое название — Editor 1",WS_CAPTION|WS_SYSMENU|WS_POPUP|WS_CLIPCHILDREN,x,y,width,height,window,NULL,wc.hInstance,NULL);
 if(!dialog){error(L"Не удалось открыть окно переименования.");return;}
 EnableWindow(window,FALSE);ShowWindow(dialog,SW_SHOW);SetForegroundWindow(dialog);SetFocus(promptInput);
 MSG msg;while(!promptDone&&GetMessageW(&msg,NULL,0,0)>0){
  if(msg.message==WM_KEYDOWN&&msg.wParam==VK_ESCAPE){SendMessageW(dialog,WM_COMMAND,ID_CANCEL,0);continue;}
  if(msg.message==WM_KEYDOWN&&msg.wParam==VK_RETURN){SendMessageW(dialog,WM_COMMAND,GetFocus()==promptButtons[0]?ID_CANCEL:ID_OK,0);continue;}
  if(!IsDialogMessageW(dialog,&msg)){TranslateMessage(&msg);DispatchMessageW(&msg);}
 }
 EnableWindow(window,TRUE);SetForegroundWindow(window);
 if(!promptAccepted||!wcscmp(promptValue,current))return;
 wchar_t old[MAX_PATH],next[MAX_PATH];pathFor(current,old,MAX_PATH);pathFor(promptValue,next,MAX_PATH);
 if(GetFileAttributesW(next)!=INVALID_FILE_ATTRIBUTES){MessageBoxW(window,L"Заметка с таким названием уже существует.",L"Editor 1",MB_ICONWARNING);return;}
 if(!MoveFileW(old,next)){error(L"Не удалось переименовать заметку.");return;}wcscpy(current,promptValue);devLog(L"Note renamed");refreshList();
}
static void deleteNote(void) { if(!current[0])return;if(MessageBoxW(window,L"Удалить эту заметку? Это действие нельзя отменить.",L"Editor 1",MB_YESNO|MB_ICONWARNING)!=IDYES)return;wchar_t path[MAX_PATH];pathFor(current,path,MAX_PATH);if(!DeleteFileW(path)){error(L"Не удалось удалить заметку.");return;}devLog(L"Note deleted");current[0]=0;dirty=0;free(text);text=duptext(L"");SetWindowTextW(search,L"");refreshList();if(SendMessageW(list,LB_GETCOUNT,0,0)>0){wchar_t name[MAX_PATH];SendMessageW(list,LB_GETTEXT,0,(LPARAM)name);loadNote(name);}else showContent(); }
static void duplicateNote(void){
 if(!current[0]||!saveNote(1))return;
 wchar_t base[81],name[128],path[MAX_PATH];wcsncpy(base,current,80);base[80]=0;HANDLE reservation=INVALID_HANDLE_VALUE;int found=0;
 for(int i=1;i<10000;i++){if(i==1)swprintf(name,128,L"Копия — %ls",base);else swprintf(name,128,L"Копия %d — %ls",i,base);pathFor(name,path,MAX_PATH);
  reservation=CreateFileW(path,GENERIC_WRITE,0,NULL,CREATE_NEW,FILE_ATTRIBUTE_NORMAL,NULL);if(reservation!=INVALID_HANDLE_VALUE){found=1;CloseHandle(reservation);break;}if(GetLastError()!=ERROR_FILE_EXISTS&&GetLastError()!=ERROR_ALREADY_EXISTS)break;}
 if(!found){error(L"Не удалось создать копию заметки.");return;}
 if(!writeUtf8(path,text?text:L"")){DeleteFileW(path);error(L"Не удалось сохранить копию заметки.");return;}
 devLog(L"Note duplicated");SetWindowTextW(search,L"");loadNote(name);
}
static void drawText(HDC dc,const wchar_t *str,RECT r,HFONT font,COLORREF color,UINT flags){SetBkMode(dc,TRANSPARENT);SetTextColor(dc,color);HGDIOBJ old=SelectObject(dc,font);DrawTextW(dc,str,-1,&r,flags);SelectObject(dc,old);}
static void fill(HDC dc,RECT r,COLORREF color){HBRUSH b=CreateSolidBrush(color);FillRect(dc,&r,b);DeleteObject(b);}
// Adapted from ysc3839/win32-darkmode (MIT); see THIRD_PARTY_LICENSE.txt.
// This fallback is only applied to our own window, never to other processes.
typedef struct { int Attrib; PVOID pvData; SIZE_T cbData; } WINDOW_COMPOSITION_DATA;
typedef BOOL (WINAPI *SetCompositionFn)(HWND, WINDOW_COMPOSITION_DATA *);
static void darkTitle(HWND h) {
 BOOL dark=TRUE;
 HMODULE dwm=LoadLibraryW(L"dwmapi.dll");
 if(dwm){typedef HRESULT (WINAPI *DwmSetFn)(HWND,DWORD,LPCVOID,DWORD);DwmSetFn set=(DwmSetFn)GetProcAddress(dwm,"DwmSetWindowAttribute");if(set && SUCCEEDED(set(h,20,&dark,sizeof(dark)))){FreeLibrary(dwm);return;}FreeLibrary(dwm);}
 HMODULE user=GetModuleHandleW(L"user32.dll");SetCompositionFn set=(SetCompositionFn)GetProcAddress(user,"SetWindowCompositionAttribute");
 if(set){WINDOW_COMPOSITION_DATA data={26,&dark,sizeof(dark)};set(h,&data);}
}
static COLORREF mixColor(COLORREF a,COLORREF b,int amount){return RGB((GetRValue(a)*(100-amount)+GetRValue(b)*amount)/100,(GetGValue(a)*(100-amount)+GetGValue(b)*amount)/100,(GetBValue(a)*(100-amount)+GetBValue(b)*amount)/100);}
static void rounded(HDC dc,RECT r,COLORREF color,int radius){HBRUSH brush=CreateSolidBrush(color);HGDIOBJ oldBrush=SelectObject(dc,brush);HGDIOBJ oldPen=SelectObject(dc,GetStockObject(NULL_PEN));RoundRect(dc,r.left,r.top,r.right,r.bottom,radius,radius);SelectObject(dc,oldPen);SelectObject(dc,oldBrush);DeleteObject(brush);}
// The rounded-box signed distance equation is adapted from the pixel shader in
// oversmartnader/liquid-glass-windows-trial (MIT). We shade it in software
// against the application's own backdrop; there is no desktop capture.
static float glassSdf(float x,float y,float halfW,float halfH,float radius){
 float dx=fabsf(x)-halfW+radius,dy=fabsf(y)-halfH+radius;
 return fminf(fmaxf(dx,dy),0.0f)+sqrtf(fmaxf(dx,0)*fmaxf(dx,0)+fmaxf(dy,0)*fmaxf(dy,0))-radius;
}
static unsigned char channel(float n){return (unsigned char)(n<0?0:n>255?255:n);}
static void glassSurface(HDC dc,RECT r,COLORREF base,COLORREF behind,int radius,int hover){
 int w=r.right-r.left,h=r.bottom-r.top;if(w<2||h<2||w>2000||h>1000)return;
 BITMAPINFO bi={0};bi.bmiHeader.biSize=sizeof(BITMAPINFOHEADER);bi.bmiHeader.biWidth=w;bi.bmiHeader.biHeight=-h;bi.bmiHeader.biPlanes=1;bi.bmiHeader.biBitCount=32;bi.bmiHeader.biCompression=BI_RGB;
 void *memory=NULL;HBITMAP bmp=CreateDIBSection(dc,&bi,DIB_RGB_COLORS,&memory,NULL,0);if(!bmp||!memory){if(bmp)DeleteObject(bmp);rounded(dc,r,base,radius*2);return;}
 unsigned int *pixels=(unsigned int*)memory;
 for(int y=0;y<h;y++)for(int x=0;x<w;x++){
  float d=glassSdf(x+0.5f-w*.5f,y+0.5f-h*.5f,w*.5f-.75f,h*.5f-.75f,(float)radius);
  float coverage=fminf(1.0f,fmaxf(0.0f,0.55f-d));float fy=(float)y/(float)h;
  float rim=fmaxf(0.0f,1.8f+ d)/1.8f;rim=fminf(1.0f,rim);
  float sheen=hover<0?0.0f:(1.0f-fy)*9.0f+rim*(fy<.5f?26.0f:9.0f)+(float)hover*.075f;
  float br=GetRValue(base)+sheen,bg=GetGValue(base)+sheen*1.05f,bb=GetBValue(base)+sheen*1.2f;
  unsigned char R=channel(GetRValue(behind)*(1-coverage)+br*coverage),G=channel(GetGValue(behind)*(1-coverage)+bg*coverage),B=channel(GetBValue(behind)*(1-coverage)+bb*coverage);
  pixels[y*w+x]=((unsigned int)R<<16)|((unsigned int)G<<8)|B;
 }
 HDC mem=CreateCompatibleDC(dc);if(mem){HGDIOBJ old=SelectObject(mem,bmp);BitBlt(dc,r.left,r.top,w,h,mem,0,0,SRCCOPY);SelectObject(mem,old);DeleteDC(mem);}DeleteObject(bmp);
}
// Lucide SVGs are converted at build time into alpha masks (see generate_icons.py).
static void icon(HDC dc,int id,int x,int y,COLORREF color){
 if(id<0||id>ICON_SETTINGS)return;BITMAPINFO bi={0};bi.bmiHeader.biSize=sizeof(BITMAPINFOHEADER);bi.bmiHeader.biWidth=20;bi.bmiHeader.biHeight=-20;bi.bmiHeader.biPlanes=1;bi.bmiHeader.biBitCount=32;bi.bmiHeader.biCompression=BI_RGB;
 void *data=NULL;HBITMAP bmp=CreateDIBSection(dc,&bi,DIB_RGB_COLORS,&data,NULL,0);if(!bmp||!data){if(bmp)DeleteObject(bmp);return;}
 unsigned int *pixels=(unsigned int*)data;for(int i=0;i<400;i++){unsigned int a=icon_masks[id][i];pixels[i]=(a<<24)|((GetRValue(color)*a/255)<<16)|((GetGValue(color)*a/255)<<8)|(GetBValue(color)*a/255);}
 HDC mem=CreateCompatibleDC(dc);if(mem){HGDIOBJ old=SelectObject(mem,bmp);BLENDFUNCTION blend={AC_SRC_OVER,0,255,AC_SRC_ALPHA};AlphaBlend(dc,x,y,20,20,mem,0,0,20,20,blend);SelectObject(mem,old);DeleteDC(mem);}DeleteObject(bmp);
}
static LRESULT CALLBACK buttonProc(HWND h,UINT message,WPARAM w,LPARAM l){
 int i=(int)GetWindowLongPtrW(h,GWLP_USERDATA);
 if(message==WM_MOUSEMOVE && i>=0 && i<8 && i!=5 && !hovered[i]){hovered[i]=1;TRACKMOUSEEVENT t={sizeof(t),TME_LEAVE,h,0};TrackMouseEvent(&t);startAnimation(&hoverAnim[i],1.0f,190);}
 if(message==WM_MOUSELEAVE && i>=0 && i<8 && i!=5){hovered[i]=0;startAnimation(&hoverAnim[i],0.0f,230);}
 if(message==WM_SETFOCUS||message==WM_KILLFOCUS)InvalidateRect(h,NULL,FALSE);
 return CallWindowProcW(oldButtonProc,h,message,w,l);
}
static LRESULT CALLBACK searchProc(HWND h,UINT message,WPARAM w,LPARAM l){
 LRESULT result=CallWindowProcW(oldSearchProc,h,message,w,l);
 if(message==WM_PAINT && !GetWindowTextLengthW(h) && GetFocus()!=h){HDC dc=GetDC(h);RECT r;GetClientRect(h,&r);r.left+=2;drawText(dc,L"Поиск заметок...",r,uiSmall,MUTED,DT_SINGLELINE|DT_VCENTER);ReleaseDC(h,dc);}
 if(message==WM_SETFOCUS||message==WM_KILLFOCUS){startAnimation(&searchAnim,message==WM_SETFOCUS?1.0f:0.0f,220);InvalidateRect(h,NULL,TRUE);}
 return result;
}
static LRESULT CALLBACK listProc(HWND h,UINT message,WPARAM w,LPARAM l){
 if(message==WM_MOUSEMOVE){
  LRESULT hit=SendMessageW(h,LB_ITEMFROMPOINT,0,l);int next=HIWORD(hit)?-1:(int)LOWORD(hit);
  if(next!=hoveredList){hoveredList=next;listHoverAnim.value=listHoverAnim.target=0;listHoverAnim.active=0;startAnimation(&listHoverAnim,next>=0?1.0f:0.0f,200);InvalidateRect(h,NULL,FALSE);}else if(next>=0&&listHoverAnim.target==0)startAnimation(&listHoverAnim,1.0f,200);
  TRACKMOUSEEVENT t={sizeof(t),TME_LEAVE,h,0};TrackMouseEvent(&t);
 }else if(message==WM_MOUSELEAVE){startAnimation(&listHoverAnim,0.0f,230);}
 return CallWindowProcW(oldListProc,h,message,w,l);
}
// A single custom window owns the whole segmented switch. There are no
// overlapping rectangular child buttons or clipping regions at the ends.
static LRESULT CALLBACK ModeSwitchProc(HWND h,UINT message,WPARAM w,LPARAM l){
 switch(message){
 case WM_ERASEBKGND:return 1;
 case WM_PAINT:{
  PAINTSTRUCT ps;HDC target=BeginPaint(h,&ps);RECT r;GetClientRect(h,&r);
  HDC mem=CreateCompatibleDC(target);HBITMAP bmp=mem?CreateCompatibleBitmap(target,r.right,r.bottom):NULL;HGDIOBJ oldBitmap=bmp?SelectObject(mem,bmp):NULL;HDC dc=bmp?mem:target;
  fill(dc,r,BG);glassSurface(dc,r,RGB(37,46,64),BG,21,-1);
  float progress=modeAnim.value;RECT active={(LONG)(4+132*progress),4,(LONG)(132+120*progress),r.bottom-4};
  glassSurface(dc,active,RGB(68,83,112),RGB(37,46,64),17,(int)(modeHoverAnim.value*22));
  const wchar_t *labels[]={L"Редактор",L"Чтение"};int icons[]={ICON_PEN_LINE,ICON_BOOK_OPEN};
  for(int i=0;i<2;i++){
   RECT part=i?(RECT){136,0,r.right,r.bottom}:(RECT){0,0,136,r.bottom};
   int selected=(i==0&&!reading)||(i==1&&reading);HFONT font=selected?uiBold:uiSmall;
   COLORREF color=IsWindowEnabled(h)?(selected?TEXT:RGB(181,185,199)):MUTED;
   HGDIOBJ old=SelectObject(dc,font);SIZE size;GetTextExtentPoint32W(dc,labels[i],(int)wcslen(labels[i]),&size);SelectObject(dc,old);
   int x=part.left+(part.right-part.left-(25+size.cx))/2;
   icon(dc,icons[i],x,(r.bottom-20)/2,color);RECT textRect=part;textRect.left=x+25;
   drawText(dc,labels[i],textRect,font,color,DT_SINGLELINE|DT_LEFT|DT_VCENTER);
  }
  if(GetFocus()==h){RECT line=active;line.left+=30;line.right-=30;line.top=line.bottom-4;line.bottom-=2;rounded(dc,line,ACCENT,3);}
  if(bmp){BitBlt(target,0,0,r.right,r.bottom,mem,0,0,SRCCOPY);SelectObject(mem,oldBitmap);DeleteObject(bmp);}if(mem)DeleteDC(mem);EndPaint(h,&ps);return 0;
 }
 case WM_MOUSEMOVE:{int x=(short)LOWORD(l);int next=x<136?0:1;if(next!=hoveredMode){hoveredMode=next;startAnimation(&modeHoverAnim,next==(reading?1:0)?1.0f:0.0f,180);InvalidateRect(h,NULL,FALSE);}TRACKMOUSEEVENT t={sizeof(t),TME_LEAVE,h,0};TrackMouseEvent(&t);return 0;}
 case WM_MOUSELEAVE:hoveredMode=-1;startAnimation(&modeHoverAnim,0.0f,220);InvalidateRect(h,NULL,FALSE);return 0;
 case WM_LBUTTONDOWN:SetFocus(h);SetCapture(h);return 0;
 case WM_LBUTTONUP:{if(GetCapture()==h)ReleaseCapture();RECT r;GetClientRect(h,&r);POINT p={(short)LOWORD(l),(short)HIWORD(l)};if(PtInRect(&r,p))SendMessageW(window,WM_COMMAND,p.x<136?ID_EDIT:ID_READ,0);return 0;}
 case WM_GETDLGCODE:return DLGC_WANTARROWS;
 case WM_KEYDOWN:if(w==VK_LEFT||w==VK_RIGHT||w==VK_SPACE||w==VK_RETURN){int edit=(w==VK_LEFT)?1:(w==VK_RIGHT)?0:reading;SendMessageW(window,WM_COMMAND,edit?ID_EDIT:ID_READ,0);return 0;}break;
 case WM_SETFOCUS:case WM_KILLFOCUS:case WM_ENABLE:InvalidateRect(h,NULL,FALSE);break;
 }
 return DefWindowProcW(h,message,w,l);
}
static void formatSelected(int operation){
 if(reading||!current[0]||!editor)return;
 CHARRANGE r;SendMessageW(editor,EM_EXGETSEL,0,(LPARAM)&r);
 if(operation>=2&&operation<=5){
  const wchar_t *prefix=operation==2?L"# ":operation==3?L"- ":operation==4?L"- [ ] ":L"> ";
  LRESULT line=SendMessageW(editor,EM_LINEFROMCHAR,r.cpMin,0),start=SendMessageW(editor,EM_LINEINDEX,line,0);
  if(start<0)return;CHARRANGE at={(LONG)start,(LONG)start};SendMessageW(editor,EM_EXSETSEL,0,(LPARAM)&at);SendMessageW(editor,EM_REPLACESEL,TRUE,(LPARAM)prefix);
  LONG shift=(LONG)wcslen(prefix);r.cpMin+=shift;r.cpMax+=shift;SendMessageW(editor,EM_EXSETSEL,0,(LPARAM)&r);SetFocus(editor);return;
 }
 const wchar_t *before=operation==0?L"**":operation==1?L"*":L"[",*after=operation==0?L"**":operation==1?L"*":L"](https://)";
 LONG selected=r.cpMax-r.cpMin;size_t cap=(size_t)(selected>0?selected*2:5)+1;
 wchar_t *part=calloc(cap,sizeof(wchar_t));if(!part){error(L"Недостаточно памяти для форматирования.");return;}
 if(selected>0)SendMessageW(editor,EM_GETSELTEXT,0,(LPARAM)part);else wcscpy(part,L"текст");
 size_t count=wcslen(before)+wcslen(part)+wcslen(after)+1;wchar_t *result=malloc(count*sizeof(wchar_t));
 if(!result){free(part);error(L"Недостаточно памяти для форматирования.");return;}
 swprintf(result,count,L"%ls%ls%ls",before,part,after);SendMessageW(editor,EM_REPLACESEL,TRUE,(LPARAM)result);
 r.cpMin+=(LONG)wcslen(before);r.cpMax=r.cpMin+(selected>0?selected:(LONG)wcslen(part));SendMessageW(editor,EM_EXSETSEL,0,(LPARAM)&r);SetFocus(editor);free(result);free(part);
}
static int formatHit(int x){static const int edge[]={0,42,84,130,210,300,384,482};for(int i=0;i<7;i++)if(x>=edge[i]&&x<edge[i+1])return i;return -1;}
static LRESULT CALLBACK FormatBarProc(HWND h,UINT m,WPARAM w,LPARAM l){
 switch(m){
 case WM_ERASEBKGND:return 1;
 case WM_PAINT:{PAINTSTRUCT ps;HDC target=BeginPaint(h,&ps);RECT r;GetClientRect(h,&r);HDC mem=CreateCompatibleDC(target);HBITMAP bmp=mem?CreateCompatibleBitmap(target,r.right,r.bottom):NULL;HGDIOBJ oldBmp=bmp?SelectObject(mem,bmp):NULL;HDC dc=bmp?mem:target;fill(dc,r,BG);
  static const wchar_t *labels[]={L"B",L"I",L"H1",L"• список",L"☑ задача",L"❝ цитата",L"↗ ссылка"};static const int edge[]={0,42,84,130,210,300,384,482};
  for(int i=0;i<7;i++){RECT item={edge[i],2,edge[i+1]-3,35};if(i==formatHover&&IsWindowEnabled(h))glassSurface(dc,item,RGB(45,57,77),BG,14,(int)(formatAnim.value*25));
   COLORREF color=IsWindowEnabled(h)?TEXT:MUTED;drawText(dc,labels[i],item,(i==0||i==2)?uiBold:uiSmall,color,DT_CENTER|DT_VCENTER|DT_SINGLELINE);
   if(GetFocus()==h&&i==formatFocus){RECT mark=item;mark.left+=12;mark.right-=12;mark.top=mark.bottom-3;rounded(dc,mark,ACCENT,2);}}
  if(bmp){BitBlt(target,0,0,r.right,r.bottom,mem,0,0,SRCCOPY);SelectObject(mem,oldBmp);DeleteObject(bmp);}if(mem)DeleteDC(mem);EndPaint(h,&ps);return 0;}
 case WM_MOUSEMOVE:{int next=formatHit((short)LOWORD(l));if(next!=formatHover){formatHover=next;formatAnim.value=formatAnim.target=0;formatAnim.active=0;startAnimation(&formatAnim,next>=0?1.0f:0.0f,180);InvalidateRect(h,NULL,FALSE);}else if(next>=0&&formatAnim.target==0)startAnimation(&formatAnim,1.0f,180);TRACKMOUSEEVENT t={sizeof(t),TME_LEAVE,h,0};TrackMouseEvent(&t);return 0;}
 case WM_MOUSELEAVE:startAnimation(&formatAnim,0.0f,200);return 0;
 case WM_LBUTTONDOWN:SetCapture(h);return 0;
 case WM_LBUTTONUP:{if(GetCapture()==h)ReleaseCapture();RECT r;GetClientRect(h,&r);POINT p={(short)LOWORD(l),(short)HIWORD(l)};int hit=PtInRect(&r,p)?formatHit(p.x):-1;if(hit>=0)formatSelected(hit);return 0;}
 case WM_GETDLGCODE:return DLGC_WANTARROWS;
 case WM_KEYDOWN:if(w==VK_LEFT||w==VK_RIGHT){formatFocus=(formatFocus+(w==VK_LEFT?6:1))%7;InvalidateRect(h,NULL,FALSE);return 0;}if(w==VK_RETURN||w==VK_SPACE){formatSelected(formatFocus);return 0;}break;
 case WM_SETFOCUS:case WM_KILLFOCUS:case WM_ENABLE:InvalidateRect(h,NULL,FALSE);break;
 }return DefWindowProcW(h,m,w,l);
}
static void layout(HWND h) {
 RECT r;GetClientRect(h,&r);int w=r.right, height=r.bottom;int side=256;
 MoveWindow(search,47,105,side-74,27,TRUE);MoveWindow(buttons[0],17,156,154,42,TRUE);MoveWindow(buttons[7],177,156,62,42,TRUE);MoveWindow(list,10,262,side-20,height-390,TRUE);MoveWindow(buttons[6],17,height-116,side-34,34,TRUE);
 int right=w-22;MoveWindow(buttons[1],right-410,19,98,34,TRUE);MoveWindow(buttons[2],right-306,19,95,34,TRUE);MoveWindow(buttons[3],right-206,19,100,34,TRUE);MoveWindow(buttons[4],right-101,19,101,34,TRUE);
 int content=side+((w-side-760)>0?(w-side-760)/2:34), contentWidth=w-content-38;if(contentWidth>760)contentWidth=760;if(contentWidth<220)contentWidth=220;
 MoveWindow(buttons[5],content,215,256,42,TRUE);MoveWindow(formatBar,content,269,contentWidth,38,TRUE);MoveWindow(editor,content,321,contentWidth,height-371,TRUE);
}
static void drawItem(DRAWITEMSTRUCT *d) {
 if(d->CtlType==ODT_LISTBOX){if(d->itemID==(UINT)-1)return;wchar_t name[MAX_PATH];SendMessageW(list,LB_GETTEXT,d->itemID,(LPARAM)name);int selected=d->itemState&ODS_SELECTED;fill(d->hDC,d->rcItem,SIDE);RECT pill=d->rcItem;InflateRect(&pill,-2,-2);if(selected)glassSurface(d->hDC,pill,mixColor(SIDE,RGB(42,57,82),(int)(selectionAnim.value*100)),SIDE,10,0);else if((int)d->itemID==hoveredList&&listHoverAnim.value>0)glassSurface(d->hDC,pill,mixColor(SIDE,RGB(39,49,67),(int)(listHoverAnim.value*100)),SIDE,10,-1);icon(d->hDC,ICON_FILE_TEXT,d->rcItem.left+11,d->rcItem.top+(d->rcItem.bottom-d->rcItem.top-20)/2,selected?ACCENT:MUTED);RECT label=d->rcItem;label.left+=38;label.right-=8;drawText(d->hDC,name,label,selected?uiBold:uiSmall,selected?TEXT:RGB(169,166,177),DT_SINGLELINE|DT_VCENTER|DT_END_ELLIPSIS);return;}
 int id=d->CtlID;int index=-1;for(int i=0;i<8;i++)if(i!=5&&buttons[i]==d->hwndItem)index=i;
 int active=(id==ID_EDIT&&!reading)||(id==ID_READ&&reading);int hover=index>=0?hoverAmount[index]:0;
 COLORREF background=(id==ID_NEW||id==ID_SETTINGS||id==ID_DUPLICATE)?SIDE:((id==ID_EDIT||id==ID_READ)?RGB(37,46,64):BG);fill(d->hDC,d->rcItem,background);
 COLORREF base=id==ID_NEW?RGB(55,94,154):(active?RGB(68,83,112):background);COLORREF over=id==ID_NEW?RGB(75,121,187):RGB(63,78,101);
 COLORREF back=mixColor(base,over,hover);if(d->itemState&ODS_SELECTED)back=mixColor(back,TEXT,16);
 RECT pill=d->rcItem;InflateRect(&pill,-1,-1);if(id==ID_NEW||active||hover>0)glassSurface(d->hDC,pill,back,background,(pill.bottom-pill.top)/2,hover);
 wchar_t label[80];GetWindowTextW(d->hwndItem,label,80);COLORREF color=(d->itemState&ODS_DISABLED)?RGB(91,89,99):(id==ID_NEW||active?TEXT:RGB(181,178,189));
 int iconId=id==ID_NEW?ICON_PLUS:id==ID_OPEN?ICON_FOLDER_OPEN:id==ID_SAVE?ICON_DOWNLOAD:id==ID_RENAME?ICON_PENCIL:id==ID_DELETE?ICON_TRASH:id==ID_EDIT?ICON_PEN_LINE:id==ID_SETTINGS?ICON_SETTINGS:id==ID_DUPLICATE?ICON_FILE_TEXT:ICON_BOOK_OPEN;
 HFONT font=(id==ID_NEW||active)?uiBold:uiSmall;HGDIOBJ old=SelectObject(d->hDC,font);SIZE size;GetTextExtentPoint32W(d->hDC,label,(int)wcslen(label),&size);SelectObject(d->hDC,old);
 int group=20+5+size.cx;int x=d->rcItem.left+(d->rcItem.right-d->rcItem.left-group)/2;if(x<d->rcItem.left+3)x=d->rcItem.left+3;
 icon(d->hDC,iconId,x,d->rcItem.top+(d->rcItem.bottom-d->rcItem.top-20)/2,color);RECT textRect=d->rcItem;textRect.left=x+25;drawText(d->hDC,label,textRect,font,color,DT_SINGLELINE|DT_LEFT|DT_VCENTER|DT_END_ELLIPSIS);
 if(d->itemState&ODS_FOCUS){RECT line=d->rcItem;line.left+=13;line.right-=13;line.top=line.bottom-4;line.bottom-=2;rounded(d->hDC,line,ACCENT,3);}
}
static void drawBufferedItem(DRAWITEMSTRUCT *original){
 if(original->CtlType==ODT_LISTBOX&&original->itemID==(UINT)-1)return;
 int width=original->rcItem.right-original->rcItem.left,height=original->rcItem.bottom-original->rcItem.top;
 HDC memory=CreateCompatibleDC(original->hDC);HBITMAP bmp=memory?CreateCompatibleBitmap(original->hDC,width,height):NULL;
 if(!bmp){if(memory)DeleteDC(memory);drawItem(original);return;}
 HGDIOBJ old=SelectObject(memory,bmp);DRAWITEMSTRUCT copy=*original;copy.hDC=memory;SetRect(&copy.rcItem,0,0,width,height);drawItem(&copy);
 BitBlt(original->hDC,original->rcItem.left,original->rcItem.top,width,height,memory,0,0,SRCCOPY);SelectObject(memory,old);DeleteObject(bmp);DeleteDC(memory);
}
enum { SET_MINUS=300,SET_PLUS,SET_FACE0=310,SET_FACE1,SET_FACE2,SET_SPACE0=320,SET_SPACE1,SET_SPACE2,SET_DELAY0=330,SET_DELAY1,SET_RESET=340,SET_CANCEL,SET_APPLY };
static const int settingIds[]={SET_MINUS,SET_PLUS,SET_FACE0,SET_FACE1,SET_FACE2,SET_SPACE0,SET_SPACE1,SET_SPACE2,SET_DELAY0,SET_DELAY1,SET_RESET,SET_CANCEL,SET_APPLY};
static LRESULT CALLBACK settingsButtonProc(HWND h,UINT m,WPARAM w,LPARAM l){
 int i=(int)GetWindowLongPtrW(h,GWLP_USERDATA);if(i>=0&&i<13){
  if(m==WM_MOUSEMOVE&&!settingsHot[i]){settingsHot[i]=1;TRACKMOUSEEVENT t={sizeof(t),TME_LEAVE,h,0};TrackMouseEvent(&t);startAnimation(&settingsHover[i],1,180);InvalidateRect(h,NULL,FALSE);}
  if(m==WM_MOUSELEAVE){settingsHot[i]=0;startAnimation(&settingsHover[i],0,200);InvalidateRect(h,NULL,FALSE);}
  if(m==WM_SETFOCUS||m==WM_KILLFOCUS)InvalidateRect(h,NULL,FALSE);
 }return CallWindowProcW(oldSettingsButtonProc,h,m,w,l);
}
static void refreshSettings(HWND h){for(int i=0;i<13;i++)if(settingsButtons[i])InvalidateRect(settingsButtons[i],NULL,FALSE);InvalidateRect(h,NULL,FALSE);}
static LRESULT CALLBACK SettingsProc(HWND h,UINT m,WPARAM w,LPARAM l){
 switch(m){
 case WM_CREATE:{darkTitle(h);HINSTANCE instance=GetModuleHandleW(NULL);
  const wchar_t *labels[]={L"−",L"+",L"Sans",L"Serif",L"Mono",L"1.0",L"1.3",L"1.6",L"0,7 с",L"2 с",L"Сбросить",L"Отмена",L"Применить"};
  const int x[]={350,445,250,325,400,250,325,400,325,405,25,285,390};
  const int y[]={92,92,151,151,151,209,209,209,267,267,347,347,347};
  const int width[]={40,40,70,70,70,70,70,70,75,75,95,100,105};
  for(int i=0;i<13;i++){settingsHover[i]=(Animation){0};settingsHot[i]=0;settingsButtons[i]=CreateWindowW(L"BUTTON",labels[i],WS_CHILD|WS_VISIBLE|WS_TABSTOP|BS_OWNERDRAW,x[i],y[i],width[i],36,h,(HMENU)(INT_PTR)settingIds[i],instance,NULL);SendMessageW(settingsButtons[i],WM_SETFONT,(WPARAM)uiSmall,TRUE);SetWindowLongPtrW(settingsButtons[i],GWLP_USERDATA,(LONG_PTR)i);WNDPROC prev=(WNDPROC)SetWindowLongPtrW(settingsButtons[i],GWLP_WNDPROC,(LONG_PTR)settingsButtonProc);if(i==0)oldSettingsButtonProc=prev;roundControl(settingsButtons[i],width[i],36,20);}
  return 0;}
 case WM_ERASEBKGND:return 1;
 case WM_PAINT:{PAINTSTRUCT ps;HDC dc=BeginPaint(h,&ps);RECT r;GetClientRect(h,&r);fill(dc,r,BG);
  RECT title={25,17,r.right-20,43};drawText(dc,L"Настройки редактора",title,brandFont,TEXT,DT_SINGLELINE|DT_VCENTER);
  RECT sub={25,47,r.right-20,68};drawText(dc,L"Настройте пространство под себя",sub,uiSmall,MUTED,DT_SINGLELINE|DT_VCENTER);
  RECT textSize={25,95,235,126};drawText(dc,L"Размер текста",textSize,uiBold,TEXT,DT_SINGLELINE|DT_VCENTER);
  wchar_t size[20];swprintf(size,20,L"%d",pendingPrefs.fontSize);RECT number={390,94,445,126};drawText(dc,size,number,uiBold,TEXT,DT_CENTER|DT_SINGLELINE|DT_VCENTER);
  RECT font={25,154,240,185};drawText(dc,L"Шрифт",font,uiBold,TEXT,DT_SINGLELINE|DT_VCENTER);
  RECT spacing={25,212,240,243};drawText(dc,L"Межстрочный интервал",spacing,uiBold,TEXT,DT_SINGLELINE|DT_VCENTER);
  RECT save={25,270,260,301};drawText(dc,L"Автосохранение",save,uiBold,TEXT,DT_SINGLELINE|DT_VCENTER);
  RECT hint={25,308,r.right-25,327};drawText(dc,L"Изменения заметок сохраняются автоматически",hint,uiSmall,MUTED,DT_SINGLELINE|DT_VCENTER);
  EndPaint(h,&ps);return 0;}
 case WM_DRAWITEM:{DRAWITEMSTRUCT *d=(DRAWITEMSTRUCT*)l;int index=-1;for(int i=0;i<13;i++)if(settingIds[i]==(int)d->CtlID)index=i;if(index<0)return TRUE;
  int width=d->rcItem.right-d->rcItem.left,height=d->rcItem.bottom-d->rcItem.top;HDC mem=CreateCompatibleDC(d->hDC);HBITMAP bmp=mem?CreateCompatibleBitmap(d->hDC,width,height):NULL;HGDIOBJ old=bmp?SelectObject(mem,bmp):NULL;HDC dc=bmp?mem:d->hDC;RECT r=bmp?(RECT){0,0,width,height}:d->rcItem;fill(dc,r,BG);
  int active=(index>=2&&index<=4&&pendingPrefs.fontFace==index-2)||(index>=5&&index<=7&&pendingPrefs.spacing==index-5)||(index==8&&pendingPrefs.saveDelay==700)||(index==9&&pendingPrefs.saveDelay==2000)||index==12;
  COLORREF base=active?RGB(55,94,154):RGB(42,51,67);if(d->itemState&ODS_SELECTED)base=RGB(79,111,157);RECT pill=r;InflateRect(&pill,-1,-1);glassSurface(dc,pill,base,BG,10,(int)(settingsHover[index].value*50));
  wchar_t label[40];GetWindowTextW(d->hwndItem,label,40);drawText(dc,label,r,active?uiBold:uiSmall,TEXT,DT_CENTER|DT_SINGLELINE|DT_VCENTER);
  if(d->itemState&ODS_FOCUS){RECT line=r;line.left+=15;line.right-=15;line.top=line.bottom-4;line.bottom-=2;rounded(dc,line,ACCENT,3);}
  if(bmp){BitBlt(d->hDC,d->rcItem.left,d->rcItem.top,width,height,mem,0,0,SRCCOPY);SelectObject(mem,old);DeleteObject(bmp);}if(mem)DeleteDC(mem);return TRUE;}
 case WM_COMMAND:{int id=LOWORD(w);
  if(id==SET_MINUS&&pendingPrefs.fontSize>12)pendingPrefs.fontSize--;else if(id==SET_PLUS&&pendingPrefs.fontSize<24)pendingPrefs.fontSize++;
  else if(id>=SET_FACE0&&id<=SET_FACE2)pendingPrefs.fontFace=id-SET_FACE0;
  else if(id>=SET_SPACE0&&id<=SET_SPACE2)pendingPrefs.spacing=id-SET_SPACE0;
  else if(id==SET_DELAY0)pendingPrefs.saveDelay=700;else if(id==SET_DELAY1)pendingPrefs.saveDelay=2000;
  else if(id==SET_RESET)pendingPrefs=(Preferences){14,0,1,700};
  else if(id==SET_CANCEL){DestroyWindow(h);return 0;}
  else if(id==SET_APPLY){if(!savePreferences(pendingPrefs)){MessageBoxW(h,L"Не удалось сохранить настройки.",L"Editor 1",MB_ICONERROR);return 0;}settingsApplied=1;DestroyWindow(h);return 0;}
  refreshSettings(h);return 0;}
 case WM_CLOSE:DestroyWindow(h);return 0;
 case WM_DESTROY:settingsDone=1;for(int i=0;i<13;i++){settingsHover[i].active=0;settingsButtons[i]=NULL;}return 0;
 }return DefWindowProcW(h,m,w,l);
}
static void openSettings(void){
 if(!saveNote(1))return;pendingPrefs=prefs;settingsApplied=settingsDone=0;
 WNDCLASSW wc={0};wc.lpfnWndProc=SettingsProc;wc.hInstance=GetModuleHandleW(NULL);wc.lpszClassName=L"Editor1Settings";wc.hCursor=LoadCursor(NULL,IDC_ARROW);wc.hbrBackground=bgBrush;RegisterClassW(&wc);
 RECT r;GetWindowRect(window,&r);int width=535,height=445;HWND dialog=CreateWindowExW(WS_EX_DLGMODALFRAME,L"Editor1Settings",L"Настройки — Editor 1",WS_CAPTION|WS_SYSMENU|WS_POPUP|WS_CLIPCHILDREN,r.left+(r.right-r.left-width)/2,r.top+(r.bottom-r.top-height)/2,width,height,window,NULL,wc.hInstance,NULL);
 if(!dialog){error(L"Не удалось открыть настройки.");return;}EnableWindow(window,FALSE);ShowWindow(dialog,SW_SHOW);SetForegroundWindow(dialog);SetFocus(settingsButtons[0]);MSG msg;
 while(!settingsDone&&GetMessageW(&msg,NULL,0,0)>0){if(msg.message==WM_KEYDOWN&&msg.wParam==VK_ESCAPE){SendMessageW(dialog,WM_COMMAND,SET_CANCEL,0);continue;}if(msg.message==WM_KEYDOWN&&msg.wParam==VK_RETURN){HWND focus=GetFocus();int id=GetDlgCtrlID(focus);SendMessageW(dialog,WM_COMMAND,id>=SET_MINUS&&id<=SET_APPLY?id:SET_APPLY,0);continue;}if(!IsDialogMessageW(dialog,&msg)){TranslateMessage(&msg);DispatchMessageW(&msg);}}
 EnableWindow(window,TRUE);SetForegroundWindow(window);if(!settingsApplied)return;prefs=pendingPrefs;
 if(reading)showContent();else{CHARRANGE selected;POINT scroll={0};SendMessageW(editor,EM_EXGETSEL,0,(LPARAM)&selected);SendMessageW(editor,EM_GETSCROLLPOS,0,(LPARAM)&scroll);loading=1;SendMessageW(editor,WM_SETREDRAW,FALSE,0);styleRange(0,-1,TEXT,prefs.fontSize,0);applyDocumentStyle();SendMessageW(editor,EM_EXSETSEL,0,(LPARAM)&selected);SendMessageW(editor,EM_SETSCROLLPOS,0,(LPARAM)&scroll);SendMessageW(editor,WM_SETREDRAW,TRUE,0);InvalidateRect(editor,NULL,TRUE);loading=0;SetFocus(editor);}
}
static LRESULT CALLBACK WindowProc(HWND h,UINT m,WPARAM w,LPARAM l) {
 switch(m){
 case WM_CREATE: {
  window=h; HINSTANCE instance=GetModuleHandleW(NULL); LoadLibraryW(L"Msftedit.dll");
  darkTitle(h);
  ui=CreateFontW(-14,0,0,0,FW_NORMAL,FALSE,FALSE,FALSE,DEFAULT_CHARSET,0,0,CLEARTYPE_QUALITY,0,L"Segoe UI");uiSmall=CreateFontW(-13,0,0,0,FW_NORMAL,FALSE,FALSE,FALSE,DEFAULT_CHARSET,0,0,CLEARTYPE_QUALITY,0,L"Segoe UI");uiBold=CreateFontW(-13,0,0,0,FW_SEMIBOLD,FALSE,FALSE,FALSE,DEFAULT_CHARSET,0,0,CLEARTYPE_QUALITY,0,L"Segoe UI");bodyFont=CreateFontW(-17,0,0,0,FW_NORMAL,FALSE,FALSE,FALSE,DEFAULT_CHARSET,0,0,CLEARTYPE_QUALITY,0,L"Segoe UI");titleFont=CreateFontW(-30,0,0,0,FW_SEMIBOLD,FALSE,FALSE,FALSE,DEFAULT_CHARSET,0,0,CLEARTYPE_QUALITY,0,L"Segoe UI");brandFont=CreateFontW(-17,0,0,0,FW_SEMIBOLD,FALSE,FALSE,FALSE,DEFAULT_CHARSET,0,0,CLEARTYPE_QUALITY,0,L"Segoe UI");
  bgBrush=CreateSolidBrush(BG);sideBrush=CreateSolidBrush(SIDE);inputBrush=CreateSolidBrush(RGB(35,43,59));
  search=CreateWindowExW(0,L"EDIT",L"",WS_CHILD|WS_VISIBLE|ES_AUTOHSCROLL,0,0,0,0,h,(HMENU)ID_SEARCH,instance,NULL);SendMessageW(search,WM_SETFONT,(WPARAM)uiSmall,TRUE);oldSearchProc=(WNDPROC)SetWindowLongPtrW(search,GWLP_WNDPROC,(LONG_PTR)searchProc);
  list=CreateWindowExW(0,L"LISTBOX",L"",WS_CHILD|WS_VISIBLE|WS_VSCROLL|LBS_NOTIFY|LBS_OWNERDRAWFIXED|LBS_HASSTRINGS|LBS_NOINTEGRALHEIGHT|LBS_SORT,0,0,0,0,h,(HMENU)ID_LIST,instance,NULL);SendMessageW(list,LB_SETITEMHEIGHT,0,37);oldListProc=(WNDPROC)SetWindowLongPtrW(list,GWLP_WNDPROC,(LONG_PTR)listProc);
  const wchar_t *labels[]={L"Новая заметка",L"Открыть",L"Экспорт",L"Название",L"Удалить"};int ids[]={ID_NEW,ID_OPEN,ID_SAVE,ID_RENAME,ID_DELETE};for(int i=0;i<5;i++){buttons[i]=CreateWindowW(L"BUTTON",labels[i],WS_CHILD|WS_VISIBLE|BS_OWNERDRAW,0,0,0,0,h,(HMENU)(INT_PTR)ids[i],instance,NULL);SendMessageW(buttons[i],WM_SETFONT,(WPARAM)uiSmall,TRUE);SetWindowLongPtrW(buttons[i],GWLP_USERDATA,(LONG_PTR)i);WNDPROC original=(WNDPROC)SetWindowLongPtrW(buttons[i],GWLP_WNDPROC,(LONG_PTR)buttonProc);if(i==0)oldButtonProc=original;}
  buttons[6]=CreateWindowW(L"BUTTON",L"Настройки",WS_CHILD|WS_VISIBLE|BS_OWNERDRAW,0,0,0,0,h,(HMENU)ID_SETTINGS,instance,NULL);
  buttons[7]=CreateWindowW(L"BUTTON",L"Копия",WS_CHILD|WS_VISIBLE|BS_OWNERDRAW,0,0,0,0,h,(HMENU)ID_DUPLICATE,instance,NULL);
  for(int i=6;i<8;i++){SendMessageW(buttons[i],WM_SETFONT,(WPARAM)uiSmall,TRUE);SetWindowLongPtrW(buttons[i],GWLP_USERDATA,(LONG_PTR)i);SetWindowLongPtrW(buttons[i],GWLP_WNDPROC,(LONG_PTR)buttonProc);}
  WNDCLASSW modeClass={0};modeClass.lpfnWndProc=ModeSwitchProc;modeClass.hInstance=instance;modeClass.lpszClassName=L"Editor1ModeSwitch";modeClass.hCursor=LoadCursor(NULL,IDC_HAND);RegisterClassW(&modeClass);
  buttons[5]=CreateWindowW(modeClass.lpszClassName,L"Редактор / Чтение",WS_CHILD|WS_VISIBLE|WS_TABSTOP,0,0,0,0,h,NULL,instance,NULL);
  if(!buttons[5]){error(L"Не удалось создать переключатель режима.");return -1;}
  WNDCLASSW formatClass={0};formatClass.lpfnWndProc=FormatBarProc;formatClass.hInstance=instance;formatClass.lpszClassName=L"Editor1FormatBar";formatClass.hCursor=LoadCursor(NULL,IDC_HAND);RegisterClassW(&formatClass);
  formatBar=CreateWindowW(formatClass.lpszClassName,L"Форматирование Markdown",WS_CHILD|WS_VISIBLE|WS_TABSTOP,0,0,0,0,h,NULL,instance,NULL);if(!formatBar){error(L"Не удалось создать панель форматирования.");return -1;}
  editor=CreateWindowExW(0,MSFTEDIT_CLASS,L"",WS_CHILD|WS_VISIBLE|WS_VSCROLL|ES_MULTILINE|ES_AUTOVSCROLL|ES_NOHIDESEL,0,0,0,0,h,(HMENU)ID_EDITOR,instance,NULL);if(!editor){error(L"Не удалось запустить компонент редактирования Windows.");return -1;}SendMessageW(editor,WM_SETFONT,(WPARAM)bodyFont,TRUE);SendMessageW(editor,EM_SETBKGNDCOLOR,0,BG);SendMessageW(editor,EM_EXLIMITTEXT,0,MAX_NOTE);SendMessageW(editor,EM_SETEVENTMASK,0,ENM_CHANGE);
  wchar_t appdata[MAX_PATH];if(FAILED(SHGetFolderPathW(h,CSIDL_APPDATA|CSIDL_FLAG_CREATE,NULL,SHGFP_TYPE_CURRENT,appdata))){error(L"Не удалось найти папку данных пользователя.");return -1;}swprintf(directory,MAX_PATH,L"%ls\\Editor1",appdata);if(!CreateDirectoryW(directory,NULL)&&GetLastError()!=ERROR_ALREADY_EXISTS){error(L"Не удалось создать папку заметок.");return -1;}
  loadPreferences();
  text=duptext(L"");refreshList();int count=(int)SendMessageW(list,LB_GETCOUNT,0,0);if(count>0){wchar_t first[MAX_PATH];SendMessageW(list,LB_GETTEXT,0,(LPARAM)first);loadNote(first);}else{wchar_t path[MAX_PATH];pathFor(L"Мой дом",path,MAX_PATH);if(writeUtf8(path,welcome))loadNote(L"Мой дом");else error(L"Не удалось создать первую заметку.");}
  layout(h);return 0; }
 case WM_SIZE:layout(h);InvalidateRect(h,NULL,TRUE);return 0;
 case WM_GETMINMAXINFO:{MINMAXINFO *i=(MINMAXINFO*)l;i->ptMinTrackSize.x=850;i->ptMinTrackSize.y=600;return 0;}
 case WM_ERASEBKGND:return 1;
 case WM_PAINT:{PAINTSTRUCT ps;HDC dc=BeginPaint(h,&ps);RECT r;GetClientRect(h,&r);fill(dc,r,BG);RECT sidebar={0,0,256,r.bottom};fill(dc,sidebar,SIDE);RECT line={255,0,256,r.bottom};fill(dc,line,BORDER);RECT top={256,69,r.right,70};fill(dc,top,BORDER);RECT foot={256,r.bottom-42,r.right,r.bottom-41};fill(dc,foot,BORDER);
  RECT logo={19,18,53,52};glassSurface(dc,logo,RGB(61,100,164),SIDE,12,0);icon(dc,ICON_EDITOR_MARK,26,25,RGB(244,249,255));
  RECT brand={61,18,205,39};drawText(dc,L"editor 1",brand,brandFont,TEXT,DT_LEFT|DT_SINGLELINE|DT_VCENTER);RECT under={62,42,224,61};drawText(dc,L"пространство для мыслей",under,uiSmall,MUTED,DT_LEFT|DT_SINGLELINE|DT_VCENTER);
  RECT searchBox={17,99,239,139};rounded(dc,searchBox,mixColor(RGB(35,43,59),RGB(50,69,94),(int)(searchAnim.value*100)),32);RECT searchInner={18,100,238,138};rounded(dc,searchInner,RGB(35,43,59),30);icon(dc,ICON_SEARCH,27,109,MUTED);
  RECT lib={20,228,230,251};drawText(dc,L"ВАШИ ЗАМЕТКИ",lib,uiBold,MUTED,DT_LEFT|DT_SINGLELINE|DT_VCENTER);wchar_t count[32];swprintf(count,32,L"%d",(int)SendMessageW(list,LB_GETCOUNT,0,0));RECT countRect={210,228,233,251};drawText(dc,count,countRect,uiSmall,MUTED,DT_RIGHT|DT_SINGLELINE|DT_VCENTER);
  RECT bottomLine={20,r.bottom-75,236,r.bottom-74};fill(dc,bottomLine,BORDER);RECT bottom={20,r.bottom-61,230,r.bottom-42};drawText(dc,L"●  Личное пространство",bottom,uiBold,TEAL,DT_LEFT|DT_SINGLELINE|DT_VCENTER);RECT bottom2={32,r.bottom-40,230,r.bottom-23};drawText(dc,L"Хранится на устройстве",bottom2,uiSmall,MUTED,DT_LEFT|DT_SINGLELINE|DT_VCENTER);
  RECT bread={281,23,r.right-445,48};drawText(dc,L"Моё пространство   /   Заметки",bread,uiSmall,MUTED,DT_SINGLELINE|DT_VCENTER|DT_END_ELLIPSIS);
  int x=256+((r.right-256-760)>0?(r.right-256-760)/2:34);RECT label={x,100,r.right-20,121};drawText(dc,L"ВАША ЗАМЕТКА   ·   MARKDOWN",label,uiBold,ACCENT,DT_LEFT|DT_SINGLELINE|DT_VCENTER);
  RECT title={x,131,r.right-40,177};drawText(dc,current[0]?current:L"Нет заметок",title,titleFont,TEXT,DT_LEFT|DT_SINGLELINE|DT_VCENTER|DT_END_ELLIPSIS);
  RECT sub={x,181,r.right-20,200};drawText(dc,reading?L"Режим чтения · переключитесь в редактор, чтобы изменить текст":L"Выделите текст и используйте панель форматирования",sub,uiSmall,MUTED,DT_LEFT|DT_SINGLELINE|DT_VCENTER);
  RECT status={280,r.bottom-33,r.right-20,r.bottom-9};drawText(dc,dirty?L"●  Сохраняем изменения...":L"●  Все изменения сохранены",status,uiSmall,dirty?ACCENT:TEAL,DT_SINGLELINE|DT_VCENTER);wchar_t stats[100];swprintf(stats,100,L"Слов: %lu   ·   Знаков: %lu",(unsigned long)wordCount,(unsigned long)charCount);RECT statusRight={r.right-260,r.bottom-33,r.right-25,r.bottom-9};drawText(dc,stats,statusRight,uiSmall,MUTED,DT_RIGHT|DT_SINGLELINE|DT_VCENTER);EndPaint(h,&ps);return 0;}
 case WM_DRAWITEM:drawBufferedItem((DRAWITEMSTRUCT*)l);return TRUE;
 case WM_CTLCOLOREDIT:case WM_CTLCOLORLISTBOX:{HDC dc=(HDC)w;SetTextColor(dc,TEXT);if((HWND)l==search){SetBkColor(dc,RGB(35,43,59));return (LRESULT)inputBrush;}SetBkColor(dc,SIDE);return (LRESULT)sideBrush;}
 case WM_COMMAND: {
  int id=LOWORD(w),code=HIWORD(w);
  if(id==ID_SEARCH&&code==EN_CHANGE){KillTimer(h,4);SetTimer(h,4,250,NULL);return 0;}
  if(id==ID_EDITOR&&code==EN_CHANGE&&!loading&&!reading&&current[0]){dirty=1;KillTimer(h,1);SetTimer(h,1,(UINT)prefs.saveDelay,NULL);KillTimer(h,3);SetTimer(h,3,250,NULL);InvalidateRect(h,NULL,FALSE);return 0;}
  if(id==ID_LIST&&code==LBN_SELCHANGE){int idx=(int)SendMessageW(list,LB_GETCURSEL,0,0);if(idx!=LB_ERR){wchar_t name[MAX_PATH];SendMessageW(list,LB_GETTEXT,idx,(LPARAM)name);if(wcscmp(name,current)&&!loadNote(name))refreshList();}return 0;}
  if(id>=ID_FMT_BASE&&id<ID_FMT_BASE+7){if(GetFocus()==editor)formatSelected(id-ID_FMT_BASE);return 0;}
  if(id==ID_DEV_LOG&&devMode){if(devWindow){ShowWindow(devWindow,SW_SHOW);SetForegroundWindow(devWindow);}return 0;}
  if(id==ID_FIND_NOTES){SetFocus(search);SendMessageW(search,EM_SETSEL,0,-1);return 0;}
  if(id==ID_SETTINGS){openSettings();return 0;}if(id==ID_DUPLICATE){duplicateNote();return 0;}
  if(id==ID_NEW){createNoteWith(L"");return 0;}if(id==ID_OPEN){importNote();return 0;}if(id==ID_SAVE){exportNote();return 0;}if(id==ID_RENAME){renameNote();return 0;}if(id==ID_DELETE){deleteNote();return 0;}
  if(id==ID_EDIT&&reading){devLog(L"Mode changed: edit");reading=0;showContent();SetFocus(editor);return 0;}if(id==ID_READ&&!reading){if(!saveNote(1))return 0;devLog(L"Mode changed: read");reading=1;showContent();return 0;}break;}
 case WM_TIMER:
  if(w==1){KillTimer(h,1);saveNote(1);return 0;}
  if(w==3){KillTimer(h,3);measureText();return 0;}
  if(w==4){KillTimer(h,4);refreshList();return 0;}
  if(w==2){
   ULONGLONG now=GetTickCount64();int moving=0;
   for(int i=0;i<8;i++){if(i==5)continue;if(animation_tick(&hoverAnim[i],now)){hoverAmount[i]=(int)(hoverAnim[i].value*100);InvalidateRect(buttons[i],NULL,FALSE);}moving|=hoverAnim[i].active;}
   if(animation_tick(&modeAnim,now))InvalidateRect(buttons[5],NULL,FALSE);moving|=modeAnim.active;
   if(animation_tick(&formatAnim,now)&&formatBar)InvalidateRect(formatBar,NULL,FALSE);moving|=formatAnim.active;
   for(int i=0;i<13;i++){if(animation_tick(&settingsHover[i],now)&&settingsButtons[i])InvalidateRect(settingsButtons[i],NULL,FALSE);moving|=settingsHover[i].active;}
   if(animation_tick(&modeHoverAnim,now))InvalidateRect(buttons[5],NULL,FALSE);moving|=modeHoverAnim.active;
   for(int i=0;i<2;i++){if(animation_tick(&promptAnim[i],now)&&promptButtons[i])InvalidateRect(promptButtons[i],NULL,FALSE);moving|=promptAnim[i].active;}
   if(animation_tick(&listHoverAnim,now))InvalidateRect(list,NULL,FALSE);moving|=listHoverAnim.active;
   if(animation_tick(&selectionAnim,now))InvalidateRect(list,NULL,FALSE);moving|=selectionAnim.active;
   if(animation_tick(&searchAnim,now)){RECT area={17,99,239,139};InvalidateRect(h,&area,FALSE);}moving|=searchAnim.active;
   if(!moving)KillTimer(h,2);return 0;
  }break;
 case WM_CLOSE:devLog(L"Close requested");if(saveNote(1))DestroyWindow(h);return 0;
 case WM_DESTROY:devLog(L"Window closed");free(text);DeleteObject(ui);DeleteObject(uiSmall);DeleteObject(uiBold);DeleteObject(bodyFont);DeleteObject(titleFont);DeleteObject(brandFont);DeleteObject(bgBrush);DeleteObject(sideBrush);DeleteObject(inputBrush);PostQuitMessage(0);return 0;
 }return DefWindowProcW(h,m,w,l);
}
static LRESULT CALLBACK DevWindowProc(HWND h,UINT m,WPARAM w,LPARAM l){
 switch(m){
 case WM_CREATE:
  darkTitle(h);devEdit=CreateWindowExW(0,L"EDIT",devBuffer,WS_CHILD|WS_VISIBLE|WS_VSCROLL|ES_MULTILINE|ES_AUTOVSCROLL|ES_READONLY,12,12,500,300,h,NULL,GetModuleHandleW(NULL),NULL);
  if(devEdit){SendMessageW(devEdit,WM_SETFONT,(WPARAM)ui,TRUE);SendMessageW(devEdit,EM_SETLIMITTEXT,32767,0);SendMessageW(devEdit,EM_SETSEL,(WPARAM)-1,(LPARAM)-1);SendMessageW(devEdit,EM_SCROLLCARET,0,0);}return 0;
 case WM_SIZE:{RECT r;GetClientRect(h,&r);if(devEdit)MoveWindow(devEdit,12,12,r.right>24?r.right-24:1,r.bottom>24?r.bottom-24:1,TRUE);return 0;}
 case WM_CTLCOLOREDIT:case WM_CTLCOLORSTATIC:SetTextColor((HDC)w,TEXT);SetBkColor((HDC)w,BG);return (LRESULT)bgBrush;
 case WM_CLOSE:ShowWindow(h,SW_HIDE);return 0;
 case WM_DESTROY:devWindow=NULL;devEdit=NULL;return 0;
 }return DefWindowProcW(h,m,w,l);
}
static void openDevWindow(HINSTANCE instance,HWND owner){
 if(!devMode)return;WNDCLASSW wc={0};wc.lpfnWndProc=DevWindowProc;wc.hInstance=instance;wc.lpszClassName=L"Editor1DevLog";wc.hCursor=LoadCursor(NULL,IDC_ARROW);wc.hbrBackground=bgBrush;RegisterClassW(&wc);
 devWindow=CreateWindowW(wc.lpszClassName,L"Editor 1 — Dev Log (Ctrl+Shift+D)",WS_OVERLAPPEDWINDOW|WS_CLIPCHILDREN,CW_USEDEFAULT,CW_USEDEFAULT,720,400,owner,NULL,instance,NULL);
 if(devWindow)ShowWindow(devWindow,SW_SHOW);else devLog(L"WARNING: cannot create log window, Windows error=%lu",GetLastError());
}
int WINAPI wWinMain(HINSTANCE instance,HINSTANCE prev,PWSTR cmd,int show){
 (void)prev;(void)cmd;initDevMode();
 WNDCLASSW wc={0};wc.lpfnWndProc=WindowProc;wc.hInstance=instance;wc.lpszClassName=L"Editor1Main";wc.hCursor=LoadCursor(NULL,IDC_ARROW);wc.hIcon=LoadIcon(NULL,IDI_APPLICATION);RegisterClassW(&wc);
 HWND h=CreateWindowW(wc.lpszClassName,devMode?L"Editor 1 — версия 1.6 [DEV]":L"Editor 1 — версия 1.6",WS_OVERLAPPEDWINDOW|WS_CLIPCHILDREN,CW_USEDEFAULT,CW_USEDEFAULT,1280,850,NULL,NULL,instance,NULL);
 if(!h){devLog(L"ERROR: failed to create main window, Windows error=%lu",GetLastError());if(devFile!=INVALID_HANDLE_VALUE)CloseHandle(devFile);return 1;}
 ShowWindow(h,show);UpdateWindow(h);openDevWindow(instance,h);devLog(L"Main window ready; Ctrl+Shift+D reopens the log window");
 ACCEL keys[]={{FVIRTKEY|FCONTROL,'N',ID_NEW},{FVIRTKEY|FCONTROL,'O',ID_OPEN},{FVIRTKEY|FCONTROL,'S',ID_SAVE},{FVIRTKEY,VK_F2,ID_RENAME},{FVIRTKEY|FCONTROL|FSHIFT,'D',ID_DEV_LOG},{FVIRTKEY|FCONTROL,'B',ID_FMT_BASE},{FVIRTKEY|FCONTROL,'I',ID_FMT_BASE+1},{FVIRTKEY|FCONTROL,'D',ID_DUPLICATE},{FVIRTKEY|FCONTROL,'F',ID_FIND_NOTES},{FVIRTKEY|FCONTROL,'K',ID_FIND_NOTES}};HACCEL accel=CreateAcceleratorTableW(keys,10);MSG msg;
 while(GetMessageW(&msg,NULL,0,0)>0){if(!TranslateAcceleratorW(h,accel,&msg)){TranslateMessage(&msg);DispatchMessageW(&msg);}}
 DestroyAcceleratorTable(accel);devLog(L"Exit code %d",(int)msg.wParam);if(devFile!=INVALID_HANDLE_VALUE)CloseHandle(devFile);return (int)msg.wParam;
}
