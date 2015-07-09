// tif_getimage.cs
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

// TIFF Library
//
// Read and return a packed RGBA image.

using System;
using System.Collections.Generic;
using System.Text;

namespace Free.Ports.LibTiff
{
	public static partial class libtiff
	{
		const string photoTag="PhotometricInterpretation";

		// Helper constants used in Orientation tag handling
		const int FLIP_VERTICALLY=0x01;
		const int FLIP_HORIZONTALLY=0x02;

		static byte W2B(ushort w) { return (byte)(w>>8); }

		// Check the image to see if TIFFReadRGBAImage can deal with it.
		// 1/0 is returned according to whether or not the image can
		// be handled. If 0 is returned, emsg contains the reason
		// why it is being rejected.
		public static bool TIFFRGBAImageOK(TIFF tif, out string emsg)
		{
			TIFFDirectory td=tif.tif_dir;
			PHOTOMETRIC photometric;

			if(!tif.tif_decodestatus)
			{
				emsg="Sorry, requested compression method is not configured";
				return false;
			}

			switch(td.td_bitspersample)
			{
				case 1:
				case 2:
				case 4:
				case 8:
				case 16: break;
				default: emsg=string.Format("Sorry, can not handle images with {0}-bit samples", td.td_bitspersample);
					return false;
			}

			object[] ap=new object[2];

			int colorchannels=td.td_samplesperpixel-td.td_extrasamples;
			if(!TIFFGetField(tif, TIFFTAG.PHOTOMETRIC, ap))
			{
				switch(colorchannels)
				{
					case 1: photometric=PHOTOMETRIC.MINISBLACK; break;
					case 3: photometric=PHOTOMETRIC.RGB; break;
					default: emsg=string.Format("Missing needed {0} tag", photoTag); return false;
				}
			}
			else photometric=(PHOTOMETRIC)__GetAsUshort(ap, 0);

			switch(photometric)
			{
				case PHOTOMETRIC.MINISWHITE:
				case PHOTOMETRIC.MINISBLACK:
				case PHOTOMETRIC.PALETTE:
					if(td.td_planarconfig==PLANARCONFIG.CONTIG&&td.td_samplesperpixel!=1&&td.td_bitspersample<8)
					{
						emsg=string.Format("Sorry, can not handle contiguous data with {0}={1}, and {2}={3} and Bits/Sample={4}",
							photoTag, photometric, "Samples/pixel", td.td_samplesperpixel, td.td_bitspersample);
						return false;
					}

					// We should likely validate that any extra samples are either
					// to be ignored, or are alpha, and if alpha we should try to use
					// them. But for now we won't bother with this.
					break;
				case PHOTOMETRIC.YCBCR:
					// TODO: if at all meaningful and useful, make more complete
					// support check here, or better still, refactor to let supporting
					// code decide whether there is support and what meaningfull
					// error to return
					break;
				case PHOTOMETRIC.RGB:
					if(colorchannels<3)
					{
						emsg=string.Format("Sorry, can not handle RGB image with {0}={1}", "Color channels", colorchannels);
						return false;
					}
					break;
				case PHOTOMETRIC.SEPARATED:
					{
						TIFFGetFieldDefaulted(tif, TIFFTAG.INKSET, ap);
						INKSET inkset=(INKSET)__GetAsUshort(ap, 0);
						if(inkset!=INKSET.CMYK)
						{
							emsg=string.Format("Sorry, can not handle separated image with {0}={1}", "InkSet", inkset);
							return false;
						}
						if(td.td_samplesperpixel<4)
						{
							emsg=string.Format("Sorry, can not handle separated image with {0}={1}", "Samples/pixel", td.td_samplesperpixel);
							return false;
						}
						break;
					}
				case PHOTOMETRIC.LOGL:
					if(td.td_compression!=COMPRESSION.SGILOG)
					{
						emsg=string.Format("Sorry, LogL data must have {0}={1}", "Compression", COMPRESSION.SGILOG);
						return false;
					}
					break;
				case PHOTOMETRIC.LOGLUV:
					if(td.td_compression!=COMPRESSION.SGILOG&&
						td.td_compression!=COMPRESSION.SGILOG24)
					{
						emsg=string.Format("Sorry, LogLuv data must have {0}={1} or {2}", "Compression", COMPRESSION.SGILOG, COMPRESSION.SGILOG24);
						return false;
					}
					if(td.td_planarconfig!=PLANARCONFIG.CONTIG)
					{
						emsg=string.Format("Sorry, can not handle LogLuv images with {0}={1}", "Planarconfiguration", td.td_planarconfig);
						return false;
					}
					break;
				case PHOTOMETRIC.CIELAB: break;
				default: emsg=string.Format("Sorry, can not handle image with {0}={1}", photoTag, photometric);
					return false;
			}

			emsg="";
			return true;
		}

		public static void TIFFRGBAImageEnd(TIFFRGBAImage img)
		{
			img.Map=null;
			img.BWmap=img.PALmap=null;
			img.ycbcr=null;
			img.cielab=null;

			img.redcmap=img.greencmap=img.bluecmap=null;
		}

		static bool isCCITTCompression(TIFF tif)
		{
			object[] ap=new object[2];
			TIFFGetField(tif, TIFFTAG.COMPRESSION, ap);
			COMPRESSION compress=(COMPRESSION)__GetAsUshort(ap, 0);

			return (compress==COMPRESSION.CCITTFAX3||compress==COMPRESSION.CCITTFAX4||compress==COMPRESSION.CCITTRLE||compress==COMPRESSION.CCITTRLEW);
		}

		public static bool TIFFRGBAImageBegin(TIFFRGBAImage img, TIFF tif, bool stop, out string emsg)
		{
			// Initialize to normal values
			img.row_offset=0;
			img.col_offset=0;
			img.redcmap=img.greencmap=img.bluecmap=null;
			img.req_orientation=ORIENTATION.BOTLEFT;	// It is the default

			img.tif=tif;
			img.stoponerr=stop;

			object[] ap=new object[4];
			TIFFGetFieldDefaulted(tif, TIFFTAG.BITSPERSAMPLE, ap);
			img.bitspersample=__GetAsUshort(ap, 0);

			switch(img.bitspersample)
			{
				case 1:
				case 2:
				case 4:
				case 8:
				case 16: break;
				default:
					emsg=string.Format("Sorry, can not handle images with {0}-bit samples", img.bitspersample);
					return false;
			}
			img.alpha=0;
			TIFFGetFieldDefaulted(tif, TIFFTAG.SAMPLESPERPIXEL, ap);
			img.samplesperpixel=__GetAsUshort(ap, 0);

			TIFFGetFieldDefaulted(tif, TIFFTAG.EXTRASAMPLES, ap);
			ushort extrasamples=__GetAsUshort(ap, 0);
			ushort[] sampleinfo=(ushort[])ap[1];

			if(extrasamples>=1)
			{
				switch((EXTRASAMPLE)sampleinfo[0])
				{
					case EXTRASAMPLE.UNSPECIFIED:	// Workaround for some images without
						if(img.samplesperpixel>3)	// correct info about alpha channel
							img.alpha=EXTRASAMPLE.ASSOCALPHA;
						break;
					case EXTRASAMPLE.ASSOCALPHA:	// data is pre-multiplied
					case EXTRASAMPLE.UNASSALPHA:	// data is not pre-multiplied
						img.alpha=(EXTRASAMPLE)sampleinfo[0];
						break;
				}
			}

#if DEFAULT_EXTRASAMPLE_AS_ALPHA
			if(!TIFFGetField(tif, TIFFTAG.PHOTOMETRIC, ap)) img.photometric=PHOTOMETRIC.MINISWHITE;
			else img.photometric=(PHOTOMETRIC)__GetAsUshort(ap, 0);

			if(extrasamples==0&&img.samplesperpixel==4&&img.photometric==PHOTOMETRIC.RGB)
			{
				img.alpha=EXTRASAMPLE.ASSOCALPHA;
				extrasamples=1;
			}
#endif

			int colorchannels=img.samplesperpixel-extrasamples;
			TIFFGetFieldDefaulted(tif, TIFFTAG.COMPRESSION, ap);
			COMPRESSION compress=(COMPRESSION)__GetAsUshort(ap, 0);

			TIFFGetFieldDefaulted(tif, TIFFTAG.PLANARCONFIG, ap);
			PLANARCONFIG planarconfig=(PLANARCONFIG)__GetAsUshort(ap, 0);

			if(!TIFFGetField(tif, TIFFTAG.PHOTOMETRIC, ap))
			{
				switch(colorchannels)
				{
					case 1:
						if(isCCITTCompression(tif)) img.photometric=PHOTOMETRIC.MINISWHITE;
						else img.photometric=PHOTOMETRIC.MINISBLACK;
						break;
					case 3:
						img.photometric=PHOTOMETRIC.RGB;
						break;
					default:
						emsg=string.Format("Missing needed {0} tag", photoTag);
						return false;
				}
			}
			else img.photometric=(PHOTOMETRIC)__GetAsUshort(ap, 0);

			switch(img.photometric)
			{
				case PHOTOMETRIC.PALETTE:
					ushort[] red_orig, green_orig, blue_orig;
					if(!TIFFGetField(tif, TIFFTAG.COLORMAP, ap))
					{
						emsg="Missing required \"Colormap\" tag";
						return false;
					}

					red_orig=ap[0] as ushort[];
					green_orig=ap[1] as ushort[];
					blue_orig=ap[2] as ushort[];

					// copy the colormaps so we can modify them
					int n_color=(1<<img.bitspersample);
					try
					{
						img.redcmap=new ushort[n_color];
						img.greencmap=new ushort[n_color];
						img.bluecmap=new ushort[n_color];
					}
					catch
					{
						emsg="Out of memory for colormap copy";
						return false;
					}

					Array.Copy(red_orig, img.redcmap, n_color);
					Array.Copy(green_orig, img.greencmap, n_color);
					Array.Copy(blue_orig, img.bluecmap, n_color);

					goto case PHOTOMETRIC.MINISBLACK; // fall thru...
				case PHOTOMETRIC.MINISWHITE:
				case PHOTOMETRIC.MINISBLACK:
					if(planarconfig==PLANARCONFIG.CONTIG&&img.samplesperpixel!=1&&img.bitspersample<8)
					{
						emsg=string.Format("Sorry, can not handle contiguous data with {0}={1}, and {2}={3} and Bits/Sample={4}", photoTag, img.photometric, "Samples/pixel", img.samplesperpixel, img.bitspersample);
						return false;
					}
					break;
				case PHOTOMETRIC.YCBCR:
					// It would probably be nice to have a reality check here.
					if(planarconfig==PLANARCONFIG.CONTIG)
					{
						// can rely on libjpeg to convert to RGB
						// XXX should restore current state on exit
						switch(compress)
						{
							case COMPRESSION.JPEG:
								// TODO: when complete tests verify complete desubsampling
								// and YCbCr handling, remove use of TIFFTAG_JPEGCOLORMODE in
								// favor of tif_getimage.c native handling
								TIFFSetField(tif, TIFFTAG.JPEGCOLORMODE, JPEGCOLORMODE.RGB);
								img.photometric=PHOTOMETRIC.RGB;
								break;
							default: break; // do nothing
						}
					}
					// TODO: if at all meaningful and useful, make more complete
					// support check here, or better still, refactor to let supporting
					// code decide whether there is support and what meaningfull
					// error to return
					break;
				case PHOTOMETRIC.RGB:
					if(colorchannels<3)
					{
						emsg=string.Format("Sorry, can not handle RGB image with {0}={1}", "Color channels", colorchannels);
						return false;
					}
					break;
				case PHOTOMETRIC.SEPARATED:
					{
						TIFFGetFieldDefaulted(tif, TIFFTAG.INKSET, ap);
						INKSET inkset=(INKSET)__GetAsUshort(ap, 0);

						if(inkset!=INKSET.CMYK)
						{
							emsg=string.Format("Sorry, can not handle separated image with {0}={1}", "InkSet", inkset);
							return false;
						}
						if(img.samplesperpixel<4)
						{
							emsg=string.Format("Sorry, can not handle separated image with {0}={1}", "Samples/pixel", img.samplesperpixel);
							return false;
						}
					}
					break;
				case PHOTOMETRIC.LOGL:
					if(compress!=COMPRESSION.SGILOG)
					{
						emsg=string.Format("Sorry, LogL data must have{0}={1}", "Compression", COMPRESSION.SGILOG);
						return false;
					}
					TIFFSetField(tif, TIFFTAG.SGILOGDATAFMT, SGILOGDATAFMT._8BIT);
					img.photometric=PHOTOMETRIC.MINISBLACK;	// little white lie
					img.bitspersample=8;
					break;
				case PHOTOMETRIC.LOGLUV:
					if(compress!=COMPRESSION.SGILOG&&compress!=COMPRESSION.SGILOG24)
					{
						emsg=string.Format("Sorry, LogLuv data must have {0}={1} or {2}", "Compression", COMPRESSION.SGILOG, COMPRESSION.SGILOG24);
						return false;
					}
					if(planarconfig!=PLANARCONFIG.CONTIG)
					{
						emsg=string.Format("Sorry, can not handle LogLuv images with {0}={1}", "Planarconfiguration", planarconfig);
						return false;
					}
					TIFFSetField(tif, TIFFTAG.SGILOGDATAFMT, SGILOGDATAFMT._8BIT);
					img.photometric=PHOTOMETRIC.RGB;		// little white lie
					img.bitspersample=8;
					break;
				case PHOTOMETRIC.CIELAB:
					break;
				default:
					emsg=string.Format("Sorry, can not handle image with {0}={1}", photoTag, img.photometric);
					return false;
			}
			img.Map=null;
			img.BWmap=null;
			img.PALmap=null;
			img.ycbcr=null;
			img.cielab=null;

			TIFFGetField(tif, TIFFTAG.IMAGEWIDTH, ap); img.width=__GetAsUint(ap, 0);
			TIFFGetField(tif, TIFFTAG.IMAGELENGTH, ap); img.height=__GetAsUint(ap, 0);
			TIFFGetFieldDefaulted(tif, TIFFTAG.ORIENTATION, ap); img.orientation=(ORIENTATION)__GetAsUshort(ap, 0);

			img.isContig=!(planarconfig==PLANARCONFIG.SEPARATE&&colorchannels>1);
			if(img.isContig)
			{
				if(!PickContigCase(img))
				{
					emsg="Sorry, can not handle image";
					return false;
				}
			}
			else
			{
				if(!PickSeparateCase(img))
				{
					emsg="Sorry, can not handle image";
					return false;
				}
			}

			emsg="";
			return true;
		}

		public static bool TIFFRGBAImageGet(TIFFRGBAImage img, uint[] raster, uint raster_offset, uint w, uint h)
		{
			if(img.get==null)
			{
				TIFFErrorExt(img.tif.tif_clientdata, TIFFFileName(img.tif), "No \"get\" routine setup");
				return false;
			}

			if(img.contig==null&&img.separate==null)
			{
				TIFFErrorExt(img.tif.tif_clientdata, TIFFFileName(img.tif), "No \"put\" routine setupl; probably can not handle image format");
				return false;
			}

			return img.get(img, raster, raster_offset, w, h);
		}

