// ----------------------------------------------------------------------------
// PSP Player Emulation Suite
// Copyright (C) 2006 Ben Vanik (noxa)
// Licensed under the LGPL - see License.txt in the project root for details
// ----------------------------------------------------------------------------

#include "StdAfx.h"
#include "R4000Generator.h"
#include "R4000Cpu.h"
#include "R4000Core.h"
#include "R4000Memory.h"
#include "R4000GenContext.h"

#include "CodeGenerator.h"

// When true, a breakpoint will be inserted on a bad read
#define BREAKONINVALIDACCESS

using namespace System::Diagnostics;
using namespace Noxa::Emulation::Psp;
using namespace Noxa::Emulation::Psp::CodeGen;
using namespace Noxa::Emulation::Psp::Cpu;

#define g context->Generator

extern R4000Ctx* _cpuCtx;

void EmitAddressTranslation( R4000Generator *gen )
{
	gen->and( gen->eax, 0x3FFFFFFF );
}

// R4000Controller.cpp
int ErrorDebugBreak( uint pc );

int __readMemoryThunk( uint pc, uint targetAddress )
{
#ifdef DEBUGGING
	Diag::Instance->Emulator->CurrentInstance->Cpu->Memory->MemorySystem->DumpMainMemory( "readErrorThunk.bin" );
	R4000Ctx* ctx = _cpuCtx;
	MemoryError^ error = gcnew MemoryError( MemoryErrorCode::InvalidRead, pc, targetAddress, 4 );
	if( Diag::ThrowError( error ) == true )
	{
		int breakResult = ErrorDebugBreak( pc );
	}
#endif
	//return R4000Cpu::GlobalCpu->Memory->ReadWord( targetAddress );
	return 0;
}

void __writeMemoryThunk( uint pc, uint targetAddress, uint width, uint value )
{
#ifdef DEBUGGING
	Diag::Instance->Emulator->CurrentInstance->Cpu->Memory->MemorySystem->DumpMainMemory( "writeErrorThunk.bin" );
	R4000Ctx* ctx = _cpuCtx;
	MemoryError^ error = gcnew MemoryError( MemoryErrorCode::InvalidWrite, pc, targetAddress, ( byte )width, value );
	if( Diag::ThrowError( error ) == true )
	{
		int breakResult = ErrorDebugBreak( pc );
	}
#endif
	//R4000Cpu::GlobalCpu->Memory->WriteWord( targetAddress, width, value );
}

extern int TriggerMemoryBreakpoint( uint pc, bool isRead, int bpId );

#pragma unmanaged

#define MAXIMUM_MEMORY_BREAKPOINTS 128
int readBreakpointCount = 0;
int writeBreakpointCount = 0;
uint readBreakpoints[ MAXIMUM_MEMORY_BREAKPOINTS ] = { -1 };
uint writeBreakpoints[ MAXIMUM_MEMORY_BREAKPOINTS ] = { -1 };
int readBreakpointIds[ MAXIMUM_MEMORY_BREAKPOINTS ];
int writeBreakpointIds[ MAXIMUM_MEMORY_BREAKPOINTS ];

void __memoryBreakpointCheck( uint pc, uint targetAddress, bool isRead )
{
	int* breakpointIds = ( isRead == true ) ? readBreakpointIds : writeBreakpointIds;
	uint* breakpoints = ( isRead == true ) ? readBreakpoints : writeBreakpoints;
	int realCount = ( isRead == true ) ? readBreakpointCount : writeBreakpointCount;
	for( int n = 0, validCount = 0; n < MAXIMUM_MEMORY_BREAKPOINTS, validCount < realCount; n++ )
	{
		if( breakpoints[ n ] == targetAddress )
		{
			// Breakpoint hit!
			TriggerMemoryBreakpoint( pc, isRead, breakpointIds[ n ] );
			break;
		}
		else if( breakpoints[ n ] >= 0 )
			validCount++;
	}
}

#pragma managed

