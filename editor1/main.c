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
static HFONT ui, uiSmall, uiBold, bodyFont, titleFont;
static HBRUSH bgBrush, sideBrush, inputBrush;
static wchar_t directory[MAX_PATH], current[MAX_PATH], *text;
static int dirty, loading, reading = 1, promptDone, promptAccepted;
static wchar_t promptValue[180];
static const COLORREF BG = RGB(31,31,31), SIDE = RGB(26,26,26), BORDER = RGB(51,51,51), TEXT = RGB(213,211,207), MUTED = RGB(138,138,135), ACCENT = RGB(218,126,77), TEAL = RGB(126,186,181);
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
   styleRange(position,position+(LONG)count,level==1?RGB(225,93,101):(level?ACCENT:(p[0]==L'>'?MUTED:TEXT)),level==1?23:(level==2?18:(level==3?16:14)),level>0);
   position+=(LONG)count+1;free(line);if(!end)break;p=end+1;
  }
  SendMessageW(editor,EM_SETREADONLY,TRUE,0);
 }
 SendMessageW(editor,EM_SETSEL,0,0); SendMessageW(editor,WM_SETREDRAW,TRUE,0);InvalidateRect(editor,NULL,TRUE); loading=0;InvalidateRect(window,NULL,FALSE);
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
static void layout(HWND h) {
 RECT r;GetClientRect(h,&r);int w=r.right, height=r.bottom;int side=238;
 MoveWindow(search,15,91,side-30,30,TRUE);MoveWindow(buttons[0],15,154,side-30,34,TRUE);MoveWindow(list,8,244,side-16,height-326,TRUE);
 int right=w-20;MoveWindow(buttons[1],right-286,15,67,30,TRUE);MoveWindow(buttons[2],right-214,15,70,30,TRUE);MoveWindow(buttons[3],right-139,15,67,30,TRUE);MoveWindow(buttons[4],right-67,15,67,30,TRUE);
 int content=side+((w-side-760)>0?(w-side-760)/2:34), contentWidth=w-content-38;if(contentWidth>760)contentWidth=760;if(contentWidth<220)contentWidth=220;
 MoveWindow(buttons[5],content,112,105,32,TRUE);MoveWindow(buttons[6],content+111,112,95,32,TRUE);MoveWindow(editor,content,166,contentWidth,height-210,TRUE);
}
static void drawItem(DRAWITEMSTRUCT *d) {
 if(d->CtlType==ODT_LISTBOX){if(d->itemID==(UINT)-1)return;wchar_t name[MAX_PATH];SendMessageW(list,LB_GETTEXT,d->itemID,(LPARAM)name);int selected=d->itemState&ODS_SELECTED;fill(d->hDC,d->rcItem,selected?RGB(53,53,53):SIDE);RECT icon=d->rcItem;icon.left+=10;icon.right=icon.left+17;drawText(d->hDC,L"▤",icon,ui,selected?ACCENT:MUTED,DT_SINGLELINE|DT_VCENTER);RECT label=d->rcItem;label.left+=32;label.right-=5;drawText(d->hDC,name,label,uiSmall,selected?TEXT:RGB(159,159,155),DT_SINGLELINE|DT_VCENTER|DT_END_ELLIPSIS);return;}
 int id=d->CtlID;int active=(id==ID_EDIT&&!reading)||(id==ID_READ&&reading);COLORREF color=(d->itemState&ODS_DISABLED)?MUTED:TEXT;COLORREF back=(d->itemState&ODS_SELECTED)?RGB(72,72,72):(active?RGB(75,72,68):(id==ID_NEW?RGB(57,57,57):BG));fill(d->hDC,d->rcItem,back);wchar_t label[80];GetWindowTextW(d->hwndItem,label,80);drawText(d->hDC,label,d->rcItem,uiSmall,color,DT_SINGLELINE|DT_CENTER|DT_VCENTER);
}
static LRESULT CALLBACK WindowProc(HWND h,UINT m,WPARAM w,LPARAM l) {
 switch(m){
 case WM_CREATE: {
  window=h; HINSTANCE instance=GetModuleHandleW(NULL); LoadLibraryW(L"Msftedit.dll");
  ui=CreateFontW(-14,0,0,0,FW_NORMAL,FALSE,FALSE,FALSE,DEFAULT_CHARSET,0,0,CLEARTYPE_QUALITY,0,L"Segoe UI");uiSmall=CreateFontW(-13,0,0,0,FW_NORMAL,FALSE,FALSE,FALSE,DEFAULT_CHARSET,0,0,CLEARTYPE_QUALITY,0,L"Segoe UI");uiBold=CreateFontW(-13,0,0,0,FW_SEMIBOLD,FALSE,FALSE,FALSE,DEFAULT_CHARSET,0,0,CLEARTYPE_QUALITY,0,L"Segoe UI");bodyFont=CreateFontW(-17,0,0,0,FW_NORMAL,FALSE,FALSE,FALSE,DEFAULT_CHARSET,0,0,CLEARTYPE_QUALITY,0,L"Georgia");titleFont=CreateFontW(-20,0,0,0,FW_SEMIBOLD,FALSE,FALSE,FALSE,DEFAULT_CHARSET,0,0,CLEARTYPE_QUALITY,0,L"Segoe UI");
  bgBrush=CreateSolidBrush(BG);sideBrush=CreateSolidBrush(SIDE);inputBrush=CreateSolidBrush(RGB(38,38,38));
  search=CreateWindowExW(0,L"EDIT",L"",WS_CHILD|WS_VISIBLE|ES_AUTOHSCROLL,0,0,0,0,h,(HMENU)ID_SEARCH,instance,NULL);SendMessageW(search,WM_SETFONT,(WPARAM)uiSmall,TRUE);SendMessageW(search,EM_SETCUEBANNER,0,(LPARAM)L"Поиск заметок...");
  list=CreateWindowExW(0,L"LISTBOX",L"",WS_CHILD|WS_VISIBLE|WS_VSCROLL|LBS_NOTIFY|LBS_OWNERDRAWFIXED|LBS_HASSTRINGS|LBS_NOINTEGRALHEIGHT|LBS_SORT,0,0,0,0,h,(HMENU)ID_LIST,instance,NULL);SendMessageW(list,LB_SETITEMHEIGHT,0,29);
  const wchar_t *labels[]={L"＋  Новая заметка",L"Открыть",L"Экспорт",L"Название",L"Удалить",L"Редактор",L"Чтение"};int ids[]={ID_NEW,ID_OPEN,ID_SAVE,ID_RENAME,ID_DELETE,ID_EDIT,ID_READ};for(int i=0;i<7;i++){buttons[i]=CreateWindowW(L"BUTTON",labels[i],WS_CHILD|WS_VISIBLE|BS_OWNERDRAW,0,0,0,0,h,(HMENU)(INT_PTR)ids[i],instance,NULL);SendMessageW(buttons[i],WM_SETFONT,(WPARAM)uiSmall,TRUE);}
  editor=CreateWindowExW(0,MSFTEDIT_CLASS,L"",WS_CHILD|WS_VISIBLE|WS_VSCROLL|ES_MULTILINE|ES_AUTOVSCROLL|ES_NOHIDESEL,0,0,0,0,h,(HMENU)ID_EDITOR,instance,NULL);if(!editor){error(L"Не удалось запустить компонент редактирования Windows.");return -1;}SendMessageW(editor,WM_SETFONT,(WPARAM)bodyFont,TRUE);SendMessageW(editor,EM_SETBKGNDCOLOR,0,BG);SendMessageW(editor,EM_EXLIMITTEXT,0,MAX_NOTE);SendMessageW(editor,EM_SETEVENTMASK,0,ENM_CHANGE);
  wchar_t appdata[MAX_PATH];if(FAILED(SHGetFolderPathW(h,CSIDL_APPDATA|CSIDL_FLAG_CREATE,NULL,SHGFP_TYPE_CURRENT,appdata))){error(L"Не удалось найти папку данных пользователя.");return -1;}swprintf(directory,MAX_PATH,L"%ls\\Editor1",appdata);if(!CreateDirectoryW(directory,NULL)&&GetLastError()!=ERROR_ALREADY_EXISTS){error(L"Не удалось создать папку заметок.");return -1;}
  text=duptext(L"");refreshList();int count=(int)SendMessageW(list,LB_GETCOUNT,0,0);if(count>0){wchar_t first[MAX_PATH];SendMessageW(list,LB_GETTEXT,0,(LPARAM)first);loadNote(first);}else{wchar_t path[MAX_PATH];pathFor(L"Мой дом",path,MAX_PATH);if(writeUtf8(path,welcome))loadNote(L"Мой дом");else error(L"Не удалось создать первую заметку.");}
  layout(h);return 0; }
 case WM_SIZE:layout(h);InvalidateRect(h,NULL,TRUE);return 0;
 case WM_GETMINMAXINFO:{MINMAXINFO *i=(MINMAXINFO*)l;i->ptMinTrackSize.x=850;i->ptMinTrackSize.y=600;return 0;}
 case WM_ERASEBKGND:return 1;
 case WM_PAINT:{PAINTSTRUCT ps;HDC dc=BeginPaint(h,&ps);RECT r;GetClientRect(h,&r);fill(dc,r,BG);RECT sidebar={0,0,238,r.bottom};fill(dc,sidebar,SIDE);RECT line={237,0,238,r.bottom};fill(dc,line,BORDER);RECT top={238,60,r.right,61};fill(dc,top,BORDER);RECT foot={238,r.bottom-36,r.right,r.bottom-35};fill(dc,foot,BORDER);
  RECT brand={20,18,220,46};drawText(dc,L"◈  EDITOR 1",brand,uiBold,TEXT,DT_LEFT|DT_SINGLELINE|DT_VCENTER);RECT under={20,52,220,72};drawText(dc,L"ВАШЕ ПРОСТРАНСТВО",under,uiSmall,MUTED,DT_LEFT|DT_SINGLELINE|DT_VCENTER);
  RECT lib={20,207,220,230};drawText(dc,L"ЗАМЕТКИ",lib,uiBold,MUTED,DT_LEFT|DT_SINGLELINE|DT_VCENTER);RECT bottom={20,r.bottom-62,220,r.bottom-30};drawText(dc,L"ЛОКАЛЬНОЕ ХРАНИЛИЩЕ",bottom,uiSmall,MUTED,DT_LEFT|DT_SINGLELINE|DT_VCENTER);
  RECT bread={260,19,r.right-320,42};drawText(dc,current[0]?current:L"Нет заметок",bread,uiBold,TEXT,DT_SINGLELINE|DT_VCENTER|DT_END_ELLIPSIS);
  int x=238+((r.right-238-760)>0?(r.right-238-760)/2:34);RECT label={x,79,r.right-20,101};drawText(dc,L"ЛИЧНАЯ БИБЛИОТЕКА   /   ЗАМЕТКА",label,uiSmall,MUTED,DT_LEFT|DT_SINGLELINE|DT_VCENTER);
  RECT status={260,r.bottom-28,r.right-20,r.bottom-5};drawText(dc,dirty?L"●  Сохранение...":L"●  Сохранено на этом устройстве",status,uiSmall,dirty?ACCENT:TEAL,DT_SINGLELINE|DT_VCENTER);EndPaint(h,&ps);return 0;}
 case WM_DRAWITEM:drawItem((DRAWITEMSTRUCT*)l);return TRUE;
 case WM_CTLCOLOREDIT:case WM_CTLCOLORLISTBOX:{HDC dc=(HDC)w;SetTextColor(dc,TEXT);if((HWND)l==search){SetBkColor(dc,RGB(38,38,38));return (LRESULT)inputBrush;}SetBkColor(dc,SIDE);return (LRESULT)sideBrush;}
 case WM_COMMAND: {
  int id=LOWORD(w),code=HIWORD(w);
  if(id==ID_SEARCH&&code==EN_CHANGE){refreshList();return 0;}
  if(id==ID_EDITOR&&code==EN_CHANGE&&!loading&&!reading&&current[0]){dirty=1;KillTimer(h,1);SetTimer(h,1,700,NULL);InvalidateRect(h,NULL,FALSE);return 0;}
  if(id==ID_LIST&&code==LBN_SELCHANGE){int idx=(int)SendMessageW(list,LB_GETCURSEL,0,0);if(idx!=LB_ERR){wchar_t name[MAX_PATH];SendMessageW(list,LB_GETTEXT,idx,(LPARAM)name);if(wcscmp(name,current)&&!loadNote(name))refreshList();}return 0;}
  if(id==ID_NEW){createNoteWith(L"");return 0;}if(id==ID_OPEN){importNote();return 0;}if(id==ID_SAVE){exportNote();return 0;}if(id==ID_RENAME){renameNote();return 0;}if(id==ID_DELETE){deleteNote();return 0;}
  if(id==ID_EDIT&&reading){reading=0;showContent();SetFocus(editor);return 0;}if(id==ID_READ&&!reading){if(!saveNote(1))return 0;capture();reading=1;showContent();return 0;}break;}
 case WM_TIMER:if(w==1){KillTimer(h,1);saveNote(1);return 0;}break;
 case WM_CLOSE:if(saveNote(1))DestroyWindow(h);return 0;
 case WM_DESTROY:free(text);DeleteObject(ui);DeleteObject(uiSmall);DeleteObject(uiBold);DeleteObject(bodyFont);DeleteObject(titleFont);DeleteObject(bgBrush);DeleteObject(sideBrush);DeleteObject(inputBrush);PostQuitMessage(0);return 0;
 }return DefWindowProcW(h,m,w,l);
}
int WINAPI wWinMain(HINSTANCE instance,HINSTANCE prev,PWSTR cmd,int show){(void)prev;(void)cmd;WNDCLASSW wc={0};wc.lpfnWndProc=WindowProc;wc.hInstance=instance;wc.lpszClassName=L"Editor1Main";wc.hCursor=LoadCursor(NULL,IDC_ARROW);wc.hIcon=LoadIcon(NULL,IDI_APPLICATION);RegisterClassW(&wc);HWND h=CreateWindowW(wc.lpszClassName,L"Editor 1",WS_OVERLAPPEDWINDOW|WS_CLIPCHILDREN,CW_USEDEFAULT,CW_USEDEFAULT,1280,850,NULL,NULL,instance,NULL);if(!h)return 1;ShowWindow(h,show);UpdateWindow(h);ACCEL keys[]={{FVIRTKEY|FCONTROL,'N',ID_NEW},{FVIRTKEY|FCONTROL,'O',ID_OPEN},{FVIRTKEY|FCONTROL,'S',ID_SAVE},{FVIRTKEY,VK_F2,ID_RENAME}};HACCEL accel=CreateAcceleratorTableW(keys,4);MSG msg;while(GetMessageW(&msg,NULL,0,0)>0){if(!TranslateAcceleratorW(h,accel,&msg)){TranslateMessage(&msg);DispatchMessageW(&msg);}}DestroyAcceleratorTable(accel);return (int)msg.wParam;}
