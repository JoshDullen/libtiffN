#define USE_TIFFFindFieldInfoSearch
// tif_dirinfo.cs
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
// Core Directory Tag Support.

using System;
using System.Collections.Generic;
using System.IO;

namespace Free.Ports.LibTiff
{
	public static partial class libtiff
	{
		// NB:	THIS ARRAY IS ASSUMED TO BE SORTED BY TAG.
		//		If a tag can have both LONG and SHORT types then the LONG must be
		//		placed before the SHORT for writing to work properly.
		//
		// NOTE:The second field (field_readcount) and third field (field_writecount)
		//		sometimes use the values TIFF_VARIABLE (-1)
		//		and TIFFTAG_SPP (-2). The macros should be used but would throw off 
		//		the formatting of the code, so please interprete the -1 and -2 
		//		values accordingly.
		static List<TIFFFieldInfo> tiffFieldInfo=MakeTiffFieldInfo();

		static List<TIFFFieldInfo> MakeTiffFieldInfo()
		{
			List<TIFFFieldInfo> ret=new List<TIFFFieldInfo>();
			ret.Add(new TIFFFieldInfo(TIFFTAG.SUBFILETYPE, 1, 1, TIFFDataType.TIFF_LONG, FIELD.SUBFILETYPE, true, false, "SubfileType"));

			// XXX SHORT for compatibility w/ old versions of the library
			ret.Add(new TIFFFieldInfo(TIFFTAG.SUBFILETYPE, 1, 1, TIFFDataType.TIFF_SHORT, FIELD.SUBFILETYPE, true, false, "SubfileType"));
			ret.Add(new TIFFFieldInfo(TIFFTAG.OSUBFILETYPE, 1, 1, TIFFDataType.TIFF_SHORT, FIELD.SUBFILETYPE, true, false, "OldSubfileType"));
			ret.Add(new TIFFFieldInfo(TIFFTAG.IMAGEWIDTH, 1, 1, TIFFDataType.TIFF_LONG, FIELD.IMAGEDIMENSIONS, false, false, "ImageWidth"));
			ret.Add(new TIFFFieldInfo(TIFFTAG.IMAGEWIDTH, 1, 1, TIFFDataType.TIFF_SHORT, FIELD.IMAGEDIMENSIONS, false, false, "ImageWidth"));
			ret.Add(new TIFFFieldInfo(TIFFTAG.IMAGELENGTH, 1, 1, TIFFDataType.TIFF_LONG, FIELD.IMAGEDIMENSIONS, true, false, "ImageLength"));
			ret.Add(new TIFFFieldInfo(TIFFTAG.IMAGELENGTH, 1, 1, TIFFDataType.TIFF_SHORT, FIELD.IMAGEDIMENSIONS, true, false, "ImageLength"));
			ret.Add(new TIFFFieldInfo(TIFFTAG.BITSPERSAMPLE, -1, -1, TIFFDataType.TIFF_SHORT, FIELD.BITSPERSAMPLE, false, false, "BitsPerSample"));
			
			// XXX LONG for compatibility with some broken TIFF writers
			ret.Add(new TIFFFieldInfo(TIFFTAG.BITSPERSAMPLE, -1, -1, TIFFDataType.TIFF_LONG, FIELD.BITSPERSAMPLE, false, false, "BitsPerSample"));
			
			ret.Add(new TIFFFieldInfo(TIFFTAG.COMPRESSION, -1, 1, TIFFDataType.TIFF_SHORT, FIELD.COMPRESSION, false, false, "Compression"));
			
			// XXX LONG for compatibility with some broken TIFF writers
			ret.Add(new TIFFFieldInfo(TIFFTAG.COMPRESSION, -1, 1, TIFFDataType.TIFF_LONG, FIELD.COMPRESSION, false, false, "Compression"));
			
			ret.Add(new TIFFFieldInfo(TIFFTAG.PHOTOMETRIC, 1, 1, TIFFDataType.TIFF_SHORT, FIELD.PHOTOMETRIC, false, false, "PhotometricInterpretation"));
			
			// XXX LONG for compatibility with some broken TIFF writers
			ret.Add(new TIFFFieldInfo(TIFFTAG.PHOTOMETRIC, 1, 1, TIFFDataType.TIFF_LONG, FIELD.PHOTOMETRIC, false, false, "PhotometricInterpretation"));

			ret.Add(new TIFFFieldInfo(TIFFTAG.THRESHHOLDING, 1, 1, TIFFDataType.TIFF_SHORT, FIELD.THRESHHOLDING, true, false, "Threshholding"));
			ret.Add(new TIFFFieldInfo(TIFFTAG.CELLWIDTH, 1, 1, TIFFDataType.TIFF_SHORT, FIELD.IGNORE, true, false, "CellWidth"));
			ret.Add(new TIFFFieldInfo(TIFFTAG.CELLLENGTH, 1, 1, TIFFDataType.TIFF_SHORT, FIELD.IGNORE, true, false, "CellLength"));
			ret.Add(new TIFFFieldInfo(TIFFTAG.FILLORDER, 1, 1, TIFFDataType.TIFF_SHORT, FIELD.FILLORDER, false, false, "FillOrder"));
			ret.Add(new TIFFFieldInfo(TIFFTAG.DOCUMENTNAME, -1, -1, TIFFDataType.TIFF_ASCII, FIELD.CUSTOM, true, false, "DocumentName"));
			ret.Add(new TIFFFieldInfo(TIFFTAG.IMAGEDESCRIPTION, -1, -1, TIFFDataType.TIFF_ASCII, FIELD.CUSTOM, true, false, "ImageDescription"));
			ret.Add(new TIFFFieldInfo(TIFFTAG.MAKE, -1, -1, TIFFDataType.TIFF_ASCII, FIELD.CUSTOM, true, false, "Make"));
			ret.Add(new TIFFFieldInfo(TIFFTAG.MODEL, -1, -1, TIFFDataType.TIFF_ASCII, FIELD.CUSTOM, true, false, "Model"));
			ret.Add(new TIFFFieldInfo(TIFFTAG.STRIPOFFSETS, -1, -1, TIFFDataType.TIFF_LONG, FIELD.STRIPOFFSETS, false, false, "StripOffsets"));
			ret.Add(new TIFFFieldInfo(TIFFTAG.STRIPOFFSETS, -1, -1, TIFFDataType.TIFF_SHORT, FIELD.STRIPOFFSETS, false, false, "StripOffsets"));
			ret.Add(new TIFFFieldInfo(TIFFTAG.ORIENTATION, 1, 1, TIFFDataType.TIFF_SHORT, FIELD.ORIENTATION, false, false, "Orientation"));
			ret.Add(new TIFFFieldInfo(TIFFTAG.SAMPLESPERPIXEL, 1, 1, TIFFDataType.TIFF_SHORT, FIELD.SAMPLESPERPIXEL, false, false, "SamplesPerPixel"));
			ret.Add(new TIFFFieldInfo(TIFFTAG.ROWSPERSTRIP, 1, 1, TIFFDataType.TIFF_LONG, FIELD.ROWSPERSTRIP, false, false, "RowsPerStrip"));
			ret.Add(new TIFFFieldInfo(TIFFTAG.ROWSPERSTRIP, 1, 1, TIFFDataType.TIFF_SHORT, FIELD.ROWSPERSTRIP, false, false, "RowsPerStrip"));
			ret.Add(new TIFFFieldInfo(TIFFTAG.STRIPBYTECOUNTS, -1, -1, TIFFDataType.TIFF_LONG, FIELD.STRIPBYTECOUNTS, false, false, "StripByteCounts"));
			ret.Add(new TIFFFieldInfo(TIFFTAG.STRIPBYTECOUNTS, -1, -1, TIFFDataType.TIFF_SHORT, FIELD.STRIPBYTECOUNTS, false, false, "StripByteCounts"));
			ret.Add(new TIFFFieldInfo(TIFFTAG.MINSAMPLEVALUE, -2, -1, TIFFDataType.TIFF_SHORT, FIELD.MINSAMPLEVALUE, true, false, "MinSampleValue"));
			ret.Add(new TIFFFieldInfo(TIFFTAG.MAXSAMPLEVALUE, -2, -1, TIFFDataType.TIFF_SHORT, FIELD.MAXSAMPLEVALUE, true, false, "MaxSampleValue"));
			ret.Add(new TIFFFieldInfo(TIFFTAG.XRESOLUTION, 1, 1, TIFFDataType.TIFF_RATIONAL, FIELD.RESOLUTION, true, false, "XResolution"));
			ret.Add(new TIFFFieldInfo(TIFFTAG.YRESOLUTION, 1, 1, TIFFDataType.TIFF_RATIONAL, FIELD.RESOLUTION, true, false, "YResolution"));
			ret.Add(new TIFFFieldInfo(TIFFTAG.PLANARCONFIG, 1, 1, TIFFDataType.TIFF_SHORT, FIELD.PLANARCONFIG, false, false, "PlanarConfiguration"));
			ret.Add(new TIFFFieldInfo(TIFFTAG.PAGENAME, -1, -1, TIFFDataType.TIFF_ASCII, FIELD.CUSTOM, true, false, "PageName"));
			ret.Add(new TIFFFieldInfo(TIFFTAG.XPOSITION, 1, 1, TIFFDataType.TIFF_RATIONAL, FIELD.POSITION, true, false, "XPosition"));
			ret.Add(new TIFFFieldInfo(TIFFTAG.YPOSITION, 1, 1, TIFFDataType.TIFF_RATIONAL, FIELD.POSITION, true, false, "YPosition"));
			ret.Add(new TIFFFieldInfo(TIFFTAG.FREEOFFSETS, -1, -1, TIFFDataType.TIFF_LONG, FIELD.IGNORE, false, false, "FreeOffsets"));
			ret.Add(new TIFFFieldInfo(TIFFTAG.FREEBYTECOUNTS, -1, -1, TIFFDataType.TIFF_LONG, FIELD.IGNORE, false, false, "FreeByteCounts"));
			ret.Add(new TIFFFieldInfo(TIFFTAG.GRAYRESPONSEUNIT, 1, 1, TIFFDataType.TIFF_SHORT, FIELD.IGNORE, true, false, "GrayResponseUnit"));
			ret.Add(new TIFFFieldInfo(TIFFTAG.GRAYRESPONSECURVE, -1, -1, TIFFDataType.TIFF_SHORT, FIELD.IGNORE, true, false, "GrayResponseCurve"));
			ret.Add(new TIFFFieldInfo(TIFFTAG.RESOLUTIONUNIT, 1, 1, TIFFDataType.TIFF_SHORT, FIELD.RESOLUTIONUNIT, true, false, "ResolutionUnit"));
			ret.Add(new TIFFFieldInfo(TIFFTAG.PAGENUMBER, 2, 2, TIFFDataType.TIFF_SHORT, FIELD.PAGENUMBER, true, false, "PageNumber"));
			ret.Add(new TIFFFieldInfo(TIFFTAG.COLORRESPONSEUNIT, 1, 1, TIFFDataType.TIFF_SHORT, FIELD.IGNORE, true, false, "ColorResponseUnit"));
			ret.Add(new TIFFFieldInfo(TIFFTAG.TRANSFERFUNCTION, -1, -1, TIFFDataType.TIFF_SHORT, FIELD.TRANSFERFUNCTION, true, false, "TransferFunction"));
			ret.Add(new TIFFFieldInfo(TIFFTAG.SOFTWARE, -1, -1, TIFFDataType.TIFF_ASCII, FIELD.CUSTOM, true, false, "Software"));
			ret.Add(new TIFFFieldInfo(TIFFTAG.DATETIME, 20, 20, TIFFDataType.TIFF_ASCII, FIELD.CUSTOM, true, false, "DateTime"));
			ret.Add(new TIFFFieldInfo(TIFFTAG.ARTIST, -1, -1, TIFFDataType.TIFF_ASCII, FIELD.CUSTOM, true, false, "Artist"));
			ret.Add(new TIFFFieldInfo(TIFFTAG.HOSTCOMPUTER, -1, -1, TIFFDataType.TIFF_ASCII, FIELD.CUSTOM, true, false, "HostComputer"));
			ret.Add(new TIFFFieldInfo(TIFFTAG.WHITEPOINT, 2, 2, TIFFDataType.TIFF_RATIONAL, FIELD.CUSTOM, true, false, "WhitePoint"));
			ret.Add(new TIFFFieldInfo(TIFFTAG.PRIMARYCHROMATICITIES, 6, 6, TIFFDataType.TIFF_RATIONAL, FIELD.CUSTOM, true, false, "PrimaryChromaticities"));
			ret.Add(new TIFFFieldInfo(TIFFTAG.COLORMAP, -1, -1, TIFFDataType.TIFF_SHORT, FIELD.COLORMAP, true, false, "ColorMap"));
			ret.Add(new TIFFFieldInfo(TIFFTAG.HALFTONEHINTS, 2, 2, TIFFDataType.TIFF_SHORT, FIELD.HALFTONEHINTS, true, false, "HalftoneHints"));
			ret.Add(new TIFFFieldInfo(TIFFTAG.TILEWIDTH, 1, 1, TIFFDataType.TIFF_LONG, FIELD.TILEDIMENSIONS, false, false, "TileWidth"));
			ret.Add(new TIFFFieldInfo(TIFFTAG.TILEWIDTH, 1, 1, TIFFDataType.TIFF_SHORT, FIELD.TILEDIMENSIONS, false, false, "TileWidth"));
			ret.Add(new TIFFFieldInfo(TIFFTAG.TILELENGTH, 1, 1, TIFFDataType.TIFF_LONG, FIELD.TILEDIMENSIONS, false, false, "TileLength"));
			ret.Add(new TIFFFieldInfo(TIFFTAG.TILELENGTH, 1, 1, TIFFDataType.TIFF_SHORT, FIELD.TILEDIMENSIONS, false, false, "TileLength"));
			ret.Add(new TIFFFieldInfo(TIFFTAG.TILEOFFSETS, -1, 1, TIFFDataType.TIFF_LONG, FIELD.STRIPOFFSETS, false, false, "TileOffsets"));
			ret.Add(new TIFFFieldInfo(TIFFTAG.TILEBYTECOUNTS, -1, 1, TIFFDataType.TIFF_LONG, FIELD.STRIPBYTECOUNTS, false, false, "TileByteCounts"));
			ret.Add(new TIFFFieldInfo(TIFFTAG.TILEBYTECOUNTS, -1, 1, TIFFDataType.TIFF_SHORT, FIELD.STRIPBYTECOUNTS, false, false, "TileByteCounts"));
			ret.Add(new TIFFFieldInfo(TIFFTAG.SUBIFD, -1, -1, TIFFDataType.TIFF_IFD, FIELD.SUBIFD, true, true, "SubIFD"));
			ret.Add(new TIFFFieldInfo(TIFFTAG.SUBIFD, -1, -1, TIFFDataType.TIFF_LONG, FIELD.SUBIFD, true, true, "SubIFD"));
			ret.Add(new TIFFFieldInfo(TIFFTAG.INKSET, 1, 1, TIFFDataType.TIFF_SHORT, FIELD.CUSTOM, false, false, "InkSet"));
			ret.Add(new TIFFFieldInfo(TIFFTAG.INKNAMES, -1, -1, TIFFDataType.TIFF_ASCII, FIELD.INKNAMES, true, true, "InkNames"));
			ret.Add(new TIFFFieldInfo(TIFFTAG.NUMBEROFINKS, 1, 1, TIFFDataType.TIFF_SHORT, FIELD.CUSTOM, true, false, "NumberOfInks"));
			ret.Add(new TIFFFieldInfo(TIFFTAG.DOTRANGE, 2, 2, TIFFDataType.TIFF_SHORT, FIELD.CUSTOM, false, false, "DotRange"));
			ret.Add(new TIFFFieldInfo(TIFFTAG.DOTRANGE, 2, 2, TIFFDataType.TIFF_BYTE, FIELD.CUSTOM, false, false, "DotRange"));
			ret.Add(new TIFFFieldInfo(TIFFTAG.TARGETPRINTER, -1, -1, TIFFDataType.TIFF_ASCII, FIELD.CUSTOM, true, false, "TargetPrinter"));
			ret.Add(new TIFFFieldInfo(TIFFTAG.EXTRASAMPLES, -1, -1, TIFFDataType.TIFF_SHORT, FIELD.EXTRASAMPLES, false, true, "ExtraSamples"));
			
			// XXX for bogus Adobe Photoshop v2.5 files
			ret.Add(new TIFFFieldInfo(TIFFTAG.EXTRASAMPLES, -1, -1, TIFFDataType.TIFF_BYTE, FIELD.EXTRASAMPLES, false, true, "ExtraSamples"));
			ret.Add(new TIFFFieldInfo(TIFFTAG.SAMPLEFORMAT, -1, -1, TIFFDataType.TIFF_SHORT, FIELD.SAMPLEFORMAT, false, false, "SampleFormat"));
			ret.Add(new TIFFFieldInfo(TIFFTAG.SMINSAMPLEVALUE, -2, -1, TIFFDataType.TIFF_ANY, FIELD.SMINSAMPLEVALUE, true, false, "SMinSampleValue"));
			ret.Add(new TIFFFieldInfo(TIFFTAG.SMAXSAMPLEVALUE, -2, -1, TIFFDataType.TIFF_ANY, FIELD.SMAXSAMPLEVALUE, true, false, "SMaxSampleValue"));
			ret.Add(new TIFFFieldInfo(TIFFTAG.CLIPPATH, -1, -1, TIFFDataType.TIFF_BYTE, FIELD.CUSTOM, false, true, "ClipPath"));
			ret.Add(new TIFFFieldInfo(TIFFTAG.XCLIPPATHUNITS, 1, 1, TIFFDataType.TIFF_SLONG, FIELD.CUSTOM, false, false, "XClipPathUnits"));
			ret.Add(new TIFFFieldInfo(TIFFTAG.XCLIPPATHUNITS, 1, 1, TIFFDataType.TIFF_SSHORT, FIELD.CUSTOM, false, false, "XClipPathUnits"));
			ret.Add(new TIFFFieldInfo(TIFFTAG.XCLIPPATHUNITS, 1, 1, TIFFDataType.TIFF_SBYTE, FIELD.CUSTOM, false, false, "XClipPathUnits"));
			ret.Add(new TIFFFieldInfo(TIFFTAG.YCLIPPATHUNITS, 1, 1, TIFFDataType.TIFF_SLONG, FIELD.CUSTOM, false, false, "YClipPathUnits"));
			ret.Add(new TIFFFieldInfo(TIFFTAG.YCLIPPATHUNITS, 1, 1, TIFFDataType.TIFF_SSHORT, FIELD.CUSTOM, false, false, "YClipPathUnits"));
			ret.Add(new TIFFFieldInfo(TIFFTAG.YCLIPPATHUNITS, 1, 1, TIFFDataType.TIFF_SBYTE, FIELD.CUSTOM, false, false, "YClipPathUnits"));
			ret.Add(new TIFFFieldInfo(TIFFTAG.YCBCRCOEFFICIENTS, 3, 3, TIFFDataType.TIFF_RATIONAL, FIELD.CUSTOM, false, false, "YCbCrCoefficients"));
			ret.Add(new TIFFFieldInfo(TIFFTAG.YCBCRSUBSAMPLING, 2, 2, TIFFDataType.TIFF_SHORT, FIELD.YCBCRSUBSAMPLING, false, false, "YCbCrSubsampling"));
			ret.Add(new TIFFFieldInfo(TIFFTAG.YCBCRPOSITIONING, 1, 1, TIFFDataType.TIFF_SHORT, FIELD.YCBCRPOSITIONING, false, false, "YCbCrPositioning"));
			ret.Add(new TIFFFieldInfo(TIFFTAG.REFERENCEBLACKWHITE, 6, 6, TIFFDataType.TIFF_RATIONAL, FIELD.REFBLACKWHITE, true, false, "ReferenceBlackWhite"));
			
			// XXX temporarily accept LONG for backwards compatibility
			ret.Add(new TIFFFieldInfo(TIFFTAG.REFERENCEBLACKWHITE, 6, 6, TIFFDataType.TIFF_LONG, FIELD.REFBLACKWHITE, true, false, "ReferenceBlackWhite"));
			ret.Add(new TIFFFieldInfo(TIFFTAG.XMLPACKET, -1, -1, TIFFDataType.TIFF_BYTE, FIELD.CUSTOM, false, true, "XMLPacket"));
			
			// begin SGI tags
			ret.Add(new TIFFFieldInfo(TIFFTAG.MATTEING, 1, 1, TIFFDataType.TIFF_SHORT, FIELD.EXTRASAMPLES, false, false, "Matteing"));
			ret.Add(new TIFFFieldInfo(TIFFTAG.DATATYPE, -2, -1, TIFFDataType.TIFF_SHORT, FIELD.SAMPLEFORMAT, false, false, "DataType"));
			ret.Add(new TIFFFieldInfo(TIFFTAG.IMAGEDEPTH, 1, 1, TIFFDataType.TIFF_LONG, FIELD.IMAGEDEPTH, false, false, "ImageDepth"));
			ret.Add(new TIFFFieldInfo(TIFFTAG.IMAGEDEPTH, 1, 1, TIFFDataType.TIFF_SHORT, FIELD.IMAGEDEPTH, false, false, "ImageDepth"));
			ret.Add(new TIFFFieldInfo(TIFFTAG.TILEDEPTH, 1, 1, TIFFDataType.TIFF_LONG, FIELD.TILEDEPTH, false, false, "TileDepth"));
			ret.Add(new TIFFFieldInfo(TIFFTAG.TILEDEPTH, 1, 1, TIFFDataType.TIFF_SHORT, FIELD.TILEDEPTH, false, false, "TileDepth"));
			// end SGI tags

			// begin Pixar tags
			ret.Add(new TIFFFieldInfo(TIFFTAG.PIXAR_IMAGEFULLWIDTH, 1, 1, TIFFDataType.TIFF_LONG, FIELD.CUSTOM, true, false, "ImageFullWidth"));
			ret.Add(new TIFFFieldInfo(TIFFTAG.PIXAR_IMAGEFULLLENGTH, 1, 1, TIFFDataType.TIFF_LONG, FIELD.CUSTOM, true, false, "ImageFullLength"));
			ret.Add(new TIFFFieldInfo(TIFFTAG.PIXAR_TEXTUREFORMAT, -1, -1, TIFFDataType.TIFF_ASCII, FIELD.CUSTOM, true, false, "TextureFormat"));
			ret.Add(new TIFFFieldInfo(TIFFTAG.PIXAR_WRAPMODES, -1, -1, TIFFDataType.TIFF_ASCII, FIELD.CUSTOM, true, false, "TextureWrapModes"));
			ret.Add(new TIFFFieldInfo(TIFFTAG.PIXAR_FOVCOT, 1, 1, TIFFDataType.TIFF_FLOAT, FIELD.CUSTOM, true, false, "FieldOfViewCotangent"));
			ret.Add(new TIFFFieldInfo(TIFFTAG.PIXAR_MATRIX_WORLDTOSCREEN, 16, 16, TIFFDataType.TIFF_FLOAT, FIELD.CUSTOM, true, false, "MatrixWorldToScreen"));
			ret.Add(new TIFFFieldInfo(TIFFTAG.PIXAR_MATRIX_WORLDTOCAMERA, 16, 16, TIFFDataType.TIFF_FLOAT, FIELD.CUSTOM, true, false, "MatrixWorldToCamera"));
			ret.Add(new TIFFFieldInfo(TIFFTAG.COPYRIGHT, -1, -1, TIFFDataType.TIFF_ASCII, FIELD.CUSTOM, true, false, "Copyright"));
			// end Pixar tags

			ret.Add(new TIFFFieldInfo(TIFFTAG.RICHTIFFIPTC, -1, -1, TIFFDataType.TIFF_LONG, FIELD.CUSTOM, false, true, "RichTIFFIPTC"));
			ret.Add(new TIFFFieldInfo(TIFFTAG.PHOTOSHOP, -1, -1, TIFFDataType.TIFF_BYTE, FIELD.CUSTOM, false, true, "Photoshop"));
			ret.Add(new TIFFFieldInfo(TIFFTAG.EXIFIFD, 1, 1, TIFFDataType.TIFF_LONG, FIELD.CUSTOM, false, false, "EXIFIFDOffset"));
			ret.Add(new TIFFFieldInfo(TIFFTAG.ICCPROFILE, -1, -1, TIFFDataType.TIFF_UNDEFINED, FIELD.CUSTOM, false, true, "ICC Profile"));
			ret.Add(new TIFFFieldInfo(TIFFTAG.GPSIFD, 1, 1, TIFFDataType.TIFF_LONG, FIELD.CUSTOM, false, false, "GPSIFDOffset"));
			ret.Add(new TIFFFieldInfo(TIFFTAG.STONITS, 1, 1, TIFFDataType.TIFF_DOUBLE, FIELD.CUSTOM, false, false, "StoNits"));
			ret.Add(new TIFFFieldInfo(TIFFTAG.INTEROPERABILITYIFD, 1, 1, TIFFDataType.TIFF_LONG, FIELD.CUSTOM, false, false, "InteroperabilityIFDOffset"));

			// begin DNG tags
			ret.Add(new TIFFFieldInfo(TIFFTAG.DNGVERSION, 4, 4, TIFFDataType.TIFF_BYTE, FIELD.CUSTOM, false, false, "DNGVersion"));
			ret.Add(new TIFFFieldInfo(TIFFTAG.DNGBACKWARDVERSION, 4, 4, TIFFDataType.TIFF_BYTE, FIELD.CUSTOM, false, false, "DNGBackwardVersion"));
			ret.Add(new TIFFFieldInfo(TIFFTAG.UNIQUECAMERAMODEL, -1, -1, TIFFDataType.TIFF_ASCII, FIELD.CUSTOM, true, false, "UniqueCameraModel"));
			ret.Add(new TIFFFieldInfo(TIFFTAG.LOCALIZEDCAMERAMODEL, -1, -1, TIFFDataType.TIFF_ASCII, FIELD.CUSTOM, true, false, "LocalizedCameraModel"));
			ret.Add(new TIFFFieldInfo(TIFFTAG.LOCALIZEDCAMERAMODEL, -1, -1, TIFFDataType.TIFF_BYTE, FIELD.CUSTOM, true, true, "LocalizedCameraModel"));
			ret.Add(new TIFFFieldInfo(TIFFTAG.CFAPLANECOLOR, -1, -1, TIFFDataType.TIFF_BYTE, FIELD.CUSTOM, false, true, "CFAPlaneColor"));
			ret.Add(new TIFFFieldInfo(TIFFTAG.CFALAYOUT, 1, 1, TIFFDataType.TIFF_SHORT, FIELD.CUSTOM, false, false, "CFALayout"));
			ret.Add(new TIFFFieldInfo(TIFFTAG.LINEARIZATIONTABLE, -1, -1, TIFFDataType.TIFF_SHORT, FIELD.CUSTOM, false, true, "LinearizationTable"));
			ret.Add(new TIFFFieldInfo(TIFFTAG.BLACKLEVELREPEATDIM, 2, 2, TIFFDataType.TIFF_SHORT, FIELD.CUSTOM, false, false, "BlackLevelRepeatDim"));
			ret.Add(new TIFFFieldInfo(TIFFTAG.BLACKLEVEL, -1, -1, TIFFDataType.TIFF_LONG, FIELD.CUSTOM, false, true, "BlackLevel"));
			ret.Add(new TIFFFieldInfo(TIFFTAG.BLACKLEVEL, -1, -1, TIFFDataType.TIFF_SHORT, FIELD.CUSTOM, false, true, "BlackLevel"));
			ret.Add(new TIFFFieldInfo(TIFFTAG.BLACKLEVEL, -1, -1, TIFFDataType.TIFF_RATIONAL, FIELD.CUSTOM, false, true, "BlackLevel"));
			ret.Add(new TIFFFieldInfo(TIFFTAG.BLACKLEVELDELTAH, -1, -1, TIFFDataType.TIFF_SRATIONAL, FIELD.CUSTOM, false, true, "BlackLevelDeltaH"));
			ret.Add(new TIFFFieldInfo(TIFFTAG.BLACKLEVELDELTAV, -1, -1, TIFFDataType.TIFF_SRATIONAL, FIELD.CUSTOM, false, true, "BlackLevelDeltaV"));
			ret.Add(new TIFFFieldInfo(TIFFTAG.WHITELEVEL, -2, -2, TIFFDataType.TIFF_LONG, FIELD.CUSTOM, false, false, "WhiteLevel"));
			ret.Add(new TIFFFieldInfo(TIFFTAG.WHITELEVEL, -2, -2, TIFFDataType.TIFF_SHORT, FIELD.CUSTOM, false, false, "WhiteLevel"));
			ret.Add(new TIFFFieldInfo(TIFFTAG.DEFAULTSCALE, 2, 2, TIFFDataType.TIFF_RATIONAL, FIELD.CUSTOM, false, false, "DefaultScale"));
			ret.Add(new TIFFFieldInfo(TIFFTAG.BESTQUALITYSCALE, 1, 1, TIFFDataType.TIFF_RATIONAL, FIELD.CUSTOM, false, false, "BestQualityScale"));
			ret.Add(new TIFFFieldInfo(TIFFTAG.DEFAULTCROPORIGIN, 2, 2, TIFFDataType.TIFF_LONG, FIELD.CUSTOM, false, false, "DefaultCropOrigin"));
			ret.Add(new TIFFFieldInfo(TIFFTAG.DEFAULTCROPORIGIN, 2, 2, TIFFDataType.TIFF_SHORT, FIELD.CUSTOM, false, false, "DefaultCropOrigin"));
			ret.Add(new TIFFFieldInfo(TIFFTAG.DEFAULTCROPORIGIN, 2, 2, TIFFDataType.TIFF_RATIONAL, FIELD.CUSTOM, false, false, "DefaultCropOrigin"));
			ret.Add(new TIFFFieldInfo(TIFFTAG.DEFAULTCROPSIZE, 2, 2, TIFFDataType.TIFF_LONG, FIELD.CUSTOM, false, false, "DefaultCropSize"));
			ret.Add(new TIFFFieldInfo(TIFFTAG.DEFAULTCROPSIZE, 2, 2, TIFFDataType.TIFF_SHORT, FIELD.CUSTOM, false, false, "DefaultCropSize"));
			ret.Add(new TIFFFieldInfo(TIFFTAG.DEFAULTCROPSIZE, 2, 2, TIFFDataType.TIFF_RATIONAL, FIELD.CUSTOM, false, false, "DefaultCropSize"));
			ret.Add(new TIFFFieldInfo(TIFFTAG.COLORMATRIX1, -1, -1, TIFFDataType.TIFF_SRATIONAL, FIELD.CUSTOM, false, true, "ColorMatrix1"));
			ret.Add(new TIFFFieldInfo(TIFFTAG.COLORMATRIX2, -1, -1, TIFFDataType.TIFF_SRATIONAL, FIELD.CUSTOM, false, true, "ColorMatrix2"));
			ret.Add(new TIFFFieldInfo(TIFFTAG.CAMERACALIBRATION1, -1, -1, TIFFDataType.TIFF_SRATIONAL, FIELD.CUSTOM, false, true, "CameraCalibration1"));
			ret.Add(new TIFFFieldInfo(TIFFTAG.CAMERACALIBRATION2, -1, -1, TIFFDataType.TIFF_SRATIONAL, FIELD.CUSTOM, false, true, "CameraCalibration2"));
			ret.Add(new TIFFFieldInfo(TIFFTAG.REDUCTIONMATRIX1, -1, -1, TIFFDataType.TIFF_SRATIONAL, FIELD.CUSTOM, false, true, "ReductionMatrix1"));
			ret.Add(new TIFFFieldInfo(TIFFTAG.REDUCTIONMATRIX2, -1, -1, TIFFDataType.TIFF_SRATIONAL, FIELD.CUSTOM, false, true, "ReductionMatrix2"));
			ret.Add(new TIFFFieldInfo(TIFFTAG.ANALOGBALANCE, -1, -1, TIFFDataType.TIFF_RATIONAL, FIELD.CUSTOM, false, true, "AnalogBalance"));
			ret.Add(new TIFFFieldInfo(TIFFTAG.ASSHOTNEUTRAL, -1, -1, TIFFDataType.TIFF_SHORT, FIELD.CUSTOM, false, true, "AsShotNeutral"));
			ret.Add(new TIFFFieldInfo(TIFFTAG.ASSHOTNEUTRAL, -1, -1, TIFFDataType.TIFF_RATIONAL, FIELD.CUSTOM, false, true, "AsShotNeutral"));
			ret.Add(new TIFFFieldInfo(TIFFTAG.ASSHOTWHITEXY, 2, 2, TIFFDataType.TIFF_RATIONAL, FIELD.CUSTOM, false, false, "AsShotWhiteXY"));
			ret.Add(new TIFFFieldInfo(TIFFTAG.BASELINEEXPOSURE, 1, 1, TIFFDataType.TIFF_SRATIONAL, FIELD.CUSTOM, false, false, "BaselineExposure"));
			ret.Add(new TIFFFieldInfo(TIFFTAG.BASELINENOISE, 1, 1, TIFFDataType.TIFF_RATIONAL, FIELD.CUSTOM, false, false, "BaselineNoise"));
			ret.Add(new TIFFFieldInfo(TIFFTAG.BASELINESHARPNESS, 1, 1, TIFFDataType.TIFF_RATIONAL, FIELD.CUSTOM, false, false, "BaselineSharpness"));
			ret.Add(new TIFFFieldInfo(TIFFTAG.BAYERGREENSPLIT, 1, 1, TIFFDataType.TIFF_LONG, FIELD.CUSTOM, false, false, "BayerGreenSplit"));
			ret.Add(new TIFFFieldInfo(TIFFTAG.LINEARRESPONSELIMIT, 1, 1, TIFFDataType.TIFF_RATIONAL, FIELD.CUSTOM, false, false, "LinearResponseLimit"));
			ret.Add(new TIFFFieldInfo(TIFFTAG.CAMERASERIALNUMBER, -1, -1, TIFFDataType.TIFF_ASCII, FIELD.CUSTOM, true, false, "CameraSerialNumber"));
			ret.Add(new TIFFFieldInfo(TIFFTAG.LENSINFO, 4, 4, TIFFDataType.TIFF_RATIONAL, FIELD.CUSTOM, false, false, "LensInfo"));
			ret.Add(new TIFFFieldInfo(TIFFTAG.CHROMABLURRADIUS, 1, 1, TIFFDataType.TIFF_RATIONAL, FIELD.CUSTOM, false, false, "ChromaBlurRadius"));
			ret.Add(new TIFFFieldInfo(TIFFTAG.ANTIALIASSTRENGTH, 1, 1, TIFFDataType.TIFF_RATIONAL, FIELD.CUSTOM, false, false, "AntiAliasStrength"));
			ret.Add(new TIFFFieldInfo(TIFFTAG.SHADOWSCALE, 1, 1, TIFFDataType.TIFF_RATIONAL, FIELD.CUSTOM, false, false, "ShadowScale"));
			ret.Add(new TIFFFieldInfo(TIFFTAG.DNGPRIVATEDATA, -1, -1, TIFFDataType.TIFF_BYTE, FIELD.CUSTOM, false, true, "DNGPrivateData"));
			ret.Add(new TIFFFieldInfo(TIFFTAG.MAKERNOTESAFETY, 1, 1, TIFFDataType.TIFF_SHORT, FIELD.CUSTOM, false, false, "MakerNoteSafety"));
			ret.Add(new TIFFFieldInfo(TIFFTAG.CALIBRATIONILLUMINANT1, 1, 1, TIFFDataType.TIFF_SHORT, FIELD.CUSTOM, false, false, "CalibrationIlluminant1"));
			ret.Add(new TIFFFieldInfo(TIFFTAG.CALIBRATIONILLUMINANT2, 1, 1, TIFFDataType.TIFF_SHORT, FIELD.CUSTOM, false, false, "CalibrationIlluminant2"));
			ret.Add(new TIFFFieldInfo(TIFFTAG.RAWDATAUNIQUEID, 16, 16, TIFFDataType.TIFF_BYTE, FIELD.CUSTOM, false, false, "RawDataUniqueID"));
			ret.Add(new TIFFFieldInfo(TIFFTAG.ORIGINALRAWFILENAME, -1, -1, TIFFDataType.TIFF_ASCII, FIELD.CUSTOM, true, false, "OriginalRawFileName"));
			ret.Add(new TIFFFieldInfo(TIFFTAG.ORIGINALRAWFILENAME, -1, -1, TIFFDataType.TIFF_BYTE, FIELD.CUSTOM, true, true, "OriginalRawFileName"));
			ret.Add(new TIFFFieldInfo(TIFFTAG.ORIGINALRAWFILEDATA, -1, -1, TIFFDataType.TIFF_UNDEFINED, FIELD.CUSTOM, false, true, "OriginalRawFileData"));
			ret.Add(new TIFFFieldInfo(TIFFTAG.ACTIVEAREA, 4, 4, TIFFDataType.TIFF_LONG, FIELD.CUSTOM, false, false, "ActiveArea"));
			ret.Add(new TIFFFieldInfo(TIFFTAG.ACTIVEAREA, 4, 4, TIFFDataType.TIFF_SHORT, FIELD.CUSTOM, false, false, "ActiveArea"));
			ret.Add(new TIFFFieldInfo(TIFFTAG.MASKEDAREAS, -1, -1, TIFFDataType.TIFF_LONG, FIELD.CUSTOM, false, true, "MaskedAreas"));
			ret.Add(new TIFFFieldInfo(TIFFTAG.ASSHOTICCPROFILE, -1, -1, TIFFDataType.TIFF_UNDEFINED, FIELD.CUSTOM, false, true, "AsShotICCProfile"));
			ret.Add(new TIFFFieldInfo(TIFFTAG.ASSHOTPREPROFILEMATRIX, -1, -1, TIFFDataType.TIFF_SRATIONAL, FIELD.CUSTOM, false, true, "AsShotPreProfileMatrix"));
			ret.Add(new TIFFFieldInfo(TIFFTAG.CURRENTICCPROFILE, -1, -1, TIFFDataType.TIFF_UNDEFINED, FIELD.CUSTOM, false, true, "CurrentICCProfile"));
			ret.Add(new TIFFFieldInfo(TIFFTAG.CURRENTPREPROFILEMATRIX, -1, -1, TIFFDataType.TIFF_SRATIONAL, FIELD.CUSTOM, false, true, "CurrentPreProfileMatrix"));
			// end DNG tags

			return ret;
		}

