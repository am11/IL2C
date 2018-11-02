#ifndef __System_Int64_H__
#define __System_Int64_H__

#pragma once

#include <il2c.h>

#ifdef __cplusplus
extern "C" {
#endif

/////////////////////////////////////////////////////////////
// System.Int64

typedef int64_t System_Int64;

typedef __System_ValueType_VTABLE_DECL__ __System_Int64_VTABLE_DECL__;

extern __System_Int64_VTABLE_DECL__ __System_Int64_VTABLE__;
extern IL2C_RUNTIME_TYPE_DECL __System_Int64_RUNTIME_TYPE__;

#define __System_Int64_IL2C_MarkHandler__ IL2C_DEFAULT_MARK_HANDLER

extern /* virtual */ System_String* System_Int64_ToString(int64_t* this__);
extern /* virtual */ int32_t System_Int64_GetHashCode(int64_t* this__);
extern bool System_Int64_Equals(int64_t* this__, int64_t obj);
extern /* virtual */ bool System_Int64_Equals_1(int64_t* this__, System_Object* obj);
extern /* static */ bool System_Int64_TryParse(System_String* s, int64_t* result);

#ifdef __cplusplus
}
#endif

#endif
