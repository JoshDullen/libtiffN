#if CCITT_SUPPORT
// mkg3states.cs
//
// Based on LIBTIFF, Version 3.9.4 - 15-Jun-2010
// Copyright (c) 2006-2010 by the Authors
// Copyright (c) 1988-1997 Sam Leffler
// Copyright (c) 1991-1997 Silicon Graphics, Inc.
//
// Permission to use, copy, modify, distribute, and sell this software and
// its documentation for any purpose is hereby granted without fee, provided
// that (i) the above copyright notices and this permission notice appear in
// all copies of the software and related documentation, and (ii) the names of
// Sam Leffler and Silicon Graphics may not be used in any advertising or
// publicity relating to the software without the specific, prior written
// permission of Sam Leffler and Silicon Graphics.
//
// THE SOFTWARE IS PROVIDED "AS-IS" AND WITHOUT WARRANTY OF ANY KIND,
// EXPRESS, IMPLIED OR OTHERWISE, INCLUDING WITHOUT LIMITATION, ANY
// WARRANTY OF MERCHANTABILITY OR FITNESS FOR A PARTICULAR PURPOSE.
//
// IN NO EVENT SHALL SAM LEFFLER OR SILICON GRAPHICS BE LIABLE FOR
// ANY SPECIAL, INCIDENTAL, INDIRECT OR CONSEQUENTIAL DAMAGES OF ANY KIND,
// OR ANY DAMAGES WHATSOEVER RESULTING FROM LOSS OF USE, DATA OR PROFITS,
// WHETHER OR NOT ADVISED OF THE POSSIBILITY OF DAMAGE, AND ON ANY THEORY OF
// LIABILITY, ARISING OUT OF OR IN CONNECTION WITH THE USE OR PERFORMANCE
// OF THIS SOFTWARE.

using System;
using System.Collections.Generic;
using System.Text;

namespace Free.Ports.LibTiff
{
	public static partial class libtiff
	{
		struct proto
		{
			internal ushort code;	// right justified, lsb-first, zero filled
			internal ushort val;	// (pixel count)<<4 + code width

			internal proto(ushort code, ushort val)
			{
				this.code=code;
				this.val=val;
			}
		}

		#region proto[]
		static readonly proto Pass=new proto(0x0008, 4);
		static readonly proto Horiz=new proto(0x0004, 3);
		static readonly proto V0=new proto(0x0001, 1);

		static readonly proto[] VR=new proto[]
		{
			new proto(0x0006, (1<<4)+3),
			new proto(0x0030, (2<<4)+6),
			new proto(0x0060, (3<<4)+7),
		};

		static readonly proto[] VL=new proto[]
		{
			new proto(0x0002, (1<<4)+3),
			new proto(0x0010, (2<<4)+6),
			new proto(0x0020, (3<<4)+7),
		};

		static readonly proto Ext=new proto(0x0040, 7);
		static readonly proto EOLV=new proto(0x0000, 7);

		static readonly proto[] MakeUpW=new proto[]
		{
			new proto(0x001b, 1029),
			new proto(0x0009, 2053),
			new proto(0x003a, 3078),
			new proto(0x0076, 4103),
			new proto(0x006c, 5128),
			new proto(0x00ec, 6152),
			new proto(0x0026, 7176),
			new proto(0x00a6, 8200),
			new proto(0x0016, 9224),
			new proto(0x00e6, 10248),
			new proto(0x0066, 11273),
			new proto(0x0166, 12297),
			new proto(0x0096, 13321),
			new proto(0x0196, 14345),
			new proto(0x0056, 15369),
			new proto(0x0156, 16393),
			new proto(0x00d6, 17417),
			new proto(0x01d6, 18441),
			new proto(0x0036, 19465),
			new proto(0x0136, 20489),
			new proto(0x00b6, 21513),
			new proto(0x01b6, 22537),
			new proto(0x0032, 23561),
			new proto(0x0132, 24585),
			new proto(0x00b2, 25609),
			new proto(0x0006, 26630),
			new proto(0x01b2, 27657),
		};

