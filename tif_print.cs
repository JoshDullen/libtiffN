// tif_print.cs
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
// Directory Printing Support

using System;
using System.Collections.Generic;
using System.IO;

namespace Free.Ports.LibTiff
{
	public static partial class libtiff
	{
		static readonly string[] photoNames=new string[]
		{
			"min-is-white",							// PHOTOMETRIC.MINISWHITE
			"min-is-black",							// PHOTOMETRIC.MINISBLACK
			"RGB color",							// PHOTOMETRIC.RGB
			"palette color (RGB from colormap)",	// PHOTOMETRIC.PALETTE
			"transparency mask",					// PHOTOMETRIC.MASK
			"separated",							// PHOTOMETRIC.SEPARATED
			"YCbCr",								// PHOTOMETRIC.YCBCR
			"7 (0x7)",
			"CIE L*a*b*",							// PHOTOMETRIC.CIELAB
		};

		static readonly string[] orientNames=new string[]
		{
			"0 (0x0)",
			"row 0 top, col 0 lhs",		// ORIENTATION.TOPLEFT
			"row 0 top, col 0 rhs",		// ORIENTATION.TOPRIGHT
			"row 0 bottom, col 0 rhs",	// ORIENTATION.BOTRIGHT
			"row 0 bottom, col 0 lhs",	// ORIENTATION.BOTLEFT
			"row 0 lhs, col 0 top",		// ORIENTATION.LEFTTOP
			"row 0 rhs, col 0 top",		// ORIENTATION.RIGHTTOP
			"row 0 rhs, col 0 bottom",	// ORIENTATION.RIGHTBOT
			"row 0 lhs, col 0 bottom",	// ORIENTATION.LEFTBOT
		};

		static void TIFFPrintField(TextWriter fd, TIFFFieldInfo fip, uint value_count, object raw_data)
		{
			fd.Write(" {0}: ", fip.field_name);

			for(uint j=0; j<value_count; j++)
			{
				if(fip.field_type==TIFFDataType.TIFF_BYTE) fd.Write("{0}", ((byte[])raw_data)[j]);
				else if(fip.field_type==TIFFDataType.TIFF_UNDEFINED) fd.Write("0x{0:x}", (uint)((byte[])raw_data)[j]);
				else if(fip.field_type==TIFFDataType.TIFF_SBYTE) fd.Write("{0}", ((sbyte[])raw_data)[j]);
				else if(fip.field_type==TIFFDataType.TIFF_SHORT) fd.Write("{0}", ((ushort[])raw_data)[j]);
				else if(fip.field_type==TIFFDataType.TIFF_SSHORT) fd.Write("{0}", ((short[])raw_data)[j]);
				else if(fip.field_type==TIFFDataType.TIFF_LONG) fd.Write("{0}", ((uint[])raw_data)[j]);
				else if(fip.field_type==TIFFDataType.TIFF_SLONG) fd.Write("{0}", (long)((int[])raw_data)[j]);
				else if(fip.field_type==TIFFDataType.TIFF_FLOAT) fd.Write("{0}", ((float[])raw_data)[j]);
				else if(fip.field_type==TIFFDataType.TIFF_IFD) fd.Write("0x{0:x}", ((uint[])raw_data)[j]);
				else if(fip.field_type==TIFFDataType.TIFF_ASCII)
				{
					fd.Write("{0}", (string)raw_data);
					break;
				}
				else if(fip.field_type==TIFFDataType.TIFF_RATIONAL||fip.field_type==TIFFDataType.TIFF_SRATIONAL||fip.field_type==TIFFDataType.TIFF_DOUBLE)
					fd.Write("{0}", ((double[])raw_data)[j]);
				else
				{
					fd.Write("<unsupported data type in TIFFPrint>");
					break;
				}

				if(j<value_count-1) fd.Write(",");
			}

			fd.WriteLine();
		}