		static List<TIFFFieldInfo> exifFieldInfo=MakeExifFieldInfo();

		static List<TIFFFieldInfo> MakeExifFieldInfo()
		{
			List<TIFFFieldInfo> ret=new List<TIFFFieldInfo>();

			ret.Add(new TIFFFieldInfo((TIFFTAG)EXIFTAG.EXPOSURETIME, 1, 1, TIFFDataType.TIFF_RATIONAL, FIELD.CUSTOM, true, false, "ExposureTime"));
			ret.Add(new TIFFFieldInfo((TIFFTAG)EXIFTAG.FNUMBER, 1, 1, TIFFDataType.TIFF_RATIONAL, FIELD.CUSTOM, true, false, "FNumber"));
			ret.Add(new TIFFFieldInfo((TIFFTAG)EXIFTAG.EXPOSUREPROGRAM, 1, 1, TIFFDataType.TIFF_SHORT, FIELD.CUSTOM, true, false, "ExposureProgram"));
			ret.Add(new TIFFFieldInfo((TIFFTAG)EXIFTAG.SPECTRALSENSITIVITY, -1, -1, TIFFDataType.TIFF_ASCII, FIELD.CUSTOM, true, false, "SpectralSensitivity"));
			ret.Add(new TIFFFieldInfo((TIFFTAG)EXIFTAG.ISOSPEEDRATINGS, -1, -1, TIFFDataType.TIFF_SHORT, FIELD.CUSTOM, true, true, "ISOSpeedRatings"));
			ret.Add(new TIFFFieldInfo((TIFFTAG)EXIFTAG.OECF, -1, -1, TIFFDataType.TIFF_UNDEFINED, FIELD.CUSTOM, true, true, "OptoelectricConversionFactor"));
			ret.Add(new TIFFFieldInfo((TIFFTAG)EXIFTAG.EXIFVERSION, 4, 4, TIFFDataType.TIFF_UNDEFINED, FIELD.CUSTOM, true, false, "ExifVersion"));
			ret.Add(new TIFFFieldInfo((TIFFTAG)EXIFTAG.DATETIMEORIGINAL, 20, 20, TIFFDataType.TIFF_ASCII, FIELD.CUSTOM, true, false, "DateTimeOriginal"));
			ret.Add(new TIFFFieldInfo((TIFFTAG)EXIFTAG.DATETIMEDIGITIZED, 20, 20, TIFFDataType.TIFF_ASCII, FIELD.CUSTOM, true, false, "DateTimeDigitized"));
			ret.Add(new TIFFFieldInfo((TIFFTAG)EXIFTAG.COMPONENTSCONFIGURATION, 4, 4, TIFFDataType.TIFF_UNDEFINED, FIELD.CUSTOM, true, false, "ComponentsConfiguration"));
			ret.Add(new TIFFFieldInfo((TIFFTAG)EXIFTAG.COMPRESSEDBITSPERPIXEL, 1, 1, TIFFDataType.TIFF_RATIONAL, FIELD.CUSTOM, true, false, "CompressedBitsPerPixel"));
			ret.Add(new TIFFFieldInfo((TIFFTAG)EXIFTAG.SHUTTERSPEEDVALUE, 1, 1, TIFFDataType.TIFF_SRATIONAL, FIELD.CUSTOM, true, false, "ShutterSpeedValue"));
			ret.Add(new TIFFFieldInfo((TIFFTAG)EXIFTAG.APERTUREVALUE, 1, 1, TIFFDataType.TIFF_RATIONAL, FIELD.CUSTOM, true, false, "ApertureValue"));
			ret.Add(new TIFFFieldInfo((TIFFTAG)EXIFTAG.BRIGHTNESSVALUE, 1, 1, TIFFDataType.TIFF_SRATIONAL, FIELD.CUSTOM, true, false, "BrightnessValue"));
			ret.Add(new TIFFFieldInfo((TIFFTAG)EXIFTAG.EXPOSUREBIASVALUE, 1, 1, TIFFDataType.TIFF_SRATIONAL, FIELD.CUSTOM, true, false, "ExposureBiasValue"));
			ret.Add(new TIFFFieldInfo((TIFFTAG)EXIFTAG.MAXAPERTUREVALUE, 1, 1, TIFFDataType.TIFF_RATIONAL, FIELD.CUSTOM, true, false, "MaxApertureValue"));
			ret.Add(new TIFFFieldInfo((TIFFTAG)EXIFTAG.SUBJECTDISTANCE, 1, 1, TIFFDataType.TIFF_RATIONAL, FIELD.CUSTOM, true, false, "SubjectDistance"));
			ret.Add(new TIFFFieldInfo((TIFFTAG)EXIFTAG.METERINGMODE, 1, 1, TIFFDataType.TIFF_SHORT, FIELD.CUSTOM, true, false, "MeteringMode"));
			ret.Add(new TIFFFieldInfo((TIFFTAG)EXIFTAG.LIGHTSOURCE, 1, 1, TIFFDataType.TIFF_SHORT, FIELD.CUSTOM, true, false, "LightSource"));
			ret.Add(new TIFFFieldInfo((TIFFTAG)EXIFTAG.FLASH, 1, 1, TIFFDataType.TIFF_SHORT, FIELD.CUSTOM, true, false, "Flash"));
			ret.Add(new TIFFFieldInfo((TIFFTAG)EXIFTAG.FOCALLENGTH, 1, 1, TIFFDataType.TIFF_RATIONAL, FIELD.CUSTOM, true, false, "FocalLength"));
			ret.Add(new TIFFFieldInfo((TIFFTAG)EXIFTAG.SUBJECTAREA, -1, -1, TIFFDataType.TIFF_SHORT, FIELD.CUSTOM, true, true, "SubjectArea"));
			ret.Add(new TIFFFieldInfo((TIFFTAG)EXIFTAG.MAKERNOTE, -1, -1, TIFFDataType.TIFF_UNDEFINED, FIELD.CUSTOM, true, true, "MakerNote"));
			ret.Add(new TIFFFieldInfo((TIFFTAG)EXIFTAG.USERCOMMENT, -1, -1, TIFFDataType.TIFF_UNDEFINED, FIELD.CUSTOM, true, true, "UserComment"));
			ret.Add(new TIFFFieldInfo((TIFFTAG)EXIFTAG.SUBSECTIME, -1, -1, TIFFDataType.TIFF_ASCII, FIELD.CUSTOM, true, false, "SubSecTime"));
			ret.Add(new TIFFFieldInfo((TIFFTAG)EXIFTAG.SUBSECTIMEORIGINAL, -1, -1, TIFFDataType.TIFF_ASCII, FIELD.CUSTOM, true, false, "SubSecTimeOriginal"));
			ret.Add(new TIFFFieldInfo((TIFFTAG)EXIFTAG.SUBSECTIMEDIGITIZED, -1, -1, TIFFDataType.TIFF_ASCII, FIELD.CUSTOM, true, false, "SubSecTimeDigitized"));
			ret.Add(new TIFFFieldInfo((TIFFTAG)EXIFTAG.FLASHPIXVERSION, 4, 4, TIFFDataType.TIFF_UNDEFINED, FIELD.CUSTOM, true, false, "FlashpixVersion"));
			ret.Add(new TIFFFieldInfo((TIFFTAG)EXIFTAG.COLORSPACE, 1, 1, TIFFDataType.TIFF_SHORT, FIELD.CUSTOM, true, false, "ColorSpace"));
			ret.Add(new TIFFFieldInfo((TIFFTAG)EXIFTAG.PIXELXDIMENSION, 1, 1, TIFFDataType.TIFF_LONG, FIELD.CUSTOM, true, false, "PixelXDimension"));
			ret.Add(new TIFFFieldInfo((TIFFTAG)EXIFTAG.PIXELXDIMENSION, 1, 1, TIFFDataType.TIFF_SHORT, FIELD.CUSTOM, true, false, "PixelXDimension"));
			ret.Add(new TIFFFieldInfo((TIFFTAG)EXIFTAG.PIXELYDIMENSION, 1, 1, TIFFDataType.TIFF_LONG, FIELD.CUSTOM, true, false, "PixelYDimension"));
			ret.Add(new TIFFFieldInfo((TIFFTAG)EXIFTAG.PIXELYDIMENSION, 1, 1, TIFFDataType.TIFF_SHORT, FIELD.CUSTOM, true, false, "PixelYDimension"));
			ret.Add(new TIFFFieldInfo((TIFFTAG)EXIFTAG.RELATEDSOUNDFILE, 13, 13, TIFFDataType.TIFF_ASCII, FIELD.CUSTOM, true, false, "RelatedSoundFile"));
			ret.Add(new TIFFFieldInfo((TIFFTAG)EXIFTAG.FLASHENERGY, 1, 1, TIFFDataType.TIFF_RATIONAL, FIELD.CUSTOM, true, false, "FlashEnergy"));
			ret.Add(new TIFFFieldInfo((TIFFTAG)EXIFTAG.SPATIALFREQUENCYRESPONSE, -1, -1, TIFFDataType.TIFF_UNDEFINED, FIELD.CUSTOM, true, true, "SpatialFrequencyResponse"));
			ret.Add(new TIFFFieldInfo((TIFFTAG)EXIFTAG.FOCALPLANEXRESOLUTION, 1, 1, TIFFDataType.TIFF_RATIONAL, FIELD.CUSTOM, true, false, "FocalPlaneXResolution"));
			ret.Add(new TIFFFieldInfo((TIFFTAG)EXIFTAG.FOCALPLANEYRESOLUTION, 1, 1, TIFFDataType.TIFF_RATIONAL, FIELD.CUSTOM, true, false, "FocalPlaneYResolution"));
			ret.Add(new TIFFFieldInfo((TIFFTAG)EXIFTAG.FOCALPLANERESOLUTIONUNIT, 1, 1, TIFFDataType.TIFF_SHORT, FIELD.CUSTOM, true, false, "FocalPlaneResolutionUnit"));
			ret.Add(new TIFFFieldInfo((TIFFTAG)EXIFTAG.SUBJECTLOCATION, 2, 2, TIFFDataType.TIFF_SHORT, FIELD.CUSTOM, true, false, "SubjectLocation"));
			ret.Add(new TIFFFieldInfo((TIFFTAG)EXIFTAG.EXPOSUREINDEX, 1, 1, TIFFDataType.TIFF_RATIONAL, FIELD.CUSTOM, true, false, "ExposureIndex"));
			ret.Add(new TIFFFieldInfo((TIFFTAG)EXIFTAG.SENSINGMETHOD, 1, 1, TIFFDataType.TIFF_SHORT, FIELD.CUSTOM, true, false, "SensingMethod"));
			ret.Add(new TIFFFieldInfo((TIFFTAG)EXIFTAG.FILESOURCE, 1, 1, TIFFDataType.TIFF_UNDEFINED, FIELD.CUSTOM, true, false, "FileSource"));
			ret.Add(new TIFFFieldInfo((TIFFTAG)EXIFTAG.SCENETYPE, 1, 1, TIFFDataType.TIFF_UNDEFINED, FIELD.CUSTOM, true, false, "SceneType"));
			ret.Add(new TIFFFieldInfo((TIFFTAG)EXIFTAG.CFAPATTERN, -1, -1, TIFFDataType.TIFF_UNDEFINED, FIELD.CUSTOM, true, true, "CFAPattern"));
			ret.Add(new TIFFFieldInfo((TIFFTAG)EXIFTAG.CUSTOMRENDERED, 1, 1, TIFFDataType.TIFF_SHORT, FIELD.CUSTOM, true, false, "CustomRendered"));
			ret.Add(new TIFFFieldInfo((TIFFTAG)EXIFTAG.EXPOSUREMODE, 1, 1, TIFFDataType.TIFF_SHORT, FIELD.CUSTOM, true, false, "ExposureMode"));
			ret.Add(new TIFFFieldInfo((TIFFTAG)EXIFTAG.WHITEBALANCE, 1, 1, TIFFDataType.TIFF_SHORT, FIELD.CUSTOM, true, false, "WhiteBalance"));
			ret.Add(new TIFFFieldInfo((TIFFTAG)EXIFTAG.DIGITALZOOMRATIO, 1, 1, TIFFDataType.TIFF_RATIONAL, FIELD.CUSTOM, true, false, "DigitalZoomRatio"));
			ret.Add(new TIFFFieldInfo((TIFFTAG)EXIFTAG.FOCALLENGTHIN35MMFILM, 1, 1, TIFFDataType.TIFF_SHORT, FIELD.CUSTOM, true, false, "FocalLengthIn35mmFilm"));
			ret.Add(new TIFFFieldInfo((TIFFTAG)EXIFTAG.SCENECAPTURETYPE, 1, 1, TIFFDataType.TIFF_SHORT, FIELD.CUSTOM, true, false, "SceneCaptureType"));
			ret.Add(new TIFFFieldInfo((TIFFTAG)EXIFTAG.GAINCONTROL, 1, 1, TIFFDataType.TIFF_RATIONAL, FIELD.CUSTOM, true, false, "GainControl"));
			ret.Add(new TIFFFieldInfo((TIFFTAG)EXIFTAG.CONTRAST, 1, 1, TIFFDataType.TIFF_SHORT, FIELD.CUSTOM, true, false, "Contrast"));
			ret.Add(new TIFFFieldInfo((TIFFTAG)EXIFTAG.SATURATION, 1, 1, TIFFDataType.TIFF_SHORT, FIELD.CUSTOM, true, false, "Saturation"));
			ret.Add(new TIFFFieldInfo((TIFFTAG)EXIFTAG.SHARPNESS, 1, 1, TIFFDataType.TIFF_SHORT, FIELD.CUSTOM, true, false, "Sharpness"));
			ret.Add(new TIFFFieldInfo((TIFFTAG)EXIFTAG.DEVICESETTINGDESCRIPTION, -1, -1, TIFFDataType.TIFF_UNDEFINED, FIELD.CUSTOM, true, true, "DeviceSettingDescription"));
			ret.Add(new TIFFFieldInfo((TIFFTAG)EXIFTAG.SUBJECTDISTANCERANGE, 1, 1, TIFFDataType.TIFF_SHORT, FIELD.CUSTOM, true, false, "SubjectDistanceRange"));
			ret.Add(new TIFFFieldInfo((TIFFTAG)EXIFTAG.IMAGEUNIQUEID, 33, 33, TIFFDataType.TIFF_ASCII, FIELD.CUSTOM, true, false, "ImageUniqueID"));

			return ret;
		}

