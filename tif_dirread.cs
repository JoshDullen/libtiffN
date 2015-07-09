// tif_dirread.cs
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
// Directory Read Support Routines.

using System;
using System.Collections.Generic;
using System.Text;

namespace Free.Ports.LibTiff
{
	public static partial class libtiff
	{
		const int IGNORE=0; // tag placeholder used below

		// Read the next TIFF directory from a file
		// and convert it to the internal format.
		// We read directories sequentially.
		public static bool TIFFReadDirectory(TIFF tif)
		{
			string module="TIFFReadDirectory";

			ushort iv;
			uint v;
			TIFFFieldInfo fip;
			bool diroutoforderwarning=false;
			bool haveunknowntags=false;

			tif.tif_diroff=tif.tif_nextdiroff;
			// Check whether we have the last offset or bad offset (IFD looping).
			if(!TIFFCheckDirOffset(tif, tif.tif_nextdiroff)) return false;

			// Cleanup any previous compression state.
			tif.tif_cleanup(tif);
			tif.tif_curdir++;

			List<TIFFDirEntry> dir=null;
			ushort dircount=TIFFFetchDirectory(tif, tif.tif_nextdiroff, ref dir, ref tif.tif_nextdiroff);
			if(dircount==0)
			{
				TIFFErrorExt(tif.tif_clientdata, module, "{0}: Failed to read directory at offset{1}", tif.tif_name, tif.tif_nextdiroff);
				return false;
			}

			tif.tif_flags&=~TIF_FLAGS.TIFF_BEENWRITING; // reset before new dir

			// Setup default value and then make a pass over
			// the fields to check type and tag information,
			// and to extract info required to size data
			// structures. A second pass is made afterwards
			// to read in everthing not taken in the first pass.
			TIFFDirectory td=tif.tif_dir;

			// free any old stuff and reinit
			TIFFFreeDirectory(tif);
			TIFFDefaultDirectory(tif);

			// Electronic Arts writes gray-scale TIFF files
			// without a PlanarConfiguration directory entry.
			// Thus we setup a default value here, even though
			// the TIFF spec says there is no default value.
			TIFFSetField(tif, TIFFTAG.PLANARCONFIG, PLANARCONFIG.CONTIG);

			// Sigh, we must make a separate pass through the
			// directory for the following reason:
			//
			// We must process the Compression tag in the first pass
			// in order to merge in codec-private tag definitions (otherwise
			// we may get complaints about unknown tags). However, the
			// Compression tag may be dependent on the SamplesPerPixel
			// tag value because older TIFF specs permited Compression
			// to be written as a SamplesPerPixel-count tag entry.
			// Thus if we don't first figure out the correct SamplesPerPixel
			// tag value then we may end up ignoring the Compression tag
			// value because it has an incorrect count value (if the
			// true value of SamplesPerPixel is not 1).
			//
			// It sure would have been nice if Aldus had really thought
			// this stuff through carefully.
			foreach(TIFFDirEntry dp in dir)
			{
				if((tif.tif_flags&TIF_FLAGS.TIFF_SWAB)!=0)
				{
					TIFFSwab(ref dp.tdir_tag);
					TIFFSwab(ref dp.tdir_type);
					TIFFSwab(ref dp.tdir_count);
					TIFFSwab(ref dp.tdir_offset);
				}

				if((TIFFTAG)dp.tdir_tag==TIFFTAG.SAMPLESPERPIXEL)
				{
					if(!TIFFFetchNormalTag(tif, dp)) return false;
					dp.tdir_tag=IGNORE;
				}
			}

			// First real pass over the directory.
			int fix=0;
			foreach(TIFFDirEntry dp in dir)
			{
				if(dp.tdir_tag==IGNORE) continue;
				if(fix>=tif.tif_fieldinfo.Count) fix=0;

				// Silicon Beach (at least) writes unordered
				// directory tags (violating the spec). Handle
				// it here, but be obnoxious (maybe they'll fix it?).
				if(dp.tdir_tag<(ushort)tif.tif_fieldinfo[fix].field_tag)
				{
					if(!diroutoforderwarning)
					{
						TIFFWarningExt(tif.tif_clientdata, module, "{0}: invalid TIFF directory; tags are not sorted in ascending order", tif.tif_name);
						diroutoforderwarning=true;
					}
					fix=0; // O(n^2)
				}

				while(fix<tif.tif_fieldinfo.Count&&(ushort)tif.tif_fieldinfo[fix].field_tag<dp.tdir_tag) fix++;

				if(fix>=tif.tif_fieldinfo.Count||(ushort)tif.tif_fieldinfo[fix].field_tag!=dp.tdir_tag)
				{
					// Unknown tag ... we'll deal with it below
					haveunknowntags=true;
					continue;
				}

				// Null out old tags that we ignore.
				if(tif.tif_fieldinfo[fix].field_bit==FIELD.IGNORE)
				{
					dp.tdir_tag=IGNORE;
					continue;
				}

				// Check data type.
				fip=tif.tif_fieldinfo[fix];
				bool docontinue=false;
				while(dp.tdir_type!=(ushort)fip.field_type&&fix<tif.tif_fieldinfo.Count)
				{
					if(fip.field_type==TIFFDataType.TIFF_ANY) break; // wildcard

					fip=tif.tif_fieldinfo[++fix];
					if(fix>=tif.tif_fieldinfo.Count||(ushort)fip.field_tag!=dp.tdir_tag)
					{
						TIFFWarningExt(tif.tif_clientdata, module, "{0}: wrong data type {1} for \"{2}\"; tag ignored", tif.tif_name, dp.tdir_type, tif.tif_fieldinfo[fix-1].field_name);
						dp.tdir_tag=IGNORE;
						docontinue=true;
						break;
					}
				}
				if(docontinue) continue;

				// Check count if known in advance.
				if(fip.field_readcount!=TIFF_VARIABLE)
				{
					uint expected=(fip.field_readcount==TIFF_SPP)?td.td_samplesperpixel:(uint)fip.field_readcount;
					if(!CheckDirCount(tif, dp, expected))
					{
						dp.tdir_tag=IGNORE;
						continue;
					}
				}

				switch((TIFFTAG)dp.tdir_tag)
				{
					case TIFFTAG.COMPRESSION:
						// The 5.0 spec says the Compression tag has
						// one value, while earlier specs say it has
						// one value per sample. Because of this, we
						// accept the tag if one value is supplied.
						if(dp.tdir_count==1)
						{
							//was v = TIFFExtractData(tif, dp.tdir_type, dp.tdir_offset);
							v=(tif.tif_header.tiff_magic==TIFF_BIGENDIAN?(dp.tdir_offset>>tif_typeshift[dp.tdir_type])&tif_typemask[dp.tdir_type]:dp.tdir_offset&tif_typemask[dp.tdir_type]);

							if(!TIFFSetField(tif, (TIFFTAG)dp.tdir_tag, (ushort)v)) return false;
							break;
							// XXX: workaround for broken TIFFs
						}

						if((TIFFDataType)dp.tdir_type==TIFFDataType.TIFF_LONG)
						{
							if(!TIFFFetchPerSampleLongs(tif, dp, out v)||!TIFFSetField(tif, (TIFFTAG)dp.tdir_tag, (ushort)v))
								return false;
						}
						else
						{
							if(!TIFFFetchPerSampleShorts(tif, dp, out iv)||!TIFFSetField(tif, (TIFFTAG)dp.tdir_tag, iv))
								return false;
						}
						dp.tdir_tag=IGNORE;
						break;
					case TIFFTAG.STRIPOFFSETS:
					case TIFFTAG.STRIPBYTECOUNTS:
					case TIFFTAG.TILEOFFSETS:
					case TIFFTAG.TILEBYTECOUNTS:
						TIFFSetFieldBit(tif, fip.field_bit);
						break;
					case TIFFTAG.IMAGEWIDTH:
					case TIFFTAG.IMAGELENGTH:
					case TIFFTAG.IMAGEDEPTH:
					case TIFFTAG.TILELENGTH:
					case TIFFTAG.TILEWIDTH:
					case TIFFTAG.TILEDEPTH:
					case TIFFTAG.PLANARCONFIG:
					case TIFFTAG.ROWSPERSTRIP:
					case TIFFTAG.EXTRASAMPLES:
						if(!TIFFFetchNormalTag(tif, dp)) return false;
						dp.tdir_tag=IGNORE;
						break;
				}
			}

			// If we saw any unknown tags, make an extra pass over the directory
			// to deal with them. This must be done separately because the tags
			// could have become known when we registered a codec after finding
			// the Compression tag. In a correctly-sorted directory there's
			// no problem because Compression will come before any codec-private
			// tags, but if the sorting is wrong that might not hold.
			if(haveunknowntags)
			{
				fix=0;
				foreach(TIFFDirEntry dp in dir)
				{
					if(dp.tdir_tag==IGNORE) continue;
					if(fix>=tif.tif_fieldinfo.Count||dp.tdir_tag<(ushort)tif.tif_fieldinfo[fix].field_tag) fix=0;	// O(n^2)
					while(fix<tif.tif_fieldinfo.Count&&(ushort)tif.tif_fieldinfo[fix].field_tag<dp.tdir_tag) fix++;
					if(fix>=tif.tif_fieldinfo.Count||(ushort)tif.tif_fieldinfo[fix].field_tag!=dp.tdir_tag)
					{
						TIFFWarningExt(tif.tif_clientdata, module, "{0}: unknown field with tag %d (0x{1:X}) encountered", tif.tif_name, dp.tdir_tag, dp.tdir_tag);

						if(!_TIFFMergeFieldInfo(tif, TIFFCreateAnonFieldInfo(tif, (TIFFTAG)dp.tdir_tag, (TIFFDataType)dp.tdir_type)))
						{
							TIFFWarningExt(tif.tif_clientdata, module, "Registering anonymous field with tag {0} (0x{1:X}) failed", dp.tdir_tag, dp.tdir_tag);
							dp.tdir_tag=IGNORE;
							continue;
						}
						fix=0;
						while(fix<tif.tif_fieldinfo.Count&&(ushort)tif.tif_fieldinfo[fix].field_tag<dp.tdir_tag) fix++;
					}

					// Check data type.
					fip=tif.tif_fieldinfo[fix];
					while(dp.tdir_type!=(ushort)fip.field_type&&fix<tif.tif_fieldinfo.Count)
					{
						if(fip.field_type==TIFFDataType.TIFF_ANY)	// wildcard
							break;
						fip=tif.tif_fieldinfo[++fix];
						if(fix>=tif.tif_fieldinfo.Count||(ushort)fip.field_tag!=dp.tdir_tag)
						{
							TIFFWarningExt(tif.tif_clientdata, module, "{0}: wrong data type {1} for \"{2}\"; tag ignored", tif.tif_name, dp.tdir_type, tif.tif_fieldinfo[fix-1].field_name);
							dp.tdir_tag=IGNORE;
							break;
						}
					}
				}
			}

			// Allocate directory structure and setup defaults.
			if(!TIFFFieldSet(tif, FIELD.IMAGEDIMENSIONS))
			{
				MissingRequired(tif, "ImageLength");
				return false;
			}

			// Setup appropriate structures (by strip or by tile)
			if(!TIFFFieldSet(tif, FIELD.TILEDIMENSIONS))
			{
				td.td_nstrips=(uint)TIFFNumberOfStrips(tif);
				td.td_tilewidth=td.td_imagewidth;
				td.td_tilelength=td.td_rowsperstrip;
				td.td_tiledepth=td.td_imagedepth;
				tif.tif_flags&=~TIF_FLAGS.TIFF_ISTILED;
			}
			else
			{
				td.td_nstrips=(uint)TIFFNumberOfTiles(tif);
				tif.tif_flags|=TIF_FLAGS.TIFF_ISTILED;
			}

			if(td.td_nstrips==0)
			{
				TIFFErrorExt(tif.tif_clientdata, module, "{0}: cannot handle zero number of {1}", tif.tif_name, isTiled(tif)?"tiles":"strips");
				return false;
			}

			td.td_stripsperimage=td.td_nstrips;
			if(td.td_planarconfig==PLANARCONFIG.SEPARATE) td.td_stripsperimage/=td.td_samplesperpixel;
			if(!TIFFFieldSet(tif, FIELD.STRIPOFFSETS))
			{
				MissingRequired(tif, isTiled(tif)?"TileOffsets":"StripOffsets");
				return false;
			}

			// Second pass: extract other information.
			foreach(TIFFDirEntry dp in dir)
			{
				if(dp.tdir_tag==IGNORE) continue;

				switch((TIFFTAG)dp.tdir_tag)
				{
					case TIFFTAG.MINSAMPLEVALUE:
					case TIFFTAG.MAXSAMPLEVALUE:
					case TIFFTAG.BITSPERSAMPLE:
					case TIFFTAG.DATATYPE:
					case TIFFTAG.SAMPLEFORMAT:
						// The 5.0 spec says the Compression tag has
						// one value, while earlier specs say it has
						// one value per sample. Because of this, we
						// accept the tag if one value is supplied.
						//
						// The MinSampleValue, MaxSampleValue, BitsPerSample
						// DataType and SampleFormat tags are supposed to be
						// written as one value/sample, but some vendors
						// incorrectly write one value only -- so we accept
						// that as well (yech). Other vendors write correct
						// value for NumberOfSamples, but incorrect one for
						// BitsPerSample and friends, and we will read this
						// too.
						if(dp.tdir_count==1)
						{
							//was v = TIFFExtractData(tif, dp.tdir_type, dp.tdir_offset);
							v=(tif.tif_header.tiff_magic==TIFF_BIGENDIAN?(dp.tdir_offset>>tif_typeshift[dp.tdir_type])&tif_typemask[dp.tdir_type]:dp.tdir_offset&tif_typemask[dp.tdir_type]);

							if(!TIFFSetField(tif, (TIFFTAG)dp.tdir_tag, (ushort)v)) return false;
							// XXX: workaround for broken TIFFs
						}
						else if((TIFFTAG)dp.tdir_tag==TIFFTAG.BITSPERSAMPLE&&(TIFFDataType)dp.tdir_type==TIFFDataType.TIFF_LONG)
						{
							if(!TIFFFetchPerSampleLongs(tif, dp, out v)||!TIFFSetField(tif, (TIFFTAG)dp.tdir_tag, (ushort)v))
								return false;
						}
						else
						{
							if(!TIFFFetchPerSampleShorts(tif, dp, out iv)||!TIFFSetField(tif, (TIFFTAG)dp.tdir_tag, iv))
								return false;
						}
						break;
					case TIFFTAG.SMINSAMPLEVALUE:
					case TIFFTAG.SMAXSAMPLEVALUE:
						{
							double dv=0.0;
							if(!TIFFFetchPerSampleAnys(tif, dp, out dv)||!TIFFSetField(tif, (TIFFTAG)dp.tdir_tag, dv))
								return false;
						}
						break;
					case TIFFTAG.STRIPOFFSETS:
					case TIFFTAG.TILEOFFSETS:
						if(!TIFFFetchStripThing(tif, dp, td.td_nstrips, ref td.td_stripoffset)) return false;
						break;
					case TIFFTAG.STRIPBYTECOUNTS:
					case TIFFTAG.TILEBYTECOUNTS:
						if(!TIFFFetchStripThing(tif, dp, td.td_nstrips, ref td.td_stripbytecount)) return false;
						break;
					case TIFFTAG.COLORMAP:
					case TIFFTAG.TRANSFERFUNCTION:
						{
							// TransferFunction can have either 1x or 3x
							// data values; Colormap can have only 3x
							// items.
							v=1u<<td.td_bitspersample;
							if((TIFFTAG)dp.tdir_tag==TIFFTAG.COLORMAP||dp.tdir_count!=v)
							{
								if(!CheckDirCount(tif, dp, 3*v)) break;
							}

							//v*=sizeof(ushort);
							ushort[] cp=null;
							try
							{
								cp=new ushort[dp.tdir_count];
							}
							catch
							{
								TIFFErrorExt(tif.tif_clientdata, tif.tif_name, "No space to read \"TransferFunction\" tag");
							}

							if(cp==null) break;
							if(TIFFFetchData(tif, dp, cp)==0) break;

							// This deals with there being
							// only one array to apply to
							// all samples.
							uint c=1u<<td.td_bitspersample;
							if(dp.tdir_count==c) TIFFSetField(tif, (TIFFTAG)dp.tdir_tag, cp, cp, cp);
							else
							{
								try
								{
									ushort[] cp1=new ushort[v];
									ushort[] cp2=new ushort[v];
									ushort[] cp3=new ushort[v];

									Array.Copy(cp, 0*v, cp1, 0, v);
									Array.Copy(cp, 1*v, cp2, 0, v);
									Array.Copy(cp, 2*v, cp3, 0, v);

									TIFFSetField(tif, (TIFFTAG)dp.tdir_tag, cp1, cp2, cp3);
								}
								catch
								{
									TIFFErrorExt(tif.tif_clientdata, tif.tif_name, "No space to read \"TransferFunction\" tag");
								}
							}
						}
						break;
					case TIFFTAG.PAGENUMBER:
					case TIFFTAG.HALFTONEHINTS:
					case TIFFTAG.YCBCRSUBSAMPLING:
					case TIFFTAG.DOTRANGE:
						TIFFFetchShortPair(tif, dp);
						break;
					case TIFFTAG.REFERENCEBLACKWHITE:
						TIFFFetchRefBlackWhite(tif, dp);
						break;
					// BEGIN REV 4.0 COMPATIBILITY
					case TIFFTAG.OSUBFILETYPE:
						v=0;
						//was switch(TIFFExtractData(tif, dp.tdir_type, dp.tdir_offset)) 
						switch((OFILETYPE)(tif.tif_header.tiff_magic==TIFF_BIGENDIAN?(dp.tdir_offset>>tif_typeshift[dp.tdir_type])&tif_typemask[dp.tdir_type]:dp.tdir_offset&tif_typemask[dp.tdir_type]))
						{
							case OFILETYPE.REDUCEDIMAGE: v=(uint)FILETYPE.REDUCEDIMAGE; break;
							case OFILETYPE.PAGE: v=(uint)FILETYPE.PAGE; break;
						}
						if(v!=0) TIFFSetField(tif, TIFFTAG.SUBFILETYPE, v);
						break;
					// END REV 4.0 COMPATIBILITY
					default:
						TIFFFetchNormalTag(tif, dp);
						break;
				}
			}

			// Verify Palette image has a Colormap.
			if(td.td_photometric==PHOTOMETRIC.PALETTE&&!TIFFFieldSet(tif, FIELD.COLORMAP))
			{
				MissingRequired(tif, "Colormap");
				return false;
			}

			// Attempt to deal with a missing StripByteCounts tag.
			if(!TIFFFieldSet(tif, FIELD.STRIPBYTECOUNTS))
			{
				// Some manufacturers violate the spec by not giving
				// the size of the strips. In this case, assume there
				// is one uncompressed strip of data.
				if((td.td_planarconfig==PLANARCONFIG.CONTIG&&td.td_nstrips>1)||
					(td.td_planarconfig==PLANARCONFIG.SEPARATE&&td.td_nstrips!=td.td_samplesperpixel))
				{
					MissingRequired(tif, "StripByteCounts");
					return false;
				}

				TIFFWarningExt(tif.tif_clientdata, module, "{0}: TIFF directory is missing required \"{1}\" field, calculating from imagelength", tif.tif_name, TIFFFieldWithTag(tif, TIFFTAG.STRIPBYTECOUNTS).field_name);
				if(EstimateStripByteCounts(tif, dir)<0) return false;

				// Assume we have wrong StripByteCount value (in case of single strip) in
				// following cases:
				//	-	it is equal to zero along with StripOffset;
				//	-	it is larger than file itself (in case of uncompressed image);
				//	-	it is smaller than the size of the bytes per row multiplied on the
				//		number of rows. The last case should not be checked in the case of
				//		writing new image, because we may do not know the exact strip size
				//		until the whole image will be written and directory dumped out.
			}
			else if(td.td_nstrips==1&&td.td_stripoffset[0]!=0&&((td.td_stripbytecount[0]==0&&td.td_stripoffset[0]!=0)||
					(td.td_compression==COMPRESSION.NONE&&td.td_stripbytecount[0]>TIFFGetFileSize(tif)-td.td_stripoffset[0])||
					(tif.tif_mode==O.RDONLY&&td.td_compression==COMPRESSION.NONE&&td.td_stripbytecount[0]<TIFFScanlineSize(tif)*td.td_imagelength)))
			{
				// XXX: Plexus (and others) sometimes give a value of zero for
				// a tag when they don't know what the correct value is! Try
				// and handle the simple case of estimating the size of a one
				// strip image.
				TIFFWarningExt(tif.tif_clientdata, module, "{0}: Bogus \"{1}\" field, ignoring and calculating from imagelength", tif.tif_name, TIFFFieldWithTag(tif, TIFFTAG.STRIPBYTECOUNTS).field_name);
				if(EstimateStripByteCounts(tif, dir)<0) return false;
			}
			else if(td.td_planarconfig==PLANARCONFIG.CONTIG&&td.td_nstrips>2&&td.td_compression==COMPRESSION.NONE&&td.td_stripbytecount[0]!=td.td_stripbytecount[1]&&td.td_stripbytecount[0]!=0&&td.td_stripbytecount[1]!=0)
			{
				// XXX: Some vendors fill StripByteCount array with absolutely
				// wrong values (it can be equal to StripOffset array, for
				// example). Catch this case here.
				TIFFWarningExt(tif.tif_clientdata, module, "{0}: Wrong \"{1}\" field, ignoring and calculating from imagelength", tif.tif_name, TIFFFieldWithTag(tif, TIFFTAG.STRIPBYTECOUNTS).field_name);
				if(EstimateStripByteCounts(tif, dir)<0) return false;
			}

			dir=null;

			if(!TIFFFieldSet(tif, FIELD.MAXSAMPLEVALUE)) td.td_maxsamplevalue=(ushort)((1<<td.td_bitspersample)-1);

			// Setup default compression scheme.

			// XXX: We can optimize checking for the strip bounds using the sorted
			// bytecounts array. See also comments for TIFFAppendToStrip()
			// function in tif_write.cs.
			if(td.td_nstrips>1)
			{
				td.td_stripbytecountsorted=1;
				for(uint strip=1; strip<td.td_nstrips; strip++)
				{
					if(td.td_stripoffset[strip-1]>td.td_stripoffset[strip])
					{
						td.td_stripbytecountsorted=0;
						break;
					}
				}
			}

			if(!TIFFFieldSet(tif, FIELD.COMPRESSION)) TIFFSetField(tif, TIFFTAG.COMPRESSION, COMPRESSION.NONE);

			// Some manufacturers make life difficult by writing
			// large amounts of uncompressed data as a single strip.
			// This is contrary to the recommendations of the spec.
			// The following makes an attempt at breaking such images
			// into strips closer to the recommended 8k bytes. A
			// side effect, however, is that the RowsPerStrip tag
			// value may be changed.
			if(td.td_nstrips==1&&td.td_compression==COMPRESSION.NONE&&(tif.tif_flags&(TIF_FLAGS.TIFF_STRIPCHOP|TIF_FLAGS.TIFF_ISTILED))==TIF_FLAGS.TIFF_STRIPCHOP)
				ChopUpSingleUncompressedStrip(tif);

			// Reinitialize i/o since we are starting on a new directory.
			tif.tif_row=0xffffffff;
			tif.tif_curstrip=0xffffffff;
			tif.tif_col=0xffffffff;
			tif.tif_curtile=0xffffffff;
			tif.tif_tilesize=-1;

			tif.tif_scanlinesize=(uint)TIFFScanlineSize(tif);
			if(tif.tif_scanlinesize==0)
			{
				TIFFErrorExt(tif.tif_clientdata, module, "{0}: cannot handle zero scanline size", tif.tif_name);
				return false;
			}

			if(isTiled(tif))
			{
				tif.tif_tilesize=TIFFTileSize(tif);
				if(tif.tif_tilesize==0)
				{
					TIFFErrorExt(tif.tif_clientdata, module, "{0}: cannot handle zero tile size", tif.tif_name);
					return false;
				}
			}
			else
			{
				if(TIFFStripSize(tif)==0)
				{
					TIFFErrorExt(tif.tif_clientdata, module, "{0}: cannot handle zero strip size", tif.tif_name);
					return false;
				}
			}

			return true;
		}

