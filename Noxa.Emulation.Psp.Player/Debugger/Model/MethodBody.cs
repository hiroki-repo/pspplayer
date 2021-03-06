// ----------------------------------------------------------------------------
// PSP Player Emulation Suite
// Copyright (C) 2006 Ben Vanik (noxa)
// Licensed under the LGPL - see License.txt in the project root for details
// ----------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using Noxa.Emulation.Psp.Debugging.DebugModel;

namespace Noxa.Emulation.Psp.Player.Debugger.Model
{
	class MethodBody
	{
		public readonly ModuleInfo Module;
		public string Name;
		public uint TotalLines;
		public BiosFunctionToken Function;

		public readonly uint Address;
		public readonly uint Length;

		public Instruction[] Instructions;

		public int LocalsSize;
		public Label[] Labels;

		public CodeReference[] CodeReferences;
		public MemoryReference[] MemoryReferences;
		public List<ExternalReference> IncomingReferences;
		public List<ExternalReference> OutgoingReferences;

		public object UserCache;
		public int UserTop;
		public uint UserLines;

		public MethodBody( ModuleInfo moduleInfo, uint address, uint length, Instruction[] instructions )
		{
			this.Module = moduleInfo;
			this.Name = string.Format( "sub_{0:X8}", address );
			this.TotalLines = length / 4;

			this.Address = address;
			this.Length = length;

			this.IncomingReferences = new List<ExternalReference>();
			this.OutgoingReferences = new List<ExternalReference>();

			if( instructions != null )
				this.Populate( instructions );
		}

		public void Touch()
		{
			this.UserCache = null;
		}

		public void Populate( Instruction[] instructions )
		{
			this.Instructions = instructions;

			// - determine locals (loop for first addiu $sp, $sp, [size]
			// - find all labels (branch targets)

			bool foundSpMod = false;
			List<Label> labels = new List<Label>();
			List<CodeReference> codeRefs = new List<CodeReference>();
			List<MemoryReference> memRefs = new List<MemoryReference>();
			uint[] currentRegisters = new uint[ 32 ];
			uint validRegisters = 0;
			bool inDelay = false;
			foreach( Instruction instruction in instructions )
			{
				//lui $n, NNN
				//addiu $n, $n, NNN

				//lui $n, NNN
				//ori $n, $n, NNN

				//lui $n, NNN
				//l* $m, NNN($n)

				//lui $n, NNN
				//s* $m, NNN($n)

				bool wasInDelay = inDelay;
				inDelay = false;
				if( instruction.Opcode != null )
				{
					if( instruction.IsBranch == true )
					{
						uint target = 0;
						foreach( Operand op in instruction.Operands )
						{
							if( op.Type == OperandType.BranchTarget )
							{
								target = ( uint )( ( int )instruction.Address + 4 + op.Immediate );
								break;
							}
						}
						if( target > 0 )
							this.AddLabelReference( labels, instruction, target );
						inDelay = true;
					}
					else if( instruction.IsJump == true )
					{
						uint target = 0;
						foreach( Operand op in instruction.Operands )
						{
							if( op.Type == OperandType.JumpTarget )
							{
								target = ( uint )op.Immediate;
								break;
							}
						}
						if( ( target >= this.Address ) && ( target < ( this.Address + this.Length ) ) )
							this.AddLabelReference( labels, instruction, target );
						else
							this.AddCodeReference( codeRefs, instruction, target );
						inDelay = true;
					}
					else if( instruction.IsLoad == true )
					{
						int reg1 = instruction.Operands[ 1 ].Register.Ordinal;
						if( ( validRegisters & ( 1 << reg1 ) ) > 0 )
						{
							uint target = currentRegisters[ reg1 ];
							target = ( uint )( ( int )target + instruction.Operands[ 1 ].Immediate );
							this.AddMemoryReference( memRefs, instruction, target, true );
						}
					}
					else if( instruction.IsStore == true )
					{
						int reg1 = instruction.Operands[ 1 ].Register.Ordinal;
						if( ( validRegisters & ( 1 << reg1 ) ) > 0 )
						{
							uint target = currentRegisters[ reg1 ];
							target = ( uint )( ( int )target + instruction.Operands[ 1 ].Immediate );
							this.AddMemoryReference( memRefs, instruction, target, false );
						}
					}
					if( ( foundSpMod == false ) && ( instruction.Opcode.InstructionEntry.Name == "addiu" ) )
					{
						foundSpMod = true;
						int adjustment = instruction.Operands[ 2 ].Immediate / 4;
						this.LocalsSize = -adjustment;
					}
					else
					{
						uint flags = instruction.Opcode.InstructionEntry.Flags;
						if( instruction.Opcode.InstructionEntry.Name == "lui" )
						{
							int reg = instruction.Operands[ 0 ].Register.Ordinal;
							validRegisters |= ( uint )( 1 << reg );
							currentRegisters[ reg ] = ( uint )instruction.Operands[ 1 ].Immediate << 16;
						}
						else if( ( flags & InstructionTables.OUT_RT ) > 0 )
						{
							if( instruction.Opcode.InstructionEntry.Name == "addiu" )
							{
								int reg1 = instruction.Operands[ 1 ].Register.Ordinal;
								if( ( validRegisters & ( 1 << reg1 ) ) > 0 )
								{
									int reg2 = instruction.Operands[ 0 ].Register.Ordinal;
									validRegisters |= ( uint )( 1 << reg2 );
									currentRegisters[ reg2 ] = ( uint )( ( int )currentRegisters[ reg1 ] + instruction.Operands[ 2 ].Immediate );
								}
							}
							else if( instruction.Opcode.InstructionEntry.Name == "ori" )
							{
								int reg1 = instruction.Operands[ 1 ].Register.Ordinal;
								if( ( validRegisters & ( 1 << reg1 ) ) > 0 )
								{
									int reg2 = instruction.Operands[ 0 ].Register.Ordinal;
									validRegisters |= ( uint )( 1 << reg2 );
									currentRegisters[ reg2 ] = currentRegisters[ reg1 ] | ( uint )instruction.Operands[ 2 ].Immediate;
								}
							}
							else
							{
								// Changed - no longer valid
								int reg = instruction.Operands[ 0 ].Register.Ordinal;
								validRegisters &= ~( uint )( 1 << reg );
							}
						}
					}
				}
				if( wasInDelay == true )
					validRegisters = 0;
			}

			// Sort all
			this.SortAndSet( labels, codeRefs, memRefs );

			// Add labels to instructions
			foreach( Label label in this.Labels )
			{
				foreach( Instruction instruction in this.Instructions )
				{
					if( label.Address == instruction.Address )
					{
						instruction.Label = label;
						break;
					}
				}
			}

			this.TotalLines = ( uint )( this.Instructions.Length + this.Labels.Length );
		}

