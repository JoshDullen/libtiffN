#if PACKBITS_SUPPORT
// tif_packbits.cs
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
// PackBits Compression Algorithm Support

using System;
using System.Collections.Generic;
using System.Text;

namespace Free.Ports.LibTiff
{
	public static partial class libtiff
	{
		class PackBitsState : ICodecState
		{
			internal int data;
		}

		static bool PackBitsPreEncode(TIFF tif, ushort sampleNumber)
		{
			PackBitsState state=null;

			try
			{
				tif.tif_data=state=new PackBitsState();
			}
			catch
			{
				return false;
			}
			
			// Calculate the scanline/tile-width size in bytes.
			if(isTiled(tif)) state.data=TIFFTileRowSize(tif);
			else state.data=TIFFScanlineSize(tif);

			return true;
		}

		static bool PackBitsPostEncode(TIFF tif)
		{
			tif.tif_data=null;
			return true;
		}

		enum PackBitsEncodeState
		{
			BASE,
			LITERAL,
			RUN,
			LITERAL_RUN
		}

		const byte Minus1=255;
		const byte Minus127=129;

		// Encode a run of pixels.
		static bool PackBitsEncode(TIFF tif, byte[] buf, int cc, ushort s)
		{
			return PackBitsEncode1(tif, buf, 0, cc, s);
		}

		// Encode a run of pixels to *(buf+buf_offset)..
		static bool PackBitsEncode1(TIFF tif, byte[] buf, int buf_offset, int cc, ushort s)
		{
			unsafe
			{
				fixed(byte* bp_=buf)
				{
					byte* bp=bp_+buf_offset;

					uint op=tif.tif_rawcp;
					uint ep=tif.tif_rawdatasize;
					uint lastliteral=0;
					PackBitsEncodeState state=PackBitsEncodeState.BASE;

					while(cc>0)
					{
						// Find the longest string of identical bytes.
						byte b=*bp++;
						cc--;
						int n=1;
						for(; cc>0&&b==*bp; cc--, bp++) n++;
again:
						if(op+2>=ep)
						{ // insure space for new data
							// Be careful about writing the last
							// literal. Must write up to that point
							// and then copy the remainder to the
							// front of the buffer.
							if(state==PackBitsEncodeState.LITERAL||state==PackBitsEncodeState.LITERAL_RUN)
							{
								uint slop=op-lastliteral;
								tif.tif_rawcc+=lastliteral-tif.tif_rawcp;
								if(!TIFFFlushData1(tif)) return false;
								op=tif.tif_rawcp;
								while((slop--)>0) tif.tif_rawdata[op++]=tif.tif_rawdata[lastliteral++];
								lastliteral=tif.tif_rawcp;
							}
							else
							{
								tif.tif_rawcc+=op-tif.tif_rawcp;
								if(!TIFFFlushData1(tif)) return false;
								op=tif.tif_rawcp;
							}
						}
						switch(state)
						{
							case PackBitsEncodeState.BASE: // initial state, set run/literal
								if(n>1)
								{
									state=PackBitsEncodeState.RUN;
									if(n>128)
									{
										tif.tif_rawdata[op++]=Minus127;
										tif.tif_rawdata[op++]=b;
										n-=128;
										goto again;
									}
									tif.tif_rawdata[op++]=(byte)(-(n-1));
									tif.tif_rawdata[op++]=b;
								}
								else
								{
									lastliteral=op;
									tif.tif_rawdata[op++]=0;
									tif.tif_rawdata[op++]=b;
									state=PackBitsEncodeState.LITERAL;
								}
								break;
							case PackBitsEncodeState.LITERAL: // last object was literal string
								if(n>1)
								{
									state=PackBitsEncodeState.LITERAL_RUN;
									if(n>128)
									{
										tif.tif_rawdata[op++]=Minus127;
										tif.tif_rawdata[op++]=b;
										n-=128;
										goto again;
									}
									tif.tif_rawdata[op++]=(byte)(-(n-1)); // encode run
									tif.tif_rawdata[op++]=b;
								}
								else
								{ // extend literal
									tif.tif_rawdata[lastliteral]++;
									if(tif.tif_rawdata[lastliteral]==127) state=PackBitsEncodeState.BASE;
									tif.tif_rawdata[op++]=b;
								}
								break;
							case PackBitsEncodeState.RUN: // last object was run
								if(n>1)
								{
									if(n>128)
									{
										tif.tif_rawdata[op++]=Minus127;
										tif.tif_rawdata[op++]=b;
										n-=128;
										goto again;
									}
									tif.tif_rawdata[op++]=(byte)(-(n-1));
									tif.tif_rawdata[op++]=b;
								}
								else
								{
									lastliteral=op;
									tif.tif_rawdata[op++]=0;
									tif.tif_rawdata[op++]=b;
									state=PackBitsEncodeState.LITERAL;
								}
								break;
							case PackBitsEncodeState.LITERAL_RUN: // literal followed by a run
								// Check to see if previous run should
								// be converted to a literal, in which
								// case we convert literal-run-literal
								// to a single literal.
								if(n==1&&tif.tif_rawdata[op-2]==Minus1&&tif.tif_rawdata[lastliteral]<126)
								{
									state=((tif.tif_rawdata[lastliteral]+=2)==127?PackBitsEncodeState.BASE:PackBitsEncodeState.LITERAL);
									tif.tif_rawdata[op-2]=tif.tif_rawdata[op-1]; // replicate
								}
								else state=PackBitsEncodeState.RUN;
								goto again;
						}
					}
					tif.tif_rawcc+=op-tif.tif_rawcp;
					tif.tif_rawcp=op;
				}
			}
			return true;
		}

