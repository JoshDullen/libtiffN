// tif_strip.cs
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
// Strip-organized Image Support Routines.

using System;
using System.Collections.Generic;
using System.Text;

namespace Free.Ports.LibTiff
{
	public static partial class libtiff
	{
		static uint summarize(TIFF tif, uint summand1, uint summand2, string where)
		{
			uint bytes=summand1+summand2;

			if(bytes-summand1!=summand2)
			{
				TIFFErrorExt(tif.tif_clientdata, tif.tif_name, "Integer overflow in {0}", where);
				bytes=0;
			}

			return bytes;
		}

		static uint multiply(TIFF tif, uint nmemb, uint elem_size, string where)
		{
			if(elem_size==0) return 0;

			uint bytes=nmemb*elem_size;

			if(bytes/elem_size!=nmemb)
			{
				TIFFErrorExt(tif.tif_clientdata, tif.tif_name, "Integer overflow in {0}", where);
				bytes=0;
			}

			return bytes;
		}

		// Compute which strip a (row, sample) value is in.
		public static int TIFFComputeStrip(TIFF tif, uint row, ushort sample)
		{
			TIFFDirectory td=tif.tif_dir;
			uint strip;

			strip=row/td.td_rowsperstrip;
			if(td.td_planarconfig==PLANARCONFIG.SEPARATE)
			{
				if(sample>=td.td_samplesperpixel)
				{
					TIFFErrorExt(tif.tif_clientdata, tif.tif_name, "{0}: Sample out of range, max {1}", sample, td.td_samplesperpixel);
					return 0;
				}
				strip+=sample*td.td_stripsperimage;
			}
			return (int)strip;
		}

		// Compute how many strips are in an image.
		public static int TIFFNumberOfStrips(TIFF tif)
		{
			TIFFDirectory td=tif.tif_dir;
			uint nstrips;

			nstrips=(td.td_rowsperstrip==0xffffffff?1:TIFFhowmany(td.td_imagelength, td.td_rowsperstrip));
			if(td.td_planarconfig==PLANARCONFIG.SEPARATE) nstrips=multiply(tif, nstrips, td.td_samplesperpixel, "TIFFNumberOfStrips");
			return (int)nstrips;
		}

		// Compute the # bytes in a variable height, row-aligned strip.
		public static int TIFFVStripSize(TIFF tif, uint nrows)
		{
			TIFFDirectory td=tif.tif_dir;

			if(nrows==0xffffffff) nrows=td.td_imagelength;

			if(td.td_planarconfig==PLANARCONFIG.CONTIG&&td.td_photometric==PHOTOMETRIC.YCBCR&&!isUpSampled(tif))
			{
				// Packed YCbCr data contain one Cb+Cr for every
				// HorizontalSampling*VerticalSampling Y values.
				// Must also roundup width and height when calculating
				// since images that are not a multiple of the
				// horizontal/vertical subsampling area include
				// YCbCr data for the extended image.
				object[] ap=new object[2];
				TIFFGetField(tif, TIFFTAG.YCBCRSUBSAMPLING, ap);
				ushort ycbcrsubsampling0=__GetAsUshort(ap, 0);
				ushort ycbcrsubsampling1=__GetAsUshort(ap, 1);

				uint samplingarea=(uint)(ycbcrsubsampling0*ycbcrsubsampling1);
				if(samplingarea==0)
				{
					TIFFErrorExt(tif.tif_clientdata, tif.tif_name, "Invalid YCbCr subsampling");
					return 0;
				}

				uint w=TIFFroundup(td.td_imagewidth, ycbcrsubsampling0);
				uint scanline=TIFFhowmany8(multiply(tif, w, td.td_bitspersample, "TIFFVStripSize"));
				nrows=TIFFroundup(nrows, ycbcrsubsampling1);

				// NB: don't need TIFFhowmany here 'cuz everything is rounded
				scanline=multiply(tif, nrows, scanline, "TIFFVStripSize");
				return (int)summarize(tif, scanline, multiply(tif, 2, (scanline/samplingarea), "TIFFVStripSize"), "TIFFVStripSize");
			}

			return (int)multiply(tif, nrows, (uint)TIFFScanlineSize(tif), "TIFFVStripSize");
		}

		// Compute the # bytes in a raw strip.
		public static int TIFFRawStripSize(TIFF tif, uint strip)
		{
			TIFFDirectory td=tif.tif_dir;
			int bytecount=(int)td.td_stripbytecount[strip];

			if(bytecount<=0)
			{
				TIFFErrorExt(tif.tif_clientdata, tif.tif_name, "{0}: Invalid strip byte count, strip {1}", bytecount, strip);
				return -1;
			}

			return bytecount;
		}

		// Compute the # bytes in a (row-aligned) strip.
		//
		// Note that if RowsPerStrip is larger than the
		// recorded ImageLength, then the strip size is
		// truncated to reflect the actual space required
		// to hold the strip.
		public static int TIFFStripSize(TIFF tif)
		{
			TIFFDirectory td=tif.tif_dir;
			uint rps=td.td_rowsperstrip;
			if(rps>td.td_imagelength) rps=td.td_imagelength;
			return TIFFVStripSize(tif, rps);
		}

		// Compute a default strip size based on the image
		// characteristics and a requested value. If the
		// request is <1 then we choose a strip size according
		// to certain heuristics.
		public static uint TIFFDefaultStripSize(TIFF tif, uint request)
		{
			return tif.tif_defstripsize(tif, request);
		}