		private void SortAndSet( List<Label> labels, List<CodeReference> codeRefs, List<MemoryReference> memRefs )
		{
			labels.Sort( delegate( Label a, Label b )
			{
				return a.Address.CompareTo( b.Address );
			} );
			this.Labels = labels.ToArray();
			codeRefs.Sort( delegate( CodeReference a, CodeReference b )
			{
				return a.Address.CompareTo( b.Address );
			} );
			this.CodeReferences = codeRefs.ToArray();
			memRefs.Sort( delegate( MemoryReference a, MemoryReference b )
			{
				return a.Address.CompareTo( b.Address );
			} );
			this.MemoryReferences = memRefs.ToArray();
		}

		private void AddLabelReference( List<Label> labels, Instruction instruction, uint target )
		{
			foreach( Label label in labels )
			{
				if( label.Address == target )
				{
					label.References.Add( instruction );
					instruction.Reference = label;
					return;
				}
			}
			Label newLabel = new Label();
			newLabel.Name = string.Format( "loc_{0:X8}", target );
			newLabel.Address = target;
			newLabel.References.Add( instruction );
			instruction.Reference = newLabel;
			labels.Add( newLabel );
		}

		private void AddCodeReference( List<CodeReference> codeRefs, Instruction instruction, uint target )
		{
			if( target == 0 )
				return;
			foreach( CodeReference codeRef in codeRefs )
			{
				if( codeRef.Address == target )
				{
					codeRef.References.Add( instruction );
					instruction.Reference = codeRef;
					return;
				}
			}
			CodeReference newCodeRef = new CodeReference();
			newCodeRef.Address = target;
			newCodeRef.References.Add( instruction );
			instruction.Reference = newCodeRef;
			codeRefs.Add( newCodeRef );
		}

