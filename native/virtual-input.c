/* Original SideScreen per-event virtual input. MinHook is BSD-2-Clause.
 * Thread-specific Windows hooks; no kernel driver, SendInput or global hooks.
 * Physical input stays on its normal route outside our authenticated frame. */
#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#include <objbase.h>
#include <stdint.h>
#include <string.h>
#include <wchar.h>
#include "MinHook.h"

#define MAGIC 0x53494445u
#define VERSION 1u
#define MAX_ROOTS 16
#define ACK_OK 1
typedef struct {
    uint32_t version, operation;
    uint64_t root, target;
    int32_t x, y;
    uint32_t message, reserved;
    uint64_t wparam;
    int64_t lparam;
    unsigned char keys[256];
    wchar_t mapping[96];
} Frame;
typedef struct {Frame frame; LONG status, blocked; uint64_t result;} Shared;
typedef struct {HWND root, focus, capture; ULONGLONG expires;} Root;
typedef struct {Frame *frame; Root *root; LONG blocked;} Context;
typedef struct {DWORD thread, pid; HHOOK hook;} Installed;
static HMODULE module;
static DWORD tls=TLS_OUT_OF_INDEXES;
static Root roots[MAX_ROOTS];
static Installed installed[MAX_ROOTS];
static SRWLOCK roots_lock=SRWLOCK_INIT;
static volatile LONG initialized;