void AddMemoryBreakpoint( int id, uint address, bool isRead )
{
	if( isRead == true )
		readBreakpointCount++;
	else
		writeBreakpointCount++;
	int* breakpointIds = ( isRead == true ) ? readBreakpointIds : writeBreakpointIds;
	uint* breakpoints = ( isRead == true ) ? readBreakpoints : writeBreakpoints;
	for( int n = 0; n < MAXIMUM_MEMORY_BREAKPOINTS; n++ )
	{
		if( breakpoints[ n ] == -1 )
		{
			breakpointIds[ n ] = id;
			breakpoints[ n ] = address;
			break;
		}
	}
}

void RemoveMemoryBreakpoint( uint address, bool isRead )
{
	if( isRead == true )
		readBreakpointCount--;
	else
		writeBreakpointCount--;
	int* breakpointIds = ( isRead == true ) ? readBreakpointIds : writeBreakpointIds;
	uint* breakpoints = ( isRead == true ) ? readBreakpoints : writeBreakpoints;
	for( int n = 0; n < MAXIMUM_MEMORY_BREAKPOINTS; n++ )
	{
		if( breakpoints[ n ] == address )
		{
			breakpointIds[ n ] = -1;
			breakpoints[ n ] = -1;
			break;
		}
	}
}

// EAX = address in guest space, result in EAX
void EmitAddressLookup( R4000GenContext^ context, int address, bool isRead )
{
	g->and( EAX, 0x3FFFFFFF );

#ifdef DEBUGGING
	Label* noBreakpoints = g->DefineLabel();
	if( isRead == true )
		g->cmp( g->dword_ptr[ &readBreakpointCount ], ( uint )0 );
	else
		g->cmp( g->dword_ptr[ &writeBreakpointCount ], ( uint )0 );
	g->jz( noBreakpoints );
	g->push( EAX );
	g->push( EBX );
	g->push( ECX );
	g->push( EDX );
	g->push( ( uint )isRead );
	g->push( EAX );
	g->push( ( uint )( address - 4 ) );
	g->mov( EBX, (int)&__memoryBreakpointCheck );
	g->call( EBX );
	g->add( ESP, 12 );
	g->pop( EDX );
	g->pop( ECX );
	g->pop( EBX );
	g->pop( EAX );
	g->MarkLabel( noBreakpoints );
#endif

	Label* l1 = g->DefineLabel();
	Label* l2 = g->DefineLabel();
	Label* l3 = g->DefineLabel();
	Label* l4 = g->DefineLabel();

	// if < 0x0800000 && > MainMemoryBound, skip and check framebuffer or do a read from method
	g->cmp( EAX, MainMemoryBase );
	g->jb( l1 );
	g->cmp( EAX, MainMemoryBound );
	g->ja( l1 );

	// else, do a direct main memory read
	g->sub( EAX, MainMemoryBase ); // get to offset in main memory
	g->add( EAX, (int)context->MainMemory );
	g->jmp( l4 );

	// case to handle read call
	g->MarkLabel( l1 );

	// if < 0x0400000 && > VideoMemoryBound, skip and do a read from method
	g->cmp( EAX, VideoMemoryBase );
	g->jb( l2 );
	// Test for shadowing? ECX = address fixed up
	g->mov( ECX, EAX );
	g->and( ECX, 0x041FFFFF );
	g->cmp( ECX, VideoMemoryBound );
	g->ja( l2 );

	// else, do a direct fb read
	g->sub( ECX, VideoMemoryBase );
	g->add( ECX, (int)context->FrameBuffer );
	g->mov( EAX, ECX );
	g->jmp( l4 );

	g->MarkLabel( l2 );

#ifdef SUPPORTSCRATCHPAD
	// if < ScratchPadBase && > ScratchPadBound, skip and check framebuffer or do a read from method
	g->cmp( EAX, ScratchPadBase );
	g->jb( l3 );
	g->cmp( EAX, ScratchPadBound );
	g->ja( l3 );

	// else, do a direct scratch pad read
	g->sub( EAX, ScratchPadBase ); // get to offset in main memory
	g->add( EAX, (int)context->ScratchPad );
	g->jmp( l4 );
#endif

	g->MarkLabel( l3 );

	g->push( EAX );
	g->push( ( uint )( address - 4 ) );
#ifdef BREAKONINVALIDACCESS
	g->int3();
#endif
	g->mov( EBX, (int)&__readMemoryThunk );
	g->call( EBX );
	g->add( ESP, 8 );
	g->mov( EAX, 0 );

	// done
	g->MarkLabel( l4 );
}

