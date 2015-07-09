#if THUNDER_SUPPORT
// tif_thunder.cs
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
// ThunderScan 4-bit Compression Algorithm Support

using System;
using System.Collections.Generic;
using System.Text;

namespace Free.Ports.LibTiff
{
	public static partial class libtiff
	{
		// ThunderScan uses an encoding scheme designed for
		// 4-bit pixel values. Data is encoded in bytes, with
		// each byte split into a 2-bit code word and a 6-bit
		// data value. The encoding gives raw data, runs of
		// pixels, or pixel values encoded as a delta from the
		// previous pixel value. For the latter, either 2-bit
		// or 3-bit delta values are used, with the deltas packed
		// into a single byte.
		const int THUNDER_DATA=0x3f;		// mask for 6-bit data
		const int THUNDER_CODE=0xc0;		// mask for 2-bit code word
		// code values
		const int THUNDER_RUN=0x00;			// run of pixels w/ encoded count
		const int THUNDER_2BITDELTAS=0x40;	// 3 pixels w/ encoded 2-bit deltas
		const int DELTA2_SKIP=2;			// skip code for 2-bit deltas
		const int THUNDER_3BITDELTAS=0x80;	// 2 pixels w/ encoded 3-bit deltas
		const int DELTA3_SKIP=4;			// skip code for 3-bit deltas
		const int THUNDER_RAW=0xc0;			// raw data encoded

		static readonly int[] twobitdeltas=new int[4] { 0, 1, 0, -1 };
		static readonly int[] threebitdeltas=new int[8] { 0, 1, 2, 3, 0, -3, -2, -1 };

		static bool ThunderDecode(TIFF tif, byte[] buf, uint buf_offset, uint maxpixels)
		{
			uint bp=tif.tif_rawcp;
			int cc=(int)tif.tif_rawcc;
			byte lastpixel=0;
			int npixels=0;

			unsafe
			{
				fixed(byte* buf_=buf)
				{
					byte* op=buf_+buf_offset;

					while(cc>0&&npixels<maxpixels)
					{
						int n, delta;

						n=tif.tif_rawdata[bp++];
						cc--;
						switch(n&THUNDER_CODE)
						{
							case THUNDER_RUN: // pixel run
								// Replicate the last pixel n times,
								// where n is the lower-order 6 bits.
								if((npixels&1)!=0)
								{
									op[0]|=lastpixel;
									lastpixel=*op++;
									npixels++;
									n--;
								}
								else lastpixel|=(byte)(lastpixel<<4);

								npixels+=n;
								if(npixels<maxpixels)
								{
									for(; n>0; n-=2) *op++=lastpixel;
								}
								if(n==-1) *--op&=0xf0;
								lastpixel&=0xf;
								break;
							case THUNDER_2BITDELTAS: // 2-bit deltas
								if((delta=((n>>4)&3))!=DELTA2_SKIP)
								{
									lastpixel=(byte)((lastpixel+twobitdeltas[delta])&0xf);
									if((npixels++&1)!=0) *op++|=(byte)lastpixel;
									else op[0]=(byte)(lastpixel<<4);
								}
								if((delta=((n>>2)&3))!=DELTA2_SKIP)
								{
									lastpixel=(byte)((lastpixel+twobitdeltas[delta])&0xf);
									if((npixels++&1)!=0) *op++|=(byte)lastpixel;
									else op[0]=(byte)(lastpixel<<4);
								}
								if((delta=(n&3))!=DELTA2_SKIP)
								{
									lastpixel=(byte)((lastpixel+twobitdeltas[delta])&0xf);
									if((npixels++&1)!=0) *op++|=(byte)lastpixel;
									else op[0]=(byte)(lastpixel<<4);
								}
								break;
							case THUNDER_3BITDELTAS: // 3-bit deltas
								if((delta=((n>>3)&7))!=DELTA3_SKIP)
								{
									lastpixel=(byte)((lastpixel+threebitdeltas[delta])&0xf);
									if((npixels++&1)!=0) *op++|=(byte)lastpixel;
									else op[0]=(byte)(lastpixel<<4);
								}
								if((delta=(n&7))!=DELTA3_SKIP)
								{
									lastpixel=(byte)((lastpixel+threebitdeltas[delta])&0xf);
									if((npixels++&1)!=0) *op++|=(byte)lastpixel;
									else op[0]=(byte)(lastpixel<<4);
								}
								break;
							case THUNDER_RAW: // raw data
								lastpixel=(byte)(n&0xf);
								if((npixels++&1)!=0) *op++|=(byte)lastpixel;
								else op[0]=(byte)(lastpixel<<4);
								break;
						}
					}
				}
			}

			tif.tif_rawcp=bp;
			tif.tif_rawcc=(uint)cc;

			if(npixels!=maxpixels)
			{
				TIFFErrorExt(tif.tif_clientdata, tif.tif_name, "ThunderDecode: {0} data at scanline {1} ({2} != {3})", npixels<maxpixels?"Not enough":"Too much", tif.tif_row, npixels, maxpixels);
				return false;
			}
			return true;
		}

		static bool ThunderDecodeRow(TIFF tif, byte[] buf, int occ, ushort s)
		{
			uint row=0;
			while(occ>0)
			{
				if(!ThunderDecode(tif, buf, row, tif.tif_dir.td_imagewidth)) return false;
				occ-=(int)tif.tif_scanlinesize;
				row+=tif.tif_scanlinesize;
			}
			return true;
		}

		static bool TIFFInitThunderScan(TIFF tif, COMPRESSION scheme)
		{
			tif.tif_decoderow=ThunderDecodeRow;
			tif.tif_decodestrip=ThunderDecodeRow;
			return true;
		}
	}
}
#endif // THUNDER_SUPPORT
