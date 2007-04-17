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
	partial class IoFileMgrForUser : Module
	{
		#region Properties

		public override string Name
		{
			get
			{
				return "IoFileMgrForUser";
			}
		}

		#endregion

		#region State Management

		public IoFileMgrForUser( Kernel kernel )
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
		[BiosFunction( 0x3251EA56, "sceIoPollAsync" )]
		// SDK location: /user/pspiofilemgr.h:419
		// SDK declaration: int sceIoPollAsync(SceUID fd, SceInt64 *res);
		public int sceIoPollAsync( int fd, long res ){ return Module.NotImplementedReturn; }

		[NotImplemented]
		[Stateless]
		[BiosFunction( 0xE23EEC33, "sceIoWaitAsync" )]
		// SDK location: /user/pspiofilemgr.h:399
		// SDK declaration: int sceIoWaitAsync(SceUID fd, SceInt64 *res);
		public int sceIoWaitAsync( int fd, long res ){ return Module.NotImplementedReturn; }

		[NotImplemented]
		[Stateless]
		[BiosFunction( 0x35DBD746, "sceIoWaitAsyncCB" )]
		// SDK location: /user/pspiofilemgr.h:409
		// SDK declaration: int sceIoWaitAsyncCB(SceUID fd, SceInt64 *res);
		public int sceIoWaitAsyncCB( int fd, long res ){ return Module.NotImplementedReturn; }

		[NotImplemented]
		[Stateless]
		[BiosFunction( 0xCB05F8D6, "sceIoGetAsyncStat" )]
		// SDK location: /user/pspiofilemgr.h:430
		// SDK declaration: int sceIoGetAsyncStat(SceUID fd, int poll, SceInt64 *res);
		public int sceIoGetAsyncStat( int fd, int poll, long res ){ return Module.NotImplementedReturn; }

		[NotImplemented]
		[Stateless]
		[BiosFunction( 0xB293727F, "sceIoChangeAsyncPriority" )]
		// SDK location: /user/pspiofilemgr.h:458
		// SDK declaration: int sceIoChangeAsyncPriority(SceUID fd, int pri);
		public int sceIoChangeAsyncPriority( int fd, int pri ){ return Module.NotImplementedReturn; }

		[NotImplemented]
		[Stateless]
		[BiosFunction( 0xA12A0514, "sceIoSetAsyncCallback" )]
		// SDK location: /user/pspiofilemgr.h:469
		// SDK declaration: int sceIoSetAsyncCallback(SceUID fd, SceUID cb, void *argp);
		public int sceIoSetAsyncCallback( int fd, int cb, int argp ){ return Module.NotImplementedReturn; }

		[NotImplemented]
		[Stateless]
		[BiosFunction( 0xE8BC6571, "sceIoCancel" )]
		// SDK location: /user/pspiofilemgr.h:439
		// SDK declaration: int sceIoCancel(SceUID fd);
		public int sceIoCancel( int fd ){ return Module.NotImplementedReturn; }
	}
}

/* GenerateStubsV2: auto-generated - A3F18B53 */