// EAX = address, result in EAX
void EmitDirectMemoryRead( R4000GenContext^ context, int address )
{
#ifdef DEBUGGING
	Label* noBreakpoints = g->DefineLabel();
	g->cmp( g->dword_ptr[ &readBreakpointCount ], ( uint )0 );
	g->jz( noBreakpoints );
	g->push( EAX );
	g->push( EBX );
	g->push( ECX );
	g->push( EDX );
	g->push( ( uint )1 );
	g->push( EAX );
	g->push( ( uint )( address - 4 ) );
	g->mov( EBX, (int)&__memoryBreakpointCheck );
	g->call( EBX );
	g->add( ESP, 12 );
	g->pop( EDX );
	g->pop( ECX );
	g->pop( EBX );
	g->pop( EAX );
	g->MarkLabel( noBreakpoints );
#endif

	Label* l1 = g->DefineLabel();
	Label* l2 = g->DefineLabel();
	Label* l3 = g->DefineLabel();
	Label* l4 = g->DefineLabel();

	// if < 0x0800000 && > MainMemoryBound, skip and check framebuffer or do a read from method
	g->cmp( EAX, MainMemoryBase );
	g->jb( l1 );
	g->cmp( EAX, MainMemoryBound );
	g->ja( l1 );

	// else, do a direct main memory read
	g->sub( EAX, MainMemoryBase ); // get to offset in main memory
	g->mov( EAX, g->dword_ptr[ EAX + (int)context->MainMemory ] );
	g->jmp( l4 );

	// case to handle read call
	g->MarkLabel( l1 );

	// if < 0x0400000 && > VideoMemoryBound, skip and do a read from method
	g->cmp( EAX, VideoMemoryBase );
	g->jb( l2 );
	// Test for shadowing? ECX = address fixed up
	g->mov( ECX, EAX );
	g->and( ECX, 0x041FFFFF );
	g->cmp( ECX, VideoMemoryBound );
	g->ja( l2 );

	// else, do a direct fb read
	g->sub( ECX, VideoMemoryBase );
	g->mov( EAX, g->dword_ptr[ ECX + (int)context->FrameBuffer ] );
	g->jmp( l4 );

	g->MarkLabel( l2 );

#ifdef SUPPORTSCRATCHPAD
	// if < ScratchPadBase && > ScratchPadBound, skip and check framebuffer or do a read from method
	g->cmp( EAX, ScratchPadBase );
	g->jb( l3 );
	g->cmp( EAX, ScratchPadBound );
	g->ja( l3 );

	// else, do a direct scratch pad read
	g->sub( EAX, ScratchPadBase ); // get to offset in main memory
	g->mov( EAX, g->dword_ptr[ EAX + (int)context->ScratchPad ] );
	g->jmp( l4 );
#endif

	g->MarkLabel( l3 );

	g->push( EAX );
	g->push( ( uint )( address - 4 ) );
#ifdef BREAKONINVALIDACCESS
	g->int3();
#endif
	g->mov( EBX, (int)&__readMemoryThunk );
	g->call( EBX );
	g->add( ESP, 8 );

	// done
	g->MarkLabel( l4 );
}