		// Encode a rectangular chunk of pixels. We break it up
		// into row-sized pieces to insure that encoded runs do
		// not span rows. Otherwise, there can be problems with
		// the decoder if data is read, for example, by scanlines
		// when it was encoded by strips.
		static bool PackBitsEncodeChunk(TIFF tif, byte[] buf, int cc, ushort s)
		{
			int rowsize=((PackBitsState)tif.tif_data).data;
			int bp=0;
			while(cc>0)
			{
				int chunk=rowsize;
				if(cc<chunk) chunk=cc;

				if(!PackBitsEncode1(tif, buf, bp, chunk, s)) return false;
				bp+=chunk;
				cc-=chunk;
			}

			return true;
		}

		static bool PackBitsDecode(TIFF tif, byte[] op, int occ, ushort s)
		{
			uint bp=tif.tif_rawcp;
			int cc=(int)tif.tif_rawcc;

			uint op_ind=0;
			while(cc>0&&(long)occ>0)
			{
				int n=tif.tif_rawdata[bp++];
				cc--;
				// Watch out for compilers that
				// don't sign extend chars...
				if(n>=128) n-=256;
				if(n<0)
				{ // replicate next byte -n+1 times
					if(n==-128) continue; // nop
					n=-n+1;
					if(occ<n)
					{
						TIFFWarningExt(tif.tif_clientdata, tif.tif_name, "PackBitsDecode: discarding {0} bytes to avoid buffer overrun", n-occ);
						n=occ;
					}
					occ-=n;
					byte b=tif.tif_rawdata[bp++];
					cc--;
					while((n--)>0) op[op_ind++]=b;
				}
				else
				{ // copy next n+1 bytes literally
					if(occ<n+1)
					{
						TIFFWarningExt(tif.tif_clientdata, tif.tif_name, "PackBitsDecode: discarding {0} bytes to avoid buffer overrun", n-occ+1);
						n=occ-1;
					}
					n++;
					Array.Copy(tif.tif_rawdata, bp, op, op_ind, n);
					op_ind+=(uint)n; occ-=n;
					bp+=(uint)n; cc-=n;
				}
			}
			tif.tif_rawcp=bp;
			tif.tif_rawcc=(uint)cc;
			if(occ>0)
			{
				TIFFErrorExt(tif.tif_clientdata, tif.tif_name, "PackBitsDecode: Not enough data for scanline {0}", tif.tif_row);
				return false;
			}
			return true;
		}

		static bool TIFFInitPackBits(TIFF tif, COMPRESSION scheme)
		{
			//tif.tif_predecode=PackBitsPreDecode;
			tif.tif_decoderow=PackBitsDecode;
			tif.tif_decodestrip=PackBitsDecode;
			tif.tif_decodetile=PackBitsDecode;
			tif.tif_preencode=PackBitsPreEncode;
			tif.tif_postencode=PackBitsPostEncode;
			tif.tif_encoderow=PackBitsEncode;
			tif.tif_encodestrip=PackBitsEncodeChunk;
			tif.tif_encodetile=PackBitsEncodeChunk;
			return true;
		}
	}
}
#endif // PACKBITS_SUPPORT