		static readonly proto[] MakeUpB=new proto[]
		{
			new proto(0x03c0, 1034),
			new proto(0x0130, 2060),
			new proto(0x0930, 3084),
			new proto(0x0da0, 4108),
			new proto(0x0cc0, 5132),
			new proto(0x02c0, 6156),
			new proto(0x0ac0, 7180),
			new proto(0x06c0, 8205),
			new proto(0x16c0, 9229),
			new proto(0x0a40, 10253),
			new proto(0x1a40, 11277),
			new proto(0x0640, 12301),
			new proto(0x1640, 13325),
			new proto(0x09c0, 14349),
			new proto(0x19c0, 15373),
			new proto(0x05c0, 16397),
			new proto(0x15c0, 17421),
			new proto(0x0dc0, 18445),
			new proto(0x1dc0, 19469),
			new proto(0x0940, 20493),
			new proto(0x1940, 21517),
			new proto(0x0540, 22541),
			new proto(0x1540, 23565),
			new proto(0x0b40, 24589),
			new proto(0x1b40, 25613),
			new proto(0x04c0, 26637),
			new proto(0x14c0, 27661),
		};

		static readonly proto[] MakeUp=new proto[]
		{
			new proto(0x0080, 28683),
			new proto(0x0180, 29707),
			new proto(0x0580, 30731),
			new proto(0x0480, 31756),
			new proto(0x0c80, 32780),
			new proto(0x0280, 33804),
			new proto(0x0a80, 34828),
			new proto(0x0680, 35852),
			new proto(0x0e80, 36876),
			new proto(0x0380, 37900),
			new proto(0x0b80, 38924),
			new proto(0x0780, 39948),
			new proto(0x0f80, 40972),
		};

		static readonly proto[] TermW=new proto[]
		{
			new proto(0x00ac, 8),
			new proto(0x0038, 22),
			new proto(0x000e, 36),
			new proto(0x0001, 52),
			new proto(0x000d, 68),
			new proto(0x0003, 84),
			new proto(0x0007, 100),
			new proto(0x000f, 116),
			new proto(0x0019, 133),
			new proto(0x0005, 149),
			new proto(0x001c, 165),
			new proto(0x0002, 181),
			new proto(0x0004, 198),
			new proto(0x0030, 214),
			new proto(0x000b, 230),
			new proto(0x002b, 246),
			new proto(0x0015, 262),
			new proto(0x0035, 278),
			new proto(0x0072, 295),
			new proto(0x0018, 311),
			new proto(0x0008, 327),
			new proto(0x0074, 343),
			new proto(0x0060, 359),
			new proto(0x0010, 375),
			new proto(0x000a, 391),
			new proto(0x006a, 407),
			new proto(0x0064, 423),
			new proto(0x0012, 439),
			new proto(0x000c, 455),
			new proto(0x0040, 472),
			new proto(0x00c0, 488),
			new proto(0x0058, 504),
			new proto(0x00d8, 520),
			new proto(0x0048, 536),
			new proto(0x00c8, 552),
			new proto(0x0028, 568),
			new proto(0x00a8, 584),
			new proto(0x0068, 600),
			new proto(0x00e8, 616),
			new proto(0x0014, 632),
			new proto(0x0094, 648),
			new proto(0x0054, 664),
			new proto(0x00d4, 680),
			new proto(0x0034, 696),
			new proto(0x00b4, 712),
			new proto(0x0020, 728),
			new proto(0x00a0, 744),
			new proto(0x0050, 760),
			new proto(0x00d0, 776),
			new proto(0x004a, 792),
			new proto(0x00ca, 808),
			new proto(0x002a, 824),
			new proto(0x00aa, 840),
			new proto(0x0024, 856),
			new proto(0x00a4, 872),
			new proto(0x001a, 888),
			new proto(0x009a, 904),
			new proto(0x005a, 920),
			new proto(0x00da, 936),
			new proto(0x0052, 952),
			new proto(0x00d2, 968),
			new proto(0x004c, 984),
			new proto(0x00cc, 1000),
			new proto(0x002c, 1016),
		};

