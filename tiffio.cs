// tiffio.cs
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

// TIFF I/O Library Definitions.

using System;
using System.Collections.Generic;
using System.IO;

namespace Free.Ports.LibTiff
{
	// Seek method constants
	public enum SEEK
	{
		SET=0,
		CUR=1,
		END=2
	}

	// Flags to pass to TIFFPrintDirectory to control
	// printing of data structures that are potentially
	// very large. Bit-or these flags to enable printing
	// multiple items.
	[Flags]
	public enum TIFFPRINT : uint
	{
		NONE=0x0,				// no extra info
		STRIPS=0x1,				// strips/tiles info
		CURVES=0x2,				// color/gray response curves
		COLORMAP=0x4,			// colormap
		JPEGQTABLES=0x100,		// JPEG Q matrices
		JPEGACTABLES=0x200,		// JPEG AC tables
		JPEGDCTABLES=0x200		// JPEG DC tables
	}

	public static partial class libtiff
	{
		// Colour conversion stuff
		// reference white
		public const float D65_X0=95.0470f;
		public const float D65_Y0=100.0f;
		public const float D65_Z0=108.8827f;

		public const float D50_X0=96.4250f;
		public const float D50_Y0=100.0f;
		public const float D50_Z0=82.4680f;
	}

	// Structure for holding information about a display device.
	public class TIFFDisplay
	{
		public float[,] d_mat=new float[3, 3];// XYZ => luminance matrix
		public float d_YCR;		// Light o/p for reference white
		public float d_YCG;
		public float d_YCB;
		public uint d_Vrwr;		// Pixel values for ref. white
		public uint d_Vrwg;
		public uint d_Vrwb;
		public float d_Y0R;		// Residual light for black pixel
		public float d_Y0G;
		public float d_Y0B;
		public float d_gammaR;	// Gamma values for the three guns
		public float d_gammaG;
		public float d_gammaB;

		// Color conversion constants. We will define display types here.
		public static TIFFDisplay display_sRGB
		{
			get
			{
				TIFFDisplay ret=new TIFFDisplay();

				// XYZ => luminance matrix
				ret.d_mat=new float[,] { { 3.2410f, -1.5374f, -0.4986f }, { -0.9692f, 1.8760f, 0.0416f }, { 0.0556f, -0.2040f, 1.0570f } };

				ret.d_YCR=ret.d_YCG=ret.d_YCB=100.0f;			// Light o/p for reference white
				ret.d_Vrwr=ret.d_Vrwg=ret.d_Vrwb=255;			// Pixel values for ref. white
				ret.d_Y0R=ret.d_Y0G=ret.d_Y0B=1.0f;				// Residual light o/p for black pixel
				ret.d_gammaR=ret.d_gammaG=ret.d_gammaB=2.4f;	// Gamma values for the three guns
				return ret;
			}
		}

		public void CopyTo(TIFFDisplay t)
		{
			t.d_mat[0, 0]=d_mat[0, 0];
			t.d_mat[0, 1]=d_mat[0, 1];
			t.d_mat[0, 2]=d_mat[0, 2];
			t.d_mat[1, 0]=d_mat[1, 0];
			t.d_mat[1, 1]=d_mat[1, 1];
			t.d_mat[1, 2]=d_mat[1, 2];
			t.d_mat[2, 0]=d_mat[2, 0];
			t.d_mat[2, 1]=d_mat[2, 1];
			t.d_mat[2, 2]=d_mat[2, 2];
			t.d_YCR=d_YCR;
			t.d_YCG=d_YCG;
			t.d_YCB=d_YCB;
			t.d_Vrwr=d_Vrwr;
			t.d_Vrwg=d_Vrwg;
			t.d_Vrwb=d_Vrwb;
			t.d_Y0R=d_Y0R;
			t.d_Y0G=d_Y0G;
			t.d_Y0B=d_Y0B;
			t.d_gammaR=d_gammaR;
			t.d_gammaG=d_gammaG;
			t.d_gammaB=d_gammaB;
		}
	}

	public class TIFFYCbCrToRGB // YCbCr=>RGB support
	{
		public int[] Cr_r_tab=new int[256];
		public int[] Cb_b_tab=new int[256];
		public int[] Cr_g_tab=new int[256];
		public int[] Cb_g_tab=new int[256];
		public int[] Y_tab=new int[256];
	}

	public class TIFFCIELabToRGB // CIE Lab 1976=>RGB support
	{
		public const int CIELABTORGB_TABLE_RANGE=1500;

		public int range;					// Size of conversion table
		public float rstep, gstep, bstep;
		public float X0, Y0, Z0;			// Reference white point
		public TIFFDisplay display=new TIFFDisplay();
		public float[] Yr2r=new float[CIELABTORGB_TABLE_RANGE+1];	// Conversion of Yr to r
		public float[] Yg2g=new float[CIELABTORGB_TABLE_RANGE+1];	// Conversion of Yg to g
		public float[] Yb2b=new float[CIELABTORGB_TABLE_RANGE+1];	// Conversion of Yb to b
	}

	// The image reading and conversion routines invoke
	// "put routines" to copy/image/whatever tiles of
	// raw image data. A default set of routines are
	// provided to convert/copy raw image data to 8-bit
	// packed ABGR format rasters. Applications can supply
	// alternate routines that unpack the data into a
	// different format or, for example, unpack the data
	// and draw the unpacked raster on the display.
	delegate void tileContigRoutine(TIFFRGBAImage img, uint[] cp, uint cp_offset, uint x, uint y, uint w, uint h, int fromskew, int toskew, byte[] pp, uint pp_offset);
	delegate void tileSeparateRoutine(TIFFRGBAImage img, uint[] cp, uint cp_offset, uint x, uint y, uint w, uint h, int fromskew, int toskew, byte[] r, byte[] g, byte[] b, byte[] a, uint rgba_offset);

