// tiff.cs
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

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace Free.Ports.LibTiff
{
	// Tag Image File Format (TIFF)
	//
	// Based on Rev 6.0 from:
	//		Developer's Desk
	//		Aldus Corporation
	//		411 First Ave. South
	//		Suite 200
	//		Seattle, WA 98104
	//		206-622-5500
	//
	//		(http://partners.adobe.com/asn/developer/PDFS/TN/TIFF6.pdf)
	//
	// For Big TIFF design notes see the following link
	//		http://www.remotesensing.org/libtiff/bigtiffdesign.html

	public static partial class libtiff
	{
		public const int TIFF_VERSION=42;
		public const int TIFF_BIGTIFF_VERSION=43;

		public const ushort TIFF_BIGENDIAN=0x4d4d;
		public const ushort TIFF_LITTLEENDIAN=0x4949;
		public const ushort MDI_LITTLEENDIAN=0x5045;
		public const ushort MDI_BIGENDIAN=0x4550;

		public const int TIFF_MAGIC_SIZE=2;
		public const int TIFF_VERSION_SIZE=2;
		public const int TIFF_DIROFFSET_SIZE=4;

		// Set the native cpu bit order (FILLORDER_LSB2MSB or FILLORDER_MSB2LSB)
		const TIF_FLAGS HOST_FILLORDER=TIF_FLAGS.FILLORDER_LSB2MSB;
	}

	// TIFF header.
	public class TIFFHeader
	{
		public ushort tiff_magic;	// magic number (defines byte order)
		public ushort tiff_version;	// TIFF version number
		public uint tiff_diroff;	// byte offset to first directory
	}

	// TIFF Image File Directories are comprised of a table of field
	// descriptors of the form shown below. The table is sorted in
	// ascending order by tag. The values associated with each entry are
	// disjoint and may appear anywhere in the file (so long as they are
	// placed on a word boundary).
	//
	// If the value is 4 bytes or less, then it is placed in the offset
	// field to save space. If the value is less than 4 bytes, it is
	// left-justified in the offset field.
	public class TIFFDirEntry
	{
		public ushort tdir_tag;		// see below
		public ushort tdir_type;	// data type; see below
		public uint tdir_count;		// number of items; length in spec
		public uint tdir_offset;	// byte offset to field data
	}

	// NB: In the comments below,
	//	- items marked with a + are obsoleted by revision 5.0,
	//	- items marked with a ! are introduced in revision 6.0.
	//	- items marked with a % are introduced post revision 6.0.
	//	- items marked with a $ are obsoleted by revision 6.0.
	//	- items marked with a & are introduced by Adobe DNG specification.

	// Tag data type information.
	//
	// Note: RATIONALs are the ratio of two 32-bit integer values.
	public enum TIFFDataType
	{
		TIFF_NOTYPE=0,		// placeholder
		TIFF_BYTE=1,		// 8-bit unsigned integer
		TIFF_ASCII=2,		// 8-bit bytes w/ last byte null
		TIFF_SHORT=3,		// 16-bit unsigned integer
		TIFF_LONG=4,		// 32-bit unsigned integer
		TIFF_RATIONAL=5,	// 64-bit unsigned fraction
		TIFF_SBYTE=6,		// !8-bit signed integer
		TIFF_UNDEFINED=7,	// !8-bit untyped data
		TIFF_SSHORT=8,		// !16-bit signed integer
		TIFF_SLONG=9,		// !32-bit signed integer
		TIFF_SRATIONAL=10,	// !64-bit signed fraction
		TIFF_FLOAT=11,		// !32-bit IEEE floating point
		TIFF_DOUBLE=12,		// !64-bit IEEE floating point
		TIFF_IFD=13,		// %32-bit unsigned integer (offset)

		// Stuff, related to tag handling and creating custom tags.
		TIFF_ANY=TIFF_NOTYPE,	// for field descriptor searching
	}

	// TIFF Tag Definitions.
	public enum TIFFTAG
	{
		SUBFILETYPE=254,			// subfile data descriptor
		OSUBFILETYPE=255,			// +kind of data in subfile
		IMAGEWIDTH=256,				// image width in pixels
		IMAGELENGTH=257,			// image height in pixels
		BITSPERSAMPLE=258,			// bits per channel (sample)
		COMPRESSION=259,			// data compression technique
		PHOTOMETRIC=262,			// photometric interpretation
		THRESHHOLDING=263,			// +thresholding used on data
		CELLWIDTH=264,				// +dithering matrix width
		CELLLENGTH=265,				// +dithering matrix height
		FILLORDER=266,				// data order within a byte
		DOCUMENTNAME=269,			// name of doc. image is from
		IMAGEDESCRIPTION=270,		// info about image
		MAKE=271,					// scanner manufacturer name
		MODEL=272,					// scanner model name/number
		STRIPOFFSETS=273,			// offsets to data strips
		ORIENTATION=274,			// +image orientation
		SAMPLESPERPIXEL=277,		// samples per pixel
		ROWSPERSTRIP=278,			// rows per strip of data
		STRIPBYTECOUNTS=279,		// bytes counts for strips
		MINSAMPLEVALUE=280,			// +minimum sample value
		MAXSAMPLEVALUE=281,			// +maximum sample value
		XRESOLUTION=282,			// pixels/resolution in x
		YRESOLUTION=283,			// pixels/resolution in y
		PLANARCONFIG=284,			// storage organization
		PAGENAME=285,				// page name image is from
		XPOSITION=286,				// x page offset of image lhs
		YPOSITION=287,				// y page offset of image lhs
		FREEOFFSETS=288,			// +byte offset to free block
		FREEBYTECOUNTS=289,			// +sizes of free blocks
		GRAYRESPONSEUNIT=290,		// $gray scale curve accuracy
		GRAYRESPONSECURVE=291,		// $gray scale response curve
		GROUP3OPTIONS=292,			// 32 flag bits
		T4OPTIONS=292,				// TIFF 6.0 proper name alias
		GROUP4OPTIONS=293,			// 32 flag bits
		T6OPTIONS=293,				// TIFF 6.0 proper name
		RESOLUTIONUNIT=296,			// units of resolutions
		PAGENUMBER=297,				// page numbers of multi-page
		COLORRESPONSEUNIT=300,		// $color curve accuracy
		TRANSFERFUNCTION=301,		// !colorimetry info
		SOFTWARE=305,				// name & release
		DATETIME=306,				// creation date and time
		ARTIST=315,					// creator of image
		HOSTCOMPUTER=316,			// machine where created
		PREDICTOR=317,				// prediction scheme w/ LZW
		WHITEPOINT=318,				// image white point
		PRIMARYCHROMATICITIES=319,	// !primary chromaticities
		COLORMAP=320,				// RGB map for pallette image
		HALFTONEHINTS=321,			// !highlight+shadow info
		TILEWIDTH=322,				// !rows/data tile
		TILELENGTH=323,				// !cols/data tile
		TILEOFFSETS=324,			// !offsets to data tiles
		TILEBYTECOUNTS=325,			// !byte counts for tiles
		BADFAXLINES=326,			// lines w/ wrong pixel count
		CLEANFAXDATA=327,			// regenerated line info
		CONSECUTIVEBADFAXLINES=328,	// max consecutive bad lines
		SUBIFD=330,					// subimage descriptors
		INKSET=332,					// !inks in separated image
		INKNAMES=333,				// !ascii names of inks
		NUMBEROFINKS=334,			// !number of inks
		DOTRANGE=336,				// !0% and 100% dot codes
		TARGETPRINTER=337,			// !separation target
		EXTRASAMPLES=338,			// !info about extra samples
		SAMPLEFORMAT=339,			// !data sample format
		SMINSAMPLEVALUE=340,		// !variable MinSampleValue
		SMAXSAMPLEVALUE=341,		// !variable MaxSampleValue
		CLIPPATH=343,				// %ClipPath [Adobe TIFF technote 2]
		XCLIPPATHUNITS=344,			// %XClipPathUnits [Adobe TIFF technote 2]
		YCLIPPATHUNITS=345,			// %YClipPathUnits [Adobe TIFF technote 2]
		INDEXED=346,				// %Indexed [Adobe TIFF Technote 3]
		JPEGTABLES=347,				// %JPEG table stream
		OPIPROXY=351,				// %OPI Proxy [Adobe TIFF technote]

		// Tags 512-521 are obsoleted by Technical Note #2 which specifies a revised JPEG-in-TIFF scheme.
		JPEGPROC=512,				// !JPEG processing algorithm
		JPEGIFOFFSET=513,			// !pointer to SOI marker
		JPEGIFBYTECOUNT=514,		// !JFIF stream length
		JPEGRESTARTINTERVAL=515,	// !restart interval length
		JPEGLOSSLESSPREDICTORS=517,	// !lossless proc predictor
		JPEGPOINTTRANSFORM=518,		// !lossless point transform
		JPEGQTABLES=519,			// !Q matrice offsets
		JPEGDCTABLES=520,			// !DCT table offsets
		JPEGACTABLES=521,			// !AC coefficient offsets

		YCBCRCOEFFICIENTS=529,		// !RGB => YCbCr transform
		YCBCRSUBSAMPLING=530,		// !YCbCr subsampling factors
		YCBCRPOSITIONING=531,		// !subsample positioning
		REFERENCEBLACKWHITE=532,	// !colorimetry info
		XMLPACKET=700,				// %XML packet [Adobe XMP Specification, January 2004
		OPIIMAGEID=32781,			// %OPI ImageID [Adobe TIFF technote]

		// tags 32952-32956 are private tags registered to Island Graphics
		REFPTS=32953,				// image reference points
		REGIONTACKPOINT=32954,		// region-xform tack point
		REGIONWARPCORNERS=32955,	// warp quadrilateral
		REGIONAFFINE=32956,			// affine transformation mat

		// tags 32995-32999 are private tags registered to SGI
		MATTEING=32995,				// $use ExtraSamples
		DATATYPE=32996,				// $use SampleFormat
		IMAGEDEPTH=32997,			// z depth of image
		TILEDEPTH=32998,			// z depth/data tile

		// tags 33300-33309 are private tags registered to Pixar
		//
		// TIFFTAG_PIXAR_IMAGEFULLWIDTH and TIFFTAG_PIXAR_IMAGEFULLLENGTH
		// are set when an image has been cropped out of a larger image.
		// They reflect the size of the original uncropped image.
		// The TIFFTAG_XPOSITION and TIFFTAG_YPOSITION can be used
		// to determine the position of the smaller image in the larger one.
		PIXAR_IMAGEFULLWIDTH=33300,		// full image size in x
		PIXAR_IMAGEFULLLENGTH=33301,	// full image size in y

		// tags 33302-33306 are used to identify special image modes and data
		// used by Pixar's texture formats.
		PIXAR_TEXTUREFORMAT=33302,		// texture map format
		PIXAR_WRAPMODES=33303,			// s & t wrap modes
		PIXAR_FOVCOT=33304,				// cotan(fov) for env. maps
		PIXAR_MATRIX_WORLDTOSCREEN=33305,
		PIXAR_MATRIX_WORLDTOCAMERA=33306,

		// tag 33405 is a private tag registered to Eastman Kodak
		WRITERSERIALNUMBER=33405,		// device serial number

		// tag 33432 is listed in the 6.0 spec w/ unknown ownership
		COPYRIGHT=33432,				// copyright string

		// IPTC TAG from RichTIFF specifications
		RICHTIFFIPTC=33723,

		// tag 33918-33920? is a private tag registered to INTERGRAPH
		INTERGRAPH_PACKET_DATA=33918,
		INTERGRAPH_FLAG_REGISTERS=33919,
		INTERGRAPH_MATRIX=33920,

		// tags 34016-34029 are reserved for ANSI IT8 TIFF/IT <dkelly@apago.com)
		IT8SITE=34016,						// site name
		IT8COLORSEQUENCE=34017,				// color seq. [RGB,CMYK,etc]
		IT8HEADER=34018,					// DDES Header
		IT8RASTERPADDING=34019,				// raster scanline padding
		IT8BITSPERRUNLENGTH=34020,			// # of bits in short run
		IT8BITSPEREXTENDEDRUNLENGTH=34021,	// # of bits in long run
		IT8COLORTABLE=34022,				// LW colortable
		IT8IMAGECOLORINDICATOR=34023,		// BP/BL image color switch
		IT8BKGCOLORINDICATOR=34024,			// BP/BL bg color switch
		IT8IMAGECOLORVALUE=34025,			// BP/BL image color value
		IT8BKGCOLORVALUE=34026,				// BP/BL bg color value
		IT8PIXELINTENSITYRANGE=34027,		// MP pixel intensity value
		IT8TRANSPARENCYINDICATOR=34028,		// HC transparency switch
		IT8COLORCHARACTERIZATION=34029,		// color character. table
		IT8HCUSAGE=34030,					// HC usage indicator
		IT8TRAPINDICATOR=34031,				// Trapping indicator (untrapped=0, trapped=1)
		IT8CMYKEQUIVALENT=34032,			// CMYK color equivalents

		// tags 34232-34236 are private tags registered to Texas Instruments
		FRAMECOUNT=34232,			// Sequence Frame Count

		// tag 34377 is private tag registered to Adobe for PhotoShop
		PHOTOSHOP=34377,

		// tags 34665, 34853 and 40965 are documented in EXIF specification
		EXIFIFD=34665,				// Pointer to EXIF private directory

		// tag 34675 is a private tag registered to Adobe?
		ICCPROFILE=34675,			// ICC profile data

		// tag 34750 is a private tag registered to Pixel Magic
		JBIGOPTIONS=34750,			// JBIG options

		GPSIFD=34853,				// Pointer to GPS private directory

		// tags 34908-34914 are private tags registered to SGI
		FAXRECVPARAMS=34908,		// encoded Class 2 ses. parms
		FAXSUBADDRESS=34909,		// received SubAddr string
		FAXRECVTIME=34910,			// receive time (secs)
		FAXDCS=34911,				// encoded fax ses. params, Table 2/T.30

		// tags 37439-37443 are registered to SGI <gregl@sgi.com>
		STONITS=37439,				// Sample value to Nits

		// tag 34929 is a private tag registered to FedEx
		FEDEX_EDR=34929,			// unknown use

		INTEROPERABILITYIFD=40965,	// Pointer to Interoperability private directory

		// Adobe Digital Negative (DNG) format tags
		DNGVERSION=50706,				// &DNG version number
		DNGBACKWARDVERSION=50707,		// &DNG compatibility version
		UNIQUECAMERAMODEL=50708,		// &name for the camera model
		LOCALIZEDCAMERAMODEL=50709,		// &localized camera model name
		CFAPLANECOLOR=50710,			// &CFAPattern=>LinearRaw space mapping
		CFALAYOUT=50711,				// &spatial layout of the CFA
		LINEARIZATIONTABLE=50712,		// &lookup table description
		BLACKLEVELREPEATDIM=50713,		// &repeat pattern size for the BlackLevel tag
		BLACKLEVEL=50714,				// &zero light encoding level
		BLACKLEVELDELTAH=50715,			// &zero light encoding level differences (columns)
		BLACKLEVELDELTAV=50716,			// &zero light encoding level differences (rows)
		WHITELEVEL=50717,				// &fully saturated encoding level
		DEFAULTSCALE=50718,				// &default scale factors
		DEFAULTCROPORIGIN=50719,		// &origin of the final image area
		DEFAULTCROPSIZE=50720,			// &size of the final image area
		COLORMATRIX1=50721,				// &XYZ=>reference color space transformation matrix 1
		COLORMATRIX2=50722,				// &XYZ=>reference color space transformation matrix 2
		CAMERACALIBRATION1=50723,		// &calibration matrix 1
		CAMERACALIBRATION2=50724,		// &calibration matrix 2
		REDUCTIONMATRIX1=50725,			// &dimensionality reduction matrix 1
		REDUCTIONMATRIX2=50726,			// &dimensionality reduction matrix 2
		ANALOGBALANCE=50727,			// &gain applied the stored raw values
		ASSHOTNEUTRAL=50728,			// &selected white balance in linear reference space
		ASSHOTWHITEXY=50729,			// &selected white balance in x-y chromaticity coordinates
		BASELINEEXPOSURE=50730,			// &how much to move the zero point
		BASELINENOISE=50731,			// &relative noise level
		BASELINESHARPNESS=50732,		// &relative amount of sharpening
		BAYERGREENSPLIT=50733,			// &how closely the values of the green pixels in the blue/green rows
										// track the values of the green pixels in the red/green rows
		LINEARRESPONSELIMIT=50734,		// &non-linear encoding range
		CAMERASERIALNUMBER=50735,		// &camera's serial number
		LENSINFO=50736,					// info about the lens
		CHROMABLURRADIUS=50737,			// &chroma blur radius
		ANTIALIASSTRENGTH=50738,		// &relative strength of the camera's anti-alias filter
		SHADOWSCALE=50739,				// &used by Adobe Camera Raw
		DNGPRIVATEDATA=50740,			// &manufacturer's private data
		MAKERNOTESAFETY=50741,			// &whether the EXIF MakerNote tag is safe to preserve
										// along with the rest of the EXIF data
		CALIBRATIONILLUMINANT1=50778,	// &illuminant 1
		CALIBRATIONILLUMINANT2=50779,	// &illuminant 2
		BESTQUALITYSCALE=50780,			// &best quality multiplier
		RAWDATAUNIQUEID=50781,			// &unique identifier for the raw image data
		ORIGINALRAWFILENAME=50827,		// &file name of the original raw file
		ORIGINALRAWFILEDATA=50828,		// &contents of the original raw file
		ACTIVEAREA=50829,				// &active (non-masked) pixels of the sensor
		MASKEDAREAS=50830,				// &list of coordinates of fully masked pixels
		ASSHOTICCPROFILE=50831,			// &these two tags used to
		ASSHOTPREPROFILEMATRIX=50832,	// map cameras's color space into ICC profile space
		CURRENTICCPROFILE=50833,		// &
		CURRENTPREPROFILEMATRIX=50834,	// &

		// tag 65535 is an undefined tag used by Eastman Kodak
		DCSHUESHIFTVALUES=65535,	// hue shift correction data

		// The following are "pseudo tags" that can be used to control
		// codec-specific functionality. These tags are not written to file.
		// Note that these values start at 0xffff+1 so that they'll never
		// collide with Aldus-assigned tags.
		//
		// If you want your private pseudo tags "registered" (i.e. added to
		// this file), please post a bug report via the tracking system at
		// http://www.remotesensing.org/libtiff/bugs.html with the appropriate
		// C definitions to add.

		FAXMODE=65536,				// Group 3/4 format control
		JPEGQUALITY=65537,			// Compression quality level (Note: quality level is on the IJG 0-100 scale. Default value is 75)
		JPEGCOLORMODE=65538,		// Auto RGB<=>YCbCr convert?
		JPEGTABLESMODE=65539,		// What to put in JPEGTables
		//FAXFILLFUNC=65540,		// G3/G4 fill function
		PIXARLOGDATAFMT=65549,		// PixarLogCodec I/O data sz

		// tags 65550-65556 are allocated to Oceana Matrix <dev@oceana.com>
		DCSIMAGERTYPE=65550,		// imager model & filter
		DCSINTERPMODE=65551,		// interpolation mode
		DCSBALANCEARRAY=65552,		// color balance values
		DCSCORRECTMATRIX=65553,		// color correction values
		DCSGAMMA=65554,				// gamma value
		DCSTOESHOULDERPTS=65555,	// toe & shoulder points
		DCSCALIBRATIONFD=65556,		// calibration file desc

		// Note: quality level is on the ZLIB 1-9 scale. Default value is -1
		ZIPQUALITY=65557,			// compression quality level
		PIXARLOGQUALITY=65558,		// PixarLog uses same scale

		// tag 65559 is allocated to Oceana Matrix <dev@oceana.com>
		DCSCLIPRECTANGLE=65559,		// area of image to acquire

		SGILOGDATAFMT=65560,		// SGILog user data format
		SGILOGENCODE=65561			// SGILog data encoding control
	}

	[Flags]
	public enum FILETYPE
	{
		REDUCEDIMAGE=0x1,	// reduced resolution version
		PAGE=0x2,			// one page of many
		MASK=0x4			// transparency mask
	}

	public enum OFILETYPE
	{
		IMAGE=1,		// full resolution image data
		REDUCEDIMAGE=2,	// reduced size image data
		PAGE=3			// one page of many
	}

	public enum COMPRESSION
	{
		NONE=1,				// dump mode
		CCITTRLE=2,			// CCITT modified Huffman RLE
		CCITTFAX3=3,		// CCITT Group 3 fax encoding
		CCITT_T4=3,			// CCITT T.4 (TIFF 6 name)
		CCITTFAX4=4,		// CCITT Group 4 fax encoding
		CCITT_T6=4,			// CCITT T.6 (TIFF 6 name)
		LZW=5,				// Lempel-Ziv & Welch
		OJPEG=6,			// !6.0 JPEG
		JPEG=7,				// %JPEG DCT compression
		ADOBE_DEFLATE=8,	// Deflate compression, as recognized by Adobe
		NEXT=32766,			// NeXT 2-bit RLE
		CCITTRLEW=32771,	// #1 w/ word alignment
		PACKBITS=32773,		// Macintosh RLE
		THUNDERSCAN=32809,	// ThunderScan RLE

		// codes 32895-32898 are reserved for ANSI IT8 TIFF/IT <dkelly@apago.com)
		IT8CTPAD=32895,		// IT8 CT w/padding
		IT8LW=32896,		// IT8 Linework RLE
		IT8MP=32897,		// IT8 Monochrome picture
		IT8BL=32898,		// IT8 Binary line art

		// compression codes 32908-32911 are reserved for Pixar
		PIXARFILM=32908,	// Pixar companded 10bit LZW
		PIXARLOG=32909,		// Pixar companded 11bit ZIP
		DEFLATE=32946,		// Deflate compression

		// compression code 32947 is reserved for Oceana Matrix <dev@oceana.com>
		DCS=32947,			// Kodak DCS encoding
		JBIG=34661,			// ISO JBIG
		SGILOG=34676,		// SGI Log Luminance RLE
		SGILOG24=34677,		// SGI Log 24-bit packed
		JP2000=34712		// Leadtools JPEG2000
	}

	public enum PHOTOMETRIC
	{
		MINISWHITE=0,	// min value is white
		MINISBLACK=1,	// min value is black
		RGB=2,			// RGB color model
		PALETTE=3,		// color map indexed
		MASK=4,			// $holdout mask
		SEPARATED=5,	// !color separations (CMYK)
		YCBCR=6,		// !CCIR 601
		CIELAB=8,		// !1976 CIE L*a*b*
		ICCLAB=9,		// ICC L*a*b* [Adobe TIFF Technote 4]
		ITULAB=10,		// ITU L*a*b*
		CFA=32803,		// Bayer Filter Raw Images
		LinearRaw=34892,
		LOGL=32844,		// CIE Log2(L)
		LOGLUV=32845	// CIE Log2(L) (u',v')
	}

	public enum THRESHHOLD
	{
		BILEVEL=1,		// b&w art scan
		HALFTONE=2,		// or dithered scan
		ERRORDIFFUSE=3	// usually floyd-steinberg
	}

	public enum FILLORDER
	{
		MSB2LSB=1,	// most significant => least
		LSB2MSB=2	// least significant => most
	}

	public enum ORIENTATION
	{
		TOPLEFT=1,	// row 0 top, col 0 lhs
		TOPRIGHT=2,	// row 0 top, col 0 rhs
		BOTRIGHT=3,	// row 0 bottom, col 0 rhs
		BOTLEFT=4,	// row 0 bottom, col 0 lhs
		LEFTTOP=5,	// row 0 lhs, col 0 top
		RIGHTTOP=6,	// row 0 rhs, col 0 top
		RIGHTBOT=7,	// row 0 rhs, col 0 bottom
		LEFTBOT=8	// row 0 lhs, col 0 bottom
	}

	public enum PLANARCONFIG
	{
		CONTIG=1,	// single image plane
		SEPARATE=2	// separate planes of data
	}

	public enum GRAYRESPONSEUNIT
	{
		_10S=1,		// tenths of a unit
		_100S=2,	// hundredths of a unit
		_1000S=3,	// thousandths of a unit
		_10000S=4,	// ten-thousandths of a unit
		_100000S=5	// hundred-thousandths
	}

	[Flags]
	public enum GROUP3OPT
	{
		_2DENCODING=0x1,	// 2-dimensional coding
		UNCOMPRESSED=0x2,	// data not compressed
		FILLBITS=0x4		// fill to byte boundary
	}

	public enum GROUP4OPT
	{
		UNCOMPRESSED=0x2	// data not compressed
	}

	public enum RESUNIT
	{
		NONE=1,			// no meaningful units
		INCH=2,			// english
		CENTIMETER=3	// metric
	}

	public enum COLORRESPONSEUNIT
	{
		_10S=1,		// tenths of a unit
		_100S=2,	// hundredths of a unit
		_1000S=3,	// thousandths of a unit
		_10000S=4,	// ten-thousandths of a unit
		_100000S=5	// hundred-thousandths
	}

	public enum PREDICTOR
	{
		NONE=1,			// no prediction scheme used
		HORIZONTAL=2,	// horizontal differencing
		FLOATINGPOINT=3	// floating point predictor
	}

	public enum CLEANFAXDATA
	{
		CLEAN=0,		// no errors detected
		REGENERATED=1,	// receiver regenerated lines
		UNCLEAN=2		// uncorrected errors exist
	}

	public enum INKSET
	{
		CMYK=1,		// !cyan-magenta-yellow-black color
		MULTIINK=2	// !multi-ink or hi-fi color
	}

	public enum EXTRASAMPLE
	{
		UNSPECIFIED=0,	// !unspecified data
		ASSOCALPHA=1,	// !associated alpha data
		UNASSALPHA=2	// !unassociated alpha data
	}

	public enum SAMPLEFORMAT
	{
		UINT=1,			// !unsigned integer data
		INT=2,			// !signed integer data
		IEEEFP=3,		// !IEEE floating point data
		VOID=4,			// !untyped data
		COMPLEXINT=5,	// !complex signed int
		COMPLEXIEEEFP=6	// !complex ieee floating
	}

	public enum JPEGPROC
	{
		BASELINE=1,	// !baseline sequential
		LOSSLESS=14	// !Huffman coded lossless
	}

	public enum YCBCRPOSITION
	{
		CENTERED=1,	// !as in PostScript Level 2
		COSITED=2	// !as in CCIR 601-1
	}

	[Flags]
	public enum FAXMODE
	{
		CLASSIC=0x0000,		// default, include RTC
		NORTC=0x0001,		// no RTC at end of data
		NOEOL=0x0002,		// no EOL code at end of row
		BYTEALIGN=0x0004,	// byte align row
		WORDALIGN=0x0008,	// word align row
		CLASSF=NORTC		// TIFF Class F
	}

	public enum JPEGCOLORMODE
	{
		RAW=0x0000,	// no conversion (default)
		RGB=0x0001	// do auto conversion
	}

	[Flags]
	public enum JPEGTABLESMODE
	{
		QUANT=0x0001,		// include quantization tbls
		HUFF=0x0002,		// include Huffman tbls
		// Note: default is JPEGTABLESMODE_QUANT | JPEGTABLESMODE_HUFF
	}

	public enum PIXARLOGDATAFMT
	{
		_8BIT=0,		// regular u_char samples
		_8BITABGR=1,	// ABGR-order u_chars
		_11BITLOG=2,	// 11-bit log-encoded (raw)
		_12BITPICIO=3,	// as per PICIO (1.0==2048)
		_16BIT=4,		// signed short samples
		FLOAT=5			// IEEE float samples
	}

	public enum DCSIMAGERMODEL
	{
		M3=0,	// M3 chip (1280 x 1024)
		M5=1,	// M5 chip (1536 x 1024)
		M6=2	// M6 chip (3072 x 2048)
	}

	public enum DCSIMAGERFILTER
	{
		IR=0,		// infrared filter
		MONO=1,		// monochrome filter
		CFA=2,		// color filter array
		OTHER=3		// other filter
	}

	public enum DCSINTERPMODE
	{
		NORMAL=0x0,	// whole image, default
		PREVIEW=0x1	// preview of image (384x256)
	}

	public enum SGILOGDATAFMT
	{
		FLOAT=0,	// IEEE float samples
		_16BIT=1,	// 16-bit samples
		RAW=2,		// uninterpreted data
		_8BIT=3		// 8-bit RGB monitor values
	}

	public enum SGILOGENCODE
	{
		NODITHER=0,	// do not dither encoded values
		RANDITHER=1	// randomly dither encd values
	}

	// EXIF tags
	public enum EXIFTAG
	{
		EXPOSURETIME=33434,				// Exposure time
		FNUMBER=33437,					// F number
		EXPOSUREPROGRAM=34850,			// Exposure program
		SPECTRALSENSITIVITY=34852,		// Spectral sensitivity
		ISOSPEEDRATINGS=34855,			// ISO speed rating
		OECF=34856,						// Optoelectric conversion factor
		EXIFVERSION=36864,				// Exif version
		DATETIMEORIGINAL=36867,			// Date and time of original data generation
		DATETIMEDIGITIZED=36868,		// Date and time of digital data generation
		COMPONENTSCONFIGURATION=37121,	// Meaning of each component
		COMPRESSEDBITSPERPIXEL=37122,	// Image compression mode
		SHUTTERSPEEDVALUE=37377,		// Shutter speed
		APERTUREVALUE=37378,			// Aperture
		BRIGHTNESSVALUE=37379,			// Brightness
		EXPOSUREBIASVALUE=37380,		// Exposure bias
		MAXAPERTUREVALUE=37381,			// Maximum lens aperture
		SUBJECTDISTANCE=37382,			// Subject distance
		METERINGMODE=37383,				// Metering mode
		LIGHTSOURCE=37384,				// Light source
		FLASH=37385,					// Flash
		FOCALLENGTH=37386,				// Lens focal length
		SUBJECTAREA=37396,				// Subject area
		MAKERNOTE=37500,				// Manufacturer notes
		USERCOMMENT=37510,				// User comments
		SUBSECTIME=37520,				// DateTime subseconds
		SUBSECTIMEORIGINAL=37521,		// DateTimeOriginal subseconds
		SUBSECTIMEDIGITIZED=37522,		// DateTimeDigitized subseconds
		FLASHPIXVERSION=40960,			// Supported Flashpix version
		COLORSPACE=40961,				// Color space information
		PIXELXDIMENSION=40962,			// Valid image width
		PIXELYDIMENSION=40963,			// Valid image height
		RELATEDSOUNDFILE=40964,			// Related audio file
		FLASHENERGY=41483,				// Flash energy
		SPATIALFREQUENCYRESPONSE=41484,	// Spatial frequency response
		FOCALPLANEXRESOLUTION=41486,	// Focal plane X resolution
		FOCALPLANEYRESOLUTION=41487,	// Focal plane Y resolution
		FOCALPLANERESOLUTIONUNIT=41488,	// Focal plane resolution unit
		SUBJECTLOCATION=41492,			// Subject location
		EXPOSUREINDEX=41493,			// Exposure index
		SENSINGMETHOD=41495,			// Sensing method
		FILESOURCE=41728,				// File source
		SCENETYPE=41729,				// Scene type
		CFAPATTERN=41730,				// CFA pattern
		CUSTOMRENDERED=41985,			// Custom image processing
		EXPOSUREMODE=41986,				// Exposure mode
		WHITEBALANCE=41987,				// White balance
		DIGITALZOOMRATIO=41988,			// Digital zoom ratio
		FOCALLENGTHIN35MMFILM=41989,	// Focal length in 35 mm film
		SCENECAPTURETYPE=41990,			// Scene capture type
		GAINCONTROL=41991,				// Gain control
		CONTRAST=41992,					// Contrast
		SATURATION=41993,				// Saturation
		SHARPNESS=41994,				// Sharpness
		DEVICESETTINGDESCRIPTION=41995,	// Device settings description
		SUBJECTDISTANCERANGE=41996,		// Subject distance range
		IMAGEUNIQUEID=42016				// Unique image ID
	}
}