		private void AddMemoryReference( List<MemoryReference> memRefs, Instruction instruction, uint target, bool isRead )
		{
			//Debug.WriteLine( string.Format( "adding memory reference at {0:X8} ({1}) to {2:X8} for {3}", instruction.Address, instruction, target, isRead ? "read" : "write" ) );
			foreach( MemoryReference memRef in memRefs )
			{
				if( memRef.Address == target )
				{
					memRef.References.Add( instruction );
					if( isRead == true )
						memRef.Reads++;
					else
						memRef.Writes++;
					instruction.Reference = memRef;
					return;
				}
			}
			MemoryReference newMemRef = new MemoryReference();
			newMemRef.Address = target;
			newMemRef.References.Add( instruction );
			if( isRead == true )
				newMemRef.Reads++;
			else
				newMemRef.Writes++;
			instruction.Reference = newMemRef;
			memRefs.Add( newMemRef );
		}

		public void FinalizeReferences()
		{
			// Add ExternalReferences to instructions that need them
			foreach( Instruction instruction in this.Instructions )
			{
				foreach( ExternalReference extRef in this.OutgoingReferences )
				{
					if( extRef.SourceAddress == instruction.Address )
					{
						instruction.ExternalReference = extRef;
						break;
					}
				}
			}
		}
	}

	abstract class Reference
	{
		public uint Address;
		public List<Instruction> References = new List<Instruction>();
	}

	class Label : Reference
	{
		public string Name;
		public override string ToString()
		{
			return string.Format( "{0} ({1:X8}) - {2} refs", this.Name, this.Address, this.References.Count );
		}
	}

	class MemoryReference : Reference
	{
		public int Reads;
		public int Writes;
		public override string ToString()
		{
			return string.Format( "Mem {0:X8}, {1}r/{2}w - {3} refs", this.Address, this.Reads, this.Writes, this.References.Count );
		}
	}

	class CodeReference : Reference
	{
		public override string ToString()
		{
			return string.Format( "Code {0:X8} - {1} refs", this.Address, this.References.Count );
		}
	}

	class ExternalReference
	{
		public MethodBody Method;
		public uint SourceAddress;
		public uint TargetAddress;
		public override string ToString()
		{
			return string.Format( "Ref {0:X8}->{1:X8} in {2} ({3:X8})", this.SourceAddress, this.TargetAddress, this.Method.Name, this.Method.Address );
		}
	}

	class Instruction
	{
		public readonly uint Address;
		public readonly uint Code;
		public readonly Opcode Opcode;
		public readonly Operand[] Operands;

		public Label Label;
		public Reference Reference;
		public ExternalReference ExternalReference;

		public string Comment;

		public Breakpoint Breakpoint;

		public Instruction( uint address, uint code )
		{
			this.Address = address;
			this.Code = code;

			InstructionEntry instructionEntry = InstructionTables.GetInstruction( code );
			if( instructionEntry == null )
			{
				Debug.WriteLine( string.Format( "Instruction::ctor: no entry found for code {0:X8} at {1:X8}", code, address ) );
			}
			else
			{
				Debug.Assert( instructionEntry.Formatter != null );

				bool formatted = instructionEntry.Formatter( address, code, instructionEntry, out this.Opcode, out this.Operands );
				Debug.Assert( formatted == true );
			}
		}

		public override string ToString()
		{
			if( this.Opcode == null )
				return "UNKNOWN";
			else
				return this.Opcode.ToString() + this.GetOperandsString();
		}

		public string GetOperandsString()
		{
			if( this.Operands == null )
				return string.Empty;
			StringBuilder sb = new StringBuilder();
			for( int n = 0; n < this.Operands.Length; n++ )
			{
				Operand op = this.Operands[ n ];
				bool last = ( n == this.Operands.Length - 1 );
				sb.AppendFormat( " {0}{1}", op.ToString(), last ? "" : "," );
			}
			return sb.ToString();
		}

		public string GetResolvedOperandString( bool hex )
		{
			if( this.Operands == null )
				return string.Empty;
			StringBuilder sb = new StringBuilder();
			for( int n = 0; n < this.Operands.Length; n++ )
			{
				Operand op = this.Operands[ n ];
				sb.Append( ' ' );
				sb.Append( this.GetResolvedOperandString( op, hex ) );
				bool last = ( n == this.Operands.Length - 1 );
				if( last == false )
					sb.Append( ',' );
			}
			return sb.ToString();
		}

