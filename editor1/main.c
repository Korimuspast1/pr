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
#define MAX_NOTE (4 * 1024 * 1024)

static HWND window, list, search, editor, buttons[7], promptInput;
static HFONT ui, uiSmall, uiBold, bodyFont, titleFont, brandFont;
static WNDPROC oldButtonProc, oldSearchProc;
static int hovered[7], hoverAmount[7];
static HBRUSH bgBrush, sideBrush, inputBrush;
static wchar_t directory[MAX_PATH], current[MAX_PATH], *text;
static int dirty, loading, reading = 1, promptDone, promptAccepted;
static wchar_t promptValue[180];
static const COLORREF BG = RGB(28,28,33), SIDE = RGB(22,22,27), BORDER = RGB(45,45,53), TEXT = RGB(232,229,237), MUTED = RGB(143,140,153), ACCENT = RGB(177,157,224), TEAL = RGB(137,185,167);
static const wchar_t *welcome = L"# Мой дом\r\n\r\n## Пространство для мыслей\r\n\r\nДобро пожаловать в Editor 1. Здесь можно собирать идеи, вести заметки и сосредоточиться на том, что действительно важно.\r\n\r\n## Заметки\r\n\r\n- Создайте новую заметку в боковой панели\r\n- Переключитесь в режим редактирования, чтобы писать\r\n- Откройте или экспортируйте Markdown и текстовые файлы\r\n\r\n## Идеи\r\n\r\nВаше пространство — ваши правила. Начните с одной мысли.\r\n\r\n> Всё большое начинается с маленькой заметки.\r\n";