// EAX = address, EBX = data
void EmitDirectMemoryWrite( R4000GenContext^ context, int address, int width )
{
#ifdef DEBUGGING
	Label* noBreakpoints = g->DefineLabel();
	g->cmp( g->dword_ptr[ &writeBreakpointCount ], ( uint )0 );
	g->jz( noBreakpoints );
	g->push( EAX );
	g->push( EBX );
	g->push( ECX );
	g->push( EDX );
	g->push( ( uint )0 );
	g->push( EAX );
	g->push( ( uint )( address - 4 ) );
	g->mov( EBX, (int)&__memoryBreakpointCheck );
	g->call( EBX );
	g->add( ESP, 12 );
	g->pop( EDX );
	g->pop( ECX );
	g->pop( EBX );
	g->pop( EAX );
	g->MarkLabel( noBreakpoints );
#endif

	Label* l1 = g->DefineLabel();
	Label* l2 = g->DefineLabel();
	Label* l3 = g->DefineLabel();
	Label* l4 = g->DefineLabel();

	// if < 0x0800000 && > MainMemoryBound, skip and do a write from method
	g->cmp( EAX, MainMemoryBase );
	g->jb( l1 );
	g->cmp( EAX, MainMemoryBound );
	g->ja( l1 );

	// else, do a direct read
	g->sub( EAX, MainMemoryBase ); // get to offset in main memory
	switch( width )
	{
	case 1:
		g->mov( g->byte_ptr[ EAX + (int)context->MainMemory ], BL );
		break;
	case 2:
		g->mov( g->word_ptr[ EAX + (int)context->MainMemory ], BX );
		break;
	case 4:
		g->mov( g->dword_ptr[ EAX + (int)context->MainMemory ], EBX );
		break;
	}
	g->jmp( l4 );

	// case to handle read call
	g->MarkLabel( l1 );

	// if < 0x0400000 && > VideoMemoryBound, skip and do a read from method
	g->cmp( EAX, VideoMemoryBase );
	g->jb( l2 );
	// Test for shadowing? ECX = address fixed up
	g->mov( ECX, EAX );
	g->and( ECX, 0x041FFFFF );
	g->cmp( ECX, VideoMemoryBound );
	g->ja( l2 );
	
	// else, do a direct fb read
	g->sub( ECX, VideoMemoryBase ); // get to offset in fb
	switch( width )
	{
	case 1:
		g->mov( g->byte_ptr[ ECX + (int)context->FrameBuffer ], BL );
		break;
	case 2:
		g->mov( g->word_ptr[ ECX + (int)context->FrameBuffer ], BX );
		break;
	case 4:
		g->mov( g->dword_ptr[ ECX + (int)context->FrameBuffer ], EBX );
		break;
	}
	g->jmp( l4 );

	g->MarkLabel( l2 );

#ifdef SUPPORTSCRATCHPAD
	g->cmp( EAX, ScratchPadBase );
	g->jb( l3 );
	g->cmp( EAX, ScratchPadBound );
	g->ja( l3 );

	// else, do a direct read
	g->sub( EAX, ScratchPadBase ); // get to offset in main memory
	switch( width )
	{
	case 1:
		g->mov( g->byte_ptr[ EAX + (int)context->ScratchPad ], BL );
		break;
	case 2:
		g->mov( g->word_ptr[ EAX + (int)context->ScratchPad ], BX );
		break;
	case 4:
		g->mov( g->dword_ptr[ EAX + (int)context->ScratchPad ], EBX );
		break;
	}
	g->jmp( l4 );
#endif

	g->MarkLabel( l3 );

	switch( width )
	{
	case 1:
		g->movzx( EBX, BL );
		break;
	case 2:
		g->movzx( EBX, BX );
		break;
	}
	g->push( EBX );
	g->push( ( uint )width );
	g->push( EAX );
	g->push( ( uint )( address - 4 ) );
#ifdef BREAKONINVALIDACCESS
	g->int3();
#endif
	g->mov( EBX, (int)&__writeMemoryThunk );
	g->call( EBX );
	g->add( ESP, 16 );

	// done
	g->MarkLabel( l4 );
}

GenerationResult LB( R4000GenContext^ context, int pass, int address, uint code, byte opcode, byte rs, byte rt, ushort imm )
{
	if( pass == 0 )
	{
	}
	else if( pass == 1 )
	{
		g->mov( EAX, MREG( CTX, rs ) );
		if( imm != 0 )
			g->add( EAX, SE( imm ) );
		EmitAddressTranslation( g );

		EmitDirectMemoryRead( context, address );

		// Byte mask & sign extend
		g->movsx( EAX, AL );

		g->mov( MREG( CTX, rt ), EAX );
	}
	return GenerationResult::Success;
}