		static readonly proto[] TermB=new proto[]
		{
			new proto(0x03b0, 10),
			new proto(0x0002, 19),
			new proto(0x0003, 34),
			new proto(0x0001, 50),
			new proto(0x0006, 67),
			new proto(0x000c, 84),
			new proto(0x0004, 100),
			new proto(0x0018, 117),
			new proto(0x0028, 134),
			new proto(0x0008, 150),
			new proto(0x0010, 167),
			new proto(0x0050, 183),
			new proto(0x0070, 199),
			new proto(0x0020, 216),
			new proto(0x00e0, 232),
			new proto(0x0030, 249),
			new proto(0x03a0, 266),
			new proto(0x0060, 282),
			new proto(0x0040, 298),
			new proto(0x0730, 315),
			new proto(0x00b0, 331),
			new proto(0x01b0, 347),
			new proto(0x0760, 363),
			new proto(0x00a0, 379),
			new proto(0x0740, 395),
			new proto(0x00c0, 411),
			new proto(0x0530, 428),
			new proto(0x0d30, 444),
			new proto(0x0330, 460),
			new proto(0x0b30, 476),
			new proto(0x0160, 492),
			new proto(0x0960, 508),
			new proto(0x0560, 524),
			new proto(0x0d60, 540),
			new proto(0x04b0, 556),
			new proto(0x0cb0, 572),
			new proto(0x02b0, 588),
			new proto(0x0ab0, 604),
			new proto(0x06b0, 620),
			new proto(0x0eb0, 636),
			new proto(0x0360, 652),
			new proto(0x0b60, 668),
			new proto(0x05b0, 684),
			new proto(0x0db0, 700),
			new proto(0x02a0, 716),
			new proto(0x0aa0, 732),
			new proto(0x06a0, 748),
			new proto(0x0ea0, 764),
			new proto(0x0260, 780),
			new proto(0x0a60, 796),
			new proto(0x04a0, 812),
			new proto(0x0ca0, 828),
			new proto(0x0240, 844),
			new proto(0x0ec0, 860),
			new proto(0x01c0, 876),
			new proto(0x0e40, 892),
			new proto(0x0140, 908),
			new proto(0x01a0, 924),
			new proto(0x09a0, 940),
			new proto(0x0d40, 956),
			new proto(0x0340, 972),
			new proto(0x05a0, 988),
			new proto(0x0660, 1004),
			new proto(0x0e60, 1020),
		};

		static readonly proto EOLH=new proto(0x0000, 11);
		#endregion

		static void FillTable(TIFFFaxTabEnt[] T, int Size, proto P, S State)
		{
			int limit=1<<Size;

			byte width=(byte)(P.val&15);
			uint param=(uint)(P.val>>4);
			int incr=1<<width;
			for(int code=P.code; code<limit; code+=incr)
			{
				T[code].State=State;
				T[code].Width=width;
				T[code].Param=param;
			}
		}

		static void FillTable(TIFFFaxTabEnt[] T, int Size, proto[] Ps, S State)
		{
			int limit=1<<Size;

			foreach(proto P in Ps)
			{
				byte width=(byte)(P.val&15);
				uint param=(uint)(P.val>>4);
				int incr=1<<width;
				for(int code=P.code; code<limit; code+=incr)
				{
					T[code].State=State;
					T[code].Width=width;
					T[code].Param=param;
				}
			}
		}

		static readonly TIFFFaxTabEnt[] TIFFFaxMainTable=new TIFFFaxTabEnt[128];
		static readonly TIFFFaxTabEnt[] TIFFFaxWhiteTable=new TIFFFaxTabEnt[4096];
		static readonly TIFFFaxTabEnt[] TIFFFaxBlackTable=new TIFFFaxTabEnt[8192];

		static libtiff()
		{
			FillTable(TIFFFaxMainTable, 7, Pass, S.Pass);
			FillTable(TIFFFaxMainTable, 7, Horiz, S.Horiz);
			FillTable(TIFFFaxMainTable, 7, V0, S.V0);
			FillTable(TIFFFaxMainTable, 7, VR, S.VR);
			FillTable(TIFFFaxMainTable, 7, VL, S.VL);
			FillTable(TIFFFaxMainTable, 7, Ext, S.Ext);
			FillTable(TIFFFaxMainTable, 7, EOLV, S.EOL);

			FillTable(TIFFFaxWhiteTable, 12, MakeUpW, S.MakeUpW);
			FillTable(TIFFFaxWhiteTable, 12, MakeUp, S.MakeUp);
			FillTable(TIFFFaxWhiteTable, 12, TermW, S.TermW);
			FillTable(TIFFFaxWhiteTable, 12, EOLH, S.EOL);

			FillTable(TIFFFaxBlackTable, 13, MakeUpB, S.MakeUpB);
			FillTable(TIFFFaxBlackTable, 13, MakeUp, S.MakeUp);
			FillTable(TIFFFaxBlackTable, 13, TermB, S.TermB);
			FillTable(TIFFFaxBlackTable, 13, EOLH, S.EOL);
		}
	}
}
#endif // CCITT_SUPPORT