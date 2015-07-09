// tif_dir.cs
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
// Directory Tag Get & Set Routines.
// (and also some miscellaneous stuff)

using System;
using System.Collections.Generic;

namespace Free.Ports.LibTiff
{
	// "Library-private" Directory-related Definitions.

	// Internal format of a TIFF directory entry.
	class TIFFDirectory
	{
		// bit vector of fields that are set
		internal uint[] td_fieldsset=new uint[libtiff.FIELD_SETLONGS];

		internal uint td_imagewidth, td_imagelength, td_imagedepth;
		internal uint td_tilewidth, td_tilelength, td_tiledepth;
		internal FILETYPE td_subfiletype;
		internal ushort td_bitspersample;
		internal SAMPLEFORMAT td_sampleformat;
		internal COMPRESSION td_compression;
		internal PHOTOMETRIC td_photometric;
		internal THRESHHOLD td_threshholding;
		internal FILLORDER td_fillorder;
		internal ORIENTATION td_orientation;
		internal ushort td_samplesperpixel;
		internal uint td_rowsperstrip;
		internal ushort td_minsamplevalue, td_maxsamplevalue;
		internal double td_sminsamplevalue, td_smaxsamplevalue;
		internal double td_xresolution, td_yresolution;
		internal RESUNIT td_resolutionunit;
		internal PLANARCONFIG td_planarconfig;
		internal double td_xposition, td_yposition;
		internal ushort[] td_pagenumber=new ushort[2];
		internal ushort[][] td_colormap=new ushort[3][];
		internal ushort[] td_halftonehints=new ushort[2];
		internal ushort td_extrasamples;
		internal ushort[] td_sampleinfo;

		// even though the name is misleading, td_stripsperimage is the number
		// of striles (=strips or tiles) per plane, and td_nstrips the total
		// number of striles
		internal uint td_stripsperimage;
		internal uint td_nstrips;			// size of offset & bytecount arrays
		internal uint[] td_stripoffset;
		internal uint[] td_stripbytecount;	// FIXME: it should be tsize_t array
		internal int td_stripbytecountsorted;	// is the bytecount array sorted ascending?
		internal ushort td_nsubifd;
		internal uint[] td_subifd;

		// YCbCr parameters
		internal ushort[] td_ycbcrsubsampling=new ushort[2];
		internal YCBCRPOSITION td_ycbcrpositioning;

		// Colorimetry parameters
		internal double[] td_refblackwhite;
		internal ushort[][] td_transferfunction=new ushort[3][];

		// CMYK parameters
		internal int td_inknameslen;
		internal string td_inknames;

		internal int td_customValueCount;
		internal List<TIFFTagValue> td_customValues;

		public void Clear()
		{
			td_fieldsset=new uint[libtiff.FIELD_SETLONGS];
			td_imagewidth=td_imagelength=td_imagedepth=0;
			td_tilewidth=td_tilelength=td_tiledepth=0;
			td_subfiletype=0;
			td_bitspersample=0;
			td_sampleformat=0;
			td_compression=0;
			td_photometric=0;
			td_threshholding=0;
			td_fillorder=0;
			td_orientation=0;
			td_samplesperpixel=0;
			td_rowsperstrip=0;
			td_minsamplevalue=td_maxsamplevalue=0;
			td_sminsamplevalue=td_smaxsamplevalue=0;
			td_xresolution=td_yresolution=0;
			td_resolutionunit=0;
			td_planarconfig=0;
			td_xposition=td_yposition=0;
			td_pagenumber=new ushort[2];
			td_colormap=new ushort[3][];
			td_halftonehints=new ushort[2];
			td_extrasamples=0;
			td_sampleinfo=null;

			td_stripsperimage=0;
			td_nstrips=0;				// size of offset & bytecount arrays
			td_stripoffset=null;
			td_stripbytecount=null;
			td_stripbytecountsorted=0;	// is the bytecount array sorted ascending?
			td_nsubifd=0;
			td_subifd=null;

			// YCbCr parameters
			td_ycbcrsubsampling=new ushort[2];
			td_ycbcrpositioning=0;

			// Colorimetry parameters
			td_refblackwhite=null;
			td_transferfunction=new ushort[3][];

			// CMYK parameters
			td_inknameslen=0;
			td_inknames=null;

			td_customValueCount=0;
			td_customValues.Clear();
		}
	}

	// Field flags used to indicate fields that have
	// been set in a directory, and to reference fields
	// when manipulating a directory.

	// FIELD_IGNORE is used to signify tags that are to
	// be processed but otherwise ignored. This permits
	// antiquated tags to be quietly read and discarded.
	// Note that a bit *is* allocated for ignored tags;
	// this is understood by the directory reading logic
	// which uses this fact to avoid special-case handling
	public enum FIELD : int
	{
		IGNORE=0,
		PSEUDO=0,

		// multi-item fields
		IMAGEDIMENSIONS=1,
		TILEDIMENSIONS=2,
		RESOLUTION=3,
		POSITION=4,

		// single-item fields
		SUBFILETYPE=5,
		BITSPERSAMPLE=6,
		COMPRESSION=7,
		PHOTOMETRIC=8,
		THRESHHOLDING=9,
		FILLORDER=10,
		ORIENTATION=15,
		SAMPLESPERPIXEL=16,
		ROWSPERSTRIP=17,
		MINSAMPLEVALUE=18,
		MAXSAMPLEVALUE=19,
		PLANARCONFIG=20,
		RESOLUTIONUNIT=22,
		PAGENUMBER=23,
		STRIPBYTECOUNTS=24,
		STRIPOFFSETS=25,
		COLORMAP=26,
		EXTRASAMPLES=31,
		SAMPLEFORMAT=32,
		SMINSAMPLEVALUE=33,
		SMAXSAMPLEVALUE=34,
		IMAGEDEPTH=35,
		TILEDEPTH=36,
		HALFTONEHINTS=37,
		YCBCRSUBSAMPLING=39,
		YCBCRPOSITIONING=40,
		REFBLACKWHITE=41,
		TRANSFERFUNCTION=44,
		INKNAMES=46,
		SUBIFD=49,
		CUSTOM=65,
		// end of support for well-known tags; codec-private tags follow
		CODEC=66,	// base of codec-private tags

#if JPEG_SUPPORT
		JPEG_JPEGTABLES=FIELD.CODEC+0,
		JPEG_RECVPARAMS=FIELD.CODEC+1,
		JPEG_SUBADDRESS=FIELD.CODEC+2,
		JPEG_RECVTIME=FIELD.CODEC+3,
		JPEG_FAXDCS=FIELD.CODEC+4,
#endif

#if CCITT_SUPPORT
		CCITT_BADFAXLINES=FIELD.CODEC+0,
		CCITT_CLEANFAXDATA=FIELD.CODEC+1,
		CCITT_BADFAXRUN=FIELD.CODEC+2,
		CCITT_RECVPARAMS=FIELD.CODEC+3,
		CCITT_SUBADDRESS=FIELD.CODEC+4,
		CCITT_RECVTIME=FIELD.CODEC+5,
		CCITT_FAXDCS=FIELD.CODEC+6,
		CCITT_OPTIONS=FIELD.CODEC+7,
#endif
	}

	public static partial class libtiff
	{
		internal const int FIELD_SETLONGS=4;

		// Pseudo-tags don't normally need field bits since they
		// are not written to an output file (by definition).
		// The library also has express logic to always query a
		// codec for a pseudo-tag so allocating a field bit for
		// one is a waste. If codec wants to promote the notion
		// of a pseudo-tag being "set" or "unset" then it can
		// do using internal state flags without polluting the
		// field bit space defined for real tags.
		const int FIELD_PSEUDO=0;
		const int FIELD_LAST=(32*FIELD_SETLONGS-1);

		static bool TIFFFieldSet(TIFF tif, FIELD field)
		{
			return (tif.tif_dir.td_fieldsset[((int)field)/32]&(1u<<(((int)field)&0x1f)))!=0;
		}