		public static void TIFFSetupFieldInfo(TIFF tif, TIFFFieldInfo info)
		{
			tif.tif_fieldinfo.Clear();

			if(!_TIFFMergeFieldInfo(tif, info))
				TIFFErrorExt(tif.tif_clientdata, "TIFFSetupFieldInfo", "Setting up field info failed");
		}

		public static void TIFFSetupFieldInfo(TIFF tif, List<TIFFFieldInfo> info)
		{
			tif.tif_fieldinfo.Clear();

			if(!_TIFFMergeFieldInfo(tif, info))
				TIFFErrorExt(tif.tif_clientdata, "TIFFSetupFieldInfo", "Setting up field info failed");
		}

		static int tagCompare(TIFFFieldInfo ta, TIFFFieldInfo tb)
		{
			if(ta.field_tag!=tb.field_tag) return (int)ta.field_tag-(int)tb.field_tag;
			return (int)tb.field_type-(int)ta.field_type;
		}

		static int tagNameCompare(TIFFFieldInfo ta, TIFFFieldInfo tb)
		{
			int ret=ta.field_name.CompareTo(tb.field_name);
			if(ret!=0) return ret;
			return (ta.field_type==TIFFDataType.TIFF_ANY)?0:((int)tb.field_type-(int)ta.field_type);
		}

