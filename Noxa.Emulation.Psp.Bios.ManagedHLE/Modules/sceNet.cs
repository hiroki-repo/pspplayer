// ----------------------------------------------------------------------------
// PSP Player Emulation Suite
// Copyright (C) 2006 Ben Vanik (noxa)
// Licensed under the LGPL - see License.txt in the project root for details
// ----------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;

using Noxa.Utilities;
using Noxa.Emulation.Psp;
using Noxa.Emulation.Psp.Bios;
using Noxa.Emulation.Psp.Cpu;

namespace Noxa.Emulation.Psp.Bios.ManagedHLE.Modules
{
	class sceNet : Module
	{
		#region Properties

		public override string Name
		{
			get
			{
				return "sceNet";
			}
		}

		#endregion

		#region State Management

		public sceNet( Kernel kernel )
			: base( kernel )
		{
		}

		public override void Start()
		{
		}

		public override void Stop()
		{
		}

		#endregion

		[NotImplemented]
		[Stateless]
		[BiosFunction( 0x39AF39A6, "sceNetInit" )]
		// SDK location: /net/pspnet.h:22
		// SDK declaration: public int sceNetInit(int unk1, int unk2, int unk3, int unk4, int unk5);
		public int sceNetInit( int unk1, int unk2, int unk3, int unk4, int unk5 )
		{
			return Module.NotImplementedReturn;
		}

		[NotImplemented]
		[Stateless]
		[BiosFunction( 0x281928A9, "sceNetTerm" )]
		// SDK location: /net/pspnet.h:23
		// SDK declaration: public int sceNetTerm();
		public int sceNetTerm()
		{
			return Module.NotImplementedReturn;
		}

		[NotImplemented]
		[Stateless]
		[BiosFunction( 0x50647530, "sceNetFreeThreadinfo" )]
		public int sceNetFreeThreadinfo()
		{
			return Module.NotImplementedReturn;
		}

		[NotImplemented]
		[Stateless]
		[BiosFunction( 0xAD6844C6, "sceNetThreadAbort" )]
		public int sceNetThreadAbort()
		{
			return Module.NotImplementedReturn;
		}

		[NotImplemented]
		[Stateless]
		[BiosFunction( 0x89360950, "sceNetEtherNtostr" )]
		// SDK location: /net/pspnet.h:25
		// SDK declaration: public int sceNetEtherNtostr(unsigned char *mac, char *name);
		public int sceNetEtherNtostr( int mac, int name )
		{
			return Module.NotImplementedReturn;
		}

		[NotImplemented]
		[Stateless]
		[BiosFunction( 0xD27961C9, "sceNetEtherStrton" )]
		public int sceNetEtherStrton()
		{
			return Module.NotImplementedReturn;
		}

		[NotImplemented]
		[Stateless]
		[BiosFunction( 0x0BF0A3AE, "sceNetGetLocalEtherAddr" )]
		public int sceNetGetLocalEtherAddr()
		{
			return Module.NotImplementedReturn;
		}

		[NotImplemented]
		[Stateless]
		[BiosFunction( 0xCC393E48, "sceNetGetMallocStat" )]
		public int sceNetGetMallocStat()
		{
			return Module.NotImplementedReturn;
		}

		[NotImplemented]
		[Stateless]
		[BiosFunction( 0x1858883D, "sceNetRand" )]
		public int sceNetRand()
		{
			return Module.NotImplementedReturn;
		}
	}
}

/* GenerateStubsV2: auto-generated - 01CEE94A */