		// Read the specified image into an ABGR-format rastertaking in account
		// specified orientation.
		public static bool TIFFReadRGBAImageOriented(TIFF tif, uint rwidth, uint rheight, uint[] raster, ORIENTATION orientation, bool stop)
		{
			string emsg="";
			TIFFRGBAImage img=new TIFFRGBAImage();
			bool ok;

			if(TIFFRGBAImageOK(tif, out emsg)&&TIFFRGBAImageBegin(img, tif, stop, out emsg))
			{
				img.req_orientation=orientation;
				// XXX verify rwidth and rheight against width and height
				ok=TIFFRGBAImageGet(img, raster, (rheight-img.height)*rwidth, rwidth, img.height);
				TIFFRGBAImageEnd(img);
			}
			else
			{
				TIFFErrorExt(tif.tif_clientdata, TIFFFileName(tif), emsg);
				ok=false;
			}

			return ok;
		}

		// Read the specified image into an ABGR-format raster. Use bottom left
		// origin for raster by default.
		public static bool TIFFReadRGBAImage(TIFF tif, uint rwidth, uint rheight, uint[] raster, bool stop)
		{
			return TIFFReadRGBAImageOriented(tif, rwidth, rheight, raster, ORIENTATION.BOTLEFT, stop);
		}

		static int setorientation(TIFFRGBAImage img)
		{
			switch(img.orientation)
			{
				case ORIENTATION.TOPLEFT:
				case ORIENTATION.LEFTTOP:
					if(img.req_orientation==ORIENTATION.TOPRIGHT||img.req_orientation==ORIENTATION.RIGHTTOP) return FLIP_HORIZONTALLY;
					else if(img.req_orientation==ORIENTATION.BOTRIGHT||img.req_orientation==ORIENTATION.RIGHTBOT) return FLIP_HORIZONTALLY|FLIP_VERTICALLY;
					else if(img.req_orientation==ORIENTATION.BOTLEFT||img.req_orientation==ORIENTATION.LEFTBOT) return FLIP_VERTICALLY;
					else return 0;
				case ORIENTATION.TOPRIGHT:
				case ORIENTATION.RIGHTTOP:
					if(img.req_orientation==ORIENTATION.TOPLEFT||img.req_orientation==ORIENTATION.LEFTTOP) return FLIP_HORIZONTALLY;
					else if(img.req_orientation==ORIENTATION.BOTRIGHT||img.req_orientation==ORIENTATION.RIGHTBOT) return FLIP_VERTICALLY;
					else if(img.req_orientation==ORIENTATION.BOTLEFT||img.req_orientation==ORIENTATION.LEFTBOT) return FLIP_HORIZONTALLY|FLIP_VERTICALLY;
					else return 0;
				case ORIENTATION.BOTRIGHT:
				case ORIENTATION.RIGHTBOT:
					if(img.req_orientation==ORIENTATION.TOPLEFT||img.req_orientation==ORIENTATION.LEFTTOP) return FLIP_HORIZONTALLY|FLIP_VERTICALLY;
					else if(img.req_orientation==ORIENTATION.TOPRIGHT||img.req_orientation==ORIENTATION.RIGHTTOP) return FLIP_VERTICALLY;
					else if(img.req_orientation==ORIENTATION.BOTLEFT||img.req_orientation==ORIENTATION.LEFTBOT) return FLIP_HORIZONTALLY;
					else return 0;
				case ORIENTATION.BOTLEFT:
				case ORIENTATION.LEFTBOT:
					if(img.req_orientation==ORIENTATION.TOPLEFT||img.req_orientation==ORIENTATION.LEFTTOP) return FLIP_VERTICALLY;
					else if(img.req_orientation==ORIENTATION.TOPRIGHT||img.req_orientation==ORIENTATION.RIGHTTOP) return FLIP_HORIZONTALLY|FLIP_VERTICALLY;
					else if(img.req_orientation==ORIENTATION.BOTRIGHT||img.req_orientation==ORIENTATION.RIGHTBOT) return FLIP_HORIZONTALLY;
					else return 0;
				default: return 0; // NOTREACHED

			}
		}

		// Get an tile-organized image that has
		//	PlanarConfiguration contiguous if SamplesPerPixel > 1
		// or
		//	SamplesPerPixel == 1
		static bool gtTileContig(TIFFRGBAImage img, uint[] raster, uint raster_offset, uint w, uint h)
		{
			TIFF tif=img.tif;
			byte[] buf=null;

			try
			{
				buf=new byte[TIFFTileSize(tif)];
			}
			catch
			{
				TIFFErrorExt(tif.tif_clientdata, TIFFFileName(tif), "No space for tile buffer");
				return false;
			}

			object[] ap=new object[2];
			TIFFGetField(tif, TIFFTAG.TILEWIDTH, ap); uint tw=__GetAsUint(ap, 0);
			TIFFGetField(tif, TIFFTAG.TILELENGTH, ap); uint th=__GetAsUint(ap, 0);

			int flip=setorientation(img);
			uint y;
			int toskew;
			if((flip&FLIP_VERTICALLY)!=0)
			{
				y=h-1;
				toskew=-(int)(tw+w);
			}
			else
			{
				y=0;
				toskew=-(int)(tw-w);
			}

			bool ret=true;
			uint nrow;
			tileContigRoutine put=img.contig;
			for(uint row=0; row<h; row+=nrow)
			{
				uint rowstoread=(uint)(th-(row+img.row_offset)%th);
				nrow=(row+rowstoread>h?h-row:rowstoread);
				for(uint col=0; col<w; col+=tw)
				{
					if(TIFFReadTile(tif, buf, (uint)(col+img.col_offset), (uint)(row+img.row_offset), 0, 0)<0&&img.stoponerr)
					{
						ret=false;
						break;
					}

					uint pos=(uint)(((row+img.row_offset)%th)*TIFFTileRowSize(tif));

					if(col+tw>w)
					{
						// Tile is clipped horizontally. Calculate
						// visible portion and skewing factors.
						uint npix=w-col;
						int fromskew=(int)(tw-npix);
						put(img, raster, raster_offset+y*w+col, col, y, npix, nrow, fromskew, toskew+fromskew, buf, pos);
					}
					else
					{
						put(img, raster, raster_offset+y*w+col, col, y, tw, nrow, 0, toskew, buf, pos);
					}
				}

				if((flip&FLIP_VERTICALLY)!=0) y-=nrow;
				else y+=nrow;
			}
			buf=null;

			if((flip&FLIP_HORIZONTALLY)!=0)
			{
				unsafe
				{
					fixed(uint* raster_=raster)
					{
						for(uint line=0; line<h; line++)
						{
							uint* left=raster_+raster_offset+(line*w);
							uint* right=left+w-1;

							while(left<right)
							{
								uint temp=*left;
								*left=*right;
								*right=temp;
								left++; right--;
							}
						}
					}
				}
			}

			return ret;
		}

		// Get an tile-organized image that has
		//	SamplesPerPixel > 1
		//	PlanarConfiguration separated
		// We assume that all such images are RGB.
		static bool gtTileSeparate(TIFFRGBAImage img, uint[] raster, uint raster_offset, uint w, uint h)
		{
			TIFF tif=img.tif;
			tileSeparateRoutine put=img.separate;
			EXTRASAMPLE alpha=img.alpha;

			byte[] p0, p1, p2, pa=null;
			int tilesize=TIFFTileSize(tif);
			try
			{
				p0=new byte[tilesize];
				p1=new byte[tilesize];
				p2=new byte[tilesize];
				if(alpha!=0) pa=new byte[tilesize];
			}
			catch
			{
				TIFFErrorExt(tif.tif_clientdata, TIFFFileName(tif), "No space for tile buffer");
				return false;
			}

			object[] ap=new object[2];
			TIFFGetField(tif, TIFFTAG.TILEWIDTH, ap); uint tw=__GetAsUint(ap, 0);
			TIFFGetField(tif, TIFFTAG.TILELENGTH, ap); uint th=__GetAsUint(ap, 0);

			int flip=setorientation(img);
			int toskew;
			uint y;
			if((flip&FLIP_VERTICALLY)!=0)
			{
				y=h-1;
				toskew=-(int)(tw+w);
			}
			else
			{
				y=0;
				toskew=-(int)(tw-w);
			}

			uint pos;

			bool ret=true;
			uint nrow;
			for(uint row=0; row<h; row+=nrow)
			{
				uint rowstoread=(uint)(th-(row+img.row_offset)%th);
				nrow=(row+rowstoread>h?h-row:rowstoread);
				for(uint col=0; col<w; col+=tw)
				{
					if(TIFFReadTile(tif, p0, (uint)(col+img.col_offset), (uint)(row+img.row_offset), 0, 0)<0&&img.stoponerr)
					{
						ret=false;
						break;
					}
					if(TIFFReadTile(tif, p1, (uint)(col+img.col_offset), (uint)(row+img.row_offset), 0, 1)<0&&img.stoponerr)
					{
						ret=false;
						break;
					}
					if(TIFFReadTile(tif, p2, (uint)(col+img.col_offset), (uint)(row+img.row_offset), 0, 2)<0&&img.stoponerr)
					{
						ret=false;
						break;
					}
					if(alpha!=0)
					{
						if(TIFFReadTile(tif, pa, (uint)(col+img.col_offset), (uint)(row+img.row_offset), 0, 3)<0&&img.stoponerr)
						{
							ret=false;
							break;
						}
					}

					pos=(uint)(((row+img.row_offset)%th)*TIFFTileRowSize(tif));

					if(col+tw>w)
					{
						// Tile is clipped horizontally. Calculate
						// visible portion and skewing factors.
						uint npix=w-col;
						int fromskew=(int)(tw-npix);
						put(img, raster, raster_offset+y*w+col, col, y, npix, nrow, fromskew, toskew+fromskew, p0, p1, p2, pa, pos);
					}
					else put(img, raster, raster_offset+y*w+col, col, y, tw, nrow, 0, toskew, p0, p1, p2, pa, pos);
				}

				if((flip&FLIP_VERTICALLY)!=0) y-=nrow;
				else y+=nrow;
			}

			if((flip&FLIP_HORIZONTALLY)!=0)
			{
				unsafe
				{
					fixed(uint* raster_=raster)
					{
						for(uint line=0; line<h; line++)
						{
							uint* left=raster_+raster_offset+(line*w);
							uint* right=left+w-1;

							while(left<right)
							{
								uint temp=*left;
								*left=*right;
								*right=temp;
								left++; right--;
							}
						}
					}
				}
			}

			return ret;
		}

		// Get a strip-organized image that has
		//	PlanarConfiguration contiguous if SamplesPerPixel > 1
		// or
		//	SamplesPerPixel == 1
		static bool gtStripContig(TIFFRGBAImage img, uint[] raster, uint raster_offset, uint w, uint h)
		{
			TIFF tif=img.tif;
			byte[] buf;

			try
			{
				buf=new byte[TIFFStripSize(tif)];
			}
			catch
			{
				TIFFErrorExt(tif.tif_clientdata, TIFFFileName(tif), "No space for strip buffer");
				return false;
			}

			int flip=setorientation(img);
			int toskew;
			uint y;
			if((flip&FLIP_VERTICALLY)!=0)
			{
				y=h-1;
				toskew=-(int)(w+w);
			}
			else
			{
				y=0;
				toskew=-(int)(w-w);
			}

			object[] ap=new object[2];
			TIFFGetFieldDefaulted(tif, TIFFTAG.ROWSPERSTRIP, ap); uint rowsperstrip=__GetAsUint(ap, 0);
			TIFFGetFieldDefaulted(tif, TIFFTAG.YCBCRSUBSAMPLING, ap);
			ushort subsamplinghor=__GetAsUshort(ap, 0);
			ushort subsamplingver=__GetAsUshort(ap, 1);

			tileContigRoutine put=img.contig;
			uint nrow;
			bool ret=true;
			int scanline=TIFFNewScanlineSize(tif);
			uint imagewidth=img.width;
			int fromskew=(int)(w<imagewidth?imagewidth-w:0);
			for(uint row=0; row<h; row+=nrow)
			{
				uint rowstoread=(uint)(rowsperstrip-(row+img.row_offset)%rowsperstrip);
				nrow=(row+rowstoread>h?h-row:rowstoread);
				uint nrowsub=nrow;
				if((nrowsub%subsamplingver)!=0) nrowsub+=subsamplingver-nrowsub%subsamplingver;
				if(TIFFReadEncodedStrip(tif, TIFFComputeStrip(tif, (uint)(row+img.row_offset), 0), buf, (int)(((row+img.row_offset)%rowsperstrip+nrowsub)*scanline))<0&&img.stoponerr)
				{
					ret=false;
					break;
				}

				uint pos=(uint)(((row+img.row_offset)%rowsperstrip)*scanline);
				put(img, raster, raster_offset+y*w, 0, y, w, nrow, fromskew, toskew, buf, pos);

				if((flip&FLIP_VERTICALLY)!=0) y-=nrow;
				else y+=nrow;
			}

			if((flip&FLIP_HORIZONTALLY)!=0)
			{
				unsafe
				{
					fixed(uint* raster_=raster)
					{
						for(uint line=0; line<h; line++)
						{
							uint* left=raster_+raster_offset+(line*w);
							uint* right=left+w-1;

							while(left<right)
							{
								uint temp=*left;
								*left=*right;
								*right=temp;
								left++; right--;
							}
						}
					}
				}
			}

			return ret;
		}