		public static void TIFFMergeFieldInfo(TIFF tif, TIFFFieldInfo info)
		{
			if(!_TIFFMergeFieldInfo(tif, info))
			{
				TIFFErrorExt(tif.tif_clientdata, "TIFFMergeFieldInfo", "Merging block of 1 fields failed");
			}
		}

		public static void TIFFMergeFieldInfo(TIFF tif, List<TIFFFieldInfo> info)
		{
			if(!_TIFFMergeFieldInfo(tif, info))
			{
				TIFFErrorExt(tif.tif_clientdata, "TIFFMergeFieldInfo", "Merging block of %d fields failed", info.Count);
			}
		}

		public static bool _TIFFMergeFieldInfo(TIFF tif, TIFFFieldInfo info)
		{
			string module="_TIFFMergeFieldInfo";

			tif.tif_foundfield=null;

			try
			{
				TIFFFieldInfo fip=TIFFFindFieldInfo(tif, info.field_tag, info.field_type);
				if(fip==null) tif.tif_fieldinfo.Add(info);
			}
			catch
			{
				TIFFErrorExt(tif.tif_clientdata, module, "Failed to allocate field info array");
				return false;
			}

			// Sort the field info by tag number
			tif.tif_fieldinfo.Sort(tagCompare);

			return true;
		}