		static void TIFFSetFieldBit(TIFF tif, FIELD field)
		{
			tif.tif_dir.td_fieldsset[((int)field)/32]|=(1u<<(((int)field)&0x1f));
		}

		static void TIFFClrFieldBit(TIFF tif, FIELD field)
		{
			tif.tif_dir.td_fieldsset[((int)field)/32]&=~(1u<<(((int)field)&0x1f));
		}

		static bool FieldSet(uint[] fields, FIELD field)
		{
			return (fields[((int)field)/32]&(1u<<(((int)field)&0x1f)))!=0;
		}

		static void ResetFieldBit(uint[] fields, FIELD field)
		{
			fields[((int)field)/32]&=~(1u<<(((int)field)&0x1f));
		}

		// These are used in the backwards compatibility code...
		const uint DATATYPE_VOID=0;		// !untyped data
		const uint DATATYPE_INT=1;		// !signed integer data
		const uint DATATYPE_UINT=2;		// !unsigned integer data
		const uint DATATYPE_IEEEFP=3;	// !IEEE floating point data

		static void TIFFsetByteArray(ref byte[] vpp, byte[] vp, uint n)
		{
			vpp=null;
			if(vp!=null)
			{
				try
				{
					vpp=new byte[n];
					Array.Copy(vp, vpp, n);
				}
				catch
				{
				}
			}
		}

		static void TIFFsetShortArray(ref ushort[] wpp, ushort[] wp, uint n)
		{
			wpp=null;
			if(wp!=null)
			{
				try
				{
					wpp=new ushort[n];
					Array.Copy(wp, wpp, n);
				}
				catch
				{
				}
			}
		}

		static void TIFFsetLongArray(ref uint[] lpp, uint[] lp, uint n)
		{
			lpp=null;
			if(lp!=null)
			{
				try
				{
					lpp=new uint[n];
					Array.Copy(lp, lpp, n);
				}
				catch
				{
				}
			}
		}

		static void TIFFsetFloatArray(ref float[] fpp, float[] fp, uint n)
		{
			fpp=null;
			if(fp!=null)
			{
				try
				{
					fpp=new float[n];
					Array.Copy(fp, fpp, n);
				}
				catch
				{
				}
			}
		}

		static void TIFFsetDoubleArray(ref double[] dpp, double[] dp, uint n)
		{
			dpp=null;
			if(dp!=null)
			{
				try
				{
					dpp=new double[n];
					Array.Copy(dp, dpp, n);
				}
				catch
				{
				}
			}
		}

		// Install extra samples information.
		static bool setExtraSamples(TIFFDirectory td, object[] ap, out uint v)
		{
			v=(uint)__GetAsUint(ap, 0);

			if((ushort)v>td.td_samplesperpixel) return false;

			Array a=ap[1] as Array;
			if(v>1&&a==null) return false;		// typically missing param

			if(a==null&&v==1)
			{
				if(!(ap[1] is ushort)) // typically wrong param type, maybe EXTRASAMPLE[]
				{
					if(!(ap[1] is EXTRASAMPLE)) return false;

					EXTRASAMPLE va=(EXTRASAMPLE)ap[1];
					ushort[] tmp=new ushort[1];

					if(va>EXTRASAMPLE.UNASSALPHA)
					{
						// XXX: Corel Draw is known to produce incorrect
						// ExtraSamples tags which must be patched here if we
						// want to be able to open some of the damaged TIFF
						// files:
						if((int)va==999) va=EXTRASAMPLE.UNASSALPHA;
						else return false;
					}
					tmp[0]=(ushort)va;

					td.td_extrasamples=(ushort)v;
					TIFFsetShortArray(ref td.td_sampleinfo, tmp, td.td_extrasamples);
				}
				else
				{
					ushort[] va=new ushort[1];

					va[0]=(ushort)ap[1];

					if(va[0]>(ushort)EXTRASAMPLE.UNASSALPHA)
					{
						// XXX: Corel Draw is known to produce incorrect
						// ExtraSamples tags which must be patched here if we
						// want to be able to open some of the damaged TIFF
						// files:
						if(va[0]==999) va[0]=(ushort)EXTRASAMPLE.UNASSALPHA;
						else return false;
					}

					td.td_extrasamples=(ushort)v;
					TIFFsetShortArray(ref td.td_sampleinfo, va, td.td_extrasamples);
				}
			}
			else
			{
				if(!(a is ushort[])) // typically wrong param type, maybe EXTRASAMPLE[]
				{
					if(!(a is EXTRASAMPLE[])) return false;

					EXTRASAMPLE[] va=(EXTRASAMPLE[])a;
					ushort[] tmp=new ushort[v];

					for(int i=0; i<v; i++)
					{
						if(va[i]>EXTRASAMPLE.UNASSALPHA)
						{
							// XXX: Corel Draw is known to produce incorrect
							// ExtraSamples tags which must be patched here if we
							// want to be able to open some of the damaged TIFF
							// files:
							if((int)va[i]==999) va[i]=EXTRASAMPLE.UNASSALPHA;
							else return false;
						}
						tmp[i]=(ushort)va[i];
					}

					td.td_extrasamples=(ushort)v;
					TIFFsetShortArray(ref td.td_sampleinfo, tmp, td.td_extrasamples);
				}
				else
				{
					ushort[] va=(ushort[])a;

					for(int i=0; i<v; i++)
					{
						if(va[i]>(ushort)EXTRASAMPLE.UNASSALPHA)
						{
							// XXX: Corel Draw is known to produce incorrect
							// ExtraSamples tags which must be patched here if we
							// want to be able to open some of the damaged TIFF
							// files:
							if(va[i]==999) va[i]=(ushort)EXTRASAMPLE.UNASSALPHA;
							else return false;
						}
					}

					td.td_extrasamples=(ushort)v;
					TIFFsetShortArray(ref td.td_sampleinfo, va, td.td_extrasamples);
				}
			}

			return true;
		}

		static uint checkInkNamesString(TIFF tif, uint slen, string s)
		{
			TIFFDirectory td=tif.tif_dir;
			ushort i=td.td_samplesperpixel;

			if(slen>0)
			{
				string[] inks=s.Split('\0');
				if(inks.Length>=i)
				{
					uint len=(uint)inks[0].Length+1;
					for(int a=1; a<i; a++) len+=(uint)inks[a].Length+1;
					return len;
				}
				i-=(ushort)inks.Length;
			}

			TIFFErrorExt(tif.tif_clientdata, "TIFFSetField", "{0}: Invalid InkNames value; expecting {1} names, found {2}", tif.tif_name, td.td_samplesperpixel, td.td_samplesperpixel-i);
			return 0;
		}