		public static TIFFDirEntry TIFFReadDirectoryFind(List<TIFFDirEntry> dir, ushort dircount, ushort tagid)
		{
			foreach(TIFFDirEntry m in dir)
			{
				if(m.tdir_tag==tagid) return m;
			}
			return null;
		}

		// Read custom directory from the arbitarry offset.
		// The code is very similar to TIFFReadDirectory().
		public static bool TIFFReadCustomDirectory(TIFF tif, uint diroff, List<TIFFFieldInfo> info)
		{
			string module="TIFFReadCustomDirectory";

			TIFFSetupFieldInfo(tif, info);

			List<TIFFDirEntry> dir=null;
			uint nextdiroff=0;
			ushort dircount=TIFFFetchDirectory(tif, diroff, ref dir, ref nextdiroff);
			if(dircount==0)
			{
				TIFFErrorExt(tif.tif_clientdata, module, "{0}: Failed to read custom directory at offset {1}", tif.tif_name, diroff);
				return false;
			}

			TIFFFreeDirectory(tif);

			TIFFDirectory td=tif.tif_dir;
			td.Clear();

			int fix=0;
			foreach(TIFFDirEntry dp in dir)
			{
				if((tif.tif_flags&TIF_FLAGS.TIFF_SWAB)!=0)
				{
					TIFFSwab(ref dp.tdir_tag);
					TIFFSwab(ref dp.tdir_type);
					TIFFSwab(ref dp.tdir_count);
					TIFFSwab(ref dp.tdir_offset);
				}

				if(fix>=tif.tif_fieldinfo.Count||dp.tdir_tag==IGNORE) continue;

				while(fix<tif.tif_fieldinfo.Count&&tif.tif_fieldinfo[fix].field_tag<(TIFFTAG)dp.tdir_tag) fix++;

				if(fix>=tif.tif_fieldinfo.Count||tif.tif_fieldinfo[fix].field_tag!=(TIFFTAG)dp.tdir_tag)
				{
					TIFFWarningExt(tif.tif_clientdata, module, "{0}: unknown field with tag {1} (0x{1:X}) encountered", tif.tif_name, dp.tdir_tag);

					if(!_TIFFMergeFieldInfo(tif, TIFFCreateAnonFieldInfo(tif, (TIFFTAG)dp.tdir_tag, (TIFFDataType)dp.tdir_type)))
					{
						TIFFWarningExt(tif.tif_clientdata, module, "Registering anonymous field with tag {0} (0x{0:X}) failed", dp.tdir_tag);
						dp.tdir_tag=IGNORE;
						continue;
					}

					fix=0;
					while(fix<tif.tif_fieldinfo.Count&&tif.tif_fieldinfo[fix].field_tag<(TIFFTAG)dp.tdir_tag) fix++;
				}

				// Null out old tags that we ignore.
				if(tif.tif_fieldinfo[fix].field_bit==FIELD.IGNORE)
				{
					dp.tdir_tag=IGNORE;
					continue;
				}

				// Check data type.
				TIFFFieldInfo fip=tif.tif_fieldinfo[fix];
				bool docontinue=false;
				while(dp.tdir_type!=(ushort)fip.field_type&&fix<tif.tif_fieldinfo.Count)
				{
					if(fip.field_type==TIFFDataType.TIFF_ANY) break; // wildcard

					fip=tif.tif_fieldinfo[++fix];
					if(fix>=tif.tif_fieldinfo.Count||fip.field_tag!=(TIFFTAG)dp.tdir_tag)
					{
						TIFFWarningExt(tif.tif_clientdata, module, "{0}: wrong data type {1} for \"{2}\"; tag ignored", tif.tif_name, dp.tdir_type, tif.tif_fieldinfo[fix-1].field_name);
						dp.tdir_tag=IGNORE;
						docontinue=true;
						break;
					}
				}
				if(docontinue) continue;

				// Check count if known in advance.
				if(fip.field_readcount!=TIFF_VARIABLE)
				{
					uint expected=(fip.field_readcount==TIFF_SPP)?(uint)td.td_samplesperpixel:(uint)fip.field_readcount;
					if(!CheckDirCount(tif, dp, expected))
					{
						dp.tdir_tag=IGNORE;
						continue;
					}
				}

				// EXIF tags which need to be specifically processed.
				switch(dp.tdir_tag)
				{
					case (ushort)EXIFTAG.SUBJECTDISTANCE: TIFFFetchSubjectDistance(tif, dp);
						break;
					default:
						TIFFFetchNormalTag(tif, dp);
						break;
				}
			}

			dir=null;
			return true;
		}

