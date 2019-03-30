#include "il2c_private.h"

/////////////////////////////////////////////////////////////
// System.Threading.Thread

void System_Threading_Thread__ctor(System_Threading_Thread* this__, System_Threading_ThreadStart* start)
{
    il2c_assert(this__ != NULL);

    // TODO: ArgumentNullException
    il2c_assert(start != NULL);

    this__->start__ = (System_Delegate*)start;
    this__->rawHandle__ = -1;
}

void System_Threading_Thread__ctor_1(System_Threading_Thread* this__, System_Threading_ParameterizedThreadStart* start)
{
    il2c_assert(this__ != NULL);

    // TODO: ArgumentNullException
    il2c_assert(start != NULL);

    this__->start__ = (System_Delegate*)start;
    this__->rawHandle__ = -1;
}

extern IL2C_TLS_INDEX g_TlsIndex__;

void System_Threading_Thread_Finalize(System_Threading_Thread* this__)
{
    il2c_assert(this__ != NULL);

    if (il2c_likely__(this__->rawHandle__ != -1))
    {
        il2c_close_thread_handle__(this__->rawHandle__);
#if defined(_DEBUG)
        this__->rawHandle__ = -1;
        this__->id__ = 0;
        this__->start__ = NULL;
        this__->parameter__ = NULL;
#endif
    }
}

static IL2C_THREAD_ENTRY_POINT_RESULT_TYPE System_Threading_Thread_InternalEntryPoint(
    IL2C_THREAD_ENTRY_POINT_PARAMETER_TYPE parameter)
{
    il2c_assert(parameter != NULL);

    System_Threading_Thread* pThread = (System_Threading_Thread*)parameter;
    il2c_assert(pThread->vptr0__ == &System_Threading_Thread_VTABLE__);
    il2c_assert(il2c_isinst(pThread->start__, System_Threading_ThreadStart) != NULL);
    il2c_assert(pThread->parameter__ == NULL);

    // Set real thread id.
    pThread->id__ = il2c_get_current_thread_id__();

    // Save IL2C_THREAD_CONTEXT into tls.
    il2c_set_tls_value(g_TlsIndex__, (void*)&pThread->pFrame__);

    // It's naive for passing handle if startup with suspending not implemented. (pthread/FreeRTOS)
    while (pThread->rawHandle__ == -1);

    // Invoke delegate.
    // TODO: catch exception.
    System_Threading_ThreadStart_Invoke(
        (System_Threading_ThreadStart*)(pThread->start__));

#if defined(_DEBUG)
    il2c_set_tls_value(g_TlsIndex__, NULL);
#endif

    // Unregister GC root tracking.
    il2c_unregister_root_reference__(pThread, false);

    IL2C_THREAD_ENTRY_POINT_RETURN(0);
}

static IL2C_THREAD_ENTRY_POINT_RESULT_TYPE System_Threading_Thread_InternalEntryPointWithParameter(
    IL2C_THREAD_ENTRY_POINT_PARAMETER_TYPE parameter)
{
    il2c_assert(parameter != NULL);

    System_Threading_Thread* pThread = (System_Threading_Thread*)parameter;
    il2c_assert(pThread->vptr0__ == &System_Threading_Thread_VTABLE__);
    il2c_assert(il2c_isinst(pThread->start__, System_Threading_ParameterizedThreadStart) != NULL);

    // Set real thread id.
    pThread->id__ = il2c_get_current_thread_id__();

    // Save IL2C_THREAD_CONTEXT into tls.
    il2c_set_tls_value(g_TlsIndex__, (void*)&pThread->pFrame__);

    // It's naive for passing handle if startup with suspending not implemented. (pthread/FreeRTOS)
    while (pThread->rawHandle__ == -1);

    // Invoke delegate.
    // TODO: catch exception.
    System_Threading_ParameterizedThreadStart_Invoke(
        (System_Threading_ParameterizedThreadStart*)(pThread->start__),
        pThread->parameter__);

#if defined(_DEBUG)
    il2c_set_tls_value(g_TlsIndex__, NULL);
#endif

    // Unregister GC root tracking.
    il2c_unregister_root_reference__(pThread, false);

    IL2C_THREAD_ENTRY_POINT_RETURN(0);
}