		// Get a strip-organized image with
		//	SamplesPerPixel > 1
		//	PlanarConfiguration separated
		// We assume that all such images are RGB.
		static bool gtStripSeparate(TIFFRGBAImage img, uint[] raster, uint raster_offset, uint w, uint h)
		{
			TIFF tif=img.tif;
			tileSeparateRoutine put=img.separate;
			EXTRASAMPLE alpha=img.alpha;

			byte[] p0, p1, p2, pa=null;
			int stripsize=TIFFStripSize(tif);
			try
			{
				p0=new byte[stripsize];
				p1=new byte[stripsize];
				p2=new byte[stripsize];
				if(alpha!=0) pa=new byte[stripsize];
			}
			catch
			{
				TIFFErrorExt(tif.tif_clientdata, TIFFFileName(tif), "No space for strip buffer");
				return false;
			}

			int flip=setorientation(img);
			int toskew;
			uint y;
			if((flip&FLIP_VERTICALLY)!=0)
			{
				y=h-1;
				toskew=-(int)(w+w);
			}
			else
			{
				y=0;
				toskew=-(int)(w-w);
			}

			object[] ap=new object[2];

			TIFFGetFieldDefaulted(tif, TIFFTAG.ROWSPERSTRIP, ap); uint rowsperstrip=__GetAsUint(ap, 0);

			uint nrow;
			uint imagewidth=img.width;
			bool ret=true;

			int scanline=TIFFScanlineSize(tif);
			int fromskew=(int)(w<imagewidth?imagewidth-w:0);

			for(uint row=0; row<h; row+=nrow)
			{
				uint rowstoread=(uint)(rowsperstrip-(row+img.row_offset)%rowsperstrip);
				nrow=(row+rowstoread>h?h-row:rowstoread);
				uint offset_row=(uint)(row+img.row_offset);
				if(TIFFReadEncodedStrip(tif, TIFFComputeStrip(tif, offset_row, 0), p0, (int)((row+img.row_offset)%rowsperstrip+nrow)*scanline)<0&&img.stoponerr)
				{
					ret=false;
					break;
				}
				if(TIFFReadEncodedStrip(tif, TIFFComputeStrip(tif, offset_row, 1), p1, (int)((row+img.row_offset)%rowsperstrip+nrow)*scanline)<0&&img.stoponerr)
				{
					ret=false;
					break;
				}
				if(TIFFReadEncodedStrip(tif, TIFFComputeStrip(tif, offset_row, 2), p2, (int)((row+img.row_offset)%rowsperstrip+nrow)*scanline)<0&&img.stoponerr)
				{
					ret=false;
					break;
				}
				if(alpha!=0)
				{
					if(TIFFReadEncodedStrip(tif, TIFFComputeStrip(tif, offset_row, 3), pa, (int)((row+img.row_offset)%rowsperstrip+nrow)*scanline)<0&&img.stoponerr)
					{
						ret=false;
						break;
					}
				}

				uint pos=(uint)(((row+img.row_offset)%rowsperstrip)*scanline);
				put(img, raster, raster_offset+y*w, 0, y, w, nrow, fromskew, toskew, p0, p1, p2, pa, pos);

				if((flip&FLIP_VERTICALLY)!=0) y-=nrow;
				else y+=nrow;
			}

			if((flip&FLIP_HORIZONTALLY)!=0)
			{
				unsafe
				{
					fixed(uint* raster_=raster)
					{
						for(uint line=0; line<h; line++)
						{
							uint* left=raster_+raster_offset+(line*w);
							uint* right=left+w-1;

							while(left<right)
							{
								uint temp=*left;
								*left=*right;
								*right=temp;
								left++; right--;
							}
						}
					}
				}
			}

			return ret;
		}

		// The following routines move decoded data returned
		// from the TIFF library into rasters filled with packed
		// ABGR pixels (i.e. suitable for passing to lrecwrite.)
		//
		// The routines have been created according to the most
		// important cases and optimized. PickContigCase and
		// PickSeparateCase analyze the parameters and select
		// the appropriate "get" and "put" routine to use.

		// 8-bit palette => colormap/RGB
		static void put8bitcmaptile(TIFFRGBAImage img, uint[] cp0, uint cp_offset, uint x, uint y, uint w, uint h, int fromskew, int toskew, byte[] pp0, uint pp_offset)
		{
			uint[][] PALmap=img.PALmap;
			int samplesperpixel=img.samplesperpixel;

			unsafe
			{
				fixed(uint* cp_=cp0)
				{
					uint* cp=cp_+cp_offset;
					fixed(byte* pp_=pp0)
					{
						byte* pp=pp_+pp_offset;

						while((h--)>0)
						{
							for(x=w; (x--)>0; )
							{
								*cp++=PALmap[*pp][0];
								pp+=samplesperpixel;
							}
							cp+=toskew;
							pp+=fromskew;
						}
					}
				}
			}
		}

		// 4-bit palette => colormap/RGB
		static void put4bitcmaptile(TIFFRGBAImage img, uint[] cp0, uint cp_offset, uint x, uint y, uint w, uint h, int fromskew, int toskew, byte[] pp0, uint pp_offset)
		{
			uint[][] PALmap=img.PALmap;

			unsafe
			{
				fixed(uint* cp_=cp0)
				{
					uint* cp=cp_+cp_offset;
					fixed(byte* pp_=pp0)
					{
						byte* pp=pp_+pp_offset;

						fromskew/=2;
						while((h--)>0)
						{
							uint _x;
							for(_x=w; _x>=2; _x-=2)
							{
								uint[] bw=PALmap[*pp++];
								*cp++=bw[0];
								*cp++=bw[1];
							}

							if(_x>0)
							{
								uint[] bw=PALmap[*pp++];
								*cp++=bw[0];
							}

							cp+=toskew;
							pp+=fromskew;
						}
					}
				}
			}
		}

		// 2-bit palette => colormap/RGB
		static void put2bitcmaptile(TIFFRGBAImage img, uint[] cp0, uint cp_offset, uint x, uint y, uint w, uint h, int fromskew, int toskew, byte[] pp0, uint pp_offset)
		{
			uint[][] PALmap=img.PALmap;

			unsafe
			{
				fixed(uint* cp_=cp0)
				{
					uint* cp=cp_+cp_offset;
					fixed(byte* pp_=pp0)
					{
						byte* pp=pp_+pp_offset;

						fromskew/=4;
						while((h--)>0)
						{
							uint _x;
							for(_x=w; _x>=4; _x-=4)
							{
								uint[] bw=PALmap[*pp++];
								*cp++=bw[0];
								*cp++=bw[1];
								*cp++=bw[2];
								*cp++=bw[3];
							}

							if(_x>0)
							{
								uint[] bw=PALmap[*pp++]; uint bw_ind=0;
								switch(_x)
								{
									case 3: *cp++=bw[bw_ind++]; goto case 2;
									case 2: *cp++=bw[bw_ind++]; goto case 1;
									case 1: *cp++=bw[bw_ind++]; break;
								}
							}

							cp+=toskew;
							pp+=fromskew;
						}
					}
				}
			}
		}

		// 1-bit palette => colormap/RGB
		static void put1bitcmaptile(TIFFRGBAImage img, uint[] cp0, uint cp_offset, uint x, uint y, uint w, uint h, int fromskew, int toskew, byte[] pp0, uint pp_offset)
		{
			uint[][] PALmap=img.PALmap;

			unsafe
			{
				fixed(uint* cp_=cp0)
				{
					uint* cp=cp_+cp_offset;
					fixed(byte* pp_=pp0)
					{
						byte* pp=pp_+pp_offset;

						fromskew/=8;
						while((h--)>0)
						{
							uint _x;
							for(_x=w; _x>=8; _x-=8)
							{
								uint[] bw=PALmap[*pp++];
								*cp++=bw[0];
								*cp++=bw[1];
								*cp++=bw[2];
								*cp++=bw[3];
								*cp++=bw[4];
								*cp++=bw[5];
								*cp++=bw[6];
								*cp++=bw[7];
							}

							if(_x>0)
							{
								uint[] bw=PALmap[*pp++]; uint bw_ind=0;
								switch(_x)
								{
									case 7: *cp++=bw[bw_ind++]; goto case 6;
									case 6: *cp++=bw[bw_ind++]; goto case 5;
									case 5: *cp++=bw[bw_ind++]; goto case 4;
									case 4: *cp++=bw[bw_ind++]; goto case 3;
									case 3: *cp++=bw[bw_ind++]; goto case 2;
									case 2: *cp++=bw[bw_ind++]; goto case 1;
									case 1: *cp++=bw[bw_ind++]; break;
								}
							}

							cp+=toskew;
							pp+=fromskew;
						}
					}
				}
			}
		}

		// 8-bit greyscale => colormap/RGB
		static void putgreytile(TIFFRGBAImage img, uint[] cp0, uint cp_offset, uint x, uint y, uint w, uint h, int fromskew, int toskew, byte[] pp0, uint pp_offset)
		{
			int samplesperpixel=img.samplesperpixel;
			uint[][] BWmap=img.BWmap;

			unsafe
			{
				fixed(uint* cp_=cp0)
				{
					uint* cp=cp_+cp_offset;
					fixed(byte* pp_=pp0)
					{
						byte* pp=pp_+pp_offset;

						while((h--)>0)
						{
							for(x=w; (x--)>0; )
							{
								*cp++=BWmap[*pp][0];
								pp+=samplesperpixel;
							}
							cp+=toskew;
							pp+=fromskew;
						}
					}
				}
			}
		}

		// 16-bit greyscale => colormap/RGB
		static void put16bitbwtile(TIFFRGBAImage img, uint[] cp0, uint cp_offset, uint x, uint y, uint w, uint h, int fromskew, int toskew, byte[] pp0, uint pp_offset)
		{
			int samplesperpixel=img.samplesperpixel;
			uint[][] BWmap=img.BWmap;

			unsafe
			{
				fixed(uint* cp_=cp0)
				{
					uint* cp=cp_+cp_offset;
					fixed(byte* pp_=pp0)
					{
						byte* pp=pp_+pp_offset;

						while((h--)>0)
						{
							ushort* wp=(ushort*)pp;

							for(x=w; (x--)>0; )
							{
								// use high order byte of 16bit value
								*cp++=BWmap[*wp>>8][0];
								pp+=2*samplesperpixel;
								wp+=samplesperpixel;
							}
							cp+=toskew;
							pp+=fromskew;
						}
					}
				}
			}
		}

		// 1-bit bilevel => colormap/RGB
		static void put1bitbwtile(TIFFRGBAImage img, uint[] cp0, uint cp_offset, uint x, uint y, uint w, uint h, int fromskew, int toskew, byte[] pp0, uint pp_offset)
		{
			uint[][] BWmap=img.BWmap;

			unsafe
			{
				fixed(uint* cp_=cp0)
				{
					uint* cp=cp_+cp_offset;
					fixed(byte* pp_=pp0)
					{
						byte* pp=pp_+pp_offset;

						fromskew/=8;
						while((h--)>0)
						{
							uint _x;
							for(_x=w; _x>=8; _x-=8)
							{
								uint[] bw=BWmap[*pp++];
								*cp++=bw[0];
								*cp++=bw[1];
								*cp++=bw[2];
								*cp++=bw[3];
								*cp++=bw[4];
								*cp++=bw[5];
								*cp++=bw[6];
								*cp++=bw[7];
							}

							if(_x>0)
							{
								uint[] bw=BWmap[*pp++]; uint bw_ind=0;
								switch(_x)
								{
									case 7: *cp++=bw[bw_ind++]; goto case 6;
									case 6: *cp++=bw[bw_ind++]; goto case 5;
									case 5: *cp++=bw[bw_ind++]; goto case 4;
									case 4: *cp++=bw[bw_ind++]; goto case 3;
									case 3: *cp++=bw[bw_ind++]; goto case 2;
									case 2: *cp++=bw[bw_ind++]; goto case 1;
									case 1: *cp++=bw[bw_ind++]; break;
								}
							}

							cp+=toskew;
							pp+=fromskew;
						}
					}
				}
			}
		}

		// 2-bit greyscale => colormap/RGB
		static void put2bitbwtile(TIFFRGBAImage img, uint[] cp0, uint cp_offset, uint x, uint y, uint w, uint h, int fromskew, int toskew, byte[] pp0, uint pp_offset)
		{
			uint[][] BWmap=img.BWmap;

			unsafe
			{
				fixed(uint* cp_=cp0)
				{
					uint* cp=cp_+cp_offset;
					fixed(byte* pp_=pp0)
					{
						byte* pp=pp_+pp_offset;

						fromskew/=4;
						while((h--)>0)
						{
							uint _x;
							for(_x=w; _x>=4; _x-=4)
							{
								uint[] bw=BWmap[*pp++];
								*cp++=bw[0];
								*cp++=bw[1];
								*cp++=bw[2];
								*cp++=bw[3];
							}

							if(_x>0)
							{
								uint[] bw=BWmap[*pp++]; uint bw_ind=0;
								switch(_x)
								{
									case 3: *cp++=bw[bw_ind++]; goto case 2;
									case 2: *cp++=bw[bw_ind++]; goto case 1;
									case 1: *cp++=bw[bw_ind++]; break;
								}
							}

							cp+=toskew;
							pp+=fromskew;
						}
					}
				}
			}
		}

		// 4-bit greyscale => colormap/RGB
		static void put4bitbwtile(TIFFRGBAImage img, uint[] cp0, uint cp_offset, uint x, uint y, uint w, uint h, int fromskew, int toskew, byte[] pp0, uint pp_offset)
		{
			uint[][] BWmap=img.BWmap;

			unsafe
			{
				fixed(uint* cp_=cp0)
				{
					uint* cp=cp_+cp_offset;
					fixed(byte* pp_=pp0)
					{
						byte* pp=pp_+pp_offset;

						fromskew/=2;
						while((h--)>0)
						{
							uint _x;
							for(_x=w; _x>=2; _x-=2)
							{
								uint[] bw=BWmap[*pp++];
								*cp++=bw[0];
								*cp++=bw[1];
							}

							if(_x!=0)
							{
								uint[] bw=BWmap[*pp++];
								*cp++=bw[0];
							}

							cp+=toskew;
							pp+=fromskew;
						}
					}
				}
			}
		}

		// 8-bit packed samples, no Map => RGB
		static void putRGBcontig8bittile(TIFFRGBAImage img, uint[] cp0, uint cp_offset, uint x, uint y, uint w, uint h, int fromskew, int toskew, byte[] pp0, uint pp_offset)
		{
			int samplesperpixel=img.samplesperpixel;

			unsafe
			{
				fixed(uint* cp_=cp0)
				{
					uint* cp=cp_+cp_offset;
					fixed(byte* pp_=pp0)
					{
						byte* pp=pp_+pp_offset;

						fromskew*=samplesperpixel;
						while((h--)>0)
						{
							uint _x;
							for(_x=w; _x>=8; _x-=8)
							{
								*cp++=((uint)pp[0]|((uint)pp[1]<<8)|((uint)pp[2]<<16)|0xFF000000); pp+=samplesperpixel;
								*cp++=((uint)pp[0]|((uint)pp[1]<<8)|((uint)pp[2]<<16)|0xFF000000); pp+=samplesperpixel;
								*cp++=((uint)pp[0]|((uint)pp[1]<<8)|((uint)pp[2]<<16)|0xFF000000); pp+=samplesperpixel;
								*cp++=((uint)pp[0]|((uint)pp[1]<<8)|((uint)pp[2]<<16)|0xFF000000); pp+=samplesperpixel;
								*cp++=((uint)pp[0]|((uint)pp[1]<<8)|((uint)pp[2]<<16)|0xFF000000); pp+=samplesperpixel;
								*cp++=((uint)pp[0]|((uint)pp[1]<<8)|((uint)pp[2]<<16)|0xFF000000); pp+=samplesperpixel;
								*cp++=((uint)pp[0]|((uint)pp[1]<<8)|((uint)pp[2]<<16)|0xFF000000); pp+=samplesperpixel;
								*cp++=((uint)pp[0]|((uint)pp[1]<<8)|((uint)pp[2]<<16)|0xFF000000); pp+=samplesperpixel;
							}

							if(_x>0)
							{
								switch(_x)
								{
									case 7: *cp++=((uint)pp[0]|((uint)pp[1]<<8)|((uint)pp[2]<<16)|0xFF000000); pp+=samplesperpixel; goto case 6;
									case 6: *cp++=((uint)pp[0]|((uint)pp[1]<<8)|((uint)pp[2]<<16)|0xFF000000); pp+=samplesperpixel; goto case 5;
									case 5: *cp++=((uint)pp[0]|((uint)pp[1]<<8)|((uint)pp[2]<<16)|0xFF000000); pp+=samplesperpixel; goto case 4;
									case 4: *cp++=((uint)pp[0]|((uint)pp[1]<<8)|((uint)pp[2]<<16)|0xFF000000); pp+=samplesperpixel; goto case 3;
									case 3: *cp++=((uint)pp[0]|((uint)pp[1]<<8)|((uint)pp[2]<<16)|0xFF000000); pp+=samplesperpixel; goto case 2;
									case 2: *cp++=((uint)pp[0]|((uint)pp[1]<<8)|((uint)pp[2]<<16)|0xFF000000); pp+=samplesperpixel; goto case 1;
									case 1: *cp++=((uint)pp[0]|((uint)pp[1]<<8)|((uint)pp[2]<<16)|0xFF000000); pp+=samplesperpixel; break;
								}
							}

							cp+=toskew;
							pp+=fromskew;
						}
					}
				}
			}
		}