		public string GetResolvedOperandString( Operand op, bool hex )
		{
			if( this.Reference != null )
			{
				if( op.Type == OperandType.JumpTarget )
				{
					// Find external reference
					Label label = this.Reference as Label;
					if( label != null )
					{
						return label.Name;
					}
					else
					{
						if( this.ExternalReference != null )
							return this.ExternalReference.Method.Name;
						else
							return op.ToString( hex );
					}
				}
				else if( op.Type == OperandType.BranchTarget )
				{
					// Find label
					Label label = this.Reference as Label;
					return label.Name;
				}
				else if( op.Type == OperandType.MemoryAccess )
				{
					// Find memory reference ??
					return op.ToString( hex );
				}
				else
					return op.ToString( hex );
			}
			else
				return op.ToString( hex );
		}

		public bool IsBranch
		{
			get
			{
				if( this.Opcode == null )
					return false;
				return ( this.Opcode.InstructionEntry.Flags & InstructionTables.IS_CONDBRANCH ) > 0;
			}
		}

		public bool IsJump
		{
			get
			{
				if( this.Opcode == null )
					return false;
				return ( this.Opcode.InstructionEntry.Flags & InstructionTables.IS_JUMP ) > 0;
			}
		}

		public bool IsLoad
		{
			get
			{
				if( this.Opcode == null )
					return false;
				return ( this.Opcode.InstructionEntry.Flags & InstructionTables.IN_MEM ) > 0;
			}
		}

		public bool IsStore
		{
			get
			{
				if( this.Opcode == null )
					return false;
				return ( this.Opcode.InstructionEntry.Flags & InstructionTables.OUT_MEM ) > 0;
			}
		}
	}

	class Opcode
	{
		public readonly InstructionEntry InstructionEntry;

		public readonly string Suffix;
		public readonly string NameOverride;

		public Opcode( InstructionEntry instructionEntry )
		{
			this.InstructionEntry = instructionEntry;
		}

		public Opcode( InstructionEntry instructionEntry, string suffix )
			: this( instructionEntry )
		{
			this.Suffix = suffix;
		}

		public Opcode( InstructionEntry instructionEntry, string suffix, string nameOverride )
			: this( instructionEntry, suffix )
		{
			this.NameOverride = nameOverride;
		}

		public override string ToString()
		{
			string name = this.InstructionEntry.Name;
			if( this.NameOverride != null )
				name = this.NameOverride;
			if( this.Suffix != null )
				return string.Format( "{0}{1}", name, this.Suffix );
			else
				return name;
		}
	}

	enum OperandType
	{
		Annotation,
		Immediate,
		ImmediateFloat,
		BranchTarget,
		JumpTarget,
		Register,
		MemoryAccess,
		VfpuRegister,
	}

	enum DataSize
	{
		V_Single,
		V_Pair,
		V_Triple,
		V_Quad,
		V_2x2,
		V_3x3,
		V_4x4,
		V_Invalid
	}

	class Operand
	{
		public readonly OperandType Type;

		public readonly Register Register;
		public readonly int Immediate;
		public readonly float ImmediateFloat;
		public readonly int Size;
		public readonly string Annotation;

		public readonly DataSize DataSize;
		public readonly bool Transposed;

		//public Reference Reference;

		public Operand( string annotation )
		{
			this.Type = OperandType.Annotation;
			this.Annotation = annotation;
		}

		public Operand( Register register )
		{
			this.Type = OperandType.Register;
			this.Register = register;
		}

		public Operand( OperandType type, Register register )
		{
			this.Type = type;
			this.Register = register;
		}

		public Operand( OperandType type, Register register, int offset )
		{
			this.Type = type;
			this.Register = register;
			this.Immediate = offset;
		}

		public Operand( float immediate )
		{
			this.Type = OperandType.ImmediateFloat;
			this.ImmediateFloat = immediate;
		}

		public Operand( int immediate, int size )
		{
			this.Type = OperandType.Immediate;
			this.Immediate = immediate;
			this.Size = size;
		}