		static bool _TIFFVSetField(TIFF tif, TIFFTAG tag, TIFFDataType dt, object[] ap)
		{
			string module="_TIFFVSetField";

			TIFFDirectory td=tif.tif_dir;
			bool status=true;
			uint v;

			switch(tag)
			{
				case TIFFTAG.SUBFILETYPE:
					td.td_subfiletype=(FILETYPE)__GetAsUint(ap, 0);
					break;
				case TIFFTAG.IMAGEWIDTH:
					td.td_imagewidth=__GetAsUint(ap, 0);
					break;
				case TIFFTAG.IMAGELENGTH:
					td.td_imagelength=__GetAsUint(ap, 0);
					break;
				case TIFFTAG.BITSPERSAMPLE:
					td.td_bitspersample=__GetAsUshort(ap, 0);

					// If the data require post-decoding processing to byte-swap
					// samples, set it up here. Note that since tags are required
					// to be ordered, compression code can override this behaviour
					// in the setup method if it wants to roll the post decoding
					// work in with its normal work.
					if((tif.tif_flags&TIF_FLAGS.TIFF_SWAB)!=0)
					{
						if(td.td_bitspersample==16) tif.tif_postdecode=TIFFSwab16BitData;
						else if(td.td_bitspersample==24) tif.tif_postdecode=TIFFSwab24BitData;
						else if(td.td_bitspersample==32) tif.tif_postdecode=TIFFSwab32BitData;
						else if(td.td_bitspersample==64) tif.tif_postdecode=TIFFSwab64BitData;
						else if(td.td_bitspersample==128) tif.tif_postdecode=TIFFSwab64BitData; // two 64's
					}
					break;
				case TIFFTAG.COMPRESSION:
					v=__GetAsUint(ap, 0)&0xffff;

					// If we're changing the compression scheme, the notify the
					// previous module so that it can cleanup any state it's setup.
					if(TIFFFieldSet(tif, FIELD.COMPRESSION))
					{
						if(td.td_compression==(COMPRESSION)v) break;
						tif.tif_cleanup(tif);
						tif.tif_flags&=~TIF_FLAGS.TIFF_CODERSETUP;
					}

					// Setup new compression routine state.
					if(status=TIFFSetCompressionScheme(tif, (COMPRESSION)v)) td.td_compression=(COMPRESSION)v;
					else status=false;
					break;
				case TIFFTAG.PHOTOMETRIC:
					td.td_photometric=(PHOTOMETRIC)__GetAsUshort(ap, 0);
					break;
				case TIFFTAG.THRESHHOLDING:
					td.td_threshholding=(THRESHHOLD)__GetAsUshort(ap, 0);
					break;
				case TIFFTAG.FILLORDER:
					v=__GetAsUint(ap, 0);
					if((FILLORDER)v!=FILLORDER.LSB2MSB&&(FILLORDER)v!=FILLORDER.MSB2LSB) goto badvalue;
					td.td_fillorder=(FILLORDER)v;
					break;
				case TIFFTAG.ORIENTATION:
					v=__GetAsUint(ap, 0);
					if((ORIENTATION)v<ORIENTATION.TOPLEFT||ORIENTATION.LEFTBOT<(ORIENTATION)v)
						goto badvalue;
					td.td_orientation=(ORIENTATION)v;
					break;
				case TIFFTAG.SAMPLESPERPIXEL:
					// XXX should cross check -- e.g. if pallette, then 1
					v=__GetAsUint(ap, 0);
					if(v==0) goto badvalue;
					td.td_samplesperpixel=(ushort)v;
					break;
				case TIFFTAG.ROWSPERSTRIP:
					v=__GetAsUint(ap, 0);
					if(v==0) goto badvalue;
					td.td_rowsperstrip=v;
					if(!TIFFFieldSet(tif, FIELD.TILEDIMENSIONS))
					{
						td.td_tilelength=v;
						td.td_tilewidth=td.td_imagewidth;
					}
					break;
				case TIFFTAG.MINSAMPLEVALUE:
					td.td_minsamplevalue=__GetAsUshort(ap, 0);
					break;
				case TIFFTAG.MAXSAMPLEVALUE:
					td.td_maxsamplevalue=__GetAsUshort(ap, 0);
					break;
				case TIFFTAG.SMINSAMPLEVALUE:
					td.td_sminsamplevalue=__GetAsDouble(ap, 0);
					break;
				case TIFFTAG.SMAXSAMPLEVALUE:
					td.td_smaxsamplevalue=__GetAsDouble(ap, 0);
					break;
				case TIFFTAG.XRESOLUTION:
					td.td_xresolution=__GetAsDouble(ap, 0);
					break;
				case TIFFTAG.YRESOLUTION:
					td.td_yresolution=__GetAsDouble(ap, 0);
					break;
				case TIFFTAG.PLANARCONFIG:
					v=__GetAsUint(ap, 0);
					if((PLANARCONFIG)v!=PLANARCONFIG.CONTIG&&(PLANARCONFIG)v!=PLANARCONFIG.SEPARATE) goto badvalue;
					td.td_planarconfig=(PLANARCONFIG)v;
					break;
				case TIFFTAG.XPOSITION:
					td.td_xposition=__GetAsDouble(ap, 0);
					break;
				case TIFFTAG.YPOSITION:
					td.td_yposition=__GetAsDouble(ap, 0);
					break;
				case TIFFTAG.RESOLUTIONUNIT:
					v=__GetAsUint(ap, 0);
					if((RESUNIT)v<RESUNIT.NONE||RESUNIT.CENTIMETER<(RESUNIT)v) goto badvalue;
					td.td_resolutionunit=(RESUNIT)v;
					break;
				case TIFFTAG.PAGENUMBER:
					td.td_pagenumber[0]=__GetAsUshort(ap, 0);
					td.td_pagenumber[1]=__GetAsUshort(ap, 1);
					break;
				case TIFFTAG.HALFTONEHINTS:
					td.td_halftonehints[0]=__GetAsUshort(ap, 0);
					td.td_halftonehints[1]=__GetAsUshort(ap, 1);
					break;
				case TIFFTAG.COLORMAP:
					v=1u<<td.td_bitspersample;
					TIFFsetShortArray(ref td.td_colormap[0], (ushort[])ap[0], v);
					TIFFsetShortArray(ref td.td_colormap[1], (ushort[])ap[1], v);
					TIFFsetShortArray(ref td.td_colormap[2], (ushort[])ap[2], v);
					break;
				case TIFFTAG.EXTRASAMPLES:
					if(!setExtraSamples(td, ap, out v)) goto badvalue;
					break;
				case TIFFTAG.MATTEING:
					td.td_extrasamples=(ushort)((__GetAsInt(ap, 0)!=0)?1:0);
					if(td.td_extrasamples!=0)
					{
						ushort[] sv=new ushort[] { (ushort)EXTRASAMPLE.ASSOCALPHA };
						TIFFsetShortArray(ref td.td_sampleinfo, sv, 1);
					}
					break;
				case TIFFTAG.TILEWIDTH:
					v=__GetAsUint(ap, 0);
					if((v%16)!=0)
					{
						if(tif.tif_mode!=O.RDONLY) goto badvalue;
						TIFFWarningExt(tif.tif_clientdata, tif.tif_name, "Nonstandard tile width {0}, convert file", v);
					}
					td.td_tilewidth=v;
					tif.tif_flags|=TIF_FLAGS.TIFF_ISTILED;
					break;
				case TIFFTAG.TILELENGTH:
					v=__GetAsUint(ap, 0);
					if((v%16)!=0)
					{
						if(tif.tif_mode!=O.RDONLY) goto badvalue;
						TIFFWarningExt(tif.tif_clientdata, tif.tif_name, "Nonstandard tile length {0}, convert file", v);
					}
					td.td_tilelength=v;
					tif.tif_flags|=TIF_FLAGS.TIFF_ISTILED;
					break;
				case TIFFTAG.TILEDEPTH:
					v=__GetAsUint(ap, 0);
					if(v==0) goto badvalue;
					td.td_tiledepth=v;
					break;
				case TIFFTAG.DATATYPE:
					v=__GetAsUint(ap, 0);
					switch(v)
					{
						case DATATYPE_VOID: td.td_sampleformat=SAMPLEFORMAT.VOID; break;
						case DATATYPE_INT: td.td_sampleformat=SAMPLEFORMAT.INT; break;
						case DATATYPE_UINT: td.td_sampleformat=SAMPLEFORMAT.UINT; break;
						case DATATYPE_IEEEFP: td.td_sampleformat=SAMPLEFORMAT.IEEEFP; break;
						default: goto badvalue;
					}
					break;
				case TIFFTAG.SAMPLEFORMAT:
					v=__GetAsUint(ap, 0);
					if((SAMPLEFORMAT)v<SAMPLEFORMAT.UINT||SAMPLEFORMAT.COMPLEXIEEEFP<(SAMPLEFORMAT)v) goto badvalue;
					td.td_sampleformat=(SAMPLEFORMAT)v;

					// Try to fix up the SWAB function for complex data.
					if(td.td_sampleformat==SAMPLEFORMAT.COMPLEXINT&&td.td_bitspersample==32&&tif.tif_postdecode==TIFFSwab32BitData) tif.tif_postdecode=TIFFSwab16BitData;
					else if((td.td_sampleformat==SAMPLEFORMAT.COMPLEXINT||td.td_sampleformat==SAMPLEFORMAT.COMPLEXIEEEFP)&&td.td_bitspersample==64&&
						tif.tif_postdecode==TIFFSwab64BitData) tif.tif_postdecode=TIFFSwab32BitData;

					break;
				case TIFFTAG.IMAGEDEPTH:
					td.td_imagedepth=__GetAsUint(ap, 0);
					break;
				case TIFFTAG.SUBIFD:
					if((tif.tif_flags&TIF_FLAGS.TIFF_INSUBIFD)==0)
					{
						td.td_nsubifd=__GetAsUshort(ap, 0);
						uint[] tmp=new uint[1];
						tmp[0]=(uint)ap[1];
						TIFFsetLongArray(ref td.td_subifd, tmp, td.td_nsubifd);
					}
					else
					{
						TIFFErrorExt(tif.tif_clientdata, module, "{0}: Sorry, cannot nest SubIFDs", tif.tif_name);
						status=false;
					}
					break;
				case TIFFTAG.YCBCRPOSITIONING:
					td.td_ycbcrpositioning=(YCBCRPOSITION)__GetAsUshort(ap, 0);
					break;
				case TIFFTAG.YCBCRSUBSAMPLING:
					td.td_ycbcrsubsampling[0]=__GetAsUshort(ap, 0);
					td.td_ycbcrsubsampling[1]=__GetAsUshort(ap, 1);
					break;
				case TIFFTAG.TRANSFERFUNCTION:
					v=(uint)((td.td_samplesperpixel-td.td_extrasamples)>1?3:1);
					for(int i=0; i<v; i++)
						TIFFsetShortArray(ref td.td_transferfunction[i], (ushort[])ap[i], 1u<<td.td_bitspersample);
					break;
				case TIFFTAG.REFERENCEBLACKWHITE:
					// XXX should check for null range
					TIFFsetDoubleArray(ref td.td_refblackwhite, (double[])ap[0], 6);
					break;
				case TIFFTAG.INKNAMES:
					v=__GetAsUint(ap, 0);
					string s=(string)ap[1];
					v=checkInkNamesString(tif, v, s);
					status=v>0;
					if(v>0)
					{
						td.td_inknames=s.Substring(0, (int)v);
						td.td_inknameslen=(int)v;
					}
					break;
				default:
					{
						TIFFFieldInfo fip=TIFFFindFieldInfo(tif, tag, dt); // was TIFFDataType.TIFF_ANY);
						TIFFTagValue tv;
						int iCustom;

						// This can happen if multiple images are open with different
						// codecs which have private tags. The global tag information
						// table may then have tags that are valid for one file but not
						// the other. If the client tries to set a tag that is not valid
						// for the image's codec then we'll arrive here. This
						// happens, for example, when tiffcp is used to convert between
						// compression schemes and codec-specific tags are blindly copied.
						if(fip==null||fip.field_bit!=FIELD.CUSTOM)
						{
							TIFFErrorExt(tif.tif_clientdata, module, "{0}: Invalid {1}tag \"{2}\" (not supported by codec)", tif.tif_name, isPseudoTag(tag)?"pseudo-":"", fip!=null?fip.field_name:"Unknown");
							status=false;
							break;
						}

						// Find the existing entry for this custom value.
						tv=null;
						for(iCustom=0; iCustom<td.td_customValueCount; iCustom++)
						{
							if(td.td_customValues[iCustom].info.field_tag==tag)
							{
								tv=td.td_customValues[iCustom];
								tv.value=null;
								break;
							}
						}

						// Grow the custom list if the entry was not found.
						if(tv==null)
						{
							try
							{
								td.td_customValueCount++;
								if(td.td_customValues==null) td.td_customValues=new List<TIFFTagValue>();

								tv=new TIFFTagValue();
								tv.info=fip;

								td.td_customValues.Add(tv);
							}
							catch
							{
								TIFFErrorExt(tif.tif_clientdata, module, "{0}: Failed to allocate space for list of custom values", tif.tif_name);
								status=false;
								goto end;
							}
						}

						// Set custom value ... save a copy of the custom tag value.
						if(TIFFDataSize(fip.field_type)==0)
						{
							status=false;
							TIFFErrorExt(tif.tif_clientdata, module, "{0}: Bad field type {1} for \"{2}\"", tif.tif_name, fip.field_type, fip.field_name);
							goto end;
						}

						int apcount=0;
						if(fip.field_passcount) tv.count=__GetAsInt(ap, apcount++);
						else if(fip.field_writecount==TIFF_VARIABLE) tv.count=1;
						else if(fip.field_writecount==TIFF_SPP) tv.count=td.td_samplesperpixel;
						else tv.count=fip.field_writecount;

						if(fip.field_type==TIFFDataType.TIFF_ASCII) tv.value=(string)ap[apcount++];
						else
						{
							if(tv.count<1)
							{
								status=false;
								goto end;
							}

							byte[] byteArray=null;
							sbyte[] sbyteArray=null;
							ushort[] ushortArray=null;
							short[] shortArray=null;
							uint[] uintArray=null;
							int[] intArray=null;
							float[] floatArray=null;
							double[] doubleArray=null;

							try
							{
								switch(fip.field_type)
								{
									case TIFFDataType.TIFF_UNDEFINED:
									case TIFFDataType.TIFF_BYTE:
										tv.value=byteArray=new byte[tv.count];
										break;
									case TIFFDataType.TIFF_SBYTE:
										tv.value=sbyteArray=new sbyte[tv.count];
										break;
									case TIFFDataType.TIFF_SHORT:
										tv.value=ushortArray=new ushort[tv.count];
										break;
									case TIFFDataType.TIFF_SSHORT:
										tv.value=shortArray=new short[tv.count];
										break;
									case TIFFDataType.TIFF_IFD:
									case TIFFDataType.TIFF_LONG:
										tv.value=uintArray=new uint[tv.count];
										break;
									case TIFFDataType.TIFF_SLONG:
										tv.value=intArray=new int[tv.count];
										break;
									case TIFFDataType.TIFF_FLOAT:
										tv.value=floatArray=new float[tv.count];
										break;
									case TIFFDataType.TIFF_RATIONAL:
									case TIFFDataType.TIFF_SRATIONAL:
									case TIFFDataType.TIFF_DOUBLE:
										tv.value=doubleArray=new double[tv.count];
										break;
									default:
										tv.value=null;
										break;
								}
							}
							catch
							{
								TIFFErrorExt(tif.tif_clientdata, module, "{0}: Out of memory.", tif.tif_name);
								status=false;
								goto end;
							}

							bool done=false;
							if((fip.field_passcount||fip.field_writecount==TIFF_VARIABLE||fip.field_writecount==TIFF_SPP))
							{
								Array a=ap[apcount] as Array;
								if(a!=null&&a.Length>=tv.count)
								{
									switch(fip.field_type)
									{
										case TIFFDataType.TIFF_UNDEFINED:
										case TIFFDataType.TIFF_BYTE:
											if(a is byte[])
											{
												Array.Copy(a, byteArray, tv.count);
												done=true;
											}
											else
											{
												TIFFErrorExt(tif.tif_clientdata, module, "{0}: Bad argument type for \"{2}\" ({1}). Should be a byte[].", tif.tif_name, fip.field_type, fip.field_name);
												status=false;
												goto end;
											}
											break;
										case TIFFDataType.TIFF_SBYTE:
											if(a is sbyte[])
											{
												Array.Copy(a, sbyteArray, tv.count);
												done=true;
											}
											else
											{
												TIFFErrorExt(tif.tif_clientdata, module, "{0}: Bad argument type for \"{2}\" ({1}). Should be a sbyte[].", tif.tif_name, fip.field_type, fip.field_name);
												status=false;
												goto end;
											}
											break;
										case TIFFDataType.TIFF_SHORT:
											if(a is ushort[])
											{
												Array.Copy(a, ushortArray, tv.count);
												done=true;
											}
											else
											{
												TIFFErrorExt(tif.tif_clientdata, module, "{0}: Bad argument type for \"{2}\" ({1}). Should be a ushort[].", tif.tif_name, fip.field_type, fip.field_name);
												status=false;
												goto end;
											}
											break;
										case TIFFDataType.TIFF_SSHORT:
											if(a is short[])
											{
												Array.Copy(a, shortArray, tv.count);
												done=true;
											}
											else
											{
												TIFFErrorExt(tif.tif_clientdata, module, "{0}: Bad argument type for \"{2}\" ({1}). Should be a short[].", tif.tif_name, fip.field_type, fip.field_name);
												status=false;
												goto end;
											}
											break;
										case TIFFDataType.TIFF_IFD:
										case TIFFDataType.TIFF_LONG:
											if(a is uint[])
											{
												Array.Copy(a, uintArray, tv.count);
												done=true;
											}
											else
											{
												TIFFErrorExt(tif.tif_clientdata, module, "{0}: Bad argument type for \"{2}\" ({1}). Should be a ulong[].", tif.tif_name, fip.field_type, fip.field_name);
												status=false;
												goto end;
											}
											break;
										case TIFFDataType.TIFF_SLONG:
											if(a is int[])
											{
												Array.Copy(a, intArray, tv.count);
												done=true;
											}
											else
											{
												TIFFErrorExt(tif.tif_clientdata, module, "{0}: Bad argument type for \"{2}\" ({1}). Should be a long[].", tif.tif_name, fip.field_type, fip.field_name);
												status=false;
												goto end;
											}
											break;
										case TIFFDataType.TIFF_FLOAT:
											if(a is float[])
											{
												Array.Copy(a, floatArray, tv.count);
												done=true;
											}
											else
											{
												TIFFErrorExt(tif.tif_clientdata, module, "{0}: Bad argument type for \"{2}\" ({1}). Should be a float[].", tif.tif_name, fip.field_type, fip.field_name);
												status=false;
												goto end;
											}
											break;
										case TIFFDataType.TIFF_RATIONAL:
										case TIFFDataType.TIFF_SRATIONAL:
										case TIFFDataType.TIFF_DOUBLE:
											if(a is double[])
											{
												Array.Copy(a, doubleArray, tv.count);
												done=true;
											}
											else
											{
												TIFFErrorExt(tif.tif_clientdata, module, "{0}: Bad argument type for \"{2}\" ({1}). Should be a double[].", tif.tif_name, fip.field_type, fip.field_name);
												status=false;
												goto end;
											}
											break;
										default:
											tv.value=null;
											break;
									} // switch
								} // if(a!=null&&a.Length>=tv.count)
								else if(a!=null)
								{
									TIFFErrorExt(tif.tif_clientdata, module, "{0}: Bad argument type for \"{2}\" ({1}). Array has too few elements.", tif.tif_name, fip.field_type, fip.field_name);
									status=false;
									goto end;
								}
							}

							if(!done)
							{
								try
								{
									Array a=ap[apcount] as Array;

									if(a!=null&&a.Length>=tv.count)
									{
										switch(fip.field_type)
										{
											case TIFFDataType.TIFF_UNDEFINED:
											case TIFFDataType.TIFF_BYTE:
												if(a is byte[])
												{
													Array.Copy(a, byteArray, tv.count);
													done=true;
												}
												else
												{
													TIFFErrorExt(tif.tif_clientdata, module, "{0}: Bad argument type for \"{2}\" ({1}). Should be a byte[].", tif.tif_name, fip.field_type, fip.field_name);
													status=false;
													goto end;
												}
												break;
											case TIFFDataType.TIFF_SBYTE:
												if(a is sbyte[])
												{
													Array.Copy(a, sbyteArray, tv.count);
													done=true;
												}
												else
												{
													TIFFErrorExt(tif.tif_clientdata, module, "{0}: Bad argument type for \"{2}\" ({1}). Should be a sbyte[].", tif.tif_name, fip.field_type, fip.field_name);
													status=false;
													goto end;
												}
												break;
											case TIFFDataType.TIFF_SHORT:
												if(a is ushort[])
												{
													Array.Copy(a, ushortArray, tv.count);
													done=true;
												}
												else
												{
													TIFFErrorExt(tif.tif_clientdata, module, "{0}: Bad argument type for \"{2}\" ({1}). Should be a ushort[].", tif.tif_name, fip.field_type, fip.field_name);
													status=false;
													goto end;
												}
												break;
											case TIFFDataType.TIFF_SSHORT:
												if(a is short[])
												{
													Array.Copy(a, shortArray, tv.count);
													done=true;
												}
												else
												{
													TIFFErrorExt(tif.tif_clientdata, module, "{0}: Bad argument type for \"{2}\" ({1}). Should be a short[].", tif.tif_name, fip.field_type, fip.field_name);
													status=false;
													goto end;
												}
												break;
											case TIFFDataType.TIFF_IFD:
											case TIFFDataType.TIFF_LONG:
												if(a is uint[])
												{
													Array.Copy(a, uintArray, tv.count);
													done=true;
												}
												else
												{
													TIFFErrorExt(tif.tif_clientdata, module, "{0}: Bad argument type for \"{2}\" ({1}). Should be a ulong[].", tif.tif_name, fip.field_type, fip.field_name);
													status=false;
													goto end;
												}
												break;
											case TIFFDataType.TIFF_SLONG:
												if(a is int[])
												{
													Array.Copy(a, intArray, tv.count);
													done=true;
												}
												else
												{
													TIFFErrorExt(tif.tif_clientdata, module, "{0}: Bad argument type for \"{2}\" ({1}). Should be a long[].", tif.tif_name, fip.field_type, fip.field_name);
													status=false;
													goto end;
												}
												break;
											case TIFFDataType.TIFF_FLOAT:
												if(a is float[])
												{
													Array.Copy(a, floatArray, tv.count);
													done=true;
												}
												else
												{
													TIFFErrorExt(tif.tif_clientdata, module, "{0}: Bad argument type for \"{2}\" ({1}). Should be a float[].", tif.tif_name, fip.field_type, fip.field_name);
													status=false;
													goto end;
												}
												break;
											case TIFFDataType.TIFF_RATIONAL:
											case TIFFDataType.TIFF_SRATIONAL:
											case TIFFDataType.TIFF_DOUBLE:
												if(a is double[])
												{
													Array.Copy(a, doubleArray, tv.count);
													done=true;
												}
												else
												{
													TIFFErrorExt(tif.tif_clientdata, module, "{0}: Bad argument type for \"{2}\" ({1}). Should be a double[].", tif.tif_name, fip.field_type, fip.field_name);
													status=false;
													goto end;
												}
												break;
											default:
												tv.value=null;
												break;
										} // switch
									}
									else
									{
										switch(fip.field_type)
										{
											case TIFFDataType.TIFF_UNDEFINED:
											case TIFFDataType.TIFF_BYTE:
												for(int i=0; i<tv.count; i++) byteArray[i]=__GetAsByte(ap, apcount++);
												break;
											case TIFFDataType.TIFF_SBYTE:
												for(int i=0; i<tv.count; i++) sbyteArray[i]=__GetAsSbyte(ap, apcount++);
												break;
											case TIFFDataType.TIFF_SHORT:
												for(int i=0; i<tv.count; i++) ushortArray[i]=__GetAsUshort(ap, apcount++);
												break;
											case TIFFDataType.TIFF_SSHORT:
												for(int i=0; i<tv.count; i++) shortArray[i]=__GetAsShort(ap, apcount++);
												break;
											case TIFFDataType.TIFF_IFD:
											case TIFFDataType.TIFF_LONG:
												for(int i=0; i<tv.count; i++) uintArray[i]=__GetAsUint(ap, apcount++);
												break;
											case TIFFDataType.TIFF_SLONG:
												for(int i=0; i<tv.count; i++) intArray[i]=__GetAsInt(ap, apcount++);
												break;
											case TIFFDataType.TIFF_FLOAT:
												for(int i=0; i<tv.count; i++) floatArray[i]=__GetAsFloat(ap, apcount++);
												break;
											case TIFFDataType.TIFF_RATIONAL:
											case TIFFDataType.TIFF_SRATIONAL:
											case TIFFDataType.TIFF_DOUBLE:
												for(int i=0; i<tv.count; i++) doubleArray[i]=__GetAsDouble(ap, apcount++);
												break;
										}
									}
								}
								catch
								{
									TIFFErrorExt(tif.tif_clientdata, module, "{0}: Bad argument({3}) type for \"{2}\" ({1}). Should be an Array anyway!", tif.tif_name, fip.field_type, fip.field_name, apcount);
									status=false;
									goto end;
								}
							} // if(!done)
						} // fip.field_type!=TIFFDataType.TIFF_ASCII
					} // default:
					break;
			} // switch()

			if(status)
			{
				TIFFSetFieldBit(tif, TIFFFieldWithTag(tif, tag).field_bit);
				tif.tif_flags|=TIF_FLAGS.TIFF_DIRTYDIRECT;
			}

end:
			return status;

badvalue:
			TIFFErrorExt(tif.tif_clientdata, module, "{0}: Bad value {1} for \"{2}\" tag", tif.tif_name, v, TIFFFieldWithTag(tif, tag).field_name);
			return false;
		}