		// 8-bit packed samples => RGBA w/ associated alpha
		// (known to have Map == NULL)
		static void putRGBAAcontig8bittile(TIFFRGBAImage img, uint[] cp0, uint cp_offset, uint x, uint y, uint w, uint h, int fromskew, int toskew, byte[] pp0, uint pp_offset)
		{
			int samplesperpixel=img.samplesperpixel;

			unsafe
			{
				fixed(uint* cp_=cp0)
				{
					uint* cp=cp_+cp_offset;
					fixed(byte* pp_=pp0)
					{
						byte* pp=pp_+pp_offset;

						fromskew*=samplesperpixel;
						while((h--)>0)
						{
							uint _x;
							for(_x=w; _x>=8; _x-=8)
							{
								*cp++=((uint)pp[0]|((uint)pp[1]<<8)|((uint)pp[2]<<16)|((uint)pp[3]<<24)); pp+=samplesperpixel;
								*cp++=((uint)pp[0]|((uint)pp[1]<<8)|((uint)pp[2]<<16)|((uint)pp[3]<<24)); pp+=samplesperpixel;
								*cp++=((uint)pp[0]|((uint)pp[1]<<8)|((uint)pp[2]<<16)|((uint)pp[3]<<24)); pp+=samplesperpixel;
								*cp++=((uint)pp[0]|((uint)pp[1]<<8)|((uint)pp[2]<<16)|((uint)pp[3]<<24)); pp+=samplesperpixel;
								*cp++=((uint)pp[0]|((uint)pp[1]<<8)|((uint)pp[2]<<16)|((uint)pp[3]<<24)); pp+=samplesperpixel;
								*cp++=((uint)pp[0]|((uint)pp[1]<<8)|((uint)pp[2]<<16)|((uint)pp[3]<<24)); pp+=samplesperpixel;
								*cp++=((uint)pp[0]|((uint)pp[1]<<8)|((uint)pp[2]<<16)|((uint)pp[3]<<24)); pp+=samplesperpixel;
								*cp++=((uint)pp[0]|((uint)pp[1]<<8)|((uint)pp[2]<<16)|((uint)pp[3]<<24)); pp+=samplesperpixel;
							}

							if(_x>0)
							{
								switch(_x)
								{
									case 7: *cp++=((uint)pp[0]|((uint)pp[1]<<8)|((uint)pp[2]<<16)|((uint)pp[3]<<24)); pp+=samplesperpixel; goto case 6;
									case 6: *cp++=((uint)pp[0]|((uint)pp[1]<<8)|((uint)pp[2]<<16)|((uint)pp[3]<<24)); pp+=samplesperpixel; goto case 5;
									case 5: *cp++=((uint)pp[0]|((uint)pp[1]<<8)|((uint)pp[2]<<16)|((uint)pp[3]<<24)); pp+=samplesperpixel; goto case 4;
									case 4: *cp++=((uint)pp[0]|((uint)pp[1]<<8)|((uint)pp[2]<<16)|((uint)pp[3]<<24)); pp+=samplesperpixel; goto case 3;
									case 3: *cp++=((uint)pp[0]|((uint)pp[1]<<8)|((uint)pp[2]<<16)|((uint)pp[3]<<24)); pp+=samplesperpixel; goto case 2;
									case 2: *cp++=((uint)pp[0]|((uint)pp[1]<<8)|((uint)pp[2]<<16)|((uint)pp[3]<<24)); pp+=samplesperpixel; goto case 1;
									case 1: *cp++=((uint)pp[0]|((uint)pp[1]<<8)|((uint)pp[2]<<16)|((uint)pp[3]<<24)); pp+=samplesperpixel; break;
								}
							}

							cp+=toskew;
							pp+=fromskew;
						}
					}
				}
			}
		}

		// 8-bit packed samples => RGBA w/ unassociated alpha
		// (known to have Map == NULL)
		static void putRGBUAcontig8bittile(TIFFRGBAImage img, uint[] cp0, uint cp_offset, uint x, uint y, uint w, uint h, int fromskew, int toskew, byte[] pp0, uint pp_offset)
		{
			int samplesperpixel=img.samplesperpixel;

			unsafe
			{
				fixed(uint* cp_=cp0)
				{
					uint* cp=cp_+cp_offset;
					fixed(byte* pp_=pp0)
					{
						byte* pp=pp_+pp_offset;

						fromskew*=samplesperpixel;
						while((h--)>0)
						{
							uint r, g, b, a;
							for(x=w; (x--)>0; )
							{
								a=pp[3];
								r=(a*pp[0]+127)/255;
								g=(a*pp[1]+127)/255;
								b=(a*pp[2]+127)/255;
								*cp++=(r|(g<<8)|(b<<16)|(a<<24));
								pp+=samplesperpixel;
							}
							cp+=toskew;
							pp+=fromskew;
						}
					}
				}
			}
		}

		// 16-bit packed samples => RGB
		static void putRGBcontig16bittile(TIFFRGBAImage img, uint[] cp0, uint cp_offset, uint x, uint y, uint w, uint h, int fromskew, int toskew, byte[] pp0, uint pp_offset)
		{
			int samplesperpixel=img.samplesperpixel*2;

			unsafe
			{
				fixed(uint* cp_=cp0)
				{
					uint* cp=cp_+cp_offset;
					fixed(byte* pp_=pp0)
					{
						byte* pp=pp_+pp_offset;

						fromskew*=samplesperpixel;
						while((h--)>0)
						{
							for(x=w; (x--)>0; )
							{
								*cp++=((uint)pp[1]|((uint)pp[3]<<8)|((uint)pp[5]<<16)|0xFF000000);
								pp+=samplesperpixel;
							}
							cp+=toskew;
							pp+=fromskew;
						}
					}
				}
			}
		}

		// 16-bit packed samples => RGBA w/ associated alpha
		// (known to have Map == NULL)
		static void putRGBAAcontig16bittile(TIFFRGBAImage img, uint[] cp0, uint cp_offset, uint x, uint y, uint w, uint h, int fromskew, int toskew, byte[] pp0, uint pp_offset)
		{
			int samplesperpixel=img.samplesperpixel*2;

			unsafe
			{
				fixed(uint* cp_=cp0)
				{
					uint* cp=cp_+cp_offset;
					fixed(byte* pp_=pp0)
					{
						byte* pp=pp_+pp_offset;

						fromskew*=samplesperpixel;
						while((h--)>0)
						{
							for(x=w; (x--)>0; )
							{
								*cp++=((uint)pp[1]|((uint)pp[3]<<8)|((uint)pp[5]<<16)|((uint)pp[7]<<24));
								pp+=samplesperpixel;
							}
							cp+=toskew;
							pp+=fromskew*2;
						}
					}
				}
			}
		}

		// 16-bit packed samples => RGBA w/ unassociated alpha
		// (known to have Map == NULL)
		static void putRGBUAcontig16bittile(TIFFRGBAImage img, uint[] cp0, uint cp_offset, uint x, uint y, uint w, uint h, int fromskew, int toskew, byte[] pp0, uint pp_offset)
		{
			int samplesperpixel=img.samplesperpixel;

			unsafe
			{
				fixed(uint* cp_=cp0)
				{
					uint* cp=cp_+cp_offset;
					fixed(byte* pp_=pp0)
					{
						byte* pp=pp_+pp_offset;

						ushort* wp=(ushort*)pp;

						fromskew*=samplesperpixel;
						while((h--)>0)
						{
							uint r, g, b, a;
							for(x=w; (x--)>0; )
							{
								a=W2B(wp[3]);
								r=(a*W2B(wp[0])+127)/255;
								g=(a*W2B(wp[1])+127)/255;
								b=(a*W2B(wp[2])+127)/255;
								*cp++=(r|(g<<8)|(b<<16)|(a<<24));
								wp+=samplesperpixel;
							}
							cp+=toskew;
							wp+=fromskew;
						}
					}
				}
			}
		}

		// 8-bit packed CMYK samples w/o Map => RGB
		//
		// NB: The conversion of CMYK=>RGB is *very* crude.
		static void putRGBcontig8bitCMYKtile(TIFFRGBAImage img, uint[] cp0, uint cp_offset, uint x, uint y, uint w, uint h, int fromskew, int toskew, byte[] pp0, uint pp_offset)
		{
			int samplesperpixel=img.samplesperpixel;

			unsafe
			{
				fixed(uint* cp_=cp0)
				{
					uint* cp=cp_+cp_offset;
					fixed(byte* pp_=pp0)
					{
						byte* pp=pp_+pp_offset;

						uint r, g, b, k;

						fromskew*=samplesperpixel;
						while((h--)>0)
						{
							uint _x;
							for(_x=w; _x>=8; _x-=8)
							{
								k=(uint)(255-pp[3]); r=(uint)((k*(255-pp[0]))/255); g=(uint)((k*(255-pp[1]))/255); b=(uint)((k*(255-pp[2]))/255); *cp++=(r|(g<<8)|(b<<16)|0xFF000000); pp+=samplesperpixel;
								k=(uint)(255-pp[3]); r=(uint)((k*(255-pp[0]))/255); g=(uint)((k*(255-pp[1]))/255); b=(uint)((k*(255-pp[2]))/255); *cp++=(r|(g<<8)|(b<<16)|0xFF000000); pp+=samplesperpixel;
								k=(uint)(255-pp[3]); r=(uint)((k*(255-pp[0]))/255); g=(uint)((k*(255-pp[1]))/255); b=(uint)((k*(255-pp[2]))/255); *cp++=(r|(g<<8)|(b<<16)|0xFF000000); pp+=samplesperpixel;
								k=(uint)(255-pp[3]); r=(uint)((k*(255-pp[0]))/255); g=(uint)((k*(255-pp[1]))/255); b=(uint)((k*(255-pp[2]))/255); *cp++=(r|(g<<8)|(b<<16)|0xFF000000); pp+=samplesperpixel;
								k=(uint)(255-pp[3]); r=(uint)((k*(255-pp[0]))/255); g=(uint)((k*(255-pp[1]))/255); b=(uint)((k*(255-pp[2]))/255); *cp++=(r|(g<<8)|(b<<16)|0xFF000000); pp+=samplesperpixel;
								k=(uint)(255-pp[3]); r=(uint)((k*(255-pp[0]))/255); g=(uint)((k*(255-pp[1]))/255); b=(uint)((k*(255-pp[2]))/255); *cp++=(r|(g<<8)|(b<<16)|0xFF000000); pp+=samplesperpixel;
								k=(uint)(255-pp[3]); r=(uint)((k*(255-pp[0]))/255); g=(uint)((k*(255-pp[1]))/255); b=(uint)((k*(255-pp[2]))/255); *cp++=(r|(g<<8)|(b<<16)|0xFF000000); pp+=samplesperpixel;
								k=(uint)(255-pp[3]); r=(uint)((k*(255-pp[0]))/255); g=(uint)((k*(255-pp[1]))/255); b=(uint)((k*(255-pp[2]))/255); *cp++=(r|(g<<8)|(b<<16)|0xFF000000); pp+=samplesperpixel;
							}

							if(_x>0)
							{
								switch(_x)
								{
									case 7: k=(uint)(255-pp[3]); r=(uint)((k*(255-pp[0]))/255); g=(uint)((k*(255-pp[1]))/255); b=(uint)((k*(255-pp[2]))/255); *cp++=(r|(g<<8)|(b<<16)|0xFF000000); pp+=samplesperpixel; goto case 6;
									case 6: k=(uint)(255-pp[3]); r=(uint)((k*(255-pp[0]))/255); g=(uint)((k*(255-pp[1]))/255); b=(uint)((k*(255-pp[2]))/255); *cp++=(r|(g<<8)|(b<<16)|0xFF000000); pp+=samplesperpixel; goto case 5;
									case 5: k=(uint)(255-pp[3]); r=(uint)((k*(255-pp[0]))/255); g=(uint)((k*(255-pp[1]))/255); b=(uint)((k*(255-pp[2]))/255); *cp++=(r|(g<<8)|(b<<16)|0xFF000000); pp+=samplesperpixel; goto case 4;
									case 4: k=(uint)(255-pp[3]); r=(uint)((k*(255-pp[0]))/255); g=(uint)((k*(255-pp[1]))/255); b=(uint)((k*(255-pp[2]))/255); *cp++=(r|(g<<8)|(b<<16)|0xFF000000); pp+=samplesperpixel; goto case 3;
									case 3: k=(uint)(255-pp[3]); r=(uint)((k*(255-pp[0]))/255); g=(uint)((k*(255-pp[1]))/255); b=(uint)((k*(255-pp[2]))/255); *cp++=(r|(g<<8)|(b<<16)|0xFF000000); pp+=samplesperpixel; goto case 2;
									case 2: k=(uint)(255-pp[3]); r=(uint)((k*(255-pp[0]))/255); g=(uint)((k*(255-pp[1]))/255); b=(uint)((k*(255-pp[2]))/255); *cp++=(r|(g<<8)|(b<<16)|0xFF000000); pp+=samplesperpixel; goto case 1;
									case 1: k=(uint)(255-pp[3]); r=(uint)((k*(255-pp[0]))/255); g=(uint)((k*(255-pp[1]))/255); b=(uint)((k*(255-pp[2]))/255); *cp++=(r|(g<<8)|(b<<16)|0xFF000000); pp+=samplesperpixel; break;
								}
							}

							cp+=toskew;
							pp+=fromskew;
						}
					}
				}
			}
		}

		// 8-bit packed CMYK samples w/Map => RGB
		//
		// NB: The conversion of CMYK=>RGB is *very* crude.
		static void putRGBcontig8bitCMYKMaptile(TIFFRGBAImage img, uint[] cp0, uint cp_offset, uint x, uint y, uint w, uint h, int fromskew, int toskew, byte[] pp0, uint pp_offset)
		{
			int samplesperpixel=img.samplesperpixel;
			byte[] Map=img.Map;

			unsafe
			{
				fixed(uint* cp_=cp0)
				{
					uint* cp=cp_+cp_offset;
					fixed(byte* pp_=pp0)
					{
						byte* pp=pp_+pp_offset;
						ushort r, g, b, k;

						fromskew*=samplesperpixel;
						while((h--)>0)
						{
							for(x=w; (x--)>0; )
							{
								k=(ushort)(255-pp[3]);
								r=(ushort)((k*(255-pp[0]))/255);
								g=(ushort)((k*(255-pp[1]))/255);
								b=(ushort)((k*(255-pp[2]))/255);
								*cp++=((uint)Map[r]|((uint)Map[g]<<8)|((uint)Map[b]<<16)|0xFF000000);
								pp+=samplesperpixel;
							}
							pp+=fromskew;
							cp+=toskew;
						}
					}
				}
			}
		}

