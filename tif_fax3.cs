#if CCITT_SUPPORT
// tif_fax3.cs
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

// TIFF Library.
//
// CCITT Group 3 (T.4) and Group 4 (T.6) Decompression Support.
//
// This file contains support for decoding and encoding TIFF
// compression algorithms 2, 3, 4, and 32771.
//
// Decoder support is derived, with permission, from the code
// in Frank Cringle's viewfax program;
//		Copyright (C) 1990, 1995 Frank D. Cringle.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace Free.Ports.LibTiff
{
	public static partial class libtiff
	{
		// To override the default routine used to image decoded
		// spans one can use the pseduo tag TIFFTAG_FAXFILLFUNC.
		// The routine must have the type signature given below;
		// for example:
		//
		// fillruns(byte[] buf, uint buf_offset, uint[] run_buf, uint runs, uint erun, uint lastx)
		//
		// where buf is place to set the bits, runs is the array of b&w run
		// lengths (white then black), erun is the last run in the array, and
		// lastx is the width of the row in pixels. Fill routines can assume
		// the run array has room for at least lastx runs and can overwrite
		// data in the run array as needed (e.g. to append zero runs to bring
		// the count up to a nice multiple).
		delegate void TIFFFaxFillFunc(byte[] buf, uint buf_offset, uint[] run_buf, uint runs, uint erun, uint lastx);

		// The default run filler; made external for other decoders.

		// finite state machine codes
		enum S
		{
			Null=0,
			Pass=1,
			Horiz=2,
			V0=3,
			VR=4,
			VL=5,
			Ext=6,
			TermW=7,
			TermB=8,
			MakeUpW=9,
			MakeUpB=10,
			MakeUp=11,
			EOL=12
		}

		struct TIFFFaxTabEnt
		{ // state table entry
			internal S State;		// see above
			internal byte Width;	// width of code in bits
			internal uint Param;	// unsigned 32-bit run length in bits
		}

		// see mkg3states.cs for tables
		//static readonly TIFFFaxTabEnt[] TIFFFaxMainTable=new TIFFFaxTabEnt[128];
		//static readonly TIFFFaxTabEnt[] TIFFFaxWhiteTable=new TIFFFaxTabEnt[4096];
		//static readonly TIFFFaxTabEnt[] TIFFFaxBlackTable=new TIFFFaxTabEnt[8192];

		// Compression+decompression state blocks are
		// derived from this "base state" block.
		class Fax3BaseState : ICodecState
		{
			internal O rw_mode;			// O_RDONLY for decode, else encode
			internal FAXMODE mode;		// operating mode
			internal uint rowbytes;		// bytes in a decoded scanline
			internal uint rowpixels;		// pixels in a scanline

			internal CLEANFAXDATA cleanfaxdata;	// CleanFaxData tag
			internal uint badfaxrun;				// BadFaxRun tag
			internal uint badfaxlines;			// BadFaxLines tag
			internal GROUP3OPT groupoptions;		// Group 3/4 options tag
			internal uint recvparams;				// encoded Class 2 session params
			internal string subaddress;			// subaddress string
			internal uint recvtime;				// time spent receiving (secs)
			internal string faxdcs;				// Table 2/T.30 encoded session params
			internal TIFFVGetMethod vgetparent;	// super-class method
			internal TIFFVSetMethod vsetparent;	// super-class method
			internal TIFFPrintMethod printdir;	// super-class method
		}

		enum Ttag
		{
			G3_1D,
			G3_2D
		}

		class Fax3CodecState : Fax3BaseState
		{
			// Decoder state info
			internal byte[] bitmap;			// bit reversal table
			internal uint data;				// current i/o byte/word
			internal int bit;					// current i/o bit in byte
			internal int EOLcnt;				// count of EOL codes recognized
			internal TIFFFaxFillFunc fill;	// fill routine
			internal uint[] runs;				// b&w runs for current/previous row
			internal uint refruns;			// runs for reference line
			internal uint curruns;			// runs for current line

			// Encoder state info
			internal Ttag tag;		// encoding state
			internal byte[] refline;	// reference line for 2d decoding
			internal int k;			// #rows left that can be 2d encoded
			internal int maxk;		// max #rows that can be 2d encoded

			internal uint line;
		}

		static bool is2DEncoding(Fax3BaseState sp)
		{
			return (sp.groupoptions&GROUP3OPT._2DENCODING)!=0;
		}

		static bool isAligned(uint p, int size)
		{
			return (p&(size-1))==0;
		}

		// Setup state for decoding a strip.
		static bool Fax3PreDecode(TIFF tif, ushort sampleNumber)
		{
			Fax3CodecState sp=tif.tif_data as Fax3CodecState;

#if DEBUG
			if(sp==null) throw new Exception("sp==null");
#endif

			sp.bit=0;			// force initial read
			sp.data=0;
			sp.EOLcnt=0;		// force initial scan for EOL

			// Decoder assumes lsb-to-msb bit order. Note that we select
			// this here rather than in Fax3SetupState so that viewers can
			// hold the image open, fiddle with the FillOrder tag value,
			// and then re-decode the image. Otherwise they'd need to close
			// and open the image to get the state reset.
			sp.bitmap=TIFFGetBitRevTable(tif.tif_dir.td_fillorder!=FILLORDER.LSB2MSB);
			if(sp.refruns!=uint.MaxValue)
			{	// init reference line to white
				sp.runs[sp.refruns]=sp.rowpixels;
				sp.runs[sp.refruns+1]=0;
			}
			sp.line=0;
			return true;
		}

		// Routine for handling various errors/conditions.
		// Note how they are "glued into the decoder" by
		// overriding the definitions used by the decoder.
		static void Fax3Unexpected(string module, TIFF tif, uint line, uint a0)
		{
			TIFFErrorExt(tif.tif_clientdata, module, "{0}: Bad code word at line {1} of {2} {3} (x {4})",
				tif.tif_name, line, isTiled(tif)?"tile":"strip", isTiled(tif)?tif.tif_curtile:tif.tif_curstrip, a0);
		}

		static void Fax3Extension(string module, TIFF tif, uint line, uint a0)
		{
			TIFFErrorExt(tif.tif_clientdata, module, "{0}: Uncompressed data (not supported) at line {1} of {2} {3} (x {4})",
				tif.tif_name, line, isTiled(tif)?"tile":"strip", isTiled(tif)?tif.tif_curtile:tif.tif_curstrip, a0);
		}

		static void Fax3BadLength(string module, TIFF tif, uint line, uint a0, uint lastx)
		{
			TIFFWarningExt(tif.tif_clientdata, module, "{0}: {1} at line {2} of {3} {4} (got {5}, expected {6})",
				tif.tif_name, a0<lastx?"Premature EOL":"Line length mismatch", line, isTiled(tif)?"tile":"strip", isTiled(tif)?tif.tif_curtile:tif.tif_curstrip, a0, lastx);
		}

		static void Fax3PrematureEOF(string module, TIFF tif, uint line, uint a0)
		{
			TIFFWarningExt(tif.tif_clientdata, module, "{0}: Premature EOF at line {1} of {2} {3} (x {4})",
				tif.tif_name, line, isTiled(tif)?"tile":"strip", isTiled(tif)?tif.tif_curtile:tif.tif_curstrip, a0);
		}

		// Decode the requested amount of G3 1D-encoded data.
		static bool Fax3Decode1D(TIFF tif, byte[] buf, int occ, ushort s)
		{
			string module="Fax3Decode1D";
			Fax3CodecState sp=tif.tif_data as Fax3CodecState;
			int lastx=(int)sp.rowpixels;	// last element in row
			byte[] bitmap=sp.bitmap;		// input data bit reverser
			TIFFFaxTabEnt TabEnt;

			uint BitAcc=sp.data;		// bit accumulator
			int BitsAvail=sp.bit;		// # valid bits in BitAcc
			int EOLcnt=sp.EOLcnt;		// # EOL codes recognized
			uint cp=tif.tif_rawcp;		// next byte of input data
			uint ep=cp+tif.tif_rawcc;	// end of input data

			uint thisrun=sp.curruns;	// current row's run array
			uint buf_ind=0;

			while(occ>0)
			{
				int a0=0;				// reference element
				int RunLength=0;		// length of current run
				uint pa=thisrun;		// place to stuff next run

#if FAX3_DEBUG
				Debug.WriteLine("");
				Debug.WriteLine(string.Format("BitAcc={0:X8}, BitsAvail = {1}", BitAcc, BitsAvail));
				Debug.WriteLine(string.Format("-------------------- {0}", tif.tif_row));
#endif

				if(EOLcnt==0)
				{
					for(; ; )
					{
						if(BitsAvail<11)
						{
							if(cp>=ep)
							{
								if(BitsAvail==0) goto EOF1D;	// no valid bits
								BitsAvail=11;					// pad with zeros
							}
							else
							{
								BitAcc|=((uint)bitmap[tif.tif_rawdata[cp++]])<<BitsAvail;
								if((BitsAvail+=8)<11)
								{
									if(cp>=ep)
									{
										// NB: we know BitsAvail is non-zero here
										BitsAvail=11;		// pad with zeros
									}
									else
									{
										BitAcc|=((uint)bitmap[tif.tif_rawdata[cp++]])<<BitsAvail;
										BitsAvail+=8;
									}
								}
							}
						}

						if((BitAcc&((1<<11)-1))==0) break;
						BitsAvail-=1;
						BitAcc>>=1;
					}
				} // if(EOLcnt==0)

				for(; ; )
				{
					if(BitsAvail<8)
					{
						if(cp>=ep)
						{
							if(BitsAvail==0) goto EOF1D;	// no valid bits
							BitsAvail=8;					// pad with zeros
						}
						else
						{
							BitAcc|=((uint)bitmap[tif.tif_rawdata[cp++]])<<BitsAvail;
							BitsAvail+=8;
						}
					}

					if((BitAcc&((1<<8)-1))!=0) break;
					BitsAvail-=8;
					BitAcc>>=8;
				} // for(; ; )

				while((BitAcc&1)==0)
				{
					BitsAvail-=1;
					BitAcc>>=1;
				}

				// EOL bit
				BitsAvail-=1;
				BitAcc>>=1;

				EOLcnt=0;				// reset EOL counter/flag

				for(; ; )
				{
					for(; ; )
					{
						if(BitsAvail<12)
						{
							if(cp>=ep)
							{
								if(BitsAvail==0) goto eof1d; // no valid bits
								BitsAvail=12; // pad with zeros
							}
							else
							{
								BitAcc|=((uint)bitmap[tif.tif_rawdata[cp++]])<<BitsAvail;
								if((BitsAvail+=8)<12)
								{
									if(cp>=ep)
									{
										// NB: we know BitsAvail is non-zero here
										BitsAvail=12; // pad with zeros
									}
									else
									{
										BitAcc|=((uint)bitmap[tif.tif_rawdata[cp++]])<<BitsAvail;
										BitsAvail+=8;
									}
								}
							}
						}

						TabEnt=TIFFFaxWhiteTable[(BitAcc&((1<<12)-1))];
						BitsAvail-=TabEnt.Width;
						BitAcc>>=TabEnt.Width;

						bool done=false;
						switch(TabEnt.State)
						{
							case S.EOL:
								EOLcnt=1;
								goto done1d;
							case S.TermW:
								sp.runs[pa++]=(uint)RunLength+TabEnt.Param;
								a0+=(int)TabEnt.Param;
								RunLength=0;
								done=true;
								break;
							case S.MakeUpW:
							case S.MakeUp:
								a0+=(int)TabEnt.Param;
								RunLength+=(int)TabEnt.Param;
								break;
							default:
								Fax3Unexpected(module, tif, sp.line, (uint)a0);
								goto done1d;
						}
						if(done) break;
					} // for(; ; )

					if(a0>=lastx) goto done1d;
					for(; ; )
					{
						if(BitsAvail<13)
						{
							if(cp>=ep)
							{
								if(BitsAvail==0) goto eof1d; // no valid bits
								BitsAvail=13; // pad with zeros
							}
							else
							{
								BitAcc|=((uint)bitmap[tif.tif_rawdata[cp++]])<<BitsAvail;
								if((BitsAvail+=8)<13)
								{
									if(cp>=ep)
									{
										// NB: we know BitsAvail is non-zero here
										BitsAvail=13; // pad with zeros
									}
									else
									{
										BitAcc|=((uint)bitmap[tif.tif_rawdata[cp++]])<<BitsAvail;
										BitsAvail+=8;
									}
								}
							}
						}

						TabEnt=TIFFFaxBlackTable[(BitAcc&((1<<13)-1))];
						BitsAvail-=TabEnt.Width;
						BitAcc>>=TabEnt.Width;

						bool done=false;
						switch(TabEnt.State)
						{
							case S.EOL:
								EOLcnt=1;
								goto done1d;
							case S.TermB:
								sp.runs[pa++]=(uint)RunLength+TabEnt.Param;
								a0+=(int)TabEnt.Param;
								RunLength=0;
								done=true;
								break;
							case S.MakeUpB:
							case S.MakeUp:
								a0+=(int)TabEnt.Param;
								RunLength+=(int)TabEnt.Param;
								break;
							default:
								Fax3Unexpected(module, tif, sp.line, (uint)a0);
								goto done1d;
						}
						if(done) break;
					} // for(; ; )

					if(a0>=lastx) goto done1d;
					if(sp.runs[pa-1]==0&&sp.runs[pa-2]==0) pa-=2;
				} // for(; ; )
eof1d:
				Fax3PrematureEOF(module, tif, sp.line, (uint)a0);

				if(RunLength!=0)
				{
					sp.runs[pa++]=(uint)RunLength;
					RunLength=0;
				}

				if(a0!=lastx)
				{
					Fax3BadLength(module, tif, sp.line, (uint)a0, (uint)lastx);
					while(a0>lastx&&pa>thisrun) a0-=(int)sp.runs[--pa];
					if(a0<lastx)
					{
						if(a0<0) a0=0;
						if(((pa-thisrun)&1)!=0)
						{
							sp.runs[pa++]=(uint)RunLength;
							RunLength=0;
						}

						sp.runs[pa++]=(uint)(RunLength+(lastx-a0));
						a0+=(int)(lastx-a0);
						RunLength=0;
					}
					else if(a0>lastx)
					{
						sp.runs[pa++]=(uint)(RunLength+lastx);
						a0+=lastx;
						RunLength=0;

						sp.runs[pa++]=(uint)RunLength;
						RunLength=0;
					}
				} // if(a0!=lastx)

				goto EOF1Da;

done1d:
				if(RunLength!=0)
				{
					sp.runs[pa++]=(uint)RunLength;
					RunLength=0;
				}

				if(a0!=lastx)
				{
					Fax3BadLength(module, tif, sp.line, (uint)a0, (uint)lastx);
					while(a0>lastx&&pa>thisrun) a0-=(int)sp.runs[--pa];
					if(a0<lastx)
					{
						if(a0<0) a0=0;
						if(((pa-thisrun)&1)!=0)
						{
							sp.runs[pa++]=(uint)RunLength;
							RunLength=0;
						}

						sp.runs[pa++]=(uint)(RunLength+(lastx-a0));
						a0+=(int)(lastx-a0);
						RunLength=0;
					}
					else if(a0>lastx)
					{
						sp.runs[pa++]=(uint)(RunLength+lastx);
						a0+=lastx;
						RunLength=0;

						sp.runs[pa++]=(uint)RunLength;
						RunLength=0;
					}
				} // if(a0!=lastx)

				sp.fill(buf, buf_ind, sp.runs, thisrun, pa, (uint)lastx);
				buf_ind+=sp.rowbytes;
				occ-=(int)sp.rowbytes;
				sp.line++;

				continue; // while(occ>0)

EOF1D:			// premature EOF
				if(RunLength!=0)
				{
					sp.runs[pa++]=(uint)RunLength;
					RunLength=0;
				}

				if(a0!=lastx)
				{
					Fax3BadLength(module, tif, sp.line, (uint)a0, (uint)lastx);
					while(a0>lastx&&pa>thisrun) a0-=(int)sp.runs[--pa];
					if(a0<lastx)
					{
						if(a0<0) a0=0;
						if(((pa-thisrun)&1)!=0)
						{
							sp.runs[pa++]=(uint)RunLength;
							RunLength=0;
						}

						sp.runs[pa++]=(uint)(RunLength+(lastx-a0));
						a0+=(int)(lastx-a0);
						RunLength=0;
					}
					else if(a0>lastx)
					{
						sp.runs[pa++]=(uint)(RunLength+lastx);
						a0+=lastx;
						RunLength=0;

						sp.runs[pa++]=(uint)RunLength;
						RunLength=0;
					}
				} // if(a0!=lastx)

EOF1Da:			// premature EOF
				sp.fill(buf, buf_ind, sp.runs, thisrun, pa, (uint)lastx);

				sp.bit=BitsAvail;
				sp.data=BitAcc;
				sp.EOLcnt=EOLcnt;
				tif.tif_rawcc-=cp-tif.tif_rawcp;
				tif.tif_rawcp=cp;

				return false;
			} // while(occ>0)

			sp.bit=BitsAvail;
			sp.data=BitAcc;
			sp.EOLcnt=EOLcnt;
			tif.tif_rawcc-=cp-tif.tif_rawcp;
			tif.tif_rawcp=cp;

			return true;
		}

		// Decode the requested amount of G3 2D-encoded data.
		static bool Fax3Decode2D(TIFF tif, byte[] buf, int occ, ushort s)
		{
			string module="Fax3Decode2D";
			Fax3CodecState sp=tif.tif_data as Fax3CodecState;
			int lastx=(int)sp.rowpixels;	// last element in row
			byte[] bitmap=sp.bitmap;		// input data bit reverser
			TIFFFaxTabEnt TabEnt;

			uint BitAcc=sp.data;		// bit accumulator
			int BitsAvail=sp.bit;		// # valid bits in BitAcc
			int EOLcnt=sp.EOLcnt;		// # EOL codes recognized
			uint cp=tif.tif_rawcp;		// next byte of input data
			uint ep=cp+tif.tif_rawcc;	// end of input data

			uint thisrun=sp.curruns;	// current row's run array
			int b1;						// next change on prev line
			uint pb;						// next run in reference line
			uint buf_ind=0;

			while(occ>0)
			{
				int a0=0;					// reference element
				int RunLength=0;			// length of current run
				uint pa=thisrun=sp.curruns;	// place to stuff next run

#if FAX3_DEBUG
				Debug.WriteLine("");
				Debug.Write(string.Format("BitAcc={0:X8}, BitsAvail = {1} EOLcnt = {2}", BitAcc, BitsAvail, EOLcnt));
#endif
				if(EOLcnt==0)
				{
					for(; ; )
					{
						if(BitsAvail<11)
						{
							if(cp>=ep)
							{
								if(BitsAvail==0) goto EOF2D;	// no valid bits
								BitsAvail=11;					// pad with zeros
							}
							else
							{
								BitAcc|=((uint)bitmap[tif.tif_rawdata[cp++]])<<BitsAvail;
								if((BitsAvail+=8)<11)
								{
									if(cp>=ep)
									{
										// NB: we know BitsAvail is non-zero here
										BitsAvail=11;		// pad with zeros
									}
									else
									{
										BitAcc|=((uint)bitmap[tif.tif_rawdata[cp++]])<<BitsAvail;
										BitsAvail+=8;
									}
								}
							}
						}

						if((BitAcc&((1<<11)-1))==0) break;
						BitsAvail-=1;
						BitAcc>>=1;
					}
				} // if(EOLcnt==0)

				for(; ; )
				{
					if(BitsAvail<8)
					{
						if(cp>=ep)
						{
							if(BitsAvail==0) goto EOF2D;	// no valid bits
							BitsAvail=8;					// pad with zeros
						}
						else
						{
							BitAcc|=((uint)bitmap[tif.tif_rawdata[cp++]])<<BitsAvail;
							BitsAvail+=8;
						}
					}

					if((BitAcc&((1<<8)-1))!=0) break;
					BitsAvail-=8;
					BitAcc>>=8;
				} // for(; ; )

				while((BitAcc&1)==0)
				{
					BitsAvail-=1;
					BitAcc>>=1;
				}

				// EOL bit
				BitsAvail-=1;
				BitAcc>>=1;

				EOLcnt=0;				// reset EOL counter/flag

				if(BitsAvail<1)
				{
					if(cp>=ep)
					{
						if(BitsAvail==0) goto EOF2D;	// no valid bits
						BitsAvail=1;					// pad with zeros
					}
					else
					{
						BitAcc|=((uint)bitmap[tif.tif_rawdata[cp++]])<<BitsAvail;
						BitsAvail+=8;
					}
				}

				bool is1D=(BitAcc&1)!=0;	// 1D/2D-encoding tag bit

				BitsAvail-=1;
				BitAcc>>=1;

#if FAX3_DEBUG
				Debug.WriteLine(string.Format(" {0}", is1D ? "1D" : "2D"));
				Debug.WriteLine(string.Format("-------------------- {0}", tif.tif_row));
#endif
				pb=sp.refruns;
				b1=(int)sp.runs[pb++];
				if(is1D)
				{
					for(; ; )
					{
						for(; ; )
						{
							if(BitsAvail<12)
							{
								if(cp>=ep)
								{
									if(BitsAvail==0) goto eof1d; // no valid bits
									BitsAvail=12; // pad with zeros
								}
								else
								{
									BitAcc|=((uint)bitmap[tif.tif_rawdata[cp++]])<<BitsAvail;
									if((BitsAvail+=8)<12)
									{
										if(cp>=ep)
										{
											// NB: we know BitsAvail is non-zero here
											BitsAvail=12; // pad with zeros
										}
										else
										{
											BitAcc|=((uint)bitmap[tif.tif_rawdata[cp++]])<<BitsAvail;
											BitsAvail+=8;
										}
									}
								}
							}

							TabEnt=TIFFFaxWhiteTable[(BitAcc&((1<<12)-1))];
							BitsAvail-=TabEnt.Width;
							BitAcc>>=TabEnt.Width;

							bool done=false;
							switch(TabEnt.State)
							{
								case S.EOL:
									EOLcnt=1;
									goto done1d;
								case S.TermW:
									sp.runs[pa++]=(uint)RunLength+TabEnt.Param;
									a0+=(int)TabEnt.Param;
									RunLength=0;
									done=true;
									break;
								case S.MakeUpW:
								case S.MakeUp:
									a0+=(int)TabEnt.Param;
									RunLength+=(int)TabEnt.Param;
									break;
								default:
									Fax3Unexpected(module, tif, sp.line, (uint)a0);
									goto done1d;
							}
							if(done) break;
						} // for(; ; )

						if(a0>=lastx) goto done1d;
						for(; ; )
						{
							if(BitsAvail<13)
							{
								if(cp>=ep)
								{
									if(BitsAvail==0) goto eof1d; // no valid bits
									BitsAvail=13; // pad with zeros
								}
								else
								{
									BitAcc|=((uint)bitmap[tif.tif_rawdata[cp++]])<<BitsAvail;
									if((BitsAvail+=8)<13)
									{
										if(cp>=ep)
										{
											// NB: we know BitsAvail is non-zero here
											BitsAvail=13; // pad with zeros
										}
										else
										{
											BitAcc|=((uint)bitmap[tif.tif_rawdata[cp++]])<<BitsAvail;
											BitsAvail+=8;
										}
									}
								}
							}

							TabEnt=TIFFFaxBlackTable[(BitAcc&((1<<13)-1))];
							BitsAvail-=TabEnt.Width;
							BitAcc>>=TabEnt.Width;

							bool done=false;
							switch(TabEnt.State)
							{
								case S.EOL:
									EOLcnt=1;
									goto done1d;
								case S.TermB:
									sp.runs[pa++]=(uint)RunLength+TabEnt.Param;
									a0+=(int)TabEnt.Param;
									RunLength=0;
									done=true;
									break;
								case S.MakeUpB:
								case S.MakeUp:
									a0+=(int)TabEnt.Param;
									RunLength+=(int)TabEnt.Param;
									break;
								default:
									Fax3Unexpected(module, tif, sp.line, (uint)a0);
									goto done1d;
							}
							if(done) break;
						} // for(; ; )

						if(a0>=lastx) goto done1d;
						if(sp.runs[pa-1]==0&&sp.runs[pa-2]==0) pa-=2;
					} // for(; ; )
eof1d:
					Fax3PrematureEOF(module, tif, sp.line, (uint)a0);

					if(RunLength!=0)
					{
						sp.runs[pa++]=(uint)RunLength;
						RunLength=0;
					}

					if(a0!=lastx)
					{
						Fax3BadLength(module, tif, sp.line, (uint)a0, (uint)lastx);
						while(a0>lastx&&pa>thisrun) a0-=(int)sp.runs[--pa];
						if(a0<lastx)
						{
							if(a0<0) a0=0;
							if(((pa-thisrun)&1)!=0)
							{
								sp.runs[pa++]=(uint)RunLength;
								RunLength=0;
							}

							sp.runs[pa++]=(uint)(RunLength+(lastx-a0));
							a0+=(int)(lastx-a0);
							RunLength=0;
						}
						else if(a0>lastx)
						{
							sp.runs[pa++]=(uint)(RunLength+lastx);
							a0+=lastx;
							RunLength=0;

							sp.runs[pa++]=(uint)RunLength;
							RunLength=0;
						}
					} // if(a0!=lastx)

					goto EOF2Da;

done1d:
					if(RunLength!=0)
					{
						sp.runs[pa++]=(uint)RunLength;
						RunLength=0;
					}

					if(a0!=lastx)
					{
						Fax3BadLength(module, tif, sp.line, (uint)a0, (uint)lastx);
						while(a0>lastx&&pa>thisrun) a0-=(int)sp.runs[--pa];
						if(a0<lastx)
						{
							if(a0<0) a0=0;
							if(((pa-thisrun)&1)!=0)
							{
								sp.runs[pa++]=(uint)RunLength;
								RunLength=0;
							}

							sp.runs[pa++]=(uint)(RunLength+(lastx-a0));
							a0+=(int)(lastx-a0);
							RunLength=0;
						}
						else if(a0>lastx)
						{
							sp.runs[pa++]=(uint)(RunLength+lastx);
							a0+=lastx;
							RunLength=0;

							sp.runs[pa++]=(uint)RunLength;
							RunLength=0;
						}
					} // if(a0!=lastx)
				}
				else // if(is1D)
				{
					while(a0<lastx)
					{
						if(BitsAvail<7)
						{
							if(cp>=ep)
							{
								if(BitsAvail==0) goto eof2d;	// no valid bits
								BitsAvail=7;					// pad with zeros
							}
							else
							{
								BitAcc|=((uint)bitmap[tif.tif_rawdata[cp++]])<<BitsAvail;
								BitsAvail+=8;
							}
						}

						TabEnt=TIFFFaxMainTable[BitAcc&((1<<7)-1)];
						BitsAvail-=TabEnt.Width;
						BitAcc>>=TabEnt.Width;

						switch(TabEnt.State)
						{
							case S.Pass:
								if(pa!=thisrun)
								{
									while(b1<=a0&&b1<lastx)
									{
										b1+=(int)sp.runs[pb]+(int)sp.runs[pb+1];
										pb+=2;
									}
								}

								b1+=(int)sp.runs[pb++];
								RunLength+=b1-a0;
								a0=b1;
								b1+=(int)sp.runs[pb++];
								break;
							case S.Horiz:
								if(((pa-thisrun)&1)!=0)
								{
									for(; ; )
									{	// black first
										if(BitsAvail<13)
										{
											if(cp>=ep)
											{
												if(BitsAvail==0) goto eof2d;	// no valid bits
												BitsAvail=13;					// pad with zeros
											}
											else
											{
												BitAcc|=((uint)bitmap[tif.tif_rawdata[cp++]])<<BitsAvail;
												if((BitsAvail+=8)<13)
												{
													if(cp>=ep)
													{
														// NB: we know BitsAvail is non-zero here
														BitsAvail=13;		// pad with zeros
													}
													else
													{
														BitAcc|=((uint)bitmap[tif.tif_rawdata[cp++]])<<BitsAvail;
														BitsAvail+=8;
													}
												}
											}
										}

										TabEnt=TIFFFaxBlackTable[BitAcc&((1<<13)-1)];
										BitsAvail-=TabEnt.Width;
										BitAcc>>=TabEnt.Width;

										bool done=false;
										switch(TabEnt.State)
										{
											case S.TermB:
												sp.runs[pa++]=(uint)RunLength+TabEnt.Param;
												a0+=(int)TabEnt.Param;
												RunLength=0;
												done=true;
												break;
											case S.MakeUpB:
											case S.MakeUp:
												a0+=(int)TabEnt.Param;
												RunLength+=(int)TabEnt.Param;
												break;
											default:
												Fax3Unexpected(module, tif, sp.line, (uint)a0);
												goto eol2d;
										}

										if(done) break;
									}

									for(; ; )
									{	// then white
										if(BitsAvail<12)
										{
											if(cp>=ep)
											{
												if(BitsAvail==0) goto eof2d;	// no valid bits
												BitsAvail=12;					// pad with zeros
											}
											else
											{
												BitAcc|=((uint)bitmap[tif.tif_rawdata[cp++]])<<BitsAvail;
												if((BitsAvail+=8)<12)
												{
													if(cp>=ep)
													{
														// NB: we know BitsAvail is non-zero here
														BitsAvail=12;		// pad with zeros
													}
													else
													{
														BitAcc|=((uint)bitmap[tif.tif_rawdata[cp++]])<<BitsAvail;
														BitsAvail+=8;
													}
												}
											}
										}

										TabEnt=TIFFFaxWhiteTable[BitAcc&((1<<12)-1)];
										BitsAvail-=TabEnt.Width;
										BitAcc>>=TabEnt.Width;

										bool done=false;
										switch(TabEnt.State)
										{
											case S.TermW:
												sp.runs[pa++]=(uint)RunLength+TabEnt.Param;
												a0+=(int)TabEnt.Param;
												RunLength=0;
												done=true;
												break;
											case S.MakeUpW:
											case S.MakeUp:
												a0+=(int)TabEnt.Param;
												RunLength+=(int)TabEnt.Param;
												break;
											default:
												Fax3Unexpected(module, tif, sp.line, (uint)a0);
												goto eol2d;
										}
										if(done) break;
									}
								}
								else
								{
									for(; ; )
									{	// white first
										if(BitsAvail<12)
										{
											if(cp>=ep)
											{
												if(BitsAvail==0) goto eof2d;	// no valid bits
												BitsAvail=12;					// pad with zeros
											}
											else
											{
												BitAcc|=((uint)bitmap[tif.tif_rawdata[cp++]])<<BitsAvail;
												if((BitsAvail+=8)<12)
												{
													if(cp>=ep)
													{
														// NB: we know BitsAvail is non-zero here
														BitsAvail=12;		// pad with zeros
													}
													else
													{
														BitAcc|=((uint)bitmap[tif.tif_rawdata[cp++]])<<BitsAvail;
														BitsAvail+=8;
													}
												}
											}
										}

										TabEnt=TIFFFaxWhiteTable[BitAcc&((1<<12)-1)];
										BitsAvail-=TabEnt.Width;
										BitAcc>>=TabEnt.Width;

										bool done=false;
										switch(TabEnt.State)
										{
											case S.TermW:
												sp.runs[pa++]=(uint)RunLength+TabEnt.Param;
												a0+=(int)TabEnt.Param;
												RunLength=0;
												done=true;
												break;
											case S.MakeUpW:
											case S.MakeUp:
												a0+=(int)TabEnt.Param;
												RunLength+=(int)TabEnt.Param;
												break;
											default:
												Fax3Unexpected(module, tif, sp.line, (uint)a0);
												goto eol2d;
										}

										if(done) break;
									}

									for(; ; )
									{	// then black
										if(BitsAvail<13)
										{
											if(cp>=ep)
											{
												if(BitsAvail==0) goto eof2d;	// no valid bits
												BitsAvail=13;					// pad with zeros
											}
											else
											{
												BitAcc|=((uint)bitmap[tif.tif_rawdata[cp++]])<<BitsAvail;
												if((BitsAvail+=8)<13)
												{
													if(cp>=ep)
													{
														// NB: we know BitsAvail is non-zero here
														BitsAvail=13;		// pad with zeros
													}
													else
													{
														BitAcc|=((uint)bitmap[tif.tif_rawdata[cp++]])<<BitsAvail;
														BitsAvail+=8;
													}
												}
											}
										}

										TabEnt=TIFFFaxBlackTable[BitAcc&((1<<13)-1)];
										BitsAvail-=TabEnt.Width;
										BitAcc>>=TabEnt.Width;

										bool done=false;
										switch(TabEnt.State)
										{
											case S.TermB:
												sp.runs[pa++]=(uint)RunLength+TabEnt.Param;
												a0+=(int)TabEnt.Param;
												RunLength=0;
												done=true;
												break;
											case S.MakeUpB:
											case S.MakeUp:
												a0+=(int)TabEnt.Param;
												RunLength+=(int)TabEnt.Param;
												break;
											default:
												Fax3Unexpected(module, tif, sp.line, (uint)a0);
												goto eol2d;
										}
										if(done) break;
									}
								}

								if(pa!=thisrun)
								{
									while(b1<=a0&&b1<lastx)
									{
										b1+=(int)sp.runs[pb]+(int)sp.runs[pb+1];
										pb+=2;
									}
								}
								break;
							case S.V0:
								if(pa!=thisrun)
								{
									while(b1<=a0&&b1<lastx)
									{
										b1+=(int)sp.runs[pb]+(int)sp.runs[pb+1];
										pb+=2;
									}
								}
								sp.runs[pa++]=(uint)(RunLength+(b1-a0));
								a0+=b1-a0;
								RunLength=0;
								b1+=(int)sp.runs[pb++];
								break;
							case S.VR:
								if(pa!=thisrun)
								{
									while(b1<=a0&&b1<lastx)
									{
										b1+=(int)sp.runs[pb]+(int)sp.runs[pb+1];
										pb+=2;
									}
								}
								sp.runs[pa++]=(uint)(RunLength+(b1-a0+TabEnt.Param));
								a0+=(int)(b1-a0+TabEnt.Param);
								RunLength=0;

								b1+=(int)sp.runs[pb++];
								break;
							case S.VL:
								if(pa!=thisrun)
								{
									while(b1<=a0&&b1<lastx)
									{
										b1+=(int)sp.runs[pb]+(int)sp.runs[pb+1];
										pb+=2;
									}
								}
								sp.runs[pa++]=(uint)(RunLength+(b1-a0-TabEnt.Param));
								a0+=(int)(b1-a0-TabEnt.Param);
								RunLength=0;

								b1-=(int)sp.runs[--pb];
								break;
							case S.Ext:
								sp.runs[pa++]=(uint)(lastx-a0);
								Fax3Extension(module, tif, sp.line, (uint)a0);
								goto eol2d;
							case S.EOL:
								sp.runs[pa++]=(uint)(lastx-a0);

								if(BitsAvail<4)
								{
									if(cp>=ep)
									{
										if(BitsAvail==0) goto eof2d;	// no valid bits
										BitsAvail=4;					// pad with zeros
									}
									else
									{
										BitAcc|=((uint)bitmap[tif.tif_rawdata[cp++]])<<BitsAvail;
										BitsAvail+=8;
									}
								}

								if((BitAcc&((1<<4)-1))!=0) Fax3Unexpected(module, tif, sp.line, (uint)a0);
								BitsAvail-=4;
								BitAcc>>=4;
								EOLcnt=1;
								goto eol2d;
							default:
								Fax3Unexpected(module, tif, sp.line, (uint)a0);
								goto eol2d;
						} // switch(TabEnt.State)
					} // while(a0<lastx)

					if(RunLength!=0)
					{
						if(RunLength+a0<lastx)
						{
							// expect a final V0
							if(BitsAvail<1)
							{
								if(cp>=ep)
								{
									if(BitsAvail==0) goto eof2d;	// no valid bits
									BitsAvail=1;					// pad with zeros
								}
								else
								{
									BitAcc|=((uint)bitmap[tif.tif_rawdata[cp++]])<<BitsAvail;
									BitsAvail+=8;
								}
							}

							if((BitAcc&1)==0)
							{
								Fax3Unexpected(module, tif, sp.line, (uint)a0);
								goto eol2d;
							}
							BitsAvail-=1;
							BitAcc>>=1;
						}

						sp.runs[pa++]=(uint)RunLength;
						RunLength=0;
					} // if(RunLength!=0)

eol2d:
					if(RunLength!=0)
					{
						sp.runs[pa++]=(uint)RunLength;
						RunLength=0;
					}

					if(a0!=lastx)
					{
						Fax3BadLength(module, tif, sp.line, (uint)a0, (uint)lastx);
						while(a0>lastx&&pa>thisrun) a0-=(int)sp.runs[--pa];
						if(a0<lastx)
						{
							if(a0<0) a0=0;
							if(((pa-thisrun)&1)!=0)
							{
								sp.runs[pa++]=(uint)RunLength;
								RunLength=0;
							}

							sp.runs[pa++]=(uint)(RunLength+(lastx-a0));
							a0+=(int)(lastx-a0);
							RunLength=0;
						}
						else if(a0>lastx)
						{
							sp.runs[pa++]=(uint)(RunLength+lastx);
							a0+=lastx;
							RunLength=0;

							sp.runs[pa++]=(uint)RunLength;
							RunLength=0;
						}
					} // if(a0!=lastx)

				} // if(is1D)

				sp.fill(buf, buf_ind, sp.runs, thisrun, pa, (uint)lastx);

				// imaginary change for reference
				sp.runs[pa++]=(uint)RunLength;
				RunLength=0;

				uint tmp=sp.curruns; sp.curruns=sp.refruns; sp.refruns=tmp;
				buf_ind+=sp.rowbytes;
				occ-=(int)sp.rowbytes;
				sp.line++;
				continue; // while(occ>0)

eof2d:
				Fax3PrematureEOF(module, tif, sp.line, (uint)a0);

EOF2D:			// premature EOF
				if(RunLength!=0)
				{
					sp.runs[pa++]=(uint)RunLength;
					RunLength=0;
				}

				if(a0!=lastx)
				{
					Fax3BadLength(module, tif, sp.line, (uint)a0, (uint)lastx);
					while(a0>lastx&&pa>thisrun) a0-=(int)sp.runs[--pa];
					if(a0<lastx)
					{
						if(a0<0) a0=0;
						if(((pa-thisrun)&1)!=0)
						{
							sp.runs[pa++]=(uint)RunLength;
							RunLength=0;
						}

						sp.runs[pa++]=(uint)(RunLength+(lastx-a0));
						a0+=(int)(lastx-a0);
						RunLength=0;
					}
					else if(a0>lastx)
					{
						sp.runs[pa++]=(uint)(RunLength+lastx);
						a0+=lastx;
						RunLength=0;

						sp.runs[pa++]=(uint)RunLength;
						RunLength=0;
					}
				} // if(a0!=lastx)

EOF2Da:			// premature EOF
				sp.fill(buf, buf_ind, sp.runs, thisrun, pa, (uint)lastx);

				sp.bit=BitsAvail;
				sp.data=BitAcc;
				sp.EOLcnt=EOLcnt;
				tif.tif_rawcc-=cp-tif.tif_rawcp;
				tif.tif_rawcp=cp;

				return false;
			} // while(occ>0)

			sp.bit=BitsAvail;
			sp.data=BitAcc;
			sp.EOLcnt=EOLcnt;
			tif.tif_rawcc-=cp-tif.tif_rawcp;
			tif.tif_rawcp=cp;

			return true;
		}

		// Bit-fill a row according to the white/black
		// runs generated during G3/G4 decoding.
		static readonly byte[] TIFFFax3fillmasks=new byte[] { 0x00, 0x80, 0xc0, 0xe0, 0xf0, 0xf8, 0xfc, 0xfe, 0xff };

		static void TIFFFax3fillruns(byte[] buf, uint buf_offset, uint[] run_buf, uint runs, uint erun, uint lastx)
		{
			if(((erun-runs)&1)!=0) run_buf[erun++]=0;

			uint x=0;
			for(; runs<erun; runs+=2)
			{
				uint run=run_buf[runs];
				if(x+run>lastx||run>lastx) run=run_buf[runs]=lastx-x;

				if(run!=0)
				{
					uint cp=buf_offset+x>>3;
					uint bx=x&7;
					if(run>8-bx)
					{
						if(bx!=0) // align to byte boundary
						{
							buf[cp++]&=(byte)(0xff<<(int)(8-bx));
							run-=8-bx;
						}

						int n=(int)run>>3;
						if(n!=0) // multiple bytes to fill
						{
							while(n>8)
							{
								buf[cp++]=0;
								buf[cp++]=0;
								buf[cp++]=0;
								buf[cp++]=0;
								buf[cp++]=0;
								buf[cp++]=0;
								buf[cp++]=0;
								buf[cp++]=0;
								n-=8;
							}

							while((n--)>0) buf[cp++]=0;

							run&=7;
						}

						if(run!=0) buf[cp]&=(byte)(0xff>>(int)run);
					}
					else buf[cp]&=(byte)(~(TIFFFax3fillmasks[run]>>(int)bx));

					x+=run_buf[runs];
				}
				run=run_buf[runs+1];

				if(x+run>lastx||run>lastx) run=run_buf[runs+1]=lastx-x;

				if(run!=0)
				{
					uint cp=buf_offset+x>>3;
					uint bx=x&7;
					if(run>8-bx)
					{
						if(bx!=0) // align to byte boundary
						{
							buf[cp++]|=(byte)(0xff>>(int)bx);
							run-=8-bx;
						}

						int n=(int)run>>3;
						if(n!=0) // multiple bytes to fill
						{
							while(n>8)
							{
								buf[cp++]=0xff;
								buf[cp++]=0xff;
								buf[cp++]=0xff;
								buf[cp++]=0xff;
								buf[cp++]=0xff;
								buf[cp++]=0xff;
								buf[cp++]=0xff;
								buf[cp++]=0xff;
								n-=8;
							}

							while((n--)>0) buf[cp++]=0xff;

							run&=7;
						}

						if(run!=0) buf[cp]|=(byte)(0xff00>>(int)run);
					}
					else buf[cp]|=(byte)(TIFFFax3fillmasks[run]>>(int)bx);

					x+=run_buf[runs+1];
				}
			}

#if DEBUG
			if(x!=lastx) throw new Exception("x!=lastx");
#endif
		}

		// Setup G3/G4-related compression/decompression state
		// before data is processed. This routine is called once
		// per image -- it sets up different state based on whether
		// or not decoding or encoding is being done and whether
		// 1D- or 2D-encoded data is involved.
		static bool Fax3SetupState(TIFF tif)
		{
			TIFFDirectory td=tif.tif_dir;
			Fax3CodecState sp=tif.tif_data as Fax3CodecState;
			uint rowbytes, rowpixels, nruns;

			if(td.td_bitspersample!=1)
			{
				TIFFErrorExt(tif.tif_clientdata, tif.tif_name, "Bits/sample must be 1 for Group 3/4 encoding/decoding");
				return false;
			}

			// Calculate the scanline/tile widths.
			if(isTiled(tif))
			{
				rowbytes=(uint)TIFFTileRowSize(tif);
				rowpixels=td.td_tilewidth;
			}
			else
			{
				rowbytes=(uint)TIFFScanlineSize(tif);
				rowpixels=td.td_imagewidth;
			}
			sp.rowbytes=rowbytes;
			sp.rowpixels=rowpixels;

			// Allocate any additional space required for decoding/encoding.
			bool needsRefLine=(sp.groupoptions&GROUP3OPT._2DENCODING)!=0||td.td_compression==COMPRESSION.CCITTFAX4;

			// Assure that allocation computations do not overflow.
			// TIFFroundup and TIFFSafeMultiply return zero on integer overflow
			sp.runs=null;
			nruns=TIFFroundup(rowpixels, 32);
			if(needsRefLine)
			{
				nruns=TIFFSafeMultiply(nruns, 2);
			}
			if((nruns==0)||(TIFFSafeMultiply(nruns, 2)==0))
			{
				TIFFErrorExt(tif.tif_clientdata, tif.tif_name, "Row pixels integer overflow (rowpixels {0})", rowpixels);
				return false;
			}

			try
			{
				sp.runs=new uint[TIFFSafeMultiply(nruns, 2)];
			}
			catch
			{
				TIFFErrorExt(tif.tif_clientdata, tif.tif_name, "No space for Group 3/4 run arrays");
				return false;
			}

			sp.curruns=0;
			if(needsRefLine) sp.refruns=nruns;
			else sp.refruns=uint.MaxValue;

			if(td.td_compression==COMPRESSION.CCITTFAX3&&is2DEncoding(sp))
			{	// NB: default is 1D routine
				tif.tif_decoderow=Fax3Decode2D;
				tif.tif_decodestrip=Fax3Decode2D;
				tif.tif_decodetile=Fax3Decode2D;
			}

			if(needsRefLine)
			{
				// 2d encoding requires a scanline
				// buffer for the "reference line"; the
				// scanline against which delta encoding
				// is referenced. The reference line must
				// be initialized to be "white" (done elsewhere).
				try
				{
					sp.refline=new byte[rowbytes];
				}
				catch
				{
					TIFFErrorExt(tif.tif_clientdata, "Fax3SetupState", "{0}: No space for Group 3/4 reference line", tif.tif_name);
					return false;
				}
			}
			else
			{	// 1d encoding
				sp.refline=null;
			}

			return true;
		}

		// CCITT Group 3 FAX Encoding.

		static readonly uint[] TIFFFax3msbmask=new uint[9] { 0x00, 0x01, 0x03, 0x07, 0x0f, 0x1f, 0x3f, 0x7f, 0xff };

		// Write a variable-length bit-value to
		// the output stream. Values are
		// assumed to be at most 16 bits.
		static void Fax3PutBits(TIFF tif, uint bits, int length)
		{
			Fax3CodecState sp=tif.tif_data as Fax3CodecState;
			int bit=sp.bit;
			uint data=sp.data;

			while(length>bit)
			{
				data|=bits>>(length-bit);
				length-=bit;

				if(tif.tif_rawcc>=tif.tif_rawdatasize) TIFFFlushData1(tif);
				tif.tif_rawdata[tif.tif_rawcp++]=(byte)data;
				tif.tif_rawcc++;
				data=0; bit=8;
			}
			data|=(bits&TIFFFax3msbmask[length])<<(bit-length);
			bit-=length;
			if(bit==0)
			{
				if(tif.tif_rawcc>=tif.tif_rawdatasize) TIFFFlushData1(tif);
				tif.tif_rawdata[tif.tif_rawcp++]=(byte)data;
				tif.tif_rawcc++;
				data=0; bit=8;
			}

			sp.data=data;
			sp.bit=bit;
		}

		// Write a code to the output stream.

		// Write the sequence of codes that describes
		// the specified span of zero's or one's. The
		// appropriate table that holds the make-up and
		// terminating codes is supplied.
		static void putspan(TIFF tif, uint span, TIFFFaxCodesTableEntry[] tab)
		{
			Fax3CodecState sp=tif.tif_data as Fax3CodecState;
			int bit=sp.bit;
			uint data=sp.data;
			uint code;
			int length;

			while(span>=2624)
			{
				TIFFFaxCodesTableEntry te=tab[63+(2560>>6)];
				code=te.code;
				length=te.length;

#if FAX3_DEBUG
				Console.Write("{0:X8}/{1}: {2}{3}{4}\t", data, bit, "MakeUp", tab==TIFFFaxWhiteCodes?"W":"B", te.runlen);
				for(int t=length-1; t>=0; t--) Console.Write((code&(1<<t))!=0?'1':'0');
				Console.WriteLine();
#endif

				while(length>bit)
				{
					data|=code>>(length-bit);
					length-=bit;

					if(tif.tif_rawcc>=tif.tif_rawdatasize) TIFFFlushData1(tif);
					tif.tif_rawdata[tif.tif_rawcp++]=(byte)data;
					tif.tif_rawcc++;
					data=0; bit=8;
				}
				data|=(code&TIFFFax3msbmask[length])<<(bit-length);
				bit-=length;
				if(bit==0)
				{
					if(tif.tif_rawcc>=tif.tif_rawdatasize) TIFFFlushData1(tif);
					tif.tif_rawdata[tif.tif_rawcp++]=(byte)data;
					tif.tif_rawcc++;
					data=0; bit=8;
				}

				span=(uint)(span-te.runlen);
			}

			if(span>=64)
			{
				TIFFFaxCodesTableEntry te=tab[63+(span>>6)];

#if DEBUG
				if(te.runlen!=64*(span>>6)) throw new Exception("te.runlen!=64*(span>>6)");
#endif

				code=te.code;
				length=te.length;

#if FAX3_DEBUG
				Console.Write("{0:X8}/{1}: {2}{3}{4}\t", data, bit, "MakeUp", tab==TIFFFaxWhiteCodes?"W":"B", te.runlen);
				for(int t=length-1; t>=0; t--) Console.Write((code&(1<<t))!=0?'1':'0');
				Console.WriteLine();
#endif

				while(length>bit)
				{
					data|=code>>(length-bit);
					length-=bit;

					if(tif.tif_rawcc>=tif.tif_rawdatasize) TIFFFlushData1(tif);
					tif.tif_rawdata[tif.tif_rawcp++]=(byte)data;
					tif.tif_rawcc++;
					data=0; bit=8;
				}
				data|=(code&TIFFFax3msbmask[length])<<(bit-length);
				bit-=length;
				if(bit==0)
				{
					if(tif.tif_rawcc>=tif.tif_rawdatasize) TIFFFlushData1(tif);
					tif.tif_rawdata[tif.tif_rawcp++]=(byte)data;
					tif.tif_rawcc++;
					data=0; bit=8;
				}

				span=(uint)(span-te.runlen);
			}

			code=tab[span].code;
			length=tab[span].length;

#if FAX3_DEBUG
			Console.Write("{0:X8}/{1}: {2}{3}{4}\t", data, bit, "Term", tab==TIFFFaxWhiteCodes?"W":"B", tab[span].runlen);
			for(int t=length-1; t>=0; t--) Console.Write((code&(1<<t))!=0?'1':'0');
			Console.WriteLine();
#endif

			while(length>bit)
			{
				data|=code>>(length-bit);
				length-=bit;

				if(tif.tif_rawcc>=tif.tif_rawdatasize) TIFFFlushData1(tif);
				tif.tif_rawdata[tif.tif_rawcp++]=(byte)data;
				tif.tif_rawcc++;
				data=0; bit=8;
			}
			data|=(code&TIFFFax3msbmask[length])<<(bit-length);
			bit-=length;
			if(bit==0)
			{
				if(tif.tif_rawcc>=tif.tif_rawdatasize) TIFFFlushData1(tif);
				tif.tif_rawdata[tif.tif_rawcp++]=(byte)data;
				tif.tif_rawcc++;
				data=0; bit=8;
			}

			sp.data=data;
			sp.bit=bit;
		}

		// Write an EOL code to the output stream. The zero-fill
		// logic for byte-aligning encoded scanlines is handled
		// here. We also handle writing the tag bit for the next
		// scanline when doing 2d encoding.
		static void Fax3PutEOL(TIFF tif)
		{
			Fax3CodecState sp=tif.tif_data as Fax3CodecState;
			int bit=sp.bit;
			uint data=sp.data;
			uint code;
			int tparm;

			if((sp.groupoptions&GROUP3OPT.FILLBITS)!=0)
			{
				// Force bit alignment so EOL will terminate on
				// a byte boundary. That is, force the bit alignment
				// to 16-12 = 4 before putting out the EOL code.
				int align=8-4;
				if(align!=sp.bit)
				{
					if(align>sp.bit) align=sp.bit+(8-align);
					else align=sp.bit-align;
					code=0;
					tparm=align;
					
					while(tparm>bit)
					{
						data|=code>>(tparm-bit);
						tparm-=bit;

						if(tif.tif_rawcc>=tif.tif_rawdatasize) TIFFFlushData1(tif);
						tif.tif_rawdata[tif.tif_rawcp++]=(byte)data;
						tif.tif_rawcc++;
						data=0; bit=8;
					}
					data|=(code&TIFFFax3msbmask[tparm])<<(bit-tparm);
					bit-=tparm;
					if(bit==0)
					{
						if(tif.tif_rawcc>=tif.tif_rawdatasize) TIFFFlushData1(tif);
						tif.tif_rawdata[tif.tif_rawcp++]=(byte)data;
						tif.tif_rawcc++;
						data=0; bit=8;
					}
				}
			}
			code=EOL;
			int length=12;
			if(is2DEncoding(sp))
			{
				code<<=1;
				code|=((sp.tag==Ttag.G3_1D)?1u:0u);
				length++;
			}

			while(length>bit)
			{
				data|=code>>(length-bit);
				length-=bit;

				if(tif.tif_rawcc>=tif.tif_rawdatasize) TIFFFlushData1(tif);
				tif.tif_rawdata[tif.tif_rawcp++]=(byte)data;
				tif.tif_rawcc++;
				data=0; bit=8;
			}
			data|=(code&TIFFFax3msbmask[length])<<(bit-length);
			bit-=length;
			if(bit==0)
			{
				if(tif.tif_rawcc>=tif.tif_rawdatasize) TIFFFlushData1(tif);
				tif.tif_rawdata[tif.tif_rawcp++]=(byte)data;
				tif.tif_rawcc++;
				data=0; bit=8;
			}

			sp.data=data;
			sp.bit=bit;
		}

		// Reset encoding state at the start of a strip.
		static bool Fax3PreEncode(TIFF tif, ushort sampleNumber)
		{
			Fax3CodecState sp=tif.tif_data as Fax3CodecState;

#if DEBUG
			if(sp==null) throw new Exception("sp==null");
#endif

			sp.bit=8;
			sp.data=0;
			sp.tag=Ttag.G3_1D;

			// This is necessary for Group 4; otherwise it isn't
			// needed because the first scanline of each strip ends
			// up being copied into the refline.
			if(sp.refline!=null) for(int i=0; i<sp.rowbytes; i++) sp.refline[i]=0;

			if(is2DEncoding(sp))
			{
				double res=tif.tif_dir.td_yresolution;

				// The CCITT spec says that when doing 2d encoding, you
				// should only do it on K consecutive scanlines, where K
				// depends on the resolution of the image being encoded
				// (2 for <= 200 lpi, 4 for > 200 lpi). Since the directory
				// code initializes td_yresolution to 0, this code will
				// select a K of 2 unless the YResolution tag is set
				// appropriately. (Note also that we fudge a little here
				// and use 150 lpi to avoid problems with units conversion.)
				if(tif.tif_dir.td_resolutionunit==RESUNIT.CENTIMETER) res*=2.54; // convert to inches
				sp.maxk=(res>150?4:2);
				sp.k=sp.maxk-1;
			}
			else sp.k=sp.maxk=0;

			sp.line=0;
			return true;
		}

		static readonly byte[] zeroruns=new byte[256]
		{
			8, 7, 6, 6, 5, 5, 5, 5, 4, 4, 4, 4, 4, 4, 4, 4,	// 0x00 - 0x0f
			3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3,	// 0x10 - 0x1f
			2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2,	// 0x20 - 0x2f
			2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2,	// 0x30 - 0x3f
			1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1,	// 0x40 - 0x4f
			1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1,	// 0x50 - 0x5f
			1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1,	// 0x60 - 0x6f
			1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1,	// 0x70 - 0x7f
			0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,	// 0x80 - 0x8f
			0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,	// 0x90 - 0x9f
			0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,	// 0xa0 - 0xaf
			0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,	// 0xb0 - 0xbf
			0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,	// 0xc0 - 0xcf
			0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,	// 0xd0 - 0xdf
			0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,	// 0xe0 - 0xef
			0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,	// 0xf0 - 0xff
		};

		static readonly byte[] oneruns=new byte[256]
		{
			0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,	// 0x00 - 0x0f
			0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,	// 0x10 - 0x1f
			0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,	// 0x20 - 0x2f
			0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,	// 0x30 - 0x3f
			0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,	// 0x40 - 0x4f
			0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,	// 0x50 - 0x5f
			0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,	// 0x60 - 0x6f
			0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,	// 0x70 - 0x7f
			1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1,	// 0x80 - 0x8f
			1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1,	// 0x90 - 0x9f
			1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1,	// 0xa0 - 0xaf
			1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1,	// 0xb0 - 0xbf
			2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2,	// 0xc0 - 0xcf
			2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2,	// 0xd0 - 0xdf
			3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3,	// 0xe0 - 0xef
			4, 4, 4, 4, 4, 4, 4, 4, 5, 5, 5, 5, 6, 6, 7, 8,	// 0xf0 - 0xff
		};

		// Find a span of ones or zeros using the supplied
		// table. The "base" of the bit string is supplied
		// along with the start+end bit indices.
		static uint find0span(byte[] buf, uint buf_offset, uint bs, uint be)
		{
			uint bits=be-bs;
			uint span=0;

			unsafe
			{
				fixed(byte* bp_=buf)
				{
					byte* bp=bp_+buf_offset;
					bp+=bs>>3;

					// Check partial byte on lhs.
					if(bits>0&&(bs&7)!=0)
					{
						uint n=bs&7;
						span=zeroruns[(*bp<<(int)n)&0xff];
						if(span>8-n) span=8-n;		// table value too generous
						if(span>bits) span=bits;	// constrain span to bit range
						if(n+span<8) return span;	// doesn't extend to edge of byte

						bits-=span;
						bp++;
					}

					// Scan full bytes for all 0's.
					while(bits>=8)
					{
						if(*bp!=0x00) return span+zeroruns[*bp]; // end of run
						span+=8;
						bits-=8;
						bp++;
					}

					// Check partial byte on rhs.
					if(bits>0)
					{
						uint n=zeroruns[*bp];
						span+=(n>bits?bits:n);
					}
				}
			}
			return span;
		}

		static uint find1span(byte[] buf, uint buf_offset, uint bs, uint be)
		{
			uint bits=be-bs;
			uint span=0;

			unsafe
			{
				fixed(byte* bp_=buf)
				{
					byte* bp=bp_+buf_offset;
					bp+=bs>>3;

					// Check partial byte on lhs.
					if(bits>0&&(bs&7)!=0)
					{
						uint n=bs&7;
						span=oneruns[(*bp<<(int)n)&0xff];
						if(span>8-n) span=8-n;		// table value too generous
						if(span>bits) span=bits;	// constrain span to bit range
						if(n+span<8) return span;	// doesn't extend to edge of byte

						bits-=span;
						bp++;
					}

					// Scan full bytes for all 1's.
					while(bits>=8)
					{
						if(*bp!=0xff) return span+oneruns[*bp]; // end of run
						span+=8;
						bits-=8;
						bp++;
					}

					// Check partial byte on rhs.
					if(bits>0)
					{
						uint n=oneruns[*bp];
						span+=(n>bits?bits:n);
					}
				}
			}
			return span;
		}

		// Return the offset of the next bit in the range
		// [bs..be] that is different from the specified
		// color. The end, be, is returned if no such bit exists.
		static uint finddiff(byte[] cp, uint cp_offset, uint bs, uint be, bool color)
		{
			return bs+(color?find1span(cp, cp_offset, bs, be):find0span(cp, cp_offset, bs, be));
		}

		// Like finddiff, but also check the starting bit
		// against the end in case start > end.
		static uint finddiff2(byte[] cp, uint cp_offset, uint bs, uint be, bool color)
		{
			return (bs<be?finddiff(cp, cp_offset, bs, be, color):be);
		}

		// 1d-encode a row of pixels. The encoding is
		// a sequence of all-white or all-black spans
		// of pixels encoded with Huffman codes.
		static bool Fax3Encode1DRow(TIFF tif, byte[] bp, uint bp_offset, uint bits)
		{
			Fax3CodecState sp=tif.tif_data as Fax3CodecState;
			uint span;
			uint bs=0;

			for(; ; )
			{
				span=find0span(bp, bp_offset, bs, bits); // white span
				putspan(tif, span, TIFFFaxWhiteCodes);
				bs+=span;
				if(bs>=bits) break;

				span=find1span(bp, bp_offset, bs, bits); // black span
				putspan(tif, span, TIFFFaxBlackCodes);
				bs+=span;
				if(bs>=bits) break;
			}

			if((sp.mode&(FAXMODE.BYTEALIGN|FAXMODE.WORDALIGN))!=0)
			{
				if(sp.bit!=8) // byte-align
				{
					if(tif.tif_rawcc>=tif.tif_rawdatasize) TIFFFlushData1(tif);
					tif.tif_rawdata[tif.tif_rawcp++]=(byte)sp.data;
					tif.tif_rawcc++;
					sp.data=0;
					sp.bit=8;
				}

				if((sp.mode&FAXMODE.WORDALIGN)!=0&&!isAligned(tif.tif_rawcp, sizeof(ushort)))
				{
					if(tif.tif_rawcc>=tif.tif_rawdatasize) TIFFFlushData1(tif);
					tif.tif_rawdata[tif.tif_rawcp++]=(byte)sp.data;
					tif.tif_rawcc++;
					sp.data=0;
					sp.bit=8;
				}
			}
			return true;
		}

		static readonly TIFFFaxCodesTableEntry horizcode=new TIFFFaxCodesTableEntry(3, 0x1, 0);	// 001
		static readonly TIFFFaxCodesTableEntry passcode=new TIFFFaxCodesTableEntry(4, 0x1, 0);	// 0001
		static readonly TIFFFaxCodesTableEntry[] vcodes=new TIFFFaxCodesTableEntry[7] 
		{
			new TIFFFaxCodesTableEntry(7, 0x03, 0),	// 0000 011
			new TIFFFaxCodesTableEntry(6, 0x03, 0),	// 0000 11
			new TIFFFaxCodesTableEntry(3, 0x03, 0),	// 011
			new TIFFFaxCodesTableEntry(1, 0x1, 0),	// 1
			new TIFFFaxCodesTableEntry(3, 0x2, 0),	// 010
			new TIFFFaxCodesTableEntry(6, 0x02, 0),	// 0000 10
			new TIFFFaxCodesTableEntry(7, 0x02, 0)	// 0000 010
		};

		// 2d-encode a row of pixels. Consult the CCITT
		// documentation for the algorithm.
		static bool Fax3Encode2DRow(TIFF tif, byte[] bp, uint bp_offset, byte[] rp, uint bits)
		{
			uint a0=0;
			uint a1=((bp[bp_offset]>>7)&1)!=0?0:finddiff(bp, bp_offset, 0, bits, false);
			uint b1=((rp[0]>>7)&1)!=0?0:finddiff(rp, 0, 0, bits, false);
			uint a2, b2;

			for(; ; )
			{
				b2=finddiff2(rp, 0, b1, bits, ((rp[b1>>3]>>(int)(7-(b1&7)))&1)!=0);
				if(b2>=a1)
				{
					int d=(int)b1-(int)a1;
					if(!(-3<=d&&d<=3))
					{	// horizontal mode
						a2=finddiff2(bp, bp_offset, a1, bits, ((bp[bp_offset+(a1>>3)]>>(int)(7-(a1&7)))&1)!=0);
						Fax3PutBits(tif, horizcode.code, horizcode.length);
						if(a0+a1==0||((bp[bp_offset+(a0>>3)]>>(int)(7-(a0&7)))&1)==0)
						{
							putspan(tif, a1-a0, TIFFFaxWhiteCodes);
							putspan(tif, a2-a1, TIFFFaxBlackCodes);
						}
						else
						{
							putspan(tif, a1-a0, TIFFFaxBlackCodes);
							putspan(tif, a2-a1, TIFFFaxWhiteCodes);
						}
						a0=a2;
					}
					else
					{	// vertical mode
						Fax3PutBits(tif, vcodes[d+3].code, vcodes[d+3].length);
						a0=a1;
					}
				}
				else
				{	// pass mode
					Fax3PutBits(tif, passcode.code, passcode.length);
					a0=b2;
				}

				if(a0>=bits) break;

				a1=finddiff(bp, bp_offset, a0, bits, ((bp[bp_offset+(a0>>3)]>>(int)(7-(a0&7)))&1)!=0);
				b1=finddiff(rp, 0, a0, bits, ((bp[bp_offset+(a0>>3)]>>(int)(7-(a0&7)))&1)==0);
				b1=finddiff(rp, 0, b1, bits, ((bp[bp_offset+(a0>>3)]>>(int)(7-(a0&7)))&1)!=0);
			}
			return true;
		}
		
		// Encode a buffer of pixels.
		static bool Fax3Encode(TIFF tif, byte[] bp, int cc, ushort s)
		{
			Fax3CodecState sp=tif.tif_data as Fax3CodecState;

			uint bp_ind=0;
			while(cc>0)
			{
				if((sp.mode&FAXMODE.NOEOL)==0) Fax3PutEOL(tif);
				if(is2DEncoding(sp))
				{
					if(sp.tag==Ttag.G3_1D)
					{
						if(!Fax3Encode1DRow(tif, bp, bp_ind, sp.rowpixels)) return false;
						sp.tag=Ttag.G3_2D;
					}
					else
					{
						if(!Fax3Encode2DRow(tif, bp, bp_ind, sp.refline, sp.rowpixels)) return false;
						sp.k--;
					}
					if(sp.k==0)
					{
						sp.tag=Ttag.G3_1D;
						sp.k=sp.maxk-1;
					}
					else Array.Copy(bp, bp_ind, sp.refline, 0, sp.rowbytes);
				}
				else
				{
					if(!Fax3Encode1DRow(tif, bp, bp_ind, sp.rowpixels)) return false;
				}
				bp_ind+=sp.rowbytes;
				cc-=(int)sp.rowbytes;
			}
			return true;
		}

		static bool Fax3PostEncode(TIFF tif)
		{
			Fax3CodecState sp=tif.tif_data as Fax3CodecState;

			if(sp.bit!=8)
			{
				if(tif.tif_rawcc>=tif.tif_rawdatasize) TIFFFlushData1(tif);
				tif.tif_rawdata[tif.tif_rawcp++]=(byte)sp.data;
				tif.tif_rawcc++;
				sp.data=0;
				sp.bit=8;
			}
			return true;
		}

		static void Fax3Close(TIFF tif)
		{
			Fax3CodecState sp=tif.tif_data as Fax3CodecState;

			if((sp.mode&FAXMODE.NORTC)==0)
			{
				uint code=EOL;
				int length=12;

				if(is2DEncoding(sp))
				{
					code=(code<<1)|(sp.tag==Ttag.G3_1D?1u:0u);
					length++;
				}

				for(int i=0; i<6; i++)
				{
					Fax3PutBits(tif, code, length);
				}

				if(tif.tif_rawcc>=tif.tif_rawdatasize) TIFFFlushData1(tif);
				tif.tif_rawdata[tif.tif_rawcp++]=(byte)sp.data;
				tif.tif_rawcc++;
				sp.data=0;
				sp.bit=8;
			}
		}

		static void Fax3Cleanup(TIFF tif)
		{
			Fax3CodecState sp=tif.tif_data as Fax3CodecState;

#if DEBUG
			if(sp==null) throw new Exception("sp==null");
#endif

			tif.tif_tagmethods.vgetfield=sp.vgetparent;
			tif.tif_tagmethods.vsetfield=sp.vsetparent;
			tif.tif_tagmethods.printdir=sp.printdir;

			sp.runs=null;
			sp.refline=null;

			sp.subaddress=null;
			sp.faxdcs=null;

			tif.tif_data=null;

			TIFFSetDefaultCompressionState(tif);
		}

		static readonly List<TIFFFieldInfo> faxFieldInfo=MakeFaxFieldInfo();

		static List<TIFFFieldInfo> MakeFaxFieldInfo()
		{
			List<TIFFFieldInfo> ret=new List<TIFFFieldInfo>();
			ret.Add(new TIFFFieldInfo(TIFFTAG.FAXMODE, 0, 0, TIFFDataType.TIFF_ANY, FIELD.PSEUDO, false, false, "FaxMode"));
			//ret.Add(new TIFFFieldInfo(TIFFTAG.FAXFILLFUNC, 0, 0, TIFFDataType.TIFF_ANY, FIELD.PSEUDO, false, false, "FaxFillFunc"));
			ret.Add(new TIFFFieldInfo(TIFFTAG.BADFAXLINES, 1, 1, TIFFDataType.TIFF_LONG, FIELD.CCITT_BADFAXLINES, true, false, "BadFaxLines"));
			ret.Add(new TIFFFieldInfo(TIFFTAG.BADFAXLINES, 1, 1, TIFFDataType.TIFF_SHORT, FIELD.CCITT_BADFAXLINES, true, false, "BadFaxLines"));
			ret.Add(new TIFFFieldInfo(TIFFTAG.CLEANFAXDATA, 1, 1, TIFFDataType.TIFF_SHORT, FIELD.CCITT_CLEANFAXDATA, true, false, "CleanFaxData"));
			ret.Add(new TIFFFieldInfo(TIFFTAG.CONSECUTIVEBADFAXLINES, 1, 1, TIFFDataType.TIFF_LONG, FIELD.CCITT_BADFAXRUN, true, false, "ConsecutiveBadFaxLines"));
			ret.Add(new TIFFFieldInfo(TIFFTAG.CONSECUTIVEBADFAXLINES, 1, 1, TIFFDataType.TIFF_SHORT, FIELD.CCITT_BADFAXRUN, true, false, "ConsecutiveBadFaxLines"));
			ret.Add(new TIFFFieldInfo(TIFFTAG.FAXRECVPARAMS, 1, 1, TIFFDataType.TIFF_LONG, FIELD.CCITT_RECVPARAMS, true, false, "FaxRecvParams"));
			ret.Add(new TIFFFieldInfo(TIFFTAG.FAXSUBADDRESS, -1, -1, TIFFDataType.TIFF_ASCII, FIELD.CCITT_SUBADDRESS, true, false, "FaxSubAddress"));
			ret.Add(new TIFFFieldInfo(TIFFTAG.FAXRECVTIME, 1, 1, TIFFDataType.TIFF_LONG, FIELD.CCITT_RECVTIME, true, false, "FaxRecvTime"));
			ret.Add(new TIFFFieldInfo(TIFFTAG.FAXDCS, -1, -1, TIFFDataType.TIFF_ASCII, FIELD.CCITT_FAXDCS, true, false, "FaxDcs"));
			return ret;
		}

		static readonly TIFFFieldInfo fax3FieldInfo=new TIFFFieldInfo(TIFFTAG.GROUP3OPTIONS, 1, 1, TIFFDataType.TIFF_LONG, FIELD.CCITT_OPTIONS, false, false, "Group3Options");
		static readonly TIFFFieldInfo fax4FieldInfo=new TIFFFieldInfo(TIFFTAG.GROUP4OPTIONS, 1, 1, TIFFDataType.TIFF_LONG, FIELD.CCITT_OPTIONS, false, false, "Group4Options");

		static bool Fax3VSetField(TIFF tif, TIFFTAG tag, TIFFDataType dt, object[] ap)
		{
			Fax3BaseState sp=tif.tif_data as Fax3BaseState;

#if DEBUG
			if(sp==null) throw new Exception("sp==null");
			if(sp.vsetparent==null) throw new Exception("sp.vsetparent==null");
#endif

			switch(tag)
			{
				case TIFFTAG.FAXMODE: sp.mode=(FAXMODE)__GetAsInt(ap, 0); return true; // NB: pseudo tag
				case TIFFTAG.GROUP3OPTIONS:
					// XXX: avoid reading options if compression mismatches.
					if(tif.tif_dir.td_compression==COMPRESSION.CCITTFAX3) sp.groupoptions=(GROUP3OPT)__GetAsUint(ap, 0);
					break;
				case TIFFTAG.GROUP4OPTIONS:
					// XXX: avoid reading options if compression mismatches.
					if(tif.tif_dir.td_compression==COMPRESSION.CCITTFAX4) sp.groupoptions=(GROUP3OPT)__GetAsUint(ap, 0);
					break;
				case TIFFTAG.BADFAXLINES: sp.badfaxlines=__GetAsUint(ap, 0); break;
				case TIFFTAG.CLEANFAXDATA: sp.cleanfaxdata=(CLEANFAXDATA)__GetAsUshort(ap, 0); break;
				case TIFFTAG.CONSECUTIVEBADFAXLINES: sp.badfaxrun=__GetAsUint(ap, 0); break;
				case TIFFTAG.FAXRECVPARAMS: sp.recvparams=__GetAsUint(ap, 0); break;
				case TIFFTAG.FAXSUBADDRESS: sp.subaddress=ap[0] as string; break;
				case TIFFTAG.FAXRECVTIME: sp.recvtime=__GetAsUint(ap, 0); break;
				case TIFFTAG.FAXDCS: sp.faxdcs=ap[0] as string; break;
				default: return sp.vsetparent(tif, tag, dt, ap);
			}

			TIFFFieldInfo fip=TIFFFieldWithTag(tif, tag);
			if(fip!=null) TIFFSetFieldBit(tif, fip.field_bit);
			else return false;

			tif.tif_flags|=TIF_FLAGS.TIFF_DIRTYDIRECT;
			return true;
		}

		static bool Fax3VGetField(TIFF tif, TIFFTAG tag, object[] ap)
		{
			Fax3BaseState sp=tif.tif_data as Fax3BaseState;

#if DEBUG
			if(sp==null) throw new Exception("sp==null");
#endif

			switch(tag)
			{
				case TIFFTAG.FAXMODE: ap[0]=sp.mode; break;
				case TIFFTAG.GROUP3OPTIONS:
				case TIFFTAG.GROUP4OPTIONS: ap[0]=sp.groupoptions; break;
				case TIFFTAG.BADFAXLINES: ap[0]=sp.badfaxlines; break;
				case TIFFTAG.CLEANFAXDATA: ap[0]=sp.cleanfaxdata; break;
				case TIFFTAG.CONSECUTIVEBADFAXLINES: ap[0]=sp.badfaxrun; break;
				case TIFFTAG.FAXRECVPARAMS: ap[0]=sp.recvparams; break;
				case TIFFTAG.FAXSUBADDRESS: ap[0]=sp.subaddress; break;
				case TIFFTAG.FAXRECVTIME: ap[0]=sp.recvtime; break;
				case TIFFTAG.FAXDCS: ap[0]=sp.faxdcs; break;
				default: return sp.vgetparent(tif, tag, ap);
			}

			return true;
		}

		static void Fax3PrintDir(TIFF tif, TextWriter fd, TIFFPRINT flags)
		{
			Fax3BaseState sp=tif.tif_data as Fax3BaseState;

#if DEBUG
			if(sp==null) throw new Exception("sp==null");
#endif

			if(TIFFFieldSet(tif, FIELD.CCITT_OPTIONS))
			{
				if(tif.tif_dir.td_compression==COMPRESSION.CCITTFAX4)
				{
					fd.Write(" Group 4 Options:");
					if((sp.groupoptions&GROUP3OPT.UNCOMPRESSED)!=0) fd.Write(" uncompressed data");
				}
				else
				{
					string sep=" ";
					fd.Write(" Group 3 Options:");
					if((sp.groupoptions&GROUP3OPT._2DENCODING)!=0)
					{
						fd.Write("%s2-d encoding", sep);
						sep="+";
					}
					if((sp.groupoptions&GROUP3OPT.FILLBITS)!=0)
					{
						fd.Write("%sEOL padding", sep);
						sep="+";
					}
					if((sp.groupoptions&GROUP3OPT.UNCOMPRESSED)!=0)
						fd.Write("%suncompressed data", sep);
				}
				fd.WriteLine(" ({0} = 0x{1:X})", sp.groupoptions, (int)sp.groupoptions);
			}

			if(TIFFFieldSet(tif, FIELD.CCITT_CLEANFAXDATA))
			{
				fd.Write(" Fax Data:");
				switch(sp.cleanfaxdata)
				{
					case CLEANFAXDATA.CLEAN: fd.Write(" clean"); break;
					case CLEANFAXDATA.REGENERATED: fd.Write(" receiver regenerated"); break;
					case CLEANFAXDATA.UNCLEAN: fd.Write(" uncorrected errors"); break;
				}
				fd.WriteLine(" ({0} = 0x{1:X})", sp.cleanfaxdata, (int)sp.cleanfaxdata);
			}
			if(TIFFFieldSet(tif, FIELD.CCITT_BADFAXLINES)) fd.WriteLine(" Bad Fax Lines: {0}", sp.badfaxlines);
			if(TIFFFieldSet(tif, FIELD.CCITT_BADFAXRUN)) fd.WriteLine(" Consecutive Bad Fax Lines: {0}", sp.badfaxrun);
			if(TIFFFieldSet(tif, FIELD.CCITT_RECVPARAMS)) fd.WriteLine(" Fax Receive Parameters: {0:X8}", sp.recvparams);
			if(TIFFFieldSet(tif, FIELD.CCITT_SUBADDRESS)) fd.WriteLine(" Fax SubAddress: {0}", sp.subaddress);
			if(TIFFFieldSet(tif, FIELD.CCITT_RECVTIME)) fd.WriteLine(" Fax Receive Time: {0} secs", sp.recvtime);
			if(TIFFFieldSet(tif, FIELD.CCITT_FAXDCS)) fd.WriteLine(" Fax DCS: {0}", sp.faxdcs);
		}

		static bool InitCCITTFax3(TIFF tif)
		{
			Fax3CodecState sp;

			// Merge codec-specific tag information.
			if(!_TIFFMergeFieldInfo(tif, faxFieldInfo))
			{
				TIFFErrorExt(tif.tif_clientdata, "InitCCITTFax3", "Merging common CCITT Fax codec-specific tags failed");
				return false;
			}
			// Allocate state block so tag methods have storage to record values.
			try
			{
				tif.tif_data=sp=new Fax3CodecState();
			}
			catch
			{
				TIFFErrorExt(tif.tif_clientdata, "TIFFInitCCITTFax3", "{0}: No space for state block", tif.tif_name);
				return false;
			}

			sp.rw_mode=tif.tif_mode;

			// Override parent get/set field methods.
			sp.vgetparent=tif.tif_tagmethods.vgetfield;
			tif.tif_tagmethods.vgetfield=Fax3VGetField; // hook for codec tags
			sp.vsetparent=tif.tif_tagmethods.vsetfield;
			tif.tif_tagmethods.vsetfield=Fax3VSetField; // hook for codec tags
			sp.printdir=tif.tif_tagmethods.printdir;
			tif.tif_tagmethods.printdir=Fax3PrintDir;	// hook for codec tags
			sp.groupoptions=0;
			sp.recvparams=0;
			sp.subaddress=null;
			sp.faxdcs=null;

			if(sp.rw_mode==O.RDONLY) // FIXME: improve for in place update
				tif.tif_flags|=TIF_FLAGS.TIFF_NOBITREV; // decoder does bit reversal

			sp.runs=null;
			sp.fill=TIFFFax3fillruns;
			sp.refline=null;

			// Install codec methods.
			tif.tif_setupdecode=Fax3SetupState;
			tif.tif_predecode=Fax3PreDecode;
			tif.tif_decoderow=Fax3Decode1D;
			tif.tif_decodestrip=Fax3Decode1D;
			tif.tif_decodetile=Fax3Decode1D;
			tif.tif_setupencode=Fax3SetupState;
			tif.tif_preencode=Fax3PreEncode;
			tif.tif_postencode=Fax3PostEncode;
			tif.tif_encoderow=Fax3Encode;
			tif.tif_encodestrip=Fax3Encode;
			tif.tif_encodetile=Fax3Encode;
			tif.tif_close=Fax3Close;
			tif.tif_cleanup=Fax3Cleanup;

			return true;
		}

		static bool TIFFInitCCITTFax3(TIFF tif, COMPRESSION scheme)
		{
			if(InitCCITTFax3(tif))
			{
				// Merge codec-specific tag information.
				if(!_TIFFMergeFieldInfo(tif, fax3FieldInfo))
				{
					TIFFErrorExt(tif.tif_clientdata, "TIFFInitCCITTFax3", "Merging CCITT Fax 3 codec-specific tags failed");
					return false;
				}

				// The default format is Class/F-style w/o RTC.
				return TIFFSetField(tif, TIFFTAG.FAXMODE, FAXMODE.CLASSF);
			}

			return true;
		}

		// CCITT Group 4 (T.6) Facsimile-compatible
		// Compression Scheme Support.

		// Decode the requested amount of G4-encoded data.
		static bool Fax4Decode(TIFF tif, byte[] buf, int occ, ushort s)
		{
			string module="Fax4Decode";
			Fax3CodecState sp=tif.tif_data as Fax3CodecState;
			int lastx=(int)sp.rowpixels;	// last element in row
			uint thisrun;					// current row's run array
			byte[] bitmap=sp.bitmap;		// input data bit reverser
			TIFFFaxTabEnt TabEnt;

			uint BitAcc=sp.data;		// bit accumulator
			int BitsAvail=sp.bit;		// # valid bits in BitAcc
			int EOLcnt=sp.EOLcnt;		// # EOL codes recognized
			uint cp=tif.tif_rawcp;		// next byte of input data
			uint ep=cp+tif.tif_rawcc;	// end of input data

			uint buf_ind=0;
			while(occ>0)
			{
				int a0=0;					// reference element
				int RunLength=0;			// length of current run
				uint pa=thisrun=sp.curruns;	// place to stuff next run
				uint pb=sp.refruns;			// next run in reference line
				int b1=(int)sp.runs[pb++];	// next change on prev line

#if FAX3_DEBUG
				Debug.WriteLine("");
				Debug.WriteLine(string.Format("BitAcc={0:X8}, BitsAvail = {1}", BitAcc, BitsAvail));
				Debug.WriteLine(string.Format("-------------------- {0}", tif.tif_row));
#endif

				while(a0<lastx)
				{
					if(BitsAvail<7)
					{
						if(cp>=ep)
						{
							if(BitsAvail==0) goto eof2d;	// no valid bits
							BitsAvail=7;					// pad with zeros
						}
						else
						{
							BitAcc|=((uint)bitmap[tif.tif_rawdata[cp++]])<<BitsAvail;
							BitsAvail+=8;
						}
					}

					TabEnt=TIFFFaxMainTable[BitAcc&((1<<7)-1)];
					BitsAvail-=TabEnt.Width;
					BitAcc>>=TabEnt.Width;

					switch(TabEnt.State)
					{
						case S.Pass:
							if(pa!=thisrun)
							{
								while(b1<=a0&&b1<lastx)
								{
									b1+=(int)sp.runs[pb]+(int)sp.runs[pb+1];
									pb+=2;
								}
							}

							b1+=(int)sp.runs[pb++];
							RunLength+=b1-a0;
							a0=b1;
							b1+=(int)sp.runs[pb++];
							break;
						case S.Horiz:
							if(((pa-thisrun)&1)!=0)
							{
								for(; ; )
								{	// black first
									if(BitsAvail<13)
									{
										if(cp>=ep)
										{
											if(BitsAvail==0) goto eof2d;	// no valid bits
											BitsAvail=13;					// pad with zeros
										}
										else
										{
											BitAcc|=((uint)bitmap[tif.tif_rawdata[cp++]])<<BitsAvail;
											if((BitsAvail+=8)<13)
											{
												if(cp>=ep)
												{
													// NB: we know BitsAvail is non-zero here
													BitsAvail=13;		// pad with zeros
												}
												else
												{
													BitAcc|=((uint)bitmap[tif.tif_rawdata[cp++]])<<BitsAvail;
													BitsAvail+=8;
												}
											}
										}
									}

									TabEnt=TIFFFaxBlackTable[BitAcc&((1<<13)-1)];
									BitsAvail-=TabEnt.Width;
									BitAcc>>=TabEnt.Width;

									bool done=false;
									switch(TabEnt.State)
									{
										case S.TermB:
											sp.runs[pa++]=(uint)RunLength+TabEnt.Param;
											a0+=(int)TabEnt.Param;
											RunLength=0;
											done=true;
											break;
										case S.MakeUpB:
										case S.MakeUp:
											a0+=(int)TabEnt.Param;
											RunLength+=(int)TabEnt.Param;
											break;
										default:
											Fax3Unexpected(module, tif, sp.line, (uint)a0);
											goto eol2d;
									}

									if(done) break;
								}

								for(; ; )
								{	// then white
									if(BitsAvail<12)
									{
										if(cp>=ep)
										{
											if(BitsAvail==0) goto eof2d;	// no valid bits
											BitsAvail=12;					// pad with zeros
										}
										else
										{
											BitAcc|=((uint)bitmap[tif.tif_rawdata[cp++]])<<BitsAvail;
											if((BitsAvail+=8)<12)
											{
												if(cp>=ep)
												{
													// NB: we know BitsAvail is non-zero here
													BitsAvail=12;		// pad with zeros
												}
												else
												{
													BitAcc|=((uint)bitmap[tif.tif_rawdata[cp++]])<<BitsAvail;
													BitsAvail+=8;
												}
											}
										}
									}

									TabEnt=TIFFFaxWhiteTable[BitAcc&((1<<12)-1)];
									BitsAvail-=TabEnt.Width;
									BitAcc>>=TabEnt.Width;

									bool done=false;
									switch(TabEnt.State)
									{
										case S.TermW:
											sp.runs[pa++]=(uint)RunLength+TabEnt.Param;
											a0+=(int)TabEnt.Param;
											RunLength=0;
											done=true;
											break;
										case S.MakeUpW:
										case S.MakeUp:
											a0+=(int)TabEnt.Param;
											RunLength+=(int)TabEnt.Param;
											break;
										default:
											Fax3Unexpected(module, tif, sp.line, (uint)a0);
											goto eol2d;
									}
									if(done) break;
								}
							}
							else
							{
								for(; ; )
								{	// white first
									if(BitsAvail<12)
									{
										if(cp>=ep)
										{
											if(BitsAvail==0) goto eof2d;	// no valid bits
											BitsAvail=12;					// pad with zeros
										}
										else
										{
											BitAcc|=((uint)bitmap[tif.tif_rawdata[cp++]])<<BitsAvail;
											if((BitsAvail+=8)<12)
											{
												if(cp>=ep)
												{
													// NB: we know BitsAvail is non-zero here
													BitsAvail=12;		// pad with zeros
												}
												else
												{
													BitAcc|=((uint)bitmap[tif.tif_rawdata[cp++]])<<BitsAvail;
													BitsAvail+=8;
												}
											}
										}
									}

									TabEnt=TIFFFaxWhiteTable[BitAcc&((1<<12)-1)];
									BitsAvail-=TabEnt.Width;
									BitAcc>>=TabEnt.Width;

									bool done=false;
									switch(TabEnt.State)
									{
										case S.TermW:
											sp.runs[pa++]=(uint)RunLength+TabEnt.Param;
											a0+=(int)TabEnt.Param;
											RunLength=0;
											done=true;
											break;
										case S.MakeUpW:
										case S.MakeUp:
											a0+=(int)TabEnt.Param;
											RunLength+=(int)TabEnt.Param;
											break;
										default:
											Fax3Unexpected(module, tif, sp.line, (uint)a0);
											goto eol2d;
									}

									if(done) break;
								}

								for(; ; )
								{	// then black
									if(BitsAvail<13)
									{
										if(cp>=ep)
										{
											if(BitsAvail==0) goto eof2d;	// no valid bits
											BitsAvail=13;					// pad with zeros
										}
										else
										{
											BitAcc|=((uint)bitmap[tif.tif_rawdata[cp++]])<<BitsAvail;
											if((BitsAvail+=8)<13)
											{
												if(cp>=ep)
												{
													// NB: we know BitsAvail is non-zero here
													BitsAvail=13;		// pad with zeros
												}
												else
												{
													BitAcc|=((uint)bitmap[tif.tif_rawdata[cp++]])<<BitsAvail;
													BitsAvail+=8;
												}
											}
										}
									}

									TabEnt=TIFFFaxBlackTable[BitAcc&((1<<13)-1)];
									BitsAvail-=TabEnt.Width;
									BitAcc>>=TabEnt.Width;

									bool done=false;
									switch(TabEnt.State)
									{
										case S.TermB:
											sp.runs[pa++]=(uint)RunLength+TabEnt.Param;
											a0+=(int)TabEnt.Param;
											RunLength=0;
											done=true;
											break;
										case S.MakeUpB:
										case S.MakeUp:
											a0+=(int)TabEnt.Param;
											RunLength+=(int)TabEnt.Param;
											break;
										default:
											Fax3Unexpected(module, tif, sp.line, (uint)a0);
											goto eol2d;
									}
									if(done) break;
								}
							}

							if(pa!=thisrun)
							{
								while(b1<=a0&&b1<lastx)
								{
									b1+=(int)sp.runs[pb]+(int)sp.runs[pb+1];
									pb+=2;
								}
							}
							break;
						case S.V0:
							if(pa!=thisrun)
							{
								while(b1<=a0&&b1<lastx)
								{
									b1+=(int)sp.runs[pb]+(int)sp.runs[pb+1];
									pb+=2;
								}
							}
							sp.runs[pa++]=(uint)(RunLength+(b1-a0));
							a0+=b1-a0;
							RunLength=0;
							b1+=(int)sp.runs[pb++];
							break;
						case S.VR:
							if(pa!=thisrun)
							{
								while(b1<=a0&&b1<lastx)
								{
									b1+=(int)sp.runs[pb]+(int)sp.runs[pb+1];
									pb+=2;
								}
							}
							sp.runs[pa++]=(uint)(RunLength+(b1-a0+TabEnt.Param));
							a0+=(int)(b1-a0+TabEnt.Param);
							RunLength=0;

							b1+=(int)sp.runs[pb++];
							break;
						case S.VL:
							if(pa!=thisrun)
							{
								while(b1<=a0&&b1<lastx)
								{
									b1+=(int)sp.runs[pb]+(int)sp.runs[pb+1];
									pb+=2;
								}
							}
							sp.runs[pa++]=(uint)(RunLength+(b1-a0-TabEnt.Param));
							a0+=(int)(b1-a0-TabEnt.Param);
							RunLength=0;

							b1-=(int)sp.runs[--pb];
							break;
						case S.Ext:
							sp.runs[pa++]=(uint)(lastx-a0);
							Fax3Extension(module, tif, sp.line, (uint)a0);
							goto eol2d;
						case S.EOL:
							sp.runs[pa++]=(uint)(lastx-a0);

							if(BitsAvail<4)
							{
								if(cp>=ep)
								{
									if(BitsAvail==0) goto eof2d;	// no valid bits
									BitsAvail=4;					// pad with zeros
								}
								else
								{
									BitAcc|=((uint)bitmap[tif.tif_rawdata[cp++]])<<BitsAvail;
									BitsAvail+=8;
								}
							}

							if((BitAcc&((1<<4)-1))!=0) Fax3Unexpected(module, tif, sp.line, (uint)a0);
							BitsAvail-=4;
							BitAcc>>=4;
							EOLcnt=1;
							goto eol2d;
						default:
							Fax3Unexpected(module, tif, sp.line, (uint)a0);
							goto eol2d;
					} // switch(TabEnt.State)
				} // while(a0<lastx)

				if(RunLength!=0)
				{
					if(RunLength+a0<lastx)
					{
						// expect a final V0
						if(BitsAvail<1)
						{
							if(cp>=ep)
							{
								if(BitsAvail==0) goto eof2d;	// no valid bits
								BitsAvail=1;					// pad with zeros
							}
							else
							{
								BitAcc|=((uint)bitmap[tif.tif_rawdata[cp++]])<<BitsAvail;
								BitsAvail+=8;
							}
						}

						if((BitAcc&1)==0)
						{
							Fax3Unexpected(module, tif, sp.line, (uint)a0);
							goto eol2d;
						}
						BitsAvail-=1;
						BitAcc>>=1;
					}

					sp.runs[pa++]=(uint)RunLength;
					RunLength=0;
				} // if(RunLength!=0)

eol2d:
				if(RunLength!=0)
				{
					sp.runs[pa++]=(uint)RunLength;
					RunLength=0;
				}

				if(a0!=lastx)
				{
					Fax3BadLength(module, tif, sp.line, (uint)a0, (uint)lastx);
					while(a0>lastx&&pa>thisrun) a0-=(int)sp.runs[--pa];
					if(a0<lastx)
					{
						if(a0<0) a0=0;
						if(((pa-thisrun)&1)!=0)
						{
							sp.runs[pa++]=(uint)RunLength;
							RunLength=0;
						}

						sp.runs[pa++]=(uint)(RunLength+(lastx-a0));
						a0+=(int)(lastx-a0);
						RunLength=0;
					}
					else if(a0>lastx)
					{
						sp.runs[pa++]=(uint)(RunLength+lastx);
						a0+=lastx;
						RunLength=0;

						sp.runs[pa++]=(uint)RunLength;
						RunLength=0;
					}
				} // if(a0!=lastx)

				if(EOLcnt!=0) goto EOFG4;

				sp.fill(buf, buf_ind, sp.runs, thisrun, pa, (uint)lastx);
				sp.runs[pa++]=(uint)RunLength; // imaginary change for reference
				RunLength=0;
				uint tmp=sp.curruns; sp.curruns=sp.refruns; sp.refruns=tmp;
				buf_ind+=sp.rowbytes;
				occ-=(int)sp.rowbytes;
				sp.line++;

				continue; // while(occ>0)

eof2d:
				Fax3PrematureEOF(module, tif, sp.line, (uint)a0);

				if(RunLength!=0)
				{
					sp.runs[pa++]=(uint)RunLength;
					RunLength=0;
				}

				if(a0!=lastx)
				{
					Fax3BadLength(module, tif, sp.line, (uint)a0, (uint)lastx);
					while(a0>lastx&&pa>thisrun) a0-=(int)sp.runs[--pa];
					if(a0<lastx)
					{
						if(a0<0) a0=0;
						if(((pa-thisrun)&1)!=0)
						{
							sp.runs[pa++]=(uint)RunLength;
							RunLength=0;
						}

						sp.runs[pa++]=(uint)(RunLength+(lastx-a0));
						a0+=(int)(lastx-a0);
						RunLength=0;
					}
					else if(a0>lastx)
					{
						sp.runs[pa++]=(uint)(RunLength+lastx);
						a0+=lastx;
						RunLength=0;

						sp.runs[pa++]=(uint)RunLength;
						RunLength=0;
					}
				} // if(a0!=lastx)

EOFG4:
				if(BitsAvail<13)
				{
					if(cp>=ep)
					{
						if(BitsAvail==0) goto BADG4;	// no valid bits
						BitsAvail=13;					// pad with zeros
					}
					else
					{
						BitAcc|=((uint)bitmap[tif.tif_rawdata[cp++]])<<BitsAvail;
						if((BitsAvail+=8)<13)
						{
							if(cp>=ep)
							{
								// NB: we know BitsAvail is non-zero here
								BitsAvail=13;		// pad with zeros
							}
							else
							{
								BitAcc|=((uint)bitmap[tif.tif_rawdata[cp++]])<<BitsAvail;
								BitsAvail+=8;
							}
						}
					}
				}

BADG4:
#if FAX3_DEBUG
				if((BitAcc&((1<<13)-1))!=0x1001) Debug.WriteLine("Bad EOFB");
#endif
				BitsAvail-=13;
				BitAcc>>=13;
				sp.fill(buf, buf_ind, sp.runs, thisrun, pa, (uint)lastx);

				sp.bit=BitsAvail;
				sp.data=BitAcc;
				sp.EOLcnt=EOLcnt;
				tif.tif_rawcc-=cp-tif.tif_rawcp;
				tif.tif_rawcp=cp;

				return sp.line!=0;	// don't error on badly-terminated strips
			} // while(occ>0)

			sp.bit=BitsAvail;
			sp.data=BitAcc;
			sp.EOLcnt=EOLcnt;
			tif.tif_rawcc-=cp-tif.tif_rawcp;
			tif.tif_rawcp=cp;

			return true;
		}

		// Encode the requested amount of data.
		static bool Fax4Encode(TIFF tif, byte[] bp, int cc, ushort s)
		{
			Fax3CodecState sp=tif.tif_data as Fax3CodecState;

			uint bp_ind=0;
			while(cc>0)
			{
				if(!Fax3Encode2DRow(tif, bp, bp_ind, sp.refline, sp.rowpixels)) return false;
				Array.Copy(bp, bp_ind, sp.refline, 0, sp.rowbytes);
				bp_ind+=sp.rowbytes;
				cc-=(int)sp.rowbytes;
			}
			return true;
		}

		static bool Fax4PostEncode(TIFF tif)
		{
			Fax3CodecState sp=tif.tif_data as Fax3CodecState;

			// terminate strip w/ EOFB
			Fax3PutBits(tif, EOL, 12);
			Fax3PutBits(tif, EOL, 12);
			if(sp.bit!=8)
			{
				if(tif.tif_rawcc>=tif.tif_rawdatasize) TIFFFlushData1(tif);
				tif.tif_rawdata[tif.tif_rawcp++]=(byte)sp.data;
				tif.tif_rawcc++;
				sp.data=0;
				sp.bit=8;
			}
			return true;
		}

		static bool TIFFInitCCITTFax4(TIFF tif, COMPRESSION scheme)
		{
			if(InitCCITTFax3(tif))
			{ // reuse G3 support
				// Merge codec-specific tag information.
				if(!_TIFFMergeFieldInfo(tif, fax4FieldInfo))
				{
					TIFFErrorExt(tif.tif_clientdata, "TIFFInitCCITTFax4", "Merging CCITT Fax 4 codec-specific tags failed");
					return false;
				}

				tif.tif_decoderow=Fax4Decode;
				tif.tif_decodestrip=Fax4Decode;
				tif.tif_decodetile=Fax4Decode;
				tif.tif_encoderow=Fax4Encode;
				tif.tif_encodestrip=Fax4Encode;
				tif.tif_encodetile=Fax4Encode;
				tif.tif_postencode=Fax4PostEncode;

				// Suppress RTC at the end of each strip.
				return TIFFSetField(tif, TIFFTAG.FAXMODE, FAXMODE.NORTC);
			}

			return false;
		}

		// CCITT Group 3 1-D Modified Huffman RLE Compression Support.
		// (Compression algorithms 2 and 32771)

		// Decode the requested amount of RLE-encoded data.
		static bool Fax3DecodeRLE(TIFF tif, byte[] buf, int occ, ushort s)
		{
			string module="Fax3DecodeRLE";
			Fax3CodecState sp=tif.tif_data as Fax3CodecState;

			int lastx=(int)sp.rowpixels;	// last element in row
			byte[] bitmap=sp.bitmap;		// input data bit reverser
			FAXMODE mode=sp.mode;

			uint BitAcc=sp.data;		// bit accumulator
			int BitsAvail=sp.bit;		// # valid bits in BitAcc
			int EOLcnt=sp.EOLcnt;		// # EOL codes recognized
			uint cp=tif.tif_rawcp;		// next byte of input data
			uint ep=cp+tif.tif_rawcc;	// end of input data
			uint thisrun=sp.curruns;	// current row's run array

			uint buf_ind=0;
			while(occ>0)
			{
				int a0=0;				// reference element
				int RunLength=0;		// length of current run
				uint pa=thisrun;		// place to stuff next run

#if FAX3_DEBUG
				Debug.WriteLine(string.Format("\nBitAcc={0:X8}, BitsAvail = {1}", BitAcc, BitsAvail));
				Debug.WriteLine(string.Format("-------------------- {0}", tif.tif_row));
#endif

				for(; ; )
				{
					for(; ; )
					{
						if(BitsAvail<12)
						{
							if(cp>=ep)
							{
								if(BitsAvail==0) goto eof1d; // no valid bits
								BitsAvail=12; // pad with zeros
							}
							else
							{
								BitAcc|=((uint)bitmap[tif.tif_rawdata[cp++]])<<BitsAvail;
								if((BitsAvail+=8)<12)
								{
									if(cp>=ep)
									{
										// NB: we know BitsAvail is non-zero here
										BitsAvail=12; // pad with zeros
									}
									else
									{
										BitAcc|=((uint)bitmap[tif.tif_rawdata[cp++]])<<BitsAvail;
										BitsAvail+=8;
									}
								}
							}
						}

						TIFFFaxTabEnt TabEnt=TIFFFaxWhiteTable[BitAcc&((1<<12)-1)];
						BitsAvail-=TabEnt.Width;
						BitAcc>>=TabEnt.Width;

						bool done=false;
						switch(TabEnt.State)
						{
							case S.EOL:
								EOLcnt=1;
								goto done1d;
							case S.TermW:
								sp.runs[pa++]=(uint)(RunLength+TabEnt.Param);
								a0+=(int)TabEnt.Param;
								RunLength=0;
								done=true;
								break;
							case S.MakeUpW:
							case S.MakeUp:
								a0+=(int)TabEnt.Param;
								RunLength+=(int)TabEnt.Param;
								break;
							default:
								Fax3Unexpected(module, tif, sp.line, (uint)a0);
								goto done1d;
						}
						if(done) break;
					} // for(; ; ) // 2

					if(a0>=lastx) goto done1d;
					for(; ; )
					{
						if(BitsAvail<13)
						{
							if(cp>=ep)
							{
								if(BitsAvail==0) goto eof1d; // no valid bits
								BitsAvail=13; // pad with zeros
							}
							else
							{
								BitAcc|=((uint)bitmap[tif.tif_rawdata[cp++]])<<BitsAvail;
								if((BitsAvail+=8)<13)
								{
									if(cp>=ep)
									{
										// NB: we know BitsAvail is non-zero here
										BitsAvail=13; // pad with zeros
									}
									else
									{
										BitAcc|=((uint)bitmap[tif.tif_rawdata[cp++]])<<BitsAvail;
										BitsAvail+=8;
									}
								}
							}
						}

						TIFFFaxTabEnt TabEnt=TIFFFaxBlackTable[BitAcc&((1<<13)-1)];
						BitsAvail-=TabEnt.Width;
						BitAcc>>=TabEnt.Width;

						bool done=false;
						switch(TabEnt.State)
						{
							case S.EOL:
								EOLcnt=1;
								goto done1d;
							case S.TermB:
								sp.runs[pa++]=(uint)(RunLength+TabEnt.Param);
								a0+=(int)TabEnt.Param;
								RunLength=0;
								done=true;
								break;
							case S.MakeUpB:
							case S.MakeUp:
								a0+=(int)TabEnt.Param;
								RunLength+=(int)TabEnt.Param;
								break;
							default:
								Fax3Unexpected(module, tif, sp.line, (uint)a0);
								goto done1d;
						}
						if(done) break;
					} // for(; ; ) // 3

					if(a0>=lastx) goto done1d;
					if(sp.runs[pa-1]==0&&sp.runs[pa-2]==0) pa-=2;
				} // for(; ; ) // 1

eof1d:
				Fax3PrematureEOF(module, tif, sp.line, (uint)a0);

				if(RunLength!=0)
				{
					sp.runs[pa++]=(uint)RunLength;
					a0+=0;
					RunLength=0;
				}

				if(a0!=lastx)
				{
					Fax3BadLength(module, tif, sp.line, (uint)a0, (uint)lastx);
					while(a0>lastx&&pa>thisrun) a0-=(int)sp.runs[--pa];
					if(a0<lastx)
					{
						if(a0<0) a0=0;
						if(((pa-thisrun)&1)!=0)
						{
							sp.runs[pa++]=(uint)RunLength;
							a0+=0;
							RunLength=0;
						}
						sp.runs[pa++]=(uint)(RunLength+(lastx-a0));
						a0+=(lastx-a0);
						RunLength=0;
					}
					else if(a0>lastx)
					{
						sp.runs[pa++]=(uint)(RunLength+lastx);
						a0+=lastx;
						RunLength=0;

						sp.runs[pa++]=(uint)RunLength;
						a0+=0;
						RunLength=0;
					}
				} // if(a0!=lastx)

				// premature EOF
				sp.fill(buf, buf_ind, sp.runs, thisrun, pa, (uint)lastx);

				sp.bit=BitsAvail;
				sp.data=BitAcc;
				sp.EOLcnt=EOLcnt;
				tif.tif_rawcc-=cp-tif.tif_rawcp;
				tif.tif_rawcp=cp;

				return false;

done1d:
				if(RunLength!=0)
				{
					sp.runs[pa++]=(uint)RunLength;
					a0+=0;
					RunLength=0;
				}

				if(a0!=lastx)
				{
					Fax3BadLength(module, tif, sp.line, (uint)a0, (uint)lastx);
					while(a0>lastx&&pa>thisrun) a0-=(int)sp.runs[--pa];
					if(a0<lastx)
					{
						if(a0<0) a0=0;
						if(((pa-thisrun)&1)!=0)
						{
							sp.runs[pa++]=(uint)RunLength;
							a0+=0;
							RunLength=0;
						}
						sp.runs[pa++]=(uint)(RunLength+(lastx-a0));
						a0+=(lastx-a0);
						RunLength=0;
					}
					else if(a0>lastx)
					{
						sp.runs[pa++]=(uint)(RunLength+lastx);
						a0+=lastx;
						RunLength=0;

						sp.runs[pa++]=(uint)RunLength;
						a0+=0;
						RunLength=0;
					}
				} // if(a0!=lastx)

				sp.fill(buf, buf_ind, sp.runs, thisrun, pa, (uint)lastx);

				// Cleanup at the end of the row.
				if((mode&FAXMODE.BYTEALIGN)!=0)
				{
					int n=BitsAvail-(BitsAvail&~7);
					BitsAvail-=n;
					BitAcc>>=n;
				}
				else if((mode&FAXMODE.WORDALIGN)!=0)
				{
					int n=BitsAvail-(BitsAvail&~15);
					BitsAvail-=n;
					BitAcc>>=n;
					if(BitsAvail==0&&!isAligned(cp, sizeof(ushort))) cp++;
				}

				buf_ind+=sp.rowbytes;
				occ-=(int)sp.rowbytes;
				sp.line++;
			} // while(occ>0)

			sp.bit=BitsAvail;
			sp.data=BitAcc;
			sp.EOLcnt=EOLcnt;
			tif.tif_rawcc-=cp-tif.tif_rawcp;
			tif.tif_rawcp=cp;

			return true;
		}

		static bool TIFFInitCCITTRLE(TIFF tif, COMPRESSION scheme)
		{
			if(InitCCITTFax3(tif))
			{ // reuse G3 support
				tif.tif_decoderow=Fax3DecodeRLE;
				tif.tif_decodestrip=Fax3DecodeRLE;
				tif.tif_decodetile=Fax3DecodeRLE;
				// Suppress RTC+EOLs when encoding and byte-align data.
				return TIFFSetField(tif, TIFFTAG.FAXMODE, FAXMODE.NORTC|FAXMODE.NOEOL|FAXMODE.BYTEALIGN);
			}
			return false;
		}

		static bool TIFFInitCCITTRLEW(TIFF tif, COMPRESSION scheme)
		{
			if(InitCCITTFax3(tif))
			{ // reuse G3 support
				tif.tif_decoderow=Fax3DecodeRLE;
				tif.tif_decodestrip=Fax3DecodeRLE;
				tif.tif_decodetile=Fax3DecodeRLE;
				// Suppress RTC+EOLs when encoding and word-align data.
				return TIFFSetField(tif, TIFFTAG.FAXMODE, FAXMODE.NORTC|FAXMODE.NOEOL|FAXMODE.WORDALIGN);
			}

			return false;
		}
	}
}
#endif // CCITT_SUPPORT