		public static bool _TIFFMergeFieldInfo(TIFF tif, List<TIFFFieldInfo> info)
		{
			string module="_TIFFMergeFieldInfo";

			tif.tif_foundfield=null;

			try
			{
				foreach(TIFFFieldInfo fi in info)
				{
					TIFFFieldInfo fip=TIFFFindFieldInfo(tif, fi.field_tag, fi.field_type);
					if(fip==null) tif.tif_fieldinfo.Add(fi);
				}
			}
			catch
			{
				TIFFErrorExt(tif.tif_clientdata, module, "Failed to allocate field info array");
				return false;
			}

			// Sort the field info by tag number
			tif.tif_fieldinfo.Sort(tagCompare);
	
			return true;
		}

		public static void TIFFPrintFieldInfo(TIFF tif, StreamWriter fd)
		{
			fd.WriteLine("{0}: ", tif.tif_name);

			int i=0;
			foreach(TIFFFieldInfo fip in tif.tif_fieldinfo)
				fd.WriteLine("field[{0, 2}] {1, 5}, {2, 2}, {3, 2}, {4}, {5, 2}, {6}, {7}, {8}", i++, fip.field_tag,
					fip.field_readcount, fip.field_writecount, fip.field_type, fip.field_bit,
					fip.field_oktochange?"TRUE ":"FALSE", fip.field_passcount?"TRUE ":"FALSE", fip.field_name);
		}

