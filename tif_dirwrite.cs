// tif_dirwrite.cs
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
// Directory Write Support Routines.

using System;
using System.Collections.Generic;
using System.Text;

namespace Free.Ports.LibTiff
{
	public static partial class libtiff
	{
		// Write the contents of the current directory
		// to the specified file. This routine doesn't
		// handle overwriting a directory with auxiliary
		// storage that's been changed.
		static bool TIFFWriteDirectory(TIFF tif, bool done)
		{
			if(tif.tif_mode==O.RDONLY) return true;

			// Clear write state so that subsequent images with
			// different characteristics get the right buffers
			// setup for them.
			if(done)
			{
				if((tif.tif_flags&TIF_FLAGS.TIFF_POSTENCODE)!=0)
				{
					tif.tif_flags&=~TIF_FLAGS.TIFF_POSTENCODE;
					if(!tif.tif_postencode(tif))
					{
						TIFFErrorExt(tif.tif_clientdata, tif.tif_name, "Error post-encoding before directory write");
						return false;
					}
				}
				tif.tif_close(tif); // shutdown encoder

				// Flush any data that might have been written
				// by the compression close+cleanup routines.
				if(tif.tif_rawcc>0&&(tif.tif_flags&TIF_FLAGS.TIFF_BEENWRITING)!=0&&!TIFFFlushData1(tif))
				{
					TIFFErrorExt(tif.tif_clientdata, tif.tif_name, "Error flushing data before directory write");
					return false;
				}

				tif.tif_rawdata=null;
				tif.tif_rawcc=0;
				tif.tif_rawdatasize=0;

				tif.tif_flags&=~(TIF_FLAGS.TIFF_BEENWRITING|TIF_FLAGS.TIFF_BUFFERSETUP);
			}

			TIFFDirectory td=tif.tif_dir;

			// Some Irfan view versions don't like RowsPerStrip set in tiled tifs
			if(isTiled(tif)) TIFFClrFieldBit(tif, FIELD.ROWSPERSTRIP);

			// Size the directory so that we can calculate
			// offsets for the data items that aren't kept
			// in-place in each field.
			uint nfields=0;
			uint[] fields=new uint[FIELD_SETLONGS];
			for(FIELD b=0; b<=(FIELD)FIELD_LAST; b++)
			{
				if(TIFFFieldSet(tif, b)&&b!=FIELD.CUSTOM) nfields+=(b<FIELD.SUBFILETYPE?2u:1u);
			}

			nfields+=(uint)td.td_customValueCount;
			int dirsize=(int)nfields*12; // 12: sizeof(TIFFDirEntry);

			List<TIFFDirEntry> dirs=null;
			try
			{
				dirs=new List<TIFFDirEntry>();
				for(int i=0; i<nfields; i++) dirs.Add(new TIFFDirEntry());
			}
			catch
			{
				TIFFErrorExt(tif.tif_clientdata, tif.tif_name, "Cannot write directory, out of space");
				return false;
			}

			// Directory hasn't been placed yet, put
			// it at the end of the file and link it
			// into the existing directory structure.
			if(tif.tif_diroff==0&&!TIFFLinkDirectory(tif)) return false;

			//tif.tif_dataoff=(uint)(tif.tif_diroff+sizeof(uint16)+dirsize+sizeof(toff_t));
			tif.tif_dataoff=(uint)(tif.tif_diroff+2+dirsize+4);

			if((tif.tif_dataoff&1)==1) tif.tif_dataoff++;

			TIFFSeekFile(tif, tif.tif_dataoff, SEEK.SET);
			tif.tif_curdir++;

			// Setup external form of directory
			// entries and write data items.
			td.td_fieldsset.CopyTo(fields, 0);

			// Write out ExtraSamples tag only if
			// extra samples are present in the data.
			if(FieldSet(fields, FIELD.EXTRASAMPLES)&&td.td_extrasamples==0)
			{
				ResetFieldBit(fields, FIELD.EXTRASAMPLES);
				nfields--;
				dirsize-=12; // 12: sizeof(TIFFDirEntry);
			} // XXX

			int dirsnumber=0;
			TIFFTAG tag;
			foreach(TIFFFieldInfo fip in tif.tif_fieldinfo)
			{
				TIFFDirEntry dir=(dirsnumber<dirs.Count)?dir=dirs[dirsnumber]:null;

				// For custom fields, we test to see if the custom field
				// is set or not. For normal fields, we just use the
				// FieldSet test.
				if(fip.field_bit==FIELD.CUSTOM)
				{
					bool is_set=false;

					for(int ci=0; ci<td.td_customValueCount; ci++) is_set|=(td.td_customValues[ci].info==fip);
					if(!is_set) continue;
				}
				else if(!FieldSet(fields, fip.field_bit)) continue;

				// Handle other fields.
				switch(fip.field_bit)
				{
					case FIELD.STRIPOFFSETS:

						// We use one field bit for both strip and tile
						// offsets, and so must be careful in selecting
						// the appropriate field descriptor (so that tags
						// are written in sorted order).
						tag=isTiled(tif)?TIFFTAG.TILEOFFSETS:TIFFTAG.STRIPOFFSETS;
						if(tag!=fip.field_tag) continue;

						dir.tdir_tag=(ushort)tag;
						dir.tdir_type=(ushort)TIFFDataType.TIFF_LONG;
						dir.tdir_count=td.td_nstrips;
						if(!TIFFWriteLongArray(tif, dir, td.td_stripoffset)) return false;
						break;
					case FIELD.STRIPBYTECOUNTS:
						// We use one field bit for both strip and tile
						// byte counts, and so must be careful in selecting
						// the appropriate field descriptor (so that tags
						// are written in sorted order).
						tag=isTiled(tif)?TIFFTAG.TILEBYTECOUNTS:TIFFTAG.STRIPBYTECOUNTS;
						if(tag!=fip.field_tag) continue;

						dir.tdir_tag=(ushort)tag;
						dir.tdir_type=(ushort)TIFFDataType.TIFF_LONG;
						dir.tdir_count=td.td_nstrips;
						if(!TIFFWriteLongArray(tif, dir, td.td_stripbytecount)) return false;
						break;
					case FIELD.ROWSPERSTRIP:
						TIFFSetupShortLong(tif, TIFFTAG.ROWSPERSTRIP, dir, td.td_rowsperstrip);
						break;
					case FIELD.COLORMAP:
						if(!TIFFWriteShortTable(tif, TIFFTAG.COLORMAP, dir, 3, td.td_colormap)) return false;
						break;
					case FIELD.IMAGEDIMENSIONS:
						TIFFSetupShortLong(tif, TIFFTAG.IMAGEWIDTH, dir, td.td_imagewidth);
						dirsnumber++;
						dir=dirs[dirsnumber];
						TIFFSetupShortLong(tif, TIFFTAG.IMAGELENGTH, dir, td.td_imagelength);
						break;
					case FIELD.TILEDIMENSIONS:
						TIFFSetupShortLong(tif, TIFFTAG.TILEWIDTH, dir, td.td_tilewidth);
						dirsnumber++;
						dir=dirs[dirsnumber];
						TIFFSetupShortLong(tif, TIFFTAG.TILELENGTH, dir, td.td_tilelength);
						break;
					case FIELD.COMPRESSION:
						TIFFSetupShort(tif, TIFFTAG.COMPRESSION, dir, (ushort)td.td_compression);
						break;
					case FIELD.PHOTOMETRIC:
						TIFFSetupShort(tif, TIFFTAG.PHOTOMETRIC, dir, (ushort)td.td_photometric);
						break;
					case FIELD.POSITION:
						if(!TIFFWriteRational(tif, TIFFDataType.TIFF_RATIONAL, TIFFTAG.XPOSITION, dir, td.td_xposition)) return false;
						dirsnumber++;
						dir=dirs[dirsnumber];
						if(!TIFFWriteRational(tif, TIFFDataType.TIFF_RATIONAL, TIFFTAG.YPOSITION, dir, td.td_yposition)) return false;
						break;
					case FIELD.RESOLUTION:
						if(!TIFFWriteRational(tif, TIFFDataType.TIFF_RATIONAL, TIFFTAG.XRESOLUTION, dir, td.td_xresolution)) return false;
						dirsnumber++;
						dir=dirs[dirsnumber];
						if(!TIFFWriteRational(tif, TIFFDataType.TIFF_RATIONAL, TIFFTAG.YRESOLUTION, dir, td.td_yresolution)) return false;
						break;
					case FIELD.BITSPERSAMPLE:
					case FIELD.MINSAMPLEVALUE:
					case FIELD.MAXSAMPLEVALUE:
					case FIELD.SAMPLEFORMAT:
						if(!TIFFWritePerSampleShorts(tif, fip.field_tag, dir)) return false;
						break;
					case FIELD.SMINSAMPLEVALUE:
					case FIELD.SMAXSAMPLEVALUE:
						if(!TIFFWritePerSampleAnys(tif, TIFFSampleToTagType(tif), fip.field_tag, dir)) return false;
						break;
					case FIELD.PAGENUMBER:
					case FIELD.HALFTONEHINTS:
					case FIELD.YCBCRSUBSAMPLING:
						if(!TIFFSetupShortPair(tif, fip.field_tag, dir)) return false;
						break;
					case FIELD.INKNAMES:
						if(!TIFFWriteInkNames(tif, dir)) return false;
						break;
					case FIELD.TRANSFERFUNCTION:
						if(!TIFFWriteTransferFunction(tif, dir)) return false;
						break;
					case FIELD.SUBIFD:
						// XXX: Always write this field using LONG type
						// for backward compatibility.
						dir.tdir_tag=(ushort)fip.field_tag;
						dir.tdir_type=(ushort)TIFFDataType.TIFF_LONG;
						dir.tdir_count=(uint)td.td_nsubifd;
						if(!TIFFWriteLongArray(tif, dir, td.td_subifd)) return false;

						// Total hack: if this directory includes a SubIFD
						// tag then force the next <n> directories to be
						// written as "sub directories" of this one. This
						// is used to write things like thumbnails and
						// image masks that one wants to keep out of the
						// normal directory linkage access mechanism.
						if(dir.tdir_count>0)
						{
							tif.tif_flags|=TIF_FLAGS.TIFF_INSUBIFD;
							tif.tif_nsubifd=(ushort)dir.tdir_count;
							if(dir.tdir_count>1) tif.tif_subifdoff=dir.tdir_offset;
							//was else tif.tif_subifdoff=(uint)(tif.tif_diroff+2+((char*)&dir.tdir_offset-data));
							else tif.tif_subifdoff=(uint)(tif.tif_diroff+2+12*dirsnumber-4);
						}
						break;
					default:
						// XXX: Should be fixed and removed.
						if(fip.field_tag==TIFFTAG.DOTRANGE)
						{
							if(!TIFFSetupShortPair(tif, fip.field_tag, dir)) return false;
						}
						else if(!TIFFWriteNormalTag(tif, dir, fip)) return false;
						break;
				}
				dirsnumber++;

				if(fip.field_bit!=FIELD.CUSTOM) ResetFieldBit(fields, fip.field_bit);
			}

			// Write directory.
			ushort dircount=(ushort)nfields;
			uint diroff=tif.tif_nextdiroff;
			if((tif.tif_flags&TIF_FLAGS.TIFF_SWAB)!=0)
			{
				// The file's byte order is opposite to the
				// native machine architecture. We overwrite
				// the directory information with impunity
				// because it'll be released below after we
				// write it to the file. Note that all the
				// other tag construction routines assume that
				// we do this byte-swapping; i.e. they only
				// byte-swap indirect data.
				foreach(TIFFDirEntry dir in dirs)
				{
					TIFFSwab(ref dir.tdir_tag);
					TIFFSwab(ref dir.tdir_type);
					TIFFSwab(ref dir.tdir_count);
					TIFFSwab(ref dir.tdir_offset);
				}
				dircount=(ushort)nfields;
				TIFFSwab(ref dircount);
				TIFFSwab(ref diroff);
			}

			TIFFSeekFile(tif, tif.tif_diroff, SEEK.SET);
			if(!WriteOK(tif, dircount))
			{
				TIFFErrorExt(tif.tif_clientdata, tif.tif_name, "Error writing directory count");
				return false;
			}

			if(!WriteOK(tif, dirs, (ushort)dirsize))
			{
				TIFFErrorExt(tif.tif_clientdata, tif.tif_name, "Error writing directory contents");
				return false;
			}

			if(!WriteOK(tif, diroff))
			{
				TIFFErrorExt(tif.tif_clientdata, tif.tif_name, "Error writing directory link");
				return false;
			}

			if(done)
			{
				TIFFFreeDirectory(tif);
				tif.tif_flags&=~TIF_FLAGS.TIFF_DIRTYDIRECT;
				tif.tif_cleanup(tif);

				// Reset directory-related state for subsequent directories.
				TIFFCreateDirectory(tif);
			}

			return true;
		}