		public Operand( int immediate, int size, string annotation )
		{
			this.Type = OperandType.Immediate;
			this.Immediate = immediate;
			this.Size = size;
			this.Annotation = annotation;
		}

		public Operand( OperandType type, int immediate )
		{
			this.Type = type;
			this.Immediate = immediate;
		}

		public Operand( Register register, DataSize dataSize )
			: this( register, dataSize, false )
		{
		}

		public Operand( Register register, DataSize dataSize, bool transposed )
		{
			this.Type = OperandType.VfpuRegister;
			this.Register = register;
			this.DataSize = dataSize;
			this.Transposed = transposed;
		}

		private static string GetVRNotation( Operand op )
		{
			int reg = op.Register.Ordinal;
			int mtx = ( reg >> 2 ) & 7;
			int idx = reg & 3;
			int fsl = 0;
			bool transpose = op.Transposed;
			char c;
			switch( op.DataSize )
			{
				case DataSize.V_Single:
					transpose = false;
					c = 'S';
					fsl = ( reg >> 5 ) & 3;
					break;
				case DataSize.V_Pair:
					c = 'C';
					fsl = ( reg >> 5 ) & 2;
					break;
				case DataSize.V_Triple:
					c = 'C';
					fsl = ( reg >> 6 ) & 1;
					break;
				case DataSize.V_Quad:
					c = 'C';
					fsl = ( reg >> 5 ) & 2;
					break;

				case DataSize.V_2x2:
					c = 'M';
					fsl = ( reg >> 5 ) & 2;
					break;
				case DataSize.V_3x3:
					c = 'M';
					fsl = ( reg >> 6 ) & 1;
					break;
				case DataSize.V_4x4:
					c = 'M';
					fsl = ( reg >> 5 ) & 2;
					break;
				default:
				case DataSize.V_Invalid:
					c = 'I';
					fsl = 0;
					break;
			}
			if( transpose && c == 'C' )
				c = 'R';
			if( transpose && c == 'M' )
				c = 'E';
			return string.Format( "{0}{1}{2}{3}", c, mtx, idx, fsl );
		}

		// Could be:
		// - an annotation					li $t0, foo [foo]
		// - a register						li $t0, 0 [$t0]
		// - a jump/branch register			be $t0, $t1 [$t1]
		// - an offset register				lw $t0, 123($t1) [$t1 and 123]
		// - an immediate					li $t0, 123 [123]
		// - an immediate with annotation	li $t0, CC[123]  [CC is annotation, 123 is value]
		// - a jump immediate				j 0x1234 [0x1234]
		// - a VFPU register of various sizes/transposed

		public override string ToString()
		{
			return this.ToString( true );
		}

		public string ToString( bool hex )
		{
			switch( this.Type )
			{
				default:
				case OperandType.Annotation:
					return this.Annotation;
				case OperandType.BranchTarget:
					if( hex == true )
						return ( ( ( this.Immediate <= 9 ) && ( this.Immediate >= 0 ) ) ? string.Empty : "0x" ) + this.Immediate.ToString( "X4" );
					else
						return this.Immediate.ToString();
				case OperandType.JumpTarget:
					if( this.Register != null )
						return this.Register.ToString();
					else
						return string.Format( "0x{0:X8}", this.Immediate );
				case OperandType.MemoryAccess:
					if( hex == true )
						return ( ( ( this.Immediate <= 9 ) && ( this.Immediate >= 0 ) ) ? string.Empty : "0x" ) + string.Format( "{0:X}({1})", ( this.Immediate & 0xFFFF ), this.Register.ToString() );
					else
						return string.Format( "{0}({1})", this.Immediate, this.Register.ToString() );
				case OperandType.Register:
					return this.Register.ToString();
				case OperandType.VfpuRegister:
					return Operand.GetVRNotation( this );
				case OperandType.Immediate:
					if( this.Annotation != null )
						return string.Format( "{0:X}[{1}]", this.Annotation, this.Immediate );
					else if( hex == true )
						return ( ( ( this.Immediate <= 9 ) && ( this.Immediate >= 0 ) ) ? string.Empty : "0x" ) + ( this.Immediate & 0xFFFF ).ToString( "X" );
					else
						return this.Immediate.ToString();
				case OperandType.ImmediateFloat:
					return this.ImmediateFloat.ToString();
			}
		}
	}
}