		// Return size of TIFFDataType in bytes
		public static int TIFFDataWidth(TIFFDataType type)
		{
			switch(type)
			{
				case TIFFDataType.TIFF_NOTYPE:
				case TIFFDataType.TIFF_BYTE:
				case TIFFDataType.TIFF_ASCII:
				case TIFFDataType.TIFF_SBYTE:
				case TIFFDataType.TIFF_UNDEFINED: return 1;
				case TIFFDataType.TIFF_SHORT:
				case TIFFDataType.TIFF_SSHORT: return 2;
				case TIFFDataType.TIFF_LONG:
				case TIFFDataType.TIFF_SLONG:
				case TIFFDataType.TIFF_FLOAT:
				case TIFFDataType.TIFF_IFD: return 4;
				case TIFFDataType.TIFF_RATIONAL:
				case TIFFDataType.TIFF_SRATIONAL:
				case TIFFDataType.TIFF_DOUBLE: return 8;
				default: return 0; // will return 0 for unknown types
			}
		}

		// Return size of TIFFDataType in bytes.
		//
		// XXX: We need a separate function to determine the space needed
		// to store the value. For TIFF_RATIONAL values TIFFDataWidth() returns 8,
		// but we use 4-byte float to represent rationals.
		static int TIFFDataSize(TIFFDataType type)
		{
			switch(type)
			{
				case TIFFDataType.TIFF_BYTE:
				case TIFFDataType.TIFF_ASCII:
				case TIFFDataType.TIFF_SBYTE:
				case TIFFDataType.TIFF_UNDEFINED: return 1;
				case TIFFDataType.TIFF_SHORT:
				case TIFFDataType.TIFF_SSHORT: return 2;
				case TIFFDataType.TIFF_LONG:
				case TIFFDataType.TIFF_SLONG:
				case TIFFDataType.TIFF_FLOAT:
				case TIFFDataType.TIFF_IFD:
				case TIFFDataType.TIFF_RATIONAL:
				case TIFFDataType.TIFF_SRATIONAL: return 4;
				case TIFFDataType.TIFF_DOUBLE: return 8;
				default: return 0;
			}
		}