		public static bool TIFFWriteDirectory(TIFF tif)
		{
			return TIFFWriteDirectory(tif, true);
		}

		// Similar to TIFFWriteDirectory(), writes the directory out
		// but leaves all data structures in memory so that it can be
		// written again. This will make a partially written TIFF file
		// readable before it is successfully completed/closed.
		public static bool TIFFCheckpointDirectory(TIFF tif)
		{
			// Setup the strips arrays, if they haven't already been.
			if(tif.tif_dir.td_stripoffset==null) TIFFSetupStrips(tif);

			bool rc=TIFFWriteDirectory(tif, false);
			TIFFSetWriteOffset(tif, TIFFSeekFile(tif, 0, SEEK.END));
			return rc;
		}

		public static bool TIFFWriteCustomDirectory(TIFF tif, ref uint diroff)
		{
			if(tif.tif_mode==O.RDONLY) return true;

			TIFFDirectory td=tif.tif_dir;

			// Size the directory so that we can calculate
			// offsets for the data items that aren't kept
			// in-place in each field.
			uint nfields=0;
			uint[] fields=new uint[FIELD_SETLONGS];
			for(FIELD b=0; b<=(FIELD)FIELD_LAST; b++)
			{
				if(TIFFFieldSet(tif, b)&&b!=FIELD.CUSTOM) nfields+=(b<FIELD.SUBFILETYPE?2u:1u);
			}

			nfields+=(uint)td.td_customValueCount;
			int dirsize=(int)nfields*12; // 12: sizeof(TIFFDirEntry);

			List<TIFFDirEntry> dirs=null;
			try
			{
				dirs=new List<TIFFDirEntry>();
				for(int i=0; i<nfields; i++) dirs.Add(new TIFFDirEntry());
			}
			catch
			{
				TIFFErrorExt(tif.tif_clientdata, tif.tif_name, "Cannot write directory, out of space");
				return false;
			}
			
			// Put the directory at the end of the file.
			tif.tif_diroff=(uint)((TIFFSeekFile(tif, 0, SEEK.END)+1)&~1);

			//tif.tif_dataoff=(uint)(tif.tif_diroff+sizeof(uint16)+dirsize+sizeof(toff_t));
			tif.tif_dataoff=(uint)(tif.tif_diroff+2+dirsize+4);

			if((tif.tif_dataoff&1)==1) tif.tif_dataoff++;

			TIFFSeekFile(tif, tif.tif_dataoff, SEEK.SET);

			// Setup external form of directory
			// entries and write data items.
			td.td_fieldsset.CopyTo(fields, 0);

			int dirsnumber=0;
			foreach(TIFFFieldInfo fip in tif.tif_fieldinfo)
			{
				TIFFDirEntry dir=(dirsnumber<dirs.Count)?dir=dirs[dirsnumber]:null;

				// For custom fields, we test to see if the custom field
				// is set or not. For normal fields, we just use the
				// FieldSet test.
				if(fip.field_bit==FIELD.CUSTOM)
				{
					bool is_set=false;

					for(int ci=0; ci<td.td_customValueCount; ci++) is_set|=td.td_customValues[ci].info==fip;
					if(!is_set) continue;
				}
				else if(!FieldSet(fields, fip.field_bit)) continue;

				dirsnumber++;

				if(fip.field_bit!=FIELD.CUSTOM) ResetFieldBit(fields, fip.field_bit);
			}

			// Write directory.
			ushort dircount=(ushort)nfields;
			diroff=tif.tif_nextdiroff;
			if((tif.tif_flags&TIF_FLAGS.TIFF_SWAB)!=0)
			{
				// The file's byte order is opposite to the
				// native machine architecture. We overwrite
				// the directory information with impunity
				// because it'll be released below after we
				// write it to the file. Note that all the
				// other tag construction routines assume that
				// we do this byte-swapping; i.e. they only
				// byte-swap indirect data.
				foreach(TIFFDirEntry dir in dirs)
				{
					TIFFSwab(ref dir.tdir_tag);
					TIFFSwab(ref dir.tdir_type);
					TIFFSwab(ref dir.tdir_count);
					TIFFSwab(ref dir.tdir_offset);
				}

				dircount=(ushort)nfields;
				TIFFSwab(ref dircount);
				TIFFSwab(ref diroff);
			}

			TIFFSeekFile(tif, tif.tif_diroff, SEEK.SET);
			if(!WriteOK(tif, dircount))
			{
				TIFFErrorExt(tif.tif_clientdata, tif.tif_name, "Error writing directory count");
				return false;
			}

			if(!WriteOK(tif, dirs, (ushort)dirsize))
			{
				TIFFErrorExt(tif.tif_clientdata, tif.tif_name, "Error writing directory contents");
				return false;
			}

			if(!WriteOK(tif, diroff))
			{
				TIFFErrorExt(tif.tif_clientdata, tif.tif_name, "Error writing directory link");
				return false;
			}

			return true;
		}

