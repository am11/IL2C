#ifndef __System_UInt16_H__
#define __System_UInt16_H__

#pragma once

#include <il2c.h>

#ifdef __cplusplus
extern "C" {
#endif

/////////////////////////////////////////////////////////////
// System.UInt16

typedef uint16_t System_UInt16;

typedef __System_ValueType_VTABLE_DECL__ __System_UInt16_VTABLE_DECL__;

extern __System_UInt16_VTABLE_DECL__ __System_UInt16_VTABLE__;
extern IL2C_RUNTIME_TYPE_DECL __System_UInt16_RUNTIME_TYPE__;

#define __System_UInt16_IL2C_MarkHandler__ IL2C_DEFAULT_MARK_HANDLER

extern /* virtual */ System_String* System_UInt16_ToString(uint16_t* this__);
extern /* virtual */ int32_t System_UInt16_GetHashCode(uint16_t* this__);
extern bool System_UInt16_Equals(uint16_t* this__, uint16_t obj);
extern /* virtual */ bool System_UInt16_Equals_1(uint16_t* this__, System_Object* obj);
extern /* static */ bool System_UInt16_TryParse(System_String* s, uint16_t* result);

#ifdef __cplusplus
}
#endif

#endif