		static bool TIFFPrettyPrintField(TIFF tif, TextWriter fd, TIFFTAG tag, uint value_count, object raw_data)
		{
			TIFFDirectory td=tif.tif_dir;

			switch(tag)
			{
				case TIFFTAG.INKSET:
					fd.Write(" Ink Set: ");
					switch((INKSET)((ushort[])raw_data)[0])
					{
						case INKSET.CMYK:
							fd.WriteLine("CMYK");
							break;
						default:
							fd.WriteLine("{0} (0x{0:X})", ((ushort[])raw_data)[0]);
							break;
					}
					return true;
				case TIFFTAG.DOTRANGE:
					fd.WriteLine(" Dot Range: {0}-{1}", ((ushort[])raw_data)[0], ((ushort[])raw_data)[1]);
					return true;
				case TIFFTAG.WHITEPOINT:
					fd.WriteLine(" White Point: {0}-{1}", ((double[])raw_data)[0], ((double[])raw_data)[1]);
					return true;
				case TIFFTAG.REFERENCEBLACKWHITE:
					fd.WriteLine(" Reference Black/White:");
					for(ushort i=0; i<3; i++) fd.WriteLine("\t{0}: {1} {2}", i, ((double[])raw_data)[2*i+0], ((double[])raw_data)[2*i+1]);
					return true;
				case TIFFTAG.XMLPACKET:
					fd.WriteLine(" XMLPacket (XMP Metadata):");
					for(uint i=0; i<value_count; i++) fd.Write((char)((byte[])raw_data)[i]);
					fd.WriteLine();
					return true;
				case TIFFTAG.RICHTIFFIPTC:
					// XXX: for some weird reason RichTIFFIPTC tag
					// defined as array of LONG values.
					fd.WriteLine(" RichTIFFIPTC Data: <present>, {0} bytes", value_count*4);
					return true;
				case TIFFTAG.PHOTOSHOP:
					fd.WriteLine(" Photoshop Data: <present>, {0} bytes", value_count);
					return true;
				case TIFFTAG.ICCPROFILE:
					fd.WriteLine(" ICC Profile: <present>, {0} bytes", value_count);
					return true;
				case TIFFTAG.STONITS:
					fd.WriteLine(" Sample to Nits conversion factor: {0}", ((double[])raw_data)[0]);
					return true;
			}

			return false;
		}