GenerationResult LH( R4000GenContext^ context, int pass, int address, uint code, byte opcode, byte rs, byte rt, ushort imm )
{
	if( pass == 0 )
	{
	}
	else if( pass == 1 )
	{
		g->mov( EAX, MREG( CTX, rs ) );
		if( imm != 0 )
			g->add( EAX, SE( imm ) );
		EmitAddressTranslation( g );

		EmitDirectMemoryRead( context, address );

		// Short mask & sign extend
		g->movsx( EAX, AX );

		g->mov( MREG( CTX, rt ), EAX );
	}
	return GenerationResult::Success;
}

GenerationResult LWL( R4000GenContext^ context, int pass, int address, uint code, byte opcode, byte rs, byte rt, ushort imm )
{
	if( pass == 0 )
	{
	}
	else if( pass == 1 )
	{
		//LOADCTXBASE( EDX );
		g->mov( EAX, MREG( CTX, rs ) );
		if( imm != 0 )
			g->add( EAX, SE( imm ) );
		EmitAddressTranslation( g );
		g->mov( ECX, EAX ); // store address in ECX

		// Read existing data in to EAX - dword aligned
		g->and( EAX, 0xFFFFFFFC );
		EmitDirectMemoryRead( context, address );

		// Build final data - done as follows:
		/*
		if( ebx == 0 )
			final = ( oldreg & 0x00FFFFFF ) | ( ( mem << 24 ) & 0xFF000000 );	- m 24
		else if( ebx == 1 )
			final = ( oldreg & 0x0000FFFF ) | ( ( mem << 16 ) & 0xFFFF0000 );	- m 16
		else if( ebx == 2 )
			final = ( oldreg & 0x000000FF ) | ( ( mem << 8 ) & 0xFFFFFF00 );	- m 8
		else if( ebx == 3 )
			final = ( oldreg & 0x00000000 ) | ( ( mem << 0 ) & 0xFFFFFFFF );	- m 0	
		*/

		// With this, we do:
		// ecx = [0...3] (from addr)
		// ecx = xor 3 (invert bits to make easier)
		// ecx *= 8 (so [0...24])
		// ebx = 0xFFFFFFFF << cl
		// ebx is now the mask for the memory!
		// invert to get mask for oldreg!

		g->and( ECX, 0x3 ); // ecx = address (in ecx) & 0x3
		g->xor( ECX, 0x3 );
		g->shl( ECX, 3 );	// *= 8
		g->mov( EBX, 0xFFFFFFFF );
		g->shl( EBX, CL );

		g->shl( EAX, CL );		// shift memory over to match mask
		g->and( EAX, EBX );		// mem (in eax) &= mask in ebx

		g->not( EBX );			// invert mask for oldreg
		g->mov( ECX, MREG( CTX, rt ) );
		g->and( ECX, EBX );		// oldreg (in ecx) &= mask in ebx

		g->or( EAX, ECX );		// mem | oldreg

		g->mov( MREG( CTX, rt ), EAX );
	}
	return GenerationResult::Success;
}

GenerationResult LW( R4000GenContext^ context, int pass, int address, uint code, byte opcode, byte rs, byte rt, ushort imm )
{
	if( pass == 0 )
	{
	}
	else if( pass == 1 )
	{
		g->mov( EAX, MREG( CTX, rs ) );
		if( imm != 0 )
			g->add( EAX, SE( imm ) );
		EmitAddressTranslation( g );

		EmitDirectMemoryRead( context, address );

		g->mov( MREG( CTX, rt ), EAX );
	}
	return GenerationResult::Success;
}

GenerationResult LBU( R4000GenContext^ context, int pass, int address, uint code, byte opcode, byte rs, byte rt, ushort imm )
{
	if( pass == 0 )
	{
	}
	else if( pass == 1 )
	{
		g->mov( EAX, MREG( CTX, rs ) );
		if( imm != 0 )
			g->add( EAX, SE( imm ) );
		EmitAddressTranslation( g );

		EmitDirectMemoryRead( context, address );

		// Byte mask
		g->and( EAX, 0x000000FF );

		g->mov( MREG( CTX, rt ), EAX );
	}
	return GenerationResult::Success;
}