		// Return nearest TIFFDataType to the sample type of an image.
		static TIFFDataType TIFFSampleToTagType(TIFF tif)
		{
			uint bps=TIFFhowmany8(tif.tif_dir.td_bitspersample);

			switch(tif.tif_dir.td_sampleformat)
			{
				case SAMPLEFORMAT.IEEEFP: return (bps==4?TIFFDataType.TIFF_FLOAT:TIFFDataType.TIFF_DOUBLE);
				case SAMPLEFORMAT.INT: return (bps<=1?TIFFDataType.TIFF_SBYTE:bps<=2?TIFFDataType.TIFF_SSHORT:TIFFDataType.TIFF_SLONG);
				case SAMPLEFORMAT.UINT: return (bps<=1?TIFFDataType.TIFF_BYTE:bps<=2?TIFFDataType.TIFF_SHORT:TIFFDataType.TIFF_LONG);
				case SAMPLEFORMAT.VOID: return TIFFDataType.TIFF_UNDEFINED;
			}
			// NOTREACHED
			return TIFFDataType.TIFF_UNDEFINED;
		}

		static TIFFFieldInfo TIFFFindFieldInfo(TIFF tif, TIFFTAG tag, TIFFDataType dt)
		{
			if(tif.tif_foundfield!=null&&tif.tif_foundfield.field_tag==tag&&
				(dt==TIFFDataType.TIFF_ANY||dt==tif.tif_foundfield.field_type)) return tif.tif_foundfield;

			// If we are invoked with no field information, then just return.
			if(tif.tif_fieldinfo==null||tif.tif_fieldinfo.Count==0) return null;

#if !USE_TIFFFindFieldInfoSearch
			foreach(TIFFFieldInfo fip in tif.tif_fieldinfo)
			{
				if(fip.field_tag==tag&&(dt==TIFFDataType.TIFF_ANY||fip.field_type==dt)) return tif.tif_foundfield=fip;
			}

			return null;
#else
			return TIFFFindFieldInfoSearch(tif, tag, dt, 0, tif.tif_fieldinfo.Count);
#endif
		}

#if USE_TIFFFindFieldInfoSearch
		static TIFFFieldInfo TIFFFindFieldInfoSearch(TIFF tif, TIFFTAG tag, TIFFDataType dt, int min, int num)
		{
			if(num==0) return null;

			TIFFFieldInfo fip=tif.tif_fieldinfo[min+num/2];
			if(fip.field_tag==tag)
			{
				int pos=min+num/2;
				if(dt==TIFFDataType.TIFF_ANY)
				{
					for(; ; ) // Find first
					{
						if(pos==0) break;
						if(tif.tif_fieldinfo[pos-1].field_tag!=tag) break;

						pos--;
						fip=tif.tif_fieldinfo[pos];
					}

					return tif.tif_foundfield=fip;
				}

				if(fip.field_type==dt) return tif.tif_foundfield=fip;

				//if(fip.field_type>dt) return TIFFFindFieldInfoSearch(tif, tag, dt, min, num/2);
				//return TIFFFindFieldInfoSearch(tif, tag, dt, min+num/2+1, num-(num/2+1));

				for(; ; ) // preceding fieldinfos and exit if found DataType
				{
					if(pos==0) break;
					if(tif.tif_fieldinfo[pos-1].field_tag!=tag) break;

					pos--;
					fip=tif.tif_fieldinfo[pos];

					if(fip.field_type==dt) return tif.tif_foundfield=fip;
				}

				pos=min+num/2;

				for(; ; ) // succeding fieldinfos  first and exit if found DataType
				{
					if(pos==(tif.tif_fieldinfo.Count-1)) break;
					if(tif.tif_fieldinfo[pos+1].field_tag!=tag) break;

					pos++;
					fip=tif.tif_fieldinfo[pos];

					if(fip.field_type==dt) return tif.tif_foundfield=fip;
				}

				return null;
			}

			if(fip.field_tag>tag) return TIFFFindFieldInfoSearch(tif, tag, dt, min, num/2);
			return TIFFFindFieldInfoSearch(tif, tag, dt, min+num/2+1, num-(num/2+1));
		}
#endif

