// uvcode.cs
//
// Based on Version 1.0 generated April 7, 1997 by Greg Ward Larson, SGI
// Copyright (c) 2006-2010 by the Authors

namespace Free.Ports.LibTiff
{
	public static partial class libtiff
	{
		struct UV_ROW
		{
			internal float ustart;
			internal short nus, ncum;

			internal UV_ROW(float ustart, short nus, short ncum)
			{
				this.ustart=ustart;
				this.nus=nus;
				this.ncum=ncum;
			}
		}

		const float UV_SQSIZ=0.003500f;
		const short UV_NDIVS=16289;
		const float UV_VSTART=0.016940f;
		const short UV_NVS=163;

		static readonly UV_ROW[] uv_row=new UV_ROW[UV_NVS]
		{
			new UV_ROW(0.247663f, 4, 0),
			new UV_ROW(0.243779f, 6, 4),
			new UV_ROW(0.241684f, 7, 10),
			new UV_ROW(0.237874f, 9, 17),
			new UV_ROW(0.235906f, 10, 26),
			new UV_ROW(0.232153f, 12, 36),
			new UV_ROW(0.228352f, 14, 48),
			new UV_ROW(0.226259f, 15, 62),
			new UV_ROW(0.222371f, 17, 77),
			new UV_ROW(0.220410f, 18, 94),
			new UV_ROW(0.214710f, 21, 112),
			new UV_ROW(0.212714f, 22, 133),
			new UV_ROW(0.210721f, 23, 155),
			new UV_ROW(0.204976f, 26, 178),
			new UV_ROW(0.202986f, 27, 204),
			new UV_ROW(0.199245f, 29, 231),
			new UV_ROW(0.195525f, 31, 260),
			new UV_ROW(0.193560f, 32, 291),
			new UV_ROW(0.189878f, 34, 323),
			new UV_ROW(0.186216f, 36, 357),
			new UV_ROW(0.186216f, 36, 393),
			new UV_ROW(0.182592f, 38, 429),
			new UV_ROW(0.179003f, 40, 467),
			new UV_ROW(0.175466f, 42, 507),
			new UV_ROW(0.172001f, 44, 549),
			new UV_ROW(0.172001f, 44, 593),
			new UV_ROW(0.168612f, 46, 637),
			new UV_ROW(0.168612f, 46, 683),
			new UV_ROW(0.163575f, 49, 729),
			new UV_ROW(0.158642f, 52, 778),
			new UV_ROW(0.158642f, 52, 830),
			new UV_ROW(0.158642f, 52, 882),
			new UV_ROW(0.153815f, 55, 934),
			new UV_ROW(0.153815f, 55, 989),
			new UV_ROW(0.149097f, 58, 1044),
			new UV_ROW(0.149097f, 58, 1102),
			new UV_ROW(0.142746f, 62, 1160),
			new UV_ROW(0.142746f, 62, 1222),
			new UV_ROW(0.142746f, 62, 1284),
			new UV_ROW(0.138270f, 65, 1346),
			new UV_ROW(0.138270f, 65, 1411),
			new UV_ROW(0.138270f, 65, 1476),
			new UV_ROW(0.132166f, 69, 1541),
			new UV_ROW(0.132166f, 69, 1610),
			new UV_ROW(0.126204f, 73, 1679),
			new UV_ROW(0.126204f, 73, 1752),
			new UV_ROW(0.126204f, 73, 1825),
			new UV_ROW(0.120381f, 77, 1898),
			new UV_ROW(0.120381f, 77, 1975),
			new UV_ROW(0.120381f, 77, 2052),
			new UV_ROW(0.120381f, 77, 2129),
			new UV_ROW(0.112962f, 82, 2206),
			new UV_ROW(0.112962f, 82, 2288),
			new UV_ROW(0.112962f, 82, 2370),
			new UV_ROW(0.107450f, 86, 2452),
			new UV_ROW(0.107450f, 86, 2538),
			new UV_ROW(0.107450f, 86, 2624),
			new UV_ROW(0.107450f, 86, 2710),
			new UV_ROW(0.100343f, 91, 2796),
			new UV_ROW(0.100343f, 91, 2887),
			new UV_ROW(0.100343f, 91, 2978),
			new UV_ROW(0.095126f, 95, 3069),
			new UV_ROW(0.095126f, 95, 3164),
			new UV_ROW(0.095126f, 95, 3259),
			new UV_ROW(0.095126f, 95, 3354),
			new UV_ROW(0.088276f, 100, 3449),
			new UV_ROW(0.088276f, 100, 3549),
			new UV_ROW(0.088276f, 100, 3649),
			new UV_ROW(0.088276f, 100, 3749),
			new UV_ROW(0.081523f, 105, 3849),
			new UV_ROW(0.081523f, 105, 3954),
			new UV_ROW(0.081523f, 105, 4059),
			new UV_ROW(0.081523f, 105, 4164),
			new UV_ROW(0.074861f, 110, 4269),
			new UV_ROW(0.074861f, 110, 4379),
			new UV_ROW(0.074861f, 110, 4489),
			new UV_ROW(0.074861f, 110, 4599),
			new UV_ROW(0.068290f, 115, 4709),
			new UV_ROW(0.068290f, 115, 4824),
			new UV_ROW(0.068290f, 115, 4939),
			new UV_ROW(0.068290f, 115, 5054),
			new UV_ROW(0.063573f, 119, 5169),
			new UV_ROW(0.063573f, 119, 5288),
			new UV_ROW(0.063573f, 119, 5407),
			new UV_ROW(0.063573f, 119, 5526),
			new UV_ROW(0.057219f, 124, 5645),
			new UV_ROW(0.057219f, 124, 5769),
			new UV_ROW(0.057219f, 124, 5893),
			new UV_ROW(0.057219f, 124, 6017),
			new UV_ROW(0.050985f, 129, 6141),
			new UV_ROW(0.050985f, 129, 6270),
			new UV_ROW(0.050985f, 129, 6399),
			new UV_ROW(0.050985f, 129, 6528),
			new UV_ROW(0.050985f, 129, 6657),
			new UV_ROW(0.044859f, 134, 6786),
			new UV_ROW(0.044859f, 134, 6920),
			new UV_ROW(0.044859f, 134, 7054),
			new UV_ROW(0.044859f, 134, 7188),
			new UV_ROW(0.040571f, 138, 7322),
			new UV_ROW(0.040571f, 138, 7460),
			new UV_ROW(0.040571f, 138, 7598),
			new UV_ROW(0.040571f, 138, 7736),
			new UV_ROW(0.036339f, 142, 7874),
			new UV_ROW(0.036339f, 142, 8016),
			new UV_ROW(0.036339f, 142, 8158),
			new UV_ROW(0.036339f, 142, 8300),
			new UV_ROW(0.032139f, 146, 8442),
			new UV_ROW(0.032139f, 146, 8588),
			new UV_ROW(0.032139f, 146, 8734),
			new UV_ROW(0.032139f, 146, 8880),
			new UV_ROW(0.027947f, 150, 9026),
			new UV_ROW(0.027947f, 150, 9176),
			new UV_ROW(0.027947f, 150, 9326),
			new UV_ROW(0.023739f, 154, 9476),
			new UV_ROW(0.023739f, 154, 9630),
			new UV_ROW(0.023739f, 154, 9784),
			new UV_ROW(0.023739f, 154, 9938),
			new UV_ROW(0.019504f, 158, 10092),
			new UV_ROW(0.019504f, 158, 10250),
			new UV_ROW(0.019504f, 158, 10408),
			new UV_ROW(0.016976f, 161, 10566),
			new UV_ROW(0.016976f, 161, 10727),
			new UV_ROW(0.016976f, 161, 10888),
			new UV_ROW(0.016976f, 161, 11049),
			new UV_ROW(0.012639f, 165, 11210),
			new UV_ROW(0.012639f, 165, 11375),
			new UV_ROW(0.012639f, 165, 11540),
			new UV_ROW(0.009991f, 168, 11705),
			new UV_ROW(0.009991f, 168, 11873),
			new UV_ROW(0.009991f, 168, 12041),
			new UV_ROW(0.009016f, 170, 12209),
			new UV_ROW(0.009016f, 170, 12379),
			new UV_ROW(0.009016f, 170, 12549),
			new UV_ROW(0.006217f, 173, 12719),
			new UV_ROW(0.006217f, 173, 12892),
			new UV_ROW(0.005097f, 175, 13065),
			new UV_ROW(0.005097f, 175, 13240),
			new UV_ROW(0.005097f, 175, 13415),
			new UV_ROW(0.003909f, 177, 13590),
			new UV_ROW(0.003909f, 177, 13767),
			new UV_ROW(0.002340f, 177, 13944),
			new UV_ROW(0.002389f, 170, 14121),
			new UV_ROW(0.001068f, 164, 14291),
			new UV_ROW(0.001653f, 157, 14455),
			new UV_ROW(0.000717f, 150, 14612),
			new UV_ROW(0.001614f, 143, 14762),
			new UV_ROW(0.000270f, 136, 14905),
			new UV_ROW(0.000484f, 129, 15041),
			new UV_ROW(0.001103f, 123, 15170),
			new UV_ROW(0.001242f, 115, 15293),
			new UV_ROW(0.001188f, 109, 15408),
			new UV_ROW(0.001011f, 103, 15517),
			new UV_ROW(0.000709f, 97, 15620),
			new UV_ROW(0.000301f, 89, 15717),
			new UV_ROW(0.002416f, 82, 15806),
			new UV_ROW(0.003251f, 76, 15888),
			new UV_ROW(0.003246f, 69, 15964),
			new UV_ROW(0.004141f, 62, 16033),
			new UV_ROW(0.005963f, 55, 16095),
			new UV_ROW(0.008839f, 47, 16150),
			new UV_ROW(0.010490f, 40, 16197),
			new UV_ROW(0.016994f, 31, 16237),
			new UV_ROW(0.023659f, 21, 16268)
		};
	}
}