		// EXIF is important special case of custom IFD, so we have a special
		// function to read it.
		public static bool TIFFReadEXIFDirectory(TIFF tif, uint diroff)
		{
			return TIFFReadCustomDirectory(tif, diroff, exifFieldInfo);
		}

		static int EstimateStripByteCounts(TIFF tif, List<TIFFDirEntry> dir)
		{
			string module="EstimateStripByteCounts";

			TIFFDirectory td=tif.tif_dir;
			uint strip;

			try
			{
				td.td_stripbytecount=new uint[td.td_nstrips];
			}
			catch
			{
				TIFFErrorExt(tif.tif_clientdata, tif.tif_name, "No space for \"StripByteCounts\" array");
			}

			if(td.td_stripbytecount==null) return -1;

			if(td.td_compression!=COMPRESSION.NONE)
			{
				//uint space=sizeof(TIFFHeader)+sizeof(ushort)+(dir.Count*sizeof(TIFFDirEntry))+sizeof(uint);
				uint space=(uint)(8+2+(dir.Count*12)+4);

				uint filesize=TIFFGetFileSize(tif);

				// calculate amount of space used by indirect values
				foreach(TIFFDirEntry dp in dir)
				{
					uint cc=(uint)TIFFDataWidth((TIFFDataType)dp.tdir_type);
					if(cc==0)
					{
						TIFFErrorExt(tif.tif_clientdata, module, "{0}: Cannot determine size of unknown tag type {1}", tif.tif_name, dp.tdir_type);
						return -1;
					}
					cc=cc*dp.tdir_count;
					if(cc>4) space+=cc; // 4: sizeof(uint)
				}

				space=filesize-space;
				if(td.td_planarconfig==PLANARCONFIG.SEPARATE) space/=td.td_samplesperpixel;
				for(strip=0; strip<td.td_nstrips; strip++) td.td_stripbytecount[strip]=space;

				// This gross hack handles the case were the offset to
				// the last strip is past the place where we think the strip
				// should begin. Since a strip of data must be contiguous,
				// it's safe to assume that we've overestimated the amount
				// of data in the strip and trim this number back accordingly.
				strip--;
				if(((uint)(td.td_stripoffset[strip]+td.td_stripbytecount[strip]))>filesize) td.td_stripbytecount[strip]=filesize-td.td_stripoffset[strip];
			}
			else if(isTiled(tif))
			{
				uint bytespertile=(uint)TIFFTileSize(tif);
				for(strip=0; strip<td.td_nstrips; strip++) td.td_stripbytecount[strip]=bytespertile;
			}
			else
			{
				uint rowbytes=(uint)TIFFScanlineSize(tif);
				uint rowsperstrip=td.td_imagelength/td.td_stripsperimage;
				for(strip=0; strip<td.td_nstrips; strip++) td.td_stripbytecount[strip]=rowbytes*rowsperstrip;
			}

			TIFFSetFieldBit(tif, FIELD.STRIPBYTECOUNTS);
			if(!TIFFFieldSet(tif, FIELD.ROWSPERSTRIP)) td.td_rowsperstrip=td.td_imagelength;

			return 1;
		}