void System_Threading_Thread_Start(System_Threading_Thread* this__)
{
    il2c_assert(this__ != NULL);

    // TODO: InvalidOperationException? (Auto attached managed thread)
    il2c_assert(this__->start__ != NULL);

    // TODO: ThreadStateException? (Already started)
    il2c_assert(this__->rawHandle__ == -1);

    // Register GC root tracking.
    il2c_register_root_reference__(this__, false);

    // Create (suspended if available) thread.
    intptr_t rawHandle = il2c_create_thread__(
        System_Threading_Thread_InternalEntryPoint, this__);

    // TODO: OutOfMemoryException
    il2c_assert(rawHandle >= 0);

    // It's naive for passing handle if startup with suspending not implemented. (pthread/FreeRTOS)
    this__->rawHandle__ = rawHandle;
    il2c_resume_thread__(rawHandle);
}

void System_Threading_Thread_Start_2(System_Threading_Thread* this__, System_Object* parameter)
{
    il2c_assert(this__ != NULL);

    // TODO: InvalidOperationException? (Auto attached managed thread)
    il2c_assert(this__->start__ != NULL);

    // TODO: ThreadStateException? (Already started)
    il2c_assert(this__->rawHandle__ == -1);

    // Register GC root tracking.
    il2c_register_root_reference__(this__, false);

    // Store parameter
    this__->parameter__ = parameter;

    // Create (suspended if available) thread.
    intptr_t rawHandle = il2c_create_thread__(
        System_Threading_Thread_InternalEntryPointWithParameter, this__);

    // TODO: OutOfMemoryException
    il2c_assert(rawHandle >= 0);

    // It's naive for passing handle if startup with suspending not implemented. (pthread/FreeRTOS)
    this__->rawHandle__ = rawHandle;
    il2c_resume_thread__(rawHandle);
}

void System_Threading_Thread_Join(System_Threading_Thread* this__)
{
    il2c_assert(this__ != NULL);
    il2c_assert(this__->rawHandle__ >= 0);
    il2c_assert(this__->start__ != NULL);

    il2c_join_thread__(this__->rawHandle__);
}

System_Threading_Thread* System_Threading_Thread_get_CurrentThread(void)
{
    // Get thread context.
#if defined(IL2C_USE_LINE_INFORMATION)
    IL2C_THREAD_CONTEXT* pThreadContext = il2c_acquire_thread_context__(__FILE__, __LINE__);
#else
    IL2C_THREAD_CONTEXT* pThreadContext = il2c_acquire_thread_context__();
#endif

    // Come from unoffsetted:
    return (System_Threading_Thread*)(((uint8_t*)pThreadContext) - offsetof(System_Threading_Thread, pFrame__));
}

void System_Threading_Thread_Sleep(int millisecondsTimeout)
{
    il2c_sleep((uint32_t)millisecondsTimeout);
}

/////////////////////////////////////////////////
// VTable and runtime type info declarations

static void System_Threading_Thread_MarkHandler__(System_Threading_Thread* thread)
{
    il2c_assert(thread != NULL);
    il2c_assert(thread->vptr0__ == &System_Threading_Thread_VTABLE__);

    // Check start and parameter field.
    if (il2c_likely__(thread->start__ != NULL))
    {
        il2c_default_mark_handler_for_objref__(thread->start__);
    }
    if (thread->parameter__ != NULL)
    {
        il2c_default_mark_handler_for_objref__(thread->parameter__);
    }

    ///////////////////////////////////////////////////////////////
    // Check IL2C_EXECUTION_FRAME.
    // It's important step for GC collecting sequence.
    // All method execution frame traversal begins this location.

    if (il2c_likely__(thread->pFrame__ != NULL))
    {
        il2c_default_mark_handler_for_tracking_information__(thread->pFrame__);
    }
}

System_Threading_Thread_VTABLE_DECL__ System_Threading_Thread_VTABLE__ = {
    0, // Adjustor offset
    (bool(*)(void*, System_Object*))System_Object_Equals,
    (void(*)(void*))System_Threading_Thread_Finalize,
    (int32_t(*)(void*))System_Object_GetHashCode,
    (System_String* (*)(void*))System_Object_ToString
};

IL2C_RUNTIME_TYPE_BEGIN(
    System_Threading_Thread,
    "System.Threading.Thread",
    IL2C_TYPE_REFERENCE | IL2C_TYPE_WITH_MARK_HANDLER,
    sizeof(System_Threading_Thread),
    System_Object,
    System_Threading_Thread_MarkHandler__,
    0)
IL2C_RUNTIME_TYPE_END();