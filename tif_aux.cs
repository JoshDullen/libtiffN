// tif_aux.cs
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
// Auxiliary Support Routines.

using System;
using System.Collections.Generic;
using System.Text;

namespace Free.Ports.LibTiff
{
	public static partial class libtiff
	{
		static bool TIFFDefaultTransferFunction(TIFFDirectory td)
		{
			ushort[][] tf=td.td_transferfunction;

			tf[0]=tf[1]=tf[2]=null;
			if(td.td_bitspersample>=4*8-2) return false; //4: sizeof(tsize_t)

			int n=1<<td.td_bitspersample;
			try
			{
				tf[0]=new ushort[n];

				tf[0][0]=0;
				for(int i=1; i<n; i++)
				{
					double t=(double)i/((double)n-1.0);
					tf[0][i]=(ushort)Math.Floor(65535*Math.Pow(t, 2.2)+0.5);
				}

				if(td.td_samplesperpixel-td.td_extrasamples>1)
				{
					tf[1]=new ushort[n];
					tf[2]=new ushort[n];
					tf[0].CopyTo(tf[1], 0);
					tf[0].CopyTo(tf[2], 0);
				}

				return true;
			}
			catch
			{
				tf[0]=tf[1]=tf[2]=null;
				return false;
			}
		}

		static bool TIFFDefaultRefBlackWhite(TIFFDirectory td)
		{
			if(td.td_photometric==PHOTOMETRIC.YCBCR)
			{
				// YCbCr (Class Y) images must have the
				// ReferenceBlackWhite tag set. Fix the
				// broken images, which lacks that tag.
				try
				{
					td.td_refblackwhite=new double[] { 0.0, 255.0, 128.0, 255.0, 128.0, 255.0 };
				}
				catch
				{
					return false;
				}
			}
			else
			{
				try
				{
					td.td_refblackwhite=new double[6];
				}
				catch
				{
					return false;
				}

				// Assume RGB (Class R)
				for(int i=0; i<3; i++)
				{
					td.td_refblackwhite[2*i+0]=0.0;
					td.td_refblackwhite[2*i+1]=(double)((1<<td.td_bitspersample)-1);
				}
			}

			return true;
		}

		// Like TIFFGetField, but return any default
		// value if the tag is not present in the directory.
		//
		// NB:	We use the value in the directory, rather than
		//		explicit values so that defaults exist only in one
		//		place in the library -- in TIFFDefaultDirectory.
		public static bool TIFFVGetFieldDefaulted(TIFF tif, TIFFTAG tag, object[] ap)
		{
			TIFFDirectory td=tif.tif_dir;

			if(TIFFVGetField(tif, tag, ap)) return true;

			switch(tag)
			{
				case TIFFTAG.SUBFILETYPE:
					ap[0]=td.td_subfiletype;
					return true;
				case TIFFTAG.BITSPERSAMPLE:
					ap[0]=td.td_bitspersample;
					return true;
				case TIFFTAG.THRESHHOLDING:
					ap[0]=td.td_threshholding;
					return true;
				case TIFFTAG.FILLORDER:
					ap[0]=td.td_fillorder;
					return true;
				case TIFFTAG.ORIENTATION:
					ap[0]=td.td_orientation;
					return true;
				case TIFFTAG.SAMPLESPERPIXEL:
					ap[0]=td.td_samplesperpixel;
					return true;
				case TIFFTAG.ROWSPERSTRIP:
					ap[0]=td.td_rowsperstrip;
					return true;
				case TIFFTAG.MINSAMPLEVALUE:
					ap[0]=td.td_minsamplevalue;
					return true;
				case TIFFTAG.MAXSAMPLEVALUE:
					ap[0]=td.td_maxsamplevalue;
					return true;
				case TIFFTAG.PLANARCONFIG:
					ap[0]=td.td_planarconfig;
					return true;
				case TIFFTAG.RESOLUTIONUNIT:
					ap[0]=td.td_resolutionunit;
					return true;
				case TIFFTAG.PREDICTOR:
					TIFFPredictorState sp=(TIFFPredictorState)tif.tif_data;
					ap[0]=sp.predictor;
					return true;
				case TIFFTAG.DOTRANGE:
					ap[0]=0;
					ap[1]=(1<<td.td_bitspersample)-1;
					return true;
				case TIFFTAG.INKSET:
					ap[0]=INKSET.CMYK;
					return true;
				case TIFFTAG.NUMBEROFINKS:
					ap[0]=(ushort)4;
					return true;
				case TIFFTAG.EXTRASAMPLES:
					ap[0]=td.td_extrasamples;
					ap[1]=td.td_sampleinfo;
					return true;
				case TIFFTAG.MATTEING:
					ap[0]=(ushort)((td.td_extrasamples==1&&td.td_sampleinfo[0]==(ushort)EXTRASAMPLE.ASSOCALPHA)?1:0);
					return true;
				case TIFFTAG.TILEDEPTH:
					ap[0]=td.td_tiledepth;
					return true;
				case TIFFTAG.DATATYPE:
					ap[0]=td.td_sampleformat-1;
					return true;
				case TIFFTAG.SAMPLEFORMAT:
					ap[0]=td.td_sampleformat;
					return true;
				case TIFFTAG.IMAGEDEPTH:
					ap[0]=td.td_imagedepth;
					return true;
				case TIFFTAG.YCBCRCOEFFICIENTS:
					// defaults are from CCIR Recommendation 601-1
					double[] ycbcrcoeffs=new double[] { 0.299, 0.587, 0.114 };
					ap[0]=ycbcrcoeffs;
					return true;
				case TIFFTAG.YCBCRSUBSAMPLING:
					ap[0]=td.td_ycbcrsubsampling[0];
					ap[1]=td.td_ycbcrsubsampling[1];
					return true;
				case TIFFTAG.YCBCRPOSITIONING:
					ap[0]=td.td_ycbcrpositioning;
					return true;
				case TIFFTAG.WHITEPOINT:
					// TIFF 6.0 specification tells that it is no default
					// value for the WhitePoint, but AdobePhotoshop TIFF
					// Technical Note tells that it should be CIE D50.
					ap[0]=new double[] { D50_X0/(D50_X0+D50_Y0+D50_Z0), D50_Y0/(D50_X0+D50_Y0+D50_Z0) };
					return true;
				case TIFFTAG.TRANSFERFUNCTION:
					if(td.td_transferfunction[0]==null&&!TIFFDefaultTransferFunction(td))
					{
						TIFFErrorExt(tif.tif_clientdata, tif.tif_name, "No space for \"TransferFunction\" tag");
						return false;
					}
					ap[0]=td.td_transferfunction[0];
					if(td.td_samplesperpixel-td.td_extrasamples>1)
					{
						ap[1]=td.td_transferfunction[1];
						ap[2]=td.td_transferfunction[2];
					}
					return true;
				case TIFFTAG.REFERENCEBLACKWHITE:
					{
						if(td.td_refblackwhite==null&&!TIFFDefaultRefBlackWhite(td)) return false;

						ap[0]=td.td_refblackwhite;
						return true;
					}
			}
			return false;
		}

		// Like TIFFGetField, but return any default
		// value if the tag is not present in the directory.
		public static bool TIFFGetFieldDefaulted(TIFF tif, TIFFTAG tag, object[] ap)
		{
			return TIFFVGetFieldDefaulted(tif, tag, ap);
		}
	}
}