		// Process tags that are not special cased.
		static bool TIFFWriteNormalTag(TIFF tif, TIFFDirEntry dir, TIFFFieldInfo fip)
		{
			uint wc=(uint)fip.field_writecount;

			dir.tdir_tag=(ushort)fip.field_tag;
			dir.tdir_type=(ushort)fip.field_type;
			dir.tdir_count=wc;

			switch(fip.field_type)
			{
				case TIFFDataType.TIFF_SHORT:
					{
						object[] ap=new object[2];
						TIFFGetField(tif, fip.field_tag, ap);

						if(fip.field_passcount)
						{
							// Assume TIFF_VARIABLE
							dir.tdir_count=wc=__GetAsUint(ap, 0);
							ushort[] wp=ap[1] as ushort[];
							if(wp==null) return false;

							if(!TIFFWriteShortArray(tif, dir, wp)) return false;
						}
						else
						{
							if(wc==1)
							{
								ushort sv=__GetAsUshort(ap, 0);
								//was dir.tdir_offset=TIFFInsertData(tif, dir.tdir_type, sv);
								dir.tdir_offset=((uint)(tif.tif_header.tiff_magic==TIFF_BIGENDIAN?(sv&tif_typemask[dir.tdir_type])<<tif_typeshift[dir.tdir_type]:sv&tif_typemask[dir.tdir_type]));
							}
							else
							{
								ushort[] wp=ap[0] as ushort[];
								if(wp==null) return false;

								if(!TIFFWriteShortArray(tif, dir, wp)) return false;
							}
						}
					}
					break;
				case TIFFDataType.TIFF_SSHORT:
					{
						object[] ap=new object[2];
						TIFFGetField(tif, fip.field_tag, ap);

						if(fip.field_passcount)
						{
							// Assume TIFF_VARIABLE
							dir.tdir_count=wc=__GetAsUint(ap, 0);
							short[] wp=ap[1] as short[];
							if(wp==null) return false;

							if(!TIFFWriteShortArray(tif, dir, wp)) return false;
						}
						else
						{
							if(wc==1)
							{
								short sv=__GetAsShort(ap, 0);
								//was dir.tdir_offset=TIFFInsertData(tif, dir.tdir_type, sv);
								dir.tdir_offset=((uint)(tif.tif_header.tiff_magic==TIFF_BIGENDIAN?(sv&tif_typemask[dir.tdir_type])<<tif_typeshift[dir.tdir_type]:sv&tif_typemask[dir.tdir_type]));
							}
							else
							{
								short[] wp=ap[0] as short[];
								if(wp==null) return false;

								if(!TIFFWriteShortArray(tif, dir, wp)) return false;
							}
						}
					}
					break;
				case TIFFDataType.TIFF_SLONG:
					{
						object[] ap=new object[2];
						TIFFGetField(tif, fip.field_tag, ap);

						if(fip.field_passcount)
						{
							// Assume TIFF_VARIABLE
							dir.tdir_count=wc=__GetAsUint(ap, 0);
							int[] lp=ap[1] as int[];
							if(lp==null) return false;

							if(!TIFFWriteLongArray(tif, dir, lp)) return false;
						}
						else
						{
							if(wc==1)
							{
								// XXX handle LONG=>SHORT conversion
								dir.tdir_offset=__GetAsUint(ap, 0);
							}
							else
							{
								int[] lp=ap[0] as int[];
								if(lp==null) return false;

								if(!TIFFWriteLongArray(tif, dir, lp)) return false;
							}
						}
					}
					break;
				case TIFFDataType.TIFF_LONG:
				case TIFFDataType.TIFF_IFD:
					{
						object[] ap=new object[2];
						TIFFGetField(tif, fip.field_tag, ap);

						if(fip.field_passcount)
						{
							// Assume TIFF_VARIABLE
							dir.tdir_count=wc=__GetAsUint(ap, 0);
							uint[] lp=ap[1] as uint[];
							if(lp==null) return false;

							if(!TIFFWriteLongArray(tif, dir, lp)) return false;
						}
						else
						{
							if(wc==1)
							{
								// XXX handle LONG=>SHORT conversion
								dir.tdir_offset=__GetAsUint(ap, 0);
							}
							else
							{
								uint[] lp=ap[0] as uint[];
								if(lp==null) return false;

								if(!TIFFWriteLongArray(tif, dir, lp)) return false;
							}
						}
					}
					break;
				case TIFFDataType.TIFF_RATIONAL:
				case TIFFDataType.TIFF_SRATIONAL:
					{
						object[] ap=new object[2];
						TIFFGetField(tif, fip.field_tag, ap);

						if(fip.field_passcount)
						{
							// Assume TIFF_VARIABLE
							dir.tdir_count=wc=__GetAsUint(ap, 0);
							double[] fp=ap[1] as double[];
							if(fp==null) return false;

							if(!TIFFWriteRationalArray(tif, dir, fp)) return false;
						}
						else
						{
							if(wc==1)
							{
								double fv=__GetAsDouble(ap, 0);
								if(!TIFFWriteRational(tif, fip.field_type, fip.field_tag, dir, fv)) return false;
							}
							else
							{
								double[] fp=ap[0] as double[];
								if(fp==null) return false;

								if(!TIFFWriteRationalArray(tif, dir, fp)) return false;
							}
						}
					}
					break;
				case TIFFDataType.TIFF_FLOAT:
					{
						object[] ap=new object[2];
						TIFFGetField(tif, fip.field_tag, ap);

						if(fip.field_passcount)
						{
							// Assume TIFF_VARIABLE
							dir.tdir_count=wc=__GetAsUint(ap, 0);
							float[] fp=ap[1] as float[];
							if(fp==null) return false;

							if(!TIFFWriteFloatArray(tif, dir, fp)) return false;
						}
						else
						{
							if(wc==1)
							{
								float[] fv=new float[1];
								fv[0]=__GetAsFloat(ap, 0);
								if(!TIFFWriteFloatArray(tif, dir, fv)) return false;
							}
							else
							{
								float[] fp=ap[0] as float[];
								if(fp==null) return false;

								if(!TIFFWriteFloatArray(tif, dir, fp)) return false;
							}
						}
					}
					break;
				case TIFFDataType.TIFF_DOUBLE:
					{
						object[] ap=new object[2];
						TIFFGetField(tif, fip.field_tag, ap);

						if(fip.field_passcount)
						{
							// Assume TIFF_VARIABLE
							dir.tdir_count=wc=__GetAsUint(ap, 0);
							double[] dp=ap[1] as double[];
							if(dp==null) return false;

							if(!TIFFWriteDoubleArray(tif, dir, dp)) return false;
						}
						else
						{
							if(wc==1)
							{
								double[] dv=new double[1];
								dv[0]=__GetAsDouble(ap, 0);

								if(!TIFFWriteDoubleArray(tif, dir, dv)) return false;
							}
							else
							{
								double[] dp=ap[0] as double[];
								if(dp==null) return false;

								if(!TIFFWriteDoubleArray(tif, dir, dp)) return false;
							}
						}
					}
					break;
				case TIFFDataType.TIFF_ASCII:
					{
						object[] ap=new object[2];
						TIFFGetField(tif, fip.field_tag, ap);

						string cp;
						if(fip.field_passcount)
						{
							// Assume TIFF_VARIABLE
							wc=__GetAsUint(ap, 0);
							cp=ap[1] as string;
							if(cp==null) return false;
						}
						else
						{
							cp=ap[0] as string;
							if(cp==null) return false;
						}

						cp=cp.TrimEnd('\0');
						cp+='\0';
						dir.tdir_count=(uint)cp.Length;
						if(!TIFFWriteByteArray(tif, dir, Encoding.ASCII.GetBytes(cp))) return false;
					}
					break;
				case TIFFDataType.TIFF_BYTE:
					{
						object[] ap=new object[2];
						TIFFGetField(tif, fip.field_tag, ap);

						if(fip.field_passcount)
						{
							// Assume TIFF_VARIABLE
							dir.tdir_count=wc=__GetAsUint(ap, 0);
							byte[] cp=ap[1] as byte[];
							if(cp==null) return false;

							if(!TIFFWriteByteArray(tif, dir, cp)) return false;
						}
						else
						{
							if(wc==1)
							{
								byte[] cv=new byte[1];
								cv[0]=__GetAsByte(ap, 0);

								if(!TIFFWriteByteArray(tif, dir, cv)) return false;
							}
							else
							{
								byte[] cp=ap[0] as byte[];
								if(cp==null) return false;

								if(!TIFFWriteByteArray(tif, dir, cp)) return false;
							}
						}
					}
					break;
				case TIFFDataType.TIFF_SBYTE:
					{
						object[] ap=new object[2];
						TIFFGetField(tif, fip.field_tag, ap);

						if(fip.field_passcount)
						{
							// Assume TIFF_VARIABLE
							dir.tdir_count=wc=__GetAsUint(ap, 0);
							sbyte[] cp=ap[1] as sbyte[];
							if(cp==null) return false;

							if(!TIFFWriteByteArray(tif, dir, cp)) return false;
						}
						else
						{
							if(wc==1)
							{
								sbyte[] cv=new sbyte[1];
								cv[0]=__GetAsSbyte(ap, 0);

								if(!TIFFWriteByteArray(tif, dir, cv)) return false;
							}
							else
							{
								sbyte[] cp=ap[0] as sbyte[];
								if(cp==null) return false;

								if(!TIFFWriteByteArray(tif, dir, cp)) return false;
							}
						}
					}
					break;
				case TIFFDataType.TIFF_UNDEFINED:
					{
						object[] ap=new object[2];
						TIFFGetField(tif, fip.field_tag, ap);

						byte[] cp;
						if((int)wc==TIFF_VARIABLE)
						{
							// Assume TIFF_VARIABLE
							dir.tdir_count=wc=__GetAsUint(ap, 0);
							cp=ap[1] as byte[];
							if(cp==null) return false;
						}
						else
						{
							cp=ap[0] as byte[];
							if(cp==null) return false;
						}

						if(!TIFFWriteByteArray(tif, dir, cp)) return false;
					}
					break;
				case TIFFDataType.TIFF_NOTYPE:
					break;
			}
			return true;
		}

