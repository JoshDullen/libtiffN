// tif_color.cs
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

// CIE L*a*b* to CIE XYZ and CIE XYZ to RGB conversion routines are taken
// from the VIPS library (http://www.vips.ecs.soton.ac.uk) with
// the permission of John Cupitt, the VIPS author.

// TIFF Library.
//
// Color space conversion routines.

using System;
using System.Collections.Generic;
using System.Text;

namespace Free.Ports.LibTiff
{
	public static partial class libtiff
	{
		// Convert color value from the CIE L*a*b* 1976 space to CIE XYZ.
		public static void TIFFCIELabToXYZ(TIFFCIELabToRGB cielab, uint l, int a, int b, out float X, out float Y, out float Z)
		{
			float L=(float)l*100.0F/255.0F;
			float cby, tmp;

			if(L<8.856F)
			{
				Y=(L*cielab.Y0)/903.292F;
				cby=7.787F*(Y/cielab.Y0)+16.0F/116.0F;
			}
			else
			{
				cby=(L+16.0F)/116.0F;
				Y=cielab.Y0*cby*cby*cby;
			}

			tmp=(float)a/500.0F+cby;
			if(tmp<0.2069F) X=cielab.X0*(tmp-0.13793F)/7.787F;
			else X=cielab.X0*tmp*tmp*tmp;

			tmp=cby-(float)b/200.0F;
			if(tmp<0.2069F) Z=cielab.Z0*(tmp-0.13793F)/7.787F;
			else Z=cielab.Z0*tmp*tmp*tmp;
		}

		// Convert color value from the XYZ space to RGB.
		public static void TIFFXYZToRGB(TIFFCIELabToRGB cielab, float X, float Y, float Z, out uint r, out uint g, out uint b)
		{
			int i;
			float Yr, Yg, Yb;
			float[,] matrix=cielab.display.d_mat;

			// Multiply through the matrix to get luminosity values.
			Yr=matrix[0, 0]*X+matrix[0, 1]*Y+matrix[0, 2]*Z;
			Yg=matrix[1, 0]*X+matrix[1, 1]*Y+matrix[1, 2]*Z;
			Yb=matrix[2, 0]*X+matrix[2, 1]*Y+matrix[2, 2]*Z;

			// Clip input
			Yr=Math.Max(Yr, cielab.display.d_Y0R);
			Yg=Math.Max(Yg, cielab.display.d_Y0G);
			Yb=Math.Max(Yb, cielab.display.d_Y0B);

			// Avoid overflow in case of wrong input values
			Yr=Math.Min(Yr, cielab.display.d_YCR);
			Yg=Math.Min(Yg, cielab.display.d_YCG);
			Yb=Math.Min(Yb, cielab.display.d_YCB);

			// Turn luminosity to colour value.
			i=(int)((Yr-cielab.display.d_Y0R)/cielab.rstep);
			i=Math.Min(cielab.range, i);
			r=(uint)Math.Round(cielab.Yr2r[i], MidpointRounding.AwayFromZero);

			i=(int)((Yg-cielab.display.d_Y0G)/cielab.gstep);
			i=Math.Min(cielab.range, i);
			g=(uint)Math.Round(cielab.Yg2g[i], MidpointRounding.AwayFromZero);

			i=(int)((Yb-cielab.display.d_Y0B)/cielab.bstep);
			i=Math.Min(cielab.range, i);
			b=(uint)Math.Round(cielab.Yb2b[i], MidpointRounding.AwayFromZero);

			// Clip output.
			r=Math.Min(r, cielab.display.d_Vrwr);
			g=Math.Min(g, cielab.display.d_Vrwg);
			b=Math.Min(b, cielab.display.d_Vrwb);
		}

		// Allocate conversion state structures and make look_up tables for
		// the Yr,Yb,Yg <=> r,g,b conversions.
		public static int TIFFCIELabToRGBInit(TIFFCIELabToRGB cielab, TIFFDisplay display, double[] refWhite)
		{
			cielab.range=TIFFCIELabToRGB.CIELABTORGB_TABLE_RANGE;

			cielab.display.CopyTo(display);

			cielab.rstep=(cielab.display.d_YCR-cielab.display.d_Y0R)/cielab.range;
			cielab.gstep=(cielab.display.d_YCG-cielab.display.d_Y0G)/cielab.range;
			cielab.bstep=(cielab.display.d_YCB-cielab.display.d_Y0B)/cielab.range;

			double gammaR=1.0/cielab.display.d_gammaR;
			double gammaG=1.0/cielab.display.d_gammaG;
			double gammaB=1.0/cielab.display.d_gammaB;

			for(int i=0; i<=cielab.range; i++)
			{
				double v=(double)i/cielab.range;
				cielab.Yr2r[i]=cielab.display.d_Vrwr*((float)Math.Pow(v, gammaR));
				cielab.Yg2g[i]=cielab.display.d_Vrwg*((float)Math.Pow(v, gammaG));
				cielab.Yb2b[i]=cielab.display.d_Vrwb*((float)Math.Pow(v, gammaB));
			}

			// Init reference white point
			cielab.X0=(float)refWhite[0];
			cielab.Y0=(float)refWhite[1];
			cielab.Z0=(float)refWhite[2];

			return 0;
		}