GenerationResult LHU( R4000GenContext^ context, int pass, int address, uint code, byte opcode, byte rs, byte rt, ushort imm )
{
	if( pass == 0 )
	{
	}
	else if( pass == 1 )
	{
		g->mov( EAX, MREG( CTX, rs ) );
		if( imm != 0 )
			g->add( EAX, SE( imm ) );
		EmitAddressTranslation( g );

		EmitDirectMemoryRead( context, address );

		// Short mask
		g->and( EAX, 0x0000FFFF );

		g->mov( MREG( CTX, rt ), EAX );
	}
	return GenerationResult::Success;
}

GenerationResult LWR( R4000GenContext^ context, int pass, int address, uint code, byte opcode, byte rs, byte rt, ushort imm )
{
	if( pass == 0 )
	{
	}
	else if( pass == 1 )
	{
		g->mov( EAX, MREG( CTX, rs ) );
		if( imm != 0 )
			g->add( EAX, SE( imm ) );
		EmitAddressTranslation( g );
		g->mov( ECX, EAX ); // store address in ECX

		// Read existing data in to EAX - dword aligned
		g->and( EAX, 0xFFFFFFFC );
		EmitDirectMemoryRead( context, address );

		// Build final data - done as follows:
		/*
		if( ebx == 0 )
			final = ( oldreg & 0x00000000 ) | ( ( mem >> 0 ) & 0xFFFFFFFF );	- m 0
		else if( ebx == 1 )
			final = ( oldreg & 0xFF000000 ) | ( ( mem >> 8 ) & 0x00FFFFFF );	- m 8
		else if( ebx == 2 )
			final = ( oldreg & 0xFFFF0000 ) | ( ( mem >> 16 ) & 0x0000FFFF );	- m 16
		if( ebx == 3 )
			final = ( oldreg & 0xFFFFFF00 ) | ( ( mem >> 24 ) & 0x000000FF );	- m 24
		*/

		// With this, we do:
		// ecx = [0...3] (from addr)
		// ecx *= 8 (so [0...24])
		// ebx = 0xFFFFFFFF >> cl
		// ebx is now the mask for the memory!
		// invert to get mask for oldreg!

		g->and( ECX, 0x3 ); // ecx = address (in ecx) & 0x3
		g->shl( ECX, 3 );	// *= 8
		g->mov( EBX, 0xFFFFFFFF );
		g->shr( EBX, CL );

		g->shr( EAX, CL );		// shift to match mask
		g->and( EAX, EBX );		// mem (in eax) &= mask in ebx

		g->not( EBX );			// invert mask for oldreg
		g->mov( ECX, MREG( CTX, rt ) );
		g->and( ECX, EBX );		// oldreg (in ecx) &= mask in ebx

		g->or( EAX, ECX );		// mem | oldreg

		g->mov( MREG( CTX, rt ), EAX );
	}
	return GenerationResult::Success;
}

GenerationResult SB( R4000GenContext^ context, int pass, int address, uint code, byte opcode, byte rs, byte rt, ushort imm )
{
	if( pass == 0 )
	{
	}
	else if( pass == 1 )
	{
		g->mov( EAX, MREG( CTX, rs ) );
		if( imm != 0 )
			g->add( EAX, SE( imm ) );
		EmitAddressTranslation( g );
		g->mov( EBX, MREG( CTX, rt ) );

		EmitDirectMemoryWrite( context, address, 1 );
	}
	return GenerationResult::Success;
}

GenerationResult SH( R4000GenContext^ context, int pass, int address, uint code, byte opcode, byte rs, byte rt, ushort imm )
{
	if( pass == 0 )
	{
	}
	else if( pass == 1 )
	{
		g->mov( EAX, MREG( CTX, rs ) );
		if( imm != 0 )
			g->add( EAX, SE( imm ) );
		EmitAddressTranslation( g );
		g->mov( EBX, MREG( CTX, rt ) );

		EmitDirectMemoryWrite( context, address, 2 );
	}
	return GenerationResult::Success;
}