		// Setup a directory entry with either a SHORT
		// or LONG type according to the value.
		static void TIFFSetupShortLong(TIFF tif, TIFFTAG tag, TIFFDirEntry dir, uint v)
		{
			dir.tdir_tag=(ushort)tag;
			dir.tdir_count=1;
			if(v>0xffff)
			{
				dir.tdir_type=(ushort)TIFFDataType.TIFF_LONG;
				dir.tdir_offset=v;
			}
			else
			{
				dir.tdir_type=(ushort)TIFFDataType.TIFF_SHORT;
				//was dir.tdir_offset=TIFFInsertData(tif, (int) TIFF_SHORT, v);
				dir.tdir_offset=(uint)(tif.tif_header.tiff_magic==TIFF_BIGENDIAN?(v&tif_typemask[(int)TIFFDataType.TIFF_SHORT])<<tif_typeshift[(int)TIFFDataType.TIFF_SHORT]:v&tif_typemask[(int)TIFFDataType.TIFF_SHORT]);
			}
		}

		// Setup a SHORT directory entry
		static void TIFFSetupShort(TIFF tif, TIFFTAG tag, TIFFDirEntry dir, ushort v)
		{
			dir.tdir_tag=(ushort)tag;
			dir.tdir_count=1;
			dir.tdir_type=(ushort)TIFFDataType.TIFF_SHORT;
			//was dir.tdir_offset=TIFFInsertData(tif, (int) TIFF_SHORT, v);
			dir.tdir_offset=(uint)(tif.tif_header.tiff_magic==TIFF_BIGENDIAN?(v&tif_typemask[(int)TIFFDataType.TIFF_SHORT])<<tif_typeshift[(int)TIFFDataType.TIFF_SHORT]:v&tif_typemask[(int)TIFFDataType.TIFF_SHORT]);
		}