		// 8-bit unpacked samples => RGB
		static void putRGBseparate8bittile(TIFFRGBAImage img, uint[] cp0, uint cp_offset, uint x, uint y, uint w, uint h, int fromskew, int toskew, byte[] r0, byte[] g0, byte[] b0, byte[] a0, uint rgba_offset)
		{
			unsafe
			{
				fixed(uint* cp_=cp0)
				{
					uint* cp=cp_+cp_offset;
					fixed(byte* r_=r0, g_=g0, b_=b0)
					{
						byte* r=r_+rgba_offset;
						byte* g=g_+rgba_offset;
						byte* b=b_+rgba_offset;

						while((h--)>0)
						{
							uint _x;
							for(_x=w; _x>=8; _x-=8)
							{
								*cp++=((uint)(*r++)|((uint)(*g++)<<8)|((uint)(*b++)<<16)|0xFF000000);
								*cp++=((uint)(*r++)|((uint)(*g++)<<8)|((uint)(*b++)<<16)|0xFF000000);
								*cp++=((uint)(*r++)|((uint)(*g++)<<8)|((uint)(*b++)<<16)|0xFF000000);
								*cp++=((uint)(*r++)|((uint)(*g++)<<8)|((uint)(*b++)<<16)|0xFF000000);
								*cp++=((uint)(*r++)|((uint)(*g++)<<8)|((uint)(*b++)<<16)|0xFF000000);
								*cp++=((uint)(*r++)|((uint)(*g++)<<8)|((uint)(*b++)<<16)|0xFF000000);
								*cp++=((uint)(*r++)|((uint)(*g++)<<8)|((uint)(*b++)<<16)|0xFF000000);
								*cp++=((uint)(*r++)|((uint)(*g++)<<8)|((uint)(*b++)<<16)|0xFF000000);
							}

							if(_x>0)
							{
								switch(_x)
								{
									case 7: *cp++=((uint)(*r++)|((uint)(*g++)<<8)|((uint)(*b++)<<16)|0xFF000000); goto case 6;
									case 6: *cp++=((uint)(*r++)|((uint)(*g++)<<8)|((uint)(*b++)<<16)|0xFF000000); goto case 5;
									case 5: *cp++=((uint)(*r++)|((uint)(*g++)<<8)|((uint)(*b++)<<16)|0xFF000000); goto case 4;
									case 4: *cp++=((uint)(*r++)|((uint)(*g++)<<8)|((uint)(*b++)<<16)|0xFF000000); goto case 3;
									case 3: *cp++=((uint)(*r++)|((uint)(*g++)<<8)|((uint)(*b++)<<16)|0xFF000000); goto case 2;
									case 2: *cp++=((uint)(*r++)|((uint)(*g++)<<8)|((uint)(*b++)<<16)|0xFF000000); goto case 1;
									case 1: *cp++=((uint)(*r++)|((uint)(*g++)<<8)|((uint)(*b++)<<16)|0xFF000000); break;
								}
							}

							r+=fromskew; g+=fromskew; b+=fromskew;
							cp+=toskew;
						}
					}
				}
			}
		}

		// 8-bit unpacked samples => RGBA w/ associated alpha
		static void putRGBAAseparate8bittile(TIFFRGBAImage img, uint[] cp0, uint cp_offset, uint x, uint y, uint w, uint h, int fromskew, int toskew, byte[] r0, byte[] g0, byte[] b0, byte[] a0, uint rgba_offset)
		{
			unsafe
			{
				fixed(uint* cp_=cp0)
				{
					uint* cp=cp_+cp_offset;
					fixed(byte* r_=r0, g_=g0, b_=b0, a_=a0)
					{
						byte* r=r_+rgba_offset;
						byte* g=g_+rgba_offset;
						byte* b=b_+rgba_offset;
						byte* a=a_+rgba_offset;

						while((h--)>0)
						{
							uint _x;
							for(_x=w; _x>=8; _x-=8)
							{
								*cp++=((uint)(*r++)|((uint)(*g++)<<8)|((uint)(*b++)<<16)|((uint)(*a++)<<24));
								*cp++=((uint)(*r++)|((uint)(*g++)<<8)|((uint)(*b++)<<16)|((uint)(*a++)<<24));
								*cp++=((uint)(*r++)|((uint)(*g++)<<8)|((uint)(*b++)<<16)|((uint)(*a++)<<24));
								*cp++=((uint)(*r++)|((uint)(*g++)<<8)|((uint)(*b++)<<16)|((uint)(*a++)<<24));
								*cp++=((uint)(*r++)|((uint)(*g++)<<8)|((uint)(*b++)<<16)|((uint)(*a++)<<24));
								*cp++=((uint)(*r++)|((uint)(*g++)<<8)|((uint)(*b++)<<16)|((uint)(*a++)<<24));
								*cp++=((uint)(*r++)|((uint)(*g++)<<8)|((uint)(*b++)<<16)|((uint)(*a++)<<24));
								*cp++=((uint)(*r++)|((uint)(*g++)<<8)|((uint)(*b++)<<16)|((uint)(*a++)<<24));
							}

							if(_x>0)
							{
								switch(_x)
								{
									case 7: *cp++=((uint)(*r++)|((uint)(*g++)<<8)|((uint)(*b++)<<16)|((uint)(*a++)<<24)); goto case 6;
									case 6: *cp++=((uint)(*r++)|((uint)(*g++)<<8)|((uint)(*b++)<<16)|((uint)(*a++)<<24)); goto case 5;
									case 5: *cp++=((uint)(*r++)|((uint)(*g++)<<8)|((uint)(*b++)<<16)|((uint)(*a++)<<24)); goto case 4;
									case 4: *cp++=((uint)(*r++)|((uint)(*g++)<<8)|((uint)(*b++)<<16)|((uint)(*a++)<<24)); goto case 3;
									case 3: *cp++=((uint)(*r++)|((uint)(*g++)<<8)|((uint)(*b++)<<16)|((uint)(*a++)<<24)); goto case 2;
									case 2: *cp++=((uint)(*r++)|((uint)(*g++)<<8)|((uint)(*b++)<<16)|((uint)(*a++)<<24)); goto case 1;
									case 1: *cp++=((uint)(*r++)|((uint)(*g++)<<8)|((uint)(*b++)<<16)|((uint)(*a++)<<24)); break;
								}
							}

							r+=fromskew; g+=fromskew; b+=fromskew; a+=fromskew;
							cp+=toskew;
						}
					}
				}
			}
		}

		// 8-bit unpacked samples => RGBA w/ unassociated alpha
		static void putRGBUAseparate8bittile(TIFFRGBAImage img, uint[] cp0, uint cp_offset, uint x, uint y, uint w, uint h, int fromskew, int toskew, byte[] r0, byte[] g0, byte[] b0, byte[] a0, uint rgba_offset)
		{
			unsafe
			{
				fixed(uint* cp_=cp0)
				{
					uint* cp=cp_+cp_offset;
					fixed(byte* r_=r0, g_=g0, b_=b0, a_=a0)
					{
						byte* r=r_+rgba_offset;
						byte* g=g_+rgba_offset;
						byte* b=b_+rgba_offset;
						byte* a=a_+rgba_offset;

						while((h--)>0)
						{
							uint rv, gv, bv, av;
							for(x=w; (x--)>0; )
							{
								av=*a++;
								rv=(*r++*av+127)/255;
								gv=(*g++*av+127)/255;
								bv=(*b++*av+127)/255;
								*cp++=(rv|(gv<<8)|(bv<<16)|(av<<24));
							}

							r+=fromskew; g+=fromskew; b+=fromskew; a+=fromskew;
							cp+=toskew;
						}
					}
				}
			}
		}

		// 16-bit unpacked samples => RGB
		static void putRGBseparate16bittile(TIFFRGBAImage img, uint[] cp0, uint cp_offset, uint x, uint y, uint w, uint h, int fromskew, int toskew, byte[] r0, byte[] g0, byte[] b0, byte[] a0, uint rgba_offset)
		{
			unsafe
			{
				fixed(uint* cp_=cp0)
				{
					uint* cp=cp_+cp_offset;
					fixed(byte* r_=r0, g_=g0, b_=b0)
					{
						byte* r=r_+rgba_offset+1; // +1 ignore LSB
						byte* g=g_+rgba_offset+1;
						byte* b=b_+rgba_offset+1;

						while((h--)>0)
						{
							for(x=0; x<w; x++)
							{
								*cp++=((uint)*r|((uint)*g<<8)|((uint)*b<<16)|0xFF000000);
								r+=2; g+=2; b+=2;
							}

							r+=2*fromskew; g+=2*fromskew; b+=2*fromskew;
							cp+=toskew;
						}
					}
				}
			}
		}

		// 16-bit unpacked samples => RGBA w/ associated alpha
		static void putRGBAAseparate16bittile(TIFFRGBAImage img, uint[] cp0, uint cp_offset, uint x, uint y, uint w, uint h, int fromskew, int toskew, byte[] r0, byte[] g0, byte[] b0, byte[] a0, uint rgba_offset)
		{
			unsafe
			{
				fixed(uint* cp_=cp0)
				{
					uint* cp=cp_+cp_offset;
					fixed(byte* r_=r0, g_=g0, b_=b0, a_=a0)
					{
						byte* r=r_+rgba_offset+1; // +1 ignore LSB
						byte* g=g_+rgba_offset+1;
						byte* b=b_+rgba_offset+1;
						byte* a=a_+rgba_offset+1;

						while((h--)>0)
						{
							for(x=0; x<w; x++)
							{
								*cp++=((uint)*r|((uint)*g<<8)|((uint)*b<<16)|((uint)*a<<24));
								r+=2; g+=2; b+=2; a+=2;
							}

							r+=2*fromskew; g+=2*fromskew; b+=2*fromskew; a+=2*fromskew;
							cp+=toskew;
						}
					}
				}
			}
		}

		// 16-bit unpacked samples => RGBA w/ unassociated alpha
		static void putRGBUAseparate16bittile(TIFFRGBAImage img, uint[] cp0, uint cp_offset, uint x, uint y, uint w, uint h, int fromskew, int toskew, byte[] r0, byte[] g0, byte[] b0, byte[] a0, uint rgba_offset)
		{
			unsafe
			{
				fixed(uint* cp_=cp0)
				{
					uint* cp=cp_+cp_offset;
					fixed(byte* r_=r0, g_=g0, b_=b0, a_=a0)
					{
						byte* r=r_+rgba_offset;
						byte* g=g_+rgba_offset;
						byte* b=b_+rgba_offset;
						byte* a=a_+rgba_offset;

						ushort* wr=(ushort*)r;
						ushort* wg=(ushort*)g;
						ushort* wb=(ushort*)b;
						ushort* wa=(ushort*)a;

						while((h--)>0)
						{
							uint r1, g1, b1, a1;
							for(x=w; (x--)>0; )
							{
								a1=W2B(*wa++);
								r1=(a1*W2B(*wr++)+127)/255;
								g1=(a1*W2B(*wg++)+127)/255;
								b1=(a1*W2B(*wb++)+127)/255;
								*cp++=(r1|(g1<<8)|(b1<<16)|(a1<<24));
							}
							wr+=fromskew; wg+=fromskew; wb+=fromskew; wa+=fromskew;
							cp+=toskew;
						}
					}
				}
			}
		}

		// 8-bit packed CIE L*a*b 1976 samples => RGB
		static void putcontig8bitCIELab(TIFFRGBAImage img, uint[] cp0, uint cp_offset, uint x, uint y, uint w, uint h, int fromskew, int toskew, byte[] pp0, uint pp_offset)
		{
			unsafe
			{
				fixed(uint* cp_=cp0)
				{
					uint* cp=cp_+cp_offset;
					fixed(byte* pp_=pp0)
					{
						byte* pp=pp_+pp_offset;

						float X, Y, Z;
						uint r, g, b;

						fromskew*=3;
						while((h--)>0)
						{
							for(x=w; (x--)>0; )
							{
								TIFFCIELabToXYZ(img.cielab, pp[0], pp[1], pp[2], out X, out Y, out Z);
								TIFFXYZToRGB(img.cielab, X, Y, Z, out r, out g, out b);
								*cp++=(r|(g<<8)|(b<<16)|0xFF000000);
								pp+=3;
							}
							cp+=toskew;
							pp+=fromskew;
						}
					}
				}
			}
		}

		// YCbCr => RGB conversion and packing routines.