		static void MissingRequired(TIFF tif, string tagname)
		{
			string module="MissingRequired";
			TIFFErrorExt(tif.tif_clientdata, module, "{0}: TIFF directory is missing required \"{1}\" field", tif.tif_name, tagname);
		}

		// Check the directory offset against the list of already seen directory
		// offsets. This is a trick to prevent IFD looping. The one can create TIFF
		// file with looped directory pointers. We will maintain a list of already
		// seen directories and check every IFD offset against that list.
		static bool TIFFCheckDirOffset(TIFF tif, uint diroff)
		{
			if(diroff==0) return false; // no more directories

			for(int n=0; n<tif.tif_dirnumber; n++)
			{
				if(tif.tif_dirlist[n]==tif.tif_diroff) return false;
			}

			try
			{
				tif.tif_dirlist.Add(tif.tif_diroff);
				tif.tif_dirnumber++;
			}
			catch
			{
				TIFFErrorExt(tif.tif_clientdata, "TIFFCheckDirOffset", "{0}: Failed to allocate space for IFD list", tif.tif_name);
				return false;
			}

			return true;
		}

		// Check the count field of a directory
		// entry against a known value. The caller
		// is expected to skip/ignore the tag if
		// there is a mismatch.
		static bool CheckDirCount(TIFF tif, TIFFDirEntry dir, uint count)
		{
			if(count>dir.tdir_count)
			{
				TIFFWarningExt(tif.tif_clientdata, tif.tif_name, "incorrect count for field \"{0}\" ({1}, expecting {2}); tag ignored", TIFFFieldWithTag(tif, (TIFFTAG)dir.tdir_tag).field_name, dir.tdir_count, count);
				return false;
			}

			if(count<dir.tdir_count)
			{
				TIFFWarningExt(tif.tif_clientdata, tif.tif_name, "incorrect count for field \"{0}\" ({1}, expecting {2}); tag trimmed", TIFFFieldWithTag(tif, (TIFFTAG)dir.tdir_tag).field_name, dir.tdir_count, count);
				return true;
			}

			return true;
		}

		// Read IFD structure from the specified offset. If the pointer to
		// nextdiroff variable has been specified, read it too. Function returns a
		// number of fields in the directory or 0 if failed.
		static ushort TIFFFetchDirectory(TIFF tif, uint diroff, ref List<TIFFDirEntry> pdir, ref uint nextdiroff)
		{
			string module="TIFFFetchDirectory";

			tif.tif_diroff=diroff;

			if(!SeekOK(tif, tif.tif_diroff))
			{
				TIFFErrorExt(tif.tif_clientdata, module, "{0}: Seek error accessing TIFF directory", tif.tif_name);
				return 0;
			}

			ushort dircount;
			if(!ReadOK(tif, out dircount))
			{
				TIFFErrorExt(tif.tif_clientdata, module, "{0}: Can not read TIFF directory count", tif.tif_name);
				return 0;
			}

			if((tif.tif_flags&TIF_FLAGS.TIFF_SWAB)!=0) TIFFSwab(ref dircount);

			List<TIFFDirEntry> dir=null;
			try
			{
				dir=new List<TIFFDirEntry>();
			}
			catch
			{
				return 0;
			}

			if(!ReadOK(tif, dir, dircount))
			{
				TIFFErrorExt(tif.tif_clientdata, module, "{0}: Can not read TIFF directory", tif.tif_name);
				return 0;
			}

			// Read offset to next directory for sequential scans.if needed.
			if(nextdiroff!=0) ReadOK(tif, out nextdiroff);

			if(nextdiroff!=0&&(tif.tif_flags&TIF_FLAGS.TIFF_SWAB)!=0) TIFFSwab(ref nextdiroff);

			pdir=dir;
			return dircount;
		}