		// Setup a directory entry that references a
		// samples/pixel array of SHORT values and
		// (potentially) write the associated indirect
		// values.
		static bool TIFFWritePerSampleShorts(TIFF tif, TIFFTAG tag, TIFFDirEntry dir)
		{
			ushort samples=tif.tif_dir.td_samplesperpixel;
			ushort[] w=null;
			object[] ap=null;

			try
			{
				w=new ushort[samples];
				ap=new object[samples];
			}
			catch
			{
				TIFFErrorExt(tif.tif_clientdata, tif.tif_name, "No space to write per-sample shorts");
				return false;
			}

			TIFFGetField(tif, tag, ap);
			for(int i=0; i<samples; i++) w[i]=__GetAsUshort(ap, 0);

			dir.tdir_tag=(ushort)tag;
			dir.tdir_type=(ushort)TIFFDataType.TIFF_SHORT;
			dir.tdir_count=samples;

			return TIFFWriteShortArray(tif, dir, w);
		}

		// Setup a directory entry that references a samples/pixel array of "type"
		// values and (potentially) write the associated indirect values. The source
		// data from TIFFGetField() for the specified tag must be returned as double.
		static bool TIFFWritePerSampleAnys(TIFF tif, TIFFDataType type, TIFFTAG tag, TIFFDirEntry dir)
		{
			ushort samples=tif.tif_dir.td_samplesperpixel;
			double[] w=null;
			object[] ap=null;

			try
			{
				w=new double[samples];
				ap=new object[samples];
			}
			catch
			{
				TIFFErrorExt(tif.tif_clientdata, tif.tif_name, "No space to write per-sample shorts");
				return false;
			}

			TIFFGetField(tif, tag, ap);
			for(int i=0; i<samples; i++) w[i]=__GetAsDouble(ap, i);

			return TIFFWriteAnyArray(tif, type, tag, dir, samples, w);
		}

		// Setup a pair of shorts that are returned by
		// value, rather than as a reference to an array.
		static bool TIFFSetupShortPair(TIFF tif, TIFFTAG tag, TIFFDirEntry dir)
		{
			ushort[] v=null;
			object[] ap=null;

			try
			{
				v=new ushort[2];
				ap=new object[2];
			}
			catch
			{
				TIFFErrorExt(tif.tif_clientdata, tif.tif_name, "No space to write per-sample shorts");
				return false;
			}

			TIFFGetField(tif, tag, ap);
			v[0]=__GetAsUshort(ap, 0);
			v[1]=__GetAsUshort(ap, 1);

			dir.tdir_tag=(ushort)tag;
			dir.tdir_type=(ushort)TIFFDataType.TIFF_SHORT;
			dir.tdir_count=2;
			return TIFFWriteShortArray(tif, dir, v);
		}

		// Setup a directory entry for an NxM table of shorts,
		// where M is known to be 2**bitspersample, and write
		// the associated indirect data.
		static bool TIFFWriteShortTable(TIFF tif, TIFFTAG tag, TIFFDirEntry dir, uint n, ushort[][] table)
		{
			dir.tdir_tag=(ushort)tag;
			dir.tdir_type=(ushort)TIFFDataType.TIFF_SHORT;

			// XXX -- yech, fool TIFFWriteData
			dir.tdir_count=(uint)(1<<tif.tif_dir.td_bitspersample);
			uint off=tif.tif_dataoff;
			byte[] buf=new byte[dir.tdir_count*2];
			for(uint i=0; i<n; i++)
			{
				for(int a=0; a<dir.tdir_count; a++) BitConverter.GetBytes(table[i][a]).CopyTo(buf, a*2);
				if(!TIFFWriteData(tif, dir, buf)) return false;
			}

			dir.tdir_count*=n;
			dir.tdir_offset=off;
			return true;
		}