		static TIFFFieldInfo TIFFFindFieldInfoByName(TIFF tif, string field_name, TIFFDataType dt)
		{
			if(tif.tif_foundfield!=null&&tif.tif_foundfield.field_name==field_name&&
				(dt==TIFFDataType.TIFF_ANY||dt==tif.tif_foundfield.field_type)) return tif.tif_foundfield;

			// If we are invoked with no field information, then just return.
			if(tif.tif_fieldinfo==null||tif.tif_fieldinfo.Count==0) return null;

			foreach(TIFFFieldInfo fip in tif.tif_fieldinfo)
			{
				if(fip.field_name==field_name&&(dt==TIFFDataType.TIFF_ANY||fip.field_type==dt)) return tif.tif_foundfield=fip;
			}

			return null;
		}

		static TIFFFieldInfo TIFFFieldWithTag(TIFF tif, TIFFTAG tag)
		{
			TIFFFieldInfo fip=TIFFFindFieldInfo(tif, tag, TIFFDataType.TIFF_ANY);
			if(fip==null)
			{
				TIFFErrorExt(tif.tif_clientdata, "TIFFFieldWithTag", "Internal error, unknown tag 0x{0:X}", tag);

#if DEBUG
				throw new Exception("fip==null");
#endif
				// NOTREACHED
			}
			return fip;
		}

		static TIFFFieldInfo TIFFFieldWithName(TIFF tif, string field_name)
		{
			TIFFFieldInfo fip=TIFFFindFieldInfoByName(tif, field_name, TIFFDataType.TIFF_ANY);
			if(fip==null)
			{
				TIFFErrorExt(tif.tif_clientdata, "TIFFFieldWithName", "Internal error, unknown tag {0}", field_name);

#if DEBUG
				throw new Exception("fip==null");
#endif
				// NOTREACHED
			}
			return fip;
		}

		static TIFFFieldInfo TIFFFindOrRegisterFieldInfo(TIFF tif, TIFFTAG tag, TIFFDataType dt)
		{
			TIFFFieldInfo fld=TIFFFindFieldInfo(tif, tag, dt);
			if(fld==null)
			{
				fld=TIFFCreateAnonFieldInfo(tif, tag, dt);
				if(!_TIFFMergeFieldInfo(tif, fld)) return null;
			}

			return fld;
		}

		static TIFFFieldInfo TIFFCreateAnonFieldInfo(TIFF tif, TIFFTAG tag, TIFFDataType field_type)
		{
			try
			{
				// ??? TIFFFieldInfo fld=new TIFFFieldInfo(tag, TIFF_VARIABLE2, TIFF_VARIABLE2, field_type, FIELD.CUSTOM, true, true, "Tag "+tag);
				TIFFFieldInfo fld=new TIFFFieldInfo(tag, TIFF_VARIABLE, TIFF_VARIABLE, field_type, FIELD.CUSTOM, true, true, "Tag "+tag);
				return fld;
			}
			catch
			{
				return null;
			}
		}
	}
}