		static uint _TIFFDefaultStripSize(TIFF tif, uint s)
		{
			if((int)s<1)
			{
				// If RowsPerStrip is unspecified, try to break the
				// image up into strips that are approximately
				// STRIP_SIZE_DEFAULT bytes long.
				int scanline=TIFFScanlineSize(tif);
				s=(uint)(STRIP_SIZE_DEFAULT/(scanline==0?1:scanline));
				if(s==0) s=1; // very wide images
			}
			return s;
		}

		// Return the number of bytes to read/write in a call to
		// one of the scanline-oriented i/o routines. Note that
		// this number may be 1/samples-per-pixel if data is
		// stored as separate planes.
		public static int TIFFScanlineSize(TIFF tif)
		{
			TIFFDirectory td=tif.tif_dir;
			uint scanline;

			if(td.td_planarconfig==PLANARCONFIG.CONTIG)
			{
				if(td.td_photometric==PHOTOMETRIC.YCBCR&&!isUpSampled(tif))
				{
					object[] ap=new object[2];
					TIFFGetField(tif, TIFFTAG.YCBCRSUBSAMPLING, ap);
					ushort ycbcrsubsampling0=__GetAsUshort(ap, 0);
					//ushort ycbcrsubsampling1=__GetAsUshort(ap, 1); // not needed

					if(ycbcrsubsampling0==0)
					{
						TIFFErrorExt(tif.tif_clientdata, tif.tif_name, "Invalid YCbCr subsampling");
						return 0;
					}

					scanline=TIFFroundup(td.td_imagewidth, ycbcrsubsampling0);
					scanline=TIFFhowmany8(multiply(tif, scanline, td.td_bitspersample, "TIFFScanlineSize"));
					return (int)summarize(tif, scanline, multiply(tif, 2, scanline/ycbcrsubsampling0, "TIFFVStripSize"), "TIFFVStripSize");
				}

				scanline=multiply(tif, td.td_imagewidth, td.td_samplesperpixel, "TIFFScanlineSize");
			}
			else scanline=td.td_imagewidth;

			return (int)TIFFhowmany8(multiply(tif, scanline, td.td_bitspersample, "TIFFScanlineSize"));
		}

		// Some stuff depends on this older version of TIFFScanlineSize
		// TODO: resolve this
		public static int TIFFOldScanlineSize(TIFF tif)
		{
			TIFFDirectory td=tif.tif_dir;
			uint scanline=multiply(tif, td.td_bitspersample, td.td_imagewidth, "TIFFScanlineSize");
			if(td.td_planarconfig==PLANARCONFIG.CONTIG) scanline=multiply(tif, scanline, td.td_samplesperpixel, "TIFFScanlineSize");
			return (int)TIFFhowmany8(scanline);
		}

		// Return the number of bytes to read/write in a call to
		// one of the scanline-oriented i/o routines. Note that
		// this number may be 1/samples-per-pixel if data is
		// stored as separate planes.
		// The ScanlineSize in case of YCbCrSubsampling is defined as the
		// strip size divided by the strip height, i.e. the size of a pack of vertical
		// subsampling lines divided by vertical subsampling. It should thus make
		// sense when multiplied by a multiple of vertical subsampling.
		// Some stuff depends on this newer version of TIFFScanlineSize
		// TODO: resolve this
		public static int TIFFNewScanlineSize(TIFF tif)
		{
			TIFFDirectory td=tif.tif_dir;
			uint scanline;

			if(td.td_planarconfig==PLANARCONFIG.CONTIG)
			{
				if(td.td_photometric==PHOTOMETRIC.YCBCR&&!isUpSampled(tif))
				{
					object[] ap=new object[2];

					TIFFGetField(tif, TIFFTAG.YCBCRSUBSAMPLING, ap);
					ushort[] ycbcrsubsampling=new ushort[2];
					ycbcrsubsampling[0]=__GetAsUshort(ap, 0);
					ycbcrsubsampling[1]=__GetAsUshort(ap, 1);

					if(ycbcrsubsampling[0]*ycbcrsubsampling[1]==0)
					{
						TIFFErrorExt(tif.tif_clientdata, tif.tif_name, "Invalid YCbCr subsampling");
						return 0;
					}

					return (int)(((((td.td_imagewidth+ycbcrsubsampling[0]-1)/ycbcrsubsampling[0])*(ycbcrsubsampling[0]*ycbcrsubsampling[1]+2)*td.td_bitspersample+7)/8)/ycbcrsubsampling[1]);
				}
				else scanline=multiply(tif, td.td_imagewidth, td.td_samplesperpixel, "TIFFScanlineSize");
			}
			else scanline=td.td_imagewidth;

			return (int)TIFFhowmany8(multiply(tif, scanline, td.td_bitspersample, "TIFFScanlineSize"));
		}

		// Return the number of bytes required to store a complete
		// decoded and packed raster scanline (as opposed to the
		// I/O size returned by TIFFScanlineSize which may be less
		// if data is store as separate planes).
		public static int TIFFRasterScanlineSize(TIFF tif)
		{
			TIFFDirectory td=tif.tif_dir;
			uint scanline=multiply(tif, td.td_bitspersample, td.td_imagewidth, "TIFFRasterScanlineSize");
			if(td.td_planarconfig==PLANARCONFIG.CONTIG)
			{
				scanline=multiply(tif, scanline, td.td_samplesperpixel, "TIFFRasterScanlineSize");
				return (int)TIFFhowmany8(scanline);
			}
			
			return (int)multiply(tif, TIFFhowmany8(scanline), td.td_samplesperpixel, "TIFFRasterScanlineSize");
		}
	}
}