		// Write/copy data associated with an ASCII or opaque tag value.
		static bool TIFFWriteByteArray(TIFF tif, TIFFDirEntry dir, byte[] cp)
		{
			if(dir.tdir_count<=4)
			{
				//was _TIFFmemcpy(&dir.tdir_offset, cp, dir.tdir_count);
				dir.tdir_offset=0;

				if(tif.tif_header.tiff_magic==TIFF_BIGENDIAN)
				{
					dir.tdir_offset=(uint)cp[0]<<24;
					if(dir.tdir_count>=2) dir.tdir_offset|=(uint)cp[1]<<16;
					if(dir.tdir_count>=3) dir.tdir_offset|=(uint)cp[2]<<8;
					if(dir.tdir_count==4) dir.tdir_offset|=cp[3];
				}
				else
				{
					switch(dir.tdir_count)
					{
						case 4: dir.tdir_offset=cp[3]; dir.tdir_offset<<=8; goto case 3;
						case 3: dir.tdir_offset+=cp[2]; dir.tdir_offset<<=8; goto case 2;
						case 2: dir.tdir_offset+=cp[1]; dir.tdir_offset<<=8; goto case 1;
						case 1: dir.tdir_offset+=cp[0]; break;
					}
				}
				return true;
			}

				return TIFFWriteData(tif, dir, cp);
		}

		static bool TIFFWriteByteArray(TIFF tif, TIFFDirEntry dir, sbyte[] cp)
		{
			byte[] buf=new byte[dir.tdir_count];
			for(int i=0; i<dir.tdir_count; i++) buf[i]=(byte)cp[i];
			return TIFFWriteByteArray(tif, dir, buf);
		}

		// Setup a directory entry of an array of SHORT
		// and write the associated indirect values.
		static bool TIFFWriteShortArray(TIFF tif, TIFFDirEntry dir, ushort[] v)
		{
			if(dir.tdir_count<=2)
			{
				if(tif.tif_header.tiff_magic==TIFF_BIGENDIAN)
				{
					dir.tdir_offset=(uint)(((int)v[0])<<16);
					if(dir.tdir_count==2) dir.tdir_offset=dir.tdir_offset|(uint)v[1];
				}
				else
				{
					dir.tdir_offset=v[0];
					if(dir.tdir_count==2) dir.tdir_offset=dir.tdir_offset|((uint)v[1])<<16;
				}
				return true;
			}

			byte[] buf=new byte[dir.tdir_count*2];
			for(int i=0; i<dir.tdir_count; i++) BitConverter.GetBytes(v[i]).CopyTo(buf, i*2);
			return TIFFWriteData(tif, dir, buf);
		}

		// Setup a directory entry of an array of
		// SSHORT and write the associated indirect values.
		static bool TIFFWriteShortArray(TIFF tif, TIFFDirEntry dir, short[] v)
		{
			if(dir.tdir_count<=2)
			{
				if(tif.tif_header.tiff_magic==TIFF_BIGENDIAN)
				{
					dir.tdir_offset=(uint)(((int)v[0])<<16);
					if(dir.tdir_count==2) dir.tdir_offset=dir.tdir_offset|(uint)(ushort)v[1];
				}
				else
				{
					dir.tdir_offset=(ushort)v[0];
					if(dir.tdir_count==2) dir.tdir_offset=dir.tdir_offset|((uint)v[1])<<16;
				}
				return true;
			}

			byte[] buf=new byte[dir.tdir_count*2];
			for(int i=0; i<dir.tdir_count; i++) BitConverter.GetBytes(v[i]).CopyTo(buf, i*2);
			return TIFFWriteData(tif, dir, buf);
		}

		// Setup a directory entry of an array of LONG
		// and write the associated indirect values.
		static bool TIFFWriteLongArray(TIFF tif, TIFFDirEntry dir, uint[] v)
		{
			if(dir.tdir_count==1)
			{
				dir.tdir_offset=v[0];
				return true;
			}

			byte[] buf=new byte[dir.tdir_count*4];
			for(int i=0; i<dir.tdir_count; i++) BitConverter.GetBytes(v[i]).CopyTo(buf, i*4);
			return TIFFWriteData(tif, dir, buf);
		}

		// Setup a directory entry of an array of
		// SLONG and write the associated indirect values.
		static bool TIFFWriteLongArray(TIFF tif, TIFFDirEntry dir, int[] v)
		{
			if(dir.tdir_count==1)
			{
				dir.tdir_offset=(uint)v[0];
				return true;
			}

			byte[] buf=new byte[dir.tdir_count*4];
			for(int i=0; i<dir.tdir_count; i++) BitConverter.GetBytes(v[i]).CopyTo(buf, i*4);
			return TIFFWriteData(tif, dir, buf);
		}

		// Setup a directory entry of an array of RATIONAL
		// or SRATIONAL and write the associated indirect values.
		static bool TIFFWriteRational(TIFF tif, TIFFDataType type, TIFFTAG tag, TIFFDirEntry dir, double fv)
		{
			dir.tdir_tag=(ushort)tag;
			dir.tdir_type=(ushort)type;
			dir.tdir_count=1;

			byte[] t=null;

			try
			{
				t=new byte[8];
			}
			catch
			{
				TIFFErrorExt(tif.tif_clientdata, tif.tif_name, "No space to write RATIONAL array");
				return false;
			}

			int sign=1;
			uint den=1;

			if(fv<0)
			{
				if(type==TIFFDataType.TIFF_RATIONAL)
				{
					TIFFWarningExt(tif.tif_clientdata, tif.tif_name, "\"{0}\": Information lost writing value ({1}) as (unsigned) RATIONAL", TIFFFieldWithTag(tif, tag).field_name, fv);
					fv=0;
				}
				else
				{
					fv=-fv;
					sign=-1;
				}
			}

			if(fv>0)
			{
				while(fv<(1<<(31-3))&&den<(1<<(31-3)))
				{
					fv*=1<<3;
					den*=1<<3;
				}
			}

			BitConverter.GetBytes((uint)(sign*(fv+0.5))).CopyTo(t, 0);
			BitConverter.GetBytes(den).CopyTo(t, 4);

			return TIFFWriteData(tif, dir, t);
		}

		// Setup a directory entry of an array of RATIONAL
		// or SRATIONAL and write the associated indirect values.
		static bool TIFFWriteRationalArray(TIFF tif, TIFFDirEntry dir, double[] v)
		{
			byte[] t=null;

			try
			{
				t=new byte[8*dir.tdir_count];
			}
			catch
			{
				TIFFErrorExt(tif.tif_clientdata, tif.tif_name, "No space to write RATIONAL array");
				return false;
			}

			for(uint i=0; i<dir.tdir_count; i++)
			{
				double fv=v[i];
				int sign=1;
				uint den=1;

				if(fv<0)
				{
					if(dir.tdir_type==(ushort)TIFFDataType.TIFF_RATIONAL)
					{
						TIFFWarningExt(tif.tif_clientdata, tif.tif_name, "\"{0}\": Information lost writing value ({1}) as (unsigned) RATIONAL", TIFFFieldWithTag(tif, (TIFFTAG)dir.tdir_tag).field_name, fv);
						fv=0;
					}
					else
					{
						fv=-fv;
						sign=-1;
					}
				}

				if(fv>0)
				{
					while(fv<(1<<(31-3))&&den<(1<<(31-3)))
					{
						fv*=1<<3;
						den*=1<<3;
					}
				}

				BitConverter.GetBytes((uint)(sign*(fv+0.5))).CopyTo(t, i*8);
				BitConverter.GetBytes(den).CopyTo(t, i*8+4);
			}

			return TIFFWriteData(tif, dir, t);
		}