		// Return 1/0 according to whether or not
		// it is permissible to set the tag's value.
		// Note that we allow ImageLength to be changed
		// so that we can append and extend to images.
		// Any other tag may not be altered once writing
		// has commenced, unless its value has no effect
		// on the format of the data that is written.
		static bool OkToChangeTag(TIFF tif, TIFFTAG tag)
		{
			TIFFFieldInfo fip=TIFFFindFieldInfo(tif, tag, TIFFDataType.TIFF_ANY);
			if(fip==null)
			{	// unknown tag
				TIFFErrorExt(tif.tif_clientdata, "TIFFSetField", "{0}: Unknown {1}tag {2}", tif.tif_name, isPseudoTag(tag)?"pseudo-":"", tag);
				return false;
			}

			if(tag!=TIFFTAG.IMAGELENGTH&&(tif.tif_flags&TIF_FLAGS.TIFF_BEENWRITING)!=0&&!fip.field_oktochange)
			{
				// Consult info table to see if tag can be changed
				// after we've started writing. We only allow changes
				// to those tags that don't/shouldn't affect the
				// compression and/or format of the data.
				TIFFErrorExt(tif.tif_clientdata, "TIFFSetField", "{0}: Cannot modify tag \"{1}\" while writing", tif.tif_name, fip.field_name);
				return false;
			}
			return true;
		}