		// Fetch a contiguous directory item.
		static int TIFFFetchData(TIFF tif, TIFFDirEntry dir, byte[] cp)
		{
			uint w=(uint)TIFFDataWidth((TIFFDataType)dir.tdir_type);

			// FIXME: butecount should have tsize_t type, but for now libtiff
			// defines tsize_t as a signed 32-bit integer and we are losing
			// ability to read arrays larger than 2^31 bytes. So we are using
			// uint32 instead of tsize_t here.
			uint cc=dir.tdir_count*w;

			// Check for overflow.
			if(dir.tdir_count==0||w==0||cc/w!=dir.tdir_count)
			{
				TIFFErrorExt(tif.tif_clientdata, tif.tif_name, "Error fetching data for field \"{0}\"", TIFFFieldWithTag(tif, (TIFFTAG)dir.tdir_tag).field_name);
				return 0;
			}

			if(!SeekOK(tif, dir.tdir_offset))
			{
				TIFFErrorExt(tif.tif_clientdata, tif.tif_name, "Error fetching data for field \"{0}\"", TIFFFieldWithTag(tif, (TIFFTAG)dir.tdir_tag).field_name);
				return 0;
			}

			if(!ReadOK(tif, cp, (int)cc))
			{
				TIFFErrorExt(tif.tif_clientdata, tif.tif_name, "Error fetching data for field \"{0}\"", TIFFFieldWithTag(tif, (TIFFTAG)dir.tdir_tag).field_name);
				return 0;
			}

			if((tif.tif_flags&TIF_FLAGS.TIFF_SWAB)==0) return (int)cc;

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

			return (int)cc;
		}

		static int TIFFFetchData(TIFF tif, TIFFDirEntry dir, ushort[] cp)
		{
			byte[] buf=new byte[dir.tdir_count*2];
			int ret=TIFFFetchData(tif, dir, buf);
			if(ret==0) return 0;

			for(int i=0; i<dir.tdir_count; i++) cp[i]=BitConverter.ToUInt16(buf, i*2);
			return ret;
		}

		// Fetch an ASCII item from the file.
		static int TIFFFetchString(TIFF tif, TIFFDirEntry dir, out string cp)
		{
			if(dir.tdir_count<=4)
			{
				uint l=dir.tdir_offset;
				if((tif.tif_flags&TIF_FLAGS.TIFF_SWAB)!=0) TIFFSwab(ref l); // swab 'back' see in TIFFReadDirectory & TIFFReadCustomDirectory

				cp=System.Text.Encoding.ASCII.GetString(BitConverter.GetBytes(l));
				return 1;
			}

			cp=null;

			byte[] buf=new byte[dir.tdir_count];
			int ret=TIFFFetchData(tif, dir, buf);
			if(ret==0) return 0;

			cp=System.Text.Encoding.ASCII.GetString(buf);
			return ret;
		}

		// Convert numerator+denominator to double.
		static bool cvtRational(TIFF tif, TIFFDirEntry dir, uint num, uint denom, out double rv)
		{
			if(num==0&&denom==0)
			{
				rv=0;
				return true;
			}

			if(denom==0)
			{
				TIFFErrorExt(tif.tif_clientdata, tif.tif_name, "{0}: Rational with zero denominator (num = {1})", TIFFFieldWithTag(tif, (TIFFTAG)dir.tdir_tag).field_name, num);
				rv=double.NaN;
				return false;
			}

			if((TIFFDataType)dir.tdir_type==TIFFDataType.TIFF_RATIONAL) rv=((double)num/(double)denom);
			else rv=((double)(int)num/(double)(int)denom);

			return true;
		}

		// Fetch a rational item from the file
		// at offset off and return the value
		// as a floating point number.
		static double TIFFFetchRational(TIFF tif, TIFFDirEntry dir)
		{
			byte[] buf=new byte[8];
			if(TIFFFetchData(tif, dir, buf)==0) return 1.0;

			double v;
			return !cvtRational(tif, dir, BitConverter.ToUInt32(buf, 0), BitConverter.ToUInt32(buf, 4), out v)?1.0:v;
		}

		// Fetch a single floating point value
		// from the offset field and return it
		// as a native float.
		static float TIFFFetchFloat(TIFF tif, TIFFDirEntry dir)
		{
			return BitConverter.ToSingle(BitConverter.GetBytes(dir.tdir_offset), 0);
		}

		// Fetch a double item from the file
		// at offset off and return the value.
		static double TIFFFetchDouble(TIFF tif, TIFFDirEntry dir)
		{
			byte[] buf=new byte[8];
			if(TIFFFetchData(tif, dir, buf)==0) return 1.0;

			return BitConverter.ToDouble(buf, 0);
		}

		// Fetch an array of BYTE values.
		static bool TIFFFetchByteArray(TIFF tif, TIFFDirEntry dir, byte[] v)
		{
			if(dir.tdir_count<=4)
			{
				// Extract data from offset field.
				if(tif.tif_header.tiff_magic==TIFF_BIGENDIAN)
				{
					switch(dir.tdir_count)
					{
						case 4: v[3]=(byte)(dir.tdir_offset&0xff); goto case 3;
						case 3: v[2]=(byte)((dir.tdir_offset>>8)&0xff); goto case 2;
						case 2: v[1]=(byte)((dir.tdir_offset>>16)&0xff); goto case 1;
						case 1: v[0]=(byte)(dir.tdir_offset>>24); break;
					}
				}
				else
				{
					switch(dir.tdir_count)
					{
						case 4: v[3]=(byte)(dir.tdir_offset>>24); goto case 3;
						case 3: v[2]=(byte)((dir.tdir_offset>>16)&0xff); goto case 2;
						case 2: v[1]=(byte)((dir.tdir_offset>>8)&0xff); goto case 1;
						case 1: v[0]=(byte)(dir.tdir_offset&0xff); break;
					}
				}
				return true;
			}

			return TIFFFetchData(tif, dir, v)!=0;
		}

		// Fetch an array of SBYTE values.
		static bool TIFFFetchByteArray(TIFF tif, TIFFDirEntry dir, sbyte[] v)
		{
			if(dir.tdir_count<=4)
			{
				// Extract data from offset field.
				if(tif.tif_header.tiff_magic==TIFF_BIGENDIAN)
				{
					switch(dir.tdir_count)
					{
						case 4: v[3]=(sbyte)(dir.tdir_offset&0xff); goto case 3;
						case 3: v[2]=(sbyte)((dir.tdir_offset>>8)&0xff); goto case 2;
						case 2: v[1]=(sbyte)((dir.tdir_offset>>16)&0xff); goto case 1;
						case 1: v[0]=(sbyte)(dir.tdir_offset>>24); break;
					}
				}
				else
				{
					switch(dir.tdir_count)
					{
						case 4: v[3]=(sbyte)(dir.tdir_offset>>24); goto case 3;
						case 3: v[2]=(sbyte)((dir.tdir_offset>>16)&0xff); goto case 2;
						case 2: v[1]=(sbyte)((dir.tdir_offset>>8)&0xff); goto case 1;
						case 1: v[0]=(sbyte)(dir.tdir_offset&0xff); break;
					}
				}
				return true;
			}

			byte[] buf=new byte[dir.tdir_count];
			if(TIFFFetchData(tif, dir, buf)==0) return false;
			for(int i=0; i<dir.tdir_count; i++) v[i]=(sbyte)buf[i];
			return true;
		}

		// Fetch an array of SHORT values.
		static bool TIFFFetchShortArray(TIFF tif, TIFFDirEntry dir, ushort[] v)
		{
			if(dir.tdir_count<=2)
			{
				if(tif.tif_header.tiff_magic==TIFF_BIGENDIAN)
				{
					switch(dir.tdir_count)
					{
						case 2: v[1]=(ushort)(dir.tdir_offset&0xffff); goto case 1;
						case 1: v[0]=(ushort)(dir.tdir_offset>>16); break;
					}
				}
				else
				{
					switch(dir.tdir_count)
					{
						case 2: v[1]=(ushort)(dir.tdir_offset>>16); goto case 1;
						case 1: v[0]=(ushort)(dir.tdir_offset&0xffff); break;
					}
				}
				return true;
			}

			byte[] buf=new byte[dir.tdir_count*2];
			if(TIFFFetchData(tif, dir, buf)==0) return false;
			for(int i=0; i<dir.tdir_count; i++) v[i]=BitConverter.ToUInt16(buf, i*2);
			return true;
		}

