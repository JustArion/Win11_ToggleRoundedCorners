namespace Dawn.Libs.ToggleRoundedCorners.Native.PatternScanning;

// To get these I'm using IDA Pro 8.3 along with SigMakerEx 1.1.2
// https://github.com/kweatherman/sigmakerex
internal static class Patterns
{
    /* bool __fastcall CWindowList::IsRemoteAppMode(CWindowList *__hidden this)
    ?IsRemoteAppMode@CWindowList@@UEAA_NXZ proc near
    mov     rax, cs:?s_pDesktopManagerInstance@CDesktopManager@@0PEAV1@EA ; CDesktopManager * CDesktopManager::s_pDesktopManagerInstance
    mov     ecx, [rax+20h]
    sub     ecx, 5
    cmp     ecx, 2
    setbe   al
    retn
    ?IsRemoteAppMode@CWindowList@@UEAA_NXZ endp 
     */
    public const string IS_REMOTE_APP_MODE_PATTERN = "48 8B 05 ?? ?? ?? ?? 8B 48 20 83 E9 05 83 F9 02";

    /* private: enum CORNER_STYLE CTopLevelWindow::GetEffectiveCornerStyle(void)
    ?GetEffectiveCornerStyle@CTopLevelWindow@@AEAA?AW4CORNER_STYLE@@XZ proc near
    push    rbx
    sub     rsp, 20h
    mov     rax, cs:?s_pDesktopManagerInstance@CDesktopManager@@0PEAV1@EA ; CDesktopManager * CDesktopManager::s_pDesktopManagerInstance
    mov     rbx, rcx
    cmp     dword ptr [rax+20h], 5
    jz      short loc_18001382E
     */
    public const string GET_EFFECTIVE_CORNER_STYLE_FUNCTION_PATTERN = "40 53 48 83 EC 20 48 8B 05 ?? ?? ?? ?? 48 8B D9 83";
    
    public const string GET_EFFECTIVE_CORNER_STYLE_PATTERN = "48 8B 05 ?? ?? ?? ?? 48 8B D9 83 78 20 05";

    /* ULONG __stdcall CDesktopManager::DwmEventThreadProc(PVOID Parameter)
    ; __unwind { // __GSHandlerCheck_EH4
    mov     rax, rsp
    mov     [rax+10h], rbx
    mov     [rax+18h], rsi
    mov     [rax+20h], rdi
    push    rbp
    push    r12
    push    r13
    push    r14
    push    r15
    lea     rbp, [rax-78h]
    sub     rsp, 150h
    mov     rax, cs:__security_cookie
    xor     rax, rsp
    mov     [rbp+70h+var_30], rax
    mov     rsi, rcx
    xor     ebx, ebx
    mov     r15d, 800401F0h
    mov     [rsp+170h+hObject], rbx
    mov     qword ptr [rsp+170h+var_108], rbx
    mov     qword ptr [rsp+170h+Frequency], rbx
    mov     r12b, bl
    mov     r13b, bl
    mov     [rsp+170h+var_140], bl
    mov     r14b, bl
    mov     [rsp+170h+var_138], bx
    mov     [rbp+70h+hSCObject], rbx
    mov     [rbp+70h+nCount], ebx
    call    cs:__imp_GetCurrentThreadId
    nop     dword ptr [rax+rax+00h]
    mov     ecx, eax
    mov     rax, cs:?s_pDesktopManagerInstance@CDesktopManager@@0PEAV1@EA ; CDesktopManager * CDesktopManager::s_pDesktopManagerInstance
    mov     [rax+468h], ecx
    xor     ecx, ecx        ; dwErrCode
    call    cs:__imp_SetLastError
    nop     dword ptr [rax+rax+00h]
    lea     rcx, [rsp+170h+Frequency] ; lpFrequency
    call    cs:__imp_QueryPerformanceFrequency
    nop     dword ptr [rax+rax+00h]
    test    eax, eax
    jz      loc_180052367
     */
    public const string DWM_EVENT_THREAD_PROC_PATTERN = "48 8B 05 ?? ?? ?? ?? 89 88 68 04 00 00";

    /* __int64 __fastcall CWindowIconic::Initialize(CWindowIconic *__hidden this, struct CWindowData *)
     mov     rax, [rsi]
    mov     rcx, [rdi+50h]
    mov     rax, [rax+10h]
    mov     [rcx+90h], rax
    mov     dword ptr [rsp+48h+arg_0], ebp
    lea     r8, [rsp+48h+arg_0] ; enum IconicRepresentationType *
    mov     rdx, rdi        ; struct CWindowIconic *
    mov     rcx, cs:?s_pDesktopManagerInstance@CDesktopManager@@0PEAV1@EA ; CDesktopManager * CDesktopManager::s_pDesktopManagerInstance
    mov     rcx, [rcx+0E0h] ; this
    call    ?RegisterIconicRepresentation@CIconicBitmapRegistry@@QEAAJPEAVCWindowIconic@@PEAW4IconicRepresentationType@@@Z ; CIconicBitmapRegistry::RegisterIconicRepresentation(CWindowIconic *,IconicRepresentationType *)
    xor     r8d, r8d
    mov     edx, dword ptr [rsp+48h+arg_0]
    mov     rcx, rdi
    call    ?SetRepresentationType@CWindowIconic@@QEAAJW4IconicRepresentationType@@_N@Z ; CWindowIconic::SetRepresentationType(IconicRepresentationType,bool)
    mov     ebx, eax
    test    eax, eax
    js      loc_18004D767
     */
    public const string INITIALIZE_PATTERN = "48 8B 0D ?? ?? ?? ?? 48 8B 89 E0 00 00 00 E8 ?? ?? ?? ?? 45 33 C0";

    /* void __fastcall CStoryboard::Abandon(CStoryboard *__hidden this)
     mov     rax, cs:?s_pDesktopManagerInstance@CDesktopManager@@0PEAV1@EA ; CDesktopManager * CDesktopManager::s_pDesktopManagerInstance
     movups  xmm0, xmmword ptr [rbx+30h]
     movdqu  [rsp+48h+var_18], xmm0
     xor     r9d, r9d
     lea     r8d, [r9+1]
     lea     rdx, [rsp+48h+var_18]
     mov     rcx, [rax+0A8h]
     call    ?OnSetAnimationClockTime@CAnimationClockCoordinator@@QEAAJU_GUID@@W4DWMPANIMATION_CLOCK_TIME@@PEBT_LARGE_INTEGER@@@Z ; CAnimationClockCoordinator::OnSetAnimationClockTime(_GUID,DWMPANIMATION_CLOCK_TIME,_LARGE_INTEGER const *)
     nop
     */
    public const string ABANDON_PATTERN = "48 8B 05 ?? ?? ?? ?? 0F 10 43 30";
}