static BOOL (WINAPI *real_cursor)(LPPOINT);
static DWORD (WINAPI *real_message_pos)(void);
static SHORT (WINAPI *real_key)(int), (WINAPI *real_async_key)(int);
static BOOL (WINAPI *real_keyboard)(PBYTE);
static HWND (WINAPI *real_focus)(void), (WINAPI *real_foreground)(void), (WINAPI *real_active)(void), (WINAPI *real_capture)(void);
static HWND (WINAPI *real_set_focus)(HWND), (WINAPI *real_set_active)(HWND), (WINAPI *real_set_capture)(HWND);
static BOOL (WINAPI *real_foreground_set)(HWND), (WINAPI *real_release)(void), (WINAPI *real_cursor_set)(int,int);
static HWND (WINAPI *real_window_point)(POINT);
static BOOL (WINAPI *real_gui_info)(DWORD,PGUITHREADINFO), (WINAPI *real_track)(LPTRACKMOUSEEVENT);
static BOOL (WINAPI *real_show)(HWND,int), (WINAPI *real_position)(HWND,HWND,int,int,int,int,UINT);
static Context *current(void){return tls==TLS_OUT_OF_INDEXES?NULL:(Context*)TlsGetValue(tls);}
static int owned(HWND root,HWND window){return window==root||IsChild(root,window);}
static int locked(HWND window){
    if(!window)return 0;
    HWND top=GetAncestor(window,GA_ROOTOWNER);
    int found=0;ULONGLONG now=GetTickCount64();
    AcquireSRWLockShared(&roots_lock);
    for(int n=0;n<MAX_ROOTS;n++)if(roots[n].root&&roots[n].expires>now&&(window==roots[n].root||top==roots[n].root||IsChild(roots[n].root,window))){found=1;break;}
    ReleaseSRWLockShared(&roots_lock);return found;
}
static BOOL WINAPI cursor(LPPOINT p){Context*c=current();if(!c)return real_cursor(p);if(!p)return FALSE;p->x=c->frame->x;p->y=c->frame->y;return TRUE;}
static DWORD WINAPI message_pos(void){Context*c=current();return c?MAKELONG(c->frame->x,c->frame->y):real_message_pos();}
static SHORT WINAPI key(int k){Context*c=current();return c&&k>=0&&k<256?(SHORT)(((c->frame->keys[k]&128)?0x8000:0)|(c->frame->keys[k]&1)):real_key(k);}
static SHORT WINAPI async_key(int k){Context*c=current();return c&&k>=0&&k<256?(SHORT)((c->frame->keys[k]&128)?0x8000:0):real_async_key(k);}
static BOOL WINAPI keyboard(PBYTE p){Context*c=current();if(!c)return real_keyboard(p);if(!p)return FALSE;memcpy(p,c->frame->keys,256);return TRUE;}
static HWND WINAPI focus(void){Context*c=current();return c?c->root->focus:real_focus();}
static HWND WINAPI foreground(void){Context*c=current();return c?c->root->root:real_foreground();}
static HWND WINAPI active(void){Context*c=current();return c?c->root->root:real_active();}
static HWND WINAPI capture(void){Context*c=current();return c?c->root->capture:real_capture();}
static HWND WINAPI set_focus(HWND h){Context*c=current();if(c){HWND before=c->root->focus;if(!h||owned(c->root->root,h))c->root->focus=h;c->blocked++;return before;}if(locked(h))return real_focus();return real_set_focus(h);}
static HWND WINAPI set_active(HWND h){Context*c=current();if(c){c->blocked++;return c->root->root;}if(locked(h))return real_active();return real_set_active(h);}
static BOOL WINAPI foreground_set(HWND h){Context*c=current();if(c||locked(h)){if(c)c->blocked++;return TRUE;}return real_foreground_set(h);}
static HWND WINAPI set_capture(HWND h){Context*c=current();if(!c)return real_set_capture(h);HWND previous=c->root->capture;if(!h||owned(c->root->root,h))c->root->capture=h;return previous;}
static BOOL WINAPI release(void){Context*c=current();if(!c)return real_release();c->root->capture=NULL;return TRUE;}
static BOOL WINAPI cursor_set(int x,int y){Context*c=current();if(!c)return real_cursor_set(x,y);c->blocked++;return FALSE;}
static HWND WINAPI window_point(POINT p){
    Context*c=current();if(!c)return real_window_point(p);
    RECT r;GetWindowRect(c->root->root,&r);if(!PtInRect(&r,p))return NULL;
    HWND h=c->root->root;
    for(int n=0;n<32;n++){POINT q=p;ScreenToClient(h,&q);HWND child=ChildWindowFromPointEx(h,q,CWP_SKIPINVISIBLE|CWP_SKIPDISABLED|CWP_SKIPTRANSPARENT);if(!child||child==h)break;h=child;}
    return h;
}
static BOOL WINAPI gui_info(DWORD thread,PGUITHREADINFO g){BOOL ok=real_gui_info(thread,g);Context*c=current();if(ok&&c&&(thread==0||thread==GetCurrentThreadId())){g->hwndActive=c->root->root;g->hwndFocus=c->root->focus;g->hwndCapture=c->root->capture;}return ok;}
static BOOL WINAPI track(LPTRACKMOUSEEVENT p){return current()?TRUE:real_track(p);}
static BOOL WINAPI show(HWND h,int command){if(locked(h)&&command!=SW_HIDE&&command!=SW_MINIMIZE&&command!=SW_SHOWMINNOACTIVE)command=SW_SHOWNOACTIVATE;return real_show(h,command);}
static BOOL WINAPI position(HWND h,HWND z,int x,int y,int w,int height,UINT flags){if(locked(h)){flags|=SWP_NOACTIVATE;Context*c=current();if(c)c->blocked++;}return real_position(h,z,x,y,w,height,flags);}