		// Fetch an array of SSHORT values.
		static bool TIFFFetchShortArray(TIFF tif, TIFFDirEntry dir, short[] v)
		{
			if(dir.tdir_count<=2)
			{
				if(tif.tif_header.tiff_magic==TIFF_BIGENDIAN)
				{
					switch(dir.tdir_count)
					{
						case 2: v[1]=(short)(dir.tdir_offset&0xffff); goto case 1;
						case 1: v[0]=(short)(dir.tdir_offset>>16); break;
					}
				}
				else
				{
					switch(dir.tdir_count)
					{
						case 2: v[1]=(short)(dir.tdir_offset>>16); goto case 1;
						case 1: v[0]=(short)(dir.tdir_offset&0xffff); break;
					}
				}
				return true;
			}

			byte[] buf=new byte[dir.tdir_count*2];
			if(TIFFFetchData(tif, dir, buf)==0) return false;
			for(int i=0; i<dir.tdir_count; i++) v[i]=BitConverter.ToInt16(buf, i*2);
			return true;
		}

		// Fetch a pair of SHORT or BYTE values. Some tags may have either BYTE
		// or SHORT type and this function works with both ones.
		static bool TIFFFetchShortPair(TIFF tif, TIFFDirEntry dir)
		{
			// Prevent overflowing the v stack arrays below by performing a sanity
			// check on tdir_count, this should never be greater than two.
			if(dir.tdir_count>2)
			{
				TIFFWarningExt(tif.tif_clientdata, tif.tif_name, "unexpected count for field \"{0}\", {1}, expected 2; ignored", TIFFFieldWithTag(tif, (TIFFTAG)dir.tdir_tag).field_name, dir.tdir_count);
				return false;
			}

			switch((TIFFDataType)dir.tdir_type)
			{
				case TIFFDataType.TIFF_BYTE:
					{
						byte[] v=new byte[4];
						return TIFFFetchByteArray(tif, dir, v)&&TIFFSetField(tif, (TIFFTAG)dir.tdir_tag, (TIFFDataType)dir.tdir_type, v[0], v[1]);
					}
				case TIFFDataType.TIFF_SBYTE:
					{
						sbyte[] v=new sbyte[4];
						return TIFFFetchByteArray(tif, dir, v)&&TIFFSetField(tif, (TIFFTAG)dir.tdir_tag, (TIFFDataType)dir.tdir_type, v[0], v[1]);
					}
				case TIFFDataType.TIFF_SHORT:
					{
						ushort[] v=new ushort[2];
						return TIFFFetchShortArray(tif, dir, v)&&TIFFSetField(tif, (TIFFTAG)dir.tdir_tag, (TIFFDataType)dir.tdir_type, v[0], v[1]);
					}
				case TIFFDataType.TIFF_SSHORT:
					{
						short[] v=new short[2];
						return TIFFFetchShortArray(tif, dir, v)&&TIFFSetField(tif, (TIFFTAG)dir.tdir_tag, (TIFFDataType)dir.tdir_type, v[0], v[1]);
					}
				default: return false;
			}
		}

		// Fetch an array of LONG values.
		static bool TIFFFetchLongArray(TIFF tif, TIFFDirEntry dir, uint[] v)
		{
			if(dir.tdir_count==1)
			{
				v[0]=dir.tdir_offset;
				return true;
			}

			byte[] buf=new byte[dir.tdir_count*4];
			if(TIFFFetchData(tif, dir, buf)==0) return false;
			for(int i=0; i<dir.tdir_count; i++) v[i]=BitConverter.ToUInt32(buf, i*4);
			return true;
		}

		// Fetch an array of SLONG values.
		static bool TIFFFetchLongArray(TIFF tif, TIFFDirEntry dir, int[] v)
		{
			if(dir.tdir_count==1)
			{
				v[0]=(int)dir.tdir_offset;
				return true;
			}

			byte[] buf=new byte[dir.tdir_count*4];
			if(TIFFFetchData(tif, dir, buf)==0) return false;
			for(int i=0; i<dir.tdir_count; i++) v[i]=BitConverter.ToInt32(buf, i*4);
			return true;
		}

		// Fetch an array of RATIONAL or SRATIONAL values.
		static bool TIFFFetchRationalArray(TIFF tif, TIFFDirEntry dir, double[] v)
		{
			byte[] buf=null;
			try
			{
				buf=new byte[dir.tdir_count*8];
			}
			catch
			{
				TIFFErrorExt(tif.tif_clientdata, tif.tif_name, "No space to fetch array of rationals");
				return false;
			}

			for(int i=0; i<dir.tdir_count; i++)
			{
				if(!cvtRational(tif, dir, BitConverter.ToUInt32(buf, i*8), BitConverter.ToUInt32(buf, i*8+4), out v[i])) return false;
			}
			return true;
		}

		// Fetch an array of FLOAT values.
		static bool TIFFFetchFloatArray(TIFF tif, TIFFDirEntry dir, float[] v)
		{
			if(dir.tdir_count==1)
			{
				v[0]=BitConverter.ToSingle(BitConverter.GetBytes(dir.tdir_offset), 0);
				return true;
			}

			byte[] buf=new byte[dir.tdir_count*4];
			if(TIFFFetchData(tif, dir, buf)==0) return false;
			for(int i=0; i<dir.tdir_count; i++) v[i]=BitConverter.ToSingle(buf, i*4);
			return true;
		}

		// Fetch an array of DOUBLE values.
		static bool TIFFFetchDoubleArray(TIFF tif, TIFFDirEntry dir, double[] v)
		{
			byte[] buf=new byte[dir.tdir_count*8];
			if(TIFFFetchData(tif, dir, buf)==0) return false;
			for(int i=0; i<dir.tdir_count; i++) v[i]=BitConverter.ToDouble(buf, i*8);
			return true;
		}

		// Fetch an array of ANY values. The actual values are
		// returned as doubles which should be able hold all the
		// types. Yes, there really should be an tany_t to avoid
		// this potential non-portability ... Note in particular
		// that we assume that the double return value vector is
		// large enough to read in any fundamental type. We use
		// that vector as a buffer to read in the base type vector
		// and then convert it in place to double (from end
		// to front of course).
		static bool TIFFFetchAnyArray(TIFF tif, TIFFDirEntry dir, double[] v)
		{
			switch((TIFFDataType)dir.tdir_type)
			{
				case TIFFDataType.TIFF_BYTE:
					{
						byte[] buf=new byte[dir.tdir_count];
						if(!TIFFFetchByteArray(tif, dir, buf)) return false;
						for(int i=0; i<dir.tdir_count; i++) v[i]=buf[i];
					}
					break;
				case TIFFDataType.TIFF_SBYTE:
					{
						sbyte[] buf=new sbyte[dir.tdir_count];
						if(!TIFFFetchByteArray(tif, dir, buf)) return false;
						for(int i=0; i<dir.tdir_count; i++) v[i]=buf[i];
					}
					break;
				case TIFFDataType.TIFF_SHORT:
					{
						ushort[] buf=new ushort[dir.tdir_count];
						if(!TIFFFetchShortArray(tif, dir, buf)) return false;
						for(int i=0; i<dir.tdir_count; i++) v[i]=buf[i];
					}
					break;
				case TIFFDataType.TIFF_SSHORT:
					{
						short[] buf=new short[dir.tdir_count];
						if(!TIFFFetchShortArray(tif, dir, buf)) return false;
						for(int i=0; i<dir.tdir_count; i++) v[i]=buf[i];
					}
					break;
				case TIFFDataType.TIFF_LONG:
					{
						uint[] buf=new uint[dir.tdir_count];
						if(!TIFFFetchLongArray(tif, dir, buf)) return false;
						for(int i=0; i<dir.tdir_count; i++) v[i]=buf[i];
					}
					break;
				case TIFFDataType.TIFF_SLONG:
					{
						int[] buf=new int[dir.tdir_count];
						if(!TIFFFetchLongArray(tif, dir, buf)) return false;
						for(int i=0; i<dir.tdir_count; i++) v[i]=buf[i];
					}
					break;
				case TIFFDataType.TIFF_RATIONAL:
				case TIFFDataType.TIFF_SRATIONAL:
					return TIFFFetchRationalArray(tif, dir, v);
				case TIFFDataType.TIFF_FLOAT:
					{
						float[] buf=new float[dir.tdir_count];
						if(!TIFFFetchFloatArray(tif, dir, buf)) return false;
						for(int i=0; i<dir.tdir_count; i++) v[i]=buf[i];
					}
					break;
				case TIFFDataType.TIFF_DOUBLE:
					return TIFFFetchDoubleArray(tif, dir, v);
				default:
					// TIFF_NOTYPE
					// TIFF_ASCII
					// TIFF_UNDEFINED
					TIFFErrorExt(tif.tif_clientdata, tif.tif_name, "cannot read TIFF_ANY type {0} for field \"{1}\"", dir.tdir_type, TIFFFieldWithTag(tif, (TIFFTAG)dir.tdir_tag).field_name);
					return false;
			}
			return true;
		}