		// 8-bit packed YCbCr samples w/ 4,4 subsampling => RGB
		static void putcontig8bitYCbCr44tile(TIFFRGBAImage img, uint[] cp0, uint cp_offset, uint x, uint y, uint w, uint h, int fromskew, int toskew, byte[] pp0, uint pp_offset)
		{
			unsafe
			{
				fixed(uint* cp_=cp0)
				{
					uint* cp=cp_+cp_offset;
					fixed(byte* pp_=pp0)
					{
						byte* pp=pp_+pp_offset;

						uint* cp1=cp+w+toskew;
						uint* cp2=cp1+w+toskew;
						uint* cp3=cp2+w+toskew;
						int incr=3*(int)w+4*toskew;

						// adjust fromskew
						fromskew=(fromskew*18)/4;
						if((h&3)==0&&(w&3)==0)
						{
							uint r, g, b;

							for(; h>=4; h-=4)
							{
								x=w>>2;
								do
								{
									int Cb=pp[16];
									int Cr=pp[17];

									TIFFYCbCrtoRGB(img.ycbcr, pp[0], Cb, Cr, out r, out g, out b); cp[0]=(r|(g<<8)|(b<<16)|0xFF000000);
									TIFFYCbCrtoRGB(img.ycbcr, pp[1], Cb, Cr, out r, out g, out b); cp[1]=(r|(g<<8)|(b<<16)|0xFF000000);
									TIFFYCbCrtoRGB(img.ycbcr, pp[2], Cb, Cr, out r, out g, out b); cp[2]=(r|(g<<8)|(b<<16)|0xFF000000);
									TIFFYCbCrtoRGB(img.ycbcr, pp[3], Cb, Cr, out r, out g, out b); cp[3]=(r|(g<<8)|(b<<16)|0xFF000000);
									TIFFYCbCrtoRGB(img.ycbcr, pp[4], Cb, Cr, out r, out g, out b); cp1[0]=(r|(g<<8)|(b<<16)|0xFF000000);
									TIFFYCbCrtoRGB(img.ycbcr, pp[5], Cb, Cr, out r, out g, out b); cp1[1]=(r|(g<<8)|(b<<16)|0xFF000000);
									TIFFYCbCrtoRGB(img.ycbcr, pp[6], Cb, Cr, out r, out g, out b); cp1[2]=(r|(g<<8)|(b<<16)|0xFF000000);
									TIFFYCbCrtoRGB(img.ycbcr, pp[7], Cb, Cr, out r, out g, out b); cp1[3]=(r|(g<<8)|(b<<16)|0xFF000000);
									TIFFYCbCrtoRGB(img.ycbcr, pp[8], Cb, Cr, out r, out g, out b); cp2[0]=(r|(g<<8)|(b<<16)|0xFF000000);
									TIFFYCbCrtoRGB(img.ycbcr, pp[9], Cb, Cr, out r, out g, out b); cp2[1]=(r|(g<<8)|(b<<16)|0xFF000000);
									TIFFYCbCrtoRGB(img.ycbcr, pp[10], Cb, Cr, out r, out g, out b); cp2[2]=(r|(g<<8)|(b<<16)|0xFF000000);
									TIFFYCbCrtoRGB(img.ycbcr, pp[11], Cb, Cr, out r, out g, out b); cp2[3]=(r|(g<<8)|(b<<16)|0xFF000000);
									TIFFYCbCrtoRGB(img.ycbcr, pp[12], Cb, Cr, out r, out g, out b); cp3[0]=(r|(g<<8)|(b<<16)|0xFF000000);
									TIFFYCbCrtoRGB(img.ycbcr, pp[13], Cb, Cr, out r, out g, out b); cp3[1]=(r|(g<<8)|(b<<16)|0xFF000000);
									TIFFYCbCrtoRGB(img.ycbcr, pp[14], Cb, Cr, out r, out g, out b); cp3[2]=(r|(g<<8)|(b<<16)|0xFF000000);
									TIFFYCbCrtoRGB(img.ycbcr, pp[15], Cb, Cr, out r, out g, out b); cp3[3]=(r|(g<<8)|(b<<16)|0xFF000000);

									cp+=4; cp1+=4; cp2+=4; cp3+=4;
									pp+=18;
									x--;
								} while(x!=0);
								cp+=incr; cp1+=incr; cp2+=incr; cp3+=incr;
								pp+=fromskew;
							}
						}
						else
						{
							uint r, g, b;

							while(h>0)
							{
								for(x=w; x>0; )
								{
									int Cb=pp[16];
									int Cr=pp[17];

									switch(x)
									{
										default:
											switch(h)
											{
												default: TIFFYCbCrtoRGB(img.ycbcr, pp[15], Cb, Cr, out r, out g, out b); cp3[3]=(r|(g<<8)|(b<<16)|0xFF000000); goto case 3; // FALLTHROUGH
												case 3: TIFFYCbCrtoRGB(img.ycbcr, pp[11], Cb, Cr, out r, out g, out b); cp2[3]=(r|(g<<8)|(b<<16)|0xFF000000); goto case 2; // FALLTHROUGH
												case 2: TIFFYCbCrtoRGB(img.ycbcr, pp[7], Cb, Cr, out r, out g, out b); cp1[3]=(r|(g<<8)|(b<<16)|0xFF000000); goto case 1; // FALLTHROUGH
												case 1: TIFFYCbCrtoRGB(img.ycbcr, pp[3], Cb, Cr, out r, out g, out b); cp[3]=(r|(g<<8)|(b<<16)|0xFF000000); break;
											}
											goto case 3; // FALLTHROUGH
										case 3:
											switch(h)
											{
												default: TIFFYCbCrtoRGB(img.ycbcr, pp[14], Cb, Cr, out r, out g, out b); cp3[2]=(r|(g<<8)|(b<<16)|0xFF000000); goto case 3; // FALLTHROUGH
												case 3: TIFFYCbCrtoRGB(img.ycbcr, pp[10], Cb, Cr, out r, out g, out b); cp2[2]=(r|(g<<8)|(b<<16)|0xFF000000); goto case 2; // FALLTHROUGH
												case 2: TIFFYCbCrtoRGB(img.ycbcr, pp[6], Cb, Cr, out r, out g, out b); cp1[2]=(r|(g<<8)|(b<<16)|0xFF000000); goto case 1; // FALLTHROUGH
												case 1: TIFFYCbCrtoRGB(img.ycbcr, pp[2], Cb, Cr, out r, out g, out b); cp[2]=(r|(g<<8)|(b<<16)|0xFF000000); break;
											}
											goto case 2; // FALLTHROUGH
										case 2:
											switch(h)
											{
												default: TIFFYCbCrtoRGB(img.ycbcr, pp[13], Cb, Cr, out r, out g, out b); cp3[1]=(r|(g<<8)|(b<<16)|0xFF000000); goto case 3; // FALLTHROUGH
												case 3: TIFFYCbCrtoRGB(img.ycbcr, pp[9], Cb, Cr, out r, out g, out b); cp2[1]=(r|(g<<8)|(b<<16)|0xFF000000); goto case 2; // FALLTHROUGH
												case 2: TIFFYCbCrtoRGB(img.ycbcr, pp[5], Cb, Cr, out r, out g, out b); cp1[1]=(r|(g<<8)|(b<<16)|0xFF000000); goto case 1; // FALLTHROUGH
												case 1: TIFFYCbCrtoRGB(img.ycbcr, pp[1], Cb, Cr, out r, out g, out b); cp[1]=(r|(g<<8)|(b<<16)|0xFF000000); break;
											}
											goto case 1; // FALLTHROUGH
										case 1:
											switch(h)
											{
												default: TIFFYCbCrtoRGB(img.ycbcr, pp[12], Cb, Cr, out r, out g, out b); cp3[0]=(r|(g<<8)|(b<<16)|0xFF000000); goto case 3; // FALLTHROUGH
												case 3: TIFFYCbCrtoRGB(img.ycbcr, pp[8], Cb, Cr, out r, out g, out b); cp2[0]=(r|(g<<8)|(b<<16)|0xFF000000); goto case 2; // FALLTHROUGH
												case 2: TIFFYCbCrtoRGB(img.ycbcr, pp[4], Cb, Cr, out r, out g, out b); cp1[0]=(r|(g<<8)|(b<<16)|0xFF000000); goto case 1; // FALLTHROUGH
												case 1: TIFFYCbCrtoRGB(img.ycbcr, pp[0], Cb, Cr, out r, out g, out b); cp[0]=(r|(g<<8)|(b<<16)|0xFF000000); break;
											}
											break;
									}

									if(x<4)
									{
										cp+=x; cp1+=x; cp2+=x; cp3+=x;
										x=0;
									}
									else
									{
										cp+=4; cp1+=4; cp2+=4; cp3+=4;
										x-=4;
									}
									pp+=18;
								}

								if(h<=4) break;
								h-=4;
								cp+=incr; cp1+=incr; cp2+=incr; cp3+=incr;
								pp+=fromskew;
							}
						}
					}
				}
			}
		}

		// 8-bit packed YCbCr samples w/ 4,2 subsampling => RGB
		static void putcontig8bitYCbCr42tile(TIFFRGBAImage img, uint[] cp0, uint cp_offset, uint x, uint y, uint w, uint h, int fromskew, int toskew, byte[] pp0, uint pp_offset)
		{
			unsafe
			{
				fixed(uint* cp_=cp0)
				{
					uint* cp=cp_+cp_offset;
					fixed(byte* pp_=pp0)
					{
						byte* pp=pp_+pp_offset;

						uint* cp1=cp+w+toskew;
						int incr=2*toskew+(int)w;

						fromskew=(fromskew*10)/4;
						if((h&3)==0&&(w&1)==0)
						{
							uint r, g, b;

							for(; h>=2; h-=2)
							{
								x=w>>2;
								do
								{
									int Cb=pp[8];
									int Cr=pp[9];

									TIFFYCbCrtoRGB(img.ycbcr, pp[0], Cb, Cr, out r, out g, out b); cp[0]=(r|(g<<8)|(b<<16)|0xFF000000);
									TIFFYCbCrtoRGB(img.ycbcr, pp[1], Cb, Cr, out r, out g, out b); cp[1]=(r|(g<<8)|(b<<16)|0xFF000000);
									TIFFYCbCrtoRGB(img.ycbcr, pp[2], Cb, Cr, out r, out g, out b); cp[2]=(r|(g<<8)|(b<<16)|0xFF000000);
									TIFFYCbCrtoRGB(img.ycbcr, pp[3], Cb, Cr, out r, out g, out b); cp[3]=(r|(g<<8)|(b<<16)|0xFF000000);
									TIFFYCbCrtoRGB(img.ycbcr, pp[4], Cb, Cr, out r, out g, out b); cp1[0]=(r|(g<<8)|(b<<16)|0xFF000000);
									TIFFYCbCrtoRGB(img.ycbcr, pp[5], Cb, Cr, out r, out g, out b); cp1[1]=(r|(g<<8)|(b<<16)|0xFF000000);
									TIFFYCbCrtoRGB(img.ycbcr, pp[6], Cb, Cr, out r, out g, out b); cp1[2]=(r|(g<<8)|(b<<16)|0xFF000000);
									TIFFYCbCrtoRGB(img.ycbcr, pp[7], Cb, Cr, out r, out g, out b); cp1[3]=(r|(g<<8)|(b<<16)|0xFF000000);

									cp+=4; cp1+=4;
									pp+=10;
									x--;
								} while(x!=0);
								cp+=incr; cp1+=incr;
								pp+=fromskew;
							}
						}
						else
						{
							uint r, g, b;

							while(h>0)
							{
								for(x=w; x>0; )
								{
									int Cb=pp[8];
									int Cr=pp[9];
									switch(x)
									{
										default:
											switch(h)
											{
												default: TIFFYCbCrtoRGB(img.ycbcr, pp[7], Cb, Cr, out r, out g, out b); cp1[3]=(r|(g<<8)|(b<<16)|0xFF000000); goto case 1; // FALLTHROUGH
												case 1: TIFFYCbCrtoRGB(img.ycbcr, pp[3], Cb, Cr, out r, out g, out b); cp[3]=(r|(g<<8)|(b<<16)|0xFF000000); break;
											}
											goto case 3; // FALLTHROUGH
										case 3:
											switch(h)
											{
												default: TIFFYCbCrtoRGB(img.ycbcr, pp[6], Cb, Cr, out r, out g, out b); cp1[2]=(r|(g<<8)|(b<<16)|0xFF000000); goto case 1; // FALLTHROUGH
												case 1: TIFFYCbCrtoRGB(img.ycbcr, pp[2], Cb, Cr, out r, out g, out b); cp[2]=(r|(g<<8)|(b<<16)|0xFF000000); break;
											}
											goto case 2; // FALLTHROUGH
										case 2:
											switch(h)
											{
												default: TIFFYCbCrtoRGB(img.ycbcr, pp[5], Cb, Cr, out r, out g, out b); cp1[1]=(r|(g<<8)|(b<<16)|0xFF000000); goto case 1; // FALLTHROUGH
												case 1: TIFFYCbCrtoRGB(img.ycbcr, pp[1], Cb, Cr, out r, out g, out b); cp[1]=(r|(g<<8)|(b<<16)|0xFF000000); break;
											}
											goto case 1; // FALLTHROUGH
										case 1:
											switch(h)
											{
												default: TIFFYCbCrtoRGB(img.ycbcr, pp[4], Cb, Cr, out r, out g, out b); cp1[0]=(r|(g<<8)|(b<<16)|0xFF000000); goto case 1; // FALLTHROUGH
												case 1: TIFFYCbCrtoRGB(img.ycbcr, pp[0], Cb, Cr, out r, out g, out b); cp[0]=(r|(g<<8)|(b<<16)|0xFF000000); break;
											}
											break;
									}
									if(x<4)
									{
										cp+=x; cp1+=x;
										x=0;
									}
									else
									{
										cp+=4; cp1+=4;
										x-=4;
									}
									pp+=10;
								}

								if(h<=2) break;
								h-=2;
								cp+=incr; cp1+=incr;
								pp+=fromskew;
							}
						}
					}
				}
			}
		}

		// 8-bit packed YCbCr samples w/ 4,1 subsampling => RGB
		static void putcontig8bitYCbCr41tile(TIFFRGBAImage img, uint[] cp0, uint cp_offset, uint x, uint y, uint w, uint h, int fromskew, int toskew, byte[] pp0, uint pp_offset)
		{
			unsafe
			{
				fixed(uint* cp_=cp0)
				{
					uint* cp=cp_+cp_offset;
					fixed(byte* pp_=pp0)
					{
						byte* pp=pp_+pp_offset;

						uint r, g, b;

						// XXX adjust fromskew
						do
						{
							x=w>>2;
							do
							{
								int Cb=pp[4];
								int Cr=pp[5];

								TIFFYCbCrtoRGB(img.ycbcr, pp[0], Cb, Cr, out r, out g, out b); cp[0]=(r|(g<<8)|(b<<16)|0xFF000000);
								TIFFYCbCrtoRGB(img.ycbcr, pp[1], Cb, Cr, out r, out g, out b); cp[1]=(r|(g<<8)|(b<<16)|0xFF000000);
								TIFFYCbCrtoRGB(img.ycbcr, pp[2], Cb, Cr, out r, out g, out b); cp[2]=(r|(g<<8)|(b<<16)|0xFF000000);
								TIFFYCbCrtoRGB(img.ycbcr, pp[3], Cb, Cr, out r, out g, out b); cp[3]=(r|(g<<8)|(b<<16)|0xFF000000);

								cp+=4;
								pp+=6;
								x--;
							} while(x!=0);

							if((w&3)!=0)
							{
								int Cb=pp[4];
								int Cr=pp[5];

								switch((w&3))
								{
									case 3: TIFFYCbCrtoRGB(img.ycbcr, pp[2], Cb, Cr, out r, out g, out b); cp[2]=(r|(g<<8)|(b<<16)|0xFF000000); goto case 2;
									case 2: TIFFYCbCrtoRGB(img.ycbcr, pp[1], Cb, Cr, out r, out g, out b); cp[1]=(r|(g<<8)|(b<<16)|0xFF000000); goto case 1;
									case 1: TIFFYCbCrtoRGB(img.ycbcr, pp[0], Cb, Cr, out r, out g, out b); cp[0]=(r|(g<<8)|(b<<16)|0xFF000000); break;
									case 0: break;
								}

								cp+=(w&3);
								pp+=6;
							}

							cp+=toskew;
							pp+=fromskew;
							h--;
						} while(h!=0);
					}
				}
			}
		}