		// Print the contents of the current directory
		// to the specified stdio file stream.
		public static void TIFFPrintDirectory(TIFF tif, TextWriter fd, TIFFPRINT flags)
		{
			TIFFDirectory td=tif.tif_dir;

			fd.WriteLine("TIFF Directory at offset 0x{0:X} ({0})", tif.tif_diroff);
			if(TIFFFieldSet(tif, FIELD.SUBFILETYPE))
			{
				fd.Write(" Subfile Type:");
				string sep=" ";
				if((td.td_subfiletype&FILETYPE.REDUCEDIMAGE)!=0)
				{
					fd.Write("{0}reduced-resolution image", sep);
					sep="/";
				}
				if((td.td_subfiletype&FILETYPE.PAGE)!=0)
				{
					fd.Write("{0}multi-page document", sep);
					sep="/";
				}
				if((td.td_subfiletype&FILETYPE.MASK)!=0) fd.Write("{0}transparency mask", sep);
				fd.WriteLine(" ({0} = 0x{0:X})", td.td_subfiletype);
			}
			if(TIFFFieldSet(tif, FIELD.IMAGEDIMENSIONS))
			{
				fd.Write(" Image Width: {0} Image Length: {1}", td.td_imagewidth, td.td_imagelength);
				if(TIFFFieldSet(tif, FIELD.IMAGEDEPTH)) fd.Write(" Image Depth: {0}", td.td_imagedepth);
				fd.WriteLine();
			}
			if(TIFFFieldSet(tif, FIELD.TILEDIMENSIONS))
			{
				fd.Write(" Tile Width: {0} Tile Length: {1}", td.td_tilewidth, td.td_tilelength);
				if(TIFFFieldSet(tif, FIELD.TILEDEPTH)) fd.Write(" Tile Depth: {0}", td.td_tiledepth);
				fd.WriteLine();
			}
			if(TIFFFieldSet(tif, FIELD.RESOLUTION))
			{
				fd.Write(" Resolution: {0}, {1}", td.td_xresolution, td.td_yresolution);
				if(TIFFFieldSet(tif, FIELD.RESOLUTIONUNIT))
				{
					switch(td.td_resolutionunit)
					{
						case RESUNIT.NONE: fd.Write(" (unitless)"); break;
						case RESUNIT.INCH: fd.Write(" pixels/inch"); break;
						case RESUNIT.CENTIMETER: fd.Write(" pixels/cm"); break;
						default: fd.Write(" (unit {0} = 0x{0:X})", td.td_resolutionunit); break;
					}
				}
				fd.WriteLine();
			}
			if(TIFFFieldSet(tif, FIELD.POSITION)) fd.WriteLine(" Position: {0}, {1}", td.td_xposition, td.td_yposition);
			if(TIFFFieldSet(tif, FIELD.BITSPERSAMPLE)) fd.WriteLine(" Bits/Sample: {0}", td.td_bitspersample);
			if(TIFFFieldSet(tif, FIELD.SAMPLEFORMAT))
			{
				fd.Write(" Sample Format: ");
				switch(td.td_sampleformat)
				{
					case SAMPLEFORMAT.VOID: fd.WriteLine("void"); break;
					case SAMPLEFORMAT.INT: fd.WriteLine("signed integer"); break;
					case SAMPLEFORMAT.UINT: fd.WriteLine("unsigned integer"); break;
					case SAMPLEFORMAT.IEEEFP: fd.WriteLine("IEEE floating point"); break;
					case SAMPLEFORMAT.COMPLEXINT: fd.WriteLine("complex signed integer"); break;
					case SAMPLEFORMAT.COMPLEXIEEEFP: fd.WriteLine("complex IEEE floating point"); break;
					default: fd.WriteLine("{0} (0x{0:X})", td.td_sampleformat); break;
				}
			}
			if(TIFFFieldSet(tif, FIELD.COMPRESSION))
			{
				TIFFCodec c=TIFFFindCODEC(td.td_compression);
				fd.Write(" Compression Scheme: ");
				if(c!=null) fd.WriteLine(c.name);
				else fd.WriteLine("{0} (0x{0:X})", td.td_compression);
			}
			if(TIFFFieldSet(tif, FIELD.PHOTOMETRIC))
			{
				fd.Write(" Photometric Interpretation: ");
				if((int)td.td_photometric<photoNames.Length) fd.WriteLine(photoNames[(int)td.td_photometric]);
				else
				{
					switch(td.td_photometric)
					{
						case PHOTOMETRIC.LOGL: fd.WriteLine("CIE Log2(L)"); break;
						case PHOTOMETRIC.LOGLUV: fd.WriteLine("CIE Log2(L) (u',v')"); break;
						default: fd.WriteLine("{0} (0x{0:X})", td.td_photometric); break;
					}
				}
			}
			if(TIFFFieldSet(tif, FIELD.EXTRASAMPLES)&&td.td_extrasamples!=0)
			{
				fd.Write(" Extra Samples: {0}<", td.td_extrasamples);
				string sep="";
				for(int i=0; i<td.td_extrasamples; i++)
				{
					switch((EXTRASAMPLE)td.td_sampleinfo[i])
					{
						case EXTRASAMPLE.UNSPECIFIED: fd.Write("{0}unspecified", sep); break;
						case EXTRASAMPLE.ASSOCALPHA: fd.Write("{0}assoc-alpha", sep); break;
						case EXTRASAMPLE.UNASSALPHA: fd.Write("{0}unassoc-alpha", sep); break;
						default: fd.Write("{0}{1} (0x{1:X})", sep, td.td_sampleinfo[i]); break;
					}
					sep=", ";
				}
				fd.WriteLine(">");
			}
			if(TIFFFieldSet(tif, FIELD.INKNAMES))
			{
				string[] names=td.td_inknames.Split('\0');
				fd.Write(" Ink Names: ");
				string sep="";
				for(int i=0; i<td.td_samplesperpixel&&i<names.Length; i++)
				{
					fd.Write(sep);
					TIFFprintAscii(fd, names[i]);
					sep=", ";
				}
				fd.WriteLine();
			}
			if(TIFFFieldSet(tif, FIELD.THRESHHOLDING))
			{
				fd.Write(" Thresholding: ");
				switch(td.td_threshholding)
				{
					case THRESHHOLD.BILEVEL: fd.WriteLine("bilevel art scan"); break;
					case THRESHHOLD.HALFTONE: fd.WriteLine("halftone or dithered scan"); break;
					case THRESHHOLD.ERRORDIFFUSE: fd.WriteLine("error diffused"); break;
					default: fd.WriteLine("{0} (0x{0:X})", td.td_threshholding); break;
				}
			}
			if(TIFFFieldSet(tif, FIELD.FILLORDER))
			{
				fd.Write(" FillOrder: ");
				switch(td.td_fillorder)
				{
					case FILLORDER.MSB2LSB: fd.WriteLine("msb-to-lsb"); break;
					case FILLORDER.LSB2MSB: fd.WriteLine("lsb-to-msb"); break;
					default: fd.WriteLine("{0} (0x{0:X})", td.td_fillorder); break;
				}
			}
			if(TIFFFieldSet(tif, FIELD.YCBCRSUBSAMPLING))
			{
				// For hacky reasons (see tif_jpeg.cs - JPEGFixupTestSubsampling),
				// we need to fetch this rather than trust what is in our structures.
				object[] ap=new object[2];
				TIFFGetField(tif, TIFFTAG.YCBCRSUBSAMPLING, ap);
				fd.WriteLine(" YCbCr Subsampling: {0}, {1}", __GetAsUshort(ap, 0), __GetAsUshort(ap, 1));
			}
			if(TIFFFieldSet(tif, FIELD.YCBCRPOSITIONING))
			{
				fd.Write(" YCbCr Positioning: ");
				switch(td.td_ycbcrpositioning)
				{
					case YCBCRPOSITION.CENTERED: fd.WriteLine("centered"); break;
					case YCBCRPOSITION.COSITED: fd.WriteLine("cosited"); break;
					default: fd.WriteLine("{0} (0x{0:X})", td.td_ycbcrpositioning); break;
				}
			}
			if(TIFFFieldSet(tif, FIELD.HALFTONEHINTS)) fd.WriteLine(" Halftone Hints: light {0} dark {1}", td.td_halftonehints[0], td.td_halftonehints[1]);
			if(TIFFFieldSet(tif, FIELD.ORIENTATION))
			{
				fd.Write(" Orientation: ");
				if((int)td.td_orientation<orientNames.Length) fd.WriteLine(orientNames[(int)td.td_orientation]);
				else fd.WriteLine("{0} (0x{0:X})", td.td_orientation);
			}
			if(TIFFFieldSet(tif, FIELD.SAMPLESPERPIXEL)) fd.WriteLine(" Samples/Pixel: {0}", td.td_samplesperpixel);
			if(TIFFFieldSet(tif, FIELD.ROWSPERSTRIP))
			{
				fd.Write(" Rows/Strip: ");
				if(td.td_rowsperstrip==uint.MaxValue) fd.WriteLine("(infinite)");
				else fd.WriteLine(td.td_rowsperstrip);
			}
			if(TIFFFieldSet(tif, FIELD.MINSAMPLEVALUE)) fd.WriteLine(" Min Sample Value: {0}", td.td_minsamplevalue);
			if(TIFFFieldSet(tif, FIELD.MAXSAMPLEVALUE)) fd.WriteLine(" Max Sample Value: {0}", td.td_maxsamplevalue);
			if(TIFFFieldSet(tif, FIELD.SMINSAMPLEVALUE)) fd.WriteLine(" SMin Sample Value: {0}", td.td_sminsamplevalue);
			if(TIFFFieldSet(tif, FIELD.SMAXSAMPLEVALUE)) fd.WriteLine(" SMax Sample Value: {0}", td.td_smaxsamplevalue);
			if(TIFFFieldSet(tif, FIELD.PLANARCONFIG))
			{
				fd.Write(" Planar Configuration: ");
				switch(td.td_planarconfig)
				{
					case PLANARCONFIG.CONTIG: fd.WriteLine("single image plane"); break;
					case PLANARCONFIG.SEPARATE: fd.WriteLine("separate image planes"); break;
					default: fd.WriteLine("{0} (0x{0:X})", td.td_planarconfig); break;
				}
			}
			if(TIFFFieldSet(tif, FIELD.PAGENUMBER)) fd.WriteLine(" Page Number: {0}-{1}", td.td_pagenumber[0], td.td_pagenumber[1]);
			if(TIFFFieldSet(tif, FIELD.COLORMAP))
			{
				fd.Write(" Color Map: ");
				if((flags&TIFFPRINT.COLORMAP)!=0)
				{
					fd.WriteLine();
					uint n=1u<<td.td_bitspersample;
					for(uint l=0; l<n; l++) fd.WriteLine("\t{0}: {1} {2} {3}", l, td.td_colormap[0][l], td.td_colormap[1][l], td.td_colormap[2][l]);
				}
				else fd.WriteLine("(present)");
			}
			if(TIFFFieldSet(tif, FIELD.TRANSFERFUNCTION))
			{
				fd.Write(" Transfer Function: ");
				if((flags&TIFFPRINT.CURVES)!=0)
				{
					fd.WriteLine();
					uint n=1u<<td.td_bitspersample;
					for(uint l=0; l<n; l++)
					{
						fd.Write("\t{0}: {1}", l, td.td_transferfunction[0][l]);
						for(int i=1; i<td.td_samplesperpixel; i++) fd.Write(" {0}", td.td_transferfunction[i][l]);
						fd.WriteLine();
					}
				}
				else fd.WriteLine("(present)");
			}
			if(TIFFFieldSet(tif, FIELD.SUBIFD)&&td.td_subifd!=null&&td.td_subifd.Length!=0)
			{
				fd.Write(" SubIFD Offsets:");
				for(int i=0; i<td.td_nsubifd; i++) fd.Write(" {0}", td.td_subifd[i]);
				fd.WriteLine();
			}

			// Custom tag support.
			short count=(short)TIFFGetTagListCount(tif);
			for(int i=0; i<count; i++)
			{
				TIFFTAG tag=TIFFGetTagListEntry(tif, i);

				TIFFFieldInfo fip=TIFFFieldWithTag(tif, tag);
				if(fip==null) continue;

				uint value_count;
				object raw_data=null;

				if(fip.field_passcount)
				{
					object[] ap=new object[2];
					if(!TIFFGetField(tif, tag, ap)) continue;
					value_count=__GetAsUshort(ap, 0);
					raw_data=ap[1];
				}
				else
				{
					if(fip.field_readcount==TIFF_VARIABLE) value_count=1;
					else if(fip.field_readcount==TIFF_SPP) value_count=td.td_samplesperpixel;
					else value_count=(ushort)fip.field_readcount;

					if(fip.field_type==TIFFDataType.TIFF_ASCII||fip.field_readcount==TIFF_VARIABLE||fip.field_readcount==TIFF_SPP||value_count>1)
					{
						object[] ap=new object[2];
						if(!TIFFGetField(tif, tag, ap)) continue;
						raw_data=ap[0];
					}
					else
					{
						object[] ap=new object[value_count];
						if(!TIFFGetField(tif, tag, ap)) continue;

						switch(fip.field_type)
						{
							case TIFFDataType.TIFF_UNDEFINED:
							case TIFFDataType.TIFF_BYTE: { byte[] raw_data1=new byte[value_count]; for(int a=0; a<value_count; a++) raw_data1[a]=__GetAsByte(ap, a); raw_data=raw_data1; } break;
							case TIFFDataType.TIFF_DOUBLE:
							case TIFFDataType.TIFF_RATIONAL:
							case TIFFDataType.TIFF_SRATIONAL: { double[] raw_data1=new double[value_count]; for(int a=0; a<value_count; a++) raw_data1[a]=__GetAsDouble(ap, a); raw_data=raw_data1; } break;
							case TIFFDataType.TIFF_SBYTE: { sbyte[] raw_data1=new sbyte[value_count]; for(int a=0; a<value_count; a++) raw_data1[a]=__GetAsSbyte(ap, a); raw_data=raw_data1; } break;
							case TIFFDataType.TIFF_SHORT: { ushort[] raw_data1=new ushort[value_count]; for(int a=0; a<value_count; a++) raw_data1[a]=__GetAsUshort(ap, a); raw_data=raw_data1; } break;
							case TIFFDataType.TIFF_SSHORT: { short[] raw_data1=new short[value_count]; for(int a=0; a<value_count; a++) raw_data1[a]=__GetAsShort(ap, a); raw_data=raw_data1; } break;
							case TIFFDataType.TIFF_IFD:
							case TIFFDataType.TIFF_LONG: { uint[] raw_data1=new uint[value_count]; for(int a=0; a<value_count; a++) raw_data1[a]=__GetAsUint(ap, a); raw_data=raw_data1; } break;
							case TIFFDataType.TIFF_SLONG: { int[] raw_data1=new int[value_count]; for(int a=0; a<value_count; a++) raw_data1[a]=__GetAsInt(ap, a); raw_data=raw_data1; } break;
							case TIFFDataType.TIFF_FLOAT: { float[] raw_data1=new float[value_count]; for(int a=0; a<value_count; a++) raw_data1[a]=__GetAsFloat(ap, a); raw_data=raw_data1; } break;
						}
					}
				}

				// Catch the tags which needs to be specially handled and
				// pretty print them. If tag not handled in
				// _TIFFPrettyPrintField() fall down and print it as any other tag.
				if(TIFFPrettyPrintField(tif, fd, tag, value_count, raw_data)) continue;
				else TIFFPrintField(fd, fip, value_count, raw_data);
			}

			if(tif.tif_tagmethods.printdir!=null) tif.tif_tagmethods.printdir(tif, fd, flags);

			if((flags&TIFFPRINT.STRIPS)!=0&&TIFFFieldSet(tif, FIELD.STRIPOFFSETS))
			{
				fd.WriteLine(" {0} {1}:", td.td_nstrips, isTiled(tif)?"Tiles":"Strips");
				for(uint s=0; s<td.td_nstrips; s++) fd.WriteLine("\t{0}: [{1}, {2}]", s, td.td_stripoffset[s], td.td_stripbytecount[s]);
			}
		}

		static void TIFFprintAscii(TextWriter fd, string cp)
		{
			foreach(char c in cp)
			{
				switch(c)
				{
					case '\0': fd.Write("\\0"); break;
					case '\t': fd.Write("\\t"); break;
					case '\b': fd.Write("\\b"); break;
					case '\r': fd.Write("\\r"); break;
					case '\n': fd.Write("\\n"); break;
					case '\v': fd.Write("\\v"); break;
					default: fd.Write(c); break;
				}
			}
		}

		static void TIFFprintAsciiTag(TextWriter fd, string name, string value)
		{
			fd.Write(" {0}: \"", name);
			TIFFprintAscii(fd, value);
			fd.WriteLine("\"");
		}
	}
}