		// Fetch a tag that is not handled by special case code.
		static bool TIFFFetchNormalTag(TIFF tif, TIFFDirEntry dp)
		{
			bool ok=false;
			TIFFFieldInfo fip=TIFFFieldWithTag(tif, (TIFFTAG)dp.tdir_tag);

			if(dp.tdir_count>1)
			{
				// array of values
				object cp=null;

				try
				{
					switch((TIFFDataType)dp.tdir_type)
					{
						case TIFFDataType.TIFF_UNDEFINED:
						case TIFFDataType.TIFF_BYTE:
							cp=new byte[dp.tdir_count];
							ok=TIFFFetchByteArray(tif, dp, (byte[])cp);
							break;
						case TIFFDataType.TIFF_SBYTE:
							cp=new sbyte[dp.tdir_count];
							ok=TIFFFetchByteArray(tif, dp, (sbyte[])cp);
							break;
						case TIFFDataType.TIFF_SHORT:
							cp=new ushort[dp.tdir_count];
							ok=TIFFFetchShortArray(tif, dp, (ushort[])cp);
							break;
						case TIFFDataType.TIFF_SSHORT:
							cp=new short[dp.tdir_count];
							ok=TIFFFetchShortArray(tif, dp, (short[])cp);
							break;
						case TIFFDataType.TIFF_LONG:
							cp=new uint[dp.tdir_count];
							ok=TIFFFetchLongArray(tif, dp, (uint[])cp);
							break;
						case TIFFDataType.TIFF_SLONG:
							cp=new int[dp.tdir_count];
							ok=TIFFFetchLongArray(tif, dp, (int[])cp);
							break;
						case TIFFDataType.TIFF_RATIONAL:
						case TIFFDataType.TIFF_SRATIONAL:
							cp=new double[dp.tdir_count];
							ok=TIFFFetchRationalArray(tif, dp, (double[])cp);
							break;
						case TIFFDataType.TIFF_FLOAT:
							cp=new float[dp.tdir_count];
							ok=TIFFFetchFloatArray(tif, dp, (float[])cp);
							break;
						case TIFFDataType.TIFF_DOUBLE:
							cp=new double[dp.tdir_count];
							ok=TIFFFetchDoubleArray(tif, dp, (double[])cp);
							break;
						case TIFFDataType.TIFF_ASCII:
							// Some vendors write strings w/o the trailing
							// NIL byte, so always append one just in case.
							string str;
							ok=TIFFFetchString(tif, dp, out str)!=0;
							if(ok)
							{
								str=str.TrimEnd('\0');
								str+='\0'; // XXX paranoid
								cp=str;
							}
							break;
					}
				}
				catch
				{
					TIFFErrorExt(tif.tif_clientdata, tif.tif_name, "No space to fetch tag value");
				}

				if(ok)
				{
					if(fip.field_passcount) ok=TIFFSetField(tif, (TIFFTAG)dp.tdir_tag, (TIFFDataType)dp.tdir_type, dp.tdir_count, cp);
					else
					{
						//if(cp is Array)
						//{
						//    Array arr=cp as Array;
						//    object[] ap=new object[arr.Length];
						//    int i=0;
						//    foreach(object a in arr) ap[i++]=a;

						//    ok=TIFFSetField(tif, (TIFFTAG)dp.tdir_tag, (TIFFDataType)dp.tdir_type, ap);
						//}
						//else
						ok=TIFFSetField(tif, (TIFFTAG)dp.tdir_tag, (TIFFDataType)dp.tdir_type, cp);
					}
				}
			}
			else if(CheckDirCount(tif, dp, 1))
			{ // singleton value
				switch((TIFFDataType)dp.tdir_type)
				{
					case TIFFDataType.TIFF_UNDEFINED:
					case TIFFDataType.TIFF_BYTE:
					case TIFFDataType.TIFF_SBYTE:
					case TIFFDataType.TIFF_SHORT:
					case TIFFDataType.TIFF_SSHORT:
						// If the tag is also acceptable as a LONG or SLONG
						// then TIFFSetField will expect an uint parameter
						// passed to it.
						//
						// NB:	We used TIFFFieldWithTag here (see above) knowing that
						//		it returns us the first entry in the table
						//		for the tag and that that entry is for the
						//		widest potential data type the tag may have.
						TIFFDataType type=fip.field_type;
						if(type!=TIFFDataType.TIFF_LONG&&type!=TIFFDataType.TIFF_SLONG)
						{
							ushort v=(ushort)((tif.tif_header.tiff_magic==TIFF_BIGENDIAN)?(dp.tdir_offset>>tif_typeshift[dp.tdir_type])&tif_typemask[dp.tdir_type]:dp.tdir_offset&tif_typemask[dp.tdir_type]);
							ok=(fip.field_passcount?TIFFSetField(tif, (TIFFTAG)dp.tdir_tag, (TIFFDataType)dp.tdir_type, 1, v):TIFFSetField(tif, (TIFFTAG)dp.tdir_tag, (TIFFDataType)dp.tdir_type, v));
							break;
						}
						goto case TIFFDataType.TIFF_LONG; // fall thru...
					case TIFFDataType.TIFF_LONG:
					case TIFFDataType.TIFF_SLONG:
						{
							uint v32=(uint)((tif.tif_header.tiff_magic==TIFF_BIGENDIAN)?(dp.tdir_offset>>tif_typeshift[dp.tdir_type])&tif_typemask[dp.tdir_type]:dp.tdir_offset&tif_typemask[dp.tdir_type]);
							ok=(fip.field_passcount?TIFFSetField(tif, (TIFFTAG)dp.tdir_tag, (TIFFDataType)dp.tdir_type, 1, v32):TIFFSetField(tif, (TIFFTAG)dp.tdir_tag, (TIFFDataType)dp.tdir_type, v32));
						}
						break;
					case TIFFDataType.TIFF_RATIONAL:
					case TIFFDataType.TIFF_SRATIONAL:
						{
							double v=TIFFFetchRational(tif, dp);
							ok=(fip.field_passcount?TIFFSetField(tif, (TIFFTAG)dp.tdir_tag, (TIFFDataType)dp.tdir_type, 1, v):TIFFSetField(tif, (TIFFTAG)dp.tdir_tag, (TIFFDataType)dp.tdir_type, v));
						}
						break;
					case TIFFDataType.TIFF_FLOAT:
						{
							float v=TIFFFetchFloat(tif, dp);
							ok=(fip.field_passcount?TIFFSetField(tif, (TIFFTAG)dp.tdir_tag, (TIFFDataType)dp.tdir_type, 1, v):TIFFSetField(tif, (TIFFTAG)dp.tdir_tag, (TIFFDataType)dp.tdir_type, v));
						}
						break;
					case TIFFDataType.TIFF_DOUBLE:
						{
							double v=TIFFFetchDouble(tif, dp);
							ok=(fip.field_passcount?TIFFSetField(tif, (TIFFTAG)dp.tdir_tag, (TIFFDataType)dp.tdir_type, 1, v):TIFFSetField(tif, (TIFFTAG)dp.tdir_tag, (TIFFDataType)dp.tdir_type, v));
						}
						break;
					case TIFFDataType.TIFF_ASCII:
						{
							string str;
							ok=TIFFFetchString(tif, dp, out str)!=0;
							if(ok)
							{
								str=str.TrimEnd('\0');
								str+='\0'; // XXX paranoid
								ok=(fip.field_passcount?TIFFSetField(tif, (TIFFTAG)dp.tdir_tag, (TIFFDataType)dp.tdir_type, 1, str):TIFFSetField(tif, (TIFFTAG)dp.tdir_tag, (TIFFDataType)dp.tdir_type, str));
							}
						}
						break;
				}
			}
			return ok;
		}

		// Fetch samples/pixel short values for
		// the specified tag and verify that
		// all values are the same.
		static bool TIFFFetchPerSampleShorts(TIFF tif, TIFFDirEntry dir, out ushort pl)
		{
			ushort samples=tif.tif_dir.td_samplesperpixel;

			pl=0;
			if(!CheckDirCount(tif, dir, samples)) return false;

			ushort[] v=null;
			try
			{
				v=new ushort[dir.tdir_count];
			}
			catch
			{
				TIFFErrorExt(tif.tif_clientdata, tif.tif_name, "No space to fetch per-sample values");
				return false;
			}

			if(TIFFFetchShortArray(tif, dir, v))
			{
				uint check_count=dir.tdir_count;
				if(samples<check_count) check_count=samples;

				for(uint i=1; i<check_count; i++)
				{
					if(v[i]!=v[0])
					{
						TIFFErrorExt(tif.tif_clientdata, tif.tif_name, "Cannot handle different per-sample values for field \"{0}\"", TIFFFieldWithTag(tif, (TIFFTAG)dir.tdir_tag).field_name);
						return false;
					}
				}
				pl=v[0];
			}

			return true;
		}