		// Record the value of a field in the
		// internal directory structure. The
		// field will be written to the file
		// when/if the directory structure is
		// updated.
		public static bool TIFFSetField(TIFF tif, TIFFTAG tag, params object[] ap)
		{
			return TIFFVSetField(tif, tag, TIFFDataType.TIFF_ANY, ap);
		}

		public static bool TIFFSetField(TIFF tif, TIFFTAG tag, TIFFDataType dt, params object[] ap)
		{
			return TIFFVSetField(tif, tag, dt, ap);
		}

		// Like TIFFSetField, but taking a varargs
		// parameter list. This routine is useful
		// for building higher-level interfaces on
		// top of the library.
		public static bool TIFFVSetField(TIFF tif, TIFFTAG tag, object[] ap)
		{
			return OkToChangeTag(tif, tag)?tif.tif_tagmethods.vsetfield(tif, tag, TIFFDataType.TIFF_ANY, ap):false;
		}

		public static bool TIFFVSetField(TIFF tif, TIFFTAG tag, TIFFDataType dt, object[] ap)
		{
			return OkToChangeTag(tif, tag)?tif.tif_tagmethods.vsetfield(tif, tag, dt, ap):false;
		}

		static bool _TIFFVGetField(TIFF tif, TIFFTAG tag, object[] ap)
		{
			TIFFDirectory td=tif.tif_dir;
			bool ret_val=true;

			switch(tag)
			{
				case TIFFTAG.SUBFILETYPE: ap[0]=td.td_subfiletype; break;
				case TIFFTAG.IMAGEWIDTH: ap[0]=td.td_imagewidth; break;
				case TIFFTAG.IMAGELENGTH: ap[0]=td.td_imagelength; break;
				case TIFFTAG.BITSPERSAMPLE: ap[0]=td.td_bitspersample; break;
				case TIFFTAG.COMPRESSION: ap[0]=td.td_compression; break;
				case TIFFTAG.PHOTOMETRIC: ap[0]=td.td_photometric; break;
				case TIFFTAG.THRESHHOLDING: ap[0]=td.td_threshholding; break;
				case TIFFTAG.FILLORDER: ap[0]=td.td_fillorder; break;
				case TIFFTAG.ORIENTATION: ap[0]=td.td_orientation; break;
				case TIFFTAG.SAMPLESPERPIXEL: ap[0]=td.td_samplesperpixel; break;
				case TIFFTAG.ROWSPERSTRIP: ap[0]=td.td_rowsperstrip; break;
				case TIFFTAG.MINSAMPLEVALUE: ap[0]=td.td_minsamplevalue; break;
				case TIFFTAG.MAXSAMPLEVALUE: ap[0]=td.td_maxsamplevalue; break;
				case TIFFTAG.SMINSAMPLEVALUE: ap[0]=td.td_sminsamplevalue; break;
				case TIFFTAG.SMAXSAMPLEVALUE: ap[0]=td.td_smaxsamplevalue; break;
				case TIFFTAG.XRESOLUTION: ap[0]=td.td_xresolution; break;
				case TIFFTAG.YRESOLUTION: ap[0]=td.td_yresolution; break;
				case TIFFTAG.PLANARCONFIG: ap[0]=td.td_planarconfig; break;
				case TIFFTAG.XPOSITION: ap[0]=td.td_xposition; break;
				case TIFFTAG.YPOSITION: ap[0]=td.td_yposition; break;
				case TIFFTAG.RESOLUTIONUNIT: ap[0]=td.td_resolutionunit; break;
				case TIFFTAG.PAGENUMBER:
					ap[0]=td.td_pagenumber[0];
					ap[1]=td.td_pagenumber[1];
					break;
				case TIFFTAG.HALFTONEHINTS:
					ap[0]=td.td_halftonehints[0];
					ap[1]=td.td_halftonehints[1];
					break;
				case TIFFTAG.COLORMAP:
					ap[0]=td.td_colormap[0];
					ap[1]=td.td_colormap[1];
					ap[2]=td.td_colormap[2];
					break;
				case TIFFTAG.STRIPOFFSETS:
				case TIFFTAG.TILEOFFSETS: ap[0]=td.td_stripoffset; break;
				case TIFFTAG.STRIPBYTECOUNTS:
				case TIFFTAG.TILEBYTECOUNTS: ap[0]=td.td_stripbytecount; break;
				case TIFFTAG.MATTEING:
					ap[0]=(td.td_extrasamples==1&&(EXTRASAMPLE)td.td_sampleinfo[0]==EXTRASAMPLE.ASSOCALPHA);
					break;
				case TIFFTAG.EXTRASAMPLES:
					ap[0]=td.td_extrasamples;
					ap[1]=td.td_sampleinfo;
					break;
				case TIFFTAG.TILEWIDTH: ap[0]=td.td_tilewidth; break;
				case TIFFTAG.TILELENGTH: ap[0]=td.td_tilelength; break;
				case TIFFTAG.TILEDEPTH: ap[0]=td.td_tiledepth; break;
				case TIFFTAG.DATATYPE:
					switch(td.td_sampleformat)
					{
						case SAMPLEFORMAT.UINT: ap[0]=DATATYPE_UINT; break;
						case SAMPLEFORMAT.INT: ap[0]=DATATYPE_INT; break;
						case SAMPLEFORMAT.IEEEFP: ap[0]=DATATYPE_IEEEFP; break;
						case SAMPLEFORMAT.VOID: ap[0]=DATATYPE_VOID; break;
					}
					break;
				case TIFFTAG.SAMPLEFORMAT: ap[0]=td.td_sampleformat; break;
				case TIFFTAG.IMAGEDEPTH: ap[0]=td.td_imagedepth; break;
				case TIFFTAG.SUBIFD:
					ap[0]=td.td_nsubifd;
					ap[1]=td.td_subifd;
					break;
				case TIFFTAG.YCBCRPOSITIONING: ap[0]=td.td_ycbcrpositioning; break;
				case TIFFTAG.YCBCRSUBSAMPLING:
					ap[0]=td.td_ycbcrsubsampling[0];
					ap[1]=td.td_ycbcrsubsampling[1];
					break;
				case TIFFTAG.TRANSFERFUNCTION:
					ap[0]=td.td_transferfunction[0];
					if(td.td_samplesperpixel-td.td_extrasamples>1)
					{
						ap[1]=td.td_transferfunction[1];
						ap[2]=td.td_transferfunction[2];
					}
					break;
				case TIFFTAG.REFERENCEBLACKWHITE:
					ap[0]=td.td_refblackwhite;
					break;
				case TIFFTAG.INKNAMES: ap[0]=td.td_inknames; break;
				default:
					{
						TIFFFieldInfo fip=TIFFFindFieldInfo(tif, tag, TIFFDataType.TIFF_ANY);

						// This can happen if multiple images are open with
						// different codecs which have private tags. The
						// global tag information table may then have tags
						// that are valid for one file but not the other.
						// If the client tries to get a tag that is not valid
						// for the image's codec then we'll arrive here.
						if(fip==null||fip.field_bit!=FIELD.CUSTOM)
						{
							TIFFErrorExt(tif.tif_clientdata, "_TIFFVGetField", "{0}: Invalid {1}tag \"{2}\" (not supported by codec)", tif.tif_name, isPseudoTag(tag)?"pseudo-":"", fip!=null?fip.field_name:"Unknown");
							ret_val=false;
							break;
						}

						// Do we have a custom value?
						ret_val=false;

						for(int i=0; i<td.td_customValueCount; i++)
						{
							TIFFTagValue tv=td.td_customValues[i];

							if(tv.info.field_tag!=tag) continue;

							if(fip.field_passcount)
							{
								ap[0]=(uint)tv.count; // Assume TIFF_VARIABLE
								ap[1]=tv.value;
								ret_val=true;
							}
							else
							{
								if(fip.field_type==TIFFDataType.TIFF_ASCII||fip.field_readcount==TIFF_VARIABLE||fip.field_readcount==TIFF_SPP)
								{
									ap[0]=tv.value;
									ret_val=true;
								}
								else
								{
									if(ap.Length<tv.count) break; // maybe Error or Warning?

									Array a=tv.value as Array;
									if(a==null) break; // maybe Error or Warning?

									for(int j=0; j<tv.count; j++) ap[j]=a.GetValue(j);
									ret_val=true;
								}
							}
							break;
						} // for
					} // default
					break;
			}
			return ret_val;
		}