GenerationResult SWL( R4000GenContext^ context, int pass, int address, uint code, byte opcode, byte rs, byte rt, ushort imm )
{
	if( pass == 0 )
	{
	}
	else if( pass == 1 )
	{
		//LOADCTXBASE( EDX );
		g->mov( EAX, MREG( CTX, rs ) );
		if( imm != 0 )
			g->add( EAX, SE( imm ) );
		EmitAddressTranslation( g );
		g->mov( ECX, EAX ); // store address in ECX

		// Read existing data in to EAX - dword aligned
		g->and( EAX, 0xFFFFFFFC );
		EmitDirectMemoryRead( context, address );

		// Build final data - done as follows:
		/*
		if( ebx == 0 )
			final = ( ( reg >> 24 ) & 0x000000FF ) | ( mem & 0xFFFFFF00 );	- m 24
		else if( ebx == 1 )
			final = ( ( reg >> 16 ) & 0x0000FFFF ) | ( mem & 0xFFFF0000 );	- m 16
		else if( ebx == 2 )
			final = ( ( reg >> 8 ) & 0x00FFFFFF ) | ( mem & 0xFF000000 );	- m 8
		else if( ebx == 3 )
			final = ( ( reg >> 0 ) & 0xFFFFFFFF ) | ( mem & 0x00000000 );	- m 0
		*/

		// With this, we do:
		// ecx = [0...3] (from addr)
		// ecx = xor 3 (invert to make easier)
		// ecx *= 8 (so [0...24])
		// ebx = 0xFFFFFFFF >> cl
		// ebx is now the mask for the reg!
		// invert to get mask for memory!

		// NOTE: we technically don't need to and the register here,
		// as shifting will do it for us!

		//g->int3();
		g->and( ECX, 0x3 ); // ecx = address (in ecx) & 0x3
		g->xor( ECX, 3 );	// invert
		g->shl( ECX, 3 );	// *= 8
		g->mov( EBX, 0xFFFFFFFF );
		g->shr( EBX, CL );

		g->mov( EDX, MREG( CTX, rt ) );
		g->shr( EDX, CL );		// shift to match mask
		g->and( EDX, EBX );		// reg (in ecx) &= mask in ebx

		g->not( EBX );			// invert mask for memory
		g->and( EAX, EBX );		// mem (in eax) &= mask in ebx

		g->or( EAX, EDX );		// mem | oldreg

		g->mov( EBX, EAX );
		
		// Reget the address!
		g->mov( EAX, MREG( CTX, rs ) );
		if( imm != 0 )
			g->add( EAX, SE( imm ) );
		EmitAddressTranslation( g );
		g->and( EAX, 0xFFFFFFFC );		// word align
		// Write EBX to address EAX
		EmitDirectMemoryWrite( context, address, 4 );
	}
	return GenerationResult::Success;
}

GenerationResult SW( R4000GenContext^ context, int pass, int address, uint code, byte opcode, byte rs, byte rt, ushort imm )
{
	if( pass == 0 )
	{
	}
	else if( pass == 1 )
	{
		g->mov( EAX, MREG( CTX, rs ) );
		if( imm != 0 )
			g->add( EAX, SE( imm ) );
		EmitAddressTranslation( g );
		g->mov( EBX, MREG( CTX, rt ) );

		EmitDirectMemoryWrite( context, address, 4 );
	}
	return GenerationResult::Success;
}