	// RGBA-style image support.
	delegate bool getRoutine(TIFFRGBAImage img, uint[] raster, uint raster_offset, uint w, uint h);

	// RGBA-reader state.
	public class TIFFRGBAImage
	{
		public TIFF tif;					// image handle
		public bool stoponerr;				// stop on read error
		public bool isContig;				// data is packed/separate
		public EXTRASAMPLE alpha;			// type of alpha data present
		public uint width;					// image width
		public uint height;					// image height
		public ushort bitspersample;		// image bits/sample
		public ushort samplesperpixel;		// image samples/pixel
		public ORIENTATION orientation;		// image orientation
		public ORIENTATION req_orientation;	// requested orientation
		public PHOTOMETRIC photometric;		// image photometric interp
		public ushort[] redcmap;			// colormap pallete
		public ushort[] greencmap;
		public ushort[] bluecmap;

		// get image data routine
		internal getRoutine get;

		// put decoded strip/tile
		internal tileContigRoutine contig;
		internal tileSeparateRoutine separate;

		public byte[] Map;				// sample mapping array
		public uint[][] BWmap;			// black&white map
		public uint[][] PALmap;			// palette image map
		public TIFFYCbCrToRGB ycbcr;	// YCbCr conversion state
		public TIFFCIELabToRGB cielab;	// CIE L*a*b conversion state
		public int row_offset;
		public int col_offset;
	}

	public static partial class libtiff
	{
		// Macros for extracting components from the
		// packed ABGR form returned by TIFFReadRGBAImage.
		// TIFFGetR(abgr) ((abgr) & 0xff)
		public static byte TIFFGetR(uint abgr) { return (byte)((abgr)&0xff); }

		// TIFFGetG(abgr) (((abgr) >> 8) & 0xff)
		public static byte TIFFGetG(uint abgr) { return (byte)((abgr>>8)&0xff); }

		// TIFFGetB(abgr) (((abgr) >> 16) & 0xff)
		public static byte TIFFGetB(uint abgr) { return (byte)((abgr>>16)&0xff); }

		// TIFFGetA(abgr) (((abgr) >> 24) & 0xff)
		public static byte TIFFGetA(uint abgr) { return (byte)((abgr>>24)&0xff); }
	}

	// A CODEC is a software package that implements decoding,
	// encoding, or decoding+encoding of a compression algorithm.
	// The library provides a collection of builtin codecs.
	// More codecs may be registered through calls to the library
	// and/or the builtin implementations may be overridden.
	public delegate bool TIFFInitMethod(TIFF tif, COMPRESSION scheme);

	public class TIFFCodec
	{
		public string name;
		public COMPRESSION scheme;
		public TIFFInitMethod init;

		public TIFFCodec(string name, COMPRESSION scheme, TIFFInitMethod init)
		{
			this.name=name;
			this.scheme=scheme;
			this.init=init;
		}
	}

	public delegate void TIFFErrorHandler(string a, string b, params object[] p);
	public delegate void TIFFErrorHandlerExt(Stream fd, string a, string b, params object[] p);
	public delegate int TIFFReadWriteProc(Stream fd, byte[] buf, int size);
	public delegate uint TIFFSeekProc(Stream fd, long off, SEEK whence);
	public delegate int TIFFCloseProc(Stream fd);
	public delegate uint TIFFSizeProc(Stream fd);
	public delegate void TIFFExtendProc(TIFF tif);

	public class TIFFFieldInfo
	{
		public TIFFTAG field_tag;		// field's tag
		public short field_readcount;	// read count/TIFF_VARIABLE/TIFF_SPP
		public short field_writecount;	// write count/TIFF_VARIABLE
		public TIFFDataType field_type;	// type of associated data
		public FIELD field_bit;			// bit in fieldsset bit vector
		public bool field_oktochange;	// if true, can change while writing
		public bool field_passcount;	// if true, pass dir count on set
		public string field_name;		// ASCII name

		public TIFFFieldInfo(TIFFTAG field_tag, short field_readcount, short field_writecount, TIFFDataType field_type,
			FIELD field_bit, bool field_oktochange, bool field_passcount, string field_name)
		{
			this.field_tag=field_tag;
			this.field_readcount=field_readcount;
			this.field_writecount=field_writecount;
			this.field_type=field_type;
			this.field_bit=field_bit;
			this.field_oktochange=field_oktochange;
			this.field_passcount=field_passcount;
			this.field_name=field_name;
		}
	}

	public class TIFFTagValue
	{
		public TIFFFieldInfo info;
		public int count;
		public object value;
	}

	public delegate bool TIFFVSetMethod(TIFF tif, TIFFTAG tag, TIFFDataType dt, params object[] p);
	public delegate bool TIFFVGetMethod(TIFF tif, TIFFTAG tag, object[] p);
	public delegate void TIFFPrintMethod(TIFF tif, TextWriter fd, TIFFPRINT flags);

	public class TIFFTagMethods
	{
		public TIFFVSetMethod vsetfield;	// tag set routine
		public TIFFVGetMethod vgetfield;	// tag get routine
		public TIFFPrintMethod printdir;	// directory print routine
	}

	public static partial class libtiff
	{
		public const double U_NEU=0.210526316;
		public const double V_NEU=0.473684211;
		public const double UVSCALE=410.0;

		public const short TIFF_VARIABLE=-1;	// marker for variable length tags
		public const short TIFF_SPP=-2;			// marker for SamplesPerPixel tags
	}
}