		// Return the value of a field in the
		// internal directory structure.
		public static bool TIFFGetField(TIFF tif, TIFFTAG tag, object[] ap)
		{
			return TIFFVGetField(tif, tag, ap);
		}

		// Like TIFFGetField, but taking a varargs
		// parameter list. This routine is useful
		// for building higher-level interfaces on
		// top of the library.
		public static bool TIFFVGetField(TIFF tif, TIFFTAG tag, object[] ap)
		{
			TIFFFieldInfo fip=TIFFFindFieldInfo(tif, tag, TIFFDataType.TIFF_ANY);
			return (fip!=null&&(isPseudoTag(tag)||TIFFFieldSet(tif, fip.field_bit))?tif.tif_tagmethods.vgetfield(tif, tag, ap):false);
		}

		// Release storage associated with a directory.
		public static void TIFFFreeDirectory(TIFF tif)
		{
			TIFFDirectory td=tif.tif_dir;

			for(int i=0; i<FIELD_SETLONGS; i++) td.td_fieldsset[i]=0;
			td.td_colormap[0]=null;
			td.td_colormap[1]=null;
			td.td_colormap[2]=null;
			td.td_sampleinfo=null;
			td.td_subifd=null;
			td.td_inknames=null;
			td.td_refblackwhite=null;
			td.td_transferfunction[0]=null;
			td.td_transferfunction[1]=null;
			td.td_transferfunction[2]=null;
			td.td_stripoffset=null;
			td.td_stripbytecount=null;
			TIFFClrFieldBit(tif, FIELD.YCBCRSUBSAMPLING);
			TIFFClrFieldBit(tif, FIELD.YCBCRPOSITIONING);

			// Cleanup custom tag values
			td.td_customValues=null;
			td.td_customValueCount=0;
		}