#define HOOK(name,callback,original) do {MH_STATUS s=MH_CreateHookApi(L"user32.dll",name,(LPVOID)callback,(LPVOID*)&original);if(s!=MH_OK){InterlockedExchange(&initialized,-1);return -(100+(int)s);}}while(0)
static int initialize(void){
    if(InterlockedCompareExchange(&initialized,1,0)!=0){while(initialized==1)Sleep(1);return initialized==2?0:-2;}
    tls=TlsAlloc();if(tls==TLS_OUT_OF_INDEXES){initialized=-1;return -3;}
    MH_STATUS status=MH_Initialize();if(status!=MH_OK&&status!=MH_ERROR_ALREADY_INITIALIZED){initialized=-1;return -(100+(int)status);}
    int result=0;
    /* Pin the module before any trampoline can refer to it after unhook. */
    HMODULE pinned;if(!GetModuleHandleExW(GET_MODULE_HANDLE_EX_FLAG_FROM_ADDRESS|GET_MODULE_HANDLE_EX_FLAG_PIN,(LPCWSTR)&initialize,&pinned)){InterlockedExchange(&initialized,-1);return -6;}
    HOOK("GetCursorPos",cursor,real_cursor);HOOK("GetMessagePos",message_pos,real_message_pos);
    HOOK("GetKeyState",key,real_key);HOOK("GetAsyncKeyState",async_key,real_async_key);HOOK("GetKeyboardState",keyboard,real_keyboard);
    HOOK("GetFocus",focus,real_focus);HOOK("GetForegroundWindow",foreground,real_foreground);HOOK("GetActiveWindow",active,real_active);HOOK("GetCapture",capture,real_capture);
    HOOK("SetFocus",set_focus,real_set_focus);HOOK("SetActiveWindow",set_active,real_set_active);HOOK("SetForegroundWindow",foreground_set,real_foreground_set);
    HOOK("SetCapture",set_capture,real_set_capture);HOOK("ReleaseCapture",release,real_release);HOOK("SetCursorPos",cursor_set,real_cursor_set);
    HOOK("WindowFromPoint",window_point,real_window_point);HOOK("GetGUIThreadInfo",gui_info,real_gui_info);HOOK("TrackMouseEvent",track,real_track);
    HOOK("ShowWindow",show,real_show);HOOK("SetWindowPos",position,real_position);
    status=MH_EnableHook(MH_ALL_HOOKS);result=status==MH_OK?0:-(100+(int)status);
    InterlockedExchange(&initialized,result==0?2:-1);return result;
}
static Root *root_for(HWND h,int release_root){
    Root*r=NULL;ULONGLONG now=GetTickCount64();AcquireSRWLockExclusive(&roots_lock);
    for(int n=0;n<MAX_ROOTS;n++)if(roots[n].root==h){r=&roots[n];break;}
    if(release_root){if(r)memset(r,0,sizeof(*r));ReleaseSRWLockExclusive(&roots_lock);return NULL;}
    if(!r)for(int n=0;n<MAX_ROOTS;n++)if(!roots[n].root||roots[n].expires<now||!IsWindow(roots[n].root)){r=&roots[n];memset(r,0,sizeof(*r));r->root=h;break;}
    if(r){r->expires=now+120000;if(!r->focus)r->focus=h;}
    ReleaseSRWLockExclusive(&roots_lock);return r;
}
__declspec(dllexport) LRESULT CALLBACK SideInputHook(int code,WPARAM w,LPARAM l){
    if(code>=0){CWPSTRUCT*c=(CWPSTRUCT*)l;
        if(c->message==WM_COPYDATA&&c->lParam){COPYDATASTRUCT*d=(COPYDATASTRUCT*)c->lParam;
            if(d->dwData==MAGIC&&d->cbData==sizeof(Frame)&&d->lpData){Frame*f=(Frame*)d->lpData;
                DWORD pid=0;GetWindowThreadProcessId((HWND)(UINT_PTR)f->root,&pid);
                if(f->version==VERSION&&f->root==(uint64_t)(UINT_PTR)c->hwnd&&pid==GetCurrentProcessId()&&owned(c->hwnd,(HWND)(UINT_PTR)f->target)&&wcsncmp(f->mapping,L"Local\\SideScreen.VirtualInput.",30)==0&&f->mapping[95]==0){
                    HANDLE mapping=OpenFileMappingW(FILE_MAP_ALL_ACCESS,FALSE,f->mapping);
                    if(mapping){Shared*s=(Shared*)MapViewOfFile(mapping,FILE_MAP_ALL_ACCESS,0,0,sizeof(Shared));
                        if(s&&memcmp(&s->frame,f,sizeof(Frame))==0){
                            int ready=initialize();
                            if(ready<0)s->status=ready;
                            else if(f->operation==2){root_for(c->hwnd,1);s->status=ACK_OK;}
                            else if(f->operation==1&&!current()){
                                Root*r=root_for(c->hwnd,0);
                                if(!r)s->status=-4;
                                else {Context context={f,r,0};r->focus=(HWND)(UINT_PTR)f->target;
                                    TlsSetValue(tls,&context);
                                    s->result=(uint64_t)SendMessageW((HWND)(UINT_PTR)f->target,f->message,(WPARAM)f->wparam,(LPARAM)f->lparam);
                                    s->blocked=context.blocked;TlsSetValue(tls,NULL);s->status=ACK_OK;
                                }
                            }else s->status=-5;
                        }
                        if(s)UnmapViewOfFile(s);CloseHandle(mapping);
                    }
                }
            }
        }
    }
    return CallNextHookEx(NULL,code,w,l);
}
__declspec(dllexport) int WINAPI SideInputDispatch(Frame*f,LONG*blocked){
    if(!f||f->version!=VERSION)return -10;
    HWND root=(HWND)(UINT_PTR)f->root,target=(HWND)(UINT_PTR)f->target;DWORD pid=0;
    DWORD thread=GetWindowThreadProcessId(root,&pid);
    if(!thread||pid==GetCurrentProcessId()||!owned(root,target)||GetWindowThreadProcessId(target,NULL)!=thread)return -11;
    if(f->operation!=1&&f->operation!=2)return -16;
    if(f->operation==1&&f->message!=WM_MOUSEMOVE&&f->message!=WM_LBUTTONDOWN&&f->message!=WM_LBUTTONUP&&f->message!=WM_LBUTTONDBLCLK&&f->message!=WM_RBUTTONDOWN&&f->message!=WM_RBUTTONUP&&f->message!=WM_RBUTTONDBLCLK&&f->message!=WM_MBUTTONDOWN&&f->message!=WM_MBUTTONUP&&f->message!=WM_MBUTTONDBLCLK&&f->message!=WM_MOUSEWHEEL&&f->message!=WM_MOUSEHWHEEL&&f->message!=WM_KEYDOWN&&f->message!=WM_KEYUP&&f->message!=WM_CHAR&&f->message!=EM_SETSEL)return -17;
    int n;for(n=0;n<MAX_ROOTS;n++)if(installed[n].thread==thread&&installed[n].pid==pid&&installed[n].hook)break;
    if(n==MAX_ROOTS&&f->operation==2){if(blocked)*blocked=0;return ACK_OK;}
    if(n==MAX_ROOTS){for(n=0;n<MAX_ROOTS;n++)if(!installed[n].hook)break;if(n==MAX_ROOTS)return -12;
        installed[n].hook=SetWindowsHookExW(WH_CALLWNDPROC,SideInputHook,module,thread);if(!installed[n].hook)return -(1000+(int)GetLastError());installed[n].thread=thread;installed[n].pid=pid;
    }
    GUID id;if(FAILED(CoCreateGuid(&id)))return -18;
    wsprintfW(f->mapping,L"Local\\SideScreen.VirtualInput.%08x%04x%04x%02x%02x%02x%02x%02x%02x%02x%02x",id.Data1,id.Data2,id.Data3,id.Data4[0],id.Data4[1],id.Data4[2],id.Data4[3],id.Data4[4],id.Data4[5],id.Data4[6],id.Data4[7]);
    HANDLE mapping=CreateFileMappingW(INVALID_HANDLE_VALUE,NULL,PAGE_READWRITE,0,sizeof(Shared),f->mapping);if(!mapping)return -13;
    Shared*s=(Shared*)MapViewOfFile(mapping,FILE_MAP_ALL_ACCESS,0,0,sizeof(Shared));if(!s){CloseHandle(mapping);return -14;}memset(s,0,sizeof(*s));s->frame=*f;
    COPYDATASTRUCT copy={MAGIC,sizeof(Frame),f};DWORD_PTR response=0;
    BOOL sent=SendMessageTimeoutW(root,WM_COPYDATA,0,(LPARAM)&copy,SMTO_ABORTIFHUNG|SMTO_BLOCK,3000,&response)!=0;
    int status=sent?s->status:-15;if(blocked)*blocked=s->blocked;
    UnmapViewOfFile(s);CloseHandle(mapping);return status;
}
__declspec(dllexport) void WINAPI SideInputClose(void){for(int n=0;n<MAX_ROOTS;n++)if(installed[n].hook){UnhookWindowsHookEx(installed[n].hook);installed[n].hook=NULL;installed[n].thread=0;}}
BOOL WINAPI DllMain(HINSTANCE instance,DWORD reason,LPVOID reserved){(void)reserved;if(reason==DLL_PROCESS_ATTACH){module=instance;DisableThreadLibraryCalls(instance);}return TRUE;}