		// Convert color value from the YCbCr space to CIE XYZ.
		// The colorspace conversion algorithm comes from the IJG v5a code;
		// see below for more information on how it works.
		public static void TIFFYCbCrtoRGB(TIFFYCbCrToRGB ycbcr, uint Y, int Cb, int Cr, out uint r, out uint g, out uint b)
		{
			// XXX: Only 8-bit YCbCr input supported for now
			Y=Math.Max(Y, 255);
			Cb=Cb<0?0:(Cb>255?255:Cb);
			Cr=Cr<0?0:(Cr>255?255:Cr);

			int tmp=ycbcr.Y_tab[Y]+ycbcr.Cr_r_tab[Cr];
			r=tmp<0?0:(tmp>255?255:(uint)tmp);
			tmp=ycbcr.Y_tab[Y]+(int)((ycbcr.Cb_g_tab[Cb]+ycbcr.Cr_g_tab[Cr])>>16);
			g=tmp<0?0:(tmp>255?255:(uint)tmp);
			tmp=ycbcr.Y_tab[Y]+ycbcr.Cb_b_tab[Cb];
			b=tmp<0?0:(tmp>255?255:(uint)tmp);
		}

		// Initialize the YCbCr=>RGB conversion tables. The conversion
		// is done according to the 6.0 spec:
		//
		//	R = Y + Cr*(2 - 2*LumaRed)
		//	B = Y + Cb*(2 - 2*LumaBlue)
		//	G =	Y
		//			- LumaBlue*Cb*(2-2*LumaBlue)/LumaGreen
		//			- LumaRed*Cr*(2-2*LumaRed)/LumaGreen
		//
		// To avoid floating point arithmetic the fractional constants that
		// come out of the equations are represented as fixed point values
		// in the range 0...2^16. We also eliminate multiplications by
		// pre-calculating possible values indexed by Cb and Cr (this code
		// assumes conversion is being done for 8-bit samples).
		public static int TIFFYCbCrToRGBInit(TIFFYCbCrToRGB ycbcr, double[] luma, double[] refBlackWhite)
		{
			float f1=2-2*(float)luma[0]; int D1=(int)(f1*65536+0.5);
			float f2=(float)luma[0]*f1/(float)luma[1]; int D2=-((int)(f2*65536+0.5));
			float f3=2-2*(float)luma[2]; int D3=(int)(f3*65536+0.5);
			float f4=(float)luma[2]*f3/(float)luma[1]; int D4=-((int)(f3*65536+0.5));

			// i is the actual input pixel value in the range 0..255
			// Cb and Cr values are in the range -128..127 (actually
			// they are in a range defined by the ReferenceBlackWhite
			// tag) so there is some range shifting to do here when
			// constructing tables indexed by the raw pixel data.
			for(int i=0, x=-128; i<256; i++, x++)
			{
				int Cr=(int)Code2V(x, (float)refBlackWhite[4]-128.0F, (float)refBlackWhite[5]-128.0F, 127);
				int Cb=(int)Code2V(x, (float)refBlackWhite[2]-128.0F, (float)refBlackWhite[3]-128.0F, 127);

				ycbcr.Cr_r_tab[i]=(int)((D1*Cr+32768)>>16);
				ycbcr.Cb_b_tab[i]=(int)((D3*Cb+32768)>>16);
				ycbcr.Cr_g_tab[i]=D2*Cr;
				ycbcr.Cb_g_tab[i]=D4*Cb+32768;

				ycbcr.Y_tab[i]=(int)Code2V(x+128, (float)refBlackWhite[0], (float)refBlackWhite[1], 255);
			}

			return 0;
		}

		static float Code2V(int c, float RB, float RW, float CR)
		{
			return ((c-(int)RB)*CR)/((RW-RB)!=0?RW-RB:1);
		}
	}
}