GenerationResult SWR( R4000GenContext^ context, int pass, int address, uint code, byte opcode, byte rs, byte rt, ushort imm )
{
	if( pass == 0 )
	{
	}
	else if( pass == 1 )
	{
		//LOADCTXBASE( EDX );
		g->mov( EAX, MREG( CTX, rs ) );
		if( imm != 0 )
			g->add( EAX, SE( imm ) );
		EmitAddressTranslation( g );
		g->mov( ECX, EAX ); // store address in ECX

		// Read existing data in to EAX - dword aligned
		g->and( EAX, 0xFFFFFFFC );
		EmitDirectMemoryRead( context, address );

		// Build final data - done as follows:
		/*
		if( ebx == 0 )
			final = ( ( reg << 0 ) & 0xFFFFFFFF ) | ( mem & 0x00000000 );	- m 24
		else if( ebx == 1 )
			final = ( ( reg << 8 ) & 0xFFFFFF00 ) | ( mem & 0x000000FF );	- m 16
		else if( ebx == 2 )
			final = ( ( reg << 16 ) & 0xFFFF0000 ) | ( mem & 0x0000FFFF );	- m 8
		else if( ebx == 3 )
			final = ( ( reg << 24 ) & 0xFF000000 ) | ( mem & 0x00FFFFFF );	- m 0
		*/

		// With this, we do:
		// ecx = [0...3] (from addr)
		// ecx *= 8 (so [0...24])
		// ebx = 0xFFFFFFFF << cl
		// ebx is now the mask for the reg!
		// invert to get mask for memory!

		// NOTE: we technically don't need to and the register here,
		// as shifting will do it for us!

		//g->int3();
		g->and( ECX, 0x3 ); // ecx = address (in ecx) & 0x3
		g->shl( ECX, 3 );	// *= 8
		g->mov( EBX, 0xFFFFFFFF );
		g->shl( EBX, CL );

		g->mov( EDX, MREG( CTX, rt ) );
		g->shl( EDX, CL );		// shift to match mask
		g->and( EDX, EBX );		// reg (in ecx) &= mask in ebx

		g->not( EBX );			// invert mask for memory
		g->and( EAX, EBX );		// mem (in eax) &= mask in ebx

		g->or( EAX, EDX );		// mem | oldreg

		g->mov( EBX, EAX );

		// Reget the address!
		g->mov( EAX, MREG( CTX, rs ) );
		if( imm != 0 )
			g->add( EAX, SE( imm ) );
		EmitAddressTranslation( g );
		g->and( EAX, 0xFFFFFFFC );		// word align
		// Write EBX to address EAX
		EmitDirectMemoryWrite( context, address, 4 );
	}
	return GenerationResult::Success;
}

GenerationResult CACHE( R4000GenContext^ context, int pass, int address, uint code, byte opcode, byte rs, byte rt, ushort imm )
{
	// Not implemented on purpose
	if( pass == 0 )
	{
	}
	else if( pass == 1 )
	{
	}
	return GenerationResult::Success;
}

GenerationResult LL( R4000GenContext^ context, int pass, int address, uint code, byte opcode, byte rs, byte rt, ushort imm )
{
	if( pass == 0 )
	{
	}
	else if( pass == 1 )
	{
	}
	return GenerationResult::Invalid;
}

GenerationResult SC( R4000GenContext^ context, int pass, int address, uint code, byte opcode, byte rs, byte rt, ushort imm )
{
	if( pass == 0 )
	{
	}
	else if( pass == 1 )
	{
	}
	return GenerationResult::Invalid;
}

GenerationResult LWCz( R4000GenContext^ context, int pass, int address, uint code, byte opcode, byte rs, byte rt, ushort imm )
{
	byte cop = ( byte )( opcode & 0x3 );
	if( ( cop == 0 ) || ( cop == 2 ) )
		return GenerationResult::Invalid;

	if( pass == 0 )
	{
	}
	else if( pass == 1 )
	{
		g->mov( EAX, MREG( CTX, rs ) );
		if( imm != 0 )
			g->add( EAX, SE( imm ) );
		EmitAddressTranslation( g );

		EmitDirectMemoryRead( context, address );

		switch( cop )
		{
		case 0:
			//g->mov( MCP0REG( rt ), EAX );
			break;
		case 1:
			g->mov( MCP1REG( CTX, rt, 0 ), EAX );
			break;
		case 2:
			//g->mov( MCP2REG( rt ), EAX );
			break;
		}
	}
	return GenerationResult::Success;
}

GenerationResult SWCz( R4000GenContext^ context, int pass, int address, uint code, byte opcode, byte rs, byte rt, ushort imm )
{
	byte cop = ( byte )( opcode & 0x3 );
	if( ( cop == 0 ) || ( cop == 2 ) )
		return GenerationResult::Invalid;

	if( pass == 0 )
	{
	}
	else if( pass == 1 )
	{
		g->mov( EAX, MREG( CTX, rs ) );
		if( imm != 0 )
			g->add( EAX, SE( imm ) );
		EmitAddressTranslation( g );
		
		switch( cop )
		{
		case 0:
			break;
		case 1:
			g->mov( EBX, MCP1REG( CTX, rt, 0 ) );
			break;
		case 2:
			break;
		}

		EmitDirectMemoryWrite( context, address, 4 );
	}
	return GenerationResult::Success;
}