		// Client Tag extension support (from Niles Ritter).
		static TIFFExtendProc TIFFextender=null;

		public static TIFFExtendProc TIFFSetTagExtender(TIFFExtendProc extender)
		{
			TIFFExtendProc prev=TIFFextender;
			TIFFextender=extender;
			return prev;
		}

		// Setup for a new directory. Should we automatically call
		// TIFFWriteDirectory() if the current one is dirty?
		//
		// The newly created directory will not exist on the file till
		// TIFFWriteDirectory(), TIFFFlush() or TIFFClose() is called.
		public static int TIFFCreateDirectory(TIFF tif)
		{
			TIFFDefaultDirectory(tif);
			tif.tif_diroff=0;
			tif.tif_nextdiroff=0;
			tif.tif_curoff=0;
			tif.tif_row=0xffffffff;
			tif.tif_curstrip=0xffffffff;

			return 0;
		}

		// Setup a default directory structure.
		static bool TIFFDefaultDirectory(TIFF tif)
		{
			TIFFDirectory td=tif.tif_dir;

			TIFFSetupFieldInfo(tif, tiffFieldInfo);

			//was _TIFFmemset(td, 0, sizeof (*td));
			for(int i=0; i<FIELD_SETLONGS; i++) td.td_fieldsset[i]=0;
			td.td_imagewidth=td.td_imagelength=0;
			td.td_subfiletype=0;
			td.td_compression=0;
			td.td_photometric=0;
			td.td_minsamplevalue=td.td_maxsamplevalue=0;
			td.td_sminsamplevalue=td.td_smaxsamplevalue=0;
			td.td_xresolution=td.td_yresolution=0;
			td.td_planarconfig=0;
			td.td_xposition=td.td_yposition=0;
			td.td_pagenumber[0]=td.td_pagenumber[1]=0;
			td.td_colormap[0]=td.td_colormap[1]=td.td_colormap[2]=null;
			td.td_halftonehints[0]=td.td_halftonehints[1]=0;
			td.td_extrasamples=0;
			td.td_sampleinfo=null;
			td.td_stripsperimage=0;
			td.td_nstrips=0;
			td.td_stripoffset=null;
			td.td_stripbytecount=null;
			td.td_nsubifd=0;
			td.td_subifd=null;
			td.td_refblackwhite=null;
			td.td_transferfunction[0]=null;
			td.td_transferfunction[1]=null;
			td.td_transferfunction[2]=null;
			td.td_inknameslen=0;
			td.td_inknames=null;
			td.td_customValueCount=0;
			td.td_customValues=null;

			td.td_fillorder=FILLORDER.MSB2LSB;
			td.td_bitspersample=1;
			td.td_threshholding=THRESHHOLD.BILEVEL;
			td.td_orientation=ORIENTATION.TOPLEFT;
			td.td_samplesperpixel=1;
			td.td_rowsperstrip=0xffffffff;
			td.td_tilewidth=td.td_tilelength=0;
			td.td_tiledepth=1;
			td.td_stripbytecountsorted=1; // Our own arrays always sorted.
			td.td_resolutionunit=RESUNIT.INCH;
			td.td_sampleformat=SAMPLEFORMAT.UINT;
			td.td_imagedepth=1;
			td.td_ycbcrsubsampling[0]=2;
			td.td_ycbcrsubsampling[1]=2;
			td.td_ycbcrpositioning=YCBCRPOSITION.CENTERED;
			tif.tif_postdecode=TIFFNoPostDecode;
			tif.tif_foundfield=null;
			tif.tif_tagmethods.vsetfield=_TIFFVSetField;
			tif.tif_tagmethods.vgetfield=_TIFFVGetField;
			tif.tif_tagmethods.printdir=null;

			// Give client code a chance to install their own
			// tag extensions & methods, prior to compression overloads.
			if(TIFFextender!=null) TIFFextender(tif);

			TIFFSetField(tif, TIFFTAG.COMPRESSION, COMPRESSION.NONE);

			// NB: The directory is marked dirty as a result of setting
			// up the default compression scheme. However, this really
			// isn't correct -- we want TIFF_DIRTYDIRECT to be set only
			// if the user does something. We could just do the setup
			// by hand, but it seems better to use the normal mechanism
			// (i.e. TIFFSetField).
			tif.tif_flags&=~TIF_FLAGS.TIFF_DIRTYDIRECT;

			// As per http://bugzilla.remotesensing.org/show_bug.cgi?id=19
			// we clear the ISTILED flag when setting up a new directory.
			// Should we also be clearing stuff like INSUBIFD?
			tif.tif_flags&=~TIF_FLAGS.TIFF_ISTILED;

			// Clear other directory-specific fields.
			tif.tif_tilesize=-1;
			tif.tif_scanlinesize=unchecked((uint)-1);

			return true;
		}