		// Fetch samples/pixel long values for 
		// the specified tag and verify that
		// all values are the same.
		static bool TIFFFetchPerSampleLongs(TIFF tif, TIFFDirEntry dir, out uint pl)
		{
			ushort samples=tif.tif_dir.td_samplesperpixel;

			pl=0;
			if(!CheckDirCount(tif, dir, samples)) return false;

			uint[] v=null;
			try
			{
				v=new uint[dir.tdir_count];
			}
			catch
			{
				TIFFErrorExt(tif.tif_clientdata, tif.tif_name, "No space to fetch per-sample values");
				return false;
			}

			if(TIFFFetchLongArray(tif, dir, v))
			{
				uint check_count=dir.tdir_count;
				if(samples<check_count) check_count=samples;

				for(uint i=1; i<check_count; i++)
				{
					if(v[i]!=v[0])
					{
						TIFFErrorExt(tif.tif_clientdata, tif.tif_name, "Cannot handle different per-sample values for field \"{0}\"", TIFFFieldWithTag(tif, (TIFFTAG)dir.tdir_tag).field_name);
						return false;
					}
				}
				pl=v[0];
			}

			return true;
		}

		// Fetch samples/pixel ANY values for the specified tag and verify that all
		// values are the same.
		static bool TIFFFetchPerSampleAnys(TIFF tif, TIFFDirEntry dir, out double pl)
		{
			ushort samples=tif.tif_dir.td_samplesperpixel;

			pl=0;
			if(!CheckDirCount(tif, dir, samples)) return false;

			double[] v=null;
			try
			{
				v=new double[dir.tdir_count];
			}
			catch
			{
				TIFFErrorExt(tif.tif_clientdata, tif.tif_name, "No space to fetch per-sample values");
				return false;
			}

			if(TIFFFetchAnyArray(tif, dir, v))
			{
				uint check_count=dir.tdir_count;
				if(samples<check_count) check_count=samples;

				for(uint i=1; i<check_count; i++)
				{
					if(v[i]!=v[0])
					{
						TIFFErrorExt(tif.tif_clientdata, tif.tif_name, "Cannot handle different per-sample values for field \"{0}\"", TIFFFieldWithTag(tif, (TIFFTAG)dir.tdir_tag).field_name);
						return false;
					}
				}
				pl=v[0];
			}

			return true;
		}

		// Fetch a set of offsets or lengths.
		// While this routine says "strips", in fact it's also used for tiles.
		static bool TIFFFetchStripThing(TIFF tif, TIFFDirEntry dir, uint nstrips, ref uint[] lp)
		{
			if(!CheckDirCount(tif, dir, nstrips)) return false;

			// Allocate space for strip information.
			if(lp==null)
			{
				try
				{
					lp=new uint[nstrips];
				}
				catch
				{
					TIFFErrorExt(tif.tif_clientdata, tif.tif_name, "No space for strip array");
					return false;
				}
			}
			else for(int i=0; i<nstrips; i++) lp[i]=0;

			if(dir.tdir_type==(ushort)TIFFDataType.TIFF_SHORT)
			{
				// Handle uint16=>uint32 expansion.
				ushort[] dp=null;
				try
				{
					dp=new ushort[dir.tdir_count];
				}
				catch
				{
					TIFFErrorExt(tif.tif_clientdata, tif.tif_name, "No space to fetch strip tag");
					return false;
				}

				if(TIFFFetchShortArray(tif, dir, dp))
				{
					for(int i=0; i<nstrips&&i<(int)dir.tdir_count; i++) lp[i]=dp[i];
				}
				return true;
			}

			if(nstrips!=(int)dir.tdir_count)
			{
				// Special case to incorrect length

				uint[] dp=null;
				try
				{
					dp=new uint[dir.tdir_count];
				}
				catch
				{
					TIFFErrorExt(tif.tif_clientdata, tif.tif_name, "No space to fetch strip tag");
					return false;
				}

				if(TIFFFetchLongArray(tif, dir, dp))
				{
					for(int i=0; i<nstrips&&i<(int)dir.tdir_count; i++) lp[i]=dp[i];
				}
			}

			return TIFFFetchLongArray(tif, dir, lp);
		}

		// Fetch and set the RefBlackWhite tag.
		static bool TIFFFetchRefBlackWhite(TIFF tif, TIFFDirEntry dir)
		{
			if(dir.tdir_type==(ushort)TIFFDataType.TIFF_RATIONAL) return TIFFFetchNormalTag(tif, dir);

			// Handle LONG's for backward compatibility.
			uint[] cp=null;
			double[] fp=null;
			try
			{
				cp=new uint[dir.tdir_count];
				fp=new double[dir.tdir_count];
			}
			catch
			{
				TIFFErrorExt(tif.tif_clientdata, tif.tif_name, "No space for \"ReferenceBlackWhite\" array");
				return false;
			}

			if(!TIFFFetchLongArray(tif, dir, cp)) return false;
			for(int i=0; i<dir.tdir_count; i++) fp[i]=cp[i];
			if(!TIFFSetField(tif, (TIFFTAG)dir.tdir_tag, (TIFFDataType)dir.tdir_type, fp)) return false;

			return true;
		}

		// Fetch and set the SubjectDistance EXIF tag.
		static bool TIFFFetchSubjectDistance(TIFF tif, TIFFDirEntry dir)
		{
			if(dir.tdir_count!=1||dir.tdir_type!=(ushort)TIFFDataType.TIFF_RATIONAL)
			{
				TIFFWarningExt(tif.tif_clientdata, tif.tif_name, "incorrect count or type for SubjectDistance, tag ignored");
				return false;
			}

			bool ok=false;
			byte[] buf=new byte[8];
			if(TIFFFetchData(tif, dir, buf)!=0)
			{
				uint l0=BitConverter.ToUInt32(buf, 0);
				uint l1=BitConverter.ToUInt32(buf, 4);
				double v;
				if(cvtRational(tif, dir, l0, l1, out v))
				{
					// XXX: Numerator 0xFFFFFFFF means that we have infinite
					// distance. Indicate that with a negative floating point
					// SubjectDistance value.
					ok=TIFFSetField(tif, (TIFFTAG)dir.tdir_tag, (l0!=0xFFFFFFFF)?v:-v);
				}
			}

			return ok;
		}

		// Replace a single strip (tile) of uncompressed data by
		// multiple strips (tiles), each approximately 8Kbytes.
		// This is useful for dealing with large images or
		// for dealing with machines with a limited amount
		// memory.
		static void ChopUpSingleUncompressedStrip(TIFF tif)
		{
			TIFFDirectory td=tif.tif_dir;
			uint bytecount=td.td_stripbytecount[0];
			uint offset=td.td_stripoffset[0];
			int rowbytes=TIFFVTileSize(tif, 1), stripbytes;
			uint strip, nstrips, rowsperstrip;
			uint[] newcounts=null, newoffsets=null;

			// Make the rows hold at least one scanline, but fill specified amount
			// of data if possible.
			if(rowbytes>STRIP_SIZE_DEFAULT)
			{
				stripbytes=rowbytes;
				rowsperstrip=1;
			}
			else if(rowbytes>0)
			{
				rowsperstrip=(uint)(STRIP_SIZE_DEFAULT/rowbytes);
				stripbytes=(int)(rowbytes*rowsperstrip);
			}
			else return;

			// never increase the number of strips in an image
			if(rowsperstrip>=td.td_rowsperstrip) return;

			nstrips=TIFFhowmany(bytecount, (uint)stripbytes);
			if(nstrips==0) return;// something is wonky, do nothing.

			try
			{
				newcounts=new uint[nstrips];
			}
			catch
			{
				TIFFErrorExt(tif.tif_clientdata, tif.tif_name, "No space for chopped \"StripByteCounts\" array");
			}

			try
			{
				newoffsets=new uint[nstrips];
			}
			catch
			{
				TIFFErrorExt(tif.tif_clientdata, tif.tif_name, "No space for chopped \"StripOffsets\" array");
			}

			if(newcounts==null||newoffsets==null)
			{
				// Unable to allocate new strip information, give
				// up and use the original one strip information.
				return;
			}

			// Fill the strip information arrays with new bytecounts and offsets
			// that reflect the broken-up format.
			for(strip=0; strip<nstrips; strip++)
			{
				if(stripbytes>bytecount) stripbytes=(int)bytecount;
				newcounts[strip]=(uint)stripbytes;
				newoffsets[strip]=offset;
				offset+=(uint)stripbytes;
				bytecount-=(uint)stripbytes;
			}

			// Replace old single strip info with multi-strip info.
			td.td_stripsperimage=td.td_nstrips=nstrips;
			TIFFSetField(tif, TIFFTAG.ROWSPERSTRIP, rowsperstrip);

			td.td_stripbytecount=newcounts;
			td.td_stripoffset=newoffsets;
			td.td_stripbytecountsorted=1;
		}
	}
}
