#ifndef System_Console_H__
#define System_Console_H__

#pragma once

#include <il2c.h>

#ifdef __cplusplus
extern "C" {
#endif

/////////////////////////////////////////////////////////////
// System.Console

IL2C_DECLARE_RUNTIME_TYPE(System_Console);

extern /* static */ void System_Console_Write_9(System_String* value);
extern /* static */ void System_Console_WriteLine(void);
extern /* static */ void System_Console_WriteLine_6(int32_t value);
extern /* static */ void System_Console_WriteLine_10(System_String* value);

extern /* static */ System_String* System_Console_ReadLine(void);

#ifdef __cplusplus
}
#endif

#endif