static void error(const wchar_t *message) { MessageBoxW(window, message, L"Editor 1", MB_ICONERROR); }
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
 int size=WideCharToMultiByte(CP_UTF8,0,content,-1,NULL,0,NULL,NULL); if(!size || size-1>MAX_NOTE)return 0;
 char *bytes=malloc(size); if(!bytes)return 0; WideCharToMultiByte(CP_UTF8,0,content,-1,bytes,size,NULL,NULL);
 wchar_t temp[MAX_PATH]; if(wcslen(path)+5>=MAX_PATH){free(bytes);return 0;} swprintf(temp,MAX_PATH,L"%ls.tmp",path);
 HANDLE h=CreateFileW(temp,GENERIC_WRITE,0,NULL,CREATE_ALWAYS,FILE_ATTRIBUTE_NORMAL,NULL); if(h==INVALID_HANDLE_VALUE){free(bytes);return 0;}
 DWORD written=0; int ok=WriteFile(h,bytes,(DWORD)(size-1),&written,NULL) && written==(DWORD)(size-1); if(ok) ok=FlushFileBuffers(h); CloseHandle(h); free(bytes);
 if(ok)ok=MoveFileExW(temp,path,MOVEFILE_REPLACE_EXISTING|MOVEFILE_WRITE_THROUGH); if(!ok)DeleteFileW(temp); return ok;
}
static wchar_t *readUtf8(const wchar_t *path) {
 HANDLE h=CreateFileW(path,GENERIC_READ,FILE_SHARE_READ,NULL,OPEN_EXISTING,0,NULL); if(h==INVALID_HANDLE_VALUE)return NULL;
 LARGE_INTEGER size; if(!GetFileSizeEx(h,&size)||size.QuadPart>MAX_NOTE||size.QuadPart<0){CloseHandle(h);return NULL;}
 DWORD got=0; char *buf=malloc((size_t)size.QuadPart+1); if(!buf){CloseHandle(h);return NULL;}
 if(!ReadFile(h,buf,(DWORD)size.QuadPart,&got,NULL)){free(buf);CloseHandle(h);return NULL;} CloseHandle(h); buf[got]=0;
 char *start=buf; if(got>=3&&(unsigned char)buf[0]==0xef&&(unsigned char)buf[1]==0xbb&&(unsigned char)buf[2]==0xbf)start+=3;
 int chars=MultiByteToWideChar(CP_UTF8,MB_ERR_INVALID_CHARS,start,-1,NULL,0); if(!chars){free(buf);return NULL;}
 wchar_t *result=malloc(chars*sizeof(wchar_t)); if(result)MultiByteToWideChar(CP_UTF8,0,start,-1,result,chars); free(buf); return result;
}
static void capture(void) { if(reading)return; int n=GetWindowTextLengthW(editor); wchar_t *next=malloc(((size_t)n+1)*sizeof(wchar_t)); if(next){GetWindowTextW(editor,next,n+1); free(text);text=next;} }
static int saveNote(int showError) { if(!current[0] || !dirty)return 1; capture(); wchar_t path[MAX_PATH]; pathFor(current,path,MAX_PATH); if(!writeUtf8(path,text?text:L"")){ if(showError)error(L"Не удалось сохранить заметку. Проверьте доступ к папке документов.");return 0;} dirty=0; InvalidateRect(window,NULL,FALSE); return 1; }
static void refreshList(void) {
 wchar_t filter[200]; GetWindowTextW(search,filter,200); CharLowerBuffW(filter,(DWORD)wcslen(filter));
 SendMessageW(list,LB_RESETCONTENT,0,0); wchar_t pattern[MAX_PATH]; swprintf(pattern,MAX_PATH,L"%ls\\*.md",directory); WIN32_FIND_DATAW data; HANDLE h=FindFirstFileW(pattern,&data);
 if(h!=INVALID_HANDLE_VALUE){do {if(data.dwFileAttributes&FILE_ATTRIBUTE_DIRECTORY)continue; size_t n=wcslen(data.cFileName);if(n<3)continue;data.cFileName[n-3]=0;wchar_t lower[MAX_PATH];wcsncpy(lower,data.cFileName,MAX_PATH-1);lower[MAX_PATH-1]=0;CharLowerBuffW(lower,(DWORD)wcslen(lower));if(*filter&&!wcsstr(lower,filter))continue;int i=(int)SendMessageW(list,LB_ADDSTRING,0,(LPARAM)data.cFileName);if(!_wcsicmp(current,data.cFileName))SendMessageW(list,LB_SETCURSEL,i,0);} while(FindNextFileW(h,&data)); FindClose(h);}
 InvalidateRect(window,NULL,FALSE);
}
static void styleRange(LONG start,LONG end,COLORREF color,int size,int bold) {
 CHARRANGE range={start,end}; SendMessageW(editor,EM_EXSETSEL,0,(LPARAM)&range); CHARFORMAT2W f={0};f.cbSize=sizeof(f);f.dwMask=CFM_COLOR|CFM_SIZE|CFM_BOLD;f.crTextColor=color;f.yHeight=size*20;f.dwEffects=bold?CFE_BOLD:0;SendMessageW(editor,EM_SETCHARFORMAT,SCF_SELECTION,(LPARAM)&f);
}
static void showContent(void) {
 loading=1; EnableWindow(editor,current[0]!=0); for(int i=2;i<7;i++)EnableWindow(buttons[i],current[0]!=0); SendMessageW(editor,WM_SETREDRAW,FALSE,0); SendMessageW(editor,EM_SETREADONLY,FALSE,0);
 if (!reading) { SetWindowTextW(editor,text?text:L""); styleRange(0,-1,TEXT,14,0); }
 else {
  // Render a simple, readable Markdown view without altering the original text.
  SetWindowTextW(editor,L""); const wchar_t *p=text?text:L""; LONG position=0;
  while(*p){const wchar_t *end=wcschr(p,L'\n'); size_t len=end?(size_t)(end-p):wcslen(p); if(len&&p[len-1]==L'\r')len--;
   int level=0;while(level<3&&(size_t)level<len&&p[level]==L'#')level++;size_t skip=(level&&(size_t)level<len&&p[level]==L' ')?(size_t)level+1:0;if(!skip)level=0;
   if(!level&&len>=2&&p[0]==L'>'&&p[1]==L' ')skip=2;
   size_t count=len-skip;wchar_t *line=malloc((count+3)*sizeof(wchar_t));if(!line)break;wmemcpy(line,p+skip,count);line[count]=L'\r';line[count+1]=L'\n';line[count+2]=0;
   SendMessageW(editor,EM_SETSEL,(WPARAM)-1,(LPARAM)-1);SendMessageW(editor,EM_REPLACESEL,FALSE,(LPARAM)line);
   styleRange(position,position+(LONG)count,level==1?RGB(226,206,249):(level?ACCENT:(p[0]==L'>'?MUTED:TEXT)),level==1?23:(level==2?18:(level==3?16:14)),level>0);
   position+=(LONG)count+1;free(line);if(!end)break;p=end+1;
  }
  SendMessageW(editor,EM_SETREADONLY,TRUE,0);
 }
 SendMessageW(editor,EM_SETSEL,0,0); SendMessageW(editor,WM_SETREDRAW,TRUE,0);InvalidateRect(editor,NULL,TRUE); loading=0;InvalidateRect(window,NULL,FALSE);InvalidateRect(buttons[5],NULL,FALSE);InvalidateRect(buttons[6],NULL,FALSE);
}
static int loadNote(const wchar_t *name) { if(!saveNote(1))return 0; wchar_t path[MAX_PATH];pathFor(name,path,MAX_PATH);wchar_t *content=readUtf8(path);if(!content){error(L"Не удалось открыть заметку. Поддерживаются UTF-8 файлы размером до 4 МБ.");return 0;} free(text);text=content;wcsncpy(current,name,MAX_PATH-1);current[MAX_PATH-1]=0;dirty=0;showContent();refreshList();return 1; }
static void createNoteWith(const wchar_t *content) { if(!saveNote(1))return; wchar_t name[128],path[MAX_PATH];for(int i=1;i<10000;i++){swprintf(name,128,L"Новая заметка %d",i);pathFor(name,path,MAX_PATH);if(GetFileAttributesW(path)==INVALID_FILE_ATTRIBUTES)break;} if(!writeUtf8(path,content)){error(L"Не удалось создать заметку.");return;}SetWindowTextW(search,L"");loadNote(name);reading=0;showContent();SetFocus(editor); }
static void importNote(void) { wchar_t file[MAX_PATH]=L"";OPENFILENAMEW of={0};of.lStructSize=sizeof(of);of.hwndOwner=window;of.lpstrFilter=L"Текст и Markdown\0*.md;*.txt;*.markdown\0Все файлы\0*.*\0";of.lpstrFile=file;of.nMaxFile=MAX_PATH;of.Flags=OFN_FILEMUSTEXIST|OFN_PATHMUSTEXIST|OFN_NOCHANGEDIR;if(GetOpenFileNameW(&of)){wchar_t *value=readUtf8(file);if(!value){error(L"Не удалось прочитать файл. Используйте UTF-8 файл размером до 4 МБ.");return;}createNoteWith(value);free(value);} }
static void exportNote(void) { if(!current[0])return;if(!saveNote(1))return;wchar_t file[MAX_PATH];swprintf(file,MAX_PATH,L"%ls.md",current);OPENFILENAMEW of={0};of.lStructSize=sizeof(of);of.hwndOwner=window;of.lpstrFilter=L"Markdown\0*.md\0Текст\0*.txt\0";of.lpstrDefExt=L"md";of.lpstrFile=file;of.nMaxFile=MAX_PATH;of.Flags=OFN_OVERWRITEPROMPT|OFN_PATHMUSTEXIST|OFN_NOCHANGEDIR;if(GetSaveFileNameW(&of)&&!writeUtf8(file,text?text:L""))error(L"Не удалось экспортировать файл."); }
static LRESULT CALLBACK PromptProc(HWND h,UINT m,WPARAM w,LPARAM l) { switch(m) {case WM_CREATE: CreateWindowW(L"STATIC",L"Название заметки",WS_CHILD|WS_VISIBLE,20,18,320,22,h,0,0,0);promptInput=CreateWindowExW(WS_EX_CLIENTEDGE,L"EDIT",promptValue,WS_CHILD|WS_VISIBLE|ES_AUTOHSCROLL,20,45,335,28,h,(HMENU)ID_PROMPT,0,0);SendMessageW(promptInput,WM_SETFONT,(WPARAM)ui,TRUE);SendMessageW(promptInput,EM_LIMITTEXT,120,0);CreateWindowW(L"BUTTON",L"Сохранить",WS_CHILD|WS_VISIBLE|BS_DEFPUSHBUTTON,175,88,85,30,h,(HMENU)ID_OK,0,0);CreateWindowW(L"BUTTON",L"Отмена",WS_CHILD|WS_VISIBLE,270,88,85,30,h,(HMENU)ID_CANCEL,0,0);SetFocus(promptInput);SendMessageW(promptInput,EM_SETSEL,0,-1);return 0;case WM_COMMAND:if(LOWORD(w)==ID_OK){GetWindowTextW(promptInput,promptValue,180);if(!validName(promptValue)){MessageBoxW(h,L"Введите корректное название без символов \\ / : * ? \" < > |.",L"Editor 1",MB_ICONWARNING);return 0;}promptAccepted=1;DestroyWindow(h);return 0;}if(LOWORD(w)==ID_CANCEL){DestroyWindow(h);return 0;}break;case WM_CLOSE:DestroyWindow(h);return 0;case WM_DESTROY:promptDone=1;return 0;}return DefWindowProcW(h,m,w,l); }
static void renameNote(void) { if(!current[0]||!saveNote(1))return;wcscpy(promptValue,current);promptDone=promptAccepted=0;WNDCLASSW wc={0};wc.lpfnWndProc=PromptProc;wc.hInstance=GetModuleHandleW(NULL);wc.lpszClassName=L"Editor1Prompt";wc.hCursor=LoadCursor(NULL,IDC_ARROW);wc.hbrBackground=(HBRUSH)(COLOR_BTNFACE+1);RegisterClassW(&wc);RECT r;GetWindowRect(window,&r);HWND dialog=CreateWindowExW(WS_EX_DLGMODALFRAME,L"Editor1Prompt",L"Переименовать",WS_CAPTION|WS_SYSMENU|WS_POPUP|WS_VISIBLE,r.left+120,r.top+120,395,165,window,NULL,wc.hInstance,NULL);EnableWindow(window,FALSE);MSG msg;while(!promptDone&&GetMessageW(&msg,NULL,0,0)>0){if(!IsDialogMessageW(dialog,&msg)){TranslateMessage(&msg);DispatchMessageW(&msg);}}EnableWindow(window,TRUE);SetForegroundWindow(window);if(!promptAccepted||!wcscmp(promptValue,current))return;wchar_t old[MAX_PATH],next[MAX_PATH];pathFor(current,old,MAX_PATH);pathFor(promptValue,next,MAX_PATH);if(GetFileAttributesW(next)!=INVALID_FILE_ATTRIBUTES){MessageBoxW(window,L"Заметка с таким названием уже существует.",L"Editor 1",MB_ICONWARNING);return;}if(!MoveFileW(old,next)){error(L"Не удалось переименовать заметку.");return;}wcscpy(current,promptValue);refreshList(); }
static void deleteNote(void) { if(!current[0])return;if(MessageBoxW(window,L"Удалить эту заметку? Это действие нельзя отменить.",L"Editor 1",MB_YESNO|MB_ICONWARNING)!=IDYES)return;wchar_t path[MAX_PATH];pathFor(current,path,MAX_PATH);if(!DeleteFileW(path)){error(L"Не удалось удалить заметку.");return;}current[0]=0;dirty=0;free(text);text=duptext(L"");SetWindowTextW(search,L"");refreshList();if(SendMessageW(list,LB_GETCOUNT,0,0)>0){wchar_t name[MAX_PATH];SendMessageW(list,LB_GETTEXT,0,(LPARAM)name);loadNote(name);}else showContent(); }
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
static LRESULT CALLBACK buttonProc(HWND h,UINT message,WPARAM w,LPARAM l){
 int i=(int)GetWindowLongPtrW(h,GWLP_USERDATA);
 if(message==WM_MOUSEMOVE && i>=0 && i<7 && !hovered[i]){hovered[i]=1;TRACKMOUSEEVENT t={sizeof(t),TME_LEAVE,h,0};TrackMouseEvent(&t);SetTimer(window,2,16,NULL);}
 if(message==WM_MOUSELEAVE && i>=0 && i<7){hovered[i]=0;SetTimer(window,2,16,NULL);}
 if(message==WM_SETFOCUS||message==WM_KILLFOCUS)InvalidateRect(h,NULL,FALSE);
 return CallWindowProcW(oldButtonProc,h,message,w,l);
}
static LRESULT CALLBACK searchProc(HWND h,UINT message,WPARAM w,LPARAM l){
 LRESULT result=CallWindowProcW(oldSearchProc,h,message,w,l);
 if(message==WM_PAINT && !GetWindowTextLengthW(h) && GetFocus()!=h){HDC dc=GetDC(h);RECT r;GetClientRect(h,&r);r.left+=2;drawText(dc,L"Поиск заметок...",r,uiSmall,MUTED,DT_SINGLELINE|DT_VCENTER);ReleaseDC(h,dc);}
 if(message==WM_SETFOCUS||message==WM_KILLFOCUS)InvalidateRect(h,NULL,TRUE);
 return result;
}
static void layout(HWND h) {
 RECT r;GetClientRect(h,&r);int w=r.right, height=r.bottom;int side=256;
 MoveWindow(search,47,105,side-74,27,TRUE);MoveWindow(buttons[0],17,156,side-34,42,TRUE);MoveWindow(list,10,262,side-20,height-346,TRUE);
 int right=w-22;MoveWindow(buttons[1],right-326,19,72,32,TRUE);MoveWindow(buttons[2],right-249,19,74,32,TRUE);MoveWindow(buttons[3],right-170,19,80,32,TRUE);MoveWindow(buttons[4],right-85,19,85,32,TRUE);
 int content=side+((w-side-760)>0?(w-side-760)/2:34), contentWidth=w-content-38;if(contentWidth>760)contentWidth=760;if(contentWidth<220)contentWidth=220;
 MoveWindow(buttons[5],content+4,219,110,34,TRUE);MoveWindow(buttons[6],content+118,219,100,34,TRUE);MoveWindow(editor,content,283,contentWidth,height-333,TRUE);
}
static void drawItem(DRAWITEMSTRUCT *d) {
 if(d->CtlType==ODT_LISTBOX){if(d->itemID==(UINT)-1)return;wchar_t name[MAX_PATH];SendMessageW(list,LB_GETTEXT,d->itemID,(LPARAM)name);int selected=d->itemState&ODS_SELECTED;fill(d->hDC,d->rcItem,SIDE);RECT pill=d->rcItem;InflateRect(&pill,-2,-2);if(selected)rounded(d->hDC,pill,RGB(51,46,66),10);RECT icon=d->rcItem;icon.left+=13;icon.right=icon.left+20;drawText(d->hDC,L"▤",icon,ui,selected?ACCENT:MUTED,DT_SINGLELINE|DT_VCENTER);RECT label=d->rcItem;label.left+=38;label.right-=8;drawText(d->hDC,name,label,selected?uiBold:uiSmall,selected?TEXT:RGB(169,166,177),DT_SINGLELINE|DT_VCENTER|DT_END_ELLIPSIS);return;}
 int id=d->CtlID;int index=-1;for(int i=0;i<7;i++)if(buttons[i]==d->hwndItem)index=i;
 int active=(id==ID_EDIT&&!reading)||(id==ID_READ&&reading);int hover=index>=0?hoverAmount[index]:0;
 COLORREF background=id==ID_NEW?SIDE:((id==ID_EDIT||id==ID_READ)?RGB(36,35,43):BG);fill(d->hDC,d->rcItem,background);
 COLORREF base=id==ID_NEW?RGB(105,82,154):(active?RGB(68,62,83):background);COLORREF over=id==ID_NEW?RGB(132,105,184):RGB(63,59,73);
 COLORREF back=mixColor(base,over,hover);if(d->itemState&ODS_SELECTED)back=mixColor(back,TEXT,16);
 RECT pill=d->rcItem;InflateRect(&pill,-1,-1);rounded(d->hDC,pill,back,12);
 wchar_t label[80];GetWindowTextW(d->hwndItem,label,80);COLORREF color=(d->itemState&ODS_DISABLED)?RGB(91,89,99):(id==ID_NEW||active?TEXT:RGB(181,178,189));
 drawText(d->hDC,label,d->rcItem,(id==ID_NEW||active)?uiBold:uiSmall,color,DT_SINGLELINE|DT_CENTER|DT_VCENTER);
 if(d->itemState&ODS_FOCUS){RECT line=d->rcItem;line.left+=13;line.right-=13;line.top=line.bottom-4;line.bottom-=2;rounded(d->hDC,line,ACCENT,3);}
}
static LRESULT CALLBACK WindowProc(HWND h,UINT m,WPARAM w,LPARAM l) {
 switch(m){
 case WM_CREATE: {
  window=h; HINSTANCE instance=GetModuleHandleW(NULL); LoadLibraryW(L"Msftedit.dll");
  darkTitle(h);
  ui=CreateFontW(-14,0,0,0,FW_NORMAL,FALSE,FALSE,FALSE,DEFAULT_CHARSET,0,0,CLEARTYPE_QUALITY,0,L"Segoe UI");uiSmall=CreateFontW(-13,0,0,0,FW_NORMAL,FALSE,FALSE,FALSE,DEFAULT_CHARSET,0,0,CLEARTYPE_QUALITY,0,L"Segoe UI");uiBold=CreateFontW(-13,0,0,0,FW_SEMIBOLD,FALSE,FALSE,FALSE,DEFAULT_CHARSET,0,0,CLEARTYPE_QUALITY,0,L"Segoe UI");bodyFont=CreateFontW(-17,0,0,0,FW_NORMAL,FALSE,FALSE,FALSE,DEFAULT_CHARSET,0,0,CLEARTYPE_QUALITY,0,L"Georgia");titleFont=CreateFontW(-30,0,0,0,FW_SEMIBOLD,FALSE,FALSE,FALSE,DEFAULT_CHARSET,0,0,CLEARTYPE_QUALITY,0,L"Segoe UI");brandFont=CreateFontW(-17,0,0,0,FW_SEMIBOLD,FALSE,FALSE,FALSE,DEFAULT_CHARSET,0,0,CLEARTYPE_QUALITY,0,L"Segoe UI");
  bgBrush=CreateSolidBrush(BG);sideBrush=CreateSolidBrush(SIDE);inputBrush=CreateSolidBrush(RGB(36,36,44));
  search=CreateWindowExW(0,L"EDIT",L"",WS_CHILD|WS_VISIBLE|ES_AUTOHSCROLL,0,0,0,0,h,(HMENU)ID_SEARCH,instance,NULL);SendMessageW(search,WM_SETFONT,(WPARAM)uiSmall,TRUE);oldSearchProc=(WNDPROC)SetWindowLongPtrW(search,GWLP_WNDPROC,(LONG_PTR)searchProc);
  list=CreateWindowExW(0,L"LISTBOX",L"",WS_CHILD|WS_VISIBLE|WS_VSCROLL|LBS_NOTIFY|LBS_OWNERDRAWFIXED|LBS_HASSTRINGS|LBS_NOINTEGRALHEIGHT|LBS_SORT,0,0,0,0,h,(HMENU)ID_LIST,instance,NULL);SendMessageW(list,LB_SETITEMHEIGHT,0,37);
  const wchar_t *labels[]={L"＋  Новая заметка",L"Открыть",L"Экспорт",L"Название",L"Удалить",L"Редактор",L"Чтение"};int ids[]={ID_NEW,ID_OPEN,ID_SAVE,ID_RENAME,ID_DELETE,ID_EDIT,ID_READ};for(int i=0;i<7;i++){buttons[i]=CreateWindowW(L"BUTTON",labels[i],WS_CHILD|WS_VISIBLE|BS_OWNERDRAW,0,0,0,0,h,(HMENU)(INT_PTR)ids[i],instance,NULL);SendMessageW(buttons[i],WM_SETFONT,(WPARAM)uiSmall,TRUE);SetWindowLongPtrW(buttons[i],GWLP_USERDATA,(LONG_PTR)i);WNDPROC original=(WNDPROC)SetWindowLongPtrW(buttons[i],GWLP_WNDPROC,(LONG_PTR)buttonProc);if(i==0)oldButtonProc=original;}
  editor=CreateWindowExW(0,MSFTEDIT_CLASS,L"",WS_CHILD|WS_VISIBLE|WS_VSCROLL|ES_MULTILINE|ES_AUTOVSCROLL|ES_NOHIDESEL,0,0,0,0,h,(HMENU)ID_EDITOR,instance,NULL);if(!editor){error(L"Не удалось запустить компонент редактирования Windows.");return -1;}SendMessageW(editor,WM_SETFONT,(WPARAM)bodyFont,TRUE);SendMessageW(editor,EM_SETBKGNDCOLOR,0,BG);SendMessageW(editor,EM_EXLIMITTEXT,0,MAX_NOTE);SendMessageW(editor,EM_SETEVENTMASK,0,ENM_CHANGE);
  wchar_t appdata[MAX_PATH];if(FAILED(SHGetFolderPathW(h,CSIDL_APPDATA|CSIDL_FLAG_CREATE,NULL,SHGFP_TYPE_CURRENT,appdata))){error(L"Не удалось найти папку данных пользователя.");return -1;}swprintf(directory,MAX_PATH,L"%ls\\Editor1",appdata);if(!CreateDirectoryW(directory,NULL)&&GetLastError()!=ERROR_ALREADY_EXISTS){error(L"Не удалось создать папку заметок.");return -1;}
  text=duptext(L"");refreshList();int count=(int)SendMessageW(list,LB_GETCOUNT,0,0);if(count>0){wchar_t first[MAX_PATH];SendMessageW(list,LB_GETTEXT,0,(LPARAM)first);loadNote(first);}else{wchar_t path[MAX_PATH];pathFor(L"Мой дом",path,MAX_PATH);if(writeUtf8(path,welcome))loadNote(L"Мой дом");else error(L"Не удалось создать первую заметку.");}
  layout(h);return 0; }
 case WM_SIZE:layout(h);InvalidateRect(h,NULL,TRUE);return 0;
 case WM_GETMINMAXINFO:{MINMAXINFO *i=(MINMAXINFO*)l;i->ptMinTrackSize.x=850;i->ptMinTrackSize.y=600;return 0;}
 case WM_ERASEBKGND:return 1;
 case WM_PAINT:{PAINTSTRUCT ps;HDC dc=BeginPaint(h,&ps);RECT r;GetClientRect(h,&r);fill(dc,r,BG);RECT sidebar={0,0,256,r.bottom};fill(dc,sidebar,SIDE);RECT line={255,0,256,r.bottom};fill(dc,line,BORDER);RECT top={256,69,r.right,70};fill(dc,top,BORDER);RECT foot={256,r.bottom-42,r.right,r.bottom-41};fill(dc,foot,BORDER);
  RECT logo={19,18,51,50};rounded(dc,logo,RGB(102,79,150),12);drawText(dc,L"e",logo,brandFont,RGB(248,242,255),DT_CENTER|DT_SINGLELINE|DT_VCENTER);
  RECT brand={61,18,205,39};drawText(dc,L"editor 1",brand,brandFont,TEXT,DT_LEFT|DT_SINGLELINE|DT_VCENTER);RECT under={62,42,224,61};drawText(dc,L"пространство для мыслей",under,uiSmall,MUTED,DT_LEFT|DT_SINGLELINE|DT_VCENTER);
  RECT searchBox={17,99,239,139};rounded(dc,searchBox,RGB(36,36,44),12);RECT searchIcon={28,100,46,138};drawText(dc,L"⌕",searchIcon,brandFont,MUTED,DT_SINGLELINE|DT_VCENTER);
  RECT lib={20,228,230,251};drawText(dc,L"ВАШИ ЗАМЕТКИ",lib,uiBold,MUTED,DT_LEFT|DT_SINGLELINE|DT_VCENTER);wchar_t count[32];swprintf(count,32,L"%d",(int)SendMessageW(list,LB_GETCOUNT,0,0));RECT countRect={210,228,233,251};drawText(dc,count,countRect,uiSmall,MUTED,DT_RIGHT|DT_SINGLELINE|DT_VCENTER);
  RECT bottomLine={20,r.bottom-75,236,r.bottom-74};fill(dc,bottomLine,BORDER);RECT bottom={20,r.bottom-61,230,r.bottom-42};drawText(dc,L"●  Личное пространство",bottom,uiBold,TEAL,DT_LEFT|DT_SINGLELINE|DT_VCENTER);RECT bottom2={32,r.bottom-40,230,r.bottom-23};drawText(dc,L"Хранится на устройстве",bottom2,uiSmall,MUTED,DT_LEFT|DT_SINGLELINE|DT_VCENTER);
  RECT bread={281,23,r.right-355,48};drawText(dc,L"Моё пространство   /   Заметки",bread,uiSmall,MUTED,DT_SINGLELINE|DT_VCENTER|DT_END_ELLIPSIS);
  int x=256+((r.right-256-760)>0?(r.right-256-760)/2:34);RECT label={x,100,r.right-20,121};drawText(dc,L"ВАША ЗАМЕТКА   ·   MARKDOWN",label,uiBold,ACCENT,DT_LEFT|DT_SINGLELINE|DT_VCENTER);
  RECT title={x,131,r.right-40,177};drawText(dc,current[0]?current:L"Нет заметок",title,titleFont,TEXT,DT_LEFT|DT_SINGLELINE|DT_VCENTER|DT_END_ELLIPSIS);
  RECT sub={x,181,r.right-20,200};drawText(dc,L"Место для ваших мыслей и идей",sub,uiSmall,MUTED,DT_LEFT|DT_SINGLELINE|DT_VCENTER);
  RECT toolbar={x,215,x+224,257};rounded(dc,toolbar,RGB(36,35,43),13);
  RECT status={280,r.bottom-33,r.right-20,r.bottom-9};drawText(dc,dirty?L"●  Сохраняем изменения...":L"●  Все изменения сохранены",status,uiSmall,dirty?ACCENT:TEAL,DT_SINGLELINE|DT_VCENTER);RECT statusRight={r.right-220,r.bottom-33,r.right-25,r.bottom-9};drawText(dc,reading?L"Режим чтения":L"Режим редактирования",statusRight,uiSmall,MUTED,DT_RIGHT|DT_SINGLELINE|DT_VCENTER);EndPaint(h,&ps);return 0;}
 case WM_DRAWITEM:drawItem((DRAWITEMSTRUCT*)l);return TRUE;
 case WM_CTLCOLOREDIT:case WM_CTLCOLORLISTBOX:{HDC dc=(HDC)w;SetTextColor(dc,TEXT);if((HWND)l==search){SetBkColor(dc,RGB(36,36,44));return (LRESULT)inputBrush;}SetBkColor(dc,SIDE);return (LRESULT)sideBrush;}
 case WM_COMMAND: {
  int id=LOWORD(w),code=HIWORD(w);
  if(id==ID_SEARCH&&code==EN_CHANGE){refreshList();return 0;}
  if(id==ID_EDITOR&&code==EN_CHANGE&&!loading&&!reading&&current[0]){dirty=1;KillTimer(h,1);SetTimer(h,1,700,NULL);InvalidateRect(h,NULL,FALSE);return 0;}
  if(id==ID_LIST&&code==LBN_SELCHANGE){int idx=(int)SendMessageW(list,LB_GETCURSEL,0,0);if(idx!=LB_ERR){wchar_t name[MAX_PATH];SendMessageW(list,LB_GETTEXT,idx,(LPARAM)name);if(wcscmp(name,current)&&!loadNote(name))refreshList();}return 0;}
  if(id==ID_NEW){createNoteWith(L"");return 0;}if(id==ID_OPEN){importNote();return 0;}if(id==ID_SAVE){exportNote();return 0;}if(id==ID_RENAME){renameNote();return 0;}if(id==ID_DELETE){deleteNote();return 0;}
  if(id==ID_EDIT&&reading){reading=0;showContent();SetFocus(editor);return 0;}if(id==ID_READ&&!reading){if(!saveNote(1))return 0;capture();reading=1;showContent();return 0;}break;}
 case WM_TIMER:if(w==1){KillTimer(h,1);saveNote(1);return 0;}if(w==2){int moving=0;BOOL animations=TRUE;SystemParametersInfoW(SPI_GETCLIENTAREAANIMATION,0,&animations,0);for(int i=0;i<7;i++){int target=hovered[i]?100:0;if(hoverAmount[i]!=target){int step=animations?20:100;if(hoverAmount[i]<target){hoverAmount[i]+=step;if(hoverAmount[i]>target)hoverAmount[i]=target;}else{hoverAmount[i]-=step;if(hoverAmount[i]<target)hoverAmount[i]=target;}InvalidateRect(buttons[i],NULL,FALSE);if(hoverAmount[i]!=target)moving=1;}}if(!moving)KillTimer(h,2);return 0;}break;
 case WM_CLOSE:if(saveNote(1))DestroyWindow(h);return 0;
 case WM_DESTROY:free(text);DeleteObject(ui);DeleteObject(uiSmall);DeleteObject(uiBold);DeleteObject(bodyFont);DeleteObject(titleFont);DeleteObject(brandFont);DeleteObject(bgBrush);DeleteObject(sideBrush);DeleteObject(inputBrush);PostQuitMessage(0);return 0;
 }return DefWindowProcW(h,m,w,l);
}
int WINAPI wWinMain(HINSTANCE instance,HINSTANCE prev,PWSTR cmd,int show){(void)prev;(void)cmd;WNDCLASSW wc={0};wc.lpfnWndProc=WindowProc;wc.hInstance=instance;wc.lpszClassName=L"Editor1Main";wc.hCursor=LoadCursor(NULL,IDC_ARROW);wc.hIcon=LoadIcon(NULL,IDI_APPLICATION);RegisterClassW(&wc);HWND h=CreateWindowW(wc.lpszClassName,L"Editor 1",WS_OVERLAPPEDWINDOW|WS_CLIPCHILDREN,CW_USEDEFAULT,CW_USEDEFAULT,1280,850,NULL,NULL,instance,NULL);if(!h)return 1;ShowWindow(h,show);UpdateWindow(h);ACCEL keys[]={{FVIRTKEY|FCONTROL,'N',ID_NEW},{FVIRTKEY|FCONTROL,'O',ID_OPEN},{FVIRTKEY|FCONTROL,'S',ID_SAVE},{FVIRTKEY,VK_F2,ID_RENAME}};HACCEL accel=CreateAcceleratorTableW(keys,4);MSG msg;while(GetMessageW(&msg,NULL,0,0)>0){if(!TranslateAcceleratorW(h,accel,&msg)){TranslateMessage(&msg);DispatchMessageW(&msg);}}DestroyAcceleratorTable(accel);return (int)msg.wParam;}