		static bool TIFFWriteFloatArray(TIFF tif, TIFFDirEntry dir, float[] v)
		{
			byte[] buf=new byte[dir.tdir_count*4];
			for(int i=0; i<dir.tdir_count; i++) BitConverter.GetBytes(v[i]).CopyTo(buf, i*4);

			if(dir.tdir_count==1)
			{
				dir.tdir_offset=((uint)buf[3]<<24)+((uint)buf[2]<<16)+((uint)buf[1]<<8)+(uint)buf[0];
				return true;
			}

			return TIFFWriteData(tif, dir, buf);
		}

		static bool TIFFWriteDoubleArray(TIFF tif, TIFFDirEntry dir, double[] v)
		{
			byte[] buf=new byte[dir.tdir_count*8];
			for(int i=0; i<dir.tdir_count; i++) BitConverter.GetBytes(v[i]).CopyTo(buf, i*8);
			return TIFFWriteData(tif, dir, buf);
		}

		// Write an array of "type" values for a specified tag (i.e. this is a tag
		// which is allowed to have different types, e.g. SMaxSampleType).
		// Internally the data values are represented as double since a double can
		// hold any of the TIFF tag types (yes, this should really be an abstract
		// type tany_t for portability). The data is converted into the specified
		// type in a temporary buffer and then handed off to the appropriate array
		// writer.
		static bool TIFFWriteAnyArray(TIFF tif, TIFFDataType type, TIFFTAG tag, TIFFDirEntry dir, uint n, double[] v)
		{
			dir.tdir_tag=(ushort)tag;
			dir.tdir_type=(ushort)type;
			dir.tdir_count=n;

			switch(type)
			{
				case TIFFDataType.TIFF_BYTE:
					{
						byte[] bp=null;
						try
						{
							bp=new byte[n];
						}
						catch
						{
							TIFFErrorExt(tif.tif_clientdata, tif.tif_name, "No space to write array");
							return false;
						}
						for(uint i=0; i<n; i++) bp[i]=(byte)v[i];
						return TIFFWriteByteArray(tif, dir, bp);
					}
				case TIFFDataType.TIFF_SBYTE:
					{
						sbyte[] bp=null;
						try
						{
							bp=new sbyte[n];
						}
						catch
						{
							TIFFErrorExt(tif.tif_clientdata, tif.tif_name, "No space to write array");
							return false;
						}
						for(uint i=0; i<n; i++) bp[i]=(sbyte)v[i];
						return TIFFWriteByteArray(tif, dir, bp);
					}
				case TIFFDataType.TIFF_SHORT:
					{
						ushort[] bp=null;
						try
						{
							bp=new ushort[n];
						}
						catch
						{
							TIFFErrorExt(tif.tif_clientdata, tif.tif_name, "No space to write array");
							return false;
						}
						for(uint i=0; i<n; i++) bp[i]=(ushort)v[i];
						return TIFFWriteShortArray(tif, dir, bp);
					}
				case TIFFDataType.TIFF_SSHORT:
					{
						short[] bp=null;
						try
						{
							bp=new short[n];
						}
						catch
						{
							TIFFErrorExt(tif.tif_clientdata, tif.tif_name, "No space to write array");
							return false;
						}
						for(uint i=0; i<n; i++) bp[i]=(short)v[i];
						return TIFFWriteShortArray(tif, dir, bp);
					}
				case TIFFDataType.TIFF_LONG:
					{
						uint[] bp=null;
						try
						{
							bp=new uint[n];
						}
						catch
						{
							TIFFErrorExt(tif.tif_clientdata, tif.tif_name, "No space to write array");
							return false;
						}
						for(uint i=0; i<n; i++) bp[i]=(uint)v[i];
						return TIFFWriteLongArray(tif, dir, bp);
					}
				case TIFFDataType.TIFF_SLONG:
					{
						int[] bp=null;
						try
						{
							bp=new int[n];
						}
						catch
						{
							TIFFErrorExt(tif.tif_clientdata, tif.tif_name, "No space to write array");
							return false;
						}
						for(uint i=0; i<n; i++) bp[i]=(int)v[i];
						return TIFFWriteLongArray(tif, dir, bp);
					}
				case TIFFDataType.TIFF_FLOAT:
					{
						float[] bp=null;
						try
						{
							bp=new float[n];
						}
						catch
						{
							TIFFErrorExt(tif.tif_clientdata, tif.tif_name, "No space to write array");
							return false;
						}
						for(uint i=0; i<n; i++) bp[i]=(float)v[i];
						return TIFFWriteFloatArray(tif, dir, bp);
					}
				case TIFFDataType.TIFF_DOUBLE:
					return TIFFWriteDoubleArray(tif, dir, v);
				default:
					// TIFF_NOTYPE
					// TIFF_ASCII
					// TIFF_UNDEFINED
					// TIFF_RATIONAL
					// TIFF_SRATIONAL
					return false;
			}
		}

		static bool TIFFWriteTransferFunction(TIFF tif, TIFFDirEntry dir)
		{
			TIFFDirectory td=tif.tif_dir;
			int n=1<<td.td_bitspersample;
			ushort[][] tf=td.td_transferfunction;
			uint ncols;

			// Check if the table can be written as a single column,
			// or if it must be written as 3 columns. Note that we
			// write a 3-column tag if there are 2 samples/pixel and
			// a single column of data won't suffice--hmm.
			switch(td.td_samplesperpixel-td.td_extrasamples)
			{
				default: if(TIFFmemcmp(tf[0], tf[2], n)) { ncols=3; break; } goto case 2;
				case 2: if(TIFFmemcmp(tf[0], tf[1], n)) { ncols=3; break; } goto case 1;
				case 1:
				case 0: ncols=1; break;
			}

			return TIFFWriteShortTable(tif, TIFFTAG.TRANSFERFUNCTION, dir, ncols, tf);
		}

		static bool TIFFWriteInkNames(TIFF tif, TIFFDirEntry dir)
		{
			TIFFDirectory td=tif.tif_dir;

			dir.tdir_tag=(ushort)TIFFTAG.INKNAMES;
			dir.tdir_type=(ushort)TIFFDataType.TIFF_ASCII;

			string cp=td.td_inknames;
			cp=cp.TrimEnd('\0');
			cp+='\0';
			dir.tdir_count=(uint)cp.Length;

			return TIFFWriteByteArray(tif, dir, Encoding.ASCII.GetBytes(cp));
		}

