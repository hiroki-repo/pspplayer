// ----------------------------------------------------------------------------
// PSP Player Emulation Suite
// Copyright (C) 2006 Ben Vanik (noxa)
// Licensed under the LGPL - see License.txt in the project root for details
// ----------------------------------------------------------------------------

#pragma once

// When enabled, code dealing with certain critical areas that may later
// be accessed from multiple threads will be gaurded with locks
//#define MULTITHREADED

// Runtime statistic generation - will slow things down - this is needed for IPS
// information and such
#define STATISTICS

// Gather and print out statistics about syscalls
#define SYSCALLSTATS

// When defined the native syscalls implemented in R4000BiosStubs.cpp will be used
// when allowed by the loaded BIOS. This may cause bugs, but should be a big
// win for performance
#define OVERRIDESYSCALLS

// Define to support the native video interface
#define NATIVEVIDEOINTERFACE

// -- TRACE -- Options live in TraceOptions.h

// ---------------------- Debug options -------------------------------------
#ifdef _DEBUG

// When enabled and _DEBUG is defined, an assembly listing for each block generated will be
// written to the defined path. It is overwritten during each block gen, so make sure to
// set a breakpoint at the end!
//#define GENECHOFILE "C:\\Dev\\Noxa.Emulation\\trunk\\debug\\gen.txt"

// When defined, the echo file will be cleared after each generated block
#define CLEARECHOFILE

// When defined with GENECHOFILE, really verbose messages will be added to the file
#define VERBOSEANNOTATE

// When defined, generation info will be emitted
//#define GENDEBUG

// When defined with _DEBUG, each instruction executed will print itself to debug out
//#define RUNTIMEDEBUG
//#define RUNTIMEREGS

// Emit callstack information
// Depending on the size of the stack (number of elements), context switches could get slow
#define CALLSTACKS
#define CALLSTACKSIZE	512

#endif

// -- macro hacks --
#ifndef STATISTICS
#undef SYSCALLSTATS
#endif
#ifndef GENECHOFILE
#undef CLEARECHOFILE
#undef VERBOSEANNOTATE
#endif
#ifndef RUNTIMEDEBUG
#undef RUNTIMEREGS
#endif