		// 8-bit packed YCbCr samples w/ 2,2 subsampling => RGB
		static void putcontig8bitYCbCr22tile(TIFFRGBAImage img, uint[] cp0, uint cp_offset, uint x, uint y, uint w, uint h, int fromskew, int toskew, byte[] pp0, uint pp_offset)
		{
			unsafe
			{
				fixed(uint* cp_=cp0)
				{
					uint* cp=cp_+cp_offset;
					fixed(byte* pp_=pp0)
					{
						byte* pp=pp_+pp_offset;

						uint* cp2;
						fromskew=(fromskew/2)*6;
						cp2=cp+w+toskew;
						while(h>=2)
						{
							x=w;
							while(x>=2)
							{
								uint r, g, b;
								int Cb=pp[4];
								int Cr=pp[5];
								TIFFYCbCrtoRGB(img.ycbcr, pp[0], Cb, Cr, out r, out g, out b); cp[0]=(r|(g<<8)|(b<<16)|0xFF000000);
								TIFFYCbCrtoRGB(img.ycbcr, pp[1], Cb, Cr, out r, out g, out b); cp[1]=(r|(g<<8)|(b<<16)|0xFF000000);
								TIFFYCbCrtoRGB(img.ycbcr, pp[2], Cb, Cr, out r, out g, out b); cp2[0]=(r|(g<<8)|(b<<16)|0xFF000000);
								TIFFYCbCrtoRGB(img.ycbcr, pp[3], Cb, Cr, out r, out g, out b); cp2[1]=(r|(g<<8)|(b<<16)|0xFF000000);
								cp+=2;
								cp2+=2;
								pp+=6;
								x-=2;
							}
							if(x==1)
							{
								uint r, g, b;
								int Cb=pp[4];
								int Cr=pp[5];
								TIFFYCbCrtoRGB(img.ycbcr, pp[0], Cb, Cr, out r, out g, out b); cp[0]=(r|(g<<8)|(b<<16)|0xFF000000);
								TIFFYCbCrtoRGB(img.ycbcr, pp[2], Cb, Cr, out r, out g, out b); cp2[0]=(r|(g<<8)|(b<<16)|0xFF000000);
								cp++;
								cp2++;
								pp+=6;
							}
							cp+=toskew*2+w;
							cp2+=toskew*2+w;
							pp+=fromskew;
							h-=2;
						}
						if(h==1)
						{
							x=w;
							while(x>=2)
							{
								uint r, g, b;
								int Cb=pp[4];
								int Cr=pp[5];
								TIFFYCbCrtoRGB(img.ycbcr, pp[0], Cb, Cr, out r, out g, out b); cp[0]=(r|(g<<8)|(b<<16)|0xFF000000);
								TIFFYCbCrtoRGB(img.ycbcr, pp[1], Cb, Cr, out r, out g, out b); cp[1]=(r|(g<<8)|(b<<16)|0xFF000000);
								cp+=2;
								cp2+=2;
								pp+=6;
								x-=2;
							}
							if(x==1)
							{
								uint r, g, b;
								int Cb=pp[4];
								int Cr=pp[5];
								TIFFYCbCrtoRGB(img.ycbcr, pp[0], Cb, Cr, out r, out g, out b); cp[0]=(r|(g<<8)|(b<<16)|0xFF000000);
							}
						}
					}
				}
			}
		}

		// 8-bit packed YCbCr samples w/ 2,1 subsampling => RGB
		static void putcontig8bitYCbCr21tile(TIFFRGBAImage img, uint[] cp0, uint cp_offset, uint x, uint y, uint w, uint h, int fromskew, int toskew, byte[] pp0, uint pp_offset)
		{
			unsafe
			{
				fixed(uint* cp_=cp0)
				{
					uint* cp=cp_+cp_offset;
					fixed(byte* pp_=pp0)
					{
						byte* pp=pp_+pp_offset;

						uint r, g, b;
						fromskew=(fromskew*4)/2;
						do
						{
							x=w>>1;
							do
							{
								int Cb=pp[2];
								int Cr=pp[3];

								TIFFYCbCrtoRGB(img.ycbcr, pp[0], Cb, Cr, out r, out g, out b); cp[0]=(r|(g<<8)|(b<<16)|0xFF000000);
								TIFFYCbCrtoRGB(img.ycbcr, pp[1], Cb, Cr, out r, out g, out b); cp[1]=(r|(g<<8)|(b<<16)|0xFF000000);

								cp+=2;
								pp+=4;
								x--;
							} while(x!=0);

							if((w&1)!=0)
							{
								int Cb=pp[2];
								int Cr=pp[3];

								TIFFYCbCrtoRGB(img.ycbcr, pp[0], Cb, Cr, out r, out g, out b); cp[0]=(r|(g<<8)|(b<<16)|0xFF000000);

								cp+=1;
								pp+=4;
							}

							cp+=toskew;
							pp+=fromskew;
							h--;
						} while(h!=0);
					}
				}
			}
		}

		// 8-bit packed YCbCr samples w/ 1,2 subsampling => RGB
		static void putcontig8bitYCbCr12tile(TIFFRGBAImage img, uint[] cp0, uint cp_offset, uint x, uint y, uint w, uint h, int fromskew, int toskew, byte[] pp0, uint pp_offset)
		{
			unsafe
			{
				fixed(uint* cp_=cp0)
				{
					uint* cp=cp_+cp_offset;
					fixed(byte* pp_=pp0)
					{
						byte* pp=pp_+pp_offset;

						uint r, g, b;

						uint* cp2;
						fromskew=(fromskew/2)*4;
						cp2=cp+w+toskew;
						while(h>=2)
						{
							x=w;
							do
							{
								int Cb=pp[2];
								int Cr=pp[3];
								TIFFYCbCrtoRGB(img.ycbcr, pp[0], Cb, Cr, out r, out g, out b); cp[0]=(r|(g<<8)|(b<<16)|0xFF000000);
								TIFFYCbCrtoRGB(img.ycbcr, pp[1], Cb, Cr, out r, out g, out b); cp2[0]=(r|(g<<8)|(b<<16)|0xFF000000);
								cp++;
								cp2++;
								pp+=4;
								x--;
							} while(x!=0);
							cp+=toskew*2+w;
							cp2+=toskew*2+w;
							pp+=fromskew;
							h-=2;
						}
						if(h==1)
						{
							x=w;
							do
							{
								int Cb=pp[2];
								int Cr=pp[3];
								TIFFYCbCrtoRGB(img.ycbcr, pp[0], Cb, Cr, out r, out g, out b); cp[0]=(r|(g<<8)|(b<<16)|0xFF000000);
								cp++;
								pp+=4;
								x--;
							} while(x!=0);
						}
					}
				}
			}
		}

		// 8-bit packed YCbCr samples w/ no subsampling => RGB
		static void putcontig8bitYCbCr11tile(TIFFRGBAImage img, uint[] cp0, uint cp_offset, uint x, uint y, uint w, uint h, int fromskew, int toskew, byte[] pp0, uint pp_offset)
		{
			unsafe
			{
				fixed(uint* cp_=cp0)
				{
					uint* cp=cp_+cp_offset;
					fixed(byte* pp_=pp0)
					{
						byte* pp=pp_+pp_offset;

						uint r, g, b;
						fromskew*=3;
						do
						{
							x=w; // was x = w>>1; patched 2000/09/25 warmerda@home.com
							do
							{
								int Cb=pp[1];
								int Cr=pp[2];

								TIFFYCbCrtoRGB(img.ycbcr, pp[0], Cb, Cr, out r, out g, out b); *cp++=(r|(g<<8)|(b<<16)|0xFF000000);

								pp+=3;
								x--;
							} while(x!=0);

							cp+=toskew;
							pp+=fromskew;
							h--;
						} while(h!=0);

					}
				}
			}
		}

		// 8-bit packed YCbCr samples w/ no subsampling => RGB
		static void putseparate8bitYCbCr11tile(TIFFRGBAImage img, uint[] cp0, uint cp_offset, uint x, uint y, uint w, uint h, int fromskew, int toskew, byte[] r0, byte[] g0, byte[] b0, byte[] a0, uint rgba_offset)
		{
			unsafe
			{
				fixed(uint* cp_=cp0)
				{
					uint* cp=cp_+cp_offset;
					fixed(byte* r_=r0, g_=g0, b_=b0)
					{
						byte* r=r_+rgba_offset;
						byte* g=g_+rgba_offset;
						byte* b=b_+rgba_offset;

						// TODO: naming of input vars is still off, change obfuscating declaration inside define, or resolve obfuscation
						while(h-->0)
						{
							x=w;
							do
							{
								uint dr, dg, db;
								TIFFYCbCrtoRGB(img.ycbcr, *r++, *g++, *b++, out dr, out dg, out db);
								*cp++=(dr|(dg<<8)|(db<<16)|0xFF000000);
								x--;
							} while(x!=0);
							r+=fromskew; g+=fromskew; b+=fromskew;
							cp+=toskew;
						}
					}
				}
			}
		}

		static bool initYCbCrConversion(TIFFRGBAImage img)
		{
			string module="initYCbCrConversion";

			double[] luma, refBlackWhite;

			if(img.ycbcr==null)
			{
				try
				{
					img.ycbcr=new TIFFYCbCrToRGB();
				}
				catch
				{
					TIFFErrorExt(img.tif.tif_clientdata, module, "No space for YCbCr=>RGB conversion state");
					return false;
				}
			}

			object[] ap=new object[2];
			TIFFGetFieldDefaulted(img.tif, TIFFTAG.YCBCRCOEFFICIENTS, ap); luma=ap[0] as double[];
			TIFFGetFieldDefaulted(img.tif, TIFFTAG.REFERENCEBLACKWHITE, ap); refBlackWhite=ap[0] as double[];
			if(TIFFYCbCrToRGBInit(img.ycbcr, luma, refBlackWhite)<0) return false;
			return true;
		}

		static tileContigRoutine initCIELabConversion(TIFFRGBAImage img)
		{
			string module="initCIELabConversion";

			double[] whitePoint;
			double[] refWhite=new double[3];

			if(img.cielab==null)
			{
				try
				{
					img.cielab=new TIFFCIELabToRGB();
				}
				catch
				{
					TIFFErrorExt(img.tif.tif_clientdata, module, "No space for CIE L*a*b*=>RGB conversion state.");
					return null;
				}
			}

			object[] ap=new object[2];

			TIFFGetFieldDefaulted(img.tif, TIFFTAG.WHITEPOINT, ap); whitePoint=ap[0] as double[];
			refWhite[1]=100.0;
			refWhite[0]=whitePoint[0]/whitePoint[1]*refWhite[1];
			refWhite[2]=(1.0-whitePoint[0]-whitePoint[1])/whitePoint[1]*refWhite[1];

			if(TIFFCIELabToRGBInit(img.cielab, TIFFDisplay.display_sRGB, refWhite)<0)
			{
				TIFFErrorExt(img.tif.tif_clientdata, module, "Failed to initialize CIE L*a*b*=>RGB conversion state.");
				return null;
			}

			return putcontig8bitCIELab;
		}

		// Greyscale images with less than 8 bits/sample are handled
		// with a table to avoid lots of shifts and masks. The table
		// is setup so that put*bwtile (below) can retrieve 8/bitspersample
		// pixel values simply by indexing into the table with one
		// number.
		static bool makebwmap(TIFFRGBAImage img)
		{
			byte[] Map=img.Map;
			int bitspersample=img.bitspersample;
			int nsamples=8/bitspersample;

			if(nsamples==0) nsamples=1;

			try
			{
				img.BWmap=new uint[256][];
				for(int i=0; i<256; i++) img.BWmap[i]=new uint[nsamples];
			}
			catch
			{
				TIFFErrorExt(img.tif.tif_clientdata, TIFFFileName(img.tif), "No space for B&W mapping table");
				return false;
			}

			uint c;

			for(int i=0; i<256; i++)
			{
				uint[] p=img.BWmap[i];
				switch(bitspersample)
				{
					case 1:
						c=Map[i>>7]; p[0]=(c|(c<<8)|(c<<16)|0xFF000000);
						c=Map[(i>>6)&1]; p[0]=(c|(c<<8)|(c<<16)|0xFF000000);
						c=Map[(i>>5)&1]; p[0]=(c|(c<<8)|(c<<16)|0xFF000000);
						c=Map[(i>>4)&1]; p[0]=(c|(c<<8)|(c<<16)|0xFF000000);
						c=Map[(i>>3)&1]; p[0]=(c|(c<<8)|(c<<16)|0xFF000000);
						c=Map[(i>>2)&1]; p[0]=(c|(c<<8)|(c<<16)|0xFF000000);
						c=Map[(i>>1)&1]; p[0]=(c|(c<<8)|(c<<16)|0xFF000000);
						c=Map[i&1]; p[0]=(c|(c<<8)|(c<<16)|0xFF000000);
						break;
					case 2:
						c=Map[i>>6]; p[0]=(c|(c<<8)|(c<<16)|0xFF000000);
						c=Map[(i>>4)&3]; p[0]=(c|(c<<8)|(c<<16)|0xFF000000);
						c=Map[(i>>2)&3]; p[0]=(c|(c<<8)|(c<<16)|0xFF000000);
						c=Map[i&3]; p[0]=(c|(c<<8)|(c<<16)|0xFF000000);
						break;
					case 4:
						c=Map[i>>4]; p[0]=(c|(c<<8)|(c<<16)|0xFF000000);
						c=Map[i&0xf]; p[0]=(c|(c<<8)|(c<<16)|0xFF000000);
						break;
					case 8:
					case 16:
						c=Map[i]; p[0]=(c|(c<<8)|(c<<16)|0xFF000000);
						break;
				}
			}
			return true;
		}

		// Construct a mapping table to convert from the range
		// of the data samples to [0,255] --for display. This
		// process also handles inverting B&W images when needed.
		static bool setupMap(TIFFRGBAImage img)
		{
			int range=((1<<img.bitspersample)-1);

			// treat 16 bit the same as eight bit
			if(img.bitspersample==16) range=255;

			try
			{
				img.Map=new byte[range+1];
			}
			catch
			{
				TIFFErrorExt(img.tif.tif_clientdata, TIFFFileName(img.tif), "No space for photometric conversion table");
				return false;
			}

			if(img.photometric==PHOTOMETRIC.MINISWHITE) for(int x=0; x<=range; x++) img.Map[x]=(byte)(((range-x)*255)/range);
			else for(int x=0; x<=range; x++) img.Map[x]=(byte)((x*255)/range);

			if(img.bitspersample<=16&&(img.photometric==PHOTOMETRIC.MINISBLACK||img.photometric==PHOTOMETRIC.MINISWHITE))
			{
				// Use photometric mapping table to construct
				// unpacking tables for samples <= 8 bits.
				if(!makebwmap(img)) return false;
				// no longer need Map, free it
				img.Map=null;
			}
			return true;
		}

		static int checkcmap(TIFFRGBAImage img)
		{
			int n=1<<img.bitspersample;

			unsafe
			{
				fixed(ushort* r_=img.redcmap, g_=img.greencmap, b_=img.bluecmap)
				{
					ushort* r=r_;
					ushort* g=g_;
					ushort* b=b_;

					while((n--)>0)
					{
						if(*r++>=256||*g++>=256||*b++>=256) return 16;
					}
				}
			}

			return 8;
		}

		static void cvtcmap(TIFFRGBAImage img)
		{
			ushort[] r=img.redcmap;
			ushort[] g=img.greencmap;
			ushort[] b=img.bluecmap;

			for(int i=(1<<img.bitspersample)-1; i>=0; i--)
			{
				r[i]>>=8;
				g[i]>>=8;
				b[i]>>=8;
			}
		}