		static bool TIFFAdvanceDirectory(TIFF tif, ref uint nextdir, out uint off)
		{
			string module="TIFFAdvanceDirectory";
			ushort dircount;
			off=0;

			if(!SeekOK(tif, nextdir)||!ReadOK(tif, out dircount))
			{
				TIFFErrorExt(tif.tif_clientdata, module, "{0}: Error fetching directory count", tif.tif_name);
				return false;
			}

			if((tif.tif_flags&TIF_FLAGS.TIFF_SWAB)!=0) TIFFSwab(ref dircount);

			off=TIFFSeekFile(tif, dircount*12u, SEEK.CUR); // 12=sizeof(TIFFDirEntry)

			if(!ReadOK(tif, out nextdir))
			{
				TIFFErrorExt(tif.tif_clientdata, module, "{0}: Error fetching directory link", tif.tif_name);
				return false;
			}

			if((tif.tif_flags&TIF_FLAGS.TIFF_SWAB)!=0) TIFFSwab(ref nextdir);

			return true;
		}

		// Count the number of directories in a file.
		public static ushort TIFFNumberOfDirectories(TIFF tif)
		{
			uint nextdir=tif.tif_header.tiff_diroff;
			ushort n=0;

			uint @null;
			while(nextdir!=0&&TIFFAdvanceDirectory(tif, ref nextdir, out @null)) n++;

			return n;
		}

		// Set the n-th directory as the current directory.
		// NB: Directories are numbered starting at 0.
		public static bool TIFFSetDirectory(TIFF tif, ushort dirn)
		{
			uint nextdir;
			ushort n;

			nextdir=tif.tif_header.tiff_diroff;
			uint @null;
			for(n=dirn; n>0&&nextdir!=0; n--) if(!TIFFAdvanceDirectory(tif, ref nextdir, out @null)) return false;
			tif.tif_nextdiroff=nextdir;

			// Set curdir to the actual directory index. The
			// -1 is because TIFFReadDirectory will increment
			// tif_curdir after successfully reading the directory.
			tif.tif_curdir=(ushort)((dirn-n)-1);

			// Reset tif_dirnumber counter and start new list of seen directories.
			// We need this to prevent IFD loops.
			tif.tif_dirnumber=0;
			return TIFFReadDirectory(tif);
		}

		// Set the current directory to be the directory
		// located at the specified file offset. This interface
		// is used mainly to access directories linked with
		// the SubIFD tag (e.g. thumbnail images).
		public static bool TIFFSetSubDirectory(TIFF tif, uint diroff)
		{
			tif.tif_nextdiroff=diroff;

			// Reset tif_dirnumber counter and start new list of seen directories.
			// We need this to prevent IFD loops.
			tif.tif_dirnumber=0;
			return TIFFReadDirectory(tif);
		}

		// Return file offset of the current directory.
		public static uint TIFFCurrentDirOffset(TIFF tif)
		{
			return tif.tif_diroff;
		}

		// Return an indication of whether or not we are
		// at the last directory in the file.
		public static bool TIFFLastDirectory(TIFF tif)
		{
			return tif.tif_nextdiroff==0;
		}

		// Unlink the specified directory from the directory chain.
		public static bool TIFFUnlinkDirectory(TIFF tif, ushort dirn)
		{
			string module="TIFFUnlinkDirectory";

			if(tif.tif_mode==O.RDONLY)
			{
				TIFFErrorExt(tif.tif_clientdata, module, "Can not unlink directory in read-only file");
				return false;
			}

			// Go to the directory before the one we want
			// to unlink and nab the offset of the link
			// field we'll need to patch.
			uint nextdir=tif.tif_header.tiff_diroff;
			uint off=4;
			uint n;
			for(n=dirn-1u; n>0; n--)
			{
				if(nextdir==0)
				{
					TIFFErrorExt(tif.tif_clientdata, module, "Directory {0} does not exist", dirn);
					return false;
				}

				if(!TIFFAdvanceDirectory(tif, ref nextdir, out off)) return false;
			}

			// Advance to the directory to be unlinked and fetch
			// the offset of the directory that follows.
			uint @null;
			if(!TIFFAdvanceDirectory(tif, ref nextdir, out @null)) return false;

			// Go back and patch the link field of the preceding
			// directory to point to the offset of the directory
			// that follows.
			TIFFSeekFile(tif, off, SEEK.SET);

			if((tif.tif_flags&TIF_FLAGS.TIFF_SWAB)!=0) TIFFSwab(ref nextdir);

			if(!WriteOK(tif, nextdir))
			{
				TIFFErrorExt(tif.tif_clientdata, module, "Error writing directory link");
				return false;
			}

			// Leave directory state setup safely. We don't have
			// facilities for doing inserting and removing directories,
			// so it's safest to just invalidate everything. This
			// means that the caller can only append to the directory
			// chain.
			tif.tif_cleanup(tif);
			if((tif.tif_flags&TIF_FLAGS.TIFF_MYBUFFER)!=0&&tif.tif_rawdata!=null)
			{
				tif.tif_rawdata=null;
				tif.tif_rawcc=0;
			}

			tif.tif_flags&=~(TIF_FLAGS.TIFF_BEENWRITING|TIF_FLAGS.TIFF_BUFFERSETUP|TIF_FLAGS.TIFF_POSTENCODE);
			TIFFFreeDirectory(tif);
			TIFFDefaultDirectory(tif);
			tif.tif_diroff=0;			// force link on next write
			tif.tif_nextdiroff=0;		// next write must be at end
			tif.tif_curoff=0;
			tif.tif_row=0xffffffff;
			tif.tif_curstrip=0xffffffff;
			return true;
		}
	}
}