		// Write a contiguous directory item.
		static bool TIFFWriteData(TIFF tif, TIFFDirEntry dir, byte[] cp)
		{
			if((tif.tif_flags&TIF_FLAGS.TIFF_SWAB)!=0)
			{
				switch((TIFFDataType)dir.tdir_type)
				{
					case TIFFDataType.TIFF_SHORT:
					case TIFFDataType.TIFF_SSHORT:
						TIFFSwabArrayOfShort(cp, dir.tdir_count);
						break;
					case TIFFDataType.TIFF_LONG:
					case TIFFDataType.TIFF_SLONG:
					case TIFFDataType.TIFF_FLOAT:
						TIFFSwabArrayOfLong(cp, dir.tdir_count);
						break;
					case TIFFDataType.TIFF_RATIONAL:
					case TIFFDataType.TIFF_SRATIONAL:
						TIFFSwabArrayOfLong(cp, 2*dir.tdir_count);
						break;
					case TIFFDataType.TIFF_DOUBLE:
						TIFFSwabArrayOfDouble(cp, dir.tdir_count);
						break;
				}
			}

			dir.tdir_offset=tif.tif_dataoff;
			int cc=(int)dir.tdir_count*TIFFDataWidth((TIFFDataType)dir.tdir_type);

			if(SeekOK(tif, dir.tdir_offset)&&WriteOK(tif, cp, cc))
			{
				tif.tif_dataoff+=(uint)((cc+1)&~1);
				return true;
			}

			TIFFErrorExt(tif.tif_clientdata, tif.tif_name, "Error writing data for field \"{0}\"", TIFFFieldWithTag(tif, (TIFFTAG)dir.tdir_tag).field_name);
			return false;
		}

		// Similar to TIFFWriteDirectory(), but if the directory has already
		// been written once, it is relocated to the end of the file, in case it
		// has changed in size. Note that this will result in the loss of the
		// previously used directory space.
		public static bool TIFFRewriteDirectory(TIFF tif)
		{
			string module="TIFFRewriteDirectory";

			// We don't need to do anything special if it hasn't been written.
			if(tif.tif_diroff==0) return TIFFWriteDirectory(tif);

			// Find and zero the pointer to this directory, so that TIFFLinkDirectory
			// will cause it to be added after this directories current pre-link.

			// Is it the first directory in the file?
			if(tif.tif_header.tiff_diroff==tif.tif_diroff)
			{
				tif.tif_header.tiff_diroff=0;
				tif.tif_diroff=0;

				TIFFSeekFile(tif, (uint)(TIFF_MAGIC_SIZE+TIFF_VERSION_SIZE), SEEK.SET);
				if(!WriteOK(tif, tif.tif_header.tiff_diroff))
				{
					TIFFErrorExt(tif.tif_clientdata, tif.tif_name, "Error updating TIFF header");
					return true;
				}
			}
			else
			{
				uint nextdir=tif.tif_header.tiff_diroff;
				do
				{
					ushort dircount;
					if(!SeekOK(tif, nextdir)||!ReadOK(tif, out dircount))
					{
						TIFFErrorExt(tif.tif_clientdata, module, "Error fetching directory count");
						return false;
					}

					if((tif.tif_flags&TIF_FLAGS.TIFF_SWAB)!=0) TIFFSwab(ref dircount);
					TIFFSeekFile(tif, (uint)dircount*12, SEEK.CUR);

					if(!ReadOK(tif, out nextdir))
					{
						TIFFErrorExt(tif.tif_clientdata, module, "Error fetching directory link");
						return false;
					}

					if((tif.tif_flags&TIF_FLAGS.TIFF_SWAB)!=0) TIFFSwab(ref nextdir);
				} while(nextdir!=tif.tif_diroff&&nextdir!=0);

				uint off=TIFFSeekFile(tif, 0, SEEK.CUR); // get current offset
				TIFFSeekFile(tif, off-4, SEEK.SET);
				tif.tif_diroff=0;
				if(!WriteOK(tif, tif.tif_diroff))
				{
					TIFFErrorExt(tif.tif_clientdata, module, "Error writing directory link");
					return false;
				}
			}

			// Now use TIFFWriteDirectory() normally.
			return TIFFWriteDirectory(tif);
		}

		// Link the current directory into the
		// directory chain for the file.
		static bool TIFFLinkDirectory(TIFF tif)
		{
			string module="TIFFLinkDirectory";

			uint diroff=tif.tif_diroff=(TIFFSeekFile(tif, 0, SEEK.END)+1)&~1u;
			if((tif.tif_flags&TIF_FLAGS.TIFF_SWAB)!=0) TIFFSwab(ref diroff);

			// Handle SubIFDs
			if((tif.tif_flags&TIF_FLAGS.TIFF_INSUBIFD)!=0)
			{
				TIFFSeekFile(tif, tif.tif_subifdoff, SEEK.SET);
				if(!WriteOK(tif, diroff))
				{
					TIFFErrorExt(tif.tif_clientdata, module, "{0}: Error writing SubIFD directory link", tif.tif_name);
					return false;
				}
				// Advance to the next SubIFD or, if this is
				// the last one configured, revert back to the
				// normal directory linkage.
				tif.tif_nsubifd--;
				if(tif.tif_nsubifd!=0) tif.tif_subifdoff+=4;
				else tif.tif_flags&=~TIF_FLAGS.TIFF_INSUBIFD;
				return true;
			}

			if(tif.tif_header.tiff_diroff==0)
			{
				// First directory, overwrite offset in header.
				tif.tif_header.tiff_diroff=tif.tif_diroff;
				TIFFSeekFile(tif, (uint)(TIFF_MAGIC_SIZE+TIFF_VERSION_SIZE), SEEK.SET);
				if(!WriteOK(tif, diroff))
				{
					TIFFErrorExt(tif.tif_clientdata, tif.tif_name, "Error writing TIFF header");
					return false;
				}
				return true;
			}

			// Not the first directory, search to the last and append.
			uint nextdir=tif.tif_header.tiff_diroff;
			do
			{
				ushort dircount;
				if(!SeekOK(tif, nextdir)||!ReadOK(tif, out dircount))
				{
					TIFFErrorExt(tif.tif_clientdata, module, "Error fetching directory count");
					return false;
				}

				if((tif.tif_flags&TIF_FLAGS.TIFF_SWAB)!=0) TIFFSwab(ref dircount);
				TIFFSeekFile(tif, (uint)dircount*12, SEEK.CUR);

				if(!ReadOK(tif, out nextdir))
				{
					TIFFErrorExt(tif.tif_clientdata, module, "Error fetching directory link");
					return false;
				}
				if((tif.tif_flags&TIF_FLAGS.TIFF_SWAB)!=0) TIFFSwab(ref nextdir);
			} while(nextdir!=0);

			uint off=TIFFSeekFile(tif, 0, SEEK.CUR); // get current offset
			TIFFSeekFile(tif, off-4, SEEK.SET);
			if(!WriteOK(tif, diroff))
			{
				TIFFErrorExt(tif.tif_clientdata, module, "Error writing directory link");
				return false;
			}
			return true;
		}
	}
}