		// Palette images with <= 8 bits/sample are handled
		// with a table to avoid lots of shifts and masks. The table
		// is setup so that put*cmaptile (below) can retrieve 8/bitspersample
		// pixel values simply by indexing into the table with one number.
		static bool makecmap(TIFFRGBAImage img)
		{
			int bitspersample=img.bitspersample;
			int nsamples=8/bitspersample;
			ushort[] r=img.redcmap;
			ushort[] g=img.greencmap;
			ushort[] b=img.bluecmap;

			try
			{
				img.PALmap=new uint[256][];
				for(int i=0; i<256; i++) img.PALmap[i]=new uint[nsamples];
			}
			catch
			{
				TIFFErrorExt(img.tif.tif_clientdata, TIFFFileName(img.tif), "No space for Palette mapping table");
				return false;
			}

			for(int i=0; i<256; i++)
			{
				byte c;
				uint[] p=img.PALmap[i];
				switch(bitspersample)
				{
					case 1:
						c=(byte)(i>>7); p[0]=((uint)(r[c]&0xff)|((uint)(g[c]&0xff)<<8)|((uint)(b[c]&0xff)<<16)|0xFF000000);
						c=(byte)((i>>6)&1); p[0]=((uint)(r[c]&0xff)|((uint)(g[c]&0xff)<<8)|((uint)(b[c]&0xff)<<16)|0xFF000000);
						c=(byte)((i>>5)&1); p[0]=((uint)(r[c]&0xff)|((uint)(g[c]&0xff)<<8)|((uint)(b[c]&0xff)<<16)|0xFF000000);
						c=(byte)((i>>4)&1); p[0]=((uint)(r[c]&0xff)|((uint)(g[c]&0xff)<<8)|((uint)(b[c]&0xff)<<16)|0xFF000000);
						c=(byte)((i>>3)&1); p[0]=((uint)(r[c]&0xff)|((uint)(g[c]&0xff)<<8)|((uint)(b[c]&0xff)<<16)|0xFF000000);
						c=(byte)((i>>2)&1); p[0]=((uint)(r[c]&0xff)|((uint)(g[c]&0xff)<<8)|((uint)(b[c]&0xff)<<16)|0xFF000000);
						c=(byte)((i>>1)&1); p[0]=((uint)(r[c]&0xff)|((uint)(g[c]&0xff)<<8)|((uint)(b[c]&0xff)<<16)|0xFF000000);
						c=(byte)(i&1); p[0]=((uint)(r[c]&0xff)|((uint)(g[c]&0xff)<<8)|((uint)(b[c]&0xff)<<16)|0xFF000000);
						break;
					case 2:
						c=(byte)(i>>6); p[0]=((uint)(r[c]&0xff)|((uint)(g[c]&0xff)<<8)|((uint)(b[c]&0xff)<<16)|0xFF000000);
						c=(byte)((i>>4)&3); p[0]=((uint)(r[c]&0xff)|((uint)(g[c]&0xff)<<8)|((uint)(b[c]&0xff)<<16)|0xFF000000);
						c=(byte)((i>>2)&3); p[0]=((uint)(r[c]&0xff)|((uint)(g[c]&0xff)<<8)|((uint)(b[c]&0xff)<<16)|0xFF000000);
						c=(byte)(i&3); p[0]=((uint)(r[c]&0xff)|((uint)(g[c]&0xff)<<8)|((uint)(b[c]&0xff)<<16)|0xFF000000);
						break;
					case 4:
						c=(byte)(i>>4); p[0]=((uint)(r[c]&0xff)|((uint)(g[c]&0xff)<<8)|((uint)(b[c]&0xff)<<16)|0xFF000000);
						c=(byte)(i&0xf); p[0]=((uint)(r[c]&0xff)|((uint)(g[c]&0xff)<<8)|((uint)(b[c]&0xff)<<16)|0xFF000000);
						break;
					case 8:
						c=(byte)i; p[0]=((uint)(r[c]&0xff)|((uint)(g[c]&0xff)<<8)|((uint)(b[c]&0xff)<<16)|0xFF000000);
						break;
				}
			}

			return true;
		}

		// Construct any mapping table used
		// by the associated put routine.
		static bool buildMap(TIFFRGBAImage img)
		{
			switch(img.photometric)
			{
				case PHOTOMETRIC.RGB:
				case PHOTOMETRIC.YCBCR:
				case PHOTOMETRIC.SEPARATED:
					if(img.bitspersample==8) break;
					if(!setupMap(img)) return false;
					break;
				case PHOTOMETRIC.MINISBLACK:
				case PHOTOMETRIC.MINISWHITE:
					if(!setupMap(img)) return false;
					break;
				case PHOTOMETRIC.PALETTE:
					// Convert 16-bit colormap to 8-bit (unless it looks
					// like an old-style 8-bit colormap).
					if(checkcmap(img)==16) cvtcmap(img);
					else TIFFWarningExt(img.tif.tif_clientdata, TIFFFileName(img.tif), "Assuming 8-bit colormap");

					// Use mapping table and colormap to construct
					// unpacking tables for samples < 8 bits.
					if(img.bitspersample<=8&&!makecmap(img)) return false;
					break;
			}
			return true;
		}

		// Select the appropriate conversion routine for packed data.
		static bool PickContigCase(TIFFRGBAImage img)
		{
			if(TIFFIsTiled(img.tif)) img.get=gtTileContig;
			else img.get=gtStripContig;
			img.contig=null;

			switch(img.photometric)
			{
				case PHOTOMETRIC.RGB:
					switch(img.bitspersample)
					{
						case 8:
							if(img.alpha==EXTRASAMPLE.ASSOCALPHA) img.contig=putRGBAAcontig8bittile;
							else if(img.alpha==EXTRASAMPLE.UNASSALPHA) img.contig=putRGBUAcontig8bittile;
							else img.contig=putRGBcontig8bittile;
							break;
						case 16:
							if(img.alpha==EXTRASAMPLE.ASSOCALPHA) img.contig=putRGBAAcontig16bittile;
							else if(img.alpha==EXTRASAMPLE.UNASSALPHA) img.contig=putRGBUAcontig16bittile;
							else img.contig=putRGBcontig16bittile;
							break;
					}
					break;
				case PHOTOMETRIC.SEPARATED:
					if(buildMap(img))
					{
						if(img.bitspersample==8)
						{
							if(img.Map==null) img.contig=putRGBcontig8bitCMYKtile;
							else img.contig=putRGBcontig8bitCMYKMaptile;
						}
					}
					break;
				case PHOTOMETRIC.PALETTE:
					if(buildMap(img))
					{
						switch(img.bitspersample)
						{
							case 8: img.contig=put8bitcmaptile; break;
							case 4: img.contig=put4bitcmaptile; break;
							case 2: img.contig=put2bitcmaptile; break;
							case 1: img.contig=put1bitcmaptile; break;
						}
					}
					break;
				case PHOTOMETRIC.MINISWHITE:
				case PHOTOMETRIC.MINISBLACK:
					if(buildMap(img))
					{
						switch(img.bitspersample)
						{
							case 16: img.contig=put16bitbwtile; break;
							case 8: img.contig=putgreytile; break;
							case 4: img.contig=put4bitbwtile; break;
							case 2: img.contig=put2bitbwtile; break;
							case 1: img.contig=put1bitbwtile; break;
						}
					}
					break;
				case PHOTOMETRIC.YCBCR:
					if(img.bitspersample==8)
					{
						// The 6.0 spec says that subsampling must be
						// one of 1, 2, or 4, and that vertical subsampling
						// must always be <= horizontal subsampling; so
						// there are only a few possibilities and we just
						// enumerate the cases.
						object[] ap=new object[2];
						TIFFGetFieldDefaulted(img.tif, TIFFTAG.YCBCRSUBSAMPLING, ap);
						ushort SubsamplingHor=__GetAsUshort(ap, 0);
						ushort SubsamplingVer=__GetAsUshort(ap, 1);

						switch((SubsamplingHor<<4)|SubsamplingVer)
						{
							case 0x44: img.contig=putcontig8bitYCbCr44tile; break;
							case 0x42: img.contig=putcontig8bitYCbCr42tile; break;
							case 0x41: img.contig=putcontig8bitYCbCr41tile; break;
							case 0x22: img.contig=putcontig8bitYCbCr22tile; break;
							case 0x21: img.contig=putcontig8bitYCbCr21tile; break;
							case 0x12: img.contig=putcontig8bitYCbCr12tile; break;
							case 0x11: img.contig=putcontig8bitYCbCr11tile; break;
						}
					}
					break;
				case PHOTOMETRIC.CIELAB:
					if(buildMap(img))
					{
						if(img.bitspersample==8) img.contig=initCIELabConversion(img);
					}
					break;
			}

			return ((img.get!=null)&&(img.contig!=null));
		}

		// Select the appropriate conversion routine for unpacked data.
		//
		// NB:	we assume that unpacked single channel data is directed
		//		to the "packed routines.
		static bool PickSeparateCase(TIFFRGBAImage img)
		{
			if(TIFFIsTiled(img.tif)) img.get=gtTileSeparate;
			else img.get=gtStripSeparate;
			
			img.separate=null;

			switch(img.photometric)
			{
				case PHOTOMETRIC.RGB:
					switch(img.bitspersample)
					{
						case 8:
							if(img.alpha==EXTRASAMPLE.ASSOCALPHA) img.separate=putRGBAAseparate8bittile;
							else if(img.alpha==EXTRASAMPLE.UNASSALPHA) img.separate=putRGBUAseparate8bittile;
							else img.separate=putRGBseparate8bittile;
							break;
						case 16:
							if(img.alpha==EXTRASAMPLE.ASSOCALPHA) img.separate=putRGBAAseparate16bittile;
							else if(img.alpha==EXTRASAMPLE.UNASSALPHA) img.separate=putRGBUAseparate16bittile;
							else img.separate=putRGBseparate16bittile;
							break;
					}
					break;
				case PHOTOMETRIC.YCBCR:
					if((img.bitspersample==8)&&(img.samplesperpixel==3))
					{
						if(initYCbCrConversion(img))
						{
							object[] ap=new object[2];
							TIFFGetFieldDefaulted(img.tif, TIFFTAG.YCBCRSUBSAMPLING, ap);
							ushort hs=__GetAsUshort(ap, 0);
							ushort vs=__GetAsUshort(ap, 1);
							switch((hs<<4)|vs)
							{
								case 0x11:
									img.separate=putseparate8bitYCbCr11tile;
									break;
								// TODO: add other cases here
							}
						}
					}
					break;
			}

			return ((img.get!=null)&&(img.separate!=null));
		}

		// Read a whole strip off data from the file, and convert to RGBA form.
		// If this is the last strip, then it will only contain the portion of
		// the strip that is actually within the image space. The result is
		// organized in bottom to top form.
		public static bool TIFFReadRGBAStrip(TIFF tif, uint row, uint[] raster)
		{
			string emsg="";
			TIFFRGBAImage img=new TIFFRGBAImage();

			if(TIFFIsTiled(tif))
			{
				TIFFErrorExt(tif.tif_clientdata, TIFFFileName(tif), "Can't use TIFFReadRGBAStrip() with tiled file.");
				return false;
			}

			object[] ap=new object[2];
			TIFFGetFieldDefaulted(tif, TIFFTAG.ROWSPERSTRIP, ap); uint rowsperstrip=__GetAsUint(ap, 0);

			if((row%rowsperstrip)!=0)
			{
				TIFFErrorExt(tif.tif_clientdata, TIFFFileName(tif), "Row passed to TIFFReadRGBAStrip() must be first in a strip.");
				return false;
			}

			if(TIFFRGBAImageOK(tif, out emsg)&&TIFFRGBAImageBegin(img, tif, false, out emsg))
			{
				img.row_offset=(int)row;
				img.col_offset=0;

				uint rows_to_read;
				if(row+rowsperstrip>img.height) rows_to_read=img.height-row;
				else rows_to_read=rowsperstrip;

				bool ok=TIFFRGBAImageGet(img, raster, 0, img.width, rows_to_read);

				TIFFRGBAImageEnd(img);
				return ok;
			}
			else TIFFErrorExt(tif.tif_clientdata, TIFFFileName(tif), emsg);

			return false;
		}

		// Read a whole tile off data from the file, and convert to RGBA form.
		// The returned RGBA data is organized from bottom to top of tile,
		// and may include zeroed areas if the tile extends off the image.
		public static bool TIFFReadRGBATile(TIFF tif, uint col, uint row, uint[] raster)
		{
			string emsg="";

			// Verify that our request is legal - on a tile file, and on a
			// tile boundary.
			if(!TIFFIsTiled(tif))
			{
				TIFFErrorExt(tif.tif_clientdata, TIFFFileName(tif), "Can't use TIFFReadRGBATile() with stripped file.");
				return false;
			}

			object[] ap=new object[2];
			TIFFGetFieldDefaulted(tif, TIFFTAG.TILEWIDTH, ap); uint tile_xsize=__GetAsUint(ap, 0);
			TIFFGetFieldDefaulted(tif, TIFFTAG.TILELENGTH, ap); uint tile_ysize=__GetAsUint(ap, 0);
			if((col%tile_xsize)!=0||(row%tile_ysize)!=0)
			{
				TIFFErrorExt(tif.tif_clientdata, TIFFFileName(tif), "Row/col passed to TIFFReadRGBATile() must be top left corner of a tile.");
				return false;
			}

			// Setup the RGBA reader.
			TIFFRGBAImage img=new TIFFRGBAImage();
			if(!TIFFRGBAImageOK(tif, out emsg)||!TIFFRGBAImageBegin(img, tif, false, out emsg))
			{
				TIFFErrorExt(tif.tif_clientdata, TIFFFileName(tif), emsg);
				return false;
			}

			// The TIFFRGBAImageGet() function doesn't allow us to get off the
			// edge of the image, even to fill an otherwise valid tile. So we
			// figure out how much we can read, and fix up the tile buffer to
			// a full tile configuration afterwards.
			uint read_xsize, read_ysize;
			if(row+tile_ysize>img.height) read_ysize=img.height-row;
			else read_ysize=tile_ysize;
			if(col+tile_xsize>img.width) read_xsize=img.width-col;
			else read_xsize=tile_xsize;

			// Read the chunk of imagery.
			img.row_offset=(int)row;
			img.col_offset=(int)col;

			bool ok=TIFFRGBAImageGet(img, raster, 0, read_xsize, read_ysize);
			TIFFRGBAImageEnd(img);

			// If our read was incomplete we will need to fix up the tile by
			// shifting the data around as if a full tile of data is being returned.
			//
			// This is all the more complicated because the image is organized in
			// bottom to top format.
			if(read_xsize==tile_xsize&&read_ysize==tile_ysize) return ok;

			for(uint i_row=0; i_row<read_ysize; i_row++)
			{
				Array.Copy(raster, (read_ysize-i_row-1)*read_xsize, raster, (tile_ysize-i_row-1)*tile_xsize, read_xsize);
				for(int i=0; i<(tile_xsize-read_xsize); i++) raster[(tile_ysize-i_row-1)*tile_xsize+read_xsize+i]=0;
			}

			for(uint i_row=read_ysize; i_row<tile_ysize; i_row++)
			{
				//_TIFFmemset(raster+(tile_ysize-i_row-1)*tile_xsize, 0, sizeof(uint32)*tile_xsize);
				for(int i=0; i<tile_xsize; i++) raster[(tile_ysize-i_row-1)*tile_xsize+i]=0;
			}

			return (ok);
		}
	